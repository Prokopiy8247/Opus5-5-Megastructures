using System;
using System.Threading;

namespace Megastructure
{
    /// <summary>
    /// The Unity-free sound engine of The Ring (MASTER_PROMPT 14.3, Part 9 "Audio", 11.9, defect 25).
    /// Render() synthesises every layer of the 14.3 table from <see cref="AudioParams"/> (pure values) and the event
    /// queue, then runs the master chain: per-beat trim (<see cref="AudioTrims"/>), fade with the picture, pause duck,
    /// 20 Hz DC blocker and the look-ahead limiter (threshold -3 dB, internal ceiling -1.2 dBFS, 5 ms, 80 ms release).
    /// Allocation-free after construction, double-precision phases and states, every layer gain ramped by a 50 ms
    /// one-pole, filter modulation smoothed over 20 ms (log frequency). The same code runs in OnAudioFilterRead (player)
    /// and in the EditMode offline renders.
    /// </summary>
    public sealed class RingAudioEngine
    {
        // ---------------- layers ----------------
        public const int LC1Hum = 0, LC1Air = 1, LW1 = 2, LG1 = 3, LR1 = 4, LS1 = 5, LA1 = 6, LP1 = 7, LN1 = 8, LN2 = 9,
                         LU1 = 10, LH1Sine = 11, LH1Brown = 12, LT1 = 13, LTick = 14, LAeol = 15, LX1 = 16, LXGrain = 17, LB1 = 18,
                         LayerCount = 19;

        public static readonly string[] LayerNames =
        {
            "C1 hum", "C1 air", "W1 wind", "G1 grass", "R1 river", "S1 sub", "A1 ark", "P1 pad", "N1 bells", "N2 shimmer",
            "U1 underpass", "H1 sine", "H1 brown", "T1 tunnel", "T1 ticks", "T1 aeolian", "X1 drone", "X1 grains", "B1 beacon"
        };

        /// <summary>Raw-to-unit-RMS normalisation per layer (AudioWalkSim.CalibrateLayers at 48 kHz; sines are analytic).</summary>
        public static readonly double[] LayerNorm =
        {
            1.0,     6.4704, 11.4234, 15.1568, 0.9393, 1.0, 0.8256, 0.8684, 1.0, 5.0087,
            7.5706,  1.0,    6.3833,  7.5034,  1.0,    1.0, 1.0,    1.0018, 1.0
        };
        /// <summary>W1 normalisation at Q 0.6 / fc 220 Hz (beat 5) relative to the Q 0.8 value in LayerNorm.</summary>
        public const double W1NormQ06Rel = 11.1211 / 11.4234;
        /// <summary>RMS of the W1 gust gain 0.35 + 0.65 smooth-random over a long run (levels refer to it), and of the
        /// laminar beat-5 gust 0.70 + 0.30 smooth-random.</summary>
        public const double GustGainRms = 0.6827, LaminarGustRms = 0.8510;
        /// <summary>Beat-5 laminar gust gain 0.70 + 0.30 smooth-random (the rim wall's steadier wind).</summary>
        public const double LaminarFloor = 0.70, LaminarDepth = 0.30;
        /// <summary>W1 3 s loudness compensation (<see cref="GustComp"/>): grid step (s), half width in steps (31 taps =
        /// the 3 s RMS window of 14.3 Master) and the fraction of the 3 s gust level swing (in dB) that is removed.
        /// 0.85 is the smallest tested exponent (0.5, 0.625, 0.75, 0.85, 0.9, 1.0) with every valid 3 s window of every
        /// beat within +-1.5 dB on the cinematic, manual 1.5 m/s and brisk 2.5 m/s offline walks for 9 gust seeds
        /// (worst 1.45 dB; 0.75 fails 6 of the 27 renders, worst 1.70 dB; 0.5 leaves up to 10 % of the beat-2 windows
        /// outside, brisk p05 -2.0 dB). The 3 s wind level still drifts over a 1.7 dB band (7.7 dB uncompensated), and
        /// the gust keeps its shape inside the window, its filter sweep (fc x 0.92..1.08) and the published _Gust.</summary>
        public const double GustCompStep = 0.1;
        public const int GustCompHalf = 15;
        public const double GustCompExponent = 0.85;
        /// <summary>Pan followers (A1, R1): 50 ms one-pole time constant (14.3 "every layer ramps >= 50 ms").</summary>
        public const double PanTau = AudioSpec.LayerRampTau;

        public const int ControlBlock = 16;
        /// <summary>S1 (14.3): duck 0.4 s + near silence 1.1 s after the hatch trigger, then the wind rises over 3 s.</summary>
        public const double S1DuckTime = 0.4, S1QuietEnd = 0.4 + 1.1, S1RiseTime = 3.0;
        // Component normalisation inside composite layers (raw RMS measured at 48 kHz over 60 s): every component is
        // brought to unit RMS before its relative weight, so the internal balance is the designed one.
        const double A1RumbleNorm = 1.0 / 0.144885;      // brown -> LP 160 Hz
        const double A1WhooshNorm = 1.0 / 0.052323;      // pink -> BP Q 2 (constant-Q: independent of fc)
        const double R1FlowNorm = 1.0 / 0.124536;        // brown -> BP 260 Hz Q 0.7
        const double W1Voice2 = 0.35481338923357547 * (0.075917 / 0.060866);   // -9 dB relative to voice 1 (pink BP Q 1.4 vs Q 0.8)
        const double R1GrainAmp = 4.01;                  // droplet grains at -9 dB relative to the flow
        const double A1SineAmp = 0.8913;                 // 41.2 Hz swell sine at -4 dB relative (amplitude sqrt(2) * 0.631)
        const double A1WhooshW = 0.5623;                 // whoosh at -5 dB relative
        const double BellT60 = 5.0;                      // N1 bells decay 5 s (14.3)
        const double PingT60 = 3.0, PingAttack = 0.020;  // B1: attack 20 ms, decay 3 s (14.3)
        const double PingHz = 659.3;
        static readonly double Hum54Amp = 1.4142135623730951 * DspMath.DbToLin(AudioSpec.C1Hum54Db);
        static readonly double Hum108Amp = 1.4142135623730951 * DspMath.DbToLin(AudioSpec.C1Hum108Db);

        // ---------------- configuration ----------------
        double _fs, _invFs;
        readonly uint _seed;
        public double SampleRate => _fs;
        public readonly AudioEventQueue Events = new AudioEventQueue(128);
        /// <summary>Debug / calibration: render only this layer raw (gain 1, no trim, no master chain). -1 = normal.</summary>
        public int CalibrateLayer = -1;
        /// <summary>Per-beat trim knots (dB); defaults to <see cref="AudioTrims.KnotDb"/>, replaceable by the trim fitter.</summary>
        public double[] TrimKnotDb;
        public bool FootstepsEnabled = true;

        // ---------------- clocks ----------------
        long _n;                                  // samples rendered
        double _sEst, _tEst;                      // smoothed progress and game time
        bool _clockInit;
        double _snapS, _snapT; double _snapSpeed; bool _snapPaused; long _snapN; int _lastFrame = int.MinValue;
        double _aClk;
        AudioParams _p;

        // event clocks (game time at the first crossing; NaN = not crossed)
        double _tHatch = double.NaN, _hatchParamSeen = double.NaN;
        double _t231 = double.NaN, _t556 = double.NaN, _t894 = double.NaN;
        double _prevS = double.NaN;
        int _ribK = int.MinValue;
        double _lastPingT = double.NaN;
        int _pingCount, _tickCount, _stepCount;

        // ---------------- gains ----------------
        readonly double[] _g = new double[LayerCount];
        readonly double[] _gT = new double[LayerCount];
        double _aGain;                            // per-sample one-pole for 50 ms
        double _master, _masterT, _aMaster;
        double _duckDb;
        double _gust01;
        float _gustPublished;
        double _riverPanL = 1, _riverPanR = 1, _arkPanL = 1, _arkPanR = 1;
        double _trimLin = 1.0, _trimDb;

        // ---------------- layer states ----------------
        // C1
        double _ph54, _ph108;
        DspRng _rngC1L, _rngC1R; DspPink _pinkC1L, _pinkC1R; DspSvf _lpC1L, _lpC1R;
        // W1
        DspRng _rngWL, _rngWR; DspPink _pinkWL, _pinkWR;
        DspSvf _w1L, _w1R, _w2L, _w2R;
        double _lfw1L, _lfw1R, _lfw2L, _lfw2R, _wq;
        double _phiW1L, _phiW2L, _phiW3L, _phiW1R, _phiW2R, _phiW3R;
        double _gustLag;
        long _gcK = long.MinValue; uint _gcSeed; double _gc0 = 1.0, _gc1 = 1.0, _gl0 = 1.0, _gl1 = 1.0;   // W1 3 s loudness compensation cache
        // G1
        DspRng _rngGL, _rngGR; DspPink _pinkGL, _pinkGR; DspSvf _hpGL, _hpGR, _lpGL, _lpGR;
        // R1
        DspRng _rngRL, _rngRR; DspBrown _brRL, _brRR; DspSvf _bpRL, _bpRR;
        readonly DspGrain[] _riverGrains = new DspGrain[6];
        double _nextRiverGrain; DspRng _rngRG;
        // S1
        double _phS1aL, _phS1bL, _phS1aR, _phS1bR;
        // A1
        DspRng _rngAL, _rngAR, _rngAWL, _rngAWR; DspBrown _brAL, _brAR; DspSvf _lpAL, _lpAR; DspPink _pinkAWL, _pinkAWR; DspSvf _bpAWL, _bpAWR;
        double _phA41, _lfAW;
        // P1
        static readonly double[] PadF = { 73.4, 110.0, 146.8, 164.8, 220.0 };
        static readonly double[] PadW = { 1.0, 0.8, 0.62, 0.48, 0.42 };
        readonly double[] _phPadS = new double[5], _phPadTL = new double[5], _phPadTR = new double[5];
        DspSvf _lpPL, _lpPR;
        // N1
        readonly DspDecaySine[] _bellA = new DspDecaySine[3], _bellB = new DspDecaySine[3], _bellC = new DspDecaySine[3];
        readonly double[] _bellPL = new double[3], _bellPR = new double[3];
        double _nextBell = double.NaN; DspRng _rngBell; double _bellAmp;
        // N2
        DspRng _rngNL, _rngNR; DspSvf _bpNL, _bpNR; double _lfN, _n2Comp = 1.0;
        // U1, H1, T1
        DspRng _rngUL, _rngUR; DspBrown _brUL, _brUR; DspSvf _lpUL, _lpUR;
        double _phH1;
        DspRng _rngHL, _rngHR; DspBrown _brHL, _brHR; DspSvf _lpHL, _lpHR;
        DspRng _rngTL, _rngTR; DspBrown _brTL, _brTR; DspSvf _lpTL, _lpTR;
        readonly DspClick[] _ticks = new DspClick[3];
        DspRng _rngTick;
        double _phAeol, _phVib;
        // X1
        static readonly double[] DroneF = { 55.0, 82.4, 110.0, 138.6, 164.8 };
        static readonly double[] DroneW = { 1.0, 0.72, 0.6, 0.46, 0.4 };
        readonly double[] _phDL = new double[5], _phDR = new double[5];
        double _droneNorm;
        readonly DspGrain[] _xGrains = new DspGrain[20];
        double _nextXGrain; DspRng _rngXG; double _xGrainAmp;
        // B1
        readonly DspDecaySine[] _pings = new DspDecaySine[2];
        double _pingAmp;
        // footsteps and hatch
        readonly DspFootstep[] _steps = new DspFootstep[5];
        readonly AudioEvent[] _pending = new AudioEvent[32];
        int _pendingCount;
        uint _stepCounter;
        DspHatch _hatch;
        // master
        DspDcBlock _dcL, _dcR;
        readonly DspLimiter _lim = new DspLimiter();

        // ---------------- diagnostics (read from any thread) ----------------
        int _renders;
        float _lastBlockPeak;
        public int RenderCount => Volatile.Read(ref _renders);
        public float LastBlockPeak => Volatile.Read(ref _lastBlockPeak);
        /// <summary>W1 gust 0..1 (smooth-random of the engine's game-time clock), for diagnostics.</summary>
        public float Gust01 => Volatile.Read(ref _gustPublished);
        public double LimiterMinGain { get => _lim.MinGain; set => _lim.MinGain = value; }
        public double EstimatedS => _sEst;
        public double EstimatedT => _tEst;
        public double CurrentTrimDb => _trimDb;
        public double LayerGain(int i) => _g[i];
        /// <summary>Game time of the last B1 ping (engine clock), NaN before the first; pings / rib ticks / footsteps started.</summary>
        public double LastPingT => Volatile.Read(ref _lastPingT);
        public int PingCount => Volatile.Read(ref _pingCount);
        public int TickCount => Volatile.Read(ref _tickCount);
        public int StepCount => Volatile.Read(ref _stepCount);

        public RingAudioEngine(double sampleRate = AudioSpec.SampleRate, uint seed = 1u)
        {
            _seed = seed == 0u ? 1u : seed;
            TrimKnotDb = (double[])AudioTrims.KnotDb.Clone();
            SetSampleRate(sampleRate);
            InitStates();
        }

        uint S(uint a, uint b = 0u) => DspHash.Seed(_seed, a, b);

        void InitStates()
        {
            _rngC1L = new DspRng(S(1, 1)); _rngC1R = new DspRng(S(1, 2));
            _rngWL = new DspRng(S(2, 1)); _rngWR = new DspRng(S(2, 2));
            _rngGL = new DspRng(S(3, 1)); _rngGR = new DspRng(S(3, 2));
            _rngRL = new DspRng(S(4, 1)); _rngRR = new DspRng(S(4, 2)); _rngRG = new DspRng(S(4, 3));
            _rngAL = new DspRng(S(6, 1)); _rngAR = new DspRng(S(6, 2)); _rngAWL = new DspRng(S(6, 3)); _rngAWR = new DspRng(S(6, 4));
            _rngBell = new DspRng(S(8, 1));
            _rngNL = new DspRng(S(9, 1)); _rngNR = new DspRng(S(9, 2));
            _rngUL = new DspRng(S(10, 1)); _rngUR = new DspRng(S(10, 2));
            _rngHL = new DspRng(S(12, 1)); _rngHR = new DspRng(S(12, 2));
            _rngTL = new DspRng(S(13, 1)); _rngTR = new DspRng(S(13, 2)); _rngTick = new DspRng(S(14, 1));
            _rngXG = new DspRng(S(17, 1));
            var r = new DspRng(S(99, 7));
            // L/R seeds differ (14.3 W1): independent LFO phases per channel
            _phiW1L = r.Next01(); _phiW2L = r.Next01(); _phiW3L = r.Next01();
            _phiW1R = r.Next01(); _phiW2R = r.Next01(); _phiW3R = r.Next01();
            _lfw1L = _lfw1R = Math.Log(420.0); _lfw2L = _lfw2R = Math.Log(1150.0); _wq = 0.8;
            _lfAW = Math.Log(300.0); _lfN = Math.Log(7000.0);
            double sw = 0; for (int i = 0; i < 5; i++) sw += DroneW[i] * DroneW[i] * 0.5;
            _droneNorm = 1.0 / Math.Sqrt(sw);
            _nextRiverGrain = 0.0; _nextXGrain = 0.0;
            // N1: mean strike interval 10 s; energy per strike of the 3 partials (a^2 tau / 4 each, tau = T60 / 6.908);
            // 14.3: both bell sines decay 5 s (the third partial is a slow beating twin of the 1 174.7 Hz sine)
            double eBell = (1.0 + 0.49 + 0.04) * (BellT60 / 6.908) / 4.0;
            _bellAmp = Math.Sqrt(Math.Pow(10.0, AudioSpec.N1Db / 10.0) * 10.0 / eBell);
            // B1: one ping per 4 s, T60 3 s: long-term RMS -42 dBFS
            _pingAmp = Math.Sqrt(Math.Pow(10.0, AudioSpec.B1Db / 10.0) * AudioSpec.BeaconPeriod / ((PingT60 / 6.908) / 4.0));
            // X1 grains: power = rate * E[a^2] * dur * 3/16 (Hann^2 mean 3/8, sine 1/2); a ~ U(0.4, 1): E[a^2] = 0.52
            _xGrainAmp = Math.Sqrt(1.0 / (40.0 * 0.52 * 0.060 * 3.0 / 16.0));
        }

        /// <summary>Recomputes every rate-dependent coefficient (keeps musical state).</summary>
        public void SetSampleRate(double fs)
        {
            if (!(fs > 1000.0)) fs = AudioSpec.SampleRate;
            _fs = fs; _invFs = 1.0 / fs;
            _aGain = 1.0 - Math.Exp(-1.0 / (AudioSpec.LayerRampTau * fs));
            _aMaster = 1.0 - Math.Exp(-1.0 / (0.010 * fs));
            _aClk = 1.0 - Math.Exp(-ControlBlock / (0.030 * fs));
            _lpC1L.Set(800, 0.707, fs); _lpC1R.Set(800, 0.707, fs);
            _hpGL.Set(3000, 0.707, fs); _hpGR.Set(3000, 0.707, fs); _lpGL.Set(9000, 0.707, fs); _lpGR.Set(9000, 0.707, fs);
            _bpRL.Set(260, 0.7, fs); _bpRR.Set(260, 0.7, fs);
            _lpAL.Set(160, 0.707, fs); _lpAR.Set(160, 0.707, fs);
            _lpPL.Set(900, 0.6, fs); _lpPR.Set(900, 0.6, fs);
            _lpUL.Set(120, 0.707, fs); _lpUR.Set(120, 0.707, fs);
            _lpHL.Set(220, 0.707, fs); _lpHR.Set(220, 0.707, fs);
            _lpTL.Set(120, 0.707, fs); _lpTR.Set(120, 0.707, fs);
            _w1L.Set(420, 0.8, fs); _w1R.Set(420, 0.8, fs); _w2L.Set(1150, 1.4, fs); _w2R.Set(1150, 1.4, fs);
            _bpAWL.Set(300, 2.0, fs); _bpAWR.Set(300, 2.0, fs);
            _bpNL.Set(7000, 3.0, fs); _bpNR.Set(7000, 3.0, fs);
            _dcL.Set(AudioSpec.DcBlockHz, fs); _dcR.Set(AudioSpec.DcBlockHz, fs);
            _lim.Configure(fs, AudioSpec.LimiterThresholdDb, AudioSpec.LimiterCeilingDb, AudioSpec.LimiterLookaheadMs, AudioSpec.LimiterReleaseMs);
            // a rate change mid-stream (device change) restarts the limiter's delay line: fade the master back in over
            // its 10 ms smoother instead of resuming at full level
            if (_n > 0) _master = 0.0;
        }

        /// <summary>
        /// Beacon lens intensity locked to the B1 pings (14.5.7; the same time origin and formula as the beat-7
        /// BeaconPulse.Evaluate): t0 = game time at which s first reached 905 (a walk that starts beyond it back-dates
        /// t0 at walking speed). From t0 the amplitude eases 0 -> 0.225 over 1 s (smootherstep) and the intensity is
        /// 0.775 + amp sin(2 pi (t - t0) / 4), so the pulse peaks are at t0 + 1 + 4 k: the first ping plays at the first
        /// peak (1 s after s 905, when the ease-in completes) and every later peak is a ping. 0.775 before t0
        /// (t0 NaN or negative = not reached).
        /// </summary>
        public static float BeaconIntensity(double t, double t0)
        {
            if (!(t0 >= 0.0) || t < t0) return (float)BeaconBase;
            double e = t - t0;
            double ph = e - AudioSpec.BeaconPeriod * Math.Floor(e / AudioSpec.BeaconPeriod);   // wrapped: no drift in long runs
            double amp = BeaconAmp * DspMath.Smootherstep01(Math.Min(e / BeaconEaseIn, 2.0));
            return (float)(BeaconBase + amp * Math.Sin(DspMath.TwoPi * ph / AudioSpec.BeaconPeriod));
        }

        /// <summary>14.5.7 beacon pulse: base 0.775, amplitude 0.225, ease-in 1 s (= the delay of the first peak and ping).</summary>
        public const double BeaconBase = 0.775, BeaconAmp = 0.225, BeaconEaseIn = 1.0;

        /// <summary>Game time of ping k (k >= 0) for the origin t0: the pulse peaks t0 + 1 + 4 k.</summary>
        public static double PingTime(double t0, int k) => t0 + BeaconEaseIn + k * AudioSpec.BeaconPeriod;

        /// <summary>Ping phase 0..1 at game time t (0 at every ping and pulse peak); -1 before the first ping.</summary>
        public static double PingPhase(double t, double t0)
        {
            if (!(t0 >= 0.0) || t < t0 + BeaconEaseIn) return -1.0;
            return DspMath.Frac((t - t0 - BeaconEaseIn) / AudioSpec.BeaconPeriod);
        }

        /// <summary>Main-thread / offline helper: the W1 gust 0..1 at game time t (identical to the engine's).</summary>
        public static float GustAt(uint seed, double t) => (float)DspGust.Gust01(seed, t);

        // =====================================================================================================
        public void Render(float[] interleaved, int channels, int sampleRate, in AudioParams p)
        {
            if (interleaved == null || channels <= 0) return;
            Render(interleaved, interleaved.Length / channels, channels, sampleRate, in p);
        }

        public void Render(float[] buf, int frames, int channels, int sampleRate, in AudioParams p)
        {
            if (buf == null || channels <= 0 || frames <= 0) return;
            if (sampleRate > 1000 && Math.Abs(sampleRate - _fs) > 0.5) SetSampleRate(sampleRate);
            _p = p;
            AcceptSnapshot();
            DrainEvents();
            bool cal = CalibrateLayer >= 0;
            double peak = 0.0;
            int idx = 0;
            for (int f0 = 0; f0 < frames; f0 += ControlBlock)
            {
                int cnt = Math.Min(ControlBlock, frames - f0);
                Control(cnt);
                for (int k = 0; k < cnt; k++)
                {
                    Sample(out double l, out double r);
                    if (!cal)
                    {
                        _master += _aMaster * (_masterT - _master);
                        l *= _master; r *= _master;
                        l = _dcL.Tick(l); r = _dcR.Tick(r);
                        _lim.Process(ref l, ref r);
                    }
                    if (double.IsNaN(l) || double.IsInfinity(l)) l = 0.0;
                    if (double.IsNaN(r) || double.IsInfinity(r)) r = 0.0;
                    double al = l < 0 ? -l : l, ar = r < 0 ? -r : r;
                    if (al > peak) peak = al; if (ar > peak) peak = ar;
                    if (channels == 1) buf[idx] = (float)(0.5 * (l + r));
                    else
                    {
                        buf[idx] = (float)l; buf[idx + 1] = (float)r;
                        for (int c = 2; c < channels; c++) buf[idx + c] = 0f;
                    }
                    idx += channels;
                    _n++;
                }
            }
            Volatile.Write(ref _lastBlockPeak, (float)peak);
            Interlocked.Increment(ref _renders);
        }

        // ---------------- snapshot, clocks, events ----------------
        void AcceptSnapshot()
        {
            if (_p.Frame != _lastFrame || !_clockInit)
            {
                _lastFrame = _p.Frame;
                _snapS = _p.S; _snapT = _p.T; _snapSpeed = Math.Max(0.0, _p.Speed); _snapPaused = _p.PauseDuck > 0.5f; _snapN = _n;
                if (!_clockInit) { _sEst = _p.S; _tEst = _p.T; _clockInit = true; _prevS = _p.S; }
            }
        }

        void DrainEvents()
        {
            while (Events.TryDequeue(out var e))
            {
                if (_pendingCount < _pending.Length) _pending[_pendingCount++] = e;
                else { StartEvent(_pending[0]); _pending[0] = e; }
            }
        }

        void StartEvent(in AudioEvent e)
        {
            if (e.Kind != AudioEventKind.Footstep || !FootstepsEnabled) return;
            int slot = -1;
            for (int i = 0; i < _steps.Length; i++) if (!_steps[i].Active) { slot = i; break; }
            if (slot < 0) slot = (int)(_stepCounter % (uint)_steps.Length);
            _steps[slot].Start(_fs, e.Surface, e.Foot, e.Speed, S(0xF00u, ++_stepCounter));
            Interlocked.Increment(ref _stepCount);
        }

        /// <summary>Control-rate update (every 16 samples): clocks, event clocks, triggers and every gain target.</summary>
        void Control(int cnt)
        {
            double el = Math.Min(0.25, (_n - _snapN) * _invFs);
            double sTarget = _snapS + (_snapPaused ? 0.0 : _snapSpeed * el);
            double tTarget = _snapT + (_snapPaused ? 0.0 : el);
            if (Math.Abs(sTarget - _sEst) > 3.0) _sEst = sTarget; else _sEst += _aClk * (sTarget - _sEst);
            if (Math.Abs(tTarget - _tEst) > 1.0) _tEst = tTarget; else { double nt = _tEst + _aClk * (tTarget - _tEst); if (nt > _tEst) _tEst = nt; }
            double s = _sEst, t = _tEst;
            double tE = _n * _invFs;
            bool jumped = !double.IsNaN(_prevS) && Math.Abs(s - _prevS) > 2.0;

            // --- event clocks (H8: time branch since the crossing, progress branch for jumps) ---
            if (_p.HatchT >= 0.0 && _p.HatchT != _hatchParamSeen)
            {
                _hatchParamSeen = _p.HatchT;
                _tHatch = _p.HatchT;
                if (t - _p.HatchT < 0.25 && s < 12.0) _hatch.Start(_fs, S(0xA7u));
            }
            Cross(ref _t231, AudioSpec.ArkTriggerS, s, t);
            Cross(ref _t556, 556.0, s, t);
            Cross(ref _t894, AudioSpec.BeatS[6], s, t);
            _prevS = s;

            // pending footsteps (sample-accurate to the control block in the T clock)
            for (int i = 0; i < _pendingCount;)
            {
                if (_pending[i].T <= t || _pending[i].T > t + 2.0) { StartEvent(_pending[i]); _pending[i] = _pending[--_pendingCount]; }
                else i++;
            }

            // rib ticks: one per rib passed, ribs at x = 117 + 6 k <=> s = 777 + 6 k (k 0..19)
            {
                int k = (int)Math.Floor((s - (AudioSpec.BeatS[5] + (AudioSpec.TunnelRibX0 - AudioSpec.WallX))) / AudioSpec.TunnelRibPitch);
                if (_ribK != int.MinValue && k == _ribK + 1 && k >= 0 && k < AudioSpec.TunnelRibs && !jumped) StartTick();
                _ribK = k;
            }

            // beacon pings at t0 + 1 + 4 k, t0 = when s first reached 905: locked to the pulse peaks of BeaconIntensity /
            // the beat-7 BeaconPulse, which use the same t0; a ping that is more than 0.1 s overdue (jump-in) is skipped
            // (an origin that moves by rounding, e.g. a float-computed copy replacing a double one, never re-plays a ping:
            // a due ping counts as played when one was started within half a period of it)
            if (_p.BeaconT0 >= 0.0)
            {
                double first = _p.BeaconT0 + BeaconEaseIn;
                int due = (int)Math.Floor((t - first) / AudioSpec.BeaconPeriod);
                if (due >= 0)
                {
                    double tk = first + due * AudioSpec.BeaconPeriod;
                    bool played = !double.IsNaN(_lastPingT) && Math.Abs(tk - _lastPingT) < 0.5 * AudioSpec.BeaconPeriod;
                    if (!played && t - tk < 0.1)
                    {
                        StartPing();
                        Volatile.Write(ref _lastPingT, tk);
                        Interlocked.Increment(ref _pingCount);
                    }
                }
            }

            // --- gust ---
            _gust01 = DspGust.Gust01(_p.GustSeed, t);
            Volatile.Write(ref _gustPublished, (float)_gust01);
            _gustLag += (1.0 - Math.Exp(-cnt / (0.3 * _fs))) * (_gust01 - _gustLag);

            double u = _p.SunsetU;
            double night = s >= AudioSpec.BeatS[4] ? 1.0 : DspMath.Smoothstep(0.30, 0.80, u);

            // --- trim and the S3 tunnel silence (only footsteps and tails) ---
            _trimDb = TrimAt(s);
            _trimLin = DspMath.DbToLin(_trimDb);
            double gS3 = 1.0 - (DspMath.SS(830.3, 831.0, s) - DspMath.SS(837.0, 838.5, s));
            double trim = _trimLin * gS3;

            // --- hatch, exit clocks ---
            double hatchE = double.IsNaN(_tHatch) ? double.NegativeInfinity : t - _tHatch;
            double exitE = double.IsNaN(_t894) ? double.NegativeInfinity : Math.Max(t - _t894, (s - AudioSpec.BeatS[6]) / AudioSpec.Walk);
            double exitDuck = exitE > 0 ? DspMath.Smootherstep01(exitE / 1.2) * DspMath.SS(888, 894, s) : 0.0;
            double swell = exitE > 1.2 ? DspMath.Smootherstep01((exitE - 1.2) / 10.0) * DspMath.SS(884, 894, s) : 0.0;
            double tunnelGate = DspMath.SS(770, 784, s) * (1.0 - exitDuck) * (1.0 - DspMath.SS(896, 900, s));

            // C1: room tone ducks to silence in 0.4 s at the hatch trigger (S1)
            double c1 = (hatchE > 0 ? 1.0 - DspMath.Smootherstep01(hatchE / S1DuckTime) : 1.0) * (1.0 - DspMath.SS(9.5, 11.0, s));
            SetT(LC1Hum, c1 * trim);
            SetT(LC1Air, c1 * trim * LayerNorm[LC1Air] * DspMath.DbToLin(AudioSpec.C1AirDb));

            // W1 (S1): the room tone ducks in 0.4 s, 1.1 s of near silence (only the hatch itself), then the wind rises
            // from nothing over 3 s (hatch + 1.5 .. 4.5 s); progress branch for jumps / brisk
            double windIn = Math.Max(hatchE > 0 ? DspMath.Smootherstep01((hatchE - S1QuietEnd) / S1RiseTime) : 0.0, DspMath.SS(11.0, 15.5, s));
            double w5 = DspMath.SS(582, 606, s) * (1.0 - DspMath.SS(762, 786, s));
            double compT = GustComp(t, out double compL);
            double laminarGust = (LaminarFloor + LaminarDepth * _gust01) / LaminarGustRms * compL;
            double gustRel = (DspGust.Floor + DspGust.Depth * _gust01) / GustGainRms * compT;
            gustRel = DspMath.Lerp(gustRel, laminarGust, w5);
            double windLevel = W1LevelDb(s, u)
                               - 4.0 * DspMath.Clamp01(_p.ArkCue)                                         // beat-3 felt cue
                               - 9.0 * DspMath.SS(558, 564, s) * (1.0 - DspMath.SS(592, 597, s));          // sealed underpass
            double s2 = S2Duck(u);
            double windGate = (s < 830.0 ? windIn * (1.0 - DspMath.SS(770, 778, s)) : 0.0) + (s > 880.0 ? swell : 0.0);
            double w1Norm = LayerNorm[LW1] * Math.Exp(DspMath.Lerp(0.0, Math.Log(W1NormQ06Rel), w5));
            SetT(LW1, DspMath.DbToLin(windLevel) * windGate * gustRel * DspMath.Lerp(1.0, 0.2512, s2) * w1Norm * trim);
            // wind filter targets (log-f smoothing 20 ms at control rate)
            {
                double fcBase = DspMath.Lerp(420.0, 220.0, w5) * (0.92 + 0.16 * _gustLag);
                _wq = DspMath.Lerp(0.8, 0.6, w5);
                double l1 = 2 * Math.PI * 0.07 * tE, l2 = 2 * Math.PI * 0.31 * tE, l3 = 2 * Math.PI * 0.11 * tE;
                double fc1L = fcBase * (1 + 0.35 * Math.Sin(l1 + DspMath.TwoPi * _phiW1L)) * (1 + 0.12 * Math.Sin(l2 + DspMath.TwoPi * _phiW2L));
                double fc1R = fcBase * (1 + 0.35 * Math.Sin(l1 + DspMath.TwoPi * _phiW1R)) * (1 + 0.12 * Math.Sin(l2 + DspMath.TwoPi * _phiW2R));
                double fc2L = 1150.0 * (1 + 0.15 * Math.Sin(l3 + DspMath.TwoPi * _phiW3L));
                double fc2R = 1150.0 * (1 + 0.15 * Math.Sin(l3 + DspMath.TwoPi * _phiW3R));
                double a = 1.0 - Math.Exp(-cnt / (AudioSpec.FilterSmoothTau * _fs));
                _lfw1L += a * (Math.Log(fc1L) - _lfw1L); _lfw1R += a * (Math.Log(fc1R) - _lfw1R);
                _lfw2L += a * (Math.Log(fc2L) - _lfw2L); _lfw2R += a * (Math.Log(fc2R) - _lfw2R);
                if (_gT[LW1] > 0 || _g[LW1] > 1e-7)
                {
                    _w1L.Set(Math.Exp(_lfw1L), _wq, _fs); _w1R.Set(Math.Exp(_lfw1R), _wq, _fs);
                    _w2L.Set(Math.Exp(_lfw2L), 1.4, _fs); _w2R.Set(Math.Exp(_lfw2R), 1.4, _fs);
                }
            }

            // G1 grass: x 0.4 gust (modulation depth), beats 2-4 on the meadow, 6 dB lower on the bridge
            double bridgeDip = DspMath.DbToLin(-6.0 * DspMath.SS(116, 124, s) * (1.0 - DspMath.SS(176, 184, s)));
            double g1 = DspMath.SS(18, 30, s) * (1.0 - DspMath.SS(550, 560, s)) * bridgeDip * (0.6 + 0.4 * _gust01) / 0.8;
            SetT(LG1, g1 * DspMath.DbToLin(AudioSpec.G1Db) * LayerNorm[LG1] * trim);

            // R1 river: from s 60, gain 1 / (1 + d / 12), panned to the river
            double r1 = 0.0;
            if (_p.RiverDist >= 0f)
            {
                r1 = DspMath.SS(60, 84, s) * (1.0 - DspMath.SS(264, 288, s)) / (1.0 + _p.RiverDist / 12.0);
                double pan = Math.Sin(_p.RiverAzimuth * (Math.PI / 180.0)) * DspMath.SS(2, 24, Math.Max(0f, _p.RiverCentreDist));
                DspMath.Pan(pan * 0.85, out double pl, out double pr);
                double aPan = 1.0 - Math.Exp(-cnt / (PanTau * _fs));         // 50 ms one-pole at any sample rate
                _riverPanL += aPan * (pl - _riverPanL); _riverPanR += aPan * (pr - _riverPanR);
            }
            SetT(LR1, r1 * DspMath.DbToLin(AudioSpec.R1Db) * LayerNorm[LR1] * trim);
            if (_g[LR1] > 1e-6 && tE >= _nextRiverGrain)
            {
                double rate = 10.0 + 4.0 * DspSmoothRandom.Get(S(4, 9), tE, 0.25);
                StartRiverGrain();
                _nextRiverGrain = tE - Math.Log(Math.Max(1e-9, 1.0 - _rngRG.Next01())) / rate;
            }

            // S1 ring sub: beats 2-5, out over s 774-806
            SetT(LS1, DspMath.SS(12, 36, s) * (1.0 - DspMath.SS(774, 806, s)) * DspMath.DbToLin(AudioSpec.S1Db) * trim);

            // A1 ark: in over 6 s at the spawn (s 231), gain 1 / (1 + (range / 3000)^2), pan = azimuth / 60
            double a1 = 0.0;
            if (_p.ArkRange > 0f && !double.IsNaN(_t231))
            {
                double inE = Math.Max(t - _t231, (s - AudioSpec.ArkTriggerS) / AudioSpec.Walk);
                double gr = 1.0 / (1.0 + Math.Pow(_p.ArkRange / 3000.0, 2.0));
                double gmax = 1.0 / (1.0 + Math.Pow(AudioSpec.A1RangeMin / 3000.0, 2.0));
                a1 = DspMath.Smootherstep01(inE / 6.0) * (1.0 - DspMath.SS(552, 566, s)) * gr / gmax;
                DspMath.Pan(ArkPan(_p.ArkAzimuth), out double pl, out double pr);
                double aPan = 1.0 - Math.Exp(-cnt / (PanTau * _fs));         // 50 ms one-pole at any sample rate
                _arkPanL += aPan * (pl - _arkPanL); _arkPanR += aPan * (pr - _arkPanR);
                double close = DspMath.Clamp01(gr / gmax);
                double fcw = 180.0 * Math.Pow(600.0 / 180.0, DspMath.Clamp01(0.25 + 0.5 * close + 0.25 * Math.Sin(DspMath.TwoPi * 0.045 * tE)));
                _lfAW += (1.0 - Math.Exp(-cnt / (AudioSpec.FilterSmoothTau * _fs))) * (Math.Log(fcw) - _lfAW);
                _bpAWL.Set(Math.Exp(_lfAW), 2.0, _fs); _bpAWR.Set(Math.Exp(_lfAW * 1.0 + 0.03), 2.0, _fs);
            }
            SetT(LA1, a1 * DspMath.DbToLin(AudioSpec.A1PeakDb) * LayerNorm[LA1] * trim);

            // P1 dusk pad: attack 8 s from the sunset start (u 0), release 10 s from s 556, S2 totality to -60 dBFS for 4 s
            double atk = u > 0 ? DspMath.Smootherstep01(u * AudioSpec.SunsetT / 8.0) : 0.0;
            double relE = double.IsNaN(_t556) ? double.NegativeInfinity : Math.Max(t - _t556, (s - 556.0) / AudioSpec.Walk);
            double rel = relE > 0 ? 1.0 - DspMath.Smootherstep01(relE / 10.0) : 1.0;
            double padS2 = DspMath.Lerp(1.0, DspMath.DbToLin(AudioSpec.P1S2Dbfs - AudioSpec.P1Db), s2);
            double p1Gate = atk * rel * (1.0 - DspMath.SS(590, 600, s));
            SetT(LP1, p1Gate * padS2 * DspMath.DbToLin(AudioSpec.P1Db) * LayerNorm[LP1] * trim);

            // N1 bells (night, beats 4-5), N2 shimmer (night, beat 4)
            double inUnder = DspMath.SS(558, 564, s) * (1.0 - DspMath.SS(592, 597, s));
            double n1 = night * (1.0 - DspMath.SS(770, 786, s)) * DspMath.DbToLin(-10.0 * inUnder);
            SetT(LN1, n1 * trim);
            if (double.IsNaN(_nextBell)) _nextBell = tE + 3.0 + 4.0 * _rngBell.Next01();
            if (tE >= _nextBell)
            {
                if (_gT[LN1] > 0.02 * _trimLin) StartBell();
                _nextBell = tE + 7.0 + 6.0 * _rngBell.Next01();
            }
            double n2 = night * (1.0 - DspMath.SS(552, 566, s));
            SetT(LN2, n2 * DspMath.DbToLin(AudioSpec.N2Db) * LayerNorm[LN2] * trim);
            if (_gT[LN2] > 0 || _g[LN2] > 1e-7)
            {
                double fcn = 5000.0 * Math.Pow(9000.0 / 5000.0, 0.5 + 0.5 * Math.Sin(DspMath.TwoPi * 0.05 * tE));
                _lfN += (1.0 - Math.Exp(-cnt / (AudioSpec.FilterSmoothTau * _fs))) * (Math.Log(fcn) - _lfN);
                double fcs = Math.Exp(_lfN);
                _bpNL.Set(fcs, 3.0, _fs); _bpNR.Set(fcs * 1.04, 3.0, _fs);
                _n2Comp = Math.Sqrt(7000.0 / fcs);      // white-noise band power is proportional to fc / Q
            }

            // U1 underpass, H1 wall hum, T1 tunnel
            SetT(LU1, DspMath.SS(552, 562, s) * (1.0 - DspMath.SS(591, 597, s)) * DspMath.DbToLin(AudioSpec.U1Db) * LayerNorm[LU1] * trim);
            double h1 = DspMath.SS(594, 600, s) * (1.0 - DspMath.SS(772, 784, s));
            SetT(LH1Sine, h1 * DspMath.DbToLin(AudioSpec.H1Db - 3.0103) * trim);
            SetT(LH1Brown, h1 * DspMath.DbToLin(AudioSpec.H1Db - 3.0103) * LayerNorm[LH1Brown] * trim);
            SetT(LT1, tunnelGate * DspMath.DbToLin(AudioSpec.T1Db) * LayerNorm[LT1] * trim);
            SetT(LTick, tunnelGate * DspMath.DbToLin(AudioSpec.T1TickDb) * trim);
            double x = DspMath.Clamp01((s - AudioSpec.BeatS[5]) / (AudioSpec.BeatS[6] - AudioSpec.BeatS[5]));
            double aeolDb = AudioSpec.T1AeolStartDb + (AudioSpec.T1AeolEndDb - AudioSpec.T1AeolStartDb) * x * x;
            SetT(LAeol, tunnelGate * DspMath.DbToLin(aeolDb) * trim);

            // X1 star (S4: all but footsteps duck 1.2 s, then X1 swells 10 s), B1 beacon
            SetT(LX1, swell * DspMath.DbToLin(AudioSpec.X1DroneDb) * LayerNorm[LX1] * trim);
            SetT(LXGrain, swell * DspMath.DbToLin(AudioSpec.X1GrainDb) * LayerNorm[LXGrain] * trim);
            if (_g[LXGrain] > 1e-6 && tE >= _nextXGrain)
            {
                StartXGrain();
                _nextXGrain = tE - Math.Log(Math.Max(1e-9, 1.0 - _rngXG.Next01())) / 40.0;
            }
            SetT(LB1, DspMath.SS(898, 903, s) * trim);

            // --- calibration override ---
            if (CalibrateLayer >= 0)
            {
                for (int i = 0; i < LayerCount; i++) _gT[i] = i == CalibrateLayer ? 1.0 : 0.0;
                _masterT = _master = 1.0;
                return;
            }

            // --- master: fade with the picture, ending fade (S5), pause duck 12 dB over 0.4 s ---
            double duckTarget = _p.PauseDuck > 0.5f ? AudioSpec.PauseDuckDb : 0.0;
            double step = -AudioSpec.PauseDuckDb / AudioSpec.PauseDuckTime * cnt * _invFs;
            if (_duckDb < duckTarget) _duckDb = Math.Min(duckTarget, _duckDb + step); else if (_duckDb > duckTarget) _duckDb = Math.Max(duckTarget, _duckDb - step);
            _masterT = DspMath.Clamp01(_p.PictureFade) * DspMath.Clamp01(_p.EndingFade) * DspMath.DbToLin(_duckDb);
        }

        void SetT(int i, double v) { _gT[i] = v > 0.0 && !double.IsNaN(v) ? v : 0.0; }

        /// <summary>
        /// A1 pan (14.3 "pan = azimuth / 60") for an azimuth in degrees (+ = right of the view). Stereo cannot place a
        /// source behind the listener, so the rear half plane is folded onto the front one (az -> 180 - az): the pan law
        /// is exactly az / 60 for |az| &lt;= 90 and continuous through the back (centred when the ark is straight
        /// behind), instead of jumping from hard right to hard left when the azimuth wraps through +-180.
        /// </summary>
        public static double ArkPan(double azDeg)
        {
            if (double.IsNaN(azDeg) || double.IsInfinity(azDeg)) return 0.0;
            double az = azDeg - 360.0 * Math.Floor((azDeg + 180.0) / 360.0);     // [-180, 180)
            if (az > 90.0) az = 180.0 - az; else if (az < -90.0) az = -180.0 - az;
            return DspMath.Clamp(az / 60.0, -1.0, 1.0);
        }

        /// <summary>
        /// W1 slow loudness compensation at game time t (14.3 Master: "this table wins over the layer levels"). The
        /// gust gain 0.35 + 0.65 smooth-random(0.15 Hz) swings the 3 s mean of the wind by up to -9 dB, which alone moves
        /// single 3 s windows of the wind-led beats 2 and 3 by +-2..3 dB around their targets (tolerance +-1.5 dB). The
        /// gust is a pure function of t, so the RMS of the gust gain over the centred 3 s window (31 taps, 0.1 s) is
        /// known exactly; the wind is scaled by (GustGainRms / that RMS)^GustCompExponent, which shrinks the 3 s level
        /// swing in dB by that fraction while the gust keeps its shape inside the window (the beat-5 laminar gust
        /// 0.70 + 0.30 SR gets the same treatment with its own RMS, returned in <paramref name="laminar"/>). Evaluated
        /// on a 0.1 s grid of t and interpolated linearly between grid points (smooth, no allocation, about 300 gust
        /// evaluations per second of game time). This is a deliberate arbitration of 14.3 (gust depth vs. the master
        /// RMS table) under Part 1.6.2.
        /// </summary>
        double GustComp(double t, out double laminar)
        {
            double g = t / GustCompStep, fk = Math.Floor(g);
            long k = (long)fk;
            uint seed = _p.GustSeed;
            if (k != _gcK || seed != _gcSeed)
            {
                if (k == _gcK + 1 && seed == _gcSeed) { _gc0 = _gc1; _gl0 = _gl1; GustCompAt(seed, (k + 1) * GustCompStep, out _gc1, out _gl1); }
                else { GustCompAt(seed, k * GustCompStep, out _gc0, out _gl0); GustCompAt(seed, (k + 1) * GustCompStep, out _gc1, out _gl1); }
                _gcK = k; _gcSeed = seed;
            }
            double f = g - fk;
            laminar = _gl0 + (_gl1 - _gl0) * f;
            return _gc0 + (_gc1 - _gc0) * f;
        }

        /// <summary>The compensation factors of <see cref="GustComp"/> at grid time tc (pure function, any thread): for
        /// the turbulent gust 0.35 + 0.65 SR of beats 1-4 and 7 and for the laminar beat-5 gust 0.70 + 0.30 SR.</summary>
        public static void GustCompAt(uint seed, double tc, out double turbulent, out double laminar)
        {
            double accT = 0.0, accL = 0.0;
            for (int j = -GustCompHalf; j <= GustCompHalf; j++)
            {
                double u = DspGust.Gust01(seed, tc + j * GustCompStep);
                double gt = DspGust.Floor + DspGust.Depth * u, gl = LaminarFloor + LaminarDepth * u;
                accT += gt * gt; accL += gl * gl;
            }
            double n = 2 * GustCompHalf + 1;
            turbulent = Math.Pow(GustGainRms / Math.Max(1e-6, Math.Sqrt(accT / n)), GustCompExponent);
            laminar = Math.Pow(LaminarGustRms / Math.Max(1e-6, Math.Sqrt(accL / n)), GustCompExponent);
        }

        /// <summary>The turbulent-gust factor of <see cref="GustCompAt(uint, double, out double, out double)"/>.</summary>
        public static double GustCompAt(uint seed, double tc)
        {
            GustCompAt(seed, tc, out double turbulent, out _);
            return turbulent;
        }

        static void Cross(ref double tCross, double point, double s, double t)
        {
            if (!double.IsNaN(tCross)) return;
            if (s >= point) tCross = t - (s - point) / AudioSpec.Walk;   // time branch anchored so a jump-in equals the progress branch
        }

        /// <summary>S2 totality (u 0.55): duck envelope 0..1 (in 1 s, hold 4 s, out 2.5 s) on the u * 70 s clock.</summary>
        public static double S2Duck(double u)
        {
            double e = (u - 0.55) * AudioSpec.SunsetT;
            if (e <= 0.0) return 0.0;
            return DspMath.Smootherstep01(e / 1.0) * (1.0 - DspMath.Smootherstep01((e - 5.0) / 2.5));
        }

        /// <summary>W1 level (dB) by beat with the 24 m Rule-P crossfades at the beat boundaries.</summary>
        public static double W1LevelDb(double s, double u)
        {
            double b1 = AudioSpec.BeatS[1], b2 = AudioSpec.BeatS[2], b3 = AudioSpec.BeatS[3], b4 = AudioSpec.BeatS[4];
            double l1 = AudioSpec.W1B1Db, l2 = AudioSpec.W1B2Db, l3 = AudioSpec.W1B3Db, l5 = AudioSpec.W1B5Db;
            double l4 = DspMath.Lerp(AudioSpec.W1B4StartDb, AudioSpec.W1B4EndDb, DspMath.Smootherstep01(u));
            if (s < b1 + 12) return DspMath.Lerp(l1, l2, DspMath.SS(b1 - 12, b1 + 12, s));
            if (s < b2 + 12) return DspMath.Lerp(l2, l3, DspMath.SS(b2 - 12, b2 + 12, s));
            if (s < b3 + 12) return DspMath.Lerp(l3, l4, DspMath.SS(b3 - 12, b3 + 12, s));
            if (s < b4 + 12) return DspMath.Lerp(l4, l5, DspMath.SS(b4 - 12, b4 + 12, s));
            if (s < 860) return l5;
            return AudioSpec.W1B7Db;
        }

        /// <summary>Per-beat trim (dB) at s from the knot table (smootherstep inside the 24 m boundary windows).</summary>
        public double TrimAt(double s)
        {
            var ks = AudioTrims.KnotS; var kd = TrimKnotDb;
            if (s <= ks[0]) return kd[0];
            int last = ks.Length - 1;
            if (s >= ks[last]) return kd[last];
            int lo = 0, hi = last;
            while (hi - lo > 1) { int m = (lo + hi) >> 1; if (ks[m] <= s) lo = m; else hi = m; }
            double t = (s - ks[lo]) / Math.Max(1e-9, ks[hi] - ks[lo]);
            bool boundary = AudioTrims.KnotBeat[lo] != AudioTrims.KnotBeat[hi];
            if (boundary) t = DspMath.Smootherstep01(t);
            return kd[lo] + (kd[hi] - kd[lo]) * t;
        }

        // ---------------- voice starters ----------------
        void StartTick()
        {
            for (int i = 0; i < _ticks.Length; i++)
                if (!_ticks[i].Active)
                {
                    _ticks[i].Start(_fs, 3000.0 * (0.97 + 0.06 * _rngTick.Next01()), 0.002, _rngTick.Range(-0.3, 0.3), S(0x71C, (uint)_n));
                    Interlocked.Increment(ref _tickCount);
                    return;
                }
        }

        void StartPing()
        {
            int slot = _pings[0].Active ? 1 : 0;
            _pings[slot].Start(_fs, PingHz, _pingAmp, PingT60, PingAttack);
        }

        void StartBell()
        {
            int slot = 0;
            for (int i = 0; i < 3; i++) if (!_bellA[i].Active) { slot = i; break; }
            double a = _bellAmp;
            _bellA[slot].Start(_fs, 1174.7, a, BellT60, 0.004);
            _bellB[slot].Start(_fs, 880.0, 0.7 * a, BellT60, 0.004);
            _bellC[slot].Start(_fs, 1175.3, 0.2 * a, BellT60, 0.004);
            DspMath.Pan(_rngBell.Range(-0.7, 0.7), out _bellPL[slot], out _bellPR[slot]);
        }

        void StartRiverGrain()
        {
            for (int i = 0; i < _riverGrains.Length; i++)
                if (!_riverGrains[i].Active)
                {
                    double f = 1200.0 * Math.Pow(3400.0 / 1200.0, _rngRG.Next01());
                    double a = 0.4 + 0.6 * _rngRG.Next01();
                    double pan = DspMath.Clamp(0.5 * (_riverPanR - _riverPanL) + _rngRG.Range(-0.5, 0.5), -1, 1);
                    _riverGrains[i].Start(_fs, f, a * R1GrainAmp, 0.008, pan);
                    return;
                }
        }

        void StartXGrain()
        {
            for (int i = 0; i < _xGrains.Length; i++)
                if (!_xGrains[i].Active)
                {
                    double f = 2000.0 * Math.Pow(3.0, _rngXG.Next01());
                    double a = 0.4 + 0.6 * _rngXG.Next01();
                    _xGrains[i].Start(_fs, f, a * _xGrainAmp, 0.060, _rngXG.Range(-1.0, 1.0));
                    return;
                }
        }

        // ---------------- per-sample synthesis ----------------
        bool On(int i)
        {
            double tg = _gT[i];
            double g = _g[i] + _aGain * (tg - _g[i]);
            if (g < 1e-8 && tg <= 0.0) g = 0.0;
            _g[i] = g;
            return g > 0.0;
        }

        void Sample(out double outL, out double outR)
        {
            double l = 0.0, r = 0.0;

            // C1 hum (sines 54 + 108 Hz) and air (pink -> LP 800 Hz)
            if (On(LC1Hum))
            {
                _ph54 += 54.0 * _invFs; if (_ph54 >= 1.0) _ph54 -= 1.0;
                _ph108 += 108.0 * _invFs; if (_ph108 >= 1.0) _ph108 -= 1.0;
                double h = Hum54Amp * Math.Sin(DspMath.TwoPi * _ph54) + Hum108Amp * Math.Sin(DspMath.TwoPi * _ph108 + 0.4);
                h *= _g[LC1Hum]; l += h; r += h;
            }
            if (On(LC1Air))
            {
                double g = _g[LC1Air];
                l += g * _lpC1L.Low(_pinkC1L.Next(_rngC1L.Bipolar()));
                r += g * _lpC1R.Low(_pinkC1R.Next(_rngC1R.Bipolar()));
            }

            // W1 wind: pink -> BP (fc 420, Q 0.8) + second voice BP 1150 Q 1.4 at -9 dB
            if (On(LW1))
            {
                double g = _g[LW1];
                double wl = _pinkWL.Next(_rngWL.Bipolar()), wr = _pinkWR.Next(_rngWR.Bipolar());
                l += g * (_w1L.BandNorm(wl) + W1Voice2 * _w2L.BandNorm(wl));
                r += g * (_w1R.BandNorm(wr) + W1Voice2 * _w2R.BandNorm(wr));
            }

            // G1 grass: pink -> HP 3 kHz -> LP 9 kHz
            if (On(LG1))
            {
                double g = _g[LG1];
                l += g * _lpGL.Low(_hpGL.High(_pinkGL.Next(_rngGL.Bipolar())));
                r += g * _lpGR.Low(_hpGR.High(_pinkGR.Next(_rngGR.Bipolar())));
            }

            // R1 river: brown -> BP 260 Hz Q 0.7 + droplet grains 1.2-3.4 kHz, 8 ms
            if (On(LR1))
            {
                double g = _g[LR1];
                double fl = R1FlowNorm * _bpRL.BandNorm(_brRL.Next(_rngRL.Bipolar())), fr = R1FlowNorm * _bpRR.BandNorm(_brRR.Next(_rngRR.Bipolar()));
                double gl = 0, gr = 0;
                for (int i = 0; i < _riverGrains.Length; i++)
                {
                    if (!_riverGrains[i].Active) continue;
                    _riverGrains[i].Next(out double a, out double b); gl += a; gr += b;
                }
                l += g * (fl * _riverPanL + gl);
                r += g * (fr * _riverPanR + gr);
            }

            // S1 ring sub: sines 36.7 + 55.0 Hz, the right channel detuned by 0.09 Hz (slow spatial beat)
            if (On(LS1))
            {
                double g = _g[LS1];
                _phS1aL += 36.7 * _invFs; if (_phS1aL >= 1.0) _phS1aL -= 1.0;
                _phS1bL += 55.0 * _invFs; if (_phS1bL >= 1.0) _phS1bL -= 1.0;
                _phS1aR += 36.79 * _invFs; if (_phS1aR >= 1.0) _phS1aR -= 1.0;
                _phS1bR += 55.09 * _invFs; if (_phS1bR >= 1.0) _phS1bR -= 1.0;
                l += g * (Math.Sin(DspMath.TwoPi * _phS1aL) + Math.Sin(DspMath.TwoPi * _phS1bL));
                r += g * (Math.Sin(DspMath.TwoPi * _phS1aR) + Math.Sin(DspMath.TwoPi * _phS1bR));
            }

            // A1 ark: brown -> LP 160 Hz; sine 41.2 Hz swell; whoosh pink -> BP 180-600 Hz Q 2
            if (On(LA1))
            {
                double g = _g[LA1];
                _phA41 += 41.2 * _invFs; if (_phA41 >= 1.0) _phA41 -= 1.0;
                double swellA = 0.6 + 0.4 * Math.Sin(DspMath.TwoPi * 0.05 * _n * _invFs);
                double sine = A1SineAmp * swellA * Math.Sin(DspMath.TwoPi * _phA41);
                double rl = A1RumbleNorm * _lpAL.Low(_brAL.Next(_rngAL.Bipolar())), rr = A1RumbleNorm * _lpAR.Low(_brAR.Next(_rngAR.Bipolar()));
                double wl = A1WhooshW * A1WhooshNorm * _bpAWL.BandNorm(_pinkAWL.Next(_rngAWL.Bipolar()));
                double wr = A1WhooshW * A1WhooshNorm * _bpAWR.BandNorm(_pinkAWR.Next(_rngAWR.Bipolar()));
                l += g * _arkPanL * (rl + sine + wl);
                r += g * _arkPanR * (rr + sine + wr);
            }

            // P1 dusk pad: D2 A2 D3 E3 A3 sines + triangles +-3 cents (L +, R -) -> LP 900 Hz
            if (On(LP1))
            {
                double g = _g[LP1];
                double sl = 0, sr = 0;
                const double up = 1.0017345, dn = 0.9982684;   // 2^(+-3/1200)
                for (int i = 0; i < 5; i++)
                {
                    double f = PadF[i];
                    _phPadS[i] += f * _invFs; if (_phPadS[i] >= 1.0) _phPadS[i] -= 1.0;
                    _phPadTL[i] += f * up * _invFs; if (_phPadTL[i] >= 1.0) _phPadTL[i] -= 1.0;
                    _phPadTR[i] += f * dn * _invFs; if (_phPadTR[i] >= 1.0) _phPadTR[i] -= 1.0;
                    double sn = Math.Sin(DspMath.TwoPi * _phPadS[i]);
                    double tl = 1.0 - 4.0 * Math.Abs(_phPadTL[i] - 0.5), tr = 1.0 - 4.0 * Math.Abs(_phPadTR[i] - 0.5);
                    sl += PadW[i] * (sn + 0.4 * tl);
                    sr += PadW[i] * (sn + 0.4 * tr);
                }
                l += g * _lpPL.Low(sl);
                r += g * _lpPR.Low(sr);
            }

            // N1 bells: 1174.7 + 880 Hz, T60 5 s, every 7-13 s
            if (On(LN1))
            {
                double g = _g[LN1];
                for (int i = 0; i < 3; i++)
                {
                    if (!_bellA[i].Active && !_bellB[i].Active) continue;
                    double b = _bellA[i].Next() + _bellB[i].Next() + _bellC[i].Next();
                    l += g * b * _bellPL[i]; r += g * b * _bellPR[i];
                }
            }

            // N2 shimmer: white -> BP 5-9 kHz (LFO 0.05 Hz)
            if (On(LN2))
            {
                double g = _g[LN2] * _n2Comp;
                l += g * _bpNL.BandNorm(_rngNL.Bipolar());
                r += g * _bpNR.BandNorm(_rngNR.Bipolar());
            }

            // U1 underpass: brown -> LP 120 Hz
            if (On(LU1))
            {
                double g = _g[LU1];
                l += g * _lpUL.Low(_brUL.Next(_rngUL.Bipolar()));
                r += g * _lpUR.Low(_brUR.Next(_rngUR.Bipolar()));
            }

            // H1 wall hum: sine 98.0 Hz + brown -> LP 220 Hz
            if (On(LH1Sine))
            {
                _phH1 += 98.0 * _invFs; if (_phH1 >= 1.0) _phH1 -= 1.0;
                double h = 1.4142135623730951 * _g[LH1Sine] * Math.Sin(DspMath.TwoPi * _phH1);
                l += h; r += h;
            }
            if (On(LH1Brown))
            {
                double g = _g[LH1Brown];
                l += g * _lpHL.Low(_brHL.Next(_rngHL.Bipolar()));
                r += g * _lpHR.Low(_brHR.Next(_rngHR.Bipolar()));
            }

            // T1 tunnel: brown -> LP 120 Hz; rib ticks; aeolian 196 Hz with 4.6 Hz +-3 cent vibrato
            if (On(LT1))
            {
                double g = _g[LT1];
                l += g * _lpTL.Low(_brTL.Next(_rngTL.Bipolar()));
                r += g * _lpTR.Low(_brTR.Next(_rngTR.Bipolar()));
            }
            if (On(LTick))
            {
                double g = _g[LTick];
                for (int i = 0; i < _ticks.Length; i++)
                {
                    if (!_ticks[i].Active) continue;
                    _ticks[i].Next(out double a, out double b); l += g * a; r += g * b;
                }
            }
            if (On(LAeol))
            {
                _phVib += 4.6 * _invFs; if (_phVib >= 1.0) _phVib -= 1.0;
                double f = 196.0 * (1.0 + 0.0017345 * Math.Sin(DspMath.TwoPi * _phVib));
                _phAeol += f * _invFs; if (_phAeol >= 1.0) _phAeol -= 1.0;
                double a = 1.4142135623730951 * _g[LAeol] * Math.Sin(DspMath.TwoPi * _phAeol);
                l += a; r += a;
            }

            // X1 star drone (A1 E2 A2 C#3 E3, right channel +0.07 Hz) and grains (2-6 kHz, 60 ms, 40/s, random pan)
            if (On(LX1))
            {
                double g = _g[LX1] * _droneNorm;
                double dl = 0, dr = 0;
                for (int i = 0; i < 5; i++)
                {
                    _phDL[i] += DroneF[i] * _invFs; if (_phDL[i] >= 1.0) _phDL[i] -= 1.0;
                    _phDR[i] += (DroneF[i] + 0.07) * _invFs; if (_phDR[i] >= 1.0) _phDR[i] -= 1.0;
                    dl += DroneW[i] * Math.Sin(DspMath.TwoPi * _phDL[i]);
                    dr += DroneW[i] * Math.Sin(DspMath.TwoPi * _phDR[i]);
                }
                l += g * dl; r += g * dr;
            }
            if (On(LXGrain))
            {
                double g = _g[LXGrain];
                for (int i = 0; i < _xGrains.Length; i++)
                {
                    if (!_xGrains[i].Active) continue;
                    _xGrains[i].Next(out double a, out double b); l += g * a; r += g * b;
                }
            }

            // B1 beacon pings
            if (On(LB1))
            {
                double g = _g[LB1];
                double b = _pings[0].Next() + _pings[1].Next();
                l += g * b; r += g * b;
            }

            // footsteps and hatch (absolute peak levels, not trimmed)
            if (CalibrateLayer < 0)
            {
                for (int i = 0; i < _steps.Length; i++)
                {
                    if (!_steps[i].Active) continue;
                    _steps[i].Next(out double a, out double b); l += a; r += b;
                }
                if (_hatch.Active) { _hatch.Next(out double a, out double b); l += a; r += b; }
            }
            outL = l; outR = r;
        }
    }
}
