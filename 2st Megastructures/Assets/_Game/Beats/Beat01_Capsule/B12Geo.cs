using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Geometry helpers shared by the beat 1 and beat 2 builders, on top of <see cref="MeshBuilder"/>.
    /// Every helper orients its triangles from an expected (visible-side) normal, so winding is correct by
    /// construction (Unity: front faces clockwise seen from the front). MeshBuilder.M must be rigid (no mirroring).
    /// Degenerate triangles are dropped (MeshValidation rejects them). Only managed Unity maths is used here (no
    /// Matrix4x4.TRS / Quaternion.Euler), so the generators also run headless in the geometry self-test.
    /// </summary>
    internal static class B12Geo
    {
        // |cross|^2 below this is degenerate (area < 5e-10 m^2): keeps millimetre slivers at seams, drops zero-area ones
        const float MinCross2 = 1e-18f;

        // ------------------------------------------------------------------ rotations (managed)
        /// <summary>Rotation about +Y by deg (yaw 90 maps +Z to +X, as RingConstants.DirFromAzEl).</summary>
        public static Quaternion YawQ(float deg)
        {
            float h = 0.5f * deg * Mathf.Deg2Rad;
            return new Quaternion(0f, Mathf.Sin(h), 0f, Mathf.Cos(h));
        }

        /// <summary>Rotation about +Z by deg (maps +X toward +Y).</summary>
        public static Quaternion RollQ(float deg)
        {
            float h = 0.5f * deg * Mathf.Deg2Rad;
            return new Quaternion(0f, 0f, Mathf.Sin(h), Mathf.Cos(h));
        }

        /// <summary>Rotation about +X by deg (maps +Y toward +Z).</summary>
        public static Quaternion PitchQ(float deg)
        {
            float h = 0.5f * deg * Mathf.Deg2Rad;
            return new Quaternion(Mathf.Sin(h), 0f, 0f, Mathf.Cos(h));
        }

        /// <summary>Rigid matrix from an origin and three orthonormal columns.</summary>
        public static Matrix4x4 Frame(Vector3 o, Vector3 x, Vector3 y, Vector3 z)
        {
            var m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
            m.SetColumn(1, new Vector4(y.x, y.y, y.z, 0f));
            m.SetColumn(2, new Vector4(z.x, z.y, z.z, 0f));
            m.SetColumn(3, new Vector4(o.x, o.y, o.z, 1f));
            return m;
        }

        /// <summary>Translation times rotation (managed replacement for TRS with unit scale).</summary>
        public static Matrix4x4 TR(Vector3 t, Quaternion r) => Matrix4x4.Translate(t) * Matrix4x4.Rotate(r);

        // ------------------------------------------------------------------ primitives
        /// <summary>Triangle with explicit vertex normals; winding follows the summed vertex normal.</summary>
        public static void Tri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc,
            Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 fn = Vector3.Cross(b - a, c - a);
            if (fn.sqrMagnitude < MinCross2) return;
            if (Vector3.Dot(fn, na + nb + nc) < 0f)
            {
                var t = b; b = c; c = t; var tn = nb; nb = nc; nc = tn; var tu = ub; ub = uc; uc = tu;
            }
            mb.Tri(mb.Vtx(a, na, ua), mb.Vtx(b, nb, ub), mb.Vtx(c, nc, uc));
        }

        public static void TriN(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            => Tri(mb, a, b, c, n, n, n, Vector2.zero, Vector2.right, Vector2.up);

        /// <summary>Quad a-b-c-d (a closed loop in either direction) with vertex normals; oriented by the normals.
        /// Splits into two triangles; a degenerate half is dropped.</summary>
        public static void Quad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 fn = Vector3.Cross(c - a, d - b);
            Vector3 ns = na + nb + nc + nd;
            if (Vector3.Dot(fn, ns) < 0f)
            {
                var t = b; b = d; d = t; var tn = nb; nb = nd; nd = tn; var tu = ub; ub = ud; ud = tu;
            }
            Vector3 f1 = Vector3.Cross(b - a, c - a), f2 = Vector3.Cross(c - a, d - a);
            bool t1 = f1.sqrMagnitude >= MinCross2;
            bool t2 = f2.sqrMagnitude >= MinCross2;
            // a twisted or nearly collapsed quad: let each half orient itself (same edges, so seams stay shared)
            if (t1 && t2 && (Vector3.Dot(f1, na + nb + nc) <= 0f || Vector3.Dot(f2, na + nc + nd) <= 0f))
            {
                Tri(mb, a, b, c, na, nb, nc, ua, ub, uc);
                Tri(mb, a, c, d, na, nc, nd, ua, uc, ud);
                return;
            }
            if (t1 && t2)
            {
                int i0 = mb.Vtx(a, na, ua), i1 = mb.Vtx(b, nb, ub), i2 = mb.Vtx(c, nc, uc), i3 = mb.Vtx(d, nd, ud);
                mb.Quad(i0, i1, i2, i3);
                return;
            }
            if (t1) Tri(mb, a, b, c, na, nb, nc, ua, ub, uc);
            if (t2) Tri(mb, a, c, d, na, nc, nd, ua, uc, ud);
        }

        /// <summary>Flat quad with one normal (planar uv from the edge lengths).</summary>
        public static void QuadN(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            float w = (b - a).magnitude, h = (d - a).magnitude;
            Quad(mb, a, b, c, d, n, n, n, n, Vector2.zero, new Vector2(w, 0f), new Vector2(w, h), new Vector2(0f, h));
        }

        /// <summary>Convex (or star-shaped about its centroid) polygon fan, facing n.</summary>
        public static void Fan(MeshBuilder mb, IList<Vector3> pts, Vector3 n)
        {
            int k = pts.Count;
            if (k < 3) return;
            Vector3 c = Vector3.zero;
            for (int i = 0; i < k; i++) c += pts[i];
            c /= k;
            for (int i = 0; i < k; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % k];
                Tri(mb, c, a, b, n, n, n, Vector2.zero, new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            }
        }

        /// <summary>Planar polygon (any simple shape) triangulated by ear clipping in its plane, facing n.</summary>
        public static void Polygon(MeshBuilder mb, IList<Vector3> pts, Vector3 n)
        {
            int k = pts.Count;
            if (k < 3) return;
            Frame2(n, out Vector3 ax, out Vector3 ay);
            var p2 = new List<Vector2>(k);
            for (int i = 0; i < k; i++) p2.Add(new Vector2(Vector3.Dot(pts[i], ax), Vector3.Dot(pts[i], ay)));
            var tris = new List<int>();
            B12Poly.Triangulate(p2, tris);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector3 a = pts[tris[t]], b = pts[tris[t + 1]], c = pts[tris[t + 2]];
                Tri(mb, a, b, c, n, n, n, p2[tris[t]], p2[tris[t + 1]], p2[tris[t + 2]]);
            }
        }

        /// <summary>Planar polygon with one hole (both simple, the hole strictly inside), facing n.</summary>
        public static void PolygonWithHole(MeshBuilder mb, IList<Vector3> outer, IList<Vector3> hole, Vector3 n)
        {
            Frame2(n, out Vector3 ax, out Vector3 ay);
            var o2 = new List<Vector2>(outer.Count);
            var h2 = new List<Vector2>(hole.Count);
            var all = new List<Vector3>(outer.Count + hole.Count);
            for (int i = 0; i < outer.Count; i++) { o2.Add(new Vector2(Vector3.Dot(outer[i], ax), Vector3.Dot(outer[i], ay))); all.Add(outer[i]); }
            for (int i = 0; i < hole.Count; i++) { h2.Add(new Vector2(Vector3.Dot(hole[i], ax), Vector3.Dot(hole[i], ay))); all.Add(hole[i]); }
            var tris = new List<int>();
            if (!B12Poly.TriangulateWithHole(o2, h2, tris)) return;
            var uv = new List<Vector2>(o2); uv.AddRange(h2);
            for (int t = 0; t + 2 < tris.Count; t += 3)
                Tri(mb, all[tris[t]], all[tris[t + 1]], all[tris[t + 2]], n, n, n, uv[tris[t]], uv[tris[t + 1]], uv[tris[t + 2]]);
        }

        /// <summary>Two in-plane axes for a plane with normal n.</summary>
        public static void Frame2(Vector3 n, out Vector3 ax, out Vector3 ay)
        {
            n = n.normalized;
            ax = Vector3.Cross(Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right, n).normalized;
            ay = Vector3.Cross(n, ax).normalized;
        }

        /// <summary>Chamfered box with an orientation (rotation about its centre).</summary>
        public static void Box(MeshBuilder mb, Vector3 centre, Vector3 size, float chamfer, Quaternion rot)
        {
            var old = mb.M;
            mb.M = old * TR(centre, rot);
            mb.ChamferedBox(Vector3.zero, size, chamfer);
            mb.M = old;
        }

        public static void Box(MeshBuilder mb, Vector3 centre, Vector3 size, float chamfer) => mb.ChamferedBox(centre, size, chamfer);

        /// <summary>Axis-aligned chamfered box from min/max corners.</summary>
        public static void BoxMinMax(MeshBuilder mb, Vector3 min, Vector3 max, float chamfer)
            => mb.ChamferedBox((min + max) * 0.5f, max - min, chamfer);

        /// <summary>Chamfered box spanning the segment a-b (its local z), with cross-section w x h; 'up' fixes the roll.</summary>
        public static void BoxSpan(MeshBuilder mb, Vector3 a, Vector3 b, float w, float h, float chamfer, Vector3 up)
        {
            Vector3 z = b - a;
            float len = z.magnitude;
            if (len < 1e-5f) return;
            z /= len;
            Vector3 x = Vector3.Cross(up, z);
            if (x.sqrMagnitude < 1e-8f) x = Vector3.Cross(Vector3.right, z);
            x.Normalize();
            Vector3 y = Vector3.Cross(z, x);
            var old = mb.M;
            mb.M = old * Frame(0.5f * (a + b), x, y, z);
            mb.ChamferedBox(Vector3.zero, new Vector3(w, h, len), chamfer);
            mb.M = old;
        }

        // ------------------------------------------------------------------ sweeps
        /// <summary>
        /// Sweeps a 2D profile (x = along side, y = vertical) along a path. Positions use the vertical axis (so side
        /// faces stay vertical on slopes); normals use the frame perpendicular to the path tangent. The profile is
        /// counter-clockwise (outward normals (e.y, -e.x)); edges are flat-faceted across the profile and smooth along
        /// the path. Optional end caps (star-shaped profiles; triangulated by ear clipping).
        /// </summary>
        public static void Sweep(MeshBuilder mb, IList<Vector3> path, IList<Vector3> side, IList<Vector2> profile,
            bool closedProfile, bool capStart, bool capEnd)
        {
            int np = path.Count, ns = profile.Count;
            if (np < 2 || ns < 2) return;
            var tan = new Vector3[np];
            var up = new Vector3[np];
            float[] along = new float[np];
            for (int k = 0; k < np; k++)
            {
                Vector3 t = k == 0 ? path[1] - path[0] : (k == np - 1 ? path[np - 1] - path[np - 2] : path[k + 1] - path[k - 1]);
                tan[k] = t.normalized;
                up[k] = Vector3.Cross(tan[k], side[k]).normalized;
                if (up[k].y < 0f) up[k] = -up[k];
                if (k > 0) along[k] = along[k - 1] + (path[k] - path[k - 1]).magnitude;
            }
            int edges = closedProfile ? ns : ns - 1;
            for (int e = 0; e < edges; e++)
            {
                Vector2 p0 = profile[e], p1 = profile[(e + 1) % ns];
                Vector2 ed = p1 - p0;
                if (ed.sqrMagnitude < 1e-12f) continue;
                Vector2 en = new Vector2(ed.y, -ed.x).normalized;
                float l0 = 0f, l1 = ed.magnitude;
                for (int k = 0; k < np - 1; k++)
                {
                    Vector3 a = path[k] + side[k] * p0.x + Vector3.up * p0.y;
                    Vector3 b = path[k] + side[k] * p1.x + Vector3.up * p1.y;
                    Vector3 c = path[k + 1] + side[k + 1] * p1.x + Vector3.up * p1.y;
                    Vector3 d = path[k + 1] + side[k + 1] * p0.x + Vector3.up * p0.y;
                    Vector3 n0 = (side[k] * en.x + up[k] * en.y).normalized;
                    Vector3 n1 = (side[k + 1] * en.x + up[k + 1] * en.y).normalized;
                    Quad(mb, a, b, c, d, n0, n0, n1, n1,
                        new Vector2(l0, along[k]), new Vector2(l1, along[k]), new Vector2(l1, along[k + 1]), new Vector2(l0, along[k + 1]));
                }
            }
            if (capStart || capEnd)
            {
                var pts = new List<Vector3>(ns);
                for (int end = 0; end < 2; end++)
                {
                    if (end == 0 && !capStart) continue;
                    if (end == 1 && !capEnd) continue;
                    int k = end == 0 ? 0 : np - 1;
                    pts.Clear();
                    for (int i = 0; i < ns; i++) pts.Add(path[k] + side[k] * profile[i].x + Vector3.up * profile[i].y);
                    Vector3 cn = Vector3.Cross(side[k], Vector3.up).normalized;   // plane normal of the cap polygon
                    if (Vector3.Dot(cn, end == 0 ? -tan[k] : tan[k]) < 0f) cn = -cn;
                    Polygon(mb, pts, cn);
                }
            }
        }

        /// <summary>Straight sweep helper: path along a polyline with a horizontal side vector derived from the tangent
        /// (side = up x tangent, i.e. to the right of the walking direction), or its opposite when left = true.</summary>
        public static void SweepAuto(MeshBuilder mb, IList<Vector3> path, IList<Vector2> profile, bool closedProfile, bool caps, bool left = false)
        {
            int np = path.Count;
            var side = new Vector3[np];
            for (int k = 0; k < np; k++)
            {
                Vector3 t = k == 0 ? path[1] - path[0] : (k == np - 1 ? path[np - 1] - path[np - 2] : path[k + 1] - path[k - 1]);
                t.y = 0f;
                side[k] = Vector3.Cross(Vector3.up, t).normalized * (left ? -1f : 1f);
            }
            Sweep(mb, path, side, profile, closedProfile, caps, caps);
        }

        /// <summary>Chamfered rectangle profile (counter-clockwise), centred on x = 0, from y0 to y1, chamfer ch on the
        /// top edges only (bottom edges are embedded) or on all four.</summary>
        public static Vector2[] ChamferRect(float halfW, float y0, float y1, float ch, bool bottomToo)
        {
            if (bottomToo)
                return new[]
                {
                    new Vector2(-halfW + ch, y0), new Vector2(halfW - ch, y0), new Vector2(halfW, y0 + ch), new Vector2(halfW, y1 - ch),
                    new Vector2(halfW - ch, y1), new Vector2(-halfW + ch, y1), new Vector2(-halfW, y1 - ch), new Vector2(-halfW, y0 + ch)
                };
            return new[]
            {
                new Vector2(-halfW, y0), new Vector2(halfW, y0), new Vector2(halfW, y1 - ch),
                new Vector2(halfW - ch, y1), new Vector2(-halfW + ch, y1), new Vector2(-halfW, y1 - ch)
            };
        }

        /// <summary>
        /// Round tube along a polyline (radius r, segments around, parallel-transported frame, flat end caps).
        /// Smooth normals around and along.
        /// </summary>
        public static void Tube(MeshBuilder mb, IList<Vector3> path, float r, int segments, bool caps)
        {
            int n = path.Count;
            if (n < 2 || segments < 3) return;
            var ring = new Vector3[n * segments];
            var nrm = new Vector3[n * segments];
            Vector3 t0 = (path[1] - path[0]).normalized;
            Vector3 side = Vector3.Cross(Mathf.Abs(Vector3.Dot(t0, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up, t0).normalized;
            float[] along = new float[n];
            for (int k = 0; k < n; k++)
            {
                Vector3 t = k == 0 ? (path[1] - path[0]) : (k == n - 1 ? (path[n - 1] - path[n - 2]) : (path[k + 1] - path[k - 1]));
                t.Normalize();
                side = (side - Vector3.Dot(side, t) * t).normalized;
                Vector3 up2 = Vector3.Cross(t, side);
                if (k > 0) along[k] = along[k - 1] + (path[k] - path[k - 1]).magnitude;
                for (int s = 0; s < segments; s++)
                {
                    float a = 2f * Mathf.PI * s / segments;
                    Vector3 d = side * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                    ring[k * segments + s] = path[k] + d * r;
                    nrm[k * segments + s] = d;
                }
            }
            for (int k = 0; k < n - 1; k++)
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    int a0 = k * segments + s, a1 = k * segments + s1, b0 = (k + 1) * segments + s, b1 = (k + 1) * segments + s1;
                    float ua = 2f * Mathf.PI * r * s / segments, ub = 2f * Mathf.PI * r * (s + 1) / segments;
                    Quad(mb, ring[a0], ring[a1], ring[b1], ring[b0], nrm[a0], nrm[a1], nrm[b1], nrm[b0],
                        new Vector2(ua, along[k]), new Vector2(ub, along[k]), new Vector2(ub, along[k + 1]), new Vector2(ua, along[k + 1]));
                }
            if (!caps) return;
            var pts = new List<Vector3>(segments);
            for (int end = 0; end < 2; end++)
            {
                int k = end == 0 ? 0 : n - 1;
                Vector3 tn = end == 0 ? (path[0] - path[1]).normalized : (path[n - 1] - path[n - 2]).normalized;
                pts.Clear();
                for (int s = 0; s < segments; s++) pts.Add(ring[k * segments + s]);
                Fan(mb, pts, tn);
            }
        }

        // ------------------------------------------------------------------ lathes
        /// <summary>
        /// Surface of revolution about an arbitrary axis: position = c + r (e1 cos phi + e2 sin phi) + axis * a for each
        /// profile point (r, a); flat facets across the profile (crisp chamfers), smooth around. The visible side of the
        /// profile segment from (r0, a0) to (r1, a1) is given by the 2D normal (-(a1 - a0), r1 - r0): the LEFT of the
        /// travel direction in the (r right, a up) plane. An outward-facing cylinder is therefore listed top to bottom.
        /// angles: explicit generator angles (radians, ascending, closed loop).
        /// </summary>
        public static void LatheAxis(MeshBuilder mb, Vector3 c, Vector3 axis, Vector3 e1, Vector3 e2, IList<Vector2> profile,
            IList<float> angles, bool closed = true, bool smoothProfile = false)
        {
            int na = angles.Count, np = profile.Count;
            if (na < 2 || np < 2) return;
            int cols = closed ? na : na - 1;
            // smoothProfile: per-point normals across the profile (central differences, same visible-side rule), for
            // rounded beads; otherwise flat facets
            Vector2[] pn = null;
            if (smoothProfile)
            {
                pn = new Vector2[np];
                for (int i = 0; i < np; i++)
                {
                    Vector2 t = profile[Mathf.Min(i + 1, np - 1)] - profile[Mathf.Max(i - 1, 0)];
                    pn[i] = new Vector2(-t.y, t.x).normalized;
                }
            }
            for (int i = 0; i + 1 < np; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                float dr = p1.x - p0.x, da = p1.y - p0.y;
                float len = Mathf.Sqrt(dr * dr + da * da);
                if (len < 1e-7f) continue;
                float nr = -da / len, nax = dr / len;
                Vector2 n0 = smoothProfile ? pn[i] : new Vector2(nr, nax), n1 = smoothProfile ? pn[i + 1] : n0;
                for (int s = 0; s < cols; s++)
                {
                    float fa = angles[s], fb = angles[(s + 1) % na];
                    Vector3 ra = e1 * Mathf.Cos(fa) + e2 * Mathf.Sin(fa), rb = e1 * Mathf.Cos(fb) + e2 * Mathf.Sin(fb);
                    Vector3 a0 = c + ra * p0.x + axis * p0.y, a1 = c + ra * p1.x + axis * p1.y;
                    Vector3 b0 = c + rb * p0.x + axis * p0.y, b1 = c + rb * p1.x + axis * p1.y;
                    Vector3 na0 = ra * n0.x + axis * n0.y, nb0 = rb * n0.x + axis * n0.y;
                    Vector3 na1 = ra * n1.x + axis * n1.y, nb1 = rb * n1.x + axis * n1.y;
                    Quad(mb, a0, b0, b1, a1, na0, nb0, nb1, na1,
                        new Vector2(fa * Mathf.Max(p0.x, 0.01f), 0f), new Vector2(fb * Mathf.Max(p0.x, 0.01f), 0f),
                        new Vector2(fb * Mathf.Max(p1.x, 0.01f), len), new Vector2(fa * Mathf.Max(p1.x, 0.01f), len));
                }
            }
        }

        /// <summary>Uniform closed list of n angles (radians).</summary>
        public static List<float> Angles(int n, float start = 0f)
        {
            var l = new List<float>(n);
            for (int i = 0; i < n; i++) l.Add(start + 2f * Mathf.PI * i / n);
            return l;
        }

        /// <summary>Vertical lathe (axis +Y through c) with smooth normals across the profile, see LatheAxis for the
        /// visible-side rule (here a = height).</summary>
        public static void LatheY(MeshBuilder mb, Vector3 c, IList<Vector2> profile, int segments, bool smooth)
        {
            var ang = Angles(segments);
            if (!smooth) { LatheAxis(mb, c, Vector3.up, Vector3.right, Vector3.forward, profile, ang); return; }
            int np = profile.Count;
            var n2 = new Vector2[np];
            for (int i = 0; i < np; i++)
            {
                Vector2 t = profile[Mathf.Min(i + 1, np - 1)] - profile[Mathf.Max(i - 1, 0)];
                n2[i] = new Vector2(-t.y, t.x).normalized;
            }
            for (int i = 0; i + 1 < np; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                if ((p1 - p0).sqrMagnitude < 1e-12f) continue;
                for (int s = 0; s < segments; s++)
                {
                    float fa = ang[s], fb = ang[(s + 1) % segments];
                    Vector3 ra = Vector3.right * Mathf.Cos(fa) + Vector3.forward * Mathf.Sin(fa);
                    Vector3 rb = Vector3.right * Mathf.Cos(fb) + Vector3.forward * Mathf.Sin(fb);
                    Vector3 a0 = c + ra * p0.x + Vector3.up * p0.y, a1 = c + ra * p1.x + Vector3.up * p1.y;
                    Vector3 b0 = c + rb * p0.x + Vector3.up * p0.y, b1 = c + rb * p1.x + Vector3.up * p1.y;
                    Vector3 na0 = ra * n2[i].x + Vector3.up * n2[i].y, na1 = ra * n2[i + 1].x + Vector3.up * n2[i + 1].y;
                    Vector3 nb0 = rb * n2[i].x + Vector3.up * n2[i].y, nb1 = rb * n2[i + 1].x + Vector3.up * n2[i + 1].y;
                    Quad(mb, a0, b0, b1, a1, na0, nb0, nb1, na1,
                        new Vector2(fa * p0.x, p0.y), new Vector2(fb * p0.x, p0.y), new Vector2(fb * p1.x, p1.y), new Vector2(fa * p1.x, p1.y));
                }
            }
        }

        /// <summary>Horizontal disc (fan) at height y facing up or down.</summary>
        public static void DiscY(MeshBuilder mb, Vector3 c, float r, int segments, bool up)
        {
            var pts = new List<Vector3>(segments);
            for (int s = 0; s < segments; s++)
            {
                float a = 2f * Mathf.PI * s / segments;
                pts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
            Fan(mb, pts, up ? Vector3.up : Vector3.down);
        }

        // ------------------------------------------------------------------ slabs
        /// <summary>
        /// Rectangular slab with a 45-degree chamfer of width ch on its top edges (mitred corners) and vertical skirts
        /// down to yBot (landing pad, plinths). Top at yTop.
        /// </summary>
        public static void ChamferSlab(MeshBuilder mb, float x0, float x1, float z0, float z1, float yTop, float yBot, float ch)
        {
            float yc = yTop - ch;
            Vector3 up = Vector3.up;
            QuadN(mb, new Vector3(x0 + ch, yTop, z0 + ch), new Vector3(x1 - ch, yTop, z0 + ch), new Vector3(x1 - ch, yTop, z1 - ch), new Vector3(x0 + ch, yTop, z1 - ch), up);
            Vector3 nzm = new Vector3(0f, 1f, -1f).normalized, nzp = new Vector3(0f, 1f, 1f).normalized;
            Vector3 nxm = new Vector3(-1f, 1f, 0f).normalized, nxp = new Vector3(1f, 1f, 0f).normalized;
            Quad(mb, new Vector3(x0, yc, z0), new Vector3(x1, yc, z0), new Vector3(x1 - ch, yTop, z0 + ch), new Vector3(x0 + ch, yTop, z0 + ch), nzm, nzm, nzm, nzm, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            Quad(mb, new Vector3(x1, yc, z1), new Vector3(x0, yc, z1), new Vector3(x0 + ch, yTop, z1 - ch), new Vector3(x1 - ch, yTop, z1 - ch), nzp, nzp, nzp, nzp, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            Quad(mb, new Vector3(x0, yc, z1), new Vector3(x0, yc, z0), new Vector3(x0 + ch, yTop, z0 + ch), new Vector3(x0 + ch, yTop, z1 - ch), nxm, nxm, nxm, nxm, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            Quad(mb, new Vector3(x1, yc, z0), new Vector3(x1, yc, z1), new Vector3(x1 - ch, yTop, z1 - ch), new Vector3(x1 - ch, yTop, z0 + ch), nxp, nxp, nxp, nxp, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            if (yBot < yc)
            {
                QuadN(mb, new Vector3(x0, yBot, z0), new Vector3(x1, yBot, z0), new Vector3(x1, yc, z0), new Vector3(x0, yc, z0), Vector3.back);
                QuadN(mb, new Vector3(x1, yBot, z1), new Vector3(x0, yBot, z1), new Vector3(x0, yc, z1), new Vector3(x1, yc, z1), Vector3.forward);
                QuadN(mb, new Vector3(x0, yBot, z1), new Vector3(x0, yBot, z0), new Vector3(x0, yc, z0), new Vector3(x0, yc, z1), Vector3.left);
                QuadN(mb, new Vector3(x1, yBot, z0), new Vector3(x1, yBot, z1), new Vector3(x1, yc, z1), new Vector3(x1, yc, z0), Vector3.right);
            }
        }

        /// <summary>
        /// A prism along z (x half-width halfW about cx) whose top follows top(z) and whose bottom is yBot, with a
        /// chamfer ch on the two top long edges, sampled every dz; end walls at z0 and z1. Used for decks, abutments,
        /// ramps and collider slabs.
        /// </summary>
        public static void TopFollowPrism(MeshBuilder mb, float cx, float halfW, float z0, float z1, System.Func<float, float> top,
            System.Func<float, float> bot, float ch, float dz, bool ends = true, bool bottom = true)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / Mathf.Max(dz, 0.02f)));
            var zs = new float[n + 1];
            for (int i = 0; i <= n; i++) zs[i] = Mathf.Lerp(z0, z1, (float)i / n);
            TopFollowPrism(mb, cx, halfW, zs, top, bot, ch, ends, bottom);
        }

        public static void TopFollowPrism(MeshBuilder mb, float cx, float halfW, IList<float> zs, System.Func<float, float> top,
            System.Func<float, float> bot, float ch, bool ends, bool bottom)
        {
            int n = zs.Count - 1;
            if (n < 1) return;
            // profile (x offset, y offset from the top), counter-clockwise in (x, y), bottom handled per station
            for (int i = 0; i < n; i++)
            {
                float za = zs[i], zb = zs[i + 1];
                float ta = top(za), tb = top(zb), ba = bot(za), bb = bot(zb);
                // slope normal of the top surface
                Vector3 tn = new Vector3(0f, zb - za, -(tb - ta)).normalized;
                if (tn.y < 0f) tn = -tn;
                // top
                QuadN(mb, new Vector3(cx - halfW + ch, ta, za), new Vector3(cx + halfW - ch, ta, za), new Vector3(cx + halfW - ch, tb, zb), new Vector3(cx - halfW + ch, tb, zb), tn);
                // chamfers
                if (ch > 0f)
                {
                    Vector3 nr = (tn + Vector3.right).normalized, nl = (tn + Vector3.left).normalized;
                    QuadN(mb, new Vector3(cx + halfW - ch, ta, za), new Vector3(cx + halfW, ta - ch, za), new Vector3(cx + halfW, tb - ch, zb), new Vector3(cx + halfW - ch, tb, zb), nr);
                    QuadN(mb, new Vector3(cx - halfW, ta - ch, za), new Vector3(cx - halfW + ch, ta, za), new Vector3(cx - halfW + ch, tb, zb), new Vector3(cx - halfW, tb - ch, zb), nl);
                }
                // sides
                QuadN(mb, new Vector3(cx + halfW, ba, za), new Vector3(cx + halfW, bb, zb), new Vector3(cx + halfW, tb - ch, zb), new Vector3(cx + halfW, ta - ch, za), Vector3.right);
                QuadN(mb, new Vector3(cx - halfW, ba, za), new Vector3(cx - halfW, ta - ch, za), new Vector3(cx - halfW, tb - ch, zb), new Vector3(cx - halfW, bb, zb), Vector3.left);
                if (bottom)
                {
                    Vector3 bn = new Vector3(0f, -(zb - za), (bb - ba)).normalized;
                    if (bn.y > 0f) bn = -bn;
                    QuadN(mb, new Vector3(cx - halfW, ba, za), new Vector3(cx + halfW, ba, za), new Vector3(cx + halfW, bb, zb), new Vector3(cx - halfW, bb, zb), bn);
                }
            }
            if (!ends) return;
            for (int e = 0; e < 2; e++)
            {
                float z = e == 0 ? zs[0] : zs[n];
                float t = top(z), b = bot(z);
                var pts = new List<Vector3>
                {
                    new Vector3(cx - halfW, b, z), new Vector3(cx + halfW, b, z), new Vector3(cx + halfW, t - ch, z),
                    new Vector3(cx + halfW - ch, t, z), new Vector3(cx - halfW + ch, t, z), new Vector3(cx - halfW, t - ch, z)
                };
                Polygon(mb, pts, e == 0 ? Vector3.back : Vector3.forward);
            }
        }

        /// <summary>Orthonormal frame (e1, e2) perpendicular to the unit axis n; e1 horizontal when possible.</summary>
        public static void Frame(Vector3 n, out Vector3 e1, out Vector3 e2)
        {
            e1 = Vector3.Cross(Vector3.up, n);
            if (e1.sqrMagnitude < 1e-6f) e1 = Vector3.Cross(Vector3.right, n);
            e1.Normalize();
            e2 = Vector3.Cross(n, e1).normalized;
        }

        // ------------------------------------------------------------------ misc
        /// <summary>Sorted, de-duplicated sample list (tolerance tol).</summary>
        public static float[] Merge(IEnumerable<float> a, float tol = 1e-4f)
        {
            var l = new List<float>(a);
            l.Sort();
            var o = new List<float>(l.Count);
            for (int i = 0; i < l.Count; i++) if (o.Count == 0 || l[i] - o[o.Count - 1] > tol) o.Add(l[i]);
            return o.ToArray();
        }

        /// <summary>n + 1 evenly spaced samples from a to b.</summary>
        public static IEnumerable<float> Range(float a, float b, float step)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a) / Mathf.Max(step, 1e-4f) - 1e-4f));
            for (int i = 0; i <= n; i++) yield return Mathf.Lerp(a, b, (float)i / n);
        }
    }

    /// <summary>2D polygon tools: signed area, robust ear clipping, polygon-with-hole bridging.</summary>
    internal static class B12Poly
    {
        public static double Area(IList<Vector2> p)
        {
            double a = 0;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += (double)p[j].x * p[i].y - (double)p[i].x * p[j].y;
            return 0.5 * a;
        }

        static double Cross(Vector2 a, Vector2 b, Vector2 c) => ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            double d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
            const double e = -1e-14;
            return d1 >= e && d2 >= e && d3 >= e;
        }

        static bool Same(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-16f;

        /// <summary>Triangulates a simple polygon (either orientation). Appends index triples (into pts) to tris.</summary>
        public static void Triangulate(IList<Vector2> pts, List<int> tris)
        {
            var idx = new List<int>(pts.Count);
            for (int i = 0; i < pts.Count; i++) idx.Add(i);
            EarClip(pts, idx, tris);
        }

        /// <summary>Ear clipping of the polygon given by idx into pts (orientation detected).</summary>
        public static void EarClip(IList<Vector2> pts, List<int> idx, List<int> tris)
        {
            var poly = new List<int>(idx);
            // drop consecutive duplicates
            for (int i = poly.Count - 1; i >= 0 && poly.Count > 3; i--)
            {
                int j = (i + 1) % poly.Count;
                if (i != j && Same(pts[poly[i]], pts[poly[j]]) && poly[i] == poly[j]) poly.RemoveAt(i);
            }
            double area = 0;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) area += (double)pts[poly[j]].x * pts[poly[i]].y - (double)pts[poly[i]].x * pts[poly[j]].y;
            if (area < 0) poly.Reverse();
            int guard = 0;
            while (poly.Count > 3 && guard++ < 100000)
            {
                int n = poly.Count;
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    int ip = poly[(i + n - 1) % n], ic = poly[i], inx = poly[(i + 1) % n];
                    Vector2 a = pts[ip], b = pts[ic], c = pts[inx];
                    if (Cross(a, b, c) <= 1e-14) continue;     // reflex or degenerate
                    bool ear = true;
                    for (int k = 0; k < n; k++)
                    {
                        int q = poly[k];
                        if (q == ip || q == ic || q == inx) continue;
                        Vector2 p = pts[q];
                        if (Same(p, a) || Same(p, b) || Same(p, c)) continue;
                        if (InTri(p, a, b, c)) { ear = false; break; }
                    }
                    if (ear) { best = i; break; }
                }
                if (best < 0)
                {
                    // fallback (numerically degenerate input): clip the most convex vertex
                    double bc = double.NegativeInfinity;
                    for (int i = 0; i < n; i++)
                    {
                        double cr = Cross(pts[poly[(i + n - 1) % n]], pts[poly[i]], pts[poly[(i + 1) % n]]);
                        if (cr > bc) { bc = cr; best = i; }
                    }
                    if (bc <= 0) { poly.RemoveAt(best); continue; }
                }
                tris.Add(poly[(best + n - 1) % n]); tris.Add(poly[best]); tris.Add(poly[(best + 1) % n]);
                poly.RemoveAt(best);
            }
            if (poly.Count == 3 && Cross(pts[poly[0]], pts[poly[1]], pts[poly[2]]) > 1e-14) { tris.Add(poly[0]); tris.Add(poly[1]); tris.Add(poly[2]); }
        }

        static bool SegCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        /// <summary>
        /// Triangulates outer minus hole (indices: outer 0..no-1, hole no..no+nh-1). The hole must lie strictly inside the
        /// outer polygon. Returns false when no bridge is found.
        /// </summary>
        public static bool TriangulateWithHole(IList<Vector2> outer, IList<Vector2> hole, List<int> tris)
        {
            int no = outer.Count, nh = hole.Count;
            var pts = new List<Vector2>(no + nh);
            pts.AddRange(outer); pts.AddRange(hole);
            var o = new List<int>(no); for (int i = 0; i < no; i++) o.Add(i);
            var h = new List<int>(nh); for (int i = 0; i < nh; i++) h.Add(no + i);
            if (Area(outer) < 0) o.Reverse();
            if (Area(hole) > 0) h.Reverse();
            // bridge: hole vertex with the largest x, nearest outer vertex whose segment crosses no edge
            int hi = 0;
            for (int i = 1; i < h.Count; i++) if (pts[h[i]].x > pts[h[hi]].x) hi = i;
            Vector2 hp = pts[h[hi]];
            var order = new List<int>(no);
            for (int i = 0; i < no; i++) order.Add(i);
            order.Sort((x, y) => (pts[o[x]] - hp).sqrMagnitude.CompareTo((pts[o[y]] - hp).sqrMagnitude));
            int oi = -1;
            foreach (int cand in order)
            {
                Vector2 op = pts[o[cand]];
                bool ok = true;
                for (int i = 0; i < no && ok; i++)
                {
                    int a = o[i], b = o[(i + 1) % no];
                    if (a == o[cand] || b == o[cand]) continue;
                    if (SegCross(hp, op, pts[a], pts[b])) ok = false;
                }
                for (int i = 0; i < nh && ok; i++)
                {
                    int a = h[i], b = h[(i + 1) % nh];
                    if (a == h[hi] || b == h[hi]) continue;
                    if (SegCross(hp, op, pts[a], pts[b])) ok = false;
                }
                if (ok) { oi = cand; break; }
            }
            if (oi < 0) return false;
            var merged = new List<int>(no + nh + 2);
            for (int i = 0; i <= oi; i++) merged.Add(o[i]);
            for (int i = 0; i <= nh; i++) merged.Add(h[(hi + i) % nh]);
            for (int i = oi; i < no; i++) merged.Add(o[i]);
            EarClipMerged(pts, merged, tris);
            return true;
        }

        // Ear clipping that tolerates the duplicated bridge vertices (same index twice) of a merged polygon.
        static void EarClipMerged(List<Vector2> pts, List<int> poly, List<int> tris)
        {
            int guard = 0;
            while (poly.Count > 3 && guard++ < 200000)
            {
                int n = poly.Count;
                int best = -1;
                for (int i = 0; i < n; i++)
                {
                    int ip = poly[(i + n - 1) % n], ic = poly[i], inx = poly[(i + 1) % n];
                    Vector2 a = pts[ip], b = pts[ic], c = pts[inx];
                    if (Cross(a, b, c) <= 1e-14) continue;
                    bool ear = true;
                    for (int k = 0; k < n; k++)
                    {
                        int q = poly[k];
                        if (q == ip || q == ic || q == inx) continue;
                        Vector2 p = pts[q];
                        if (Same(p, a) || Same(p, b) || Same(p, c)) continue;
                        if (InTri(p, a, b, c)) { ear = false; break; }
                    }
                    if (ear) { best = i; break; }
                }
                if (best < 0)
                {
                    double bc = double.NegativeInfinity;
                    for (int i = 0; i < n; i++)
                    {
                        double cr = Cross(pts[poly[(i + n - 1) % n]], pts[poly[i]], pts[poly[(i + 1) % n]]);
                        if (cr > bc) { bc = cr; best = i; }
                    }
                    if (bc <= 1e-14) { poly.RemoveAt(best); continue; }
                }
                tris.Add(poly[(best + n - 1) % n]); tris.Add(poly[best]); tris.Add(poly[(best + 1) % n]);
                poly.RemoveAt(best);
            }
            if (poly.Count == 3 && Cross(pts[poly[0]], pts[poly[1]], pts[poly[2]]) > 1e-14) { tris.Add(poly[0]); tris.Add(poly[1]); tris.Add(poly[2]); }
        }
    }
}
