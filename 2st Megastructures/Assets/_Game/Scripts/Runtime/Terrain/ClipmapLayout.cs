using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// C# twin of the clipmap placement in RingTerrain.shader (TerrainVertex) and the generator of its mesh
    /// (MASTER_PROMPT 14.5.2, 14.7 H7, DEVELOPMENT.md ADR-002). Every level is a 128 x 128 grid of quads with 4 vertices
    /// per quad; the vertex shader places the level around _RingCam, collapses the quads that lie inside the next finer
    /// level and geomorphs the outer 15 %. Tests use the functions below to prove the layout is crack free (the fine
    /// boundary lies on coarse grid lines, every coarse quad is either fully inside or fully outside, the boundary is
    /// fully morphed) without rendering. <see cref="VertexHeight"/> is the height twin (RingWorld.Height at the morphed
    /// position, limited by the continuous path ceiling of the coarse levels, <see cref="PathCeil"/>), so tests can bound
    /// the height change per centimetre of camera travel (<see cref="CeilMaxRate"/>).
    /// Vertex encoding: positionOS = (gx, skirt ? -1 : 0, gz) grid corner indices, uv0 = quad centre (qx + 0.5,
    /// qz + 0.5), uv1 = (spacing, level).
    /// </summary>
    public static class ClipmapLayout
    {
        public static int Levels => RingConstants.ClipLevels;
        public static int Quads => RingConstants.ClipQuads;

        /// <summary>Spacing of level k: 0.5 x 2^k (0.5 .. 128 m), an exact power of two.</summary>
        public static float Spacing(int k) => RingConstants.ClipSpacing0 * (1 << k);

        /// <summary>Half-extent of level k: 64 x spacing (32 .. 8 192 m).</summary>
        public static float HalfExtent(int k) => TerrainConfig.ClipHalf * Spacing(k);

        /// <summary>Level origin snapped to 2 x spacing (same expression as the shader).</summary>
        public static Vector2 Origin(Vector2 cam, int k)
        {
            float s = Spacing(k);
            return new Vector2(Mathf.Floor(cam.x * (0.5f / s) + 0.5f) * (2f * s), Mathf.Floor(cam.y * (0.5f / s) + 0.5f) * (2f * s));
        }

        /// <summary>World xz of grid corner (gx, gz) of level k before the morph.</summary>
        public static Vector2 GridPoint(Vector2 cam, int k, float gx, float gz)
        {
            float s = Spacing(k);
            Vector2 o = Origin(cam, k);
            return new Vector2(o.x + (gx - TerrainConfig.ClipHalf) * s, o.y + (gz - TerrainConfig.ClipHalf) * s);
        }

        /// <summary>True when quad (qx, qz) of level k lies inside the footprint of level k - 1 (not rasterised).</summary>
        public static bool QuadCollapsed(Vector2 cam, int k, int qx, int qz)
        {
            if (k <= 0) return false;
            float s = Spacing(k);
            Vector2 c = GridPoint(cam, k, qx + 0.5f, qz + 0.5f);
            Vector2 of = Origin(cam, k - 1);
            return Mathf.Max(Mathf.Abs(c.x - of.x), Mathf.Abs(c.y - of.y)) < 32f * s;
        }

        /// <summary>Geomorph weight of a level-k vertex at p (Chebyshev distance to the true camera).</summary>
        public static float MorphAlpha(Vector2 cam, Vector2 p, int k)
        {
            if (k >= Levels - 1) return 0f;
            float s = Spacing(k);
            float d = Mathf.Max(Mathf.Abs(p.x - cam.x), Mathf.Abs(p.y - cam.y));
            return Mathf.Clamp01((d - TerrainConfig.MorphStart * s) / (TerrainConfig.MorphWidth * s));
        }

        /// <summary>Final flat xz of grid corner (gx, gz) of level k: morphed (odd indices slide onto the lower even
        /// neighbour) and collapsed onto the wall face in the plain (set &gt;= 1, 14.5.5).</summary>
        public static Vector2 VertexXZ(Vector2 cam, int k, int gx, int gz, int set, out float alpha)
        {
            float s = Spacing(k);
            Vector2 p = GridPoint(cam, k, gx, gz);
            alpha = MorphAlpha(cam, p, k);
            p.x -= (gx & 1) * s * alpha;
            p.y -= (gz & 1) * s * alpha;
            if (set >= 1) p.x = Mathf.Min(p.x, RingConstants.WallX);
            return p;
        }

        /// <summary>Morphed quad size of a level-k vertex: the level spacing, growing to the coarse spacing over the morph
        /// band (the e of TerrainVertex; equal on both sides of a level boundary).</summary>
        public static float MorphedSpacing(int k, float alpha) => Spacing(k) * (1f + alpha);

        /// <summary>Distance to the path segments that carry a strip mesh (twin of RingStripDist).</summary>
        public static float StripDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            var segs = RingWorld.PathSegments;
            var idx = TerrainConfig.StripSegments;
            for (int i = 0; i < idx.Length; i++)
                if (idx[i] < segs.Length) d = Mathf.Min(d, RingWorld.SegDist(p, segs[idx[i]]));
            return d;
        }

        /// <summary>Weight of the path ceiling at morphed spacing e (twin of RingCeilWeight).</summary>
        public static float CeilWeight(float e) =>
            Mathf.Clamp01((e - TerrainConfig.PathCeilE0) / (TerrainConfig.PathCeilE1 - TerrainConfig.PathCeilE0));

        /// <summary>Half-width of the zero band of the path ceiling at morphed spacing e (twin of RingCeilBand).</summary>
        public static float CeilBand(float e) => TerrainConfig.PathCeilBand0 + TerrainConfig.PathCeilBandPerE * e;

        /// <summary>Height h limited by the path ceiling at strip distance stripDist and morphed spacing e (twin of
        /// RingPathCeil): continuous in e, 0 within the band, rising PathCeilSlope : 1 beyond it.</summary>
        public static float PathCeil(float h, float stripDist, float e)
        {
            float cw = CeilWeight(e);
            if (cw <= 0f) return h;
            float c = TerrainConfig.PathCeilSlope * Mathf.Max(stripDist - CeilBand(e), 0f);
            return h + (Mathf.Min(h, c) - h) * cw;
        }

        /// <summary>Upper bound of the change of a ceiling-limited height per metre of camera travel at a fixed vertex
        /// (e moves by at most 1 / MorphWidth per metre): 0.48 m per m, i.e. 4.8 mm per cm.</summary>
        public static float CeilMaxRate => TerrainConfig.PathCeilSlope * TerrainConfig.PathCeilBandPerE / TerrainConfig.MorphWidth;

        /// <summary>Height of grid corner (gx, gz) of level k for camera cam (twin of TerrainVertex without the skirt drop):
        /// RingWorld.Height at the morphed, plain-clamped position, limited by the path ceiling.</summary>
        public static float VertexHeight(Vector2 cam, int k, int gx, int gz, int set)
        {
            Vector2 p = VertexXZ(cam, k, gx, gz, set, out float alpha);
            float h = RingWorld.Height(p.x, p.y, set);
            return PathCeil(h, StripDist(p.x, p.y), MorphedSpacing(k, alpha));
        }

        /// <summary>Quads of levels 1..8 that are inside the finer level for every camera are not in the mesh.
        /// finest = the finest level of the stack (0 for the terrain; the beat-7 hull reuses levels 2..8 with finest 2).</summary>
        public static bool InMesh(int k, int qx, int qz, int finest = 0)
        {
            if (k <= finest) return true;
            bool inX = qx >= TerrainConfig.AlwaysCollapsedLo && qx < TerrainConfig.AlwaysCollapsedHi;
            bool inZ = qz >= TerrainConfig.AlwaysCollapsedLo && qz < TerrainConfig.AlwaysCollapsedHi;
            return !(inX && inZ);
        }

        /// <summary>Quads of level k present in the mesh.</summary>
        public static int MeshQuads(int k, int finest = 0)
        {
            int n = Quads, inner = TerrainConfig.AlwaysCollapsedHi - TerrainConfig.AlwaysCollapsedLo;
            return k <= finest ? n * n : n * n - inner * inner;
        }

        /// <summary>Pre-bend world bounds of the clipmap for every camera of the walk (the mesh itself is in grid units).</summary>
        public static Bounds PreBendBounds()
        {
            float m = TerrainConfig.ClipBendDistance;
            Vector2 a = TerrainConfig.CamMin - new Vector2(m, m), b = TerrainConfig.CamMax + new Vector2(m, m);
            var min = new Vector3(a.x, -60f, a.y);
            var max = new Vector3(b.x, 60f, b.y);
            var bb = new Bounds();
            bb.SetMinMax(min, max);
            return bb;
        }

        /// <summary>Appends level k (per-quad vertices plus inward-facing skirts along its outer edge) to mb.
        /// uv1 = (spacing, level index relative to finest); the shader collapses against the next finer level whenever
        /// that index is above 0.</summary>
        public static void AppendLevel(MeshBuilder mb, int k, int finest = 0)
        {
            int n = Quads;
            float s = Spacing(k);
            var uv1 = new Vector2(s, k - finest);
            for (int qz = 0; qz < n; qz++)
                for (int qx = 0; qx < n; qx++)
                {
                    if (!InMesh(k, qx, qz, finest)) continue;
                    var qc = new Vector2(qx + 0.5f, qz + 0.5f);
                    // clockwise seen from above (+Y): (x0, z0) -> (x0, z1) -> (x1, z1) -> (x1, z0); diagonal (x0, z0)-(x1, z1)
                    // in every level, so a fully morphed fine quad equals the coarse quad exactly
                    int a = Corner(mb, qx, qz, 0f, qc, uv1, Vector3.up);
                    int b = Corner(mb, qx, qz + 1, 0f, qc, uv1, Vector3.up);
                    int c = Corner(mb, qx + 1, qz + 1, 0f, qc, uv1, Vector3.up);
                    int d = Corner(mb, qx + 1, qz, 0f, qc, uv1, Vector3.up);
                    mb.Quad(a, b, c, d);
                }
            // skirts: one quad per boundary edge segment, hanging 2 m (shader) below the edge, facing inward
            for (int i = 0; i < n; i++)
            {
                Skirt(mb, i, 0, i + 1, 0, new Vector2(i + 0.5f, 0.5f), uv1, Vector3.forward);            // z = min edge
                Skirt(mb, i, n, i + 1, n, new Vector2(i + 0.5f, n - 0.5f), uv1, Vector3.back);           // z = max edge
                Skirt(mb, 0, i, 0, i + 1, new Vector2(0.5f, i + 0.5f), uv1, Vector3.right);              // x = min edge
                Skirt(mb, n, i, n, i + 1, new Vector2(n - 0.5f, i + 0.5f), uv1, Vector3.left);           // x = max edge
            }
        }

        static int Corner(MeshBuilder mb, int gx, int gz, float y, Vector2 quadCentre, Vector2 uv1, Vector3 n)
        {
            int i = mb.Vtx(new Vector3(gx, y, gz), n, quadCentre);
            mb.UV1[i] = uv1;
            return i;
        }

        static void Skirt(MeshBuilder mb, int gx0, int gz0, int gx1, int gz1, Vector2 quadCentre, Vector2 uv1, Vector3 inward)
        {
            int t0 = Corner(mb, gx0, gz0, 0f, quadCentre, uv1, inward);
            int t1 = Corner(mb, gx1, gz1, 0f, quadCentre, uv1, inward);
            int b1 = Corner(mb, gx1, gz1, -1f, quadCentre, uv1, inward);
            int b0 = Corner(mb, gx0, gz0, -1f, quadCentre, uv1, inward);
            Vector3 p0 = mb.V[t0], p1 = mb.V[t1], p2 = mb.V[b1];
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), inward) > 0f) mb.Quad(t0, t1, b1, b0);
            else mb.Quad(t0, b0, b1, t1);
        }
    }
}
