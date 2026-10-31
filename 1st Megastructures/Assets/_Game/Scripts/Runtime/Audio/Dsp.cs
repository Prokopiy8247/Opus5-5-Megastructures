using System;

namespace Megastructure.Audio
{
    // =====================================================================================================================
    // Unity-free DSP building blocks for the procedural score (MASTER_PROMPT 14.3, 11.9).
    //
    // Nothing in this file touches UnityEngine: EditMode tests, offline renders (-render-audio) and the audio thread
    // (OnAudioFilterRead) all run exactly the same code. Every block is allocation-free after construction, keeps its
    // recursive state in double precision and is deterministic for a given seed.
    //
    // References (implemented from the published descriptions, no code copied):
    // - TPT / Simper state-variable filter: A. Simper, "Linear Trapezoidal Integrated SVF" (Cytomic technical paper, 2013).
    // - Pink noise: P. Kellet's refined filter (musicdsp.org archive, public domain coefficients).
    // - Modal resonator: two-pole resonator, impulse response r^n sin((n+1) w) / sin(w) (J. O. Smith, "Introduction to
    //   Digital Filters", two-pole section).
    // - FDN reverb: J.-M. Jot and A. Chaigne (1991); line gain g = 10^(-3 L / (fs RT60)); see FdnReverb.cs.
    // =====================================================================================================================

    /// <summary>Small math helpers shared by the audio code.</summary>
    public static class DspMath
    {
        public const double TwoPi = 2.0 * Math.PI;

        /// <summary>Levels at or below this many dBFS count as "off" (gain exactly 0).</summary>
        public const float SilentDb = -80f;

        /// <summary>dB to linear amplitude; anything at or below <see cref="SilentDb"/> is exactly 0.</summary>
        public static float DbToLin(float db) => db <= SilentDb ? 0f : (float)Math.Pow(10.0, db / 20.0);

        /// <summary>Linear amplitude to dB (-200 for silence).</summary>
        public static double LinToDb(double lin) => lin <= 1e-10 ? -200.0 : 20.0 * Math.Log10(lin);

        public static double Clamp01(double x) => x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x);

        public static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);

        /// <summary>Ken Perlin's smootherstep 6x^5 - 15x^4 + 10x^3 on a clamped x (C2 continuous ends).</summary>
        public static double Smootherstep(double x)
        {
            x = Clamp01(x);
            return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);
        }

        /// <summary>Classic smoothstep 3x^2 - 2x^3 on a clamped x.</summary>
        public static double Smoothstep(double x)
        {
            x = Clamp01(x);
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>Per-sample multiplier that decays by 60 dB after <paramref name="t60"/> seconds.</summary>
        public static double T60ToCoef(double t60, double rate) => t60 <= 0.0 ? 0.0 : Math.Pow(10.0, -3.0 / (t60 * rate));

        /// <summary>One-pole smoothing coefficient a for a time constant tau at an update rate (y += (1 - a)(x - y)).</summary>
        public static double OnePoleCoef(double tau, double rate) => tau <= 0.0 ? 0.0 : Math.Exp(-1.0 / (tau * rate));

        /// <summary>Flush values that would decay into subnormals (slow on x86) to exactly zero.</summary>
        public static double Ftz(double x) => (x > -1e-25 && x < 1e-25) ? 0.0 : x;

        /// <summary>
        /// Gentle constant-power pan normalised to unity at centre (pan -1 = left, +1 = right). Used only with |pan| &lt;= 0.5,
        /// so the mono sum stays within 1.5 dB of the centre value (mono compatible).
        /// </summary>
        public static void Pan(double pan, out float gl, out float gr)
        {
            double a = (Clamp(pan, -1.0, 1.0) + 1.0) * (Math.PI * 0.25);
            gl = (float)(Math.Cos(a) * 1.4142135623730951);
            gr = (float)(Math.Sin(a) * 1.4142135623730951);
        }

        /// <summary>32-bit integer hash (lowbias32 by C. Wellons) used to derive independent seeds.</summary>
        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        /// <summary>Derive a non-zero seed for stream <paramref name="salt"/> from the run seed.</summary>
        public static uint MixSeed(uint seed, uint salt)
        {
            uint h = Hash(seed * 0x9E3779B9u ^ Hash(salt + 0x632BE5ABu));
            return h == 0u ? 0x6D2B79F5u : h;
        }
    }

    /// <summary>Marsaglia xorshift32 generator. A struct: keep it in a field and call it in place (never copy it).</summary>
    public struct Xorshift32
    {
        uint _s;

        public Xorshift32(uint seed) { _s = seed == 0u ? 0x6D2B79F5u : seed; }

        public uint NextUInt()
        {
            uint x = _s;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _s = x;
            return x;
        }

        /// <summary>Uniform in [-1, 1) (variance 1/3).</summary>
        public float NextBipolar() => (int)NextUInt() * (1f / 2147483648f);

        /// <summary>Uniform in [0, 1).</summary>
        public double NextUnit() => (NextUInt() >> 8) * (1.0 / 16777216.0);
    }

    /// <summary>One-pole parameter smoother (default 50 ms, 14.3) evaluated at a given update rate.</summary>
    public sealed class OnePole
    {
        public double Value;
        public double Target;
        double _a;

        public OnePole(double tau, double rate, double initial = 0.0) { SetTime(tau, rate); Value = Target = initial; }

        public void SetTime(double tau, double rate) { _a = DspMath.OnePoleCoef(tau, rate); }

        /// <summary>Advance one update; values within 1e-6 of a zero target snap to exactly 0 (true silence).</summary>
        public double Step()
        {
            Value = Target + (Value - Target) * _a;
            if (Target == 0.0 && Value < 1e-6 && Value > -1e-6) Value = 0.0;
            return Value;
        }

        public void Snap() { Value = Target; }
    }

    /// <summary>DC blocker y = x - x1 + R y1 with the pole at <c>fc</c> (8 Hz in the master chain).</summary>
    public sealed class DcBlocker
    {
        readonly double _r;
        double _x1, _y1;

        public DcBlocker(double fc, double rate) { _r = Math.Exp(-DspMath.TwoPi * fc / rate); }

        public double Process(double x)
        {
            double y = x - _x1 + _r * _y1;
            _x1 = x;
            _y1 = DspMath.Ftz(y);
            return y;
        }

        public void Reset() { _x1 = _y1 = 0.0; }
    }

    /// <summary>One-pole low-pass (in-loop damping, noise colouring). y += (1 - c)(x - y).</summary>
    public sealed class OnePoleLp
    {
        double _c, _y;

        public OnePoleLp(double fc, double rate) { Set(fc, rate); }

        public void Set(double fc, double rate) { _c = Math.Exp(-DspMath.TwoPi * fc / rate); }

        public double Process(double x)
        {
            _y = x + (_y - x) * _c;
            return _y;
        }

        public void Reset() { _y = 0.0; }
    }

    /// <summary>One-pole high-pass (complement of <see cref="OnePoleLp"/>), removes subsonic energy from noise beds.</summary>
    public sealed class OnePoleHp
    {
        double _c, _lp;

        public OnePoleHp(double fc, double rate) { _c = Math.Exp(-DspMath.TwoPi * fc / rate); }

        public double Process(double x)
        {
            _lp = x + (_lp - x) * _c;
            return x - _lp;
        }

        public void Reset() { _lp = 0.0; }
    }

    public enum SvfMode { LowPass = 0, BandPass = 1, HighPass = 2, BandPassNormalized = 3 }

    /// <summary>
    /// Topology-preserving-transform state-variable filter (Simper). Stable under fast cutoff modulation, so swept
    /// filters can update their coefficients at control rate. BandPassNormalized has unity gain at the centre frequency.
    /// </summary>
    public sealed class Svf
    {
        double _ic1, _ic2;
        double _a1, _a2, _a3, _k;

        public Svf() { Set(1000.0, 0.707, 48000.0); }

        public Svf(double fc, double q, double rate) { Set(fc, q, rate); }

        public void Set(double fc, double q, double rate)
        {
            double f = DspMath.Clamp(fc, 1.0, 0.49 * rate);
            double g = Math.Tan(Math.PI * f / rate);
            _k = 1.0 / Math.Max(q, 0.05);
            _a1 = 1.0 / (1.0 + g * (g + _k));
            _a2 = g * _a1;
            _a3 = g * _a2;
        }

        public double Process(double v0, SvfMode mode)
        {
            double v3 = v0 - _ic2;
            double v1 = _a1 * _ic1 + _a2 * v3;
            double v2 = _ic2 + _a2 * _ic1 + _a3 * v3;
            _ic1 = 2.0 * v1 - _ic1;
            _ic2 = 2.0 * v2 - _ic2;
            switch (mode)
            {
                case SvfMode.LowPass: return v2;
                case SvfMode.BandPass: return v1;
                case SvfMode.HighPass: return v0 - _k * v1 - v2;
                default: return _k * v1;
            }
        }

        public void Flush()
        {
            _ic1 = DspMath.Ftz(_ic1);
            _ic2 = DspMath.Ftz(_ic2);
        }

        public void Reset() { _ic1 = _ic2 = 0.0; }
    }

    public enum NoiseColour { White = 0, Pink = 1, Brown = 2 }

    /// <summary>
    /// Coloured noise source. Each colour is a linear filter of white xorshift noise, which lets the engine compute exact
    /// level normalisation from the impulse response energy (see <see cref="NoiseSource.ShapeImpulse"/>).
    /// Brown: leaky integrator (pole at 20 Hz, -6 dB/oct above). Pink: Kellet's 7-term filter (-3 dB/oct, 9 Hz - 20 kHz).
    /// </summary>
    public sealed class NoiseSource
    {
        public readonly NoiseColour Colour;
        Xorshift32 _rng;
        readonly double _brownC;
        double _b0, _b1, _b2, _b3, _b4, _b5, _b6, _brown;

        public NoiseSource(NoiseColour colour, uint seed, double rate)
        {
            Colour = colour;
            _rng = new Xorshift32(seed);
            _brownC = Math.Exp(-DspMath.TwoPi * 20.0 / rate);
        }

        public double Next() => Shape(_rng.NextBipolar());

        /// <summary>Apply the colouring filter to one white input sample (also used with an impulse for calibration).</summary>
        public double Shape(double w)
        {
            switch (Colour)
            {
                case NoiseColour.Pink:
                {
                    _b0 = 0.99886 * _b0 + w * 0.0555179;
                    _b1 = 0.99332 * _b1 + w * 0.0750759;
                    _b2 = 0.96900 * _b2 + w * 0.1538520;
                    _b3 = 0.86650 * _b3 + w * 0.3104856;
                    _b4 = 0.55000 * _b4 + w * 0.5329522;
                    _b5 = -0.7616 * _b5 - w * 0.0168980;
                    double pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362;
                    _b6 = w * 0.115926;
                    return pink * 0.11;
                }
                case NoiseColour.Brown:
                    _brown = w + (_brown - w) * _brownC;
                    return _brown * 6.0;
                default:
                    return w;
            }
        }

        public void ResetShape() { _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = _b6 = _brown = 0.0; }

        /// <summary>White-noise variance of <see cref="Xorshift32.NextBipolar"/> (uniform on [-1, 1)).</summary>
        public const double WhiteVariance = 1.0 / 3.0;
    }

    /// <summary>Sine oscillator with a double-precision phase accumulator (cycles, wrapped to [0, 1)).</summary>
    public sealed class SineOsc
    {
        public double Phase;

        /// <summary>Return sin(2 pi phase) and advance by <paramref name="inc"/> cycles (= f / fs).</summary>
        public double Next(double inc)
        {
            double s = Math.Sin(DspMath.TwoPi * Phase);
            Phase += inc;
            if (Phase >= 1.0) Phase -= Math.Floor(Phase);
            return s;
        }
    }

    /// <summary>Triangle oscillator (double phase accumulator); harmonics fall 12 dB/oct so aliasing is negligible here.</summary>
    public sealed class TriangleOsc
    {
        public double Phase;

        public double Next(double inc)
        {
            double p = Phase;
            Phase += inc;
            if (Phase >= 1.0) Phase -= Math.Floor(Phase);
            return 4.0 * Math.Abs(p - 0.5) - 1.0;
        }
    }

    /// <summary>
    /// Click-free attack/decay envelope: smoothstep attack over A seconds (C1 at both ends), then exponential decay
    /// reaching -60 dB after the decay time. Deactivates itself at -100 dB.
    /// </summary>
    public sealed class AdEnvelope
    {
        double _attInc, _decMul, _pos, _val;
        int _stage; // 0 idle, 1 attack, 2 decay

        public void Set(double attack, double t60, double rate)
        {
            _attInc = attack > 0.0 ? 1.0 / (attack * rate) : 1.0;
            _decMul = DspMath.T60ToCoef(t60, rate);
        }

        public void Trigger() { _stage = 1; _pos = 0.0; _val = 0.0; }

        public bool Active => _stage != 0;

        public double Next()
        {
            if (_stage == 1)
            {
                _pos += _attInc;
                if (_pos >= 1.0) { _stage = 2; _val = 1.0; }
                else _val = _pos * _pos * (3.0 - 2.0 * _pos);
            }
            else if (_stage == 2)
            {
                _val *= _decMul;
                if (_val < 1e-5) { _stage = 0; _val = 0.0; }
            }
            return _val;
        }

        public void Kill() { _stage = 0; _val = 0.0; }
    }

    /// <summary>Two-pole modal resonator with unit-amplitude impulse response r^n sin((n + 1) w) and a T60 decay.</summary>
    public sealed class ModalResonator
    {
        double _b1, _b2, _gain, _y1, _y2;

        public void Set(double freq, double t60, double amp, double rate)
        {
            double r = DspMath.T60ToCoef(t60, rate);
            double w = DspMath.TwoPi * DspMath.Clamp(freq, 1.0, 0.45 * rate) / rate;
            _b1 = 2.0 * r * Math.Cos(w);
            _b2 = -r * r;
            _gain = amp * Math.Sin(w);
        }

        public double Process(double x)
        {
            double y = x * _gain + _b1 * _y1 + _b2 * _y2;
            _y2 = _y1;
            _y1 = y;
            return y;
        }

        /// <summary>Approximate magnitude of the ringing state (used to deactivate voices).</summary>
        public double Energy => Math.Abs(_y1) + Math.Abs(_y2);

        public void Reset() { _y1 = _y2 = 0.0; }
    }

    /// <summary>
    /// Master soft limiter (14.3): out = 0.708 tanh(1.2 x) / tanh(1.2). Full scale (|x| = 1) maps to 0.708 = -3 dBFS (the
    /// ceiling); the absolute bound for any input is 0.708 / tanh(1.2) = 0.849 = -1.42 dBFS, below the -1 dBFS true-peak
    /// limit by construction. Small-signal gain +0.16 dB (absorbed by the per-beat trims).
    /// </summary>
    public static class SoftLimiter
    {
        public const double Ceiling = 0.708;
        public const double Drive = 1.2;
        static readonly double Norm = Ceiling / Math.Tanh(Drive);

        public static double Process(double x) => Norm * Math.Tanh(Drive * x);
    }
}
