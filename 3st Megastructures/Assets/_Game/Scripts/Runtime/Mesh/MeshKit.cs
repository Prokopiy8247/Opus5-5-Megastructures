// MeshKit: procedural mesh generators shared by all beat builders (MASTER_PROMPT 7.3, 8.7).
// Conventions (Unity is left-handed, Y up):
//  * Front faces are CLOCKWISE as seen from the outside; MeshBuilder.Quad(a, b, c, d) takes the four
//    corners clockwise as seen from the front side (e.g. bottom-left, top-left, top-right, bottom-right).
//  * Face normal of a front triangle (a, b, c) = cross(b - a, c - a).
//  * Vertex colour: R = baked AO (0.55..1), G = height shade (1 = none, 0 = 3 percent darker), B free, A alpha.
//  * UV0 = metres (world-ish), UV1 (Vector4) and tangent.xyz are free channels a shader may use
//    (the cable stores its smooth normal in tangent.xyz and the ridge relief in uv1.x).
//  * Meshes above 65 535 vertices automatically use 32-bit indices; bounds are recalculated and can be
//    padded for vertex-displacing shaders.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> Positions;
        public readonly List<Vector3> Normals;
        public readonly List<Vector4> Tangents;
        public readonly List<Color> Colors;
        public readonly List<Vector2> UV0;
        public readonly List<Vector4> UV1;
        public readonly List<int> Indices;

        public MeshBuilder(int capacity = 1024)
        {
            Positions = new List<Vector3>(capacity);
            Normals = new List<Vector3>(capacity);
            Tangents = new List<Vector4>(capacity);
            Colors = new List<Color>(capacity);
            UV0 = new List<Vector2>(capacity);
            UV1 = new List<Vector4>(capacity);
            Indices = new List<int>(capacity * 3);
        }

        public int VertexCount => Positions.Count;
        public int TriangleCount => Indices.Count / 3;

        public int Add(Vector3 p, Vector3 n, Vector2 uv, Color c) => Add(p, n, uv, c, Vector4.zero, Vector4.zero);

        public int Add(Vector3 p, Vector3 n, Vector2 uv, Color c, Vector4 tangent, Vector4 uv1)
        {
            Positions.Add(p); Normals.Add(n); UV0.Add(uv); Colors.Add(c); Tangents.Add(tangent); UV1.Add(uv1);
            return Positions.Count - 1;
        }

        public void Tri(int a, int b, int c) { Indices.Add(a); Indices.Add(b); Indices.Add(c); }

        /// <summary>a, b, c, d clockwise as seen from the front side.</summary>
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        public void Clear()
        {
            Positions.Clear(); Normals.Clear(); Tangents.Clear(); Colors.Clear(); UV0.Clear(); UV1.Clear(); Indices.Clear();
        }

        /// <summary>Appends another builder transformed by m (normals by the inverse transpose).</summary>
        public void Append(MeshBuilder src, Matrix4x4 m)
        {
            int baseIndex = Positions.Count;
            Matrix4x4 nm = m.inverse.transpose;
            for (int i = 0; i < src.Positions.Count; i++)
            {
                Positions.Add(m.MultiplyPoint3x4(src.Positions[i]));
                Normals.Add(nm.MultiplyVector(src.Normals[i]).normalized);
                Vector4 t = src.Tangents[i];
                Vector3 t3 = nm.MultiplyVector(new Vector3(t.x, t.y, t.z));
                Tangents.Add(new Vector4(t3.x, t3.y, t3.z, t.w));
                Colors.Add(src.Colors[i]); UV0.Add(src.UV0[i]); UV1.Add(src.UV1[i]);
            }
            for (int i = 0; i < src.Indices.Count; i++) Indices.Add(src.Indices[i] + baseIndex);
        }

        public Mesh ToMesh(string name, float boundsPadding = 0f, bool uploadAndRelease = true)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = Positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(Positions);
            mesh.SetNormals(Normals);
            mesh.SetTangents(Tangents);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, UV0);
            mesh.SetUVs(1, UV1);
            mesh.SetIndices(Indices, MeshTopology.Triangles, 0, true);
            mesh.RecalculateBounds();
            if (boundsPadding > 0f)
            {
                var b = mesh.bounds;
                b.Expand(boundsPadding * 2f);
                mesh.bounds = b;
            }
            if (uploadAndRelease) mesh.UploadMeshData(false);
            return mesh;
        }
    }

    /// <summary>A sweep frame: position, right (lateral) and up axes of the cross-section, u = arc length.</summary>
    public struct SweepFrame
    {
        public Vector3 p, right, up;
        public float u;
        public SweepFrame(Vector3 p, Vector3 right, Vector3 up, float u) { this.p = p; this.right = right; this.up = up; this.u = u; }
    }

    /// <summary>One straight edge of a 2D sweep profile with the normals at its two ends (x = right, y = up).</summary>
    public struct ProfileEdge
    {
        public Vector2 a, b, na, nb;
        public float va, vb;   // v texture coordinate (metres along the profile)
    }

    public static class MeshKit
    {
        public static readonly Color White = new Color(1, 1, 1, 1);

        public static long TriangleCount(Mesh m)
        {
            long t = 0;
            for (int i = 0; i < m.subMeshCount; i++) t += (long)m.GetIndexCount(i) / 3;
            return t;
        }

        // ------------------------------------------------------------------ convex polygon helper
        /// <summary>Adds a planar convex polygon with a flat normal; points in any order (sorted around the normal).</summary>
        public static void ConvexPolygon(MeshBuilder mb, List<Vector3> pts, Vector3 normal, Color c)
        {
            int n = pts.Count;
            if (n < 3) return;
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < n; i++) centre += pts[i];
            centre /= n;
            Vector3 ax = (pts[0] - centre).normalized;
            Vector3 ay = Vector3.Cross(normal, ax);
            var order = new List<(float ang, Vector3 p)>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 d = pts[i] - centre;
                order.Add((Mathf.Atan2(Vector3.Dot(d, ay), Vector3.Dot(d, ax)), pts[i]));
            }
            order.Sort((x, y) => x.ang.CompareTo(y.ang));
            // dominant-axis planar UVs in metres
            Vector3 an = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            int start = mb.VertexCount;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = order[i].p;
                Vector2 uv = an.x >= an.y && an.x >= an.z ? new Vector2(p.z, p.y) : (an.y >= an.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
                mb.Add(p, normal, uv, c);
            }
            for (int i = 1; i < n - 1; i++)
            {
                int a = start, b = start + i, d = start + i + 1;
                Vector3 fn = Vector3.Cross(mb.Positions[b] - mb.Positions[a], mb.Positions[d] - mb.Positions[a]);
                if (fn.sqrMagnitude < 1e-12f) continue;   // collinear points: no zero-area triangle
                if (Vector3.Dot(fn, normal) >= 0) mb.Tri(a, b, d); else mb.Tri(a, d, b);
            }
        }

        // ------------------------------------------------------------------ chamfered box
        /// <summary>
        /// Box with chamfered edges (6 faces, 12 edge bevels, 8 corner triangles, all flat-shaded so edges catch light).
        /// size = full extents; chamfer clamped to below half of the smallest extent.
        /// </summary>
        public static void ChamferedBox(MeshBuilder mb, Vector3 centre, Vector3 size, float chamfer, Quaternion rotation, Color c)
        {
            Vector3 h = size * 0.5f;
            float ch = Mathf.Clamp(chamfer, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            var pts = new List<Vector3>(4);
            for (int sx = -1; sx <= 1; sx++)
            for (int sy = -1; sy <= 1; sy++)
            for (int sz = -1; sz <= 1; sz++)
            {
                int nz = (sx != 0 ? 1 : 0) + (sy != 0 ? 1 : 0) + (sz != 0 ? 1 : 0);
                if (nz == 0) continue;
                if (ch <= 0f && nz > 1) continue;
                pts.Clear();
                // corners matching the feature signs; free axes take both signs
                for (int cx = -1; cx <= 1; cx += 2)
                for (int cy = -1; cy <= 1; cy += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                {
                    if ((sx != 0 && cx != sx) || (sy != 0 && cy != sy) || (sz != 0 && cz != sz)) continue;
                    if (sx != 0) pts.Add(new Vector3(cx * h.x, cy * (h.y - ch), cz * (h.z - ch)));
                    if (sy != 0) pts.Add(new Vector3(cx * (h.x - ch), cy * h.y, cz * (h.z - ch)));
                    if (sz != 0) pts.Add(new Vector3(cx * (h.x - ch), cy * (h.y - ch), cz * h.z));
                }
                // dedupe (faces with chamfer 0 produce duplicates)
                for (int i = pts.Count - 1; i >= 0; i--)
                    for (int j = 0; j < i; j++)
                        if ((pts[i] - pts[j]).sqrMagnitude < 1e-12f) { pts.RemoveAt(i); break; }
                if (pts.Count < 3) continue;
                Vector3 nrm = rotation * new Vector3(sx, sy, sz).normalized;
                for (int i = 0; i < pts.Count; i++) pts[i] = centre + rotation * pts[i];
                ConvexPolygon(mb, pts, nrm, c);
            }
        }

        // ------------------------------------------------------------------ lathe
        public delegate Vector3 LatheDeform(Vector3 basePos, float angle, float y, int ring);
        public delegate Color LatheColor(float y, float angle, int ring);

        /// <summary>
        /// Surface of revolution around +Y. profile = (y, r) from bottom to top. Two consecutive identical
        /// points create a hard crease (the ring is duplicated with one-sided normals). Angle is measured from
        /// +Z toward +X; segments + 1 columns (the seam column is duplicated with identical normals).
        /// Normals are computed from the final (deformed) grid. scale scales the base radius in x and z.
        /// </summary>
        public static void Lathe(MeshBuilder mb, IList<Vector2> profile, int segments, Vector3 origin, Vector2 scaleXZ,
                                 LatheDeform deform = null, LatheColor color = null, bool capBottom = false, bool capTop = false,
                                 bool inward = false, float angleOffset = 0f)
        {
            int rings = profile.Count;
            int cols = segments + 1;
            var grid = new Vector3[rings, cols];
            for (int i = 0; i < rings; i++)
            {
                float y = profile[i].x, r = profile[i].y;
                for (int j = 0; j < cols; j++)
                {
                    float ang = angleOffset + (j % segments) * (Mathf.PI * 2f / segments);
                    var bp = new Vector3(Mathf.Sin(ang) * r * scaleXZ.x, y, Mathf.Cos(ang) * r * scaleXZ.y);
                    grid[i, j] = deform != null ? deform(bp, ang, y, i) : bp;
                }
            }
            // smooth runs separated by duplicated profile points
            var runStart = new int[rings];
            var runEnd = new int[rings];
            int s0 = 0;
            for (int i = 0; i < rings; i++)
            {
                bool breakAfter = i == rings - 1 || (profile[i] - profile[i + 1]).sqrMagnitude < 1e-10f;
                if (breakAfter)
                {
                    for (int k = s0; k <= i; k++) { runStart[k] = s0; runEnd[k] = i; }
                    s0 = i + 1;
                }
            }
            int baseIndex = mb.VertexCount;
            float vAcc = 0f;
            for (int i = 0; i < rings; i++)
            {
                if (i > 0 && runStart[i] == i) { /* new run: v continues */ }
                else if (i > 0) vAcc += (profile[i] - profile[i - 1]).magnitude;
                for (int j = 0; j < cols; j++)
                {
                    int jp = (j + 1) % segments, jm = (j - 1 + segments) % segments;
                    int jj = j % segments;
                    Vector3 dTheta = grid[i, jp] - grid[i, jm];
                    int ia = Mathf.Max(runStart[i], i - 1), ib = Mathf.Min(runEnd[i], i + 1);
                    Vector3 dT = grid[ib, jj] - grid[ia, jj];
                    Vector3 n = Vector3.Cross(dTheta, dT);
                    if (n.sqrMagnitude < 1e-16f)
                    {
                        // pole (r = 0): use the profile direction
                        Vector3 radial = new Vector3(grid[i, jj].x - origin.x, 0, grid[i, jj].z - origin.z);
                        n = (profile[runEnd[i]].x > profile[runStart[i]].x && i == runEnd[i]) ? Vector3.up : (i == runStart[i] ? Vector3.down : radial);
                    }
                    n.Normalize();
                    if (inward) n = -n;
                    float ang = angleOffset + jj * (Mathf.PI * 2f / segments);
                    Color c = color != null ? color(profile[i].x, ang, i) : White;
                    float u = (float)j / segments;
                    mb.Add(origin + grid[i, j], n, new Vector2(u * 2f * Mathf.PI * Mathf.Max(profile[i].y, 0.01f), vAcc), c);
                }
            }
            for (int i = 0; i < rings - 1; i++)
            {
                if (runEnd[i] == i) continue;   // crease: no strip between duplicated rings
                for (int j = 0; j < segments; j++)
                {
                    int a = baseIndex + i * cols + j, b = baseIndex + (i + 1) * cols + j;
                    int c2 = baseIndex + (i + 1) * cols + j + 1, d = baseIndex + i * cols + j + 1;
                    // outside view: (d, c, b, a) is clockwise
                    if (!inward) mb.Quad(d, c2, b, a); else mb.Quad(a, b, c2, d);
                }
            }
            if (capBottom) Cap(mb, grid, 0, segments, origin, Vector3.down, inward, color != null ? color(profile[0].x, 0, 0) : White);
            if (capTop) Cap(mb, grid, rings - 1, segments, origin, Vector3.up, inward, color != null ? color(profile[rings - 1].x, 0, rings - 1) : White);
        }

        static void Cap(MeshBuilder mb, Vector3[,] grid, int ring, int segments, Vector3 origin, Vector3 normal, bool inward, Color c)
        {
            if (inward) normal = -normal;
            Vector3 centre = Vector3.zero;
            for (int j = 0; j < segments; j++) centre += grid[ring, j];
            centre /= segments;
            int ci = mb.Add(origin + centre, normal, new Vector2(centre.x, centre.z), c);
            int start = mb.VertexCount;
            for (int j = 0; j < segments; j++) mb.Add(origin + grid[ring, j], normal, new Vector2(grid[ring, j].x, grid[ring, j].z), c);
            for (int j = 0; j < segments; j++)
            {
                int a = start + j, b = start + (j + 1) % segments;
                Vector3 fn = Vector3.Cross(mb.Positions[a] - mb.Positions[ci], mb.Positions[b] - mb.Positions[ci]);
                if (Vector3.Dot(fn, normal) >= 0) mb.Tri(ci, a, b); else mb.Tri(ci, b, a);
            }
        }

        // ------------------------------------------------------------------ sweep
        /// <summary>Builds profile edges from a CCW polygon (x = right, y = up); corners turning more than sharpDeg are hard.</summary>
        public static List<ProfileEdge> Profile(IList<Vector2> pts, bool closed, float sharpDeg = 30f)
        {
            int n = pts.Count;
            var edges = new List<ProfileEdge>();
            int ecount = closed ? n : n - 1;
            var fn = new Vector2[ecount];
            for (int e = 0; e < ecount; e++)
            {
                Vector2 d = pts[(e + 1) % n] - pts[e];
                fn[e] = new Vector2(d.y, -d.x).normalized;   // outward for CCW polygons
            }
            float v = 0f;
            float cosSharp = Mathf.Cos(sharpDeg * Mathf.Deg2Rad);
            for (int e = 0; e < ecount; e++)
            {
                int ePrev = closed ? (e - 1 + ecount) % ecount : e - 1;
                int eNext = closed ? (e + 1) % ecount : e + 1;
                Vector2 na = fn[e], nb = fn[e];
                if (ePrev >= 0 && ePrev < ecount && Vector2.Dot(fn[ePrev], fn[e]) > cosSharp) na = (fn[ePrev] + fn[e]).normalized;
                if (eNext >= 0 && eNext < ecount && Vector2.Dot(fn[eNext], fn[e]) > cosSharp) nb = (fn[eNext] + fn[e]).normalized;
                float len = (pts[(e + 1) % n] - pts[e]).magnitude;
                edges.Add(new ProfileEdge { a = pts[e], b = pts[(e + 1) % n], na = na, nb = nb, va = v, vb = v + len });
                v += len;
            }
            return edges;
        }

        /// <summary>Regular n-gon profile (CCW), radius r, all smooth.</summary>
        public static List<Vector2> CircleProfile(int n, float r, float phase = 0f)
        {
            var pts = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = phase + i * Mathf.PI * 2f / n;
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return pts;
        }

        /// <summary>Rectangle profile (CCW) of width w and height h centred on x, top at y = 0, with chamfer ch on all 4 corners.</summary>
        public static List<Vector2> ChamferedRectProfile(float w, float h, float ch, float topY = 0f)
        {
            float x0 = -w * 0.5f, x1 = w * 0.5f, y0 = topY - h, y1 = topY;
            return new List<Vector2>
            {
                new Vector2(x0 + ch, y0), new Vector2(x1 - ch, y0), new Vector2(x1, y0 + ch), new Vector2(x1, y1 - ch),
                new Vector2(x1 - ch, y1), new Vector2(x0 + ch, y1), new Vector2(x0, y1 - ch), new Vector2(x0, y0 + ch),
            };
        }

        /// <summary>
        /// Sweeps profile edges along frames (one continuous mesh, shared vertices at frame joints, so there are no
        /// seams between segments). Caps close the ends (convex profiles only).
        /// </summary>
        public static void Sweep(MeshBuilder mb, IList<SweepFrame> frames, IList<ProfileEdge> profile, Color c,
                                 bool capStart = false, bool capEnd = false, IList<Vector2> capPolygon = null)
        {
            int nf = frames.Count;
            for (int e = 0; e < profile.Count; e++)
            {
                var pe = profile[e];
                int start = mb.VertexCount;
                for (int k = 0; k < nf; k++)
                {
                    var f = frames[k];
                    Vector3 pa = f.p + f.right * pe.a.x + f.up * pe.a.y;
                    Vector3 pb = f.p + f.right * pe.b.x + f.up * pe.b.y;
                    Vector3 na = (f.right * pe.na.x + f.up * pe.na.y).normalized;
                    Vector3 nb = (f.right * pe.nb.x + f.up * pe.nb.y).normalized;
                    mb.Add(pa, na, new Vector2(f.u, pe.va), c, Vector4.zero, new Vector4(pe.a.x, pe.a.y, 0, 0));
                    mb.Add(pb, nb, new Vector2(f.u, pe.vb), c, Vector4.zero, new Vector4(pe.b.x, pe.b.y, 0, 0));
                }
                for (int k = 0; k < nf - 1; k++)
                {
                    int A0 = start + 2 * k, B0 = A0 + 1, A1 = start + 2 * (k + 1), B1 = A1 + 1;
                    mb.Quad(A0, B0, B1, A1);
                }
            }
            if (capPolygon != null && capPolygon.Count >= 3)
            {
                if (capStart) SweepCap(mb, frames[0], capPolygon, c, false);
                if (capEnd) SweepCap(mb, frames[nf - 1], capPolygon, c, true);
            }
        }

        static void SweepCap(MeshBuilder mb, SweepFrame f, IList<Vector2> poly, Color c, bool end)
        {
            Vector3 fwd = Vector3.Cross(f.right, f.up).normalized;
            Vector3 n = end ? fwd : -fwd;
            int start = mb.VertexCount;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 p = f.p + f.right * poly[i].x + f.up * poly[i].y;
                mb.Add(p, n, poly[i], c, Vector4.zero, new Vector4(poly[i].x, poly[i].y, 0, 0));
            }
            for (int i = 1; i < poly.Count - 1; i++)
            {
                // skip zero-area fan triangles (collinear profile points, e.g. a chamfer in line with a side)
                Vector3 pa = mb.Positions[start], pb = mb.Positions[start + i], pc = mb.Positions[start + i + 1];
                if (Vector3.Cross(pb - pa, pc - pa).sqrMagnitude < 1e-12f) continue;
                if (end) mb.Tri(start, start + i, start + i + 1); else mb.Tri(start, start + i + 1, start + i);
            }
        }

        /// <summary>Rotation-minimising frames along a polyline (double reflection, Wang et al. 2008).</summary>
        public static List<SweepFrame> ParallelTransportFrames(IList<Vector3> pts, Vector3 initialUp)
        {
            int n = pts.Count;
            var frames = new List<SweepFrame>(n);
            var tangents = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(n - 1, i + 1)];
                tangents[i] = (b - a).normalized;
            }
            Vector3 r0 = Vector3.Cross(initialUp, tangents[0]).normalized;
            if (r0.sqrMagnitude < 1e-8f) r0 = Vector3.Cross(Vector3.forward, tangents[0]).normalized;
            Vector3 right = r0;
            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    Vector3 v1 = pts[i] - pts[i - 1];
                    float c1 = Vector3.Dot(v1, v1);
                    if (c1 > 1e-12f)
                    {
                        Vector3 rL = right - (2f / c1) * Vector3.Dot(v1, right) * v1;
                        Vector3 tL = tangents[i - 1] - (2f / c1) * Vector3.Dot(v1, tangents[i - 1]) * v1;
                        Vector3 v2 = tangents[i] - tL;
                        float c2 = Vector3.Dot(v2, v2);
                        right = c2 > 1e-12f ? rL - (2f / c2) * Vector3.Dot(v2, rL) * v2 : rL;
                    }
                    u += v1.magnitude;
                }
                Vector3 up = Vector3.Cross(tangents[i], right).normalized;
                frames.Add(new SweepFrame(pts[i], right.normalized, up, u));
            }
            return frames;
        }

        // ------------------------------------------------------------------ torus
        /// <summary>Torus around +Y (ring in the XZ plane) centred at centre.</summary>
        public static void Torus(MeshBuilder mb, Vector3 centre, float majorR, float minorR, int segMajor, int segMinor, Color c)
        {
            int start = mb.VertexCount;
            for (int i = 0; i <= segMajor; i++)
            {
                float a = i * Mathf.PI * 2f / segMajor;
                Vector3 radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                for (int j = 0; j <= segMinor; j++)
                {
                    float b = j * Mathf.PI * 2f / segMinor;
                    Vector3 n = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    Vector3 p = centre + radial * (majorR + minorR * Mathf.Cos(b)) + Vector3.up * (minorR * Mathf.Sin(b));
                    mb.Add(p, n, new Vector2(a * majorR, b * minorR), c);
                }
            }
            int cols = segMinor + 1;
            for (int i = 0; i < segMajor; i++)
                for (int j = 0; j < segMinor; j++)
                {
                    int a0 = start + i * cols + j, a1 = start + (i + 1) * cols + j, b1 = start + (i + 1) * cols + j + 1, b0 = start + i * cols + j + 1;
                    // choose the winding that faces outward (checked against the vertex normal)
                    Vector3 fn = Vector3.Cross(mb.Positions[a1] - mb.Positions[a0], mb.Positions[b1] - mb.Positions[a0]);
                    if (Vector3.Dot(fn, mb.Normals[a0]) >= 0) mb.Quad(a0, a1, b1, b0); else mb.Quad(a0, b0, b1, a1);
                }
        }

        // ------------------------------------------------------------------ validation (T-NORMALS)
        /// <summary>Checks index range, degenerate triangles, unit normals and normal/winding agreement.</summary>
        public static bool Validate(Mesh mesh, out string report, float minArea = 1e-9f, float minNormalDot = -0.05f)
        {
            var v = mesh.vertices; var n = mesh.normals;
            int bad = 0, degenerate = 0, inverted = 0, badNormal = 0;
            var sb = new System.Text.StringBuilder();
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var idx = mesh.GetIndices(sm);
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= v.Length || b >= v.Length || c >= v.Length) { bad++; continue; }
                    Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                    float area = fn.magnitude * 0.5f;
                    if (area < minArea) { degenerate++; continue; }
                    fn /= (2f * area);
                    if (n.Length == v.Length)
                    {
                        Vector3 avg = (n[a] + n[b] + n[c]);
                        if (avg.sqrMagnitude > 1e-12f && Vector3.Dot(avg.normalized, fn) < minNormalDot) inverted++;
                    }
                }
            }
            if (n.Length == v.Length)
                for (int i = 0; i < n.Length; i++)
                    if (Mathf.Abs(n[i].magnitude - 1f) > 1e-3f || float.IsNaN(n[i].x)) badNormal++;
            sb.Append($"{mesh.name}: verts {v.Length}, tris {TriangleCount(mesh)}, outOfRange {bad}, degenerate {degenerate}, inverted {inverted}, badNormals {badNormal}");
            report = sb.ToString();
            return bad == 0 && degenerate == 0 && inverted == 0 && badNormal == 0;
        }
    }
}
