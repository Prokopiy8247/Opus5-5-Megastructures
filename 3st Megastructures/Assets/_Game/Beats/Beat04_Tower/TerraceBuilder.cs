// TerraceBuilder: the terrace slab of beat 4 (MASTER_PROMPT 14.5.4 (a), 14.12 "Terrace", 8.7).
// A chamfered slab x in [-60, 190], z in [320, 520], top y = 36, walls down to y = -4 (into the salt;
// the bottom face is never visible and is omitted). Part of the sinking ground group.
//
// Two meshes that share every boundary vertex (watertight across meshes, same transform):
//  * Top (Terrace material, mandala M04): the flat top face. Around the cable axis it is a polar mesh
//    (144 segments x 9 rings, r = 49..66 m) carrying a soft vertex-AO ring at the cable foot
//    (r 50 -> 64 m); the region r < 49 is inside the cable (r >= 50 at every height) and left open.
//    Outside r = 66 m the top is closed by an angle zipper to the inset rectangle, whose edges are
//    subdivided (<= 50 m) so every zipper triangle stays outside the polar region (visibility).
//    The front edge carries vertices at x = +-20 (the stair edges) and has NO vertex in between, so
//    the stair's top tread abuts it on one shared edge (see StairBuilder header).
//  * Sides (Stringer material = M04 without the mandala, so no mandala lines appear on vertical
//    faces): 45-degree chamfer strips on the 4 top edges, 4 corner triangles, 4 vertical corner
//    chamfers and the 4 walls, all on the same column lists as the top face's boundary. The walls carry
//    a ground-contact AO gradient (local y 0 -> 2.5 m).
// Normals are flat per face; tangent = (normal, 1); UV0 = (x, z) on the top, (along, y) on the sides;
// vertex colour = (AO, 1, 0, 1).
//
// A third mesh is the slab's flat ground shadow (critique run06 A2): once the ground sinks in beat 5 the terrace
// is far beyond the 160 m shadow distance and cast no shadow on the salt, the strongest height cue of the "land
// drops away" frames. Like the colossus shadows (14.5.2 f) it is a flat mesh 0.03 m above the salt in ground-group
// coordinates (it sinks with the group), drawn with SaltShadow (shade tone, alpha = vertex A, queue Geometry+2,
// ZWrite off, Offset -2,-2), so where the shadow map also shades the salt (beat 4) the two never darken twice. The
// outline is the projection of the slab along -S of the beat-5 sun (ThreadMath.SunDirection(450), e = 21.9 deg,
// within 0.2 deg of the beat-4 sun): the convex hull of the footprint and of the top outline (y = 36) moved 89.6 m
// along the shadow, i.e. a hexagon reaching 73 m in front of the front wall and 51 m beyond the right wall. Its soft edge is 0.35 m
// at the wall foot and widens with the distance back to the slab like the colossus penumbra (1.1 m on the far
// edges); the hull edges that run along the two sunlit walls are moved half a contact penumbra into the footprint,
// so their ramp ends exactly at the wall foot and the lit salt is never darkened. Inside the footprint the shadow
// is hidden under the slab.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    public static class TerraceBuilder
    {
        /// <summary>Slab chamfer (14.5.4 line 693: "Slab (chamfered box, 0.05 m chamfer)").</summary>
        public const double Chamfer = 0.05;
        /// <summary>Angular segments of the polar region around the cable foot.</summary>
        public const int FootSegments = 144;
        /// <summary>Ring radii of the polar region (m from the cable axis). 49 is inside the cable (r >= 50).</summary>
        public static readonly double[] FootRings = { 49.0, 50.0, 51.0, 52.5, 54.5, 57.0, 60.0, 63.0, 66.0 };
        /// <summary>Foot AO: AO = lerp(FootAoMin, 1, smoothstep(FootAoR0, FootAoR1, r)).</summary>
        public const double FootAoMin = 0.60, FootAoR0 = 50.0, FootAoR1 = 64.0;
        /// <summary>Wall ground-contact AO rows (local y; the salt is at y = 0).</summary>
        public const double WallAoMin = 0.70, WallAoTop = 2.5;
        /// <summary>Maximum length of a boundary edge of the top face (zipper visibility, see header).</summary>
        public const double MaxEdge = 50.0;

        // exact coordinates, computed once by one expression each (shared by both meshes and by StairBuilder)
        public static float JointZ => (float)(ThreadConstants.TerraceZMin + Chamfer);   // top front edge = stair top-tread back edge
        public static float XA => (float)(ThreadConstants.TerraceXMin + Chamfer);
        public static float XB => (float)(ThreadConstants.TerraceXMax - Chamfer);
        public static float ZB => (float)(ThreadConstants.TerraceZMax - Chamfer);
        public static float YC => (float)(ThreadConstants.TerraceY - Chamfer);

        static Color Col(double ao) => new Color((float)ao, 1f, 0f, 1f);

        public static double FootAo(double r) => ThreadMath.Lerp(FootAoMin, 1.0, ThreadMath.Smoothstep(FootAoR0, FootAoR1, r));

        /// <summary>x columns of the front edge: [XA .. -20] [-20 .. 20] (one edge, the stair joint) [20 .. XB].</summary>
        public static float[] FrontColumns()
        {
            var l = new List<float>(8);
            Subdivide(l, XA, StairBuilder.X0, true);
            l.Add(StairBuilder.X1);
            Subdivide(l, StairBuilder.X1, XB, false);
            return l.ToArray();
        }
        /// <summary>x columns of the back edge.</summary>
        public static float[] BackColumns() { var l = new List<float>(8); Subdivide(l, XA, XB, true); return l.ToArray(); }
        /// <summary>z rows of the left and right edges.</summary>
        public static float[] SideRows() { var l = new List<float>(8); Subdivide(l, JointZ, ZB, true); return l.ToArray(); }

        /// <summary>Appends a (optional), the interior points and b of [a, b] split into segments of at most MaxEdge.</summary>
        static void Subdivide(List<float> l, float a, float b, bool includeA)
        {
            int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(b - a) / MaxEdge - 1e-9));
            if (includeA) l.Add(a);
            for (int k = 1; k < n; k++) l.Add((float)(a + (double)(b - a) * k / n));
            l.Add(b);
        }

        // ------------------------------------------------------------------ top face
        public static MeshBuilder BuildTop()
        {
            float y = (float)ThreadConstants.TerraceY;
            double cx = ThreadConstants.CableAxisX, cz = ThreadConstants.CableAxisZ;
            int rings = FootRings.Length;
            var mb = new MeshBuilder(rings * FootSegments + 64);
            Vector3 up = Vector3.up;
            Vector4 tUp = new Vector4(0f, 1f, 0f, 1f);

            // polar rings around the cable foot
            for (int i = 0; i < rings; i++)
            {
                double r = FootRings[i];
                Color colour = Col(FootAo(r));
                for (int j = 0; j < FootSegments; j++)
                {
                    double a = j * (2.0 * Math.PI / FootSegments);
                    var p = new Vector3((float)(cx + r * Math.Sin(a)), y, (float)(cz + r * Math.Cos(a)));
                    mb.Add(p, up, new Vector2(p.x, p.z), colour, tUp, Vector4.zero);
                }
            }
            for (int i = 0; i < rings - 1; i++)
            {
                int bA = i * FootSegments, bB = (i + 1) * FootSegments;
                for (int j = 0; j < FootSegments; j++)
                {
                    int jn = (j + 1) % FootSegments;
                    TriN(mb, bA + j, bA + jn, bB + j, up);
                    TriN(mb, bA + jn, bB + jn, bB + j, up);
                }
            }

            // outer boundary (the inset rectangle on the shared column lists)
            float[] front = FrontColumns(), back = BackColumns(), side = SideRows();
            var outer = new List<Vector3>(32);
            for (int k = 0; k < front.Length; k++) outer.Add(new Vector3(front[k], y, JointZ));
            for (int k = 1; k < side.Length; k++) outer.Add(new Vector3(XB, y, side[k]));
            for (int k = back.Length - 2; k >= 0; k--) outer.Add(new Vector3(back[k], y, ZB));
            for (int k = side.Length - 2; k >= 1; k--) outer.Add(new Vector3(XA, y, side[k]));
            int outerStart = mb.VertexCount;
            for (int k = 0; k < outer.Count; k++)
                mb.Add(outer[k], up, new Vector2(outer[k].x, outer[k].z), Col(1.0), tUp, Vector4.zero);

            // zipper between the outermost polar ring and the rectangle (both sorted by angle)
            int innerStart = (rings - 1) * FootSegments;
            var innerIdx = new int[FootSegments];
            var innerAng = new double[FootSegments];
            for (int j = 0; j < FootSegments; j++) { innerIdx[j] = innerStart + j; innerAng[j] = j * (2.0 * Math.PI / FootSegments); }
            var outerIdx = new int[outer.Count];
            var outerAng = new double[outer.Count];
            for (int k = 0; k < outer.Count; k++)
            {
                outerIdx[k] = outerStart + k;
                outerAng[k] = Wrap2Pi(Math.Atan2(outer[k].x - cx, outer[k].z - cz));
            }
            Array.Sort(outerAng, outerIdx);
            ZipperByAngle(mb, innerIdx, innerAng, outerIdx, outerAng, up);
            return mb;
        }

        // ------------------------------------------------------------------ chamfers and walls
        public static MeshBuilder BuildSides()
        {
            float X0 = (float)ThreadConstants.TerraceXMin, X1 = (float)ThreadConstants.TerraceXMax;
            float Z0 = (float)ThreadConstants.TerraceZMin, Z1 = (float)ThreadConstants.TerraceZMax;
            float Y1 = (float)ThreadConstants.TerraceY, Yc = YC;
            float[] rows = { (float)ThreadConstants.TerraceBottomY, 0f, (float)WallAoTop, Yc };
            double[] rowAo = { WallAoMin, WallAoMin, 1.0, 1.0 };
            float[] front = FrontColumns(), back = BackColumns(), side = SideRows();
            var mb = new MeshBuilder(1024);
            float s = Mathf.Sqrt(0.5f);

            // top-edge chamfer strips on the shared columns (their top edges are the top face's boundary)
            Vector3 nF = new Vector3(0f, s, -s), nB = new Vector3(0f, s, s), nL = new Vector3(-s, s, 0f), nR = new Vector3(s, s, 0f);
            for (int k = 0; k < front.Length - 1; k++)
                Quad(mb, new Vector3(front[k], Yc, Z0), new Vector3(front[k], Y1, JointZ), new Vector3(front[k + 1], Y1, JointZ), new Vector3(front[k + 1], Yc, Z0), nF, 1.0, 1.0);
            for (int k = 0; k < back.Length - 1; k++)
                Quad(mb, new Vector3(back[k], Yc, Z1), new Vector3(back[k], Y1, ZB), new Vector3(back[k + 1], Y1, ZB), new Vector3(back[k + 1], Yc, Z1), nB, 1.0, 1.0);
            for (int k = 0; k < side.Length - 1; k++)
            {
                Quad(mb, new Vector3(X0, Yc, side[k]), new Vector3(XA, Y1, side[k]), new Vector3(XA, Y1, side[k + 1]), new Vector3(X0, Yc, side[k + 1]), nL, 1.0, 1.0);
                Quad(mb, new Vector3(X1, Yc, side[k]), new Vector3(XB, Y1, side[k]), new Vector3(XB, Y1, side[k + 1]), new Vector3(X1, Yc, side[k + 1]), nR, 1.0, 1.0);
            }

            // corner triangles and vertical corner chamfers
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = corner == 1 || corner == 2, backSide = corner >= 2;
                float Xc = right ? X1 : X0, Zc = backSide ? Z1 : Z0;          // outer corner
                float Xi = right ? XB : XA, Zi = backSide ? ZB : JointZ;      // inset coordinates (exact shared values)
                float ix = right ? -1f : 1f, iz = backSide ? -1f : 1f;        // inward directions
                Vector3 p1 = new Vector3(Xc, Yc, Zi), p2 = new Vector3(Xi, Y1, Zi), p3 = new Vector3(Xi, Yc, Zc);
                Vector3 nc = new Vector3(-ix, 1f, -iz).normalized;
                int i1 = Vert(mb, p1, nc, 1.0), i2 = Vert(mb, p2, nc, 1.0), i3 = Vert(mb, p3, nc, 1.0);
                TriN(mb, i1, i2, i3, nc);
                Vector3 nv = new Vector3(-ix, 0f, -iz).normalized;
                for (int r = 0; r < rows.Length - 1; r++)
                    Quad(mb, new Vector3(Xi, rows[r], Zc), new Vector3(Xi, rows[r + 1], Zc),
                         new Vector3(Xc, rows[r + 1], Zi), new Vector3(Xc, rows[r], Zi), nv, rowAo[r], rowAo[r + 1]);
            }

            // walls (rows give the ground-contact AO)
            for (int r = 0; r < rows.Length - 1; r++)
            {
                for (int k = 0; k < front.Length - 1; k++)
                    Quad(mb, new Vector3(front[k], rows[r], Z0), new Vector3(front[k], rows[r + 1], Z0),
                         new Vector3(front[k + 1], rows[r + 1], Z0), new Vector3(front[k + 1], rows[r], Z0), Vector3.back, rowAo[r], rowAo[r + 1]);
                for (int k = 0; k < back.Length - 1; k++)
                    Quad(mb, new Vector3(back[k], rows[r], Z1), new Vector3(back[k], rows[r + 1], Z1),
                         new Vector3(back[k + 1], rows[r + 1], Z1), new Vector3(back[k + 1], rows[r], Z1), Vector3.forward, rowAo[r], rowAo[r + 1]);
                for (int k = 0; k < side.Length - 1; k++)
                {
                    Quad(mb, new Vector3(X0, rows[r], side[k]), new Vector3(X0, rows[r + 1], side[k]),
                         new Vector3(X0, rows[r + 1], side[k + 1]), new Vector3(X0, rows[r], side[k + 1]), Vector3.left, rowAo[r], rowAo[r + 1]);
                    Quad(mb, new Vector3(X1, rows[r], side[k]), new Vector3(X1, rows[r + 1], side[k]),
                         new Vector3(X1, rows[r + 1], side[k + 1]), new Vector3(X1, rows[r], side[k + 1]), Vector3.right, rowAo[r], rowAo[r + 1]);
                }
            }
            return mb;
        }

        // ------------------------------------------------------------------ flat ground shadow (critique run06 A2)
        /// <summary>Progress whose sun casts the ground shadow: beat 5 (critique A2; e = 21.9 deg, within 0.2 deg of the beat-4 sun, 14.2.4).</summary>
        public const double ShadowSunS = 450.0;
        /// <summary>Soft edge as the colossus shadows (14.5.2 f): 0.35 m at the contact, widening (6 - 0.35) m per 674 m of shadow.</summary>
        public const double ShadowPenumbra0 = 0.35, ShadowPenumbraGrowth = (6.0 - 0.35) / 674.0;
        /// <summary>Longest outline segment of the soft ring (the soft edge of a slanted side widens linearly along it; 10 m keeps the 0.25 / 0.75 contours within 1 cm of straight).</summary>
        public const double ShadowMaxSegment = 10.0;
        /// <summary>Footprint tolerance of the walk back toward the sun (the slanted sides graze the slab corners exactly).</summary>
        const double FootprintEps = 1e-6;

        /// <summary>Unit vector toward the sun that casts the ground shadow.</summary>
        public static D3 ShadowSun() => ThreadMath.SunDirection(ShadowSunS);

        /// <summary>Horizontal offset from the slab's top outline (y = 36) to its projection along -S on the shadow plane (y = 0.03).</summary>
        public static void ShadowOffset(out double ox, out double oz)
        {
            D3 sun = ShadowSun();
            double k = (ThreadConstants.TerraceY - ThreadConstants.ColossusShadowY) / Math.Max(1e-6, sun.y);
            ox = -sun.x * k;
            oz = -sun.z * k;
        }

        /// <summary>Soft-edge width at a point of the shadow outline: grows with the distance walked back toward the sun to the slab footprint (0 on the footprint, at most the full projection).</summary>
        public static double ShadowPenumbra(double x, double z)
        {
            ShadowOffset(out double ox, out double oz);
            double len = Math.Max(1e-9, Math.Sqrt(ox * ox + oz * oz));
            double u = Math.Min(len, DistanceToFootprint(x, z, -ox / len, -oz / len));
            return ShadowPenumbra0 + ShadowPenumbraGrowth * u;
        }

        /// <summary>Distance along the unit horizontal direction (dx, dz) from (x, z) to the slab footprint (0 inside, MaxValue if the ray misses it).</summary>
        static double DistanceToFootprint(double x, double z, double dx, double dz)
        {
            double t0 = 0.0, t1 = double.MaxValue;
            if (!Slab(x, dx, ThreadConstants.TerraceXMin - FootprintEps, ThreadConstants.TerraceXMax + FootprintEps, ref t0, ref t1)) return double.MaxValue;
            if (!Slab(z, dz, ThreadConstants.TerraceZMin - FootprintEps, ThreadConstants.TerraceZMax + FootprintEps, ref t0, ref t1)) return double.MaxValue;
            return t0;
        }

        static bool Slab(double p, double d, double lo, double hi, ref double t0, ref double t1)
        {
            if (Math.Abs(d) < 1e-12) return p >= lo && p <= hi;
            double a = (lo - p) / d, b = (hi - p) / d;
            if (a > b) { double t = a; a = b; b = t; }
            t0 = Math.Max(t0, a);
            t1 = Math.Min(t1, b);
            return t0 <= t1;
        }

        static bool IsFootprintCorner(D3 p)
        {
            return (p.x == ThreadConstants.TerraceXMin || p.x == ThreadConstants.TerraceXMax) && (p.z == ThreadConstants.TerraceZMin || p.z == ThreadConstants.TerraceZMax);
        }

        static double CrossXZ(D3 o, D3 a, D3 b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);

        /// <summary>Convex hull in (x, z), counter-clockwise seen from above (Andrew's monotone chain, collinear points removed).</summary>
        static List<D3> HullXZ(List<D3> input)
        {
            var p = new List<D3>(input);
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            int n = p.Count, k = 0;
            var h = new D3[2 * n];
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && CrossXZ(h[k - 2], h[k - 1], p[i]) <= 1e-9) k--;
                h[k++] = p[i];
            }
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && CrossXZ(h[k - 2], h[k - 1], p[i]) <= 1e-9) k--;
                h[k++] = p[i];
            }
            var r = new List<D3>(k);
            for (int i = 0; i < k - 1; i++) r.Add(h[i]);
            return r;
        }

        /// <summary>
        /// Geometric outline (the alpha 0.5 contour) of the ground shadow, counter-clockwise seen from above, with the
        /// outward unit normal of the edge that starts at each point and the soft-edge width there: the hull of the
        /// footprint and of the projected top outline, the hull edges on a sunlit wall moved ShadowPenumbra0 / 2 into
        /// the footprint, every edge split into segments of at most ShadowMaxSegment. A corner on a wall edge takes the
        /// contact width ShadowPenumbra0, so its outer offset lands exactly on the wall foot. y = ColossusShadowY (the
        /// salt-shadow plane, 14.5.2 f).
        /// </summary>
        public static void ShadowOutline(List<D3> points, List<D3> edgeNormals, List<double> penumbra)
        {
            points.Clear();
            edgeNormals.Clear();
            penumbra.Clear();
            ShadowOffset(out double ox, out double oz);
            double y = ThreadConstants.ColossusShadowY;
            double x0 = ThreadConstants.TerraceXMin, x1 = ThreadConstants.TerraceXMax, z0 = ThreadConstants.TerraceZMin, z1 = ThreadConstants.TerraceZMax;
            var corners = new List<D3>(8)
            {
                new D3(x0, y, z0), new D3(x1, y, z0), new D3(x1, y, z1), new D3(x0, y, z1),
                new D3(x0 + ox, y, z0 + oz), new D3(x1 + ox, y, z0 + oz), new D3(x1 + ox, y, z1 + oz), new D3(x0 + ox, y, z1 + oz),
            };
            var hull = HullXZ(corners);
            int m = hull.Count;
            // edge lines (point, unit direction, outward normal); edges between two footprint corners lie on a sunlit wall
            var lp = new D3[m];
            var ld = new D3[m];
            var ln = new D3[m];
            var onWall = new bool[m];
            for (int i = 0; i < m; i++)
            {
                D3 a = hull[i], b = hull[(i + 1) % m];
                D3 d = new D3(b.x - a.x, 0.0, b.z - a.z).Normalized;
                D3 nrm = new D3(d.z, 0.0, -d.x);
                bool wall = IsFootprintCorner(a) && IsFootprintCorner(b);
                lp[i] = wall ? a - nrm * (0.5 * ShadowPenumbra0) : a;
                onWall[i] = wall;
                ld[i] = d;
                ln[i] = nrm;
            }
            // corners: intersections of consecutive edge lines
            var g = new D3[m];
            for (int i = 0; i < m; i++)
            {
                int pi = (i + m - 1) % m;
                D3 p1 = lp[pi], d1 = ld[pi], p2 = lp[i], d2 = ld[i];
                double den = d1.x * d2.z - d1.z * d2.x;
                double t = Math.Abs(den) < 1e-12 ? 0.0 : ((p2.x - p1.x) * d2.z - (p2.z - p1.z) * d2.x) / den;
                g[i] = new D3(p1.x + d1.x * t, y, p1.z + d1.z * t);
            }
            for (int i = 0; i < m; i++)
            {
                D3 a = g[i], b = g[(i + 1) % m];
                double len = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
                int seg = Math.Max(1, (int)Math.Ceiling(len / ShadowMaxSegment - 1e-9));
                for (int j = 0; j < seg; j++)
                {
                    double f = j / (double)seg;
                    D3 q = new D3(a.x + (b.x - a.x) * f, y, a.z + (b.z - a.z) * f);
                    points.Add(q);
                    edgeNormals.Add(ln[i]);
                    bool wallCorner = onWall[i] || (j == 0 && onWall[(i + m - 1) % m]);
                    penumbra.Add(wallCorner ? ShadowPenumbra0 : ShadowPenumbra(q.x, q.z));
                }
            }
        }

        static Color ShadowCol(double alpha) => new Color(1f, 1f, 0f, (float)alpha);   // vertex data: AO 1, height shade 1, no sway, coverage

        /// <summary>
        /// The flat ground shadow of the slab (see the header): a soft ring of three loops (outer alpha 0 and inner
        /// alpha 1, offset +-penumbra / 2 along the mitred edge normals, and the geometric outline itself at alpha 0.5,
        /// so the 0.5 contour lies exactly on the projected outline even where the soft edge widens) and a fan of the
        /// inner polygon around its centroid. Every triangle faces up; UV0 = (x, z); ground-group local coordinates.
        /// </summary>
        public static MeshBuilder BuildGroundShadow()
        {
            var pts = new List<D3>(160);
            var nrm = new List<D3>(160);
            var pen = new List<double>(160);
            ShadowOutline(pts, nrm, pen);
            int n = pts.Count;
            var mb = new MeshBuilder(3 * n + 1);
            Vector3 up = Vector3.up;
            var tan = new Vector4(1f, 0f, 0f, 1f);
            var outer = new int[n];
            var mid = new int[n];
            var inner = new int[n];
            double cx = 0.0, cz = 0.0;
            for (int i = 0; i < n; i++)
            {
                D3 p = pts[i];
                D3 n1 = nrm[(i + n - 1) % n], n2 = nrm[i];
                double k = 1.0 / Math.Max(1e-6, 1.0 + n1.x * n2.x + n1.z * n2.z);
                D3 mit = new D3((n1.x + n2.x) * k, 0.0, (n1.z + n2.z) * k);   // offset 1 from both edge lines
                double h = 0.5 * pen[i];
                D3 o = p + mit * h, q = p - mit * h;
                outer[i] = mb.Add(BeatMeshUtil.V(o), up, new Vector2((float)o.x, (float)o.z), ShadowCol(0.0), tan, Vector4.zero);
                mid[i] = mb.Add(BeatMeshUtil.V(p), up, new Vector2((float)p.x, (float)p.z), ShadowCol(0.5), tan, Vector4.zero);
                inner[i] = mb.Add(BeatMeshUtil.V(q), up, new Vector2((float)q.x, (float)q.z), ShadowCol(1.0), tan, Vector4.zero);
                cx += q.x; cz += q.z;
            }
            cx /= Math.Max(1, n); cz /= Math.Max(1, n);
            var c = new Vector3((float)cx, (float)ThreadConstants.ColossusShadowY, (float)cz);
            int centre = mb.Add(c, up, new Vector2(c.x, c.z), ShadowCol(1.0), tan, Vector4.zero);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                TriN(mb, outer[i], outer[j], mid[j], up);
                TriN(mb, outer[i], mid[j], mid[i], up);
                TriN(mb, mid[i], mid[j], inner[j], up);
                TriN(mb, mid[i], inner[j], inner[i], up);
                TriN(mb, centre, inner[i], inner[j], up);
            }
            return mb;
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Quad a, b, c, d in boundary order; a, d carry aoLow and b, c aoHigh.</summary>
        static void Quad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, double aoLow, double aoHigh)
        {
            int ia = Vert(mb, a, nrm, aoLow), ib = Vert(mb, b, nrm, aoHigh), ic = Vert(mb, c, nrm, aoHigh), id = Vert(mb, d, nrm, aoLow);
            TriN(mb, ia, ib, ic, nrm);
            TriN(mb, ia, ic, id, nrm);
        }

        static int Vert(MeshBuilder mb, Vector3 p, Vector3 nrm, double ao)
        {
            Vector2 uv = Mathf.Abs(nrm.x) > Mathf.Abs(nrm.z) ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            return mb.Add(p, nrm, uv, Col(ao), new Vector4(nrm.x, nrm.y, nrm.z, 1f), Vector4.zero);
        }

        internal static void TriN(MeshBuilder mb, int a, int b, int c, Vector3 nrm)
        {
            Vector3 fn = Vector3.Cross(mb.Positions[b] - mb.Positions[a], mb.Positions[c] - mb.Positions[a]);
            if (Vector3.Dot(fn, nrm) >= 0f) mb.Tri(a, b, c); else mb.Tri(a, c, b);
        }

        internal static double Wrap2Pi(double a)
        {
            double t = 2.0 * Math.PI;
            a -= t * Math.Floor(a / t);
            return a >= t ? a - t : a;
        }

        static double WrapPi(double a)
        {
            double t = 2.0 * Math.PI;
            return a - t * Math.Floor((a + Math.PI) / t);
        }

        /// <summary>
        /// Watertight zipper between two closed loops around a common centre (inner loop a, outer loop b),
        /// given as vertex indices with ascending angles in [0, 2 pi). Each inner vertex connects to the
        /// angularly nearest outer vertices (the switch happens at the angular midpoint between two outer
        /// vertices), which keeps every edge outside the convex inner loop when the outer edges are short
        /// enough (MaxEdge). Emits na + nb triangles oriented along nrm (R3 3.4, MASTER_PROMPT 8.7).
        /// </summary>
        internal static void ZipperByAngle(MeshBuilder mb, int[] a, double[] angA, int[] b, double[] angB, Vector3 nrm)
        {
            int na = a.Length, nb = b.Length;
            int j0 = 0; double best = double.MaxValue;
            for (int j = 0; j < nb; j++)
            {
                double d = Math.Abs(WrapPi(angB[j] - angA[0]));
                if (d < best) { best = d; j0 = j; }
            }
            var B = new double[nb + 1];
            B[0] = angA[0] + WrapPi(angB[j0] - angA[0]);
            for (int j = 1; j < nb; j++) B[j] = B[j - 1] + Wrap2Pi(angB[(j0 + j) % nb] - angB[(j0 + j - 1) % nb]);
            B[nb] = B[0] + 2.0 * Math.PI;
            var A = new double[na + 1];
            for (int i = 0; i < na; i++) A[i] = angA[0] + Wrap2Pi(angA[i] - angA[0]);
            A[na] = angA[0] + 2.0 * Math.PI;
            int ia = 0, ib = 0;
            while (ia < na || ib < nb)
            {
                bool advanceA = ib >= nb || (ia < na && A[ia + 1] <= 0.5 * (B[ib] + B[ib + 1]));
                int va = a[ia % na], vb = b[(j0 + ib) % nb];
                if (advanceA) { TriN(mb, va, vb, a[(ia + 1) % na], nrm); ia++; }
                else { TriN(mb, va, vb, b[(j0 + ib + 1) % nb], nrm); ib++; }
            }
        }
    }
}
