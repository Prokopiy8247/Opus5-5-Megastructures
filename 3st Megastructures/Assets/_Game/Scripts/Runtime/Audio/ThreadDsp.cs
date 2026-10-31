// ThreadDsp: the whole procedural score of Thread (MASTER_PROMPT 14.3) as pure, allocation-free C#.
// Layers L1-L8 of 14.3.2 are summed into the listener mix that arrives in the buffer (the footstep
// AudioSources), then the master chain runs: DC block (1-pole HPF 10 Hz) -> 8-line FDN reverb (Householder,
// per-line damping, pre-delay, parameters cross-blended over 8 s, fixed delay lengths) -> master low-pass
// (the 14 kHz -> 2.2 kHz sweep, the inhale, the cloud deck) -> master gain -> soft limiter c tanh(x / c).
// Rules (14.3.1, R5): no UnityEngine, no allocation in Process, sines from a 4096-entry table with linear
// interpolation, double phase accumulators, xorshift32 PRNG streams derived from the seed, Kellet pink noise,
// state-variable filters in the stable trapezoidal (Simper/Cytomic) form (the Chamberlin form of the spec
// diverges above ~8 kHz at 48 kHz, R5 decision 2), every parameter smoothed per sample by a 20 ms one-pole.
// Each layer generator is calibrated at construction (impulse-response energy for the noise chains, closed
// form for the oscillators, both including the DC blocker) so a layer gain g yields exactly 20 log10(g) dBFS
// RMS at the master input (stereo RMS = sqrt of the mean of L^2 and R^2).
using System;
using System.Runtime.CompilerServices;

namespace Megastructure.Audio
{
    // ==================================================================================== building blocks

    /// <summary>xorshift32 (Marsaglia). The state is never 0.</summary>
    public struct XorShift32
    {
        uint m_s;
        public XorShift32(uint seed) { m_s = seed != 0u ? seed : 0x9E3779B9u; }

        /// <summary>Independent stream for (seed, stream id): the seed is hashed (murmur3 finaliser) so streams decorrelate.</summary>
        public static XorShift32 Stream(int seed, uint stream)
        {
            unchecked
            {
                uint x = (uint)seed * 0x9E3779B1u ^ (stream * 0x85EBCA77u + 0x27D4EB2Fu);
                x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16;
                return new XorShift32(x);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt() { uint x = m_s; x ^= x << 13; x ^= x >> 17; x ^= x << 5; m_s = x; return x; }
        /// <summary>Uniform in [-1, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextBipolar() { return unchecked((int)NextUInt()) * (1.0f / 2147483648.0f); }
        /// <summary>Uniform in [0, 1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next01() { return (NextUInt() >> 8) * (1.0f / 16777216.0f); }
        public double NextDouble01() { return (NextUInt() >> 1) * (1.0 / 2147483648.0); }
        public uint State { get { return m_s; } }
    }

    /// <summary>Paul Kellet's refined pinking filter (linear; input uniform white in [-1, 1)), output scaled to about unit RMS.</summary>
    public struct PinkKellet
    {
        public float b0, b1, b2, b3, b4, b5, b6;
        public const float UnitRms = 1f / 1.755f;   // R5 section 3: RMS 1.755 for uniform white in [-1, 1)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next(float white)
        {
            b0 = 0.99886f * b0 + white * 0.0555179f;
            b1 = 0.99332f * b1 + white * 0.0750759f;
            b2 = 0.96900f * b2 + white * 0.1538520f;
            b3 = 0.86650f * b3 + white * 0.3104856f;
            b4 = 0.55000f * b4 + white * 0.5329522f;
            b5 = -0.7616f * b5 - white * 0.0168980f;
            float pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f;
            b6 = white * 0.115926f;
            return pink * UnitRms;
        }
        public void Flush()
        {
            if (Math.Abs(b0) < 1e-20f) b0 = 0f; if (Math.Abs(b1) < 1e-20f) b1 = 0f; if (Math.Abs(b2) < 1e-20f) b2 = 0f;
            if (Math.Abs(b3) < 1e-20f) b3 = 0f; if (Math.Abs(b4) < 1e-20f) b4 = 0f; if (Math.Abs(b5) < 1e-20f) b5 = 0f;
        }
    }

    /// <summary>
    /// Simper/Cytomic linear trapezoidal state-variable filter (stable for every fc below Nyquist).
    /// low/band/high outputs; BandNorm = k * band has a 0 dB peak at fc.
    /// </summary>
    public struct SvfTpt
    {
        public float ic1, ic2, a1, a2, a3, k;

        public void Set(double fc, double q, double fs)
        {
            double f = Math.Min(Math.Max(fc, 1.0), 0.45 * fs);
            double g = Math.Tan(Math.PI * f / fs);
            double kk = 1.0 / Math.Max(q, 0.05);
            double A1 = 1.0 / (1.0 + g * (g + kk));
            k = (float)kk; a1 = (float)A1; a2 = (float)(g * A1); a3 = (float)(g * g * A1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Low(float x)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            return v2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float BandNorm(float x)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            return k * v1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float High(float x)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            return x - k * v1 - v2;
        }

        public void Clear() { ic1 = 0f; ic2 = 0f; }
        public void Flush() { if (Math.Abs(ic1) < 1e-20f) ic1 = 0f; if (Math.Abs(ic2) < 1e-20f) ic2 = 0f; }

        /// <summary>|H| of the low-pass at frequency f (bilinear image of 1 / (s^2 + s/Q + 1)).</summary>
        public static double LowMagnitude(double f, double fc, double q, double fs)
        {
            double fcc = Math.Min(Math.Max(fc, 1.0), 0.45 * fs);
            double g = Math.Tan(Math.PI * fcc / fs);
            double w = Math.Tan(Math.PI * Math.Min(f, 0.4999 * fs) / fs) / g;
            double re = 1.0 - w * w, im = w / Math.Max(q, 0.05);
            return 1.0 / Math.Sqrt(re * re + im * im);
        }
    }

    /// <summary>DC blocker y = x - x1 + R y1, R = exp(-2 pi fc / fs).</summary>
    public struct DcBlocker
    {
        public float x1, y1, r;
        public void Init(double fc, double fs) { r = (float)Math.Exp(-2.0 * Math.PI * fc / fs); x1 = 0f; y1 = 0f; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Process(float x) { float y = x - x1 + r * y1; x1 = x; y1 = y; return y; }
        public void Clear() { x1 = 0f; y1 = 0f; }

        /// <summary>|H| at frequency f for the blocker at fc.</summary>
        public static double Magnitude(double f, double fc, double fs)
        {
            double R = Math.Exp(-2.0 * Math.PI * fc / fs), w = 2.0 * Math.PI * f / fs, c = Math.Cos(w);
            double num = 2.0 - 2.0 * c, den = 1.0 - 2.0 * R * c + R * R;
            return den > 1e-30 ? Math.Sqrt(num / den) : 1.0;
        }
    }

    /// <summary>4096-entry sine table with a guard sample; Lookup(phase in [0,1)) with linear interpolation.</summary>
    public static class SineTable
    {
        public const int Size = AudioSpec.SineTableSize;
        public static readonly float[] T = Build();
        static float[] Build()
        {
            var t = new float[Size + 1];
            for (int i = 0; i <= Size; i++) t[i] = (float)Math.Sin(2.0 * Math.PI * i / Size);
            return t;
        }
        /// <summary>Call on the main thread (Awake) so the static initialiser never runs on the audio thread.</summary>
        public static void Warm() { Lookup(0.25); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lookup(double phase01)
        {
            double p = phase01 * Size;
            int i = (int)p;
            float fr = (float)(p - i);
            i &= Size - 1;
            return T[i] + fr * (T[i + 1] - T[i]);
        }
    }

    /// <summary>
    /// 8-line feedback delay network (R5 section 4): base primes 1427..3539 samples at 48 kHz times a fixed size
    /// factor (re-primed once), Householder feedback (y_i = x_i - 0.25 sum), unity-DC one-pole damping per line,
    /// per-line gains g_i = 10^(-3 M_i / (RT60 fs)), stereo pre-delay with a 1 s two-tap crossfade (no read
    /// pointer ever moves), outputs from two Hadamard rows. RT60, damping and wet are smoothed in double at
    /// control rate (every sub-block) with tau = 8 s / 4.6 (99 percent in 8 s).
    /// </summary>
    public sealed class FdnReverb8
    {
        const int N = 8;
        static readonly int[] BasePrimes48k = { 1427, 1783, 1973, 2099, 2579, 2879, 3221, 3539 };
        readonly float[] m_buf;
        readonly int[] m_off = new int[N], m_len = new int[N], m_pos = new int[N];
        readonly float[] m_lp = new float[N], m_gain = new float[N], m_v = new float[N];
        readonly float[] m_pre;
        readonly int m_preFrames;
        readonly double m_fs, m_meanLen;
        int m_preW, m_tapA, m_tapB, m_tPre;
        float m_xf = 1f;
        readonly float m_xfStep;
        double m_rt, m_damp, m_wet;              // smoothed (double: an 8 s float one-pole stalls short of its target)
        double m_tRt = 1.0, m_tDamp = 7000.0, m_tWet;
        float m_dampC, m_wetNorm;

        public FdnReverb8(double sampleRate, double sizeFactor, double maxPreDelaySeconds)
        {
            m_fs = sampleRate;
            int total = 0;
            for (int i = 0; i < N; i++)
            {
                int l = NearestPrime((int)Math.Round(BasePrimes48k[i] * sizeFactor * m_fs / 48000.0));
                m_len[i] = l; m_off[i] = total; total += l;
            }
            m_buf = new float[total];
            m_meanLen = total / (double)N;
            m_preFrames = (int)(maxPreDelaySeconds * m_fs) + 2;
            m_pre = new float[2 * m_preFrames];
            m_xfStep = (float)(1.0 / m_fs);
            m_rt = m_tRt; m_damp = m_tDamp; m_wet = m_tWet;
            UpdateCoefficients();
        }

        public void SetTargets(double rt60, double preDelayMs, double wetDb, double dampHz)
        {
            m_tRt = Math.Max(0.05, rt60);
            m_tDamp = Math.Max(200.0, dampHz);
            m_tWet = AudioSpec.Db(wetDb);
            int p = Math.Min(Math.Max(0, (int)(preDelayMs * 0.001 * m_fs)), m_preFrames - 2);
            if (p != m_tPre && m_xf >= 1f) { m_tapA = m_tapB; m_tapB = p; m_xf = 0f; m_tPre = p; }
        }

        /// <summary>Jump to the targets (offline renders that start mid-walk, device rebuilds).</summary>
        public void Snap()
        {
            m_rt = m_tRt; m_damp = m_tDamp; m_wet = m_tWet;
            m_tapA = m_tapB = m_tPre; m_xf = 1f;
            UpdateCoefficients();
        }

        /// <summary>Control-rate update for a sub-block of n frames.</summary>
        public void Control(int n)
        {
            double dRt = m_tRt - m_rt, dDamp = m_tDamp - m_damp, dWet = m_tWet - m_wet;
            if (Math.Abs(dRt) < 1e-9 * m_rt && Math.Abs(dDamp) < 1e-9 * m_damp && Math.Abs(dWet) <= 1e-9 * Math.Max(m_wet, 1e-6))
                return;   // converged: the coefficients are already current
            double a = 1.0 - Math.Exp(-n * 4.6 / (AudioSpec.ReverbBlendSeconds * m_fs));
            m_rt += a * dRt;
            m_damp += a * dDamp;
            m_wet += a * dWet;
            UpdateCoefficients();
        }

        void UpdateCoefficients()
        {
            for (int i = 0; i < N; i++)
                m_gain[i] = (float)Math.Min(Math.Pow(10.0, -3.0 * m_len[i] / (m_rt * m_fs)), 0.9995);
            m_dampC = m_damp >= 0.45 * m_fs ? 1f : (float)(1.0 - Math.Exp(-2.0 * Math.PI * m_damp / m_fs));
            double tau = m_rt * m_fs / 6.9078;
            m_wetNorm = (float)(m_wet * Math.Sqrt(2.0 * N * m_meanLen / Math.Max(tau, 1.0)));   // tail energy ~ wet^2
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick(float inL, float inR, out float outL, out float outR)
        {
            int pw = m_preW;
            m_pre[2 * pw] = inL; m_pre[2 * pw + 1] = inR;
            int ra = pw - m_tapA; if (ra < 0) ra += m_preFrames;
            int rb = pw - m_tapB; if (rb < 0) rb += m_preFrames;
            float dL, dR;
            if (m_xf < 1f)
            {
                float xf = m_xf + m_xfStep; if (xf > 1f) xf = 1f; m_xf = xf;
                float aL = m_pre[2 * ra], aR = m_pre[2 * ra + 1];
                dL = aL + xf * (m_pre[2 * rb] - aL);
                dR = aR + xf * (m_pre[2 * rb + 1] - aR);
            }
            else { dL = m_pre[2 * rb]; dR = m_pre[2 * rb + 1]; }
            if (++pw >= m_preFrames) pw = 0;
            m_preW = pw;

            float[] buf = m_buf; float[] lp = m_lp; float[] v = m_v; float[] gain = m_gain;
            float dc = m_dampC, sum = 0f;
            for (int i = 0; i < N; i++)
            {
                float y = buf[m_off[i] + m_pos[i]];
                float l = lp[i] + dc * (y - lp[i]);
                lp[i] = l;
                float g = gain[i] * l;
                v[i] = g; sum += g;
            }
            float h = 0.25f * sum;
            float hl = 0.5f * dL, hr = 0.5f * dR;
            for (int i = 0; i < N; i++)
            {
                float x = v[i] - h + ((i & 1) == 0 ? hl : hr);
                if (x < 1e-15f && x > -1e-15f) x = 0f;     // no denormals in the loop
                int p = m_pos[i];
                buf[m_off[i] + p] = x;
                if (++p >= m_len[i]) p = 0;
                m_pos[i] = p;
            }
            float c = 0.35355339f * m_wetNorm;
            outL = c * (v[0] + v[1] - v[2] - v[3] + v[4] + v[5] - v[6] - v[7]);
            outR = c * (v[0] - v[1] - v[2] + v[3] + v[4] - v[5] - v[6] + v[7]);
        }

        public void Clear()
        {
            Array.Clear(m_buf, 0, m_buf.Length);
            Array.Clear(m_pre, 0, m_pre.Length);
            for (int i = 0; i < N; i++) { m_lp[i] = 0f; m_v[i] = 0f; }
        }

        public void Flush() { for (int i = 0; i < N; i++) if (Math.Abs(m_lp[i]) < 1e-20f) m_lp[i] = 0f; }

        public int TotalDelayFrames { get { return m_buf.Length; } }

        static int NearestPrime(int n)
        {
            for (int d = 0; ; d++)
            {
                if (IsPrime(n - d)) return n - d;
                if (IsPrime(n + d)) return n + d;
            }
        }
        static bool IsPrime(int n)
        {
            if (n < 2) return false;
            for (int k = 2; k * k <= n; k++) if (n % k == 0) return false;
            return true;
        }
    }

    // ==================================================================================== the score

    public sealed class ThreadDsp
    {
        /// <summary>Control-rate sub-block (frames): LFOs, filter coefficients, envelopes, reverb blend.</summary>
        public const int Block = 32;
        const int MaxStars = 4;
        const float Silent = 1e-7f;

        public readonly int SampleRate;
        public readonly int Seed;
        readonly double m_fs, m_dt;
        readonly float m_aFast;        // 20 ms one-pole coefficient (per sample)
        readonly float m_aMaster;      // 50 ms for the master gain (pause duck, fades)
        readonly float[] m_l = new float[Block], m_r = new float[Block];
        double m_time;                 // seconds of audio processed (LFO clock)

        /// <summary>Number of times a non-finite block forced a full state reset (main thread logs it).</summary>
        public int NanResets;

        // ---------------------------------------------------------------- L1 wind
        XorShift32 m_rngWL, m_rngWR, m_rngGust;
        PinkKellet m_pinkWL, m_pinkWR;
        SvfTpt m_w1L, m_w1R, m_w2L, m_w2R;
        float m_windNorm, m_windBand2, m_gWind;
        double m_phW1, m_phW2, m_gustT;
        float m_gustA, m_gustB, m_gustNorm;

        // ---------------------------------------------------------------- L2 drone (4 sines)
        readonly double[] m_phDrone = new double[4], m_incDrone = new double[4];
        readonly float[] m_ampDrone = new float[4];
        double m_phDroneLfo;
        float m_gDrone;

        // ---------------------------------------------------------------- L3 pad (4 voices x 2 detuned triangles)
        readonly double[] m_phPad = new double[8], m_incPad = new double[8];
        readonly float[] m_padNorm = new float[4], m_padEnv = new float[4];
        readonly float[] m_padPanL = new float[4], m_padPanR = new float[4];
        readonly float[] m_padVoiceAmp = new float[4];
        SvfTpt m_padLpL, m_padLpR;
        float m_padUp, m_padDown, m_gPad, m_padMixA, m_padMixB;

        // ---------------------------------------------------------------- L4 colossus breath (2 sources)
        XorShift32 m_rngBL, m_rngBR;
        SvfTpt m_bLpL, m_bLpR, m_bBpL, m_bBpR;
        float m_breathNorm, m_gBLL, m_gBLR, m_gBRL, m_gBRR;
        double m_phBreath;

        // ---------------------------------------------------------------- L5 mist hush
        XorShift32 m_rngHL, m_rngHR;
        PinkKellet m_pinkHL, m_pinkHR;
        SvfTpt m_hHpL, m_hHpR, m_hLpL, m_hLpR;
        float m_hushNorm, m_gHush;

        // ---------------------------------------------------------------- L6 thread hum (5 partials)
        const int HumN = 5;
        readonly double[] m_phHum = new double[HumN], m_incHum = new double[HumN];
        readonly double[] m_phTrem = new double[HumN], m_incTrem = new double[HumN];
        readonly float[] m_humBase = new float[HumN], m_humPanL = new float[HumN], m_humPanR = new float[HumN];
        readonly float[] m_humAmpL = new float[HumN], m_humAmpR = new float[HumN];
        float m_gHum;

        // ---------------------------------------------------------------- L7 star notes
        struct StarVoice
        {
            public bool active;
            public double phase, inc;
            public float env, gl, gr;
            public int attackLeft;
            public float attackInc, decayMul;
        }
        readonly StarVoice[] m_stars = new StarVoice[MaxStars];
        XorShift32 m_rngStars;
        float m_starAmp, m_starAttackInc, m_starDecayMul, m_gStars;
        int m_starAttackFrames, m_lastNote = -1;
        double m_starRate;

        // ---------------------------------------------------------------- L8 beacon
        double m_phBeacon, m_incBeacon, m_phPulse;
        float m_beaconAmp, m_gBeacon;

        // ---------------------------------------------------------------- master
        DcBlocker m_dcL, m_dcR;
        readonly FdnReverb8 m_fdn;
        SvfTpt m_lpL, m_lpR;
        double m_lpHz;
        float m_gStep, m_gMaster;
        readonly float m_ceiling, m_invCeiling;

        public ThreadDsp(int sampleRate, int seed)
        {
            SampleRate = sampleRate > 0 ? sampleRate : (int)ThreadConstants.AudioSampleRate;
            Seed = seed;
            m_fs = SampleRate;
            m_dt = 1.0 / m_fs;
            m_aFast = (float)(1.0 - Math.Exp(-1.0 / (AudioSpec.SmoothTau * m_fs)));
            m_aMaster = (float)(1.0 - Math.Exp(-1.0 / (0.05 * m_fs)));
            m_ceiling = (float)AudioSpec.LimiterCeiling;
            m_invCeiling = 1f / m_ceiling;
            SineTable.Warm();
            m_fdn = new FdnReverb8(m_fs, AudioSpec.ReverbSize, AudioSpec.ReverbMaxPreDelay);
            Configure();
            Reset();
        }

        public double TimeSeconds { get { return m_time; } }

        // ================================================================ construction and calibration

        void Configure()
        {
            double fs = m_fs;
            double dcHz = AudioSpec.DcBlockHz;

            // ---- wind: calibrate the static chain pink -> (band 1 + band 2) -> DC block by impulse energy
            m_windBand2 = (float)AudioSpec.Db(AudioSpec.WindBand2Db);
            {
                var pink = new PinkKellet(); var b1 = new SvfTpt(); var b2 = new SvfTpt(); var dc = new DcBlocker();
                b1.Set(AudioSpec.WindFc1, AudioSpec.WindQ1, fs); b2.Set(AudioSpec.WindFc2, AudioSpec.WindQ2, fs); dc.Init(dcHz, fs);
                double e = 0; int n = (int)(3.0 * fs);
                for (int i = 0; i < n; i++)
                {
                    float x = pink.Next(i == 0 ? 1f : 0f);
                    float y = dc.Process(b1.BandNorm(x) + m_windBand2 * b2.BandNorm(x));
                    e += (double)y * y;
                }
                m_windNorm = (float)(1.0 / Math.Sqrt(Math.Max(1e-30, e / 3.0)));   // uniform white variance 1/3
            }
            // gust g = 0.55 + 0.45 n, n = cosine-interpolated uniform knots: E[n^2] = 1/4, E[n] = 0
            double gb = AudioSpec.WindGustBase, gd = AudioSpec.WindGustDepth;
            m_gustNorm = (float)(1.0 / Math.Sqrt(gb * gb + gd * gd * 0.25));

            // ---- drone: 4 sines, mean square 2 a^2 = 1, LFO power (1 + d^2 / 2), DC-block loss compensated
            {
                double f1 = ThreadConstants.DroneF1, f2 = ThreadConstants.DroneF2, det = ThreadConstants.DroneDetune;
                double[] f = { f1, f1 + det, f2, f2 + det };
                double lfo = Math.Sqrt(1.0 + 0.5 * AudioSpec.DroneLfoDepth * AudioSpec.DroneLfoDepth);
                for (int i = 0; i < 4; i++)
                {
                    m_incDrone[i] = f[i] / fs;
                    m_ampDrone[i] = (float)(Math.Sqrt(0.5) / lfo / DcBlocker.Magnitude(f[i], dcHz, fs));
                }
            }

            // ---- pad: per voice two triangles +-4 cents; the analytic power of the naive triangle through the
            // 1.1 kHz low-pass and the DC block gives the unit-RMS normaliser of each voice
            {
                var pf = ThreadConstants.PadFreqs;
                double det = Math.Pow(2.0, AudioSpec.PadDetuneCents / 1200.0);
                double[] pans = { 0.0, -0.30, 0.30, -0.15 };   // voice spread (chosen)
                for (int v = 0; v < 4; v++)
                {
                    m_incPad[2 * v] = pf[v] / det / fs;
                    m_incPad[2 * v + 1] = pf[v] * det / fs;
                    double pw = 0.0;
                    for (int k = 1; k * pf[v] < 0.5 * fs; k += 2)
                    {
                        double a = 8.0 / (Math.PI * Math.PI * k * k);
                        double hl = SvfTpt.LowMagnitude(k * pf[v], AudioSpec.PadLowpassHz, AudioSpec.PadLowpassQ, fs);
                        double hd = DcBlocker.Magnitude(k * pf[v], dcHz, fs);
                        pw += 0.5 * a * a * hl * hl * hd * hd;
                    }
                    m_padNorm[v] = (float)(1.0 / Math.Sqrt(Math.Max(1e-30, pw)));
                    ThreadAudioParams.PanGains(pans[v], out double gl, out double gr);
                    m_padPanL[v] = (float)gl; m_padPanR[v] = (float)gr;
                }
                // each channel gets a t1 + b t2 with a^2 + b^2 = 1 (uncorrelated triangles keep unit power)
                m_padMixA = 0.85f; m_padMixB = (float)Math.Sqrt(1.0 - 0.85 * 0.85);
                m_padUp = (float)(Block / (AudioSpec.PadAttack * fs));
                m_padDown = (float)(Block / (AudioSpec.PadRelease * fs));
                m_padLpL.Set(AudioSpec.PadLowpassHz, AudioSpec.PadLowpassQ, fs);
                m_padLpR = m_padLpL;
            }

            // ---- breath: white -> LP 120 Hz -> band 55-110 Hz -> DC block (impulse energy)
            {
                double fc = Math.Sqrt(AudioSpec.BreathBandLoHz * AudioSpec.BreathBandHiHz);
                double q = fc / (AudioSpec.BreathBandHiHz - AudioSpec.BreathBandLoHz);
                m_bLpL.Set(AudioSpec.BreathLowpassHz, 0.7071, fs); m_bLpR = m_bLpL;
                m_bBpL.Set(fc, q, fs); m_bBpR = m_bBpL;
                var lp = m_bLpL; var bp = m_bBpL; var dc = new DcBlocker(); dc.Init(dcHz, fs);
                double e = 0; int n = (int)(2.0 * fs);
                for (int i = 0; i < n; i++)
                {
                    float y = dc.Process(bp.BandNorm(lp.Low(i == 0 ? 1f : 0f)));
                    e += (double)y * y;
                }
                double lfo = Math.Sqrt(1.0 + 0.5 * AudioSpec.BreathLfoDepth * AudioSpec.BreathLfoDepth);
                m_breathNorm = (float)(1.0 / Math.Sqrt(Math.Max(1e-30, e / 3.0)) / lfo);
            }

            // ---- hush: pink -> HP 25 Hz -> LP 600 Hz -> DC block (impulse energy)
            {
                m_hHpL.Set(AudioSpec.HushHighpassHz, 0.7071, fs); m_hHpR = m_hHpL;
                m_hLpL.Set(AudioSpec.HushLowpassHz, 0.7071, fs); m_hLpR = m_hLpL;
                var pink = new PinkKellet(); var hp = m_hHpL; var lp = m_hLpL; var dc = new DcBlocker(); dc.Init(dcHz, fs);
                double e = 0; int n = (int)(3.0 * fs);
                for (int i = 0; i < n; i++)
                {
                    float y = dc.Process(lp.Low(hp.High(pink.Next(i == 0 ? 1f : 0f))));
                    e += (double)y * y;
                }
                m_hushNorm = (float)(1.0 / Math.Sqrt(Math.Max(1e-30, e / 3.0)));
            }

            // ---- hum: partials n of 27.5 Hz, amplitude 1 / n, DC loss compensated; tremolo rates drift +-8 percent
            {
                var parts = AudioSpec.HumPartials;
                double[] pans = { 0.0, -0.25, 0.25, -0.45, 0.45 };
                var rng = XorShift32.Stream(Seed, 0x48554D00u);
                for (int i = 0; i < HumN; i++)
                {
                    double f = ThreadConstants.HumBase * parts[i];
                    m_incHum[i] = f / fs;
                    m_humBase[i] = (float)(1.0 / parts[i] / DcBlocker.Magnitude(f, dcHz, fs));
                    m_incTrem[i] = AudioSpec.HumTremoloHz * (0.92 + 0.16 * rng.NextDouble01()) * Block / fs;
                    ThreadAudioParams.PanGains(pans[i], out double gl, out double gr);
                    m_humPanL[i] = (float)gl; m_humPanR[i] = (float)gr;
                }
            }

            // ---- star notes: Poisson rate 1 / 3.5 s; note amplitude so the long-term layer RMS is 1
            {
                m_starRate = 1.0 / AudioSpec.StarMeanInterval;
                m_starAttackFrames = Math.Max(1, (int)(AudioSpec.StarAttack * fs));
                m_starAttackInc = 1f / m_starAttackFrames;
                m_starDecayMul = (float)Math.Exp(-6.9078 / (AudioSpec.StarDecay * fs));   // "1.4 s decay" read as T60 (R5)
                double e = 0;   // integral of env^2 / 2 (sine power) in seconds
                for (int i = 0; i < m_starAttackFrames; i++) { double a = (i + 1) * (double)m_starAttackInc; e += 0.5 * a * a; }
                double env = 1.0;
                while (env > 1e-4) { e += 0.5 * env * env; env *= m_starDecayMul; }
                e /= fs;
                m_starAmp = (float)(1.0 / Math.Sqrt(Math.Max(1e-30, m_starRate * e)));
            }

            // ---- beacon: sine 440 Hz with a 4 percent pulse
            m_incBeacon = AudioSpec.BeaconHz / fs;
            m_beaconAmp = (float)(Math.Sqrt(2.0) / Math.Sqrt(1.0 + 0.5 * AudioSpec.BeaconPulseDepth * AudioSpec.BeaconPulseDepth)
                                  / DcBlocker.Magnitude(AudioSpec.BeaconHz, dcHz, fs));

            m_dcL.Init(dcHz, fs); m_dcR = m_dcL;
        }

        /// <summary>Clears every state (silence, PRNG streams back to the seed). Deterministic.</summary>
        public void Reset()
        {
            m_time = 0;
            m_rngWL = XorShift32.Stream(Seed, 1); m_rngWR = XorShift32.Stream(Seed, 2); m_rngGust = XorShift32.Stream(Seed, 3);
            m_rngBL = XorShift32.Stream(Seed, 4); m_rngBR = XorShift32.Stream(Seed, 5);
            m_rngHL = XorShift32.Stream(Seed, 6); m_rngHR = XorShift32.Stream(Seed, 7);
            m_rngStars = XorShift32.Stream(Seed, 8);
            m_pinkWL = new PinkKellet(); m_pinkWR = new PinkKellet(); m_pinkHL = new PinkKellet(); m_pinkHR = new PinkKellet();
            m_w1L.Clear(); m_w1R.Clear(); m_w2L.Clear(); m_w2R.Clear();
            m_padLpL.Clear(); m_padLpR.Clear();
            m_bLpL.Clear(); m_bLpR.Clear(); m_bBpL.Clear(); m_bBpR.Clear();
            m_hHpL.Clear(); m_hHpR.Clear(); m_hLpL.Clear(); m_hLpR.Clear();
            m_lpL.Clear(); m_lpR.Clear();
            m_dcL.Clear(); m_dcR.Clear();
            m_fdn.Clear();
            m_gustT = 0; m_gustA = m_rngGust.NextBipolar(); m_gustB = m_rngGust.NextBipolar();
            var seedRng = XorShift32.Stream(Seed, 9);   // random start phases: no two layers line up at t = 0
            for (int i = 0; i < 4; i++) m_phDrone[i] = seedRng.NextDouble01();
            for (int i = 0; i < 8; i++) m_phPad[i] = seedRng.NextDouble01();
            for (int i = 0; i < HumN; i++) { m_phHum[i] = seedRng.NextDouble01(); m_phTrem[i] = seedRng.NextDouble01(); }
            m_phW1 = seedRng.NextDouble01(); m_phW2 = seedRng.NextDouble01();
            m_phDroneLfo = 0.75; m_phBreath = 0.75; m_phBeacon = 0; m_phPulse = 0;   // LFOs start at their minimum
            for (int v = 0; v < 4; v++) { m_padEnv[v] = 0f; m_padVoiceAmp[v] = 0f; }
            for (int i = 0; i < MaxStars; i++) m_stars[i].active = false;
            m_lastNote = -1;
            m_gWind = m_gDrone = m_gPad = m_gHush = m_gHum = m_gStars = m_gBeacon = 0f;
            m_gBLL = m_gBLR = m_gBRL = m_gBRR = 0f;
            m_gStep = 0f; m_gMaster = 0f;
            m_lpHz = AudioSpec.LowpassOpenHz;
            SetLowpass(m_lpHz);
        }

        /// <summary>Jumps every smoother and envelope to the targets of p (no fade-in): offline renders that start
        /// mid-walk and DSP rebuilds after a device change.</summary>
        public void SnapTo(in ThreadAudioParams p)
        {
            int m = p.layerMask;
            m_gWind = On(m, AudioLayer.Wind) ? p.wind : 0f;
            m_gDrone = On(m, AudioLayer.Drone) ? p.drone : 0f;
            m_gPad = On(m, AudioLayer.Pad) ? p.pad : 0f;
            m_gHush = On(m, AudioLayer.Hush) ? p.hush : 0f;
            m_gHum = On(m, AudioLayer.Hum) ? p.hum : 0f;
            m_gStars = On(m, AudioLayer.Stars) ? p.stars : 0f;
            m_gBeacon = On(m, AudioLayer.Beacon) ? p.beacon : 0f;
            bool br = On(m, AudioLayer.Breath);
            m_gBLL = br ? p.breathLL : 0f; m_gBLR = br ? p.breathLR : 0f; m_gBRL = br ? p.breathRL : 0f; m_gBRR = br ? p.breathRR : 0f;
            for (int v = 0; v < 4; v++) m_padEnv[v] = p.PadGate(v);
            m_gStep = p.stepBus; m_gMaster = p.master;
            m_lpHz = Math.Max(20.0, p.lowpassHz);
            SetLowpass(m_lpHz);
            m_fdn.SetTargets(p.rt60, p.preDelayMs, p.wetDb, p.dampHz);
            m_fdn.Snap();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool On(int mask, int layer) { return (mask & (1 << layer)) != 0; }

        void SetLowpass(double hz)
        {
            m_lpL.Set(hz, 0.7071, m_fs);
            float i1 = m_lpR.ic1, i2 = m_lpR.ic2;
            m_lpR = m_lpL; m_lpR.ic1 = i1; m_lpR.ic2 = i2;   // copy coefficients, keep the right channel state
        }

        // ================================================================ processing

        /// <summary>
        /// Adds every layer to the listener mix in buf (interleaved, any channel count; channels 0/1 = L/R, others
        /// untouched, mono gets the average) and runs the master chain in place. Allocation-free.
        /// </summary>
        public void Process(float[] buf, int channels, in ThreadAudioParams p)
        {
            if (buf == null || channels <= 0) return;
            int frames = buf.Length / channels;
            int mask = p.layerMask;
            m_fdn.SetTargets(p.rt60, p.preDelayMs, p.wetDb, p.dampHz);
            float tWind = On(mask, AudioLayer.Wind) ? p.wind : 0f;
            float tDrone = On(mask, AudioLayer.Drone) ? p.drone : 0f;
            float tPad = On(mask, AudioLayer.Pad) ? p.pad : 0f;
            float tHush = On(mask, AudioLayer.Hush) ? p.hush : 0f;
            float tHum = On(mask, AudioLayer.Hum) ? p.hum : 0f;
            float tStars = On(mask, AudioLayer.Stars) ? p.stars : 0f;
            float tBeacon = On(mask, AudioLayer.Beacon) ? p.beacon : 0f;
            bool breathOn = On(mask, AudioLayer.Breath);
            double acc = 0.0;
            int f0 = 0;
            while (f0 < frames)
            {
                int n = Math.Min(Block, frames - f0);
                float[] l = m_l, r = m_r;
                for (int i = 0; i < n; i++) { l[i] = 0f; r[i] = 0f; }

                Wind(n, tWind);
                Drone(n, tDrone);
                Pad(n, tPad, in p);
                Breath(n, breathOn ? p.breathLL : 0f, breathOn ? p.breathLR : 0f, breathOn ? p.breathRL : 0f, breathOn ? p.breathRR : 0f);
                Hush(n, tHush);
                Hum(n, tHum, p.humUpper);
                Stars(n, tStars, p.starGate);
                Beacon(n, tBeacon);
                acc += Master(buf, channels, f0, n, p.stepBus, p.master, p.lowpassHz);

                m_time += n * m_dt;
                f0 += n;
            }
            if (!(acc < 1e30))   // NaN or Inf anywhere: one NaN would poison the FDN forever
            {
                Reset();
                Array.Clear(buf, 0, buf.Length);
                NanResets++;
                return;
            }
            FlushDenormals();
        }

        void FlushDenormals()
        {
            m_pinkWL.Flush(); m_pinkWR.Flush(); m_pinkHL.Flush(); m_pinkHR.Flush();
            m_w1L.Flush(); m_w1R.Flush(); m_w2L.Flush(); m_w2R.Flush();
            m_padLpL.Flush(); m_padLpR.Flush();
            m_bLpL.Flush(); m_bLpR.Flush(); m_bBpL.Flush(); m_bBpR.Flush();
            m_hHpL.Flush(); m_hHpR.Flush(); m_hLpL.Flush(); m_hLpR.Flush();
            m_lpL.Flush(); m_lpR.Flush();
            if (Math.Abs(m_dcL.y1) < 1e-20f) m_dcL.y1 = 0f;
            if (Math.Abs(m_dcR.y1) < 1e-20f) m_dcR.y1 = 0f;
            m_fdn.Flush();
        }

        // ---------------------------------------------------------------- L1 wind
        void Wind(int n, float target)
        {
            float g = m_gWind;
            // LFO clocks always advance (phase continuity whether or not the layer sounds)
            double lfo1 = SineTable.Lookup(m_phW1), lfo2 = SineTable.Lookup(m_phW2);
            m_phW1 += AudioSpec.WindLfo1Hz * n * m_dt; if (m_phW1 >= 1.0) m_phW1 -= 1.0;
            m_phW2 += AudioSpec.WindLfo2Hz * n * m_dt; if (m_phW2 >= 1.0) m_phW2 -= 1.0;
            m_gustT += n * m_dt / AudioSpec.WindGustTau;
            while (m_gustT >= 1.0) { m_gustT -= 1.0; m_gustA = m_gustB; m_gustB = m_rngGust.NextBipolar(); }
            if (target <= 0f && g < Silent) { m_gWind = 0f; return; }

            double fs = m_fs;
            double fc1 = AudioSpec.WindFc1 * (1.0 + AudioSpec.WindLfoDepth * lfo1);
            double fc2 = AudioSpec.WindFc2 * (1.0 + AudioSpec.WindLfoDepth * lfo2);
            m_w1L.Set(fc1, AudioSpec.WindQ1, fs); CopyCoef(ref m_w1R, in m_w1L);
            m_w2L.Set(fc2, AudioSpec.WindQ2, fs); CopyCoef(ref m_w2R, in m_w2L);
            float w = (float)(0.5 - 0.5 * Math.Cos(Math.PI * m_gustT));
            float gust = (float)AudioSpec.WindGustBase + (float)AudioSpec.WindGustDepth * (m_gustA + w * (m_gustB - m_gustA));
            float k = gust * m_gustNorm * m_windNorm, b2 = m_windBand2, a = m_aFast;
            float[] l = m_l, r = m_r;
            for (int i = 0; i < n; i++)
            {
                g += a * (target - g);
                float xl = m_pinkWL.Next(m_rngWL.NextBipolar());
                float xr = m_pinkWR.Next(m_rngWR.NextBipolar());
                float yl = m_w1L.BandNorm(xl) + b2 * m_w2L.BandNorm(xl);
                float yr = m_w1R.BandNorm(xr) + b2 * m_w2R.BandNorm(xr);
                float gk = g * k;
                l[i] += gk * yl; r[i] += gk * yr;
            }
            m_gWind = g;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CopyCoef(ref SvfTpt dst, in SvfTpt src) { dst.a1 = src.a1; dst.a2 = src.a2; dst.a3 = src.a3; dst.k = src.k; }

        // ---------------------------------------------------------------- L2 drone
        void Drone(int n, float target)
        {
            float g = m_gDrone;
            float lfo = 1f + (float)AudioSpec.DroneLfoDepth * SineTable.Lookup(m_phDroneLfo);
            m_phDroneLfo += AudioSpec.DroneLfoHz * n * m_dt; if (m_phDroneLfo >= 1.0) m_phDroneLfo -= 1.0;
            if (target <= 0f && g < Silent)
            {
                m_gDrone = 0f;
                for (int j = 0; j < 4; j++) { m_phDrone[j] += m_incDrone[j] * n; m_phDrone[j] -= Math.Floor(m_phDrone[j]); }
                return;
            }
            float[] T = SineTable.T; const int S = SineTable.Size;
            double p0 = m_phDrone[0], p1 = m_phDrone[1], p2 = m_phDrone[2], p3 = m_phDrone[3];
            double i0 = m_incDrone[0], i1 = m_incDrone[1], i2 = m_incDrone[2], i3 = m_incDrone[3];
            float a0 = m_ampDrone[0] * lfo, a1 = m_ampDrone[1] * lfo, a2 = m_ampDrone[2] * lfo, a3 = m_ampDrone[3] * lfo;
            float a = m_aFast;
            float[] l = m_l, r = m_r;
            for (int i = 0; i < n; i++)
            {
                g += a * (target - g);
                double q; int ix; float fr; float x;
                q = p0 * S; ix = (int)q; fr = (float)(q - ix); x = a0 * (T[ix] + fr * (T[ix + 1] - T[ix]));
                q = p1 * S; ix = (int)q; fr = (float)(q - ix); x += a1 * (T[ix] + fr * (T[ix + 1] - T[ix]));
                q = p2 * S; ix = (int)q; fr = (float)(q - ix); x += a2 * (T[ix] + fr * (T[ix + 1] - T[ix]));
                q = p3 * S; ix = (int)q; fr = (float)(q - ix); x += a3 * (T[ix] + fr * (T[ix + 1] - T[ix]));
                p0 += i0; if (p0 >= 1.0) p0 -= 1.0;
                p1 += i1; if (p1 >= 1.0) p1 -= 1.0;
                p2 += i2; if (p2 >= 1.0) p2 -= 1.0;
                p3 += i3; if (p3 >= 1.0) p3 -= 1.0;
                x *= g;
                l[i] += x; r[i] += x;
            }
            m_phDrone[0] = p0; m_phDrone[1] = p1; m_phDrone[2] = p2; m_phDrone[3] = p3;
            m_gDrone = g;
        }

        // ---------------------------------------------------------------- L3 pad
        void Pad(int n, float target, in ThreadAudioParams p)
        {
            // envelopes at control rate: linear 8 s attack / 10 s release, applied through smoothstep
            float sum = 0f;
            float up = m_padUp * n / Block, down = m_padDown * n / Block;
            for (int v = 0; v < 4; v++)
            {
                float gate = p.PadGate(v), e = m_padEnv[v];
                if (gate > e) { e += up; if (e > gate) e = gate; }
                else if (gate < e) { e -= down; if (e < gate) e = gate; }
                m_padEnv[v] = e;
                float sh = e * e * (3f - 2f * e);
                m_padVoiceAmp[v] = sh;
                sum += sh * sh;
            }
            float norm = 1f / (float)Math.Sqrt(Math.Max(1f, sum));
            float g = m_gPad;
            if ((target <= 0f && g < Silent) || sum <= 0f)
            {
                // nothing sounds: advance the gain smoother in closed form and keep the oscillators running
                m_gPad = target <= 0f && g < Silent ? 0f : target + (g - target) * (float)Math.Pow(1.0 - m_aFast, n);
                for (int j = 0; j < 8; j++) { m_phPad[j] += m_incPad[j] * n; m_phPad[j] -= Math.Floor(m_phPad[j]); }
                return;
            }
            float a = m_aFast;
            float ma = m_padMixA, mb = m_padMixB;
            float[] l = m_l, r = m_r;
            // voice gains per channel (control rate)
            float vl0 = 0, vr0 = 0, vl1 = 0, vr1 = 0, vl2 = 0, vr2 = 0, vl3 = 0, vr3 = 0;
            {
                float k0 = m_padVoiceAmp[0] * norm * m_padNorm[0], k1 = m_padVoiceAmp[1] * norm * m_padNorm[1];
                float k2 = m_padVoiceAmp[2] * norm * m_padNorm[2], k3 = m_padVoiceAmp[3] * norm * m_padNorm[3];
                vl0 = k0 * m_padPanL[0]; vr0 = k0 * m_padPanR[0];
                vl1 = k1 * m_padPanL[1]; vr1 = k1 * m_padPanR[1];
                vl2 = k2 * m_padPanL[2]; vr2 = k2 * m_padPanR[2];
                vl3 = k3 * m_padPanL[3]; vr3 = k3 * m_padPanR[3];
            }
            double[] ph = m_phPad, inc = m_incPad;
            for (int i = 0; i < n; i++)
            {
                g += a * (target - g);
                float sl = 0f, sr = 0f;
                // voice v: triangles 2v (mostly left) and 2v + 1 (mostly right); tri = 4 |phi - 0.5| - 1
                float t0 = 4f * Math.Abs((float)ph[0] - 0.5f) - 1f, t1 = 4f * Math.Abs((float)ph[1] - 0.5f) - 1f;
                sl += vl0 * (ma * t0 + mb * t1); sr += vr0 * (mb * t0 + ma * t1);
                float t2 = 4f * Math.Abs((float)ph[2] - 0.5f) - 1f, t3 = 4f * Math.Abs((float)ph[3] - 0.5f) - 1f;
                sl += vl1 * (ma * t2 + mb * t3); sr += vr1 * (mb * t2 + ma * t3);
                float t4 = 4f * Math.Abs((float)ph[4] - 0.5f) - 1f, t5 = 4f * Math.Abs((float)ph[5] - 0.5f) - 1f;
                sl += vl2 * (ma * t4 + mb * t5); sr += vr2 * (mb * t4 + ma * t5);
                float t6 = 4f * Math.Abs((float)ph[6] - 0.5f) - 1f, t7 = 4f * Math.Abs((float)ph[7] - 0.5f) - 1f;
                sl += vl3 * (ma * t6 + mb * t7); sr += vr3 * (mb * t6 + ma * t7);
                for (int j = 0; j < 8; j++) { double q = ph[j] + inc[j]; ph[j] = q >= 1.0 ? q - 1.0 : q; }
                // the low-pass is linear and time-invariant, so one filter per channel serves all voices
                l[i] += g * m_padLpL.Low(sl);
                r[i] += g * m_padLpR.Low(sr);
            }
            m_gPad = g;
        }

        // ---------------------------------------------------------------- L4 colossus breath
        void Breath(int n, float tLL, float tLR, float tRL, float tRR)
        {
            double phL = m_phBreath, phR = m_phBreath + 0.37;   // the two colossi breathe out of step
            if (phR >= 1.0) phR -= 1.0;
            float lfoL = 1f + (float)AudioSpec.BreathLfoDepth * SineTable.Lookup(phL);
            float lfoR = 1f + (float)AudioSpec.BreathLfoDepth * SineTable.Lookup(phR);
            m_phBreath += AudioSpec.BreathLfoHz * n * m_dt; if (m_phBreath >= 1.0) m_phBreath -= 1.0;
            float gLL = m_gBLL, gLR = m_gBLR, gRL = m_gBRL, gRR = m_gBRR;
            float tsum = tLL + tLR + tRL + tRR, gsum = gLL + gLR + gRL + gRR;
            if (tsum <= 0f && gsum < Silent) { m_gBLL = m_gBLR = m_gBRL = m_gBRR = 0f; return; }
            float a = m_aFast, kL = m_breathNorm * lfoL, kR = m_breathNorm * lfoR;
            float[] l = m_l, r = m_r;
            for (int i = 0; i < n; i++)
            {
                gLL += a * (tLL - gLL); gLR += a * (tLR - gLR); gRL += a * (tRL - gRL); gRR += a * (tRR - gRR);
                float xl = kL * m_bBpL.BandNorm(m_bLpL.Low(m_rngBL.NextBipolar()));
                float xr = kR * m_bBpR.BandNorm(m_bLpR.Low(m_rngBR.NextBipolar()));
                l[i] += gLL * xl + gRL * xr;
                r[i] += gLR * xl + gRR * xr;
            }
            m_gBLL = gLL; m_gBLR = gLR; m_gBRL = gRL; m_gBRR = gRR;
        }

        // ---------------------------------------------------------------- L5 mist hush
        void Hush(int n, float target)
        {
            float g = m_gHush;
            if (target <= 0f && g < Silent) { m_gHush = 0f; return; }
            float a = m_aFast, k = m_hushNorm;
            float[] l = m_l, r = m_r;
            for (int i = 0; i < n; i++)
            {
                g += a * (target - g);
                float xl = m_hLpL.Low(m_hHpL.High(m_pinkHL.Next(m_rngHL.NextBipolar())));
                float xr = m_hLpR.Low(m_hHpR.High(m_pinkHR.Next(m_rngHR.NextBipolar())));
                float gk = g * k;
                l[i] += gk * xl; r[i] += gk * xr;
            }
            m_gHush = g;
        }

        // ---------------------------------------------------------------- L6 thread hum
        void Hum(int n, float target, float upper)
        {
            // tremolos and the D-dependent normaliser at control rate
            var parts = AudioSpec.HumPartials;
            double depth = AudioSpec.HumTremoloDepth;
            double pw = 0.0;
            for (int j = 0; j < HumN; j++)
            {
                double w = parts[j] >= 5 ? Math.Max(0.0, Math.Min(1.0, upper)) : 1.0;   // 5th and 7th fade in with D
                double a0 = w / parts[j];
                pw += 0.5 * a0 * a0;
                float trem = 1f + (float)depth * SineTable.Lookup(m_phTrem[j]);
                m_phTrem[j] += m_incTrem[j] * n / Block; if (m_phTrem[j] >= 1.0) m_phTrem[j] -= 1.0;
                float amp = (float)w * m_humBase[j] * trem;
                m_humAmpL[j] = amp * m_humPanL[j];
                m_humAmpR[j] = amp * m_humPanR[j];
            }
            pw *= 1.0 + 0.5 * depth * depth;
            float norm = (float)(1.0 / Math.Sqrt(Math.Max(1e-30, pw)));
            float g = m_gHum;
            if (target <= 0f && g < Silent)
            {
                m_gHum = 0f;
                for (int j = 0; j < HumN; j++) { m_phHum[j] += m_incHum[j] * n; m_phHum[j] -= Math.Floor(m_phHum[j]); }
                return;
            }
            float[] T = SineTable.T; const int S = SineTable.Size;
            float a = m_aFast;
            float[] l = m_l, r = m_r;
            double[] ph = m_phHum, inc = m_incHum;
            float[] al = m_humAmpL, ar = m_humAmpR;
            for (int i = 0; i < n; i++)
            {
                g += a * (target - g);
                float sl = 0f, sr = 0f;
                for (int j = 0; j < HumN; j++)
                {
                    double q = ph[j] * S; int ix = (int)q; float fr = (float)(q - ix);
                    float x = T[ix] + fr * (T[ix + 1] - T[ix]);
                    sl += al[j] * x; sr += ar[j] * x;
                    double np = ph[j] + inc[j]; ph[j] = np >= 1.0 ? np - 1.0 : np;
                }
                float gk = g * norm;
                l[i] += gk * sl; r[i] += gk * sr;
            }
            m_gHum = g;
        }

        // ---------------------------------------------------------------- L7 star notes
        void Stars(int n, float target, float gate)
        {
            float g = m_gStars;
            bool any = false;
            for (int v = 0; v < MaxStars; v++) any |= m_stars[v].active;
            // Poisson trigger at control rate: P(event in n frames) = 1 - exp(-rate n / fs)
            if (gate > 0.5f && target > 0f)
            {
                double pEv = 1.0 - Math.Exp(-m_starRate * n * m_dt);
                if (m_rngStars.NextDouble01() < pEv) { TriggerStar(); any = true; }
            }
            if (!any && target <= 0f && g < Silent) { m_gStars = 0f; return; }
            if (!any) { for (int i = 0; i < n; i++) g += m_aFast * (target - g); m_gStars = g; return; }
            float[] T = SineTable.T; const int S = SineTable.Size;
            float a = m_aFast, amp = m_starAmp;
            float[] l = m_l, r = m_r;
            for (int v = 0; v < MaxStars; v++)
            {
                if (!m_stars[v].active) continue;
                StarVoice sv = m_stars[v];
                float gg = g;
                for (int i = 0; i < n; i++)
                {
                    gg += a * (target - gg);
                    if (sv.attackLeft > 0) { sv.env += sv.attackInc; sv.attackLeft--; if (sv.env > 1f) sv.env = 1f; }
                    else sv.env *= sv.decayMul;
                    double q = sv.phase * S; int ix = (int)q; float fr = (float)(q - ix);
                    float x = gg * amp * sv.env * (T[ix] + fr * (T[ix + 1] - T[ix]));
                    sv.phase += sv.inc; if (sv.phase >= 1.0) sv.phase -= 1.0;
                    l[i] += sv.gl * x; r[i] += sv.gr * x;
                }
                if (sv.attackLeft <= 0 && sv.env < 1e-4f) sv.active = false;   // -80 dB: done
                m_stars[v] = sv;
            }
            for (int i = 0; i < n; i++) g += a * (target - g);
            m_gStars = g;
        }

        void TriggerStar()
        {
            int slot = -1; float quiet = 2f;
            for (int v = 0; v < MaxStars; v++)
            {
                if (!m_stars[v].active) { slot = v; break; }
                if (m_stars[v].attackLeft <= 0 && m_stars[v].env < quiet) { quiet = m_stars[v].env; slot = v; }
            }
            if (slot < 0) return;
            var notes = ThreadConstants.StarNoteFreqs;
            int note = (int)(m_rngStars.Next01() * notes.Length);
            if (note >= notes.Length) note = notes.Length - 1;
            if (note == m_lastNote) note = (note + 1 + (int)(m_rngStars.Next01() * (notes.Length - 1))) % notes.Length;
            m_lastNote = note;
            double pan = (2.0 * m_rngStars.NextDouble01() - 1.0) * AudioSpec.StarMaxPan;
            ThreadAudioParams.PanGains(pan, out double gl, out double gr);
            StarVoice sv;
            sv.active = true;
            sv.phase = 0.0;
            sv.inc = notes[note] / m_fs;
            sv.env = 0f;
            sv.gl = (float)gl; sv.gr = (float)gr;
            sv.attackLeft = m_starAttackFrames;
            sv.attackInc = m_starAttackInc;
            sv.decayMul = m_starDecayMul;
            // a stolen voice that still rings restarts from its level (no click)
            if (m_stars[slot].active) { sv.env = m_stars[slot].env; sv.attackLeft = (int)((1f - sv.env) * m_starAttackFrames); sv.phase = m_stars[slot].phase; }
            m_stars[slot] = sv;
        }

        // ---------------------------------------------------------------- L8 beacon
        void Beacon(int n, float target)
        {
            float g = m_gBeacon;
            float pulse = 1f + (float)AudioSpec.BeaconPulseDepth * SineTable.Lookup(m_phPulse);
            m_phPulse += AudioSpec.BeaconPulseHz * n * m_dt; if (m_phPulse >= 1.0) m_phPulse -= 1.0;
            if (target <= 0f && g < Silent)
            {
                m_gBeacon = 0f;
                m_phBeacon += m_incBeacon * n; m_phBeacon -= Math.Floor(m_phBeacon);
                return;
            }
            float[] T = SineTable.T; const int S = SineTable.Size;
            float a = m_aFast, k = m_beaconAmp * pulse;
            double ph = m_phBeacon, inc = m_incBeacon;
            float[] l = m_l, r = m_r;
            for (int i = 0; i < n; i++)
            {
                g += a * (target - g);
                double q = ph * S; int ix = (int)q; float fr = (float)(q - ix);
                float x = g * k * (T[ix] + fr * (T[ix + 1] - T[ix]));
                ph += inc; if (ph >= 1.0) ph -= 1.0;
                l[i] += x; r[i] += x;
            }
            m_phBeacon = ph;
            m_gBeacon = g;
        }

        // ---------------------------------------------------------------- master chain
        double Master(float[] buf, int ch, int f0, int n, float tStep, float tMaster, float tLp)
        {
            // master low-pass cutoff: 20 ms one-pole per sub-block, coefficients per sub-block
            double target = Math.Max(20.0, tLp);
            double aBlk = 1.0 - Math.Pow(1.0 - m_aFast, n);
            double hz = m_lpHz + aBlk * (target - m_lpHz);
            if (Math.Abs(hz - m_lpHz) > 1e-6 * hz || m_lpL.k == 0f) { m_lpHz = hz; SetLowpass(hz); }
            m_fdn.Control(n);

            float aF = m_aFast, aM = m_aMaster;
            float gS = m_gStep, gM = m_gMaster;
            float c = m_ceiling, ic = m_invCeiling;
            float[] l = m_l, r = m_r;
            double acc = 0.0;
            for (int i = 0; i < n; i++)
            {
                int idx = (f0 + i) * ch;
                float inL = buf[idx], inR = ch > 1 ? buf[idx + 1] : inL;
                gS += aF * (tStep - gS);
                float L = inL * gS + l[i], R = inR * gS + r[i];
                L = m_dcL.Process(L); R = m_dcR.Process(R);
                m_fdn.Tick(L, R, out float wl, out float wr);
                L += wl; R += wr;
                L = m_lpL.Low(L); R = m_lpR.Low(R);
                gM += aM * (tMaster - gM);
                L *= gM; R *= gM;
                L = SoftClip(L, c, ic); R = SoftClip(R, c, ic);
                acc += Math.Abs(L) + Math.Abs(R);
                if (ch == 1) buf[idx] = 0.5f * (L + R);
                else { buf[idx] = L; buf[idx + 1] = R; }
            }
            m_gStep = gS; m_gMaster = gM;
            return acc;
        }

        /// <summary>y = c tanh(x / c); a 7th-order series below |x / c| = 0.1 (error below 1e-8), Math.Tanh above.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float SoftClip(float x, float c, float ic)
        {
            float u = x * ic;
            if (u < 0.1f && u > -0.1f)
            {
                float u2 = u * u;
                return c * u * (1f - u2 * (0.33333333f - u2 * (0.13333333f - u2 * 0.05396825f)));
            }
            return c * (float)Math.Tanh(u);
        }

        /// <summary>Limiter transfer (tests).</summary>
        public static float Limit(float x) { float c = (float)AudioSpec.LimiterCeiling; return SoftClip(x, c, 1f / c); }
    }
}
