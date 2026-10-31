using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// C# twin of the GPU placement of RingGrass.shader (RingFieldInstance, RingFieldExcluded) for tests and budgets
    /// (MASTER_PROMPT 14.5.2). The instance of a world cell never depends on the camera: the PCG hash is taken from the
    /// integer world cell, the camera only selects the window.
    /// </summary>
    public static class GrassLayout
    {
        /// <summary>True where no tuft or flower may grow (14.5.2 exclusions). set = _RingSet.x (0 valley, 1 plain ...).</summary>
        public static bool Excluded(float x, float z, int set)
        {
            if (set >= 1 && x >= RingConstants.WallX - 0.05f) return true;
            if (Mathf.Abs(x) < TerrainConfig.ExclPadHalfX && z > TerrainConfig.ExclPadZ0 && z < TerrainConfig.ExclPadZ1) return true;
            if (Mathf.Abs(x) < TerrainConfig.ExclBridgeHalfX && z > TerrainConfig.ExclBridgeZ0 && z < TerrainConfig.ExclBridgeZ1) return true;
            if (Mathf.Abs(x) < TerrainConfig.ExclFootbridgeHalfX && Mathf.Abs(z - TerrainConfig.ExclFootbridgeZ) < TerrainConfig.ExclFootbridgeHalfZ) return true;
            float lm = TerrainConfig.ExclLeveeMargin;
            if (x > RingConstants.LeveeX0 - lm && x < RingConstants.LeveeX1 + lm && z > RingConstants.LeveeZ0 - lm && z < RingConstants.LeveeZ1 + lm) return true;
            if (x > TerrainConfig.ExclApronX0 && x < TerrainConfig.ExclApronX1 && z > TerrainConfig.ExclApronZ0 && z < TerrainConfig.ExclApronZ1) return true;
            if (RingWorld.PathDist(x, z) < TerrainConfig.ExclPathDist) return true;
            if (RingWorld.RiverDist(x, z) < TerrainConfig.ExclRiverDist) return true;
            return false;
        }

        /// <summary>World xz and hash of instance id of a field (grid cell size, cells per side, candidates per cell,
        /// seed) for camera position cam (flat x, z). Mirrors RingFieldInstance.</summary>
        public static Vector2 InstancePosition(int id, Vector2 cam, float cell, int cells, int candidates, int seed, out uint hash)
        {
            unchecked
            {
                int cand = Mathf.Max(candidates, 1);
                int c = id / cand, k = id - c * cand;
                int cz = c / cells, cx = c - cz * cells;
                int sx = Mathf.FloorToInt(cam.x / cell + 0.5f), sz = Mathf.FloorToInt(cam.y / cell + 0.5f);
                int ix = sx + cx - cells / 2, iz = sz + cz - cells / 2;
                uint h = RingMath.Pcg3(ix, iz, (int)((uint)k + (uint)seed * 7u));
                float jx = RingMath.U01(h), jz = RingMath.U01(RingMath.Pcg(h ^ 0x9E3779B9u));
                hash = RingMath.Pcg(h + 0x68E31DA4u);
                return new Vector2((ix + jx) * cell, (iz + jz) * cell);
            }
        }

        /// <summary>Probability that an instance at distance d survives the instance-domain fade
        /// (w = saturate((1.15 fade - thr) / 0.15) &gt; 0 with thr uniform in [0, 1)).</summary>
        public static float SurvivalAt(float d, float fadeStart, float fadeEnd)
        {
            float fade = 1f - RingMath.Smoothstep(fadeStart, fadeEnd, d);
            return Mathf.Clamp01(1.15f * fade);
        }

        /// <summary>Expected number of drawn instances (exclusions ignored): integral of the fade over the disc.</summary>
        public static int ExpectedDrawn(float cell, int candidates, float accept, float fadeStart, float fadeEnd, float radius)
        {
            double sum = 0.0;
            const int steps = 200;
            double dr = radius / steps;
            for (int i = 0; i < steps; i++)
            {
                double r = (i + 0.5) * dr;
                sum += 2.0 * System.Math.PI * r * dr * SurvivalAt((float)r, fadeStart, fadeEnd);
            }
            return (int)(sum / (cell * cell) * candidates * accept);
        }
    }
}
