// FootstepBank: the footstep recipes of MASTER_PROMPT 14.3.5 rendered into mono float arrays (6 variants per
// surface, deterministic from the seed), the surface / cadence rules, and the FootstepSequencer shared by the
// runtime AudioDirector and the OfflineAudioRenderer (one step per 0.78 m of plan distance, one per 0.80 m on
// the stairs, alternate pan +-0.15, pitch +-3 percent, no immediate variant repeat).
// Every "decay" of 14.3.5 is read as T60 (env = exp(-6.9078 t / T60), R5 section 2), every clip starts at an
// exact 0 (1 ms attack) and ends at an exact 0 (cosine fade), and each variant is normalised to its exact
// peak target. Metal steps get +1 dB of resonator brightness per 0.1 of D (thin air); D is quantised to 0.05
// so the runtime can re-render the metal set in the background (AudioDirector) and the offline renderer uses
// the identical quantisation.
// Pure C#, no UnityEngine. FootstepRenderer.Render is allocation-free (scratch buffers are owned).
using System;

namespace Megastructure.Audio
{
    public static class FootstepRules
    {
        public const int SurfaceCount = 5;
        /// <summary>Clip lengths (s). Pool rule (R5): length &lt;= 4 sources / 3.2 steps/s - 0.05 s = 1.2 s.</summary>
        public static readonly double[] ClipSeconds = { 0.16, 0.22, 0.18, 1.15, 0.22 };
        public const double MaxClipSeconds = 1.15;
        /// <summary>The walkway deck begins flush with the terrace at s = 396 (14.4.2); metal from just after it.</summary>
        public const double MetalFromS = 397.0;

        /// <summary>Surface under the player at plan distance s (14.3.5 column "Surface (beat)").</summary>
        public static FootSurface SurfaceAt(double s)
        {
            if (s < ThreadConstants.StairStartZ) return FootSurface.Salt;                 // beats 1, 2
            if (s < ThreadConstants.StairEndZ) return FootSurface.Stone;                  // beat 3
            if (s < MetalFromS) return FootSurface.Terrace;                               // beat 4
            if (s < ThreadConstants.HelixEndS) return FootSurface.Metal;                  // beats 5, 6, 7
            return FootSurface.Pier;                                                      // the pier (beat 7 end)
        }

        /// <summary>Plan distance between steps: 0.80 m on the stairs (one sound per two treads), else 0.78 m.</summary>
        public static double StepLengthAt(double s)
        {
            return SurfaceAt(s) == FootSurface.Stone ? ThreadConstants.StairStepLength : ThreadConstants.StepLength;
        }

        /// <summary>Metal brightness in dB for darkness D, quantised to 0.05 of D (0.5 dB steps).</summary>
        public static int MetalBrightIndex(double D)
        {
            int q = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, D)) / AudioSpec.MetalBrightQuantum);
            return q;
        }
        public static double MetalBrightDb(int index) { return index * AudioSpec.MetalBrightQuantum * AudioSpec.MetalBrightDbPerD; }

        public static int ClipFrames(FootSurface s, int sampleRate) { return Math.Max(16, (int)(ClipSeconds[(int)s] * sampleRate)); }
    }

    /// <summary>
    /// The footstep recipes of MASTER_PROMPT 14.3.5 (lines 563-567) as named constants. "Decay" values are read as
    /// T60 (R5 section 2). Values in the "chosen" block are not in the spec (mix ratios, variant jitter, fades).
    /// CONTRACT REQUEST: move into ThreadConstants (audio section).
    /// </summary>
    public static class FootstepSpec
    {
        public const double Ln1000 = 6.9078;                  // T60 envelope: exp(-ln(1000) t / T60)
        public const double Attack = 0.001;                   // 1 ms attack (line 563; reused for every click)
        // salt crust (line 563)
        public const double SaltBandHz = 3200.0, SaltBandQ = 0.8, SaltDecay = 0.070;
        public const int SaltClicksMin = 6, SaltClicksMax = 10;
        public const double SaltClickLen = 0.001, SaltClickHzMin = 1000.0, SaltClickHzMax = 6000.0;
        public const double SaltClickT0 = 0.005, SaltClickT1 = 0.035;
        public const double SaltThumpHz = 85.0, SaltThumpDecay = 0.090, SaltThumpDb = -6.0;
        // stone steps (line 564), terrace (line 565)
        public const double StoneClickHz = 1400.0, StoneClickQ = 2.0, StoneClickDecay = 0.028;
        public const double StoneGlideFrom = 150.0, StoneGlideTo = 110.0, StoneBodyDecay = 0.110;
        public const double TerraceLowpassHz = 2500.0, TerraceBodyDecay = 0.070;
        // metal walkway (line 566)
        public const double MetalBurst = 0.002;
        public const double MetalRes1Hz = 380.0, MetalRes1Decay = 0.220, MetalRes2Hz = 1070.0, MetalRes2Decay = 0.120;
        public const double MetalThumpHz = 75.0, MetalThumpDecay = 0.060;
        public const double MetalRingHz = 190.0, MetalRingDecay = 1.2, MetalRingDb = -48.0;
        // pier (line 567)
        public const double PierTickHz = 2000.0, PierTickDecay = 0.060, PierTickDb = -46.0;
        // chosen: mix ratios, variant jitter and fades
        public const double SaltClickAmpMin = 0.25, SaltClickAmpMax = 0.5;
        public const double StoneClickGain = 2.2, StoneBodyGain = 0.8, StoneGlideTau = 0.030, StoneBodyAttack = 0.002;
        public const double MetalRes2Gain = 0.6, MetalBurstGain = 0.18, MetalThumpGain = 0.45, MetalRingAttack = 0.005;
        public const int MetalBurstCandidates = 8;
        public const double PierTickAt0 = 0.006, PierTickAtSpread = 0.006, PierTickAttack = 0.0005;
        public const double JitterFreq = 0.06, JitterDecay = 0.10, JitterBody = 0.04;
        public const double JitterMetalFreq = 0.03, JitterMetalDecay = 0.08, JitterRing = 0.02;
        public const double FadeOut = 0.040, FadeOutMetal = 0.060;
    }

    /// <summary>Renders one footstep variant into a caller buffer. Owns its scratch (allocation-free after construction).</summary>
    public sealed class FootstepRenderer
    {
        readonly int m_fs;
        readonly float[] m_a, m_b, m_c;

        public FootstepRenderer(int sampleRate)
        {
            m_fs = sampleRate > 0 ? sampleRate : (int)ThreadConstants.AudioSampleRate;
            int max = (int)(FootstepRules.MaxClipSeconds * m_fs) + 16;
            m_a = new float[max]; m_b = new float[max]; m_c = new float[max];
        }

        public int SampleRate { get { return m_fs; } }

        /// <summary>Renders (surface, variant) into dst (length >= ClipFrames); returns the frame count.</summary>
        public int Render(FootSurface surface, int variant, int seed, double metalBrightDb, float[] dst)
        {
            int n = Math.Min(dst.Length, FootstepRules.ClipFrames(surface, m_fs));
            var rng = XorShift32.Stream(seed, 0x5700u + (uint)((int)surface * 16 + variant));
            Array.Clear(dst, 0, dst.Length);
            switch (surface)
            {
                case FootSurface.Salt: Salt(dst, n, ref rng); break;
                case FootSurface.Stone: Stone(dst, n, ref rng, FootstepSpec.StoneBodyDecay, false); break;
                case FootSurface.Terrace: Stone(dst, n, ref rng, FootstepSpec.TerraceBodyDecay, true); break;
                case FootSurface.Metal: Metal(dst, n, ref rng, metalBrightDb); break;
                default: Pier(dst, n, ref rng); break;
            }
            FadeOut(dst, n, surface == FootSurface.Metal ? FootstepSpec.FadeOutMetal : FootstepSpec.FadeOut);
            NormalisePeak(dst, n, AudioSpec.FootPeakDb[(int)surface]);
            dst[0] = 0f;
            dst[n - 1] = 0f;
            return n;
        }

        // ------------------------------------------------------------ salt crust (beats 1, 2): -30 dBFS
        // noise burst -> band-pass 3.2 kHz Q 0.8, attack 1 ms, decay 70 ms; 6-10 micro-clicks (1 ms, 1-6 kHz) at
        // random 5-35 ms; thump: sine 85 Hz, decay 90 ms, -6 dB
        void Salt(float[] dst, int n, ref XorShift32 rng)
        {
            double fs = m_fs;
            var bp = new SvfTpt(); bp.Set(FootstepSpec.SaltBandHz * Jitter(ref rng, FootstepSpec.JitterFreq), FootstepSpec.SaltBandQ, fs);
            double tNoise = FootstepSpec.SaltDecay * Jitter(ref rng, FootstepSpec.JitterDecay), tThump = FootstepSpec.SaltThumpDecay * Jitter(ref rng, FootstepSpec.JitterDecay);
            double fThump = FootstepSpec.SaltThumpHz * Jitter(ref rng, FootstepSpec.JitterBody);
            float thumpGain = (float)AudioSpec.Db(FootstepSpec.SaltThumpDb);
            double ph = 0.0, inc = fThump / fs;
            for (int i = 0; i < n; i++)
            {
                double t = i / fs;
                float att = (float)Math.Min(1.0, t / FootstepSpec.Attack);
                float noise = bp.BandNorm(rng.NextBipolar()) * Env(t, tNoise);
                float thump = thumpGain * SineTable.Lookup(ph) * Env(t, tThump);
                ph += inc; if (ph >= 1.0) ph -= 1.0;
                dst[i] = att * (noise + thump);
            }
            int clicks = FootstepSpec.SaltClicksMin + (int)(rng.Next01() * (FootstepSpec.SaltClicksMax - FootstepSpec.SaltClicksMin + 1));
            if (clicks > FootstepSpec.SaltClicksMax) clicks = FootstepSpec.SaltClicksMax;
            int w = Math.Max(2, (int)(FootstepSpec.SaltClickLen * fs));
            for (int c = 0; c < clicks; c++)
            {
                int at = (int)((FootstepSpec.SaltClickT0 + (FootstepSpec.SaltClickT1 - FootstepSpec.SaltClickT0) * rng.Next01()) * fs);
                double hz = FootstepSpec.SaltClickHzMin + (FootstepSpec.SaltClickHzMax - FootstepSpec.SaltClickHzMin) * rng.Next01(), cph = 0.0;
                float amp = (float)(FootstepSpec.SaltClickAmpMin + (FootstepSpec.SaltClickAmpMax - FootstepSpec.SaltClickAmpMin) * rng.Next01());
                for (int j = 0; j < w && at + j < n; j++, cph += hz / fs)
                    dst[at + j] += amp * SineTable.Lookup(cph - Math.Floor(cph)) * (float)Math.Sin(Math.PI * j / w);
            }
        }

        // ------------------------------------------------------------ stone steps (beat 3): -28 dBFS / terrace (beat 4): -30
        // click: noise -> band-pass 1.4 kHz Q 2, decay 28 ms; body: sine glide 150 -> 110 Hz, decay 110 ms
        // terrace: the stone recipe low-passed at 2.5 kHz with a 70 ms body
        void Stone(float[] dst, int n, ref XorShift32 rng, double bodyT60, bool terrace)
        {
            double fs = m_fs;
            var bp = new SvfTpt(); bp.Set(FootstepSpec.StoneClickHz * Jitter(ref rng, FootstepSpec.JitterFreq), FootstepSpec.StoneClickQ, fs);
            double tClick = FootstepSpec.StoneClickDecay * Jitter(ref rng, FootstepSpec.JitterDecay), tBody = bodyT60 * Jitter(ref rng, FootstepSpec.JitterDecay);
            double f0 = FootstepSpec.StoneGlideFrom * Jitter(ref rng, FootstepSpec.JitterBody), f1 = FootstepSpec.StoneGlideTo * Jitter(ref rng, FootstepSpec.JitterBody);
            double ph = 0.0;
            float bodyAmp = (float)FootstepSpec.StoneBodyGain * (0.9f + 0.2f * rng.Next01());
            for (int i = 0; i < n; i++)
            {
                double t = i / fs;
                float att = (float)Math.Min(1.0, t / FootstepSpec.Attack);
                float click = bp.BandNorm(rng.NextBipolar()) * Env(t, tClick);
                double f = f1 + (f0 - f1) * Math.Exp(-t / FootstepSpec.StoneGlideTau);
                float body = bodyAmp * SineTable.Lookup(ph) * (float)Math.Min(1.0, t / FootstepSpec.StoneBodyAttack) * Env(t, tBody);
                ph += f / fs; if (ph >= 1.0) ph -= 1.0;
                dst[i] = att * (float)FootstepSpec.StoneClickGain * click + body;
            }
            if (terrace)
            {
                var lp = new SvfTpt(); lp.Set(FootstepSpec.TerraceLowpassHz, 0.7071, fs);
                for (int i = 0; i < n; i++) dst[i] = lp.Low(dst[i]);
            }
        }

        // ------------------------------------------------------------ metal walkway (beats 5-7): -29 dBFS
        // 2 ms noise burst exciting two resonators, 380 Hz (decay 220 ms) and 1 070 Hz (decay 120 ms), thump 75 Hz
        // 60 ms, faint structure ring sine 190 Hz decay 1.2 s at -48 dB; resonator brightness +1 dB per 0.1 of D
        void Metal(float[] dst, int n, ref XorShift32 rng, double brightDb)
        {
            double fs = m_fs;
            float[] r1 = m_a, r2 = m_b, burst = m_c;
            Array.Clear(r1, 0, n); Array.Clear(r2, 0, n); Array.Clear(burst, 0, n);
            int bn = Math.Min(n, Math.Max(4, (int)(FootstepSpec.MetalBurst * fs)));
            double f1 = FootstepSpec.MetalRes1Hz * Jitter(ref rng, FootstepSpec.JitterMetalFreq), f2 = FootstepSpec.MetalRes2Hz * Jitter(ref rng, FootstepSpec.JitterMetalFreq);
            double t1 = FootstepSpec.MetalRes1Decay * Jitter(ref rng, FootstepSpec.JitterMetalDecay), t2 = FootstepSpec.MetalRes2Decay * Jitter(ref rng, FootstepSpec.JitterMetalDecay);
            // 2 ms Hann-windowed noise burst; a random burst can cancel itself at a resonance (then its transient,
            // not the ring, would set the peak), so the best of 8 seeded candidates is kept: the one whose weaker
            // spectral magnitude at f1 / f2 is largest relative to its energy (deterministic, allocation-free)
            double best = -1.0;
            uint bestState = rng.State;
            for (int c = 0; c < FootstepSpec.MetalBurstCandidates; c++)
            {
                uint state = rng.State;
                double re1 = 0, im1 = 0, re2 = 0, im2 = 0, e = 0;
                for (int i = 0; i < bn; i++)
                {
                    double x = rng.NextBipolar() * (0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (bn - 1)));
                    double a1 = 2.0 * Math.PI * f1 * i / fs, a2 = 2.0 * Math.PI * f2 * i / fs;
                    re1 += x * Math.Cos(a1); im1 += x * Math.Sin(a1); re2 += x * Math.Cos(a2); im2 += x * Math.Sin(a2);
                    e += x * x;
                }
                double score = Math.Min(re1 * re1 + im1 * im1, re2 * re2 + im2 * im2) / Math.Max(1e-30, e);
                if (score > best) { best = score; bestState = state; }
            }
            var brng = new XorShift32(bestState);
            for (int i = 0; i < bn; i++)
                burst[i] = brng.NextBipolar() * (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (bn - 1)));
            // modal band-pass resonators: once the burst stops they ring as pure damped sines of T60
            Resonate(burst, r1, n, f1, t1, fs);
            Resonate(burst, r2, n, f2, t2, fs);
            // ring amplitudes, measured once the burst has ended (pure free decay from there)
            float p1 = PeakFrom(r1, bn, n), p2 = PeakFrom(r2, bn, n), pb = Peak(burst, n);
            float k1 = 1.0f / Math.Max(p1, 1e-12f);
            float k2 = (float)(FootstepSpec.MetalRes2Gain * AudioSpec.Db(brightDb)) / Math.Max(p2, 1e-12f);
            float kb = (float)FootstepSpec.MetalBurstGain / Math.Max(pb, 1e-12f);
            double tThump = FootstepSpec.MetalThumpDecay * Jitter(ref rng, FootstepSpec.JitterDecay), fThump = FootstepSpec.MetalThumpHz * Jitter(ref rng, FootstepSpec.JitterBody), ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / fs;
                float thump = (float)FootstepSpec.MetalThumpGain * SineTable.Lookup(ph) * (float)Math.Min(1.0, t / FootstepSpec.Attack) * Env(t, tThump);
                ph += fThump / fs; if (ph >= 1.0) ph -= 1.0;
                dst[i] = k1 * r1[i] + k2 * r2[i] + kb * burst[i] + thump;
            }
            NormalisePeak(dst, n, AudioSpec.FootPeakDb[(int)FootSurface.Metal]);
            // structure ring at an absolute -48 dBFS, 5 ms attack, T60 1.2 s
            float ring = (float)AudioSpec.Db(FootstepSpec.MetalRingDb);
            double fr = FootstepSpec.MetalRingHz * Jitter(ref rng, FootstepSpec.JitterRing); ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / fs;
                dst[i] += ring * SineTable.Lookup(ph) * (float)Math.Min(1.0, t / FootstepSpec.MetalRingAttack) * Env(t, FootstepSpec.MetalRingDecay);
                ph += fr / fs; if (ph >= 1.0) ph -= 1.0;
            }
        }

        // ------------------------------------------------------------ pier (beat 7 end): -32 dBFS
        // stone recipe, dry, plus a 2 kHz glass tick (decay 60 ms, -46 dBFS)
        void Pier(float[] dst, int n, ref XorShift32 rng)
        {
            Stone(dst, n, ref rng, FootstepSpec.StoneBodyDecay, false);
            NormalisePeak(dst, n, AudioSpec.FootPeakDb[(int)FootSurface.Pier]);
            double fs = m_fs;
            int at = (int)((FootstepSpec.PierTickAt0 + FootstepSpec.PierTickAtSpread * rng.Next01()) * fs);
            float tick = (float)AudioSpec.Db(FootstepSpec.PierTickDb);
            double ph = 0.0, f = FootstepSpec.PierTickHz * Jitter(ref rng, FootstepSpec.JitterRing);
            for (int i = at; i < n; i++)
            {
                double t = (i - at) / fs;
                dst[i] += tick * SineTable.Lookup(ph) * (float)Math.Min(1.0, t / FootstepSpec.PierTickAttack) * Env(t, FootstepSpec.PierTickDecay);
                ph += f / fs; if (ph >= 1.0) ph -= 1.0;
            }
        }

        // ------------------------------------------------------------ helpers
        static float Env(double t, double t60) { return (float)Math.Exp(-FootstepSpec.Ln1000 * t / t60); }

        static double Jitter(ref XorShift32 rng, double amount) { return 1.0 + amount * rng.NextBipolar(); }

        /// <summary>
        /// Modal band-pass resonator y[i] = 2 r cos(w) y[i-1] - r^2 y[i-2] + 0.5 (x[i] - x[i-2]), r = 10^(-3 / (T60 fs)):
        /// a damped sine of the given T60 once the excitation stops; the zeros at DC and Nyquist keep the burst's
        /// off-resonance energy out (an all-pole resonator rings louder in its forced transient than in its ring).
        /// </summary>
        static void Resonate(float[] x, float[] y, int n, double f, double t60, double fs)
        {
            double w = 2.0 * Math.PI * f / fs, r = Math.Pow(10.0, -3.0 / (t60 * fs));
            double c1 = 2.0 * r * Math.Cos(w), c2 = -r * r;
            double y1 = 0.0, y2 = 0.0, x1 = 0.0, x2 = 0.0;
            for (int i = 0; i < n; i++)
            {
                double xi = x[i];
                double v = c1 * y1 + c2 * y2 + 0.5 * (xi - x2);
                y2 = y1; y1 = v; x2 = x1; x1 = xi;
                y[i] = (float)v;
            }
        }

        static float PeakFrom(float[] x, int from, int n)
        {
            float p = 0f;
            for (int i = Math.Max(0, from); i < n; i++) { float a = Math.Abs(x[i]); if (a > p) p = a; }
            return p;
        }

        static float Peak(float[] x, int n)
        {
            float p = 0f;
            for (int i = 0; i < n; i++) { float a = Math.Abs(x[i]); if (a > p) p = a; }
            return p;
        }

        static void NormalisePeak(float[] x, int n, double peakDb)
        {
            float p = Peak(x, n);
            if (p <= 1e-12f) return;
            float k = (float)AudioSpec.Db(peakDb) / p;
            for (int i = 0; i < n; i++) x[i] *= k;
        }

        void FadeOut(float[] x, int n, double seconds)
        {
            int f = Math.Min(n - 1, Math.Max(1, (int)(seconds * m_fs)));
            int s0 = n - f;
            for (int i = s0; i < n; i++)
                x[i] *= (float)(0.5 + 0.5 * Math.Cos(Math.PI * (i - s0) / (f - 1 > 0 ? f - 1 : 1)));
            x[n - 1] = 0f;
        }
    }

    /// <summary>All 6 x 5 footstep variants as exact-length mono arrays (metal at one brightness).</summary>
    public sealed class FootstepBank
    {
        public readonly int SampleRate, Seed;
        public readonly int MetalBrightIndex;
        readonly float[][][] m_clips;

        public FootstepBank(int sampleRate, int seed, int metalBrightIndex)
        {
            SampleRate = sampleRate > 0 ? sampleRate : (int)ThreadConstants.AudioSampleRate;
            Seed = seed;
            MetalBrightIndex = metalBrightIndex;
            var r = new FootstepRenderer(SampleRate);
            int maxFrames = (int)(FootstepRules.MaxClipSeconds * SampleRate) + 16;
            var scratch = new float[maxFrames];
            m_clips = new float[FootstepRules.SurfaceCount][][];
            for (int s = 0; s < FootstepRules.SurfaceCount; s++)
            {
                m_clips[s] = new float[AudioSpec.FootstepVariants][];
                for (int v = 0; v < AudioSpec.FootstepVariants; v++)
                {
                    int n = r.Render((FootSurface)s, v, seed, FootstepRules.MetalBrightDb(metalBrightIndex), scratch);
                    var clip = new float[n];
                    Array.Copy(scratch, clip, n);
                    m_clips[s][v] = clip;
                }
            }
        }

        public float[] Get(FootSurface surface, int variant) { return m_clips[(int)surface][variant]; }
    }

    /// <summary>One footstep decision.</summary>
    public struct StepEvent
    {
        public FootSurface surface;
        public int variant;
        public float pan;
        public float pitch;
    }

    /// <summary>
    /// Step cadence and variation: accumulates walked plan distance and fires one step per step length; the first
    /// step fires as soon as the player starts to move (14.3.6 item 1). Deterministic from the seed.
    /// </summary>
    public struct FootstepSequencer
    {
        XorShift32 m_rng;
        double m_acc;
        int m_lastVariant;
        bool m_right;

        /// <summary>Distance already "pre-walked" when re-armed, so the first step fires after this much movement.</summary>
        public const double FirstStepAfter = 0.08;

        public static FootstepSequencer Create(int seed)
        {
            var q = new FootstepSequencer();
            q.m_rng = XorShift32.Stream(seed, 0x51E9u);
            q.m_lastVariant = -1;
            q.m_right = false;
            q.m_acc = -1.0;   // armed
            return q;
        }

        /// <summary>The next movement starts a fresh stride (call after the player stood still or teleported).</summary>
        public void Rearm() { m_acc = -1.0; }

        /// <summary>Advance by a walked distance (m) at progress s. Returns true when a step fires (at most one per call).</summary>
        public bool Advance(double distance, double s, out StepEvent e)
        {
            e = default(StepEvent);
            if (distance <= 0.0) return false;
            double len = FootstepRules.StepLengthAt(s);
            if (m_acc < 0.0) m_acc = len - FirstStepAfter;
            m_acc += distance;
            if (m_acc < len) return false;
            m_acc -= len;
            if (m_acc > len) m_acc = 0.0;   // a long frame never queues a burst of steps
            e = Next(FootstepRules.SurfaceAt(s));
            return true;
        }

        /// <summary>Distance still to walk until the next step (offline renderer: exact step placement).</summary>
        public double DistanceToNext(double s)
        {
            double len = FootstepRules.StepLengthAt(s);
            double acc = m_acc < 0.0 ? len - FirstStepAfter : m_acc;
            return Math.Max(0.0, len - acc);
        }

        /// <summary>Variant (no immediate repeat), alternating pan and pitch for a step on the given surface.</summary>
        public StepEvent Next(FootSurface surface)
        {
            int nv = AudioSpec.FootstepVariants;
            int v = (int)(m_rng.Next01() * nv); if (v >= nv) v = nv - 1;
            if (v == m_lastVariant) v = (v + 1 + (int)(m_rng.Next01() * (nv - 1))) % nv;
            m_lastVariant = v;
            m_right = !m_right;
            StepEvent e;
            e.surface = surface;
            e.variant = v;
            e.pan = (float)(m_right ? AudioSpec.FootPan : -AudioSpec.FootPan);
            e.pitch = (float)(1.0 + AudioSpec.FootPitchJitter * m_rng.NextBipolar());
            return e;
        }
    }
}
