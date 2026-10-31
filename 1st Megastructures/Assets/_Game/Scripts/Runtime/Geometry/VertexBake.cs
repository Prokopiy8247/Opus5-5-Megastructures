using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Generation-time vertex bakes (14.7.5): ambient occlusion into vertex colour R and edge wear into G, used by
    /// M_SteelPainted and M_ConcreteBoard. Both run on a <see cref="MeshBuilder"/> before <see cref="MeshBuilder.ToMesh"/>,
    /// in the builder's (post-Matrix) space. Load-time only; deterministic (PCG hashes of the vertex index and the seed,
    /// no UnityEngine.Random, no sin hashes); no per-vertex allocation (static scratch, main thread only).
    /// </summary>
    public static class VertexBake
    {
        static readonly List<int> s_near = new List<int>(64);
        static readonly Dictionary<long, int> s_cellHead = new Dictionary<long, int>(4096);
        static int[] s_next = new int[0];

        /// <summary>
        /// Hemisphere-sampled ambient occlusion against axis-aligned boxes (the bounds of nearby solids: walls, floors, beams).
        /// For each vertex (from <paramref name="firstVertex"/>), <paramref name="samples"/> cosine-weighted, stratified rays start
        /// just above the surface along its normal; a box hit at distance t &lt; maxDistance occludes with weight 1 - (t / maxDistance)^2.
        /// A ray origin strictly inside a box (deeper than the bake epsilon) counts as fully occluded; an origin on a box face (a
        /// floor vertex at the foot of a wall, a beam end against a plate) is occluded only by the rays that actually enter the
        /// box, so inside corners get the expected half occlusion instead of black seams. R is multiplied by the visibility
        /// 1 - strength * mean weight, so bakes compose and the default R = 255 means unoccluded. Boxes are pre-filtered per
        /// vertex by distance.
        /// </summary>
        /// <param name="occluders">Boxes in builder space. A solid's own bounds may be included: rays leave its surface outward.</param>
        /// <param name="maxDistance">Occlusion radius in metres (e.g. 0.5-3 m for fixtures, 10-20 m for halls).</param>
        /// <param name="samples">Rays per vertex (16-64).</param>
        /// <param name="seed">Deterministic seed (GameState.Seed mixed with a per-mesh constant).</param>
        public static void BakeAO(MeshBuilder mb, IList<Bounds> occluders, float maxDistance, int samples, uint seed,
            float strength = 1f, int firstVertex = 0)
        {
            if (mb == null || occluders == null || occluders.Count == 0 || samples <= 0 || maxDistance <= 0f) return;
            List<Vector3> P = mb.Positions, N = mb.Normals;
            List<Color32> C = mb.Colors;
            float maxD2 = maxDistance * maxDistance;
            float eps = Mathf.Max(1e-4f, maxDistance * 1e-4f);
            float inv = 1f / samples;
            uint seedHash = Pcg(seed ^ 0x9E3779B9u);
            int count = P.Count;
            for (int v = Mathf.Max(0, firstVertex); v < count; v++)
            {
                Vector3 p = P[v];
                s_near.Clear();
                for (int b = 0; b < occluders.Count; b++)
                    if (occluders[b].SqrDistance(p) <= maxD2) s_near.Add(b);
                if (s_near.Count == 0) continue;

                Vector3 n = N[v];
                Vector3 refAxis = Mathf.Abs(n.y) < 0.999f ? Vector3.up : Vector3.right;
                Vector3 t = Vector3.Cross(refAxis, n).normalized;
                Vector3 bt = Vector3.Cross(n, t);
                // a few float ULPs of the coordinates as well (shaft-scale positions reach thousands of metres)
                float epsV = Mathf.Max(eps, 1e-6f * (Mathf.Abs(p.x) + Mathf.Abs(p.y) + Mathf.Abs(p.z)));
                Vector3 o = p + n * epsV;
                float rot = (Pcg((uint)v * 0x85EBCA6Bu + seedHash) >> 8) * (1f / 16777216f);
                float occ = 0f;
                for (int s = 0; s < samples; s++)
                {
                    float u1 = (s + 0.5f) * inv;
                    float u2 = s * 0.6180339887f + rot;
                    u2 -= Mathf.Floor(u2);
                    float r = Mathf.Sqrt(u1);
                    float ph = 2f * Mathf.PI * u2;
                    Vector3 dir = t * (r * Mathf.Cos(ph)) + bt * (r * Mathf.Sin(ph)) + n * Mathf.Sqrt(Mathf.Max(0f, 1f - u1));
                    float best = float.PositiveInfinity;
                    for (int i = 0; i < s_near.Count; i++)
                    {
                        if (RayBox(o, dir, occluders[s_near[i]], maxDistance, epsV, out float hit) && hit < best) best = hit;
                    }
                    if (best <= maxDistance)
                    {
                        float q = best / maxDistance;
                        occ += 1f - q * q;
                    }
                }
                float vis = Mathf.Clamp01(1f - strength * occ * inv);
                Color32 col = C[v];
                col.r = (byte)Mathf.Clamp(Mathf.RoundToInt(col.r * vis), 0, 255);
                C[v] = col;
            }
        }

        /// <summary>
        /// Slab test of the ray o + t d, t in [0, tMax], against the box shrunk by eps on every side (by at most a quarter of its
        /// extent, so thin plates keep occluding). Consequences: an origin strictly inside the box (deeper than eps) hits at
        /// t = 0; an origin on (or within eps of) a face hits only if the ray enters the box, never when it points away; a hit
        /// requires a positive exit distance (t1 &gt; 0), so a ray that merely touches the box at its origin is a miss.
        /// </summary>
        static bool RayBox(Vector3 o, Vector3 d, Bounds b, float tMax, float eps, out float t)
        {
            Vector3 mn = b.min, mx = b.max;
            float t0 = 0f, t1 = tMax;
            for (int a = 0; a < 3; a++)
            {
                float lo = mn[a], hi = mx[a];
                float shrink = Mathf.Min(eps, 0.25f * (hi - lo));
                if (shrink > 0f) { lo += shrink; hi -= shrink; }
                float oa = o[a], da = d[a];
                if (Mathf.Abs(da) < 1e-12f)
                {
                    if (oa < lo || oa > hi) { t = 0f; return false; }
                    continue;
                }
                float ia = 1f / da;
                float ta = (lo - oa) * ia, tb = (hi - oa) * ia;
                if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
                if (ta > t0) t0 = ta;
                if (tb < t1) t1 = tb;
                if (t0 > t1 || t1 <= 0f) { t = 0f; return false; }
            }
            t = t0;
            return true;
        }

        /// <summary>
        /// Edge wear from creases: for every vertex, the largest angle between its normal and the normals of other vertices at the
        /// same position (within <paramref name="weldDistance"/>; split normals of hard edges) sets G = 255 * smoothstep(start, full,
        /// angle), never lowering an existing G. Smooth vertices (no split partner) keep their G.
        /// </summary>
        public static void EdgeWearFromNormals(MeshBuilder mb, float angleStartDeg = 20f, float angleFullDeg = 50f,
            float weldDistance = 1e-4f, int firstVertex = 0)
        {
            if (mb == null) return;
            List<Vector3> P = mb.Positions, N = mb.Normals;
            List<Color32> C = mb.Colors;
            int count = P.Count;
            int start = Mathf.Max(0, firstVertex);
            if (count - start < 2) return;
            float w = Mathf.Max(weldDistance, 1e-7f);
            float invW = 1f / w;
            float w2 = w * w;
            if (s_next.Length < count) s_next = new int[Mathf.Max(count, s_next.Length * 2)];
            s_cellHead.Clear();
            for (int v = start; v < count; v++)
            {
                long key = CellKey(P[v], invW, 0, 0, 0);
                s_next[v] = s_cellHead.TryGetValue(key, out int head) ? head : -1;
                s_cellHead[key] = v;
            }
            float span = Mathf.Max(1e-3f, angleFullDeg - angleStartDeg);
            for (int v = start; v < count; v++)
            {
                Vector3 p = P[v], n = N[v];
                float minDot = 1f;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!s_cellHead.TryGetValue(CellKey(p, invW, dx, dy, dz), out int o)) continue;
                    for (; o >= 0; o = s_next[o])
                    {
                        if (o == v || (P[o] - p).sqrMagnitude > w2) continue;
                        float d = Vector3.Dot(n, N[o]);
                        if (d < minDot) minDot = d;
                    }
                }
                if (minDot >= 1f) continue;
                float angle = Mathf.Acos(Mathf.Clamp(minDot, -1f, 1f)) * Mathf.Rad2Deg;
                float x = Mathf.Clamp01((angle - angleStartDeg) / span);
                byte g = (byte)Mathf.RoundToInt(255f * x * x * (3f - 2f * x));
                Color32 col = C[v];
                if (g > col.g) { col.g = g; C[v] = col; }
            }
        }

        static long CellKey(Vector3 p, float invW, int dx, int dy, int dz)
        {
            long x = (long)Mathf.Floor(p.x * invW) + dx, y = (long)Mathf.Floor(p.y * invW) + dy, z = (long)Mathf.Floor(p.z * invW) + dz;
            return ((x & 0x1FFFFF) << 42) | ((y & 0x1FFFFF) << 21) | (z & 0x1FFFFF);
        }

        /// <summary>PCG hash (O'Neill; Jarzynski and Olano 2020).</summary>
        public static uint Pcg(uint v)
        {
            uint state = v * 747796405u + 2891336453u;
            uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
            return (word >> 22) ^ word;
        }
    }
}
