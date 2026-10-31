using System;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// The only two textures of Abyss, generated in code at load (14.2 "Textures"): NoiseTex 256x256 tileable value noise
    /// and StreakTex 512x128 vertical water streaks. Both are LINEAR data (sRGB off), mipmapped, anisotropic 16x, repeat
    /// wrap, and bound as shader globals _AbNoiseTex / _AbStreakTex (declared in Shaders/Include/AbyssCommon.hlsl).
    /// Deterministic: a fixed salt (the textures are art, not gameplay randomness), integer PCG hashes, no UnityEngine.Random.
    ///
    /// NoiseTex channels (each histogram-equalised to a uniform distribution on [0, 1], so a threshold t selects exactly
    /// 1 - t of the surface, e.g. rust coverage): R = fBm with lattice periods 4..32 cells, G = independent fBm (rust mask),
    /// B = finer fBm (mottling), A = finest fBm. Exactly periodic over the texture (lattice periods divide 256).
    /// StreakTex: R = streak darkness centred on 0.5 (mean exactly 0.5, range [0, 1]), G = fine vertical grain (mean 0.5);
    /// periodic in both axes; one texture repeat = 12 m horizontally and 36 m vertically on the walls.
    /// </summary>
    public static class ProceduralTextures
    {
        public static readonly int NoiseTexId = Shader.PropertyToID("_AbNoiseTex");
        public static readonly int StreakTexId = Shader.PropertyToID("_AbStreakTex");

        public const int NoiseSize = 256;
        public const int StreakWidth = 512;
        public const int StreakHeight = 128;
        public const int StreakCount = 150;
        const uint Salt = 0xA5B1u;

        static Texture2D s_noise;
        static Texture2D s_streak;

        /// <summary>The generated NoiseTex (null before <see cref="Generate"/>).</summary>
        public static Texture2D NoiseTex => s_noise;

        /// <summary>The generated StreakTex (null before <see cref="Generate"/>).</summary>
        public static Texture2D StreakTex => s_streak;

        /// <summary>
        /// Generate (once per domain reload) both textures, set them as shader globals and store them on the library.
        /// Called by GameBootstrap during the black load fade; safe in EditMode. lib may be null.
        /// </summary>
        public static void Generate(MaterialLibrary lib)
        {
            if (s_noise == null)
            {
                s_noise = CreateTexture("AbNoiseTex", NoiseSize, NoiseSize, BuildNoisePixels());
            }
            if (s_streak == null)
            {
                s_streak = CreateTexture("AbStreakTex", StreakWidth, StreakHeight, BuildStreakPixels());
            }
            Shader.SetGlobalTexture(NoiseTexId, s_noise);
            Shader.SetGlobalTexture(StreakTexId, s_streak);
            if (lib != null)
            {
                lib.noiseTex = s_noise;
                lib.streakTex = s_streak;
            }
        }

        /// <summary>Destroy the cached textures (EditMode tests / domain cleanup). The next Generate rebuilds them.</summary>
        public static void Release()
        {
            if (s_noise != null) DestroyAny(s_noise);
            if (s_streak != null) DestroyAny(s_streak);
            s_noise = null;
            s_streak = null;
        }

        static void DestroyAny(UnityEngine.Object o)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        static Texture2D CreateTexture(string name, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true)   // mipChain, linear (data, not colour)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 16,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);   // box-filtered mip chain, then release the CPU copy
            return tex;
        }

        // ------------------------------------------------------------------------------------------------ hashing
        /// <summary>PCG RXS-M-XS hash (same constants as AbPcg in AbyssCommon.hlsl).</summary>
        public static uint Pcg(uint v)
        {
            uint s = v * 747796405u + 2891336453u;
            uint w = ((s >> (int)((s >> 28) + 4u)) ^ s) * 277803737u;
            return (w >> 22) ^ w;
        }

        static float U01(uint h) => (h >> 8) * (1f / 16777216f);

        static float LatticeValue(int x, int y, int period, uint salt)
        {
            uint ux = (uint)(((x % period) + period) % period);
            uint uy = (uint)(((y % period) + period) % period);
            return U01(Pcg(ux ^ Pcg(uy ^ Pcg(salt))));
        }

        static float Quintic(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>Periodic value noise at pixel (px, py) of a size x size texture with 'period' lattice cells.</summary>
        static float ValueNoise(int px, int py, int size, int period, uint salt)
        {
            float fx = (px + 0.5f) * period / size;
            float fy = (py + 0.5f) * period / size;
            int ix = (int)Math.Floor(fx);
            int iy = (int)Math.Floor(fy);
            float ux = Quintic(fx - ix);
            float uy = Quintic(fy - iy);
            float a = LatticeValue(ix, iy, period, salt);
            float b = LatticeValue(ix + 1, iy, period, salt);
            float c = LatticeValue(ix, iy + 1, period, salt);
            float d = LatticeValue(ix + 1, iy + 1, period, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        /// <summary>fBm of periodic value noise: octaves double the lattice period from p0 (gain 0.5).</summary>
        static float Fbm(int px, int py, int size, int p0, int octaves, uint salt)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = p0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(px, py, size, period, salt + (uint)o * 7919u);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Replace values by their normalised rank (histogram equalisation to uniform [0, 1)).</summary>
        static void Equalise(float[] values, float[] scratchKeys, int[] order)
        {
            int n = values.Length;
            for (int i = 0; i < n; i++) { scratchKeys[i] = values[i]; order[i] = i; }
            Array.Sort(scratchKeys, order);
            for (int r = 0; r < n; r++) values[order[r]] = (r + 0.5f) / n;
        }

        // ------------------------------------------------------------------------------------------------ NoiseTex
        /// <summary>Pixels of NoiseTex (row-major, y up). Deterministic; public for the EditMode tests.</summary>
        public static Color32[] BuildNoisePixels()
        {
            int size = NoiseSize;
            int n = size * size;
            var r = new float[n];
            var g = new float[n];
            var b = new float[n];
            var a = new float[n];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    r[i] = Fbm(x, y, size, 4, 4, Salt + 11u);
                    g[i] = Fbm(x, y, size, 4, 4, Salt + 101u);
                    b[i] = Fbm(x, y, size, 16, 3, Salt + 211u);
                    a[i] = Fbm(x, y, size, 32, 3, Salt + 307u);
                }
            }
            var keys = new float[n];
            var order = new int[n];
            Equalise(r, keys, order);
            Equalise(g, keys, order);
            Equalise(b, keys, order);
            Equalise(a, keys, order);
            var px = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                px[i] = new Color32(ToByte(r[i]), ToByte(g[i]), ToByte(b[i]), ToByte(a[i]));
            }
            return px;
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.FloorToInt(v * 256f), 0, 255);

        // ------------------------------------------------------------------------------------------------ StreakTex
        /// <summary>
        /// Pixels of StreakTex (row-major, y up = up the wall). Each streak starts at a random height (it hangs under a
        /// joint or tie hole), is 1-5 px wide (Gaussian, 2.3 cm per px horizontally at 12 m per repeat), 15-110 px long
        /// (0.28 m per px vertically), strongest at its top and fading downward; wrap-around in both axes.
        /// Deterministic; public for the EditMode tests.
        /// </summary>
        public static Color32[] BuildStreakPixels()
        {
            int w = StreakWidth, h = StreakHeight, n = w * h;
            var dark = new float[n];
            var grain = new float[n];
            for (int k = 0; k < StreakCount; k++)
            {
                uint hk = Pcg(Salt ^ (uint)(k * 2654435761u));
                float cx = U01(Pcg(hk + 1u)) * w;
                float sigma = 0.6f + 1.8f * U01(Pcg(hk + 2u));
                float strength = 0.25f + 0.75f * U01(Pcg(hk + 3u));
                float top = U01(Pcg(hk + 4u)) * h;
                float length = 15f + 95f * U01(Pcg(hk + 5u));
                int x0 = Mathf.FloorToInt(cx - 3f * sigma) - 1;
                int x1 = Mathf.CeilToInt(cx + 3f * sigma) + 1;
                int len = Mathf.CeilToInt(length);
                for (int dy = 0; dy <= len; dy++)
                {
                    float t = dy / length;                                   // 0 at the top of the streak
                    float env = Mathf.Clamp01(1f - t) * Mathf.Clamp01(1f - t) * Mathf.Clamp01(dy / 3f);
                    if (env <= 0f) continue;
                    int y = ((Mathf.FloorToInt(top) - dy) % h + h) % h;      // flows downward, wraps
                    float wobble = 0.6f * Mathf.Sin(dy * 0.11f + k);      // gentle meander (art shape, not a hash)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - (cx + wobble);
                        float gauss = Mathf.Exp(-0.5f * dx * dx / (sigma * sigma));
                        int xi = ((x % w) + w) % w;
                        dark[y * w + xi] += strength * env * gauss;
                    }
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // fine vertical grain: periodic value noise stretched 1:12 (period 128 cells across, 4 down)
                    float fx = (x + 0.5f) * 128f / w;
                    float fy = (y + 0.5f) * 4f / h;
                    int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
                    float ux = Quintic(fx - ix), uy = Quintic(fy - iy);
                    float v00 = LatticeValue2(ix, iy, 128, 4, Salt + 509u);
                    float v10 = LatticeValue2(ix + 1, iy, 128, 4, Salt + 509u);
                    float v01 = LatticeValue2(ix, iy + 1, 128, 4, Salt + 509u);
                    float v11 = LatticeValue2(ix + 1, iy + 1, 128, 4, Salt + 509u);
                    grain[y * w + x] = Mathf.Lerp(Mathf.Lerp(v00, v10, ux), Mathf.Lerp(v01, v11, ux), uy);
                }
            }
            // centre both channels on 0.5 (exact mean), scale the darkness so the largest deviation maps to the range
            double meanD = 0, meanG = 0;
            for (int i = 0; i < n; i++) { meanD += dark[i]; meanG += grain[i]; }
            meanD /= n;
            meanG /= n;
            double maxDev = 1e-6;
            for (int i = 0; i < n; i++) maxDev = Math.Max(maxDev, Math.Abs(dark[i] - meanD));
            var px = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                float d = 0.5f + 0.5f * (float)((dark[i] - meanD) / maxDev);
                float gr = 0.5f + (float)(grain[i] - meanG);
                px[i] = new Color32(ToByte(d), ToByte(gr), 128, 255);
            }
            return px;
        }

        static float LatticeValue2(int x, int y, int periodX, int periodY, uint salt)
        {
            uint ux = (uint)(((x % periodX) + periodX) % periodX);
            uint uy = (uint)(((y % periodY) + periodY) % periodY);
            return U01(Pcg(ux ^ Pcg(uy ^ Pcg(salt))));
        }
    }

    /// <summary>
    /// Meshes whose layout is a contract of a Megastructure shader (docs/SHADERS.md). Built once at load.
    /// </summary>
    public static class ShaderMeshes
    {
        /// <summary>
        /// Unit quad for Megastructure/Glow: vertices at (+/-0.5, +/-0.5, 0). The shader billboards it around the object
        /// origin; the object's uniform scale is the light's true diameter. Bounds are inflated (+/-8) because the drawn
        /// sprite grows to the minimum pixel size at distance.
        /// </summary>
        public static Mesh GlowQuad()
        {
            var m = new Mesh { name = "AbGlowQuad", hideFlags = HideFlags.DontSave };
            m.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0, false);
            m.bounds = new Bounds(Vector3.zero, new Vector3(16f, 16f, 16f));
            m.UploadMeshData(true);
            return m;
        }

        /// <summary>
        /// Fan beam prisms for Megastructure/BeamWedge mode 0: 'count' wedges centred at phi_k = phase0 + k * 2 pi / count
        /// (object-space angle in the fan plane from +X toward +Y). Each wedge is 8 vertices with
        /// UV1 = (phi_k, corner 0..3, cap 0/1, 0) and 12 triangles of a closed box; the shader builds the actual positions
        /// (R0/R1/half width/length from the material) and is winding-independent. Positions here are only a rough preview.
        /// Bounds cover every fan rotation for beams up to maxRadius + length.
        /// </summary>
        public static Mesh BeamWedgeMesh(int count, float phase0Rad, float maxRadius, float length)
        {
            var pos = new Vector3[count * 8];
            var uv1 = new Vector4[count * 8];
            var idx = new int[count * 36];
            int[] box =
            {
                0, 1, 2, 0, 2, 3,   // cap 0
                4, 6, 5, 4, 7, 6,   // cap 1
                0, 4, 5, 0, 5, 1,   // side between corners 0-1
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0,
            };
            for (int k = 0; k < count; k++)
            {
                float phi = phase0Rad + k * 2f * Mathf.PI / count;
                for (int cap = 0; cap < 2; cap++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        int v = k * 8 + cap * 4 + c;
                        float r = c < 2 ? 0.5f * maxRadius : maxRadius;
                        float a = phi + ((c == 1 || c == 2) ? 0.1f : -0.1f);
                        pos[v] = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), cap * length);
                        uv1[v] = new Vector4(phi, c, cap, 0f);
                    }
                }
                for (int i = 0; i < 36; i++) idx[k * 36 + i] = k * 8 + box[i];
            }
            var m = new Mesh { name = "AbBeamWedges", hideFlags = HideFlags.DontSave };
            m.SetVertices(pos);
            m.SetUVs(1, uv1);
            m.SetTriangles(idx, 0, false);
            float ext = maxRadius + length;
            m.bounds = new Bounds(new Vector3(0f, 0f, 0.5f * length), new Vector3(2f * ext, 2f * ext, length + 2f * ext));
            m.UploadMeshData(true);
            return m;
        }
    }
}
