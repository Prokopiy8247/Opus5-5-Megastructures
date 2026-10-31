using System;

namespace Megastructure
{
    // Pooled one-shot voices of the RingAudioEngine. All are structs living in preallocated arrays (no allocation on the
    // audio thread). Each Next() returns a stereo sample pair through out parameters.

    /// <summary>Exponentially decaying sine with an optional raised-cosine attack (bells, beacon, footstep bodies).</summary>
    public struct DspDecaySine
    {
        double _ph, _inc, _env, _mul, _amp;
        int _atk, _atkLen;
        public bool Active;

        public void Start(double fs, double freq, double amp, double t60, double attackSec, double phase0 = 0.0)
        {
            _ph = phase0; _inc = freq / fs; _amp = amp; _env = 1.0; _mul = DspMath.T60Mul(t60, fs);
            _atkLen = attackSec > 0.0 ? Math.Max(1, (int)(attackSec * fs)) : 0; _atk = 0; Active = amp > 0.0;
        }

        public double Next()
        {
            if (!Active) return 0.0;
            double a = 1.0;
            if (_atk < _atkLen) { a = 0.5 - 0.5 * Math.Cos(Math.PI * _atk / _atkLen); _atk++; }
            double s = _amp * _env * a * Math.Sin(DspMath.TwoPi * _ph);
            _ph += _inc; if (_ph >= 1.0) _ph -= 1.0;
            _env *= _mul;
            if (_env < 1e-5) Active = false;     // -100 dB: stop (no denormals)
            return s;
        }
    }

    /// <summary>Two-pole resonator, impulse response r^n sin((n + 1) w) (peak about 1).</summary>
    public struct DspReso
    {
        double _y1, _y2, _b0, _c1, _c2;
        public void Set(double f, double t60, double fs)
        {
            double w = DspMath.TwoPi * Math.Min(f, 0.45 * fs) / fs, r = DspMath.T60Mul(t60, fs);
            _b0 = Math.Sin(w); _c1 = 2.0 * r * Math.Cos(w); _c2 = -r * r; _y1 = _y2 = 0.0;
        }
        public double Tick(double x) { double y = _b0 * x + _c1 * _y1 + _c2 * _y2; _y2 = _y1; _y1 = y; return y; }
        public double Energy => _y1 * _y1 + _y2 * _y2;
    }

    /// <summary>Hann-windowed sine grain (river droplets, X1 star grains): click-free by construction.</summary>
    public struct DspGrain
    {
        double _ph, _inc, _amp, _pl, _pr;
        int _i, _len;
        public bool Active;

        public void Start(double fs, double freq, double amp, double dur, double pan)
        {
            _ph = 0.0; _inc = freq / fs; _amp = amp; _len = Math.Max(8, (int)(dur * fs)); _i = 0; Active = true;
            DspMath.Pan(pan, out _pl, out _pr);
        }

        public void Next(out double l, out double r)
        {
            if (!Active) { l = r = 0.0; return; }
            double w = 0.5 - 0.5 * Math.Cos(DspMath.TwoPi * _i / _len);
            double s = _amp * w * Math.Sin(DspMath.TwoPi * _ph);
            _ph += _inc; if (_ph >= 1.0) _ph -= 1.0;
            _i++; if (_i >= _len) Active = false;
            l = s * _pl; r = s * _pr;
        }
    }

    /// <summary>
    /// Footstep voice (14.3 "Footsteps"): a Hann-windowed noise burst into band-passes / resonators plus damped sines,
    /// per surface. Gain +-0.8 dB, pitch +-4 % per step, alternate feet (pan +-0.12, slight timbre change).
    /// Every step is peak-normalised exactly: Start() probe-renders a value copy of the voice over its first 150 ms
    /// and scales it so the output peak is -30 dBFS (times the +-0.8 dB variation and +1 dB at brisk speed).
    /// </summary>
    public struct DspFootstep
    {
        const double ProbeSec = 0.150;

        int _surface, _i, _burstLen, _len;
        double _gain, _pl, _pr;
        DspRng _rng;
        DspSvf _bp;
        DspReso _r0, _r1, _r2;
        DspDecaySine _d0, _d1;
        double _w0, _w1, _w2, _e0, _e1, _e2;
        double _invBurst;
        public bool Active;

        public void Start(double fs, int surface, int foot, double speed, uint seed)
        {
            _surface = surface < 0 || surface >= AudioSpec.SurfCount ? AudioSpec.SurfCeramic : surface;
            _rng = new DspRng(seed);
            double u1 = _rng.Range(-1.0, 1.0), u2 = _rng.Range(-1.0, 1.0);
            double pitch = (1.0 + 0.04 * u2) * (foot == 1 ? 0.985 : 1.0);
            double gainDb = 0.8 * u1 + DspMath.Clamp((speed - AudioSpec.Walk) / (AudioSpec.Brisk - AudioSpec.Walk), 0.0, 1.0) * 1.0;
            double gainLin = DspMath.DbToLin(gainDb) * DspMath.DbToLin(AudioSpec.FootPeakDb);
            _gain = 1.0;
            DspMath.Pan(foot == 1 ? 0.12 : -0.12, out _pl, out _pr);
            _i = 0;
            _w0 = _w1 = _w2 = 0.0; _e0 = _e1 = _e2 = 1.0;
            _d0.Active = false; _d1.Active = false;
            switch (_surface)
            {
                case AudioSpec.SurfMetal:
                case AudioSpec.SurfMetalRamp:
                {
                    // 3 ms noise into resonators 210 / 540 / 1300 Hz, T60 120 / 70 / 40 ms; the ramp is a little hollower
                    bool ramp = _surface == AudioSpec.SurfMetalRamp;
                    _burstLen = (int)(0.003 * fs);
                    _r0.Set(210.0 * pitch, 0.120, fs); _r1.Set(540.0 * pitch, 0.070, fs); _r2.Set(1300.0 * pitch, 0.040, fs);
                    _w0 = ramp ? 1.15 : 1.0; _w1 = ramp ? 0.70 : 0.8; _w2 = ramp ? 0.40 : 0.5;
                    _len = (int)(0.60 * fs);
                    // A 3 ms noise burst has one random coefficient per resonance (Rayleigh amplitude, about 9 dB spread
                    // between steps). Replay the burst once and normalise each resonator's drive by the burst's DFT
                    // magnitude at its frequency, so every step rings at the same level (the noise still sets the colour).
                    NormaliseDrive(fs, 210.0 * pitch, 540.0 * pitch, 1300.0 * pitch);
                    break;
                }
                case AudioSpec.SurfGrass:
                {
                    // noise BP 1.8 kHz Q 0.6 under a 110 ms envelope (10 ms attack)
                    _burstLen = (int)(0.110 * fs);
                    _bp.Reset(); _bp.Set(1800.0 * pitch, 0.6, fs);
                    _len = _burstLen + 8;
                    break;
                }
                case AudioSpec.SurfStone:
                {
                    // 3.1 kHz tick + 220 Hz (80 ms)
                    _burstLen = (int)(0.003 * fs);
                    _bp.Reset(); _bp.Set(3100.0 * pitch, 3.0, fs);
                    _d0.Start(fs, 220.0 * pitch, 0.9, 0.080, 0.0015);
                    _len = (int)(0.25 * fs);
                    break;
                }
                default:
                {
                    // ceramic family: 2.4 kHz noise 6 ms + damped sine 380 Hz (60 ms); bridge adds 85 Hz (180 ms),
                    // deck adds 220 Hz (90 ms); underpass and tunnel are ceramic into the local reverb
                    _burstLen = (int)(0.006 * fs);
                    _bp.Reset(); _bp.Set(2400.0 * pitch, 1.2, fs);
                    _d0.Start(fs, 380.0 * pitch, 0.60, 0.060, 0.0015);
                    if (_surface == AudioSpec.SurfBridge) _d1.Start(fs, 85.0 * pitch, 0.80, 0.180, 0.004);
                    else if (_surface == AudioSpec.SurfDeck) _d1.Start(fs, 220.0 * pitch, 0.55, 0.090, 0.002);
                    _len = (int)((_surface == AudioSpec.SurfBridge ? 0.9 : 0.45) * fs);
                    break;
                }
            }
            if (_burstLen < 4) _burstLen = 4;
            _invBurst = 1.0 / _burstLen;
            Active = true;
            // exact peak normalisation on a value copy (identical RNG and filter states)
            var probe = this;
            double pk = 0.0;
            int n = Math.Min(_len, (int)(ProbeSec * fs));
            for (int i = 0; i < n && probe.Active; i++)
            {
                probe.Next(out double pl, out double pr);
                double a = Math.Max(Math.Abs(pl), Math.Abs(pr));
                if (a > pk) pk = a;
            }
            _gain = gainLin / Math.Max(pk, 1e-9);
        }

        void NormaliseDrive(double fs, double f0, double f1, double f2)
        {
            int n = (int)(0.003 * fs); if (n < 4) n = 4;
            var rr = _rng;   // value copy: Next() replays the identical sequence
            double c0 = 0, s0 = 0, c1 = 0, s1 = 0, c2 = 0, s2 = 0;
            double k0 = DspMath.TwoPi * f0 / fs, k1 = DspMath.TwoPi * f1 / fs, k2 = DspMath.TwoPi * f2 / fs;
            for (int i = 0; i < n; i++)
            {
                double x = rr.Bipolar() * (0.5 - 0.5 * Math.Cos(DspMath.TwoPi * i / n));
                c0 += x * Math.Cos(k0 * i); s0 += x * Math.Sin(k0 * i);
                c1 += x * Math.Cos(k1 * i); s1 += x * Math.Sin(k1 * i);
                c2 += x * Math.Cos(k2 * i); s2 += x * Math.Sin(k2 * i);
            }
            double refM = Math.Sqrt(0.375 * n / 3.0);
            _e0 = refM / Math.Max(0.15 * refM, Math.Sqrt(c0 * c0 + s0 * s0));
            _e1 = refM / Math.Max(0.15 * refM, Math.Sqrt(c1 * c1 + s1 * s1));
            _e2 = refM / Math.Max(0.15 * refM, Math.Sqrt(c2 * c2 + s2 * s2));
        }

        public void Next(out double l, out double r)
        {
            if (!Active) { l = r = 0.0; return; }
            double x = 0.0;
            double burst = 0.0;
            if (_i < _burstLen)
            {
                double ph = _i * _invBurst;
                double w;
                if (_surface == AudioSpec.SurfGrass)
                {
                    // 10 ms raised-cosine attack, cos^2 decay to zero at 110 ms
                    const double ta = 0.010 / 0.110;
                    if (ph < ta) w = 0.5 - 0.5 * Math.Cos(Math.PI * ph / ta);
                    else { double c = Math.Cos(0.5 * Math.PI * (ph - ta) / (1.0 - ta)); w = c * c; }
                }
                else w = 0.5 - 0.5 * Math.Cos(DspMath.TwoPi * ph);
                burst = _rng.Bipolar() * w;
            }
            switch (_surface)
            {
                case AudioSpec.SurfMetal:
                case AudioSpec.SurfMetalRamp:
                    x = _w0 * _r0.Tick(burst * _e0) + _w1 * _r1.Tick(burst * _e1) + _w2 * _r2.Tick(burst * _e2);
                    break;
                case AudioSpec.SurfGrass:
                    x = _bp.BandNorm(burst);
                    break;
                default:
                    x = _bp.BandNorm(burst) + _d0.Next() + _d1.Next();
                    break;
            }
            x *= _gain;
            _i++;
            if (_i >= _len) Active = false;
            l = x * _pl; r = x * _pr;
        }
    }

    /// <summary>
    /// Hatch voice (C1, 14.3): white noise through a low-pass swept 400 -> 6 000 Hz over 1.4 s (peak -30 dBFS) and a
    /// servo sine gliding 310 -> 520 Hz over 1.2 s. Raised-cosine attacks, cos^2 releases. The sweep is peak-normalised
    /// exactly like the footsteps: Start() probe-renders a value copy (servo off) and scales the noise so its sample
    /// peak is -30 dBFS for every seed (a noise peak otherwise varies by about +-2 dB). One probe per walk (~70 k
    /// cheap iterations on the audio thread at the hatch trigger).
    /// </summary>
    public struct DspHatch
    {
        public const double NormSweep = 1.2130; // start scale before the exact normalisation (peak -30 dBFS for seed 123)
        DspRng _rngL, _rngR;
        DspSvf _lpL, _lpR;
        double _ph, _t, _invFs, _fs, _gSweep, _gServo;
        int _ctl;
        public bool Active;
        /// <summary>The noise-sweep part of the last sample (diagnostics and tests; the servo is identical in L and R).</summary>
        public double LastSweepL, LastSweepR;

        public void Start(double fs, uint seed)
        {
            _fs = fs; _invFs = 1.0 / fs; _t = 0.0; _ph = 0.0; _ctl = 0;
            _rngL = new DspRng(DspHash.Seed(seed, 11u)); _rngR = new DspRng(DspHash.Seed(seed, 12u));
            _lpL.Reset(); _lpR.Reset();
            _lpL.Set(400.0, 0.9, fs); _lpR.Set(400.0, 0.9, fs);
            _gSweep = DspMath.DbToLin(AudioSpec.HatchSweepPeakDb) * NormSweep;
            _gServo = 1.4142135623730951 * DspMath.DbToLin(AudioSpec.HatchServoDb);
            Active = true;
            // exact peak normalisation of the sweep on a value copy (identical RNG and filter states)
            var probe = this;
            probe._gServo = 0.0;
            double pk = 0.0;
            int guard = (int)(2.0 * fs);
            while (probe.Active && guard-- > 0)
            {
                probe.Next(out double l, out double r);
                double a = Math.Max(Math.Abs(l), Math.Abs(r));
                if (a > pk) pk = a;
            }
            if (pk > 1e-9) _gSweep *= DspMath.DbToLin(AudioSpec.HatchSweepPeakDb) / pk;
        }

        public void Next(out double l, out double r)
        {
            if (!Active) { l = r = 0.0; return; }
            double t = _t;
            // sweep envelope: 80 ms attack, hold, cos^2 release over 1.0 .. 1.4 s
            double ea = t < 0.08 ? 0.5 - 0.5 * Math.Cos(Math.PI * t / 0.08) : 1.0;
            double er = t < 1.0 ? 1.0 : (t < 1.4 ? Math.Cos(0.5 * Math.PI * (t - 1.0) / 0.4) : 0.0);
            double sweepEnv = ea * er * er;
            if ((_ctl++ & 15) == 0)
            {
                double x = DspMath.Clamp01(t / 1.4);
                double fc = 400.0 * Math.Pow(6000.0 / 400.0, x);
                _lpL.Set(fc, 0.9, _fs); _lpR.Set(fc, 0.9, _fs);
            }
            double gSweep = _gSweep * sweepEnv;
            double nl = _lpL.Low(_rngL.Bipolar()) * gSweep;
            double nr = _lpR.Low(_rngR.Bipolar()) * gSweep;
            LastSweepL = nl; LastSweepR = nr;
            // servo: exponential glide 310 -> 520 Hz over 1.2 s, 60 ms attack, 150 ms release
            double servo = 0.0;
            if (t < 1.2)
            {
                double x = t / 1.2;
                double f = 310.0 * Math.Pow(520.0 / 310.0, x);
                _ph += f * _invFs; if (_ph >= 1.0) _ph -= 1.0;
                double a = t < 0.06 ? 0.5 - 0.5 * Math.Cos(Math.PI * t / 0.06) : 1.0;
                double rr = t > 1.05 ? Math.Cos(0.5 * Math.PI * (t - 1.05) / 0.15) : 1.0;
                double env = a * rr * rr;
                double s = Math.Sin(DspMath.TwoPi * _ph);
                servo = _gServo * env * (0.92 * s + 0.08 * Math.Sin(2.0 * DspMath.TwoPi * _ph));
            }
            l = nl + servo; r = nr + servo;
            _t += _invFs;
            if (_t > 1.45) Active = false;
        }
    }

    /// <summary>
    /// Rib tick (T1, 14.3 "rib tick (BP 3 kHz click, 2 ms)"): a 2 ms Hann-windowed 3 kHz burst with a little noise
    /// texture, through a band-pass (3 kHz, Q 2.5) and a 6 kHz low-pass so it stays band-limited (no broadband edge).
    /// </summary>
    public struct DspClick
    {
        /// <summary>Raw-to-unit normalisation: the RMS over the click's 2 ms burst becomes 1 (measured at 48 kHz; the
        /// tick level of 14.3 (-54 dBFS) is that RMS).</summary>
        public const double Norm = 2.7245;
        DspRng _rng;
        DspSvf _bp, _lp;
        double _pl, _pr, _inv, _ph, _inc;
        int _i, _burst, _len;
        public bool Active;

        public void Start(double fs, double freq, double dur, double pan, uint seed)
        {
            _rng = new DspRng(seed);
            _bp.Reset(); _bp.Set(freq, 2.5, fs);
            _lp.Reset(); _lp.Set(6000.0, 0.707, fs);
            _burst = Math.Max(8, (int)(dur * fs)); _len = _burst + (int)(0.004 * fs); _i = 0; _inv = 1.0 / _burst;
            _ph = 0.0; _inc = freq / fs;
            DspMath.Pan(pan, out _pl, out _pr);
            Active = true;
        }

        public void Next(out double l, out double r)
        {
            if (!Active) { l = r = 0.0; return; }
            double x = 0.0;
            if (_i < _burst)
            {
                double w = 0.5 - 0.5 * Math.Cos(DspMath.TwoPi * _i * _inv);
                x = w * (Math.Sin(DspMath.TwoPi * _ph) + 0.35 * _rng.Bipolar());
                _ph += _inc; if (_ph >= 1.0) _ph -= 1.0;
            }
            double y = Norm * _lp.Low(_bp.BandNorm(x));
            _i++; if (_i >= _len) Active = false;
            l = y * _pl; r = y * _pr;
        }
    }
}
