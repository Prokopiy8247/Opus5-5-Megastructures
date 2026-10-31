using System;

namespace Megastructure.Audio
{
    /// <summary>
    /// Custom 8-line feedback delay network reverb (14.3), Unity-free.
    ///
    /// Structure per sample: stereo pre-delay (fractional, up to 250 ms, smoothly slewed) -> inject L into the even lines
    /// and R into the odd lines -> each line output passes its own in-loop one-pole damping filter and the loop gain
    /// G0_i = 10^(-3 L_i / (fs RT60)) -> orthonormal 8x8 Hadamard mix (fast Walsh-Hadamard, 24 add/sub, scale 1/sqrt 8)
    /// -> written back with the input.
    ///
    /// Damping (Jot and Chaigne 1991, exact first-order design per line; docs/research/audio.md "HF damping"): the RT60 at
    /// the damping frequency f_d is RT60 / 2 (recorded interpretation of the spec's "damping" corner). Per line
    /// G_d = 10^(-3 L_i / (fs RT60 / 2)), r = G_d / G0, c = cos(2 pi f_d / fs), A = 1 - r^2, B = 1 - r^2 c,
    /// p_i = A / (B + sqrt(B^2 - A^2)), and the loop filter is z_i = G0_i (1 - p_i) x + p_i z_i. Its DC gain is G0_i (the
    /// low-frequency RT60 is exact) and its gain at f_d is exactly G_d, so every line loses the same dB per second at
    /// every frequency: the decay is uniform across the lines and RT60 falls smoothly from RT60 (DC) to RT60 / 2 (f_d)
    /// instead of collapsing in the mid band (one fixed one-pole per pass with a 2.5 kHz corner damps the short lines more
    /// per second and cut the bridge's 7.5 s to 3.8-5.3 s at 500 Hz). Bridge preset by design: 7.2 s at 500 Hz, 6.4 s at
    /// 1 kHz, 3.75 s at 2.5 kHz (AudioTests.Fdn_BridgeBandRt60FollowsTheJotDesign measures it).
    /// The coefficients are recomputed in <see cref="SetParams"/> from each line length (cheap enough for control rate).
    ///
    /// Outputs tap the damped line outputs before the loop gain (z_i / G0_i, kept as the unit-DC state lp_i with
    /// z_i = G0_i lp_i): L = even lines, R = odd lines (alternating signs), so the two channels are decorrelated but never
    /// cancel in a mono sum.
    ///
    /// Delays 1049, 1217, 1423, 1601, 1801, 2039, 2269, 2503 samples at 48 kHz (mutually prime), scaled to the device
    /// rate. Normalisation: with unit-energy input the first pass reaches the output at 0 dB energy, so the "wet" level
    /// of the preset is the level of the early tail relative to the dry signal; the steady-state reverberant energy then
    /// grows with RT60 (+8.8 dB for 7.5 s) like a real space. A tiny constant (1e-20) in the loop keeps the lines out of
    /// the subnormal range; the engine multiplies the return by an exact 0 when true silence is required.
    /// </summary>
    public sealed class FdnReverb
    {
        public static readonly int[] BaseDelays48k = { 1049, 1217, 1423, 1601, 1801, 2039, 2269, 2503 };
        public const int Lines = 8;
        public const double MaxPreDelaySeconds = 0.25;
        /// <summary>RT60 at the damping frequency relative to the nominal RT60 (Jot design target, 14.3 interpretation).</summary>
        public const double DampingRt60Ratio = 0.5;
        const double AntiDenormal = 1e-20;
        const double InvSqrt8 = 0.35355339059327373;

        readonly double _rate;
        readonly float[][] _line = new float[Lines][];
        readonly int[] _len = new int[Lines];
        readonly int[] _pos = new int[Lines];
        readonly double[] _g = new double[Lines];     // G0_i
        readonly double[] _p = new double[Lines];     // damping pole p_i of line i
        readonly double[] _lp = new double[Lines];    // unit-DC damping state: z_i = G0_i * lp_i
        readonly double[] _y = new double[Lines];

        readonly float[] _preL, _preR;
        readonly int _preLen;
        int _preWrite;
        double _preDelay;        // current pre-delay in samples (slewed)
        double _preDelayTarget;  // requested pre-delay in samples
        readonly double _preSlew; // max change of the pre-delay per sample (samples), keeps the pitch shift below 2 %

        public double Rt60 { get; private set; }
        public double DampingHz { get; private set; }
        public double PreDelaySeconds => _preDelay / _rate;

        public FdnReverb(double rate)
        {
            _rate = rate;
            double scale = rate / 48000.0;
            for (int i = 0; i < Lines; i++)
            {
                _len[i] = Math.Max(16, (int)Math.Round(BaseDelays48k[i] * scale));
                _line[i] = new float[_len[i]];
            }
            _preLen = (int)Math.Ceiling(MaxPreDelaySeconds * rate) + 8;
            _preL = new float[_preLen];
            _preR = new float[_preLen];
            _preSlew = 0.02;
            SetParams(1.0, 4000.0, 0.005, true);
        }

        /// <summary>Delay length of line i in samples at this rate (for tests and documentation).</summary>
        public int DelayLength(int i) => _len[i];

        /// <summary>Loop gain of line i (for tests): 10^(-3 L_i / (fs RT60)).</summary>
        public double LineGain(int i) => _g[i];

        /// <summary>Damping pole p_i of line i (for tests; 0 = no damping).</summary>
        public double DampingPole(int i) => _p[i];

        /// <summary>
        /// Jot first-order damping pole for one line: the one-pole (1 - p) / (1 - p z^-1) whose gain at the damping frequency
        /// is r = G_d / G0 (unit DC gain). c = cos(2 pi f_d / fs). Returns 0 for r >= 1 (no damping).
        /// </summary>
        public static double JotPole(double r, double c)
        {
            if (r >= 1.0) return 0.0;
            double r2 = r * r;
            double a = 1.0 - r2, b = 1.0 - r2 * c;
            double disc = b * b - a * a;   // b - a = r^2 (1 - c) >= 0
            return a / (b + Math.Sqrt(disc > 0.0 ? disc : 0.0));
        }

        /// <summary>Set RT60 (s), the damping corner (Hz) and the pre-delay (s). Cheap enough for control rate.</summary>
        public void SetParams(double rt60, double dampingHz, double preDelaySeconds, bool snapPreDelay = false)
        {
            Rt60 = Math.Max(0.05, rt60);
            DampingHz = DspMath.Clamp(dampingHz, 200.0, 0.5 * _rate);
            // a corner at or above 0.45 fs means "no damping" (exact RT60 at every frequency, used by the tests)
            bool damp = DampingHz < 0.45 * _rate;
            double c = Math.Cos(DspMath.TwoPi * DampingHz / _rate);
            double rtD = Rt60 * DampingRt60Ratio;
            for (int i = 0; i < Lines; i++)
            {
                double g0 = Math.Pow(10.0, -3.0 * _len[i] / (_rate * Rt60));
                _g[i] = g0;
                if (!damp) { _p[i] = 0.0; continue; }
                double gd = Math.Pow(10.0, -3.0 * _len[i] / (_rate * rtD));
                _p[i] = JotPole(gd / g0, c);
            }
            _preDelayTarget = DspMath.Clamp(preDelaySeconds, 0.0, MaxPreDelaySeconds) * _rate;
            if (snapPreDelay) _preDelay = _preDelayTarget;
        }

        /// <summary>Clear every line (used when the engine is reset).</summary>
        public void Clear()
        {
            for (int i = 0; i < Lines; i++) { Array.Clear(_line[i], 0, _line[i].Length); _lp[i] = 0.0; }
            Array.Clear(_preL, 0, _preLen);
            Array.Clear(_preR, 0, _preLen);
        }

        /// <summary>Process one stereo sample (send in, reverb return out, before the wet gain).</summary>
        public void Process(double inL, double inR, out double outL, out double outR)
        {
            // ---- pre-delay (fractional read, slewed delay time)
            _preL[_preWrite] = (float)inL;
            _preR[_preWrite] = (float)inR;
            double dd = _preDelayTarget - _preDelay;
            if (dd > _preSlew) dd = _preSlew; else if (dd < -_preSlew) dd = -_preSlew;
            _preDelay += dd;
            double rp = _preWrite - _preDelay;
            if (rp < 0.0) rp += _preLen;
            int i0 = (int)rp;
            double fr = rp - i0;
            int i1 = i0 + 1; if (i1 >= _preLen) i1 = 0;
            double pl = _preL[i0] + (_preL[i1] - _preL[i0]) * fr;
            double pr = _preR[i0] + (_preR[i1] - _preR[i0]) * fr;
            if (++_preWrite >= _preLen) _preWrite = 0;

            // ---- read, damp, attenuate: z_i = G0_i (1 - p_i) x + p_i z_i, computed as lp_i = (1 - p_i) x + p_i lp_i, z_i = G0_i lp_i
            for (int i = 0; i < Lines; i++)
            {
                double x = _line[i][_pos[i]];
                double lp = x + (_lp[i] - x) * _p[i];
                _lp[i] = lp;
                _y[i] = lp * _g[i];
            }

            // ---- outputs (damped line outputs before the loop gain)
            outL = (_lp[0] - _lp[2] + _lp[4] - _lp[6]);
            outR = (_lp[1] - _lp[3] + _lp[5] - _lp[7]);

            // ---- orthonormal Hadamard (fast Walsh-Hadamard transform)
            double a0 = _y[0] + _y[1], a1 = _y[0] - _y[1], a2 = _y[2] + _y[3], a3 = _y[2] - _y[3];
            double a4 = _y[4] + _y[5], a5 = _y[4] - _y[5], a6 = _y[6] + _y[7], a7 = _y[6] - _y[7];
            double b0 = a0 + a2, b1 = a1 + a3, b2 = a0 - a2, b3 = a1 - a3;
            double b4 = a4 + a6, b5 = a5 + a7, b6 = a4 - a6, b7 = a5 - a7;
            double il = pl * 0.5, ir = pr * 0.5;
            Write(0, (b0 + b4) * InvSqrt8 + il);
            Write(1, (b1 + b5) * InvSqrt8 + ir);
            Write(2, (b2 + b6) * InvSqrt8 - il);
            Write(3, (b3 + b7) * InvSqrt8 - ir);
            Write(4, (b0 - b4) * InvSqrt8 + il);
            Write(5, (b1 - b5) * InvSqrt8 + ir);
            Write(6, (b2 - b6) * InvSqrt8 - il);
            Write(7, (b3 - b7) * InvSqrt8 - ir);
        }

        void Write(int i, double v)
        {
            float[] buf = _line[i];
            int p = _pos[i];
            buf[p] = (float)(v + AntiDenormal);
            if (++p >= _len[i]) p = 0;
            _pos[i] = p;
        }
    }
}
