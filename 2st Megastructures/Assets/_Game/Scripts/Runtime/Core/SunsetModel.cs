using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Beat-4 phase model (14.5.4): shadow-square geometry (twin of RingSquaresCoverage in RingAtmosphere.hlsl),
    /// the 256-entry star-visibility LUT V(u) integrated from the real SDF, night amount N(u), sky state, terminator.
    /// </summary>
    public static class SunsetModel
    {
        const int LutSize = 256;
        static float[] _lut;

        public static float LeftEdgeA(float u) => 1.2f - 5.33f * (u - 0.10f);

        static float SquareSdf(Vector2 p, Vector2 c, float side, float rotDeg)
        {
            float a = -rotDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(a), co = Mathf.Cos(a);
            Vector2 q = p - c;
            q = new Vector2(q.x * co - q.y * s, q.x * s + q.y * co);
            Vector2 d = new Vector2(Mathf.Abs(q.x) - 0.5f * side, Mathf.Abs(q.y) - 0.5f * side);
            Vector2 dm = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return dm.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        /// <summary>Coverage of the squares at star-plane position p (deg); hard edge (used for the integral).</summary>
        public static bool Covered(Vector2 p, float u, bool squaresBC = true)
        {
            const float cr = 0.99026807f;
            float e = LeftEdgeA(u);
            float cA = e + 11f / cr;
            float cB = e + 22f / cr + 6f + 8f / cr;
            float cC = cB + 8f / cr + 6f + 6f / cr;
            if (SquareSdf(p, new Vector2(cA, 0f), 22f, 8f) <= 0f) return true;
            if (!squaresBC) return false;
            if (SquareSdf(p, new Vector2(cB, 3f), 16f, 8f) <= 0f) return true;
            return SquareSdf(p, new Vector2(cC, -2f), 12f, 8f) <= 0f;
        }

        /// <summary>Visible fraction of the 1.2 deg disc for phase u, by area integration (64 x 64 samples).</summary>
        public static float IntegrateV(float u)
        {
            const int n = 64;
            const float r = RingConstants.StarRadiusDeg;
            int inside = 0, vis = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2((i + 0.5f) / n * 2f - 1f, (j + 0.5f) / n * 2f - 1f) * r;
                    if (p.sqrMagnitude > r * r) continue;
                    inside++;
                    if (!Covered(p, u)) vis++;
                }
            return inside > 0 ? (float)vis / inside : 1f;
        }

        /// <summary>V(u) from the LUT (u clamped to 0..1; 1 before, 0 after).</summary>
        public static float V(float u)
        {
            if (_lut == null)
            {
                _lut = new float[LutSize];
                for (int i = 0; i < LutSize; i++) _lut[i] = IntegrateV(i / (LutSize - 1f));
                // the disc is fully covered from u 0.55 on and stays covered
                for (int i = 0; i < LutSize; i++) if (i / (LutSize - 1f) >= 0.55f) _lut[i] = Mathf.Min(_lut[i], 0f);
            }
            if (u <= 0f) return 1f;
            if (u >= 1f) return 0f;
            float x = u * (LutSize - 1);
            int k = Mathf.Min((int)x, LutSize - 2);
            return Mathf.Lerp(_lut[k], _lut[k + 1], x - k);
        }

        public static float Night(float u) => RingMath.Smoothstep(0.30f, 0.80f, u);
        // day -> dusk over u 0 -> 0.45 (the sky warms as the squares approach and bite; KS05 at u 0.295 is dusk), dusk -> night 0.40 -> 0.95
        public static float SkyState(float u) => RingMath.Smootherstep(0.0f, 0.45f, u) + RingMath.Smootherstep(0.40f, 0.95f, u);
        // The squares emerge over u -0.30..0 (s 379.5-411) instead of -0.6..-0.2 (s 348-390): as translucent panes they
        // upstaged the ark in its own key shot (KS04, s 364; critics run04, S2). They are opaque from the start of beat 4.
        public static float SquaresAlpha(float u) => RingMath.Smoothstep(-0.30f, 0.0f, u);
        public static float TerminatorDeg(float u) => RingConstants.TerminatorMax * RingMath.Smoothstep(0.05f, 0.95f, u);
        /// <summary>Key intensity factor 0.03 + 0.97 V.</summary>
        public static float KeyFactor(float u) => RingConstants.KeyFloor + (1f - RingConstants.KeyFloor) * V(u);
    }
}
