using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Procedural mesh builder (Part 7.3 MeshKit). Accumulates vertices with normal, uv0, uv1 and colour
    /// (R = AO, G = edge wear, B/A free) under a transform matrix, then builds a Mesh with 32-bit indices when needed,
    /// recalculated tangents and exact bounds. Winding: Unity is left-handed, front faces are clockwise when seen
    /// from outside; AddQuad(a, b, c, d) expects a, b, c, d in clockwise order seen from the front.
    /// </summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Vector2> UV1 = new List<Vector2>();
        public readonly List<Color> C = new List<Color>();
        public readonly List<int> I = new List<int>();
        public Matrix4x4 M = Matrix4x4.identity;
        public Color Col = new Color(1f, 0f, 0f, 0f); // AO 1, wear 0

        public int VertexCount => V.Count;
        public int TriangleCount => I.Count / 3;

        public int Vtx(Vector3 p, Vector3 n, Vector2 uv)
        {
            V.Add(M.MultiplyPoint3x4(p));
            N.Add(M.MultiplyVector(n).normalized);
            UV.Add(uv);
            UV1.Add(Vector2.zero);
            C.Add(Col);
            return V.Count - 1;
        }

        public int Vtx(Vector3 p, Vector3 n, Vector2 uv, Color c)
        {
            var old = Col; Col = c;
            int i = Vtx(p, n, uv);
            Col = old;
            return i;
        }

        public void Tri(int a, int b, int c) { I.Add(a); I.Add(b); I.Add(c); }
        public void Quad(int a, int b, int c, int d) { I.Add(a); I.Add(b); I.Add(c); I.Add(a); I.Add(c); I.Add(d); }

        /// <summary>Flat quad from 4 points in clockwise order seen from the front; normal from the winding.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvScale = default)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (uvScale == default) uvScale = Vector2.one;
            float w = (b - a).magnitude, h = (d - a).magnitude;
            int i0 = Vtx(a, n, new Vector2(0, 0)), i1 = Vtx(b, n, new Vector2(w * uvScale.x, 0));
            int i2 = Vtx(c, n, new Vector2(w * uvScale.x, h * uvScale.y)), i3 = Vtx(d, n, new Vector2(0, h * uvScale.y));
            Quad(i0, i1, i2, i3);
        }

        public void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Tri(Vtx(a, n, Vector2.zero), Vtx(b, n, Vector2.right), Vtx(c, n, Vector2.up));
        }

        /// <summary>Box centred at c with full size s and a 45-degree chamfer of width ch on every edge (ch = 0: plain box).
        /// Chamfer faces get vertex colour G = 1 (edge wear).</summary>
        public void ChamferedBox(Vector3 c, Vector3 s, float ch)
        {
            Vector3 h = s * 0.5f;
            ch = Mathf.Clamp(ch, 0f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
            Vector3 e = h - Vector3.one * ch; // inner extents
            var old = Col;
            // six main faces (inset by ch)
            for (int axis = 0; axis < 3; axis++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Vector3 n = Vector3.zero; n[axis] = sgn;
                    int u = (axis + 1) % 3, w = (axis + 2) % 3;
                    Vector3 cu = Vector3.zero, cw = Vector3.zero; cu[u] = e[u]; cw[w] = e[w];
                    Vector3 o = c + n * h[axis];
                    Vector3 p0 = o - cu - cw, p1 = o + cu - cw, p2 = o + cu + cw, p3 = o - cu + cw;
                    Col = old;
                    AddQuadN(p0, p1, p2, p3, n);
                }
            if (ch <= 0f) { Col = old; return; }
            Col = new Color(old.r, 1f, old.b, old.a);
            // 12 edge strips
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, w = (axis + 2) % 3;
                for (int su = -1; su <= 1; su += 2)
                    for (int sw = -1; sw <= 1; sw += 2)
                    {
                        Vector3 nu = Vector3.zero; nu[u] = su; Vector3 nw = Vector3.zero; nw[w] = sw;
                        Vector3 n = (nu + nw).normalized;
                        Vector3 along = Vector3.zero; along[axis] = e[axis];
                        Vector3 pu = c + nu * h[u] + nw * e[w]; pu[axis] = c[axis];
                        Vector3 pw = c + nu * e[u] + nw * h[w]; pw[axis] = c[axis];
                        Vector3 a = pu - along, b = pu + along, cc = pw + along, d = pw - along;
                        // orient so the normal points outward
                        AddQuadN(a, b, cc, d, n);
                    }
            }
            // 8 corner triangles
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 sg = new Vector3(sx, sy, sz);
                        Vector3 px = c + Vector3.Scale(sg, new Vector3(h.x, e.y, e.z));
                        Vector3 py = c + Vector3.Scale(sg, new Vector3(e.x, h.y, e.z));
                        Vector3 pz = c + Vector3.Scale(sg, new Vector3(e.x, e.y, h.z));
                        Vector3 n = sg.normalized;
                        TriN(px, py, pz, n);
                    }
            Col = old;
        }

        void AddQuadN(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { var t = b; b = d; d = t; }   // clockwise seen from the normal side
            int i0 = Vtx(a, n, UvPlanar(a, n)), i1 = Vtx(b, n, UvPlanar(b, n)), i2 = Vtx(c, n, UvPlanar(c, n)), i3 = Vtx(d, n, UvPlanar(d, n));
            Quad(i0, i1, i2, i3);
        }

        void TriN(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { var t = b; b = c; c = t; }
            Tri(Vtx(a, n, UvPlanar(a, n)), Vtx(b, n, UvPlanar(b, n)), Vtx(c, n, UvPlanar(c, n)));
        }

        static Vector2 UvPlanar(Vector3 p, Vector3 n)
        {
            Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z);
            if (a.x >= a.z) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }

        /// <summary>Surface of revolution about +Y through c. profile = (radius, y) points from bottom to top.
        /// Smooth normals along the ring, profile normals from neighbours; caps are not added.</summary>
        public void Lathe(Vector3 c, IList<Vector2> profile, int segments, float startDeg = 0f, float sweepDeg = 360f)
        {
            int n = profile.Count;
            bool closed = Mathf.Abs(sweepDeg - 360f) < 1e-3f;
            int cols = closed ? segments : segments + 1;
            int baseIdx = V.Count;
            float len = 0f;
            var acc = new float[n];
            for (int k = 1; k < n; k++) { len += (profile[k] - profile[k - 1]).magnitude; acc[k] = len; }
            for (int s = 0; s < cols; s++)
            {
                float a = (startDeg + sweepDeg * s / segments) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int k = 0; k < n; k++)
                {
                    Vector2 t = profile[Mathf.Min(k + 1, n - 1)] - profile[Mathf.Max(k - 1, 0)];
                    Vector2 pn = new Vector2(t.y, -t.x).normalized; // outward for profiles going up with positive radius
                    Vector3 nrm = (dir * pn.x + Vector3.up * pn.y).normalized;
                    Vtx(c + dir * profile[k].x + Vector3.up * profile[k].y, nrm, new Vector2(a * Mathf.Max(profile[k].x, 0.01f), acc[k]));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                int s1 = closed ? (s + 1) % segments : s + 1;
                for (int k = 0; k < n - 1; k++)
                {
                    int a0 = baseIdx + s * n + k, a1 = baseIdx + s * n + k + 1;
                    int b0 = baseIdx + s1 * n + k, b1 = baseIdx + s1 * n + k + 1;
                    // poles (radius ~0): emit the one non-degenerate triangle of the quad (a fan), never a sliver
                    bool poleK = profile[k].x <= 1e-3f, poleK1 = profile[k + 1].x <= 1e-3f;
                    if (poleK && poleK1) continue;
                    if (poleK) { Tri(a0, b1, a1); continue; }
                    if (poleK1) { Tri(a0, b0, b1); continue; }
                    Quad(a0, b0, b1, a1);
                }
            }
        }

        /// <summary>Disc cap (fan) in the plane y = c.y facing +Y (up = true) or -Y.</summary>
        public void Disc(Vector3 c, float r, int segments, bool up)
        {
            Vector3 n = up ? Vector3.up : Vector3.down;
            int ci = Vtx(c, n, Vector2.zero);
            int first = V.Count;
            for (int s = 0; s < segments; s++)
            {
                float a = 2f * Mathf.PI * s / segments;
                Vtx(c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r, n, new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r);
            }
            for (int s = 0; s < segments; s++)
            {
                int a = first + s, b = first + (s + 1) % segments;
                if (up) Tri(ci, a, b); else Tri(ci, b, a);
            }
        }

        /// <summary>Cylinder about +Y from c (bottom centre), optional caps.</summary>
        public void Cylinder(Vector3 c, float r, float h, int segments, bool capTop = true, bool capBottom = true)
        {
            Lathe(c, new[] { new Vector2(r, 0f), new Vector2(r, h) }, segments);
            if (capTop) Disc(c + Vector3.up * h, r, segments, true);
            if (capBottom) Disc(c, r, segments, false);
        }

        public void Sphere(Vector3 c, float r, int segments, int rings)
        {
            var prof = new List<Vector2>();
            for (int k = 0; k <= rings; k++)
            {
                float t = -Mathf.PI * 0.5f + Mathf.PI * k / rings;
                prof.Add(new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r));
            }
            prof[0] = new Vector2(1e-4f, -r); prof[rings] = new Vector2(1e-4f, r);
            Lathe(c, prof, segments);
        }

        /// <summary>Round tube along a polyline (radius r, segments around). Ends capped if caps.</summary>
        public void Tube(IList<Vector3> path, float r, int segments, bool caps = true)
        {
            int n = path.Count;
            if (n < 2) return;
            int baseIdx = V.Count;
            Vector3 prevT = (path[1] - path[0]).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(prevT, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 side = Vector3.Cross(up, prevT).normalized;
            float along = 0f;
            for (int k = 0; k < n; k++)
            {
                Vector3 t = k == 0 ? (path[1] - path[0]) : (k == n - 1 ? (path[n - 1] - path[n - 2]) : (path[k + 1] - path[k - 1]));
                t.Normalize();
                // parallel transport of the frame
                side = (side - Vector3.Dot(side, t) * t).normalized;
                Vector3 up2 = Vector3.Cross(t, side);
                if (k > 0) along += (path[k] - path[k - 1]).magnitude;
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2f * Mathf.PI * s / segments;
                    Vector3 nrm = side * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                    Vtx(path[k] + nrm * r, nrm, new Vector2(a * r, along));
                }
            }
            int cols = segments + 1;
            for (int k = 0; k < n - 1; k++)
                for (int s = 0; s < segments; s++)
                {
                    int a0 = baseIdx + k * cols + s, a1 = a0 + 1, b0 = a0 + cols, b1 = b0 + 1;
                    Quad(a0, a1, b1, b0);
                }
            if (caps)
            {
                for (int end = 0; end < 2; end++)
                {
                    int k = end == 0 ? 0 : n - 1;
                    Vector3 t = end == 0 ? (path[0] - path[1]).normalized : (path[n - 1] - path[n - 2]).normalized;
                    int ci = Vtx(path[k], t, Vector2.zero);
                    int ring = V.Count;
                    for (int s = 0; s < segments; s++) { Vector3 p = V[baseIdx + k * cols + s]; Vtx(M.inverse.MultiplyPoint3x4(p), t, Vector2.zero); }
                    for (int s = 0; s < segments; s++)
                    {
                        int a = ring + s, b = ring + (s + 1) % segments;
                        if (end == 0) Tri(ci, b, a); else Tri(ci, a, b);
                    }
                }
            }
        }

        /// <summary>Extrudes a closed 2D polygon (counter-clockwise in its own xy plane, x right, y up) along a
        /// polyline path with a fixed up vector (loft of a constant section, e.g. the capsule shell and tunnels).
        /// insideOut = true flips the faces so they are seen from inside (tunnels, capsule interior).</summary>
        public void Loft(IList<Vector2> section, IList<Vector3> path, Vector3 up, bool insideOut, bool closedSection = true)
        {
            int ns = section.Count, np = path.Count;
            int baseIdx = V.Count;
            var sn = new Vector2[ns];
            for (int i = 0; i < ns; i++)
            {
                Vector2 a = section[(i - 1 + ns) % ns], b = section[(i + 1) % ns];
                if (!closedSection && i == 0) a = section[0];
                if (!closedSection && i == ns - 1) b = section[ns - 1];
                Vector2 t = (b - a).normalized;
                sn[i] = new Vector2(t.y, -t.x); // outward for CCW sections
            }
            var perim = new float[ns + 1];
            for (int i = 1; i <= ns; i++) perim[i] = perim[i - 1] + (section[i % ns] - section[i - 1]).magnitude;
            float along = 0f;
            for (int k = 0; k < np; k++)
            {
                Vector3 t = k == 0 ? (path[1] - path[0]) : (k == np - 1 ? (path[np - 1] - path[np - 2]) : (path[k + 1] - path[k - 1]));
                t.Normalize();
                Vector3 x = Vector3.Cross(up, t).normalized;
                Vector3 y = Vector3.Cross(t, x);
                if (k > 0) along += (path[k] - path[k - 1]).magnitude;
                for (int i = 0; i <= ns; i++)
                {
                    int ii = i % ns;
                    Vector3 p = path[k] + x * section[ii].x + y * section[ii].y;
                    Vector3 nrm = (x * sn[ii].x + y * sn[ii].y) * (insideOut ? -1f : 1f);
                    Vtx(p, nrm, new Vector2(perim[i], along));
                }
            }
            int cols = ns + 1;
            int segs = closedSection ? ns : ns - 1;
            for (int k = 0; k < np - 1; k++)
                for (int i = 0; i < segs; i++)
                {
                    int a0 = baseIdx + k * cols + i, a1 = a0 + 1, b0 = a0 + cols, b1 = b0 + 1;
                    if (insideOut) Quad(a0, b0, b1, a1); else Quad(a0, a1, b1, b0);
                }
        }

        /// <summary>Appends another builder's geometry transformed by m.</summary>
        public void Append(MeshBuilder o, Matrix4x4 m)
        {
            int b = V.Count;
            for (int i = 0; i < o.V.Count; i++)
            {
                V.Add(m.MultiplyPoint3x4(o.V[i])); N.Add(m.MultiplyVector(o.N[i]).normalized);
                UV.Add(o.UV[i]); UV1.Add(o.UV1[i]); C.Add(o.C[i]);
            }
            for (int i = 0; i < o.I.Count; i++) I.Add(o.I[i] + b);
        }

        public Mesh Build(string name, bool tangents = true)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = V.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            mesh.SetUVs(1, UV1);
            mesh.SetColors(C);
            mesh.SetTriangles(I, 0, true);
            if (tangents && V.Count > 0) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Mesh validation used by EditMode tests and the load-time audit (Part 8.7, 10 #29).</summary>
    public static class MeshValidation
    {
        public struct Report { public int degenerate, badIndex, nanVerts, zeroNormals, triangles; public bool ok; public string note; }

        public static Report Validate(Mesh m, float minArea = 1e-10f)
        {
            var r = new Report();
            var v = m.vertices; var n = m.normals;
            for (int i = 0; i < v.Length; i++)
            {
                if (float.IsNaN(v[i].x) || float.IsNaN(v[i].y) || float.IsNaN(v[i].z) || float.IsInfinity(v[i].x)) r.nanVerts++;
                if (n.Length == v.Length && n[i].sqrMagnitude < 0.5f) r.zeroNormals++;
            }
            for (int sm = 0; sm < m.subMeshCount; sm++)
            {
                var idx = m.GetIndices(sm);
                for (int t = 0; t + 2 < idx.Length; t += 3)
                {
                    int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                    r.triangles++;
                    if (a < 0 || b < 0 || c < 0 || a >= v.Length || b >= v.Length || c >= v.Length) { r.badIndex++; continue; }
                    if (Vector3.Cross(v[b] - v[a], v[c] - v[a]).sqrMagnitude < minArea * minArea) r.degenerate++;
                }
            }
            r.ok = r.degenerate == 0 && r.badIndex == 0 && r.nanVerts == 0 && r.zeroNormals == 0;
            return r;
        }

        /// <summary>Fraction of triangles whose winding normal agrees with the stored vertex normals (outward check).</summary>
        public static float WindingAgreement(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var idx = m.triangles;
            if (n.Length != v.Length || idx.Length == 0) return 0f;
            int agree = 0, total = 0;
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                Vector3 fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (fn.sqrMagnitude < 1e-16f) continue;
                total++;
                if (Vector3.Dot(fn, n[a] + n[b] + n[c]) > 0f) agree++;
            }
            return total > 0 ? (float)agree / total : 0f;
        }
    }
}
