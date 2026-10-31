using System;

namespace Megastructure.Audio
{
    /// <summary>
    /// The procedural score renderer (MASTER_PROMPT 14.3). Unity-free: it runs on the audio thread inside
    /// OnAudioFilterRead, in EditMode tests and in offline renders with the same code and the same results.
    ///
    /// Signal flow per 32-sample control block:
    /// <code>
    ///  snapshot (AudioParamsExchange, extrapolated clocks) -> events (dead-centre pulses, rail ticks, seeded GROAN/BELL,
    ///  door thud, queued footsteps) -> 50 ms one-pole gain smoothing + LFOs -> layers and voices into the AudioBus:
    ///    faded bus:   SUB HUM AIR FAN AIRFLOW LIFT(rumble, whine, ticks) WIND(+whistle) PULSE GROAN BELL footsteps
    ///    unfaded bus: door thud (walk real time 0.8 s, once per run), ending 31 Hz tone
    ///  reverb in  = faded send * MasterFade + unfaded send          (pre-delay -> 8-line FDN, presets blended over 4 s)
    ///  mix        = ((faded dry * MasterFade + reverb * wet * tail) * trim + unfaded dry) * duck
    ///  out        = SoftLimiter(DcBlocker8Hz(mix))                   (ceiling -3 dBFS)
    /// </code>
    /// Threading: exactly one thread renders (the audio thread, or the caller offline). The main thread only calls
    /// <see cref="Exchange"/>.Publish and enqueues footsteps through AudioEvents. Nothing in the render path allocates,
    /// locks or touches UnityEngine.
    /// </summary>
    public sealed class ProceduralAudioEngine
    {
        public const int ControlBlock = AudioBus.Size;
        const double DefaultTau = 0.05;           // 14.3: parameters smoothed by a 50 ms one-pole
        const double DuckTau = 0.25;              // pause duck, smooth
        const double TrimTau = 1.0;
        const double MaxExtrapolation = 0.25;     // seconds a snapshot may be extrapolated before the clocks hold
        const uint CalibrationSeed = 0xC0FFEEu;
        const uint ThudSeed = 0x7A0D7A0Du;        // the start thud is identical every run
        static readonly double ThudGain = Math.Pow(10.0, AudioMix.ThudPeakDb / 20.0);

        // gain slots
        const int GSub = 0, GAir = 1, GHum = 2, GPulse = 3, GGroan = 4, GGroanDry = 5, GBell = 6, GRumble = 7, GWhine = 8,
                  GTick = 9, GFan = 10, GAirflow = 11, GWind = 12, GFoot = 13, GEndTone = 14, GMaster = 15, GTail = 16,
                  GTrim = 17, GDuck = 18, GCount = 19;

        public readonly int SampleRate;
        public readonly uint Seed;
        /// <summary>Main thread -> audio thread parameter snapshots.</summary>
        public readonly AudioParamsExchange Exchange = new AudioParamsExchange();
        /// <summary>When true (player), footsteps are drained from <see cref="AudioEvents"/>; offline renders call TriggerFootstep.</summary>
        public bool DrainAudioEvents;

        // diagnostics (written by the render thread, read anywhere)
        public long SamplesRendered;
        public int SnapshotsReceived;
        public int PulsesFired, TicksFired, GroansFired, BellsFired, FootstepsPlayed, ThudsFired;
        public float PeakAbs;

        readonly double _fs, _ctrlDt;
        readonly AudioBus _bus = new AudioBus();
        readonly float[] _outL = new float[ControlBlock], _outR = new float[ControlBlock];
        int _outPos = ControlBlock;

        AudioParams _p;
        bool _has;
        long _sinceSnap;

        // layers
        readonly SubLayer _sub;
        readonly HumLayer _hum;
        readonly NoiseBed _air, _fan, _airflow, _rumble, _wind, _whistle;
        readonly ToneLayer _whine, _endTone;
        readonly double _fanSwellRms, _airflowSwellRms, _windGustRms;

        // voices
        readonly PulseVoice[] _pulses;
        readonly GroanVoice[] _groans;
        readonly BellVoice[] _bells;
        readonly BurstVoice[] _ticks;
        readonly BurstVoice _thud;
        readonly FootstepVoice[] _steps;
        readonly double[] _stepCompNorm = new double[FootstepVoice.SurfaceCount * 3];
        readonly double[] _stepTotalNorm = new double[FootstepVoice.SurfaceCount];
        int _rrPulse, _rrGroan, _rrBell, _rrTick, _rrStep;

        // gains
        readonly double[] _g = new double[GCount], _gT = new double[GCount], _gA = new double[GCount], _gPrev = new double[GCount];
        readonly double[] _eff = new double[GCount];   // previous effective (modulated) layer gain, ramp start of the next block

        // reverb and master
        readonly FdnReverb _fdn;
        readonly DcBlocker _dcL, _dcR;
        double _rvRt, _rvPre, _rvDamp, _rvWet, _rvWetLinPrev;
        double _rvFromRt, _rvFromPre, _rvFromDamp, _rvFromWet;
        float _rvToRt = -1f, _rvToPre, _rvToDamp, _rvToWet;
        double _rvProg = 1.0, _rvDur = 4.0;

        // events
        long _pulseIdx = long.MinValue, _tickIdx = long.MinValue;
        double _groanNext = double.NaN, _bellNext = double.NaN, _lastSceneT;
        Xorshift32 _groanRng, _bellRng, _footRng;
        double _thudHigh = double.NaN;    // high-water mark of the walk's real time (door thud crossing detection)
        bool _stairToggle, _footSide;

        double _lfoT;

        public ProceduralAudioEngine(int sampleRate, uint seed)
        {
            if (sampleRate < 8000) sampleRate = 48000;
            SampleRate = sampleRate;
            Seed = seed;
            _fs = sampleRate;
            _ctrlDt = ControlBlock / _fs;
            double ctrlRate = _fs / ControlBlock;

            // ---- beds (14.3 table)
            _sub = new SubLayer(_fs);
            _hum = new HumLayer(_fs);
            _air = new NoiseBed(NoiseColour.Brown, DspMath.MixSeed(seed, 10u), SvfMode.BandPassNormalized, AirCentre, 0.8, 0.0, 0.4, 0.6f, _fs);
            _fan = new NoiseBed(NoiseColour.White, DspMath.MixSeed(seed, 11u), SvfMode.LowPass, 70.0, 0.707, 20.0, 0.4, 0.5f, _fs);
            _airflow = new NoiseBed(NoiseColour.Pink, DspMath.MixSeed(seed, 12u), SvfMode.BandPassNormalized, Math.Sqrt(200.0 * 900.0), Math.Sqrt(200.0 * 900.0) / 700.0, 0.0, 0.45, 0.6f, _fs);
            _rumble = new NoiseBed(NoiseColour.White, DspMath.MixSeed(seed, 13u), SvfMode.LowPass, 120.0, 0.6, 25.0, 0.3, 0.4f, _fs);
            _wind = new NoiseBed(NoiseColour.Pink, DspMath.MixSeed(seed, 14u), SvfMode.BandPassNormalized, WindCentre, 0.6, 0.0, 0.5, 0.35f, _fs);
            _whistle = new NoiseBed(NoiseColour.White, DspMath.MixSeed(seed, 15u), SvfMode.BandPassNormalized, WhistleHz, 30.0, 0.0, 0.2, 0.5f, _fs);
            _whine = new ToneLayer(WhineBaseHz, 0.3, 0.15, 0.5f, false, _fs);
            _endTone = new ToneLayer(SubLayer.F1, 0.0, 0.0, 0f, true, _fs);
            _fanSwellRms = SwellRms(FanSwellDb);
            _airflowSwellRms = SwellRms(AirflowSwellDb);
            _windGustRms = Math.Sqrt(GustMean * GustMean + 0.5 * (GustA * GustA + GustB * GustB));

            // ---- voices, each type calibrated to a unit dry peak
            _pulses = new PulseVoice[2];
            for (int i = 0; i < _pulses.Length; i++) _pulses[i] = new PulseVoice(_fs);
            _pulses[0].Trigger(CalibrationSeed, 0.0);
            double pulseNorm = _pulses[0].MeasureInversePeak((int)(4 * _fs));
            for (int i = 0; i < _pulses.Length; i++) _pulses[i].Norm = pulseNorm;

            _groans = new GroanVoice[2];
            for (int i = 0; i < _groans.Length; i++) _groans[i] = new GroanVoice(_fs);
            _groans[0].Trigger(CalibrationSeed);
            double groanNorm = _groans[0].MeasureInversePeak((int)(7 * _fs));
            for (int i = 0; i < _groans.Length; i++) _groans[i].Norm = groanNorm;

            _bells = new BellVoice[2];
            for (int i = 0; i < _bells.Length; i++) _bells[i] = new BellVoice(_fs);
            _bells[0].Trigger(CalibrationSeed);
            double bellNorm = _bells[0].MeasureInversePeak((int)(7 * _fs));
            for (int i = 0; i < _bells.Length; i++) _bells[i].Norm = bellNorm;

            _ticks = new BurstVoice[3];
            for (int i = 0; i < _ticks.Length; i++)
                _ticks[i] = new BurstVoice(_fs, SvfMode.BandPassNormalized, 800.0, 3.0, 0.001, 0.025, 1.0, 0.0, 0.0, 0.0, 0.0) { Send = 1.0f };
            _ticks[0].Trigger(CalibrationSeed, 0.0);
            double tickNorm = _ticks[0].MeasureInversePeak((int)(0.5 * _fs));
            for (int i = 0; i < _ticks.Length; i++) _ticks[i].Norm = tickNorm;

            _thud = new BurstVoice(_fs, SvfMode.LowPass, 400.0, 0.7, 0.002, 0.25, 0.6, 30.0, 0.005, 0.5, 1.0) { Unfaded = true, Send = 1.0f };
            _thud.Trigger(ThudSeed, 0.0);
            _thud.Norm = _thud.MeasureInversePeak((int)(2 * _fs));

            _steps = new FootstepVoice[6];   // 6 x 0.35 s of history: a burst of queued steps never steals a sounding voice
            for (int i = 0; i < _steps.Length; i++) _steps[i] = new FootstepVoice(_fs);
            for (int s = 0; s < FootstepVoice.SurfaceCount; s++)
            {
                for (int c = 0; c < 3; c++)
                {
                    _steps[0].Trigger((SurfaceKind)s, 1.0, CalibrationSeed, 0.0, 1 << c, null);
                    double inv = _steps[0].MeasureInversePeak((int)(0.5 * _fs));
                    _stepCompNorm[s * 3 + c] = inv > 1e9 ? 0.0 : inv;
                }
                _steps[0].Trigger((SurfaceKind)s, 1.0, CalibrationSeed, 0.0, 7, _stepCompNorm);
                _stepTotalNorm[s] = _steps[0].MeasureInversePeak((int)(0.5 * _fs));
            }

            // ---- reverb and master
            _fdn = new FdnReverb(_fs);
            _dcL = new DcBlocker(8.0, _fs);
            _dcR = new DcBlocker(8.0, _fs);

            for (int i = 0; i < GCount; i++) _gA[i] = DspMath.OnePoleCoef(DefaultTau, ctrlRate);
            _gA[GDuck] = DspMath.OnePoleCoef(DuckTau, ctrlRate);
            _gA[GTrim] = DspMath.OnePoleCoef(TrimTau, ctrlRate);
            _gT[GDuck] = _g[GDuck] = 1.0;
            _gT[GTrim] = _g[GTrim] = 1.0;
            _gT[GTail] = _g[GTail] = 1.0;

            _footRng = new Xorshift32(DspMath.MixSeed(seed, 21u));
        }

        // ------------------------------------------------------------------ synthesis constants (14.3)
        /// <summary>AIR band 120-600 Hz: geometric centre, swept +/-35 % by a 0.07 Hz LFO.</summary>
        public static readonly double AirCentre = Math.Sqrt(120.0 * 600.0);
        public const double AirSweepHz = 0.07, AirSweepDepth = 0.35;
        /// <summary>WIND band 300-1200 Hz (geometric centre, Q 0.6) with gust LFOs 0.04 and 0.11 Hz.</summary>
        public static readonly double WindCentre = Math.Sqrt(300.0 * 1200.0);
        public const double GustAHz = 0.04, GustBHz = 0.11, GustMean = 0.75, GustA = 0.15, GustB = 0.10;
        public const double WhistleHz = 2800.0;
        public const double FanSwellDb = 4.0, AirflowSwellDb = 1.5;
        public const double WhineBaseHz = 140.0, WhineSpanHz = 60.0;

        static double SwellRms(double db)
        {
            double s = 0.0;
            const int M = 1024;
            for (int i = 0; i < M; i++) { double g = Math.Pow(10.0, db / 20.0 * Math.Sin(DspMath.TwoPi * i / M)); s += g * g; }
            return Math.Sqrt(s / M);
        }

        // ------------------------------------------------------------------ public render API

        /// <summary>Render planar stereo. Call from one thread only (the audio thread, or the offline renderer).</summary>
        public void Render(float[] left, float[] right, int offset, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                if (_outPos >= ControlBlock) { RenderBlock(); _outPos = 0; }
                left[offset + i] = _outL[_outPos];
                right[offset + i] = _outR[_outPos];
                _outPos++;
            }
            SamplesRendered += frames;
        }

        /// <summary>Render interleaved stereo frames into buf starting at frame offset (offline renders).</summary>
        public void RenderInterleavedStereo(float[] buf, int frameOffset, int frames)
        {
            int j = frameOffset * 2;
            for (int i = 0; i < frames; i++)
            {
                if (_outPos >= ControlBlock) { RenderBlock(); _outPos = 0; }
                buf[j++] = _outL[_outPos];
                buf[j++] = _outR[_outPos];
                _outPos++;
            }
            SamplesRendered += frames;
        }

        /// <summary>
        /// Fill an OnAudioFilterRead buffer: channel 0 = L, 1 = R, any further channels 0; a mono device gets (L + R) / 2.
        /// </summary>
        public void RenderInterleaved(float[] data, int channels)
        {
            if (channels <= 0) return;
            int frames = data.Length / channels;
            int j = 0;
            for (int i = 0; i < frames; i++)
            {
                if (_outPos >= ControlBlock) { RenderBlock(); _outPos = 0; }
                float l = _outL[_outPos], r = _outR[_outPos];
                _outPos++;
                if (channels == 1) { data[j++] = 0.5f * (l + r); continue; }
                data[j] = l;
                data[j + 1] = r;
                for (int c = 2; c < channels; c++) data[j + c] = 0f;
                j += channels;
            }
            SamplesRendered += frames;
        }

        /// <summary>
        /// Play one footstep now (render thread only; the player path goes through AudioEvents.Footstep instead).
        /// Pitch +/-6 %, level +/-1.5 dB from the seeded footstep stream; stairs alternate 0 / -1 semitone at -31 dBFS.
        /// </summary>
        public void TriggerFootstep(SurfaceKind kind)
        {
            int s = (int)kind;
            if (s < 0 || s >= FootstepVoice.SurfaceCount) s = 0;
            double pitch = 1.0 + AudioMix.FootstepPitchJitter * (2.0 * _footRng.NextUnit() - 1.0);
            double levelDb = AudioMix.FootstepLevelJitterDb * (2.0 * _footRng.NextUnit() - 1.0);
            uint noiseSeed = _footRng.NextUInt();
            double peakDb = AudioMix.FootstepPeakDb;
            if (kind == SurfaceKind.Stair)
            {
                if (_stairToggle) pitch *= 0.9438743126816935; // -1 semitone
                _stairToggle = !_stairToggle;
                peakDb = AudioMix.StairStepPeakDb;
            }
            _footSide = !_footSide;
            FootstepVoice v = _steps[_rrStep];
            _rrStep = (_rrStep + 1) % _steps.Length;
            v.Trigger(kind, pitch, noiseSeed, _footSide ? -0.06 : 0.06, 7, _stepCompNorm);
            v.Norm = Math.Pow(10.0, (peakDb + levelDb) / 20.0) * _stepTotalNorm[s];
            FootstepsPlayed++;
        }

        /// <summary>Bring every smoothed value and the reverb straight to the current targets (offline window renders).</summary>
        public void SnapToTargets()
        {
            if (!_has) return;
            ApplyTargets();
            for (int i = 0; i < GCount; i++) { _g[i] = _gT[i]; _gPrev[i] = _gT[i]; }
            _rvToRt = _p.RevRt60; _rvToPre = _p.RevPreDelay; _rvToDamp = _p.RevDamping; _rvToWet = _p.RevWetDb;
            _rvRt = _rvToRt; _rvPre = _rvToPre; _rvDamp = _rvToDamp; _rvWet = _rvToWet; _rvProg = 1.0;
            _rvWetLinPrev = Math.Pow(10.0, _rvWet / 20.0);
            _fdn.SetParams(_rvRt, _rvDamp, _rvPre, true);
            for (int i = 0; i < GCount; i++) _eff[i] = -1.0; // re-derive the modulated ramp starts on the next block
        }

        // ------------------------------------------------------------------ block processing

        void RenderBlock()
        {
            const int n = ControlBlock;
            bool first = false;
            if (Exchange.TryAcquire(ref _p))
            {
                _sinceSnap = 0;
                SnapshotsReceived++;
                if (!_has) { _has = true; first = true; }
            }
            _bus.Clear();
            if (!_has)
            {
                Array.Clear(_outL, 0, n); Array.Clear(_outR, 0, n);
                return;
            }
            if (first)
            {
                SnapToTargets();
                if (DrainAudioEvents) AudioEvents.DrainFootsteps();
            }

            // ---- clocks extrapolated from the snapshot
            double dt = Math.Min(_sinceSnap / _fs, MaxExtrapolation);
            double sceneT = _p.SceneTime + _p.ClockRate * dt;
            double realT = _p.RealTime + dt;
            double crank = _p.CrankTheta + _p.CrankRate * dt;
            double fanPh = _p.FanPhase + _p.FanRate * dt + AudioMix.FanSwellPhaseCycles;
            double liftS = _p.LiftS + _p.LiftV * dt;
            _sinceSnap += n;
            _lfoT += _ctrlDt;

            // ---- targets and smoothing
            ApplyTargets();
            for (int i = 0; i < GCount; i++)
            {
                _gPrev[i] = _g[i];
                double t = _gT[i];
                double v = t + (_g[i] - t) * _gA[i];
                if (t == 0.0 && v < 1e-6 && v > -1e-6) v = 0.0;
                _g[i] = v;
            }

            // ---- events
            Events(sceneT, realT, crank, liftS);

            // ---- reverb preset blend (4 s, wet limited to 6 dB/s)
            ReverbControl();

            // ---- beds (gain ramps include the LFO modulation)
            double t0 = _lfoT;
            _sub.Control(t0);
            RenderBed(GSub, _g[GSub] * SubLayer.AmFactor(t0), 0);
            RenderBed(GHum, _g[GHum] * HumLayer.Wobble(t0), 1);
            double sweep = 1.0 + AirSweepDepth * Math.Sin(DspMath.TwoPi * AirSweepHz * t0);
            if (_g[GAir] > 0.0 || _gPrev[GAir] > 0.0) _air.SetCutoff(AirCentre * sweep);
            RenderBed(GAir, _g[GAir] * Math.Sqrt(sweep), 2);  // brown noise: band power ~ 1 / fc, keep the level constant
            double swell = Math.Pow(10.0, FanSwellDb / 20.0 * Math.Sin(DspMath.TwoPi * fanPh)) / _fanSwellRms;
            RenderBed(GFan, _g[GFan] * swell, 3);
            double swellA = Math.Pow(10.0, AirflowSwellDb / 20.0 * Math.Sin(DspMath.TwoPi * fanPh + 1.0)) / _airflowSwellRms;
            RenderBed(GAirflow, _g[GAirflow] * swellA, 4);
            RenderBed(GRumble, _g[GRumble], 5);
            double gust = GustMean + GustA * Math.Sin(DspMath.TwoPi * GustAHz * t0) + GustB * Math.Sin(DspMath.TwoPi * GustBHz * t0 + 1.3);
            if (_g[GWind] > 0.0 || _gPrev[GWind] > 0.0)
            {
                double gr = (gust - GustMean) / (GustA + GustB);
                _wind.SetCutoff(WindCentre * (1.0 + 0.12 * gr));
                _whistle.SetCutoff(WhistleHz * (1.0 + 0.03 * gr));
            }
            RenderBed(GWind, _g[GWind] * gust / _windGustRms, 6);
            double wr = gust / GustMean;
            RenderBedWhistle(_g[GWind] * Math.Pow(10.0, (AudioMix.WhistleDb - AudioMix.WindDb) / 20.0) * wr * wr);
            _whine.SetFrequency(WhineBaseHz + WhineSpanHz * Math.Max(0.0, _p.LiftV) / AbyssConst.LIFT_CRUISE);
            RenderBed(GWhine, _g[GWhine], 7);
            RenderBed(GEndTone, _g[GEndTone], 8);

            // ---- voices
            for (int i = 0; i < _pulses.Length; i++) if (_pulses[i].Active) _pulses[i].Render(_bus, n, _gPrev[GPulse], _g[GPulse]);
            float groanDry = (float)_g[GGroanDry];
            for (int i = 0; i < _groans.Length; i++) if (_groans[i].Active) { _groans[i].Dry = groanDry; _groans[i].Render(_bus, n, _gPrev[GGroan], _g[GGroan]); }
            for (int i = 0; i < _bells.Length; i++) if (_bells[i].Active) _bells[i].Render(_bus, n, _gPrev[GBell], _g[GBell]);
            for (int i = 0; i < _ticks.Length; i++) if (_ticks[i].Active) _ticks[i].Render(_bus, n, _gPrev[GTick], _g[GTick]);
            for (int i = 0; i < _steps.Length; i++) if (_steps[i].Active) _steps[i].Render(_bus, n, _gPrev[GFoot], _g[GFoot]);
            if (_thud.Active) _thud.Render(_bus, n, ThudGain, ThudGain);

            // ---- master
            double wetLin = Math.Pow(10.0, _rvWet / 20.0);
            double inv = 1.0 / n;
            double f0 = _gPrev[GMaster], df = (_g[GMaster] - f0) * inv;
            double tl0 = _gPrev[GTail], dtl = (_g[GTail] - tl0) * inv;
            double tr0 = _gPrev[GTrim], dtr = (_g[GTrim] - tr0) * inv;
            double du0 = _gPrev[GDuck], ddu = (_g[GDuck] - du0) * inv;
            double w0 = _rvWetLinPrev, dw = (wetLin - w0) * inv;
            _rvWetLinPrev = wetLin;
            float peak = PeakAbs;
            for (int i = 0; i < n; i++)
            {
                double k = i + 1;
                double f = f0 + df * k, tail = tl0 + dtl * k, trim = tr0 + dtr * k, duck = du0 + ddu * k, wet = w0 + dw * k;
                double sl = _bus.SendL[i] * f + _bus.USendL[i];
                double sr = _bus.SendR[i] * f + _bus.USendR[i];
                _fdn.Process(sl, sr, out double rl, out double rr);
                double rg = wet * tail;
                double ml = ((_bus.DryL[i] * f + rl * rg) * trim + _bus.UDryL[i]) * duck;
                double mr = ((_bus.DryR[i] * f + rr * rg) * trim + _bus.UDryR[i]) * duck;
                float oL = (float)SoftLimiter.Process(_dcL.Process(ml));
                float oR = (float)SoftLimiter.Process(_dcR.Process(mr));
                _outL[i] = oL;
                _outR[i] = oR;
                float a = oL < 0f ? -oL : oL; if (a > peak) peak = a;
                a = oR < 0f ? -oR : oR; if (a > peak) peak = a;
            }
            PeakAbs = peak;
        }

        void ApplyTargets()
        {
            _gT[GSub] = _p.Sub; _gT[GAir] = _p.Air; _gT[GHum] = _p.Hum; _gT[GPulse] = _p.Pulse; _gT[GGroan] = _p.Groan;
            _gT[GGroanDry] = _p.GroanDry; _gT[GBell] = _p.Bell; _gT[GRumble] = _p.LiftRumble; _gT[GWhine] = _p.LiftWhine;
            _gT[GTick] = _p.LiftTick; _gT[GFan] = _p.Fan; _gT[GAirflow] = _p.Airflow; _gT[GWind] = _p.Wind;
            _gT[GFoot] = _p.Footsteps; _gT[GEndTone] = _p.EndTone;
            _gT[GMaster] = DspMath.Clamp01(_p.MasterFade);
            _gT[GTail] = DspMath.Clamp01(_p.TailGain);
            _gT[GTrim] = Math.Pow(10.0, _p.TrimDb / 20.0);
            _gT[GDuck] = DspMath.Clamp01(_p.Duck);
        }

        /// <summary>Render a bed whose effective gain this block is <paramref name="effective"/> (ramped from the last block).</summary>
        void RenderBed(int slot, double effective, int which)
        {
            double e0 = _eff[slot] < 0.0 ? effective : _eff[slot];
            _eff[slot] = effective;
            if (e0 == 0.0 && effective == 0.0) return;
            const int n = ControlBlock;
            switch (which)
            {
                case 0: _sub.Render(_bus, n, e0, effective); break;
                case 1: _hum.Render(_bus, n, e0, effective); break;
                case 2: _air.Render(_bus, n, e0, effective); break;
                case 3: _fan.Render(_bus, n, e0, effective); break;
                case 4: _airflow.Render(_bus, n, e0, effective); break;
                case 5: _rumble.Render(_bus, n, e0, effective); break;
                case 6: _wind.Render(_bus, n, e0, effective); break;
                case 7: _whine.Render(_bus, n, e0, effective); break;
                default: _endTone.Render(_bus, n, e0, effective); break;
            }
        }

        double _whistlePrev = -1.0;

        void RenderBedWhistle(double effective)
        {
            double e0 = _whistlePrev < 0.0 ? effective : _whistlePrev;
            _whistlePrev = effective;
            if (e0 == 0.0 && effective == 0.0) return;
            _whistle.Render(_bus, ControlBlock, e0, effective);
        }

        // ------------------------------------------------------------------ events

        void Events(double sceneT, double realT, double crank, double liftS)
        {
            // MACHINE PULSE: one dead centre every 45 degrees of the inner-left crank (= every 15 s, 14.5.4)
            double cu = crank / PulseStepDegrees;
            long pi = (long)Math.Floor(cu);
            if (_pulseIdx == long.MinValue) _pulseIdx = pi;
            else
            {
                int step = StepIndex(ref _pulseIdx, pi, cu);
                if (step > 0 && _gT[GPulse] > 0.0) FirePulse(_pulseIdx);
            }

            // LIFT rail ticks: every 12 m of travel while riding
            double tu = liftS / AudioMix.LiftTickSpacing + 1e-6;
            long ti = (long)Math.Floor(tu);
            if (_tickIdx == long.MinValue || !_p.LiftTicking) _tickIdx = ti;
            else
            {
                int step = StepIndex(ref _tickIdx, ti, tu);
                if (step > 0 && _gT[GTick] > 0.0) FireTick(_tickIdx);
            }

            // GROAN / BELL: seeded schedules on the scene clock (resynchronised after seeks)
            if (double.IsNaN(_groanNext) || sceneT < _lastSceneT - 0.5 || sceneT > _lastSceneT + 2.0) ResyncSchedules(sceneT);
            while (sceneT >= _groanNext)
            {
                if (sceneT - _groanNext < 0.5 && _gT[GGroan] > 0.0) FireGroan();
                _groanNext += GroanMin + GroanSpan * _groanRng.NextUnit();
            }
            while (sceneT >= _bellNext)
            {
                if (sceneT - _bellNext < 0.5 && _gT[GBell] > 0.0) FireBell();
                _bellNext += BellMin + BellSpan * _bellRng.NextUnit();
            }
            _lastSceneT = sceneT;

            DoorThud(realT);

            // footsteps queued by the PlayerController (player path)
            if (DrainAudioEvents)
            {
                int guard = 0;
                while (guard++ < 4 && AudioEvents.TryDequeueFootstep(out SurfaceKind s)) TriggerFootstep(s);
            }
        }

        /// <summary>Crank degrees between two dead centres (one MACHINE PULSE).</summary>
        public const double PulseStepDegrees = 45.0;

        /// <summary>
        /// Advance a monotonic event index toward the index <paramref name="target"/> = floor(<paramref name="units"/>) of
        /// the extrapolated clock. Returns +1 when exactly one boundary was crossed forward (fire the event at the new index),
        /// 0 when nothing fires. Extrapolation between snapshots can run ahead of the next snapshot (the audio thread renders
        /// in bursts, frames arrive late), so the clock may step back across a boundary it has just crossed: a step of -1 that
        /// lands less than half an interval behind the boundary is that jitter, and the index holds (the event already fired
        /// and fires again only at the next boundary). Anything else - two or more intervals, or a step back by more than
        /// half an interval - is a real seek (-clock, capture, ClockDriver.Begin): resynchronise silently.
        /// </summary>
        static int StepIndex(ref long index, long target, double units)
        {
            long diff = target - index;
            if (diff == 0) return 0;
            if (diff == 1) { index = target; return 1; }
            if (diff == -1 && index - units < 0.5) return 0;   // jitter just behind the boundary already passed: hold
            index = target;                                     // seek: resync without firing
            return 0;
        }

        /// <summary>Real seconds of backward motion of the walk's real time that count as a new run (RealTime reset).</summary>
        const double ThudResetJump = 2.0 * MaxExtrapolation;

        /// <summary>
        /// Start door thud (14.3 Start event, 14.11 timeline): exactly once per run, when the walk's real time crosses
        /// ThudTime (0.8 s). SceneClock.RealTime also runs from 0 during the black load and ClockDriver.Begin resets it when the
        /// walk starts, so the thud waits for <see cref="AudioParams.WalkStarted"/>. Crossing detection on a high-water mark:
        /// the walk starts at real time 0, extrapolation jitter never re-crosses, a backward jump of more than 0.5 s (a new
        /// Begin) re-arms. An engine created mid-walk (device change) starts from the current real time and does not replay
        /// a thud it missed; a crossing later than the 2.5 s picture fade-in (a stalled first frame) is dropped.
        /// </summary>
        void DoorThud(double realT)
        {
            if (!_p.WalkStarted) { _thudHigh = 0.0; return; }        // loading: armed for the walk start at real time 0
            if (double.IsNaN(_thudHigh)) _thudHigh = realT;          // first snapshot already inside the walk
            else if (realT < _thudHigh - ThudResetJump) _thudHigh = 0.0;   // RealTime restarted: a new run
            double at = _p.ThudTime;
            if (at >= 0.0 && _thudHigh < at && realT >= at && realT < at + AudioMix.TitleFade)
            {
                _thud.Trigger(ThudSeed, 0.0);
                ThudsFired++;
            }
            if (realT > _thudHigh) _thudHigh = realT;
        }

        public const double GroanFirst = 45.0, GroanFirstSpan = 15.0, GroanMin = 25.0, GroanSpan = 30.0;
        public const double BellFirst = 70.0, BellFirstSpan = 20.0, BellMin = 30.0, BellSpan = 40.0;

        void ResyncSchedules(double t)
        {
            _groanRng = new Xorshift32(DspMath.MixSeed(Seed, 31u));
            _bellRng = new Xorshift32(DspMath.MixSeed(Seed, 32u));
            _groanNext = GroanFirst + GroanFirstSpan * _groanRng.NextUnit();
            _bellNext = BellFirst + BellFirstSpan * _bellRng.NextUnit();
            while (_groanNext <= t) _groanNext += GroanMin + GroanSpan * _groanRng.NextUnit();
            while (_bellNext <= t) _bellNext += BellMin + BellSpan * _bellRng.NextUnit();
            _lastSceneT = t;
        }

        void FirePulse(long idx)
        {
            int bank = (int)(((-idx) % 4 + 4) % 4);
            float pan = bank == 0 ? _p.PulsePan0 : bank == 1 ? _p.PulsePan1 : bank == 2 ? _p.PulsePan2 : _p.PulsePan3;
            PulseVoice v = _pulses[_rrPulse];
            _rrPulse = (_rrPulse + 1) % _pulses.Length;
            v.Trigger(DspMath.MixSeed(Seed, 1000u + (uint)(idx & 0xFFFFF)), pan);
            PulsesFired++;
        }

        void FireTick(long idx)
        {
            BurstVoice v = _ticks[_rrTick];
            _rrTick = (_rrTick + 1) % _ticks.Length;
            v.Trigger(DspMath.MixSeed(Seed, 3000u + (uint)(idx & 0xFFFF)), 0.25);
            TicksFired++;
        }

        void FireGroan()
        {
            GroanVoice v = _groans[_rrGroan];
            _rrGroan = (_rrGroan + 1) % _groans.Length;
            v.Trigger(_groanRng.NextUInt());
            GroansFired++;
        }

        void FireBell()
        {
            BellVoice v = _bells[_rrBell];
            _rrBell = (_rrBell + 1) % _bells.Length;
            v.Trigger(_bellRng.NextUInt());
            BellsFired++;
        }

        // ------------------------------------------------------------------ reverb

        void ReverbControl()
        {
            if (_p.RevRt60 != _rvToRt || _p.RevPreDelay != _rvToPre || _p.RevDamping != _rvToDamp || _p.RevWetDb != _rvToWet)
            {
                _rvFromRt = _rvRt; _rvFromPre = _rvPre; _rvFromDamp = _rvDamp; _rvFromWet = _rvWet;
                _rvToRt = _p.RevRt60; _rvToPre = _p.RevPreDelay; _rvToDamp = _p.RevDamping; _rvToWet = _p.RevWetDb;
                _rvDur = Math.Max(0.05, _p.RevBlendSeconds);
                _rvProg = 0.0;
            }
            double wetTarget = _rvToWet;
            if (_rvProg < 1.0)
            {
                _rvProg = Math.Min(1.0, _rvProg + _ctrlDt / _rvDur);
                double s = DspMath.Smootherstep(_rvProg);
                _rvRt = Math.Exp(Math.Log(Math.Max(0.05, _rvFromRt)) + (Math.Log(Math.Max(0.05, (double)_rvToRt)) - Math.Log(Math.Max(0.05, _rvFromRt))) * s);
                _rvPre = _rvFromPre + (_rvToPre - _rvFromPre) * s;
                _rvDamp = Math.Exp(Math.Log(Math.Max(100.0, _rvFromDamp)) + (Math.Log(Math.Max(100.0, (double)_rvToDamp)) - Math.Log(Math.Max(100.0, _rvFromDamp))) * s);
                wetTarget = _rvFromWet + (_rvToWet - _rvFromWet) * s;
                _fdn.SetParams(_rvRt, _rvDamp, _rvPre);
            }
            double maxStep = AudioMix.WetRateDbPerSecond * _ctrlDt;
            double d = wetTarget - _rvWet;
            if (d > maxStep) d = maxStep; else if (d < -maxStep) d = -maxStep;
            _rvWet += d;
        }

        /// <summary>Smoothed master fade (follows AudioParams.MasterFade = GameState.PictureFade through the 50 ms one-pole; diagnostics / tests).</summary>
        public double MasterGain => _g[GMaster];
        /// <summary>Smoothed pause duck gain (1, or -15 dB while paused, 0.25 s one-pole; diagnostics / tests).</summary>
        public double DuckGain => _g[GDuck];

        /// <summary>Current blended reverb state (diagnostics / tests).</summary>
        public double ReverbRt60 => _rvRt;
        public double ReverbWetDb => _rvWet;
        public FdnReverb Reverb => _fdn;
    }
}
