using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Rounded-rectangle section in the xy plane (centre x = 0), counter-clockwise perimeter parameter u starting at
    /// the bottom centre: bottom half, BR fillet, right wall, TR fillet, top, TL fillet, left wall, BL fillet,
    /// bottom half. Normals are outward. Used for the capsule shell (14.5.1). u is arc length, so (u, z) is an
    /// isometric chart of the extruded skin (a developable surface).
    /// </summary>
    internal sealed class RRect
    {
        public readonly float A, Y0, Y1, R;
        readonly float L0, Lf, H;
        readonly float[] _start = new float[10];
        public readonly float P;

        public RRect(float halfW, float y0, float y1, float r)
        {
            A = halfW; Y0 = y0; Y1 = y1; R = r;
            L0 = A - R; Lf = 0.5f * Mathf.PI * R; H = (Y1 - Y0) - 2f * R;
            float[] len = { L0, Lf, H, Lf, 2f * L0, Lf, H, Lf, L0 };
            _start[0] = 0f;
            for (int i = 0; i < 9; i++) _start[i + 1] = _start[i] + len[i];
            P = _start[9];
        }

        /// <summary>Same centre, every dimension grown by d (an offset curve: identical normals per segment).</summary>
        public RRect Offset(float d) => new RRect(A + d, Y0 - d, Y1 + d, R + d);

        public float SegStart(int k) => _start[k];
        public float SegLen(int k) => _start[k + 1] - _start[k];

        /// <summary>Point and outward normal at perimeter parameter u (wrapped).</summary>
        public void Point(float u, out Vector2 p, out Vector2 n)
        {
            u = Mathf.Repeat(u, P);
            int k = 8;
            for (int i = 0; i < 9; i++) if (u < _start[i + 1]) { k = i; break; }
            PointSeg(k, u - _start[k], out p, out n);
        }

        /// <summary>Point on segment k at arc length s from its start.</summary>
        public void PointSeg(int k, float s, out Vector2 p, out Vector2 n)
        {
            float ar = A - R;
            switch (k)
            {
                case 0: p = new Vector2(s, Y0); n = Vector2.down; return;
                case 1: Arc(new Vector2(ar, Y0 + R), -0.5f * Mathf.PI + s / Mathf.Max(R, 1e-6f), out p, out n); return;
                case 2: p = new Vector2(A, Y0 + R + s); n = Vector2.right; return;
                case 3: Arc(new Vector2(ar, Y1 - R), s / Mathf.Max(R, 1e-6f), out p, out n); return;
                case 4: p = new Vector2(ar - s, Y1); n = Vector2.up; return;
                case 5: Arc(new Vector2(-ar, Y1 - R), 0.5f * Mathf.PI + s / Mathf.Max(R, 1e-6f), out p, out n); return;
                case 6: p = new Vector2(-A, Y1 - R - s); n = Vector2.left; return;
                case 7: Arc(new Vector2(-ar, Y0 + R), Mathf.PI + s / Mathf.Max(R, 1e-6f), out p, out n); return;
                default: p = new Vector2(-ar + s, Y0); n = Vector2.down; return;
            }
        }

        /// <summary>Point at the same (segment, fraction) as parameter u of the section <paramref name="from"/>.</summary>
        public void PointLike(RRect from, float u, out Vector2 p, out Vector2 n)
        {
            int k = from.SegmentOf(u);
            float f = from.SegLen(k) > 1e-6f ? (u - from.SegStart(k)) / from.SegLen(k) : 0f;
            PointSeg(k, f * SegLen(k), out p, out n);
        }

        void Arc(Vector2 c, float th, out Vector2 p, out Vector2 n)
        {
            n = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
            p = c + n * R;
        }

        /// <summary>Signed distance (negative inside).</summary>
        public float Sdf(Vector2 p)
        {
            float yc = 0.5f * (Y0 + Y1), hh = 0.5f * (Y1 - Y0);
            float qx = Mathf.Abs(p.x) - (A - R), qy = Mathf.Abs(p.y - yc) - (hh - R);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - R;
        }

        /// <summary>Perimeter parameter of the boundary point nearest to p.</summary>
        public float Param(Vector2 p)
        {
            float ar = A - R;
            float best = float.MaxValue, bu = 0f;
            Consider(ref best, ref bu, Mathf.Abs(p.y - Y0) + Out(p.x, 0f, ar), _start[0] + Mathf.Clamp(p.x, 0f, ar));
            Consider(ref best, ref bu, Mathf.Abs(p.x - A) + Out(p.y, Y0 + R, Y1 - R), _start[2] + Mathf.Clamp(p.y - (Y0 + R), 0f, H));
            Consider(ref best, ref bu, Mathf.Abs(p.y - Y1) + Out(p.x, -ar, ar), _start[4] + Mathf.Clamp(ar - p.x, 0f, 2f * L0));
            Consider(ref best, ref bu, Mathf.Abs(p.x + A) + Out(p.y, Y0 + R, Y1 - R), _start[6] + Mathf.Clamp((Y1 - R) - p.y, 0f, H));
            Consider(ref best, ref bu, Mathf.Abs(p.y - Y0) + Out(p.x, -ar, 0f), _start[8] + Mathf.Clamp(p.x + ar, 0f, ar));
            FilletParam(ref best, ref bu, p, new Vector2(ar, Y0 + R), -0.5f * Mathf.PI, 1);
            FilletParam(ref best, ref bu, p, new Vector2(ar, Y1 - R), 0f, 3);
            FilletParam(ref best, ref bu, p, new Vector2(-ar, Y1 - R), 0.5f * Mathf.PI, 5);
            FilletParam(ref best, ref bu, p, new Vector2(-ar, Y0 + R), Mathf.PI, 7);
            return bu;
        }

        static float Out(float v, float lo, float hi) => v < lo ? lo - v : (v > hi ? v - hi : 0f);

        static void Consider(ref float best, ref float bu, float d, float u)
        {
            if (d < best) { best = d; bu = u; }
        }

        void FilletParam(ref float best, ref float bu, Vector2 p, Vector2 c, float th0, int seg)
        {
            Vector2 v = p - c;
            float th = Mathf.Atan2(v.y, v.x);
            while (th < th0 - Mathf.PI) th += 2f * Mathf.PI;
            while (th >= th0 + Mathf.PI) th -= 2f * Mathf.PI;
            float tc = Mathf.Clamp(th, th0, th0 + 0.5f * Mathf.PI);
            Vector2 q = c + new Vector2(Mathf.Cos(tc), Mathf.Sin(tc)) * R;
            Consider(ref best, ref bu, (p - q).magnitude, _start[seg] + (tc - th0) * R);
        }

        /// <summary>Ascending perimeter samples 0..P (inclusive) with the given piece counts per segment kind.</summary>
        public List<float> Samples(int nBottomHalf, int nFillet, int nWall, int nTop)
        {
            int[] pieces = { nBottomHalf, nFillet, nWall, nFillet, nTop, nFillet, nWall, nFillet, nBottomHalf };
            var list = new List<float>();
            for (int k = 0; k < 9; k++)
            {
                int n = Mathf.Max(1, pieces[k]);
                for (int i = 0; i < n; i++) list.Add(Mathf.Lerp(_start[k], _start[k + 1], (float)i / n));
            }
            list.Add(P);
            return list;
        }

        /// <summary>Segment index (0..8) of parameter u.</summary>
        public int SegmentOf(float u)
        {
            for (int i = 0; i < 9; i++) if (u < _start[i + 1] - 1e-5f) return i;
            return 8;
        }
    }

    /// <summary>A closed hole boundary on a skin: exact 3D points and their (u, z) chart coordinates.</summary>
    internal sealed class SkinLoop
    {
        public readonly List<Vector3> P = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
    }

    /// <summary>
    /// The section rr extruded along z over the sample grid (us x zs), with at most one hole. Cells that touch the
    /// hole (plus a margin) are removed; the removed block is orthogonally convex, so its boundary is one staircase
    /// loop of grid vertices; the region between that loop and the exact hole loop is ear-clipped in the isometric
    /// (u, z) chart. Every vertex lies on the skin, the hole vertices are bit-identical to the tube that meets them,
    /// and no vertex lies on another triangle's edge: watertight (14.5.1).
    /// </summary>
    internal static class SkinGrid
    {
        public static void Build(MeshBuilder mb, MeshBuilder floorMb, RRect rr, float[] us, float[] zs, bool inside,
            SkinLoop hole, System.Func<int, bool> floorSeg, float margin = 0.015f)
        {
            int nu = us.Length, nz = zs.Length;
            float sign = inside ? -1f : 1f;
            var removed = new bool[nu - 1, nz - 1];
            if (hole != null && hole.UV.Count >= 3)
            {
                Vector2 mn = hole.UV[0], mx = hole.UV[0];
                foreach (var q in hole.UV) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
                for (int i = 0; i < nu - 1; i++)
                    for (int j = 0; j < nz - 1; j++)
                    {
                        float u0 = us[i] - margin, u1 = us[i + 1] + margin, z0 = zs[j] - margin, z1 = zs[j + 1] + margin;
                        if (u1 < mn.x || u0 > mx.x || z1 < mn.y || z0 > mx.y) continue;
                        removed[i, j] = CellTouches(hole.UV, u0, u1, z0, z1);
                    }
                // orthogonal convexity (rows, then columns, until stable)
                bool changed = true;
                int guard = 0;
                while (changed && guard++ < 16)
                {
                    changed = false;
                    for (int j = 0; j < nz - 1; j++)
                    {
                        int a = -1, b = -1;
                        for (int i = 0; i < nu - 1; i++) if (removed[i, j]) { if (a < 0) a = i; b = i; }
                        if (a >= 0) for (int i = a; i <= b; i++) if (!removed[i, j]) { removed[i, j] = true; changed = true; }
                    }
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int a = -1, b = -1;
                        for (int j = 0; j < nz - 1; j++) if (removed[i, j]) { if (a < 0) a = j; b = j; }
                        if (a >= 0) for (int j = a; j <= b; j++) if (!removed[i, j]) { removed[i, j] = true; changed = true; }
                    }
                }
            }
            for (int i = 0; i < nu - 1; i++)
            {
                float um = 0.5f * (us[i] + us[i + 1]);
                bool isFloor = floorMb != null && floorSeg != null && floorSeg(rr.SegmentOf(um));
                var target = isFloor ? floorMb : mb;
                for (int j = 0; j < nz - 1; j++)
                {
                    if (removed[i, j]) continue;
                    Emit(target, rr, us[i], us[i + 1], zs[j], zs[j + 1], sign);
                }
            }
            if (hole != null && hole.UV.Count >= 3) FillAroundHole(mb, rr, us, zs, removed, hole, sign);
        }

        public static void S(RRect rr, float u, float z, float sign, out Vector3 p, out Vector3 n)
        {
            rr.Point(u, out Vector2 p2, out Vector2 n2);
            p = new Vector3(p2.x, p2.y, z);
            n = new Vector3(n2.x, n2.y, 0f) * sign;
        }

        static void Emit(MeshBuilder mb, RRect rr, float u0, float u1, float z0, float z1, float sign)
        {
            S(rr, u0, z0, sign, out var a, out var na);
            S(rr, u1, z0, sign, out var b, out var nb);
            S(rr, u1, z1, sign, out var c, out var nc);
            S(rr, u0, z1, sign, out var d, out var nd);
            B12Geo.Quad(mb, a, b, c, d, na, nb, nc, nd, new Vector2(u0, z0), new Vector2(u1, z0), new Vector2(u1, z1), new Vector2(u0, z1));
        }

        static bool CellTouches(List<Vector2> poly, float u0, float u1, float z0, float z1)
        {
            int n = poly.Count;
            for (int k = 0; k < n; k++)
            {
                var p = poly[k];
                if (p.x >= u0 && p.x <= u1 && p.y >= z0 && p.y <= z1) return true;
            }
            if (Inside(poly, new Vector2(u0, z0)) || Inside(poly, new Vector2(u1, z0)) || Inside(poly, new Vector2(u1, z1)) || Inside(poly, new Vector2(u0, z1))) return true;
            for (int k = 0; k < n; k++)
            {
                Vector2 a = poly[k], b = poly[(k + 1) % n];
                if (SegRect(a, b, u0, u1, z0, z1)) return true;
            }
            return false;
        }

        static bool Inside(List<Vector2> poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }

        static bool SegRect(Vector2 a, Vector2 b, float u0, float u1, float z0, float z1)
        {
            // Liang-Barsky clip
            float t0 = 0f, t1 = 1f, dx = b.x - a.x, dy = b.y - a.y;
            float[] p = { -dx, dx, -dy, dy };
            float[] q = { a.x - u0, u1 - a.x, a.y - z0, z1 - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-12f) { if (q[i] < 0f) return false; continue; }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return true;
        }

        static void FillAroundHole(MeshBuilder mb, RRect rr, float[] us, float[] zs, bool[,] removed, SkinLoop hole, float sign)
        {
            int nu = us.Length, nz = zs.Length;
            // directed boundary edges of the removed block (block on the left): key = vertex index, value = next vertex
            var next = new Dictionary<int, int>();
            int V(int i, int j) => j * nu + i;
            bool R(int i, int j) => i >= 0 && j >= 0 && i < nu - 1 && j < nz - 1 && removed[i, j];
            int start = -1;
            for (int i = 0; i < nu - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                {
                    if (!removed[i, j]) continue;
                    if (!R(i, j - 1)) { next[V(i, j)] = V(i + 1, j); if (start < 0) start = V(i, j); }
                    if (!R(i + 1, j)) next[V(i + 1, j)] = V(i + 1, j + 1);
                    if (!R(i, j + 1)) next[V(i + 1, j + 1)] = V(i, j + 1);
                    if (!R(i - 1, j)) next[V(i, j + 1)] = V(i, j);
                }
            if (start < 0) return;
            var loop = new List<int>();
            int cur = start, guard = 0;
            do
            {
                loop.Add(cur);
                if (!next.TryGetValue(cur, out cur)) break;
            } while (cur != start && guard++ < 100000);
            // collapse collinear staircase vertices? keep all: they are the neighbours' grid vertices (no T-junctions)
            var outer2 = new List<Vector2>(loop.Count);
            var outer3 = new List<Vector3>(loop.Count);
            var outerN = new List<Vector3>(loop.Count);
            foreach (int v in loop)
            {
                int i = v % nu, j = v / nu;
                S(rr, us[i], zs[j], sign, out var p, out var n);
                outer2.Add(new Vector2(us[i], zs[j])); outer3.Add(p); outerN.Add(n);
            }
            var hole3 = hole.P;
            var holeN = new List<Vector3>(hole3.Count);
            foreach (var q in hole.UV) { S(rr, q.x, q.y, sign, out _, out var n); holeN.Add(n); }
            var tris = new List<int>();
            if (!B12Poly.TriangulateWithHole(outer2, hole.UV, tris)) return;
            int no = outer2.Count;
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                B12Geo.Tri(mb,
                    a < no ? outer3[a] : hole3[a - no], b < no ? outer3[b] : hole3[b - no], c < no ? outer3[c] : hole3[c - no],
                    a < no ? outerN[a] : holeN[a - no], b < no ? outerN[b] : holeN[b - no], c < no ? outerN[c] : holeN[c - no],
                    a < no ? outer2[a] : hole.UV[a - no], b < no ? outer2[b] : hole.UV[b - no], c < no ? outer2[c] : hole.UV[c - no]);
            }
        }
    }

    /// <summary>
    /// The porthole (14.5.1): a cylinder of radius R about the porthole axis (normal yaw -45 / pitch +25 through the
    /// centre C) pierces the 0.3 m shell obliquely; a lathed pod (the bevelled panel with the 0.55 m opening, the
    /// 0.80 m frame and its PaleGold ring) sits across the cylinder at a = M..P. Because the axis is 50 degrees off the
    /// wall normal, the pod lies partly in the cabin (front-inner side) and partly outside the hull (rear-outer side).
    /// For every generator angle the cylinder surface is emitted exactly where the two sides differ (cabin C, solid S,
    /// exterior X; inside = C below M, pod S, X above P; outside = C below the inner-skin crossing I, wall S up to the
    /// outer-skin crossing O, X beyond): recess walls, protruding collars and, where cabin meets exterior, a
    /// two-sided collar. The generator list is refined at every change of the order of {I, O, M, P}, so the surface
    /// is continuous; the I and O points are the exact hole loops of the two skins. Result: one sealed shell whose only
    /// aperture is the 0.55 m opening, so the key enters only through the porthole.
    /// Porthole axis check (comment required by the task): n = DirFromAzEl(-45, 25) = (-0.641, 0.423, 0.641);
    /// the star vector L = (-0.833, 0.438, 0.337); n . L = 0.935 (20.8 deg off-axis, the opening subtends 17 deg from
    /// the KS01 eye 0.9 m away). The beam travels along -L from C = (-1.46, 2.98, 5.65) and meets the right wall
    /// x = 1.3 after t = 2.76 / 0.833 = 3.31 m at y = 2.98 - 0.438 t = 1.53 (0.63 m above the floor) and
    /// z = 5.65 - 0.337 t = 4.53: the warm patch near z 4.5 of 14.5.1.
    /// </summary>
    internal sealed class PortholeAssembly
    {
        public const float R = 0.44f;           // cylinder (bay) radius: frame 0.80 m + 4 cm of bevelled panel
        public const float M = -0.03f;          // pod cabin face (axial, + = outward)
        public const float Pf = 0.03f;          // pod exterior face
        public const float Ro = 0.275f;         // opening radius (0.55 m)
        const int BaseCount = 96;
        const float Search = 1.2f;

        public readonly Vector3 C, N, E1, E2;
        public readonly List<float> Phi = new List<float>();
        public readonly List<float> Ia = new List<float>(), Oa = new List<float>();
        public readonly List<Vector3> IP = new List<Vector3>(), OP = new List<Vector3>();
        readonly RRect _in, _out;

        enum St { C, S, X }

        public PortholeAssembly(RRect inner, RRect outer)
        {
            _in = inner; _out = outer;
            C = RingConstants.PortholeCentre;
            N = RingConstants.DirFromAzEl(RingConstants.PortholeYaw, RingConstants.PortholePitch).normalized;
            B12Geo.Frame(N, out E1, out E2);
            Compute();
        }

        public Vector3 Radial(float phi) => E1 * Mathf.Cos(phi) + E2 * Mathf.Sin(phi);
        public Vector3 G(float phi, float a) => C + Radial(phi) * R + N * a;
        public Vector3 G(float phi, float r, float a) => C + Radial(phi) * r + N * a;

        float Cross(RRect rr, float phi)
        {
            float lo = -Search, hi = Search;
            Vector3 b = C + Radial(phi) * R;
            float flo = rr.Sdf(new Vector2(b.x + N.x * lo, b.y + N.y * lo));
            if (flo > 0f) return lo;
            for (int i = 0; i < 52; i++)
            {
                float m = 0.5f * (lo + hi);
                float fm = rr.Sdf(new Vector2(b.x + N.x * m, b.y + N.y * m));
                if (fm <= 0f) lo = m; else hi = m;
            }
            return 0.5f * (lo + hi);
        }

        void Compute()
        {
            // base angles; every root of I - M, I - P, O - M, O - P (a change of the order of the four breakpoints) is
            // either snapped onto the nearest base angle (within a quarter step, so no millimetre slivers) or inserted
            float step = 2f * Mathf.PI / BaseCount;
            var phis = new List<float>();
            for (int i = 0; i < BaseCount; i++) phis.Add(step * i);
            var roots = new List<float>();
            for (int i = 0; i < BaseCount; i++)
            {
                float pa = phis[i], pb = pa + step;
                for (int d = 0; d < 4; d++)
                {
                    float fa = Diff(d, pa), fb = Diff(d, pb);
                    if (fa == 0f || fb == 0f || (fa > 0f) == (fb > 0f)) continue;
                    float lo = pa, hi = pb;
                    for (int it = 0; it < 40; it++)
                    {
                        float m = 0.5f * (lo + hi);
                        float fm = Diff(d, m);
                        if ((fm > 0f) == (fa > 0f)) lo = m; else hi = m;
                    }
                    roots.Add(0.5f * (lo + hi));
                }
            }
            var snapped = new bool[BaseCount];
            var extra = new List<float>();
            foreach (float r in roots)
            {
                int near = Mathf.RoundToInt(r / step);
                int bi = ((near % BaseCount) + BaseCount) % BaseCount;
                if (Mathf.Abs(r - near * step) < 0.25f * step && !snapped[bi]) { phis[bi] = near == BaseCount ? r - 2f * Mathf.PI : r; snapped[bi] = true; }
                else extra.Add(r);
            }
            foreach (float r in extra) phis.Add(r);
            phis.Sort();
            var refined = new List<float>();
            foreach (float r in phis) if (refined.Count == 0 || r - refined[refined.Count - 1] > 1e-4f) refined.Add(r);
            foreach (var phi in refined)
            {
                float ia = Cross(_in, phi), oa = Cross(_out, phi);
                Phi.Add(phi); Ia.Add(ia); Oa.Add(oa);
                IP.Add(G(phi, ia)); OP.Add(G(phi, oa));
            }
        }

        float Diff(int d, float phi)
        {
            switch (d)
            {
                case 0: return Cross(_in, phi) - M;
                case 1: return Cross(_in, phi) - Pf;
                case 2: return Cross(_out, phi) - M;
                default: return Cross(_out, phi) - Pf;
            }
        }

        /// <summary>Hole loop of the inner (inner = true) or outer skin, in its (u, z) chart.</summary>
        public SkinLoop Loop(bool inner)
        {
            var rr = inner ? _in : _out;
            var pts = inner ? IP : OP;
            var l = new SkinLoop();
            foreach (var p in pts)
            {
                l.P.Add(p);
                l.UV.Add(new Vector2(rr.Param(new Vector2(p.x, p.y)), p.z));
            }
            return l;
        }

        static St Inside(float a) => a < M ? St.C : (a <= Pf ? St.S : St.X);

        /// <summary>
        /// The cylinder surface: cabin-visible parts into cabin, exterior-visible parts into ext, pod rims into pod.
        /// </summary>
        public void EmitTube(MeshBuilder cabin, MeshBuilder ext, MeshBuilder pod)
        {
            int n = Phi.Count;
            var vals = new float[4];
            var idx = new int[4];
            for (int k = 0; k < n; k++)
            {
                int k1 = (k + 1) % n;
                float fa = Phi[k], fb = k1 == 0 ? Phi[0] + 2f * Mathf.PI : Phi[k1];
                float fm = 0.5f * (fa + fb);
                float im = Cross(_in, fm), om = Cross(_out, fm);
                vals[0] = im; vals[1] = om; vals[2] = M; vals[3] = Pf;
                for (int q = 0; q < 4; q++) idx[q] = q;
                System.Array.Sort(idx, (x, y) => vals[x] != vals[y] ? vals[x].CompareTo(vals[y]) : x.CompareTo(y));
                Vector3 ra = Radial(Phi[k]), rb = Radial(Phi[k1]);
                for (int j = 0; j < 3; j++)
                {
                    int lo = idx[j], hi = idx[j + 1];
                    float mid = 0.5f * (vals[lo] + vals[hi]);
                    St sin = Inside(mid);
                    St sout = mid < im ? St.C : (mid <= om ? St.S : St.X);
                    if (sin == sout) continue;
                    Vector3 a0 = At(k, lo), a1 = At(k, hi), b0 = At(k1, lo), b1 = At(k1, hi);
                    float va0 = AxialOf(k, lo), va1 = AxialOf(k, hi), vb0 = AxialOf(k1, lo), vb1 = AxialOf(k1, hi);
                    if (sin == St.S)
                    {
                        Span(pod, a0, a1, b1, b0, ra, rb, fa, fb, va0, va1, vb0, vb1);                      // pod rim, faces away
                    }
                    else if (sout == St.S)
                    {
                        Span(sin == St.C ? cabin : ext, a0, a1, b1, b0, -ra, -rb, fa, fb, va0, va1, vb0, vb1); // recess wall
                    }
                    else
                    {
                        // cabin meets exterior across the cylinder: two-sided collar
                        Span(sin == St.C ? cabin : ext, a0, a1, b1, b0, -ra, -rb, fa, fb, va0, va1, vb0, vb1);
                        Span(sout == St.C ? cabin : ext, a0, a1, b1, b0, ra, rb, fa, fb, va0, va1, vb0, vb1);
                    }
                }
            }
        }

        Vector3 At(int k, int name)
        {
            switch (name)
            {
                case 0: return IP[k];
                case 1: return OP[k];
                case 2: return G(Phi[k], M);
                default: return G(Phi[k], Pf);
            }
        }

        float AxialOf(int k, int name) => name == 0 ? Ia[k] : name == 1 ? Oa[k] : name == 2 ? M : Pf;

        void Span(MeshBuilder mb, Vector3 a0, Vector3 a1, Vector3 b1, Vector3 b0, Vector3 na, Vector3 nb,
            float fa, float fb, float va0, float va1, float vb0, float vb1)
        {
            B12Geo.Quad(mb, a0, a1, b1, b0, na, na, nb, nb,
                new Vector2(fa * R, va0), new Vector2(fa * R, va1), new Vector2(fb * R, vb1), new Vector2(fb * R, vb0));
        }

        /// <summary>
        /// The pod: cabin face with a bevel, the 0.80 m frame (raised 5 cm), the bevel into the 0.55 m bore, the bore,
        /// and the exterior face; lathed with exactly the tube's generator angles so the rims at M and P are shared.
        /// frame = cabin side and bore (Metal), outside = the exterior face (Ceramic); gold = the PaleGold ring.
        /// Profiles are (r, a) with the visible side on the left of travel (B12Geo.LatheAxis).
        /// </summary>
        public void EmitPod(MeshBuilder frame, MeshBuilder outside, MeshBuilder gold)
        {
            // cabin side: from the rim inward (a decreasing = toward the cabin), then down the bore to the exterior face
            var cabinProfile = new List<Vector2>
            {
                new Vector2(R, M), new Vector2(0.41f, M - 0.012f), new Vector2(0.40f, M - 0.012f),
                new Vector2(0.395f, M - 0.042f), new Vector2(0.385f, M - 0.050f), new Vector2(0.30f, M - 0.050f),
                new Vector2(0.29f, M - 0.042f), new Vector2(Ro, M - 0.010f), new Vector2(Ro, Pf - 0.008f),
                new Vector2(Ro + 0.008f, Pf)
            };
            var outsideProfile = new List<Vector2> { new Vector2(Ro + 0.008f, Pf), new Vector2(R, Pf) };
            // PaleGold ring: a half-round bead (centre r 0.319, half-width 19 mm, 14 mm proud of the frame top at M - 0.050,
            // edges sunk 2 mm into it) with smooth normals, so the glossy gold catches the strip lights and the bright bore
            // as a highlight line from the KS01 eye instead of reading as a flat brown band
            var goldProfile = new List<Vector2>();
            const int beadSeg = 10;
            for (int k = 0; k <= beadSeg; k++)
            {
                float th = Mathf.PI * k / beadSeg;              // outer edge (r max) -> crown -> inner edge
                goldProfile.Add(new Vector2(0.319f + 0.019f * Mathf.Cos(th), M - 0.048f - 0.014f * Mathf.Sin(th)));
            }
            // visible side = left of travel in (r, a): the cabin face is listed with r decreasing and a decreasing
            // (left = -a = toward the cabin); the frame flanks, the bore (left = -r) and the exterior face (left = +a)
            // follow from the same order.
            var ang = Phi;
            B12Geo.LatheAxis(frame, C, N, E1, E2, cabinProfile, ang);
            B12Geo.LatheAxis(outside, C, N, E1, E2, outsideProfile, ang);
            B12Geo.LatheAxis(gold, C, N, E1, E2, goldProfile, ang, true, true);
        }
    }
}
