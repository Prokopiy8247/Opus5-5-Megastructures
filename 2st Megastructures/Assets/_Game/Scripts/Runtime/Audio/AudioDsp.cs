using System;

namespace Megastructure
{
    // Unity-free DSP building blocks (docs/research/05_procedural_audio.md section 6). Every state is double precision,
    // every block is allocation-free after construction, and nothing here touches UnityEngine.

    /// <summary>Seed mixing (splitmix64 finaliser) so every noise source gets an independent, deterministic stream.</summary>
    public static class DspHash
    {
        public static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public static uint Seed(uint a, uint b, uint c = 0u)
        {
            unchecked
            {
                ulong z = Mix(((ulong)a << 32) ^ b);
                z = Mix(z ^ c);
                uint s = (uint)(z ^ (z >> 32));
                return s == 0u ? 0x9E3779B9u : s;
            }
        }

        /// <summary>Uniform [0, 1) from (seed, index).</summary>
        public static double U01(uint seed, long i)
        {
            unchecked
            {
                ulong z = Mix((ulong)i * 0x9E3779B97F4A7C15UL + seed * 0xD1B54A32D192ED03UL);
                return (z >> 11) * (1.0 / 9007199254740992.0);
            }
        }
    }

    /// <summary>xorshift32 white noise (Marsaglia 2003). Uniform [-1, 1): RMS 1/sqrt(3).</summary>
    public struct DspRng
    {
        uint _s;
        public DspRng(uint seed) { _s = seed == 0u ? 0x9E3779B9u : seed; }
        public uint NextU() { uint s = _s; s ^= s << 13; s ^= s >> 17; s ^= s << 5; _s = s; return s; }
        public double Bipolar() => (int)NextU() * (1.0 / 2147483648.0);
        public double Next01() => NextU() * (1.0 / 4294967296.0);
        /// <summary>Uniform in [-1, 1].</summary>
        public double Range(double a, double b) => a + (b - a) * Next01();
    }

    /// <summary>Paul Kellet's refined pink filter ("pk3", +-0.05 dB above 9.2 Hz at 44.1 kHz), fed with white noise.</summary>
    public struct DspPink
    {
        double _b0, _b1, _b2, _b3, _b4, _b5, _b6;
        public double Next(double white)
        {
            _b0 = 0.99886 * _b0 + white * 0.0555179;
            _b1 = 0.99332 * _b1 + white * 0.0750759;
            _b2 = 0.96900 * _b2 + white * 0.1538520;
            _b3 = 0.86650 * _b3 + white * 0.3104856;
            _b4 = 0.55000 * _b4 + white * 0.5329522;
            _b5 = -0.7616 * _b5 - white * 0.0168980;
            double pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362;
            _b6 = white * 0.115926;
            return pink * 0.11;
        }
        public void Reset() { _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = _b6 = 0.0; }
    }

    /// <summary>Leaky-integrator brown noise (leak 0.02, noisehack): -6 dB/oct above about 150 Hz at 48 kHz.</summary>
    public struct DspBrown
    {
        double _last;
        public double Next(double white) { _last = (_last + 0.02 * white) / 1.02; return _last * 3.5; }
        public void Reset() { _last = 0.0; }
    }

    /// <summary>
    /// Linear trapezoidal (TPT / zero-delay-feedback) state-variable filter, Simper's optimised form
    /// (cytomic.com SvfLinearTrapOptimised2.pdf; equivalent to Zavalishin's TPT SVF with k = 2R = 1/Q).
    /// Stable under per-sample coefficient modulation. BandNorm has 0 dB gain at fc.
    /// </summary>
    public struct DspSvf
    {
        double _ic1, _ic2, _a1, _a2, _a3, _k;

        public void Set(double fc, double q, double fs)
        {
            double nyq = 0.45 * fs;
            if (!(fc > 5.0)) fc = 5.0; else if (fc > nyq) fc = nyq;
            if (!(q > 0.1)) q = 0.1;
            double g = Math.Tan(Math.PI * fc / fs);
            _k = 1.0 / q;
            _a1 = 1.0 / (1.0 + g * (g + _k));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public void Tick(double v0, out double lp, out double bp, out double hp)
        {
            double v3 = v0 - _ic2;
            double v1 = _a1 * _ic1 + _a2 * v3;
            double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2.0 * v1 - _ic1;
            _ic2 = 2.0 * v2 - _ic2;
            lp = v2; bp = v1; hp = v0 - _k * v1 - v2;
        }

        public double Low(double v0)
        {
            double v3 = v0 - _ic2;
            double v1 = _a1 * _ic1 + _a2 * v3;
            double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2.0 * v1 - _ic1; _ic2 = 2.0 * v2 - _ic2;
            return v2;
        }

        public double High(double v0)
        {
            double v3 = v0 - _ic2;
            double v1 = _a1 * _ic1 + _a2 * v3;
            double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2.0 * v1 - _ic1; _ic2 = 2.0 * v2 - _ic2;
            return v0 - _k * v1 - v2;
        }

        public double BandNorm(double v0)
        {
            double v3 = v0 - _ic2;
            double v1 = _a1 * _ic1 + _a2 * v3;
            double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2.0 * v1 - _ic1; _ic2 = 2.0 * v2 - _ic2;
            return _k * v1;
        }

        /// <summary>Flushes tiny states (denormal insurance, research 05 pitfall 11).</summary>
        public void Flush() { if (Math.Abs(_ic1) < 1e-20) _ic1 = 0.0; if (Math.Abs(_ic2) < 1e-20) _ic2 = 0.0; }
        public void Reset() { _ic1 = _ic2 = 0.0; }
    }

    /// <summary>DC blocker y = x - x1 + R y1 (JOS), R = exp(-2 pi fc / fs): 0.997385 for 20 Hz at 48 kHz.</summary>
    public struct DspDcBlock
    {
        double _x1, _y1, _r;
        public void Set(double fc, double fs) { _r = Math.Exp(-2.0 * Math.PI * fc / fs); }
        public double Tick(double x) { double y = x - _x1 + _r * _y1; _x1 = x; _y1 = Math.Abs(y) < 1e-25 ? 0.0 : y; return y; }
        public void Reset() { _x1 = _y1 = 0.0; }
    }

    /// <summary>One-pole smoother y += a (target - y), a = 1 - exp(-1 / (tau fs)).</summary>
    public struct DspSmooth
    {
        public double V;
        double _a;
        public void Set(double tau, double fs) { _a = tau <= 0.0 ? 1.0 : 1.0 - Math.Exp(-1.0 / (tau * fs)); }
        public void SetPerStep(double tau, double stepsPerSecond) { _a = tau <= 0.0 ? 1.0 : 1.0 - Math.Exp(-1.0 / (tau * stepsPerSecond)); }
        public double Next(double target) { V += _a * (target - V); return V; }
    }

    /// <summary>
    /// Smooth random (interpolated value noise, 3 octaves 1, 1/2, 1/4, quintic), a pure function of time so the main
    /// thread (RingState.Gust, grass sway) and the audio thread compute the identical W1 gust (research 05 section 6.6).
    /// </summary>
    public static class DspSmoothRandom
    {
        static double Value(uint seed, double t)
        {
            double fl = Math.Floor(t);
            long i = (long)fl;
            double f = t - fl;
            double u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
            double a = DspHash.U01(seed, i) * 2.0 - 1.0, b = DspHash.U01(seed, i + 1) * 2.0 - 1.0;
            return a + (b - a) * u;
        }

        /// <summary>[-1, 1] with about rateHz independent values per second.</summary>
        public static double Get(uint seed, double timeSec, double rateHz)
        {
            double t = timeSec * rateHz;
            unchecked
            {
                return (Value(seed, t) + 0.5 * Value(seed + 0x51u, 2.0 * t + 0.37) + 0.25 * Value(seed + 0xA3u, 4.0 * t + 0.71)) / 1.75;
            }
        }

        public static double Get01(uint seed, double timeSec, double rateHz) => 0.5 + 0.5 * Get(seed, timeSec, rateHz);
    }

    /// <summary>Gust model of W1 (14.3): gain 0.35 + 0.65 smooth-random(0.15 Hz); _Gust is the 0..1 smooth-random value.</summary>
    public static class DspGust
    {
        public const double Rate = 0.15, Floor = 0.35, Depth = 0.65;
        public static double Gust01(uint seed, double t) => DspSmoothRandom.Get01(seed, t, Rate);
        public static double Gain(uint seed, double t) => Floor + Depth * Gust01(seed, t);
    }

    /// <summary>Scalar helpers (System.Math only).</summary>
    public static class DspMath
    {
        public const double TwoPi = 2.0 * Math.PI;
        public static double Clamp01(double x) => x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x);
        public static double Clamp(double x, double a, double b) => x < a ? a : (x > b ? b : x);
        public static double Smootherstep01(double t) { t = Clamp01(t); return t * t * t * (t * (t * 6.0 - 15.0) + 10.0); }
        public static double Smoothstep01(double t) { t = Clamp01(t); return t * t * (3.0 - 2.0 * t); }

        /// <summary>HLSL-style smootherstep (e0 may exceed e1).</summary>
        public static double SS(double e0, double e1, double x)
        {
            double d = e1 - e0;
            if (Math.Abs(d) < 1e-12) return x >= e1 ? 1.0 : 0.0;
            return Smootherstep01((x - e0) / d);
        }

        public static double Smoothstep(double e0, double e1, double x)
        {
            double d = e1 - e0;
            if (Math.Abs(d) < 1e-12) return x >= e1 ? 1.0 : 0.0;
            return Smoothstep01((x - e0) / d);
        }

        public static double DbToLin(double db) => Math.Pow(10.0, db / 20.0);
        public static double LinToDb(double v) => 20.0 * Math.Log10(Math.Max(v, 1e-12));
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double Frac(double x) => x - Math.Floor(x);

        /// <summary>Smooth maximum of 14.7 H8 (k = 0.03).</summary>
        public static double Smax(double a, double b, double k = 0.03)
        {
            if (double.IsNegativeInfinity(a)) return b;
            if (double.IsNegativeInfinity(b)) return a;
            double d = a - b;
            return 0.5 * (a + b + Math.Sqrt(d * d + k * k));
        }

        /// <summary>Equal-power pan normalised to unity per channel at centre: p in [-1, 1].</summary>
        public static void Pan(double p, out double l, out double r)
        {
            p = Clamp(p, -1.0, 1.0);
            double a = 0.25 * Math.PI * (1.0 + p);
            l = 1.4142135623730951 * Math.Cos(a);
            r = 1.4142135623730951 * Math.Sin(a);
        }

        /// <summary>Per-sample multiplier for a T60 decay.</summary>
        public static double T60Mul(double t60, double fs) => Math.Exp(-6.907755278982137 / (Math.Max(1e-4, t60) * fs));
    }

    /// <summary>
    /// Stereo-linked look-ahead peak limiter (Signalsmith design): required gain per sample with a tanh knee between
    /// threshold and ceiling, moving minimum over the look-ahead window, instant attack / exponential release that
    /// never exceeds the moving minimum, box smoothing of the same length, audio delayed by L - 1 samples. For sample
    /// values this guarantees |y| &lt;= ceiling; the -1.2 dBFS internal ceiling keeps the true peak under -1 dBTP.
    /// Buffers are sized for 192 kHz up front so a sample-rate change never allocates on the audio thread.
    /// </summary>
    public sealed class DspLimiter
    {
        const int Cap = 1024;   // 5 ms at 192 kHz = 960
        int _len;
        double _thrDb, _ceilDb, _thrLin, _relCoef;
        readonly double[] _dl = new double[Cap], _dr = new double[Cap];
        readonly double[] _qv = new double[Cap];
        readonly long[] _qi = new long[Cap];
        readonly double[] _box = new double[Cap];
        int _wp, _qh, _qn, _bp;
        double _boxSum, _gRel = 1.0;
        long _n;
        public double MinGain = 1.0;          // diagnostics: lowest gain applied since the last reset of this field

        public int LatencySamples => _len - 1;

        public void Configure(double fs, double thresholdDb, double ceilingDb, double lookaheadMs, double releaseMs)
        {
            _len = (int)Math.Round(lookaheadMs * 0.001 * fs);
            if (_len < 2) _len = 2; else if (_len > Cap) _len = Cap;
            _thrDb = thresholdDb; _ceilDb = ceilingDb; _thrLin = Math.Pow(10.0, thresholdDb / 20.0);
            _relCoef = 1.0 - Math.Exp(-1.0 / (releaseMs * 0.001 * fs));
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < Cap; i++) { _dl[i] = 0.0; _dr[i] = 0.0; _box[i] = 1.0; _qv[i] = 1.0; _qi[i] = 0; }
            _wp = 0; _qh = 0; _qn = 0; _bp = 0; _boxSum = _len; _gRel = 1.0; _n = 0;
        }

        double Required(double p)
        {
            if (p <= _thrLin) return 1.0;
            double pDb = 20.0 * Math.Log10(p), range = _ceilDb - _thrDb;
            double outDb = _thrDb + range * Math.Tanh((pDb - _thrDb) / range);
            return Math.Pow(10.0, (outDb - pDb) / 20.0);
        }

        public void Process(ref double l, ref double r)
        {
            int L = _len;
            double al = l < 0 ? -l : l, ar = r < 0 ? -r : r;
            double req = Required(al > ar ? al : ar);
            while (_qn > 0 && _qi[_qh] <= _n - L) { _qh++; if (_qh == L) _qh = 0; _qn--; }
            while (_qn > 0)
            {
                int last = _qh + _qn - 1; if (last >= L) last -= L;
                if (_qv[last] >= req) _qn--; else break;
            }
            int t = _qh + _qn; if (t >= L) t -= L;
            _qv[t] = req; _qi[t] = _n; _qn++;
            double m = _qv[_qh];
            _gRel = m < _gRel ? m : _gRel + (m - _gRel) * _relCoef;
            _boxSum += _gRel - _box[_bp]; _box[_bp] = _gRel; _bp++; if (_bp == L) _bp = 0;
            if ((_n & 0xFFFF) == 0) { double s = 0; for (int i = 0; i < L; i++) s += _box[i]; _boxSum = s; }
            double g = _boxSum / L;
            if (g < MinGain) MinGain = g;
            _dl[_wp] = l; _dr[_wp] = r; _wp++; if (_wp == L) _wp = 0;
            l = _dl[_wp] * g; r = _dr[_wp] * g;
            _n++;
        }
    }
}
