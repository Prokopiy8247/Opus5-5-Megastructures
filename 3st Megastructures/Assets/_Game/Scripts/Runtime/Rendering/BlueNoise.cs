// BlueNoise: the 64 x 64 dither tile of ThreadGrade (MASTER_PROMPT 14.7.1 #4, 14.12), generated in code at
// first use by Ulichney's void-and-cluster method (SPIE 1913, 1993) on a toroidal grid with a Gaussian
// energy kernel of sigma 1.5. Deterministic (own SplitMix64 PRNG, fixed seed, first-index tie breaks),
// about 0.1 s: the energy field is updated incrementally (each toggle adds or subtracts a precomputed
// kernel window, no exp in the loop) and the kernel is truncated at 7 sigma (exp(-24.5) = 2e-11, below
// double rounding of the sums), so a toggle touches 23 x 23 cells instead of 4096.
//
// Texture: RFloat (R16 UNorm fallback), point filter, repeat wrap, no mips, linear, value (rank + 0.5) / 4096,
// i.e. a flat histogram in (0, 1) with mean 0.5. The shader remaps it monotonically to a triangular PDF, so
// the blue-noise ordering survives (research R6 3.2, 3.3). No imported asset is used (Part 6).
using System;
using System.Diagnostics;
using UnityEngine;

namespace Megastructure.Rendering
{
    public static class BlueNoise
    {
        /// <summary>Tile size (14.12: 64 x 64).</summary>
        public const int Size = ThreadConstants.DitherTile;
        /// <summary>PRNG seed of the initial binary pattern (any fixed value; the result is deterministic).</summary>
        public const ulong Seed = 20261029UL;
        /// <summary>Fraction of minority pixels in the initial random pattern (Ulichney: about 10 percent).</summary>
        public const double InitialDensity = 0.1;
        /// <summary>Kernel truncation radius in sigmas.</summary>
        public const double KernelSigmas = 7.0;

        static int[] s_ranks;
        static Texture2D s_tex;

        /// <summary>Wall time of the last generation (ms), for the load log and the under-1-s check.</summary>
        public static double LastGenerationMs { get; private set; }

        /// <summary>The cached rank map (index = y * Size + x), generated on first use.</summary>
        public static int[] Ranks
        {
            get
            {
                if (s_ranks == null)
                {
                    var sw = Stopwatch.StartNew();
                    s_ranks = GenerateRanks(Size, ThreadConstants.DitherSigma, Seed);
                    LastGenerationMs = sw.Elapsed.TotalMilliseconds;
                }
                return s_ranks;
            }
        }

        /// <summary>The dither tile texture (created once, survives scene loads; re-created if destroyed).</summary>
        public static Texture2D Get()
        {
            if (s_tex == null) s_tex = CreateTexture(Ranks, Size);
            return s_tex;
        }

        /// <summary>Uniform value stored for a rank: (rank + 0.5) / N, in (0, 1).</summary>
        public static float RankToUniform(int rank, int count) => (float)((rank + 0.5) / count);

        // ------------------------------------------------------------------------------------------------
        // Void-and-cluster.
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Void-and-cluster rank map of an n x n torus (n a power of two, at least 8). Returns rank[y * n + x],
        /// a permutation of 0 .. n*n-1. Pure C#, allocation only here (never per frame).
        /// </summary>
        public static int[] GenerateRanks(int n, double sigma, ulong seed)
        {
            if (n < 8 || (n & (n - 1)) != 0) throw new ArgumentException("n must be a power of two >= 8", nameof(n));
            if (!(sigma > 0.0)) throw new ArgumentException("sigma must be positive", nameof(sigma));
            int count = n * n, mask = n - 1, shift = 0;
            while ((1 << shift) < n) shift++;

            // Gaussian kernel window K[(dy + R) * W + (dx + R)], truncated at R = 7 sigma (never wraps onto itself).
            int R = Math.Min(n / 2 - 1, (int)Math.Ceiling(KernelSigmas * sigma));
            int W = 2 * R + 1;
            var kernel = new double[W * W];
            double inv2s2 = 1.0 / (2.0 * sigma * sigma);
            for (int dy = -R; dy <= R; dy++)
                for (int dx = -R; dx <= R; dx++)
                    kernel[(dy + R) * W + (dx + R)] = Math.Exp(-(dx * dx + dy * dy) * inv2s2);

            var state = new Field(n, mask, shift, R, W, kernel);

            // 0. Initial random pattern of ~10 percent ones (partial Fisher-Yates with SplitMix64).
            var rng = new SplitMix64(seed);
            int ones = Math.Max(1, (int)Math.Round(count * InitialDensity));
            var idx = new int[count];
            for (int i = 0; i < count; i++) idx[i] = i;
            for (int i = 0; i < ones; i++)
            {
                int j = i + (int)(rng.Next() % (ulong)(count - i));
                int t = idx[i]; idx[i] = idx[j]; idx[j] = t;
                state.Set(idx[i], true);
            }

            // 1. Prototype: move the tightest cluster to the largest void until the move is a no-op.
            for (int guard = 0; guard < 4 * count; guard++)
            {
                int c = state.TightestCluster();
                state.Set(c, false);
                int v = state.LargestVoid();
                state.Set(v, true);
                if (v == c) break;
            }
            var protoBits = (bool[])state.bits.Clone();
            var protoEnergy = (double[])state.energy.Clone();

            var rank = new int[count];
            // 2. Phase 1: remove tightest clusters; rank = number of ones left after the removal.
            for (int r = ones - 1; r >= 0; r--)
            {
                int c = state.TightestCluster();
                rank[c] = r;
                state.Set(c, false);
            }
            // 3. Phases 2 + 3: from the prototype, fill largest voids up to N - 1. With a linear energy on a torus,
            //    E0 = sum(K) - E1, so "tightest cluster of zeros" (phase 3) equals "largest void of ones" (research R6 3.3).
            Array.Copy(protoBits, state.bits, count);
            Array.Copy(protoEnergy, state.energy, count);
            for (int r = ones; r < count; r++)
            {
                int v = state.LargestVoid();
                rank[v] = r;
                state.Set(v, true);
            }
            return rank;
        }

        /// <summary>Binary pattern plus its incrementally updated energy field.</summary>
        sealed class Field
        {
            public readonly bool[] bits;
            public readonly double[] energy;
            readonly int n, mask, shift, R, W, count;
            readonly double[] kernel;

            public Field(int n, int mask, int shift, int R, int W, double[] kernel)
            {
                this.n = n; this.mask = mask; this.shift = shift; this.R = R; this.W = W; this.kernel = kernel;
                count = n * n;
                bits = new bool[count];
                energy = new double[count];
            }

            /// <summary>Set or clear pixel p and add or subtract its kernel window (toroidal).</summary>
            public void Set(int p, bool on)
            {
                bits[p] = on;
                double sign = on ? 1.0 : -1.0;
                int px = p & mask, py = p >> shift;
                for (int dy = -R; dy <= R; dy++)
                {
                    int row = ((py + dy) & mask) << shift;
                    int k = (dy + R) * W;
                    for (int dx = -R; dx <= R; dx++)
                        energy[row + ((px + dx) & mask)] += sign * kernel[k + dx + R];
                }
            }

            /// <summary>The one with the highest energy (first index on ties).</summary>
            public int TightestCluster()
            {
                int best = -1;
                double bv = double.NegativeInfinity;
                for (int i = 0; i < count; i++)
                    if (bits[i] && energy[i] > bv) { bv = energy[i]; best = i; }
                return best;
            }

            /// <summary>The zero with the lowest energy (first index on ties).</summary>
            public int LargestVoid()
            {
                int best = -1;
                double bv = double.PositiveInfinity;
                for (int i = 0; i < count; i++)
                    if (!bits[i] && energy[i] < bv) { bv = energy[i]; best = i; }
                return best;
            }
        }

        /// <summary>SplitMix64 (Steele, Lea, Flood 2014): tiny, platform-independent, deterministic.</summary>
        public struct SplitMix64
        {
            ulong m_State;
            public SplitMix64(ulong seed) { m_State = seed; }
            public ulong Next()
            {
                unchecked
                {
                    m_State += 0x9E3779B97F4A7C15UL;
                    ulong z = m_State;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    return z ^ (z >> 31);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Texture.
        // ------------------------------------------------------------------------------------------------

        /// <summary>RFloat (or R16 UNorm) point / repeat / linear / no-mip texture of (rank + 0.5) / N.</summary>
        public static Texture2D CreateTexture(int[] rank, int n)
        {
            int count = n * n;
            bool useFloat = SystemInfo.SupportsTextureFormat(TextureFormat.RFloat);
            var tex = new Texture2D(n, n, useFloat ? TextureFormat.RFloat : TextureFormat.R16, false, true)
            {
                name = "ThreadBlueNoise64",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave,
            };
            if (useFloat)
            {
                var data = new float[count];
                for (int i = 0; i < count; i++) data[i] = RankToUniform(rank[i], count);
                tex.SetPixelData(data, 0);
            }
            else
            {
                // 16-bit UNorm: 65535 / 4096 = 16 codes per rank, so the order is preserved exactly.
                var data = new ushort[count];
                for (int i = 0; i < count; i++) data[i] = (ushort)Math.Round(RankToUniform(rank[i], count) * 65535.0);
                tex.SetPixelData(data, 0);
            }
            tex.Apply(false, false);   // stays readable (16 KB) so tests and QA can verify the uploaded data
            return tex;
        }
    }
}
