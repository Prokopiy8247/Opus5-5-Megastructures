using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Build-time geometry helpers of the beat-1 gallery (14.5.1) that MeshKit does not provide: corner fillets for the
    /// floor-following runs (pipe, tray, cables, trunk, handrail), a parallel-transport tube with ThinLine vertex data,
    /// perpendicular loft frames, a floor top with a drain hole, the Glow quad and the BeamWedge doorway prism as validated
    /// MeshBuilder meshes. Main thread only, static scratch lists, no per-vertex allocation. Nothing here runs per frame.
    /// </summary>
    internal static class Beat01Geo
    {
        static readonly List<Vector3> s_tan = new List<Vector3>(256);

        /// <summary>Unit vector perpendicular to dir, as close to hint as possible (stable fallback when parallel).</summary>
        public static Vector3 Perp(Vector3 dir, Vector3 hint)
        {
            Vector3 u = hint - dir * Vector3.Dot(hint, dir);
            if (u.sqrMagnitude < 1e-8f)
            {
                Vector3 alt = Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.forward;
                u = alt - dir * Vector3.Dot(alt, dir);
            }
            return u.normalized;
        }

        /// <summary>
        /// Polyline with every interior corner replaced by a quadratic Bezier arc (tangent length r tan(phi / 2), capped at
        /// 45 % of the adjacent segments), sampled with <paramref name="segments"/> steps. Straight runs keep their two end points.
        /// </summary>
        public static void Fillet(IList<Vector3> pts, float radius, int segments, List<Vector3> result)
        {
            result.Clear();
            int n = pts.Count;
            if (n == 0) return;
            result.Add(pts[0]);
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 p = pts[i];
                Vector3 a = p - pts[i - 1], b = pts[i + 1] - p;
                float la = a.magnitude, lb = b.magnitude;
                if (la < 1e-6f || lb < 1e-6f) continue;
                a /= la; b /= lb;
                float phi = Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f));
                if (phi < 1e-3f) { result.Add(p); continue; }
                float t = Mathf.Min(radius * Mathf.Tan(0.5f * phi), Mathf.Min(0.45f * la, 0.45f * lb));
                Vector3 s = p - a * t, e = p + b * t;
                for (int k = 0; k <= segments; k++)
                {
                    float u = (float)k / segments;
                    float w0 = (1f - u) * (1f - u), w1 = 2f * u * (1f - u), w2 = u * u;
                    result.Add(s * w0 + p * w1 + e * w2);
                }
            }
            if (n > 1) result.Add(pts[n - 1]);
        }

        /// <summary>Unit tangents of a polyline by central differences (one-sided at the ends).</summary>
        public static void Tangents(IList<Vector3> path, List<Vector3> tangents)
        {
            tangents.Clear();
            int n = path.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)];
                tangents.Add(d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward);
            }
        }

        /// <summary>
        /// Loft frames perpendicular to the path (section x = right, section y = the up hint made perpendicular to the tangent),
        /// so a tray or a trunk tilts with the stair slope like the real member would.
        /// </summary>
        public static void PerpFrames(IList<Vector3> path, Vector3 up, List<LoftFrame> frames)
        {
            Tangents(path, s_tan);
            frames.Clear();
            for (int k = 0; k < path.Count; k++)
                frames.Add(new LoftFrame(path[k], Quaternion.LookRotation(s_tan[k], Perp(s_tan[k], up))));
        }

        /// <summary>
        /// Round tube along a polyline with parallel-transport frames (no twist), smooth normals, UV0 = (arc length around,
        /// length along). With <paramref name="thinLine"/> every ring writes UV1 = (local tangent, radius) and UV2 = (ring centre,
        /// 1) for the ThinLine pass (14.7.9). The builder matrix must be identity (UV1 / UV2 are stored as given). No caps.
        /// </summary>
        public static void Tube(MeshBuilder mb, IList<Vector3> path, float radius, int segments, bool thinLine, Vector3 upHint)
        {
            int n = path.Count;
            if (n < 2 || radius <= 0f) return;
            int seg = Mathf.Max(3, segments);
            Tangents(path, s_tan);
            MeshKit.Reserve(mb, n * (seg + 1), (n - 1) * seg * 6);
            bool w1 = mb.WriteUV1, w2 = mb.WriteUV2;
            Vector4 c1 = mb.CurrentUV1, c2 = mb.CurrentUV2;
            if (thinLine) { mb.WriteUV1 = true; mb.WriteUV2 = true; }
            int first = mb.VertexCount;
            Vector3 nrm = Perp(s_tan[0], upHint);
            float along = 0f;
            for (int k = 0; k < n; k++)
            {
                Vector3 t = s_tan[k];
                if (k > 0)
                {
                    nrm -= t * Vector3.Dot(nrm, t);
                    nrm = nrm.sqrMagnitude > 1e-12f ? nrm.normalized : Perp(t, upHint);
                    along += (path[k] - path[k - 1]).magnitude;
                }
                Vector3 bin = Vector3.Cross(t, nrm);
                Vector3 p = path[k];
                if (thinLine)
                {
                    mb.CurrentUV1 = new Vector4(t.x, t.y, t.z, radius);
                    mb.CurrentUV2 = new Vector4(p.x, p.y, p.z, 1f);
                }
                for (int j = 0; j <= seg; j++)
                {
                    float a = 2f * Mathf.PI * j / seg;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    mb.Vertex(p + dir * radius, dir, new Vector2(radius * a, along));
                }
            }
            int row = seg + 1;
            for (int k = 0; k < n - 1; k++)
            for (int j = 0; j < seg; j++)
            {
                int a = first + k * row + j;
                BeatKit.Tri(mb, a, a + 1, a + row + 1);
                BeatKit.Tri(mb, a, a + row + 1, a + row);
            }
            mb.WriteUV1 = w1; mb.WriteUV2 = w2; mb.CurrentUV1 = c1; mb.CurrentUV2 = c2;
        }

        /// <summary>Flat quad with its own vertices, normal n and explicit UV0 (corners in cyclic order, either orientation).</summary>
        public static void QuadUV(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i0 = mb.Vertex(a, n, ua), i1 = mb.Vertex(b, n, ub), i2 = mb.Vertex(c, n, uc), i3 = mb.Vertex(d, n, ud);
            BeatKit.Tri(mb, i0, i1, i2);
            BeatKit.Tri(mb, i0, i2, i3);
        }

        /// <summary>Upward floor quad over [x0, x1] x [z0, z1] at height y, UV0 = (x - centreX, z) (worn-path convention).</summary>
        public static void FloorQuad(MeshBuilder mb, float x0, float x1, float z0, float z1, float y, float centreX = 0f)
        {
            if (x1 - x0 < 1e-5f || z1 - z0 < 1e-5f) return;
            QuadUV(mb, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up,
                new Vector2(x0 - centreX, z0), new Vector2(x0 - centreX, z1), new Vector2(x1 - centreX, z1), new Vector2(x1 - centreX, z0));
        }

        /// <summary>
        /// Floor top over [x0, x1] x [z0, z1] with a rectangular hole [hx0, hx1] x [hz0, hz1]: a 3 x 3 grid of quads without its
        /// centre cell, so every shared edge has identical end points (no T-junction cracks).
        /// </summary>
        public static void FloorTopWithHole(MeshBuilder mb, float x0, float x1, float z0, float z1, float y,
            float hx0, float hx1, float hz0, float hz1)
        {
            float[] xs = { x0, hx0, hx1, x1 };
            float[] zs = { z0, hz0, hz1, z1 };
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                if (i == 1 && j == 1) continue;
                FloorQuad(mb, xs[i], xs[i + 1], zs[j], zs[j + 1], y);
            }
        }

        /// <summary>Vertical quad on the plane z = const facing +Z or -Z over [x0, x1] x [y0, y1] (UV0 = x, y).</summary>
        public static void ZQuad(MeshBuilder mb, float z, bool facePlusZ, float x0, float x1, float y0, float y1)
        {
            if (x1 - x0 < 1e-5f || y1 - y0 < 1e-5f) return;
            Vector3 n = facePlusZ ? Vector3.forward : Vector3.back;
            QuadUV(mb, new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z), n,
                new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0));
        }

        /// <summary>Vertical quad on the plane x = const facing +X or -X over [z0, z1] x [y0, y1] (UV0 = z, y).</summary>
        public static void XQuad(MeshBuilder mb, float x, bool facePlusX, float z0, float z1, float y0, float y1)
        {
            if (z1 - z0 < 1e-5f || y1 - y0 < 1e-5f) return;
            Vector3 n = facePlusX ? Vector3.right : Vector3.left;
            QuadUV(mb, new Vector3(x, y0, z0), new Vector3(x, y1, z0), new Vector3(x, y1, z1), new Vector3(x, y0, z1), n,
                new Vector2(z0, y0), new Vector2(z0, y1), new Vector2(z1, y1), new Vector2(z1, y0));
        }

        /// <summary>
        /// The Megastructure/Glow quad (vertices at +/-0.5 in the object XY plane) as a validated MeshBuilder mesh with bounds
        /// inflated to +/-8 (the shader grows the sprite to its minimum pixel size and doubles the quad for the falloff).
        /// </summary>
        public static Mesh GlowQuad(MeshBuilder mb, string name, List<Mesh> meshes)
        {
            mb.Clear();
            QuadUV(mb, new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                Vector3.back, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            Mesh m = BeatKit.Bake(mb, name, meshes);
            m.bounds = new Bounds(Vector3.zero, new Vector3(16f, 16f, 16f));
            return m;
        }

        /// <summary>
        /// One rectangular BeamWedge prism (mode 2, 14.7.4 / SHADERS 11) as a validated mesh: 8 vertices with UV1 = (0, corner
        /// 0..3, cap 0/1, 0) and the 12 triangles of a closed box (the shader rebuilds the positions from _BeamRect, _BeamDir and
        /// _BeamParams.w and is winding-independent). Preview positions follow the same rectangle and direction; the bounds
        /// cover the prism with a margin.
        /// </summary>
        public static Mesh BeamPrism(MeshBuilder mb, string name, List<Mesh> meshes, Vector2 halfSize, Vector3 dirObject, float length)
        {
            mb.Clear();
            Vector3 dir = dirObject.normalized;
            var centroid = new Vector3(0f, 0f, 0f) + dir * (0.5f * length);
            mb.WriteUV1 = true;
            int first = mb.VertexCount;
            for (int cap = 0; cap < 2; cap++)
            for (int c = 0; c < 4; c++)
            {
                float x = (c == 1 || c == 2) ? halfSize.x : -halfSize.x;
                float y = c >= 2 ? halfSize.y : -halfSize.y;
                Vector3 p = new Vector3(x, y, 0f) + dir * (cap * length);
                mb.CurrentUV1 = new Vector4(0f, c, cap, 0f);
                mb.Vertex(p, (p - centroid).normalized, new Vector2(x, y));
            }
            mb.WriteUV1 = false;
            int[] box =
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0,
            };
            for (int i = 0; i < box.Length; i += 3) BeatKit.Tri(mb, first + box[i], first + box[i + 1], first + box[i + 2]);
            Mesh m = BeatKit.Bake(mb, name, meshes);
            Vector3 end = dir * length;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            b.Encapsulate(end);
            b.Expand(new Vector3(2f * halfSize.x + 2f, 2f * halfSize.y + 2f, 2f));
            m.bounds = b;
            return m;
        }

        /// <summary>Bounds of builder vertices first..end (builder space), or an empty bounds at the origin.</summary>
        public static Bounds Range(MeshBuilder mb, int first) => BeatKit.VertexBounds(mb, first);

        /// <summary>Union helper that ignores empty bounds.</summary>
        public static void Encapsulate(ref Bounds acc, ref bool any, Bounds b)
        {
            if (b.size == Vector3.zero && b.center == Vector3.zero) return;
            if (!any) { acc = b; any = true; } else acc.Encapsulate(b);
        }

        /// <summary>PCG hash to [0, 1) (deterministic choices: paint wear gaps, bolt spin).</summary>
        public static float Hash01(uint v)
        {
            uint s = v * 747796405u + 2891336453u;
            uint w = ((s >> (int)((s >> 28) + 4u)) ^ s) * 277803737u;
            w = (w >> 22) ^ w;
            return (w >> 8) * (1f / 16777216f);
        }
    }
}
