// ColossusShadowShape: the flat ground shadow of one colossus (MASTER_PROMPT 14.5.2 f), pure double precision.
// The silhouette (robe envelope rings, hood rings after the head tilt, the two sleeves as bars) is projected along
// -S (the beat-2 sun ThreadMath.SunDirection(s) of beat 2: elevation 24 deg, azimuth -35 deg) onto the plane
// y = ColossusShadowY. Each part becomes a strip of rows: per row the two extreme points across the strip (the
// geometric outline, alpha 0.5) plus a soft edge whose width grows from 0.35 m at the foot to 6 m at 674 m (661 f),
// plus a quadratic term toward the far end (critique run06 C-05 / E8; vertex alpha ramps, inner and outer rows). The
// shade also fades to zero over the last 40 percent of each shadow's length, so no shadow ends in a hard line near the
// horizon; the geometric outlines (the T-SHADE coverage polygons) are unchanged by both. The robe + hood strip takes
// one row per projected envelope / hood ring, dense near the ground so the z fade (alpha 1 -> 0 between z = 210 and
// z = 240) and the path-axis crossing are represented, and ends in the shadow of the hood point that casts farthest;
// the two sleeve bars (closed cuffs included) are cut by scanlines across the shadow direction (see SleeveRows),
// which cannot fold. The material (SaltShadow) draws the shade tone with alpha = vertex A, so overlapping shadows
// never get darker than the terminator shade (14.2.2 step 3).
using System;
using System.Collections.Generic;

namespace Megastructure.Beats
{
    public struct V2
    {
        public double x, z;
        public V2(double x, double z) { this.x = x; this.z = z; }
        public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.z + b.z);
        public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.z - b.z);
        public static V2 operator *(V2 a, double k) => new V2(a.x * k, a.z * k);
        public static double Dot(V2 a, V2 b) => a.x * b.x + a.z * b.z;
        public double Length => Math.Sqrt(x * x + z * z);
        public override string ToString() => $"({x:F3}, {z:F3})";
    }

    public sealed class ColossusShadowShape
    {
        /// <summary>Vertices in world XZ with their alpha; triangles index them (counter-clockwise seen from above is NOT implied; see Up()).</summary>
        public readonly List<V2> Vertices = new List<V2>();
        public readonly List<double> Alpha = new List<double>();
        public readonly List<int> Triangles = new List<int>();
        /// <summary>Geometric outlines (alpha 0.5 contours) of the strips as closed polygons, for the shade-band test.</summary>
        public readonly List<V2[]> Outlines = new List<V2[]>();
        /// <summary>Tip of the main strip (projection of the farthest-casting hood point) and the analytic projection along -S.</summary>
        public V2 Tip, AnalyticTip;
        /// <summary>The hood point (world) that casts the tip.</summary>
        public D3 TipPoint;
        /// <summary>Shadow length from the foot to the tip along -S (m); the length fade runs over its last 40 percent.</summary>
        public double Length;
        /// <summary>Triangles whose orientation disagreed with their strip (folds; must be 0).</summary>
        public int Folds;
        /// <summary>Centroids of folded triangles (diagnostics).</summary>
        public readonly List<V2> FoldCentroids = new List<V2>();
        public D3 Sun;

        /// <summary>Unit vector toward the sun used for every colossus shadow (beat 2).</summary>
        public static D3 ShadowSun() => ThreadMath.SunDirection(ColossusSpec.ShadowSunS);

        public static V2 Project(D3 p, D3 sun)
        {
            double k = (p.y - ThreadConstants.ColossusShadowY) / Math.Max(1e-6, sun.y);
            return new V2(p.x - sun.x * k, p.z - sun.z * k);
        }

        struct Row
        {
            public V2 L, R;       // geometric outline points (alpha 0.5)
            public double P;      // soft edge width
            public bool Cap;      // all-zero alpha row (soft end cap)
        }

        public static ColossusShadowShape Build(ColossusShape c)
        {
            var sh = new ColossusShadowShape();
            D3 sun = ShadowSun();
            sh.Sun = sun;
            var d = new V2(-sun.x, -sun.z);
            d = d * (1.0 / Math.Max(1e-9, d.Length));
            var perp = new V2(d.z, -d.x);
            var foot = new V2(c.Cx, c.Cz);

            // ---------------------------------------------------------------- main strip (robe + hood)
            var rows = new List<Row>();
            var ring = new List<V2>(360);
            double neck = ColossusSpec.ShoulderEta[2];
            var etas = new List<double>();
            for (double e = 0; e <= 80.0 + 1e-9; e += 5.0) etas.Add(e);
            for (double e = 96; e < ColossusSpec.ShoulderEta[0] - 1e-9; e += 16.0) etas.Add(e);
            etas.Add(ColossusSpec.ShoulderEta[0]);
            etas.Add(ColossusSpec.ShoulderEta[1] - 6.0);
            etas.Add(ColossusSpec.ShoulderEta[1]);
            etas.Add(neck);
            foreach (double e in etas)
            {
                ring.Clear();
                for (int i = 0; i < 360; i++)
                {
                    double th = 2.0 * Math.PI * i / 360.0;
                    ring.Add(Project(c.LocalToWorld(c.RobeLocal(th, e, true)), sun));
                }
                rows.Add(MakeRow(ring, perp, foot, d));
            }
            double[] hoodT = { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.4, 4.75 };
            foreach (double t in hoodT)
            {
                ring.Clear();
                for (int i = 0; i < 180; i++)
                {
                    double th = 2.0 * Math.PI * i / 180.0;
                    ring.Add(Project(c.LocalToWorld(c.Bend(c.HoodLocalUnbent(th, t))), sun));
                }
                rows.Add(MakeRow(ring, perp, foot, d));
            }
            // the tip: the hood point whose shadow falls farthest along -S (with the forward bow of the head this is a
            // point on the back of the hood crown, not exactly the apex)
            D3 top = FarthestHoodPoint(c, sun, d);
            V2 tip = Project(top, sun);
            rows.Add(new Row { L = tip, R = tip, P = Penumbra(tip, foot, d) });
            sh.Tip = tip;
            sh.AnalyticTip = AnalyticProjection(top, sun);
            sh.TipPoint = top;
            double length = V2.Dot(tip - foot, d);
            sh.Length = length;
            sh.AddStrip(rows, 6, perp, foot, d, length);

            // ---------------------------------------------------------------- sleeves as bars
            for (int si = 0; si < c.Sleeves.Length; si++)
                sh.AddStrip(SleeveRows(c, c.Sleeves[si], sun, foot, d, perp), SleeveAcross, perp, foot, d, length);
            return sh;
        }

        const int TipSamplesT = 100, TipSamplesTheta = 360;
        const double TipT0 = 3.0;

        /// <summary>World point of the bent hood (t in [TipT0, 5], dense sampling) whose projection along -S lies farthest along d.</summary>
        static D3 FarthestHoodPoint(ColossusShape c, D3 sun, V2 d)
        {
            D3 best = c.ApexWorld;
            double bestU = V2.Dot(Project(best, sun), d);
            for (int i = 0; i <= TipSamplesT; i++)
            {
                double t = TipT0 + (5.0 - TipT0) * i / TipSamplesT;
                for (int j = 0; j < TipSamplesTheta; j++)
                {
                    D3 p = c.LocalToWorld(c.Bend(c.HoodLocalUnbent(2.0 * Math.PI * j / TipSamplesTheta, t)));
                    double u = V2.Dot(Project(p, sun), d);
                    if (u > bestU) { bestU = u; best = p; }
                }
            }
            return best;
        }

        // Sleeve bars are cut by scanlines: rows perpendicular to the shadow direction d at stations strictly
        // increasing along d, each spanning the lateral extent of the projected tube at that station. The tube between
        // two consecutive loft rings is the convex hull of their projections (exact for straight tube segments), so a
        // row can never fall behind its neighbour and the strip cannot fold, whatever the sleeve slant or bend
        // (rows taken per loft section fold at the slanted shoulder attachment and the dome).
        const int SleeveStations = 16, SleeveAcross = 4, SleeveRingSamples = 48;

        static List<Row> SleeveRows(ColossusShape c, SleeveShape s, D3 sun, V2 foot, V2 d, V2 perp)
        {
            // project every loft ring (dome apex .. cuff) to (u along d, v along perp) relative to the foot
            int nr = s.RingCount;
            var proj = new List<V2>[nr];
            for (int i = 0; i < nr; i++)
            {
                proj[i] = new List<V2>(SleeveRingSamples);
                s.Ring(i, out _, out double radius, out _, out _);
                int samples = radius > 0.0 ? SleeveRingSamples : 1;   // ring 0 (shoulder dome apex) and the cuff pole are points
                for (int k = 0; k < samples; k++)
                {
                    V2 p = Project(c.LocalToWorld(s.RingPoint(i, 2.0 * Math.PI * k / SleeveRingSamples)), sun) - foot;
                    proj[i].Add(new V2(V2.Dot(p, d), V2.Dot(p, perp)));
                }
            }
            var hulls = new List<V2[]>(nr - 1);
            var pts = new List<V2>(2 * SleeveRingSamples);
            double uMin = double.MaxValue, uMax = double.MinValue;
            for (int i = 0; i + 1 < nr; i++)
            {
                pts.Clear(); pts.AddRange(proj[i]); pts.AddRange(proj[i + 1]);
                V2[] h = ConvexHull(pts);
                hulls.Add(h);
                for (int k = 0; k < h.Length; k++) { uMin = Math.Min(uMin, h[k].x); uMax = Math.Max(uMax, h[k].x); }
            }

            // stations from half a soft edge inside each end, denser near the ends; then all-zero cap rows half a
            // soft edge outside each end, so the alpha-0.5 contour of the bar ends falls on uMin / uMax
            double p0 = PenumbraAt(uMin), p1 = PenumbraAt(uMax);
            double uA = uMin + 0.5 * p0, uB = uMax - 0.5 * p1;
            var rows = new List<Row>(SleeveStations + 2);
            for (int k = 0; k < SleeveStations; k++)
            {
                double t = k / (double)(SleeveStations - 1);
                double u = uA + (uB - uA) * (t - 0.5 / (2.0 * Math.PI) * Math.Sin(2.0 * Math.PI * t));
                if (!Interval(hulls, u, out double v0, out double v1)) continue;
                rows.Add(new Row { L = foot + d * u + perp * v0, R = foot + d * u + perp * v1, P = PenumbraAt(u) });
            }
            if (rows.Count >= 2)
            {
                Row first = rows[0], last = rows[rows.Count - 1];
                V2 back = d * -p0, fwd = d * p1;
                rows.Insert(0, new Row { L = first.L + back, R = first.R + back, P = first.P, Cap = true });
                rows.Add(new Row { L = last.L + fwd, R = last.R + fwd, P = last.P, Cap = true });
            }
            return rows;
        }

        /// <summary>
        /// Soft edge width at a distance u along the shadow from the foot: 0.35 m at the foot growing linearly to 6 m at
        /// 674 m (661 f), plus ShadowPenumbraFar (u / 674)^2 (critique run06 C-05 / E8), 30 m at 674 m.
        /// </summary>
        public static double PenumbraAt(double u)
        {
            double f = ColossusShape.Clamp(u / ThreadConstants.ColossusShadowLength, 0.0, 1.0);
            return ColossusSpec.ShadowPenumbra0 + (ColossusSpec.ShadowPenumbra1 - ColossusSpec.ShadowPenumbra0) * f
                   + ColossusSpec.ShadowPenumbraFar * f * f;
        }

        /// <summary>Shade multiplier along the shadow (critique run06 C-05 / E8): 1 up to 60 percent of its length, smoothly 0 at the tip.</summary>
        public static double LengthFade(double u, double length) =>
            1.0 - ThreadMath.Smoothstep(ColossusSpec.ShadowLengthFade0 * length, length, u);

        /// <summary>Lateral extent [v0, v1] of the union of convex polygons (in (u, v), stored as V2(x = u, z = v)) on the scanline u = const.</summary>
        static bool Interval(List<V2[]> polys, double u, out double v0, out double v1)
        {
            v0 = double.MaxValue; v1 = double.MinValue;
            for (int p = 0; p < polys.Count; p++)
            {
                V2[] h = polys[p];
                for (int i = 0, j = h.Length - 1; i < h.Length; j = i++)
                {
                    V2 a = h[j], b = h[i];
                    if ((a.x - u) * (b.x - u) > 0.0) continue;
                    double du = b.x - a.x;
                    if (Math.Abs(du) < 1e-12)
                    {
                        v0 = Math.Min(v0, Math.Min(a.z, b.z)); v1 = Math.Max(v1, Math.Max(a.z, b.z));
                        continue;
                    }
                    double v = a.z + (u - a.x) * (b.z - a.z) / du;
                    v0 = Math.Min(v0, v); v1 = Math.Max(v1, v);
                }
            }
            return v1 >= v0;
        }

        /// <summary>Convex hull (Andrew's monotone chain), counter-clockwise in (x, z), collinear points removed.</summary>
        public static V2[] ConvexHull(List<V2> input)
        {
            var p = new List<V2>(input);
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            int n = p.Count;
            if (n < 3) return p.ToArray();
            var h = new V2[2 * n];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && Cross(h[k - 2], h[k - 1], p[i]) <= 0) k--;
                h[k++] = p[i];
            }
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Cross(h[k - 2], h[k - 1], p[i]) <= 0) k--;
                h[k++] = p[i];
            }
            var r = new V2[Math.Max(1, k - 1)];
            Array.Copy(h, r, r.Length);
            return r;
        }

        static double Cross(V2 o, V2 a, V2 b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);

        static V2 AnalyticProjection(D3 p, D3 sun)
        {
            // straight-line projection along -S onto y = ColossusShadowY, written out independently of Project()
            double t = (p.y - ThreadConstants.ColossusShadowY) / sun.y;
            return new V2(p.x + (-sun.x) * t, p.z + (-sun.z) * t);
        }

        static double Penumbra(V2 centre, V2 foot, V2 d) => PenumbraAt(V2.Dot(centre - foot, d));

        static Row MakeRow(List<V2> ring, V2 across, V2 foot, V2 d)
        {
            double uMin = double.MaxValue, uMax = double.MinValue;
            V2 l = ring[0], r = ring[0];
            for (int i = 0; i < ring.Count; i++)
            {
                double u = V2.Dot(ring[i], across);
                if (u < uMin) { uMin = u; l = ring[i]; }
                if (u > uMax) { uMax = u; r = ring[i]; }
            }
            return new Row { L = l, R = r, P = Penumbra((l + r) * 0.5, foot, d) };
        }

        /// <summary>
        /// Adds one strip: per row 'across' vertices from outer-left (alpha 0) over inner-left (peak alpha) and evenly
        /// spaced interior points to inner-right and outer-right (alpha 0). If the strip is narrower than its soft edge
        /// the inner points merge and the peak alpha drops to width / softEdge (convolution of a box with the edge).
        /// Every vertex alpha is then multiplied by the z fade near the bank (661 f) and the length fade (LengthFade).
        /// </summary>
        void AddStrip(List<Row> rows, int across, V2 perp, V2 foot, V2 d, double length)
        {
            int baseIndex = Vertices.Count;
            var outline = new List<V2>(rows.Count * 2);
            var outlineR = new List<V2>(rows.Count);
            for (int k = 0; k < rows.Count; k++)
            {
                Row row = rows[k];
                V2 lr = row.R - row.L;
                double w = lr.Length;
                V2 dir = w > 1e-6 ? lr * (1.0 / w) : perp;
                double half = 0.5 * row.P;
                double peak = row.Cap ? 0.0 : (w >= row.P ? 1.0 : w / Math.Max(1e-6, row.P));
                // inner points (in metres from L along dir)
                double iL = Math.Min(half, 0.5 * w), iR = Math.Max(w - half, 0.5 * w);
                for (int a = 0; a < across; a++)
                {
                    // the outer (alpha 0) points sit half a soft edge outside L / R across the strip (perp): a row whose
                    // L-R chord is tilted must not push them along d past a neighbouring row once the edge is wide
                    V2 p;
                    double alpha;
                    if (a == 0) { p = row.L - perp * half; alpha = 0.0; }
                    else if (a == across - 1) { p = row.R + perp * half; alpha = 0.0; }
                    else
                    {
                        double f = (a - 1) / (double)(across - 3);
                        p = row.L + dir * (iL + (iR - iL) * f);
                        alpha = peak;
                    }
                    double fade = 1.0 - ThreadMath.Smoothstep(ThreadConstants.ColossusShadowFadeZ0, ThreadConstants.ColossusShadowFadeZ1, p.z);
                    fade *= LengthFade(V2.Dot(p - foot, d), length);
                    Vertices.Add(p);
                    Alpha.Add(ColossusShape.Clamp(alpha * fade, 0.0, 1.0));
                }
                if (!row.Cap) { outline.Add(row.L); outlineR.Add(row.R); }
                else if (rows.Count > 1)
                {
                    // a soft end cap: the alpha-0.5 contour lies half-way to the neighbouring row
                    Row nb = rows[k == 0 ? 1 : k - 1];
                    outline.Add((row.L + nb.L) * 0.5); outlineR.Add((row.R + nb.R) * 0.5);
                }
            }
            for (int k = outlineR.Count - 1; k >= 0; k--) outline.Add(outlineR[k]);
            Outlines.Add(outline.ToArray());

            // triangles; every triangle must face up (+Y). The expected orientation of the strip is that of its
            // first non-degenerate triangle; any triangle that disagrees is a fold (counted, then emitted facing up).
            int expected = 0;
            for (int k = 0; k + 1 < rows.Count; k++)
            {
                for (int a = 0; a + 1 < across; a++)
                {
                    int i00 = baseIndex + k * across + a, i01 = i00 + 1, i10 = i00 + across, i11 = i10 + 1;
                    TryTri(i00, i10, i11, ref expected);
                    TryTri(i00, i11, i01, ref expected);
                }
            }
        }

        /// <summary>y component of the face normal of the ground triangle (a, b, c) in Unity's convention cross(b - a, c - a).</summary>
        public static double UpComponent(V2 a, V2 b, V2 c) => (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z);

        void TryTri(int a, int b, int c, ref int expected)
        {
            V2 pa = Vertices[a], pb = Vertices[b], pc = Vertices[c];
            double up = UpComponent(pa, pb, pc);
            // degenerate or sliver (merged inner points near a tip): covers no visible area, and a triangle thinner
            // than 1 mm relative to its longest edge could flip its winding once the vertices are rounded to float
            double eMax = Math.Max((pb - pa).Length, Math.Max((pc - pb).Length, (pa - pc).Length));
            if (Math.Abs(up) < 1e-3 * Math.Max(1.0, eMax)) return;   // |up| = 2 area = height x longest edge
            int sign = up > 0 ? 1 : -1;
            if (expected == 0) expected = sign;
            else if (sign != expected)
            {
                Folds++;
                FoldCentroids.Add((Vertices[a] + Vertices[b] + Vertices[c]) * (1.0 / 3.0));
            }
            if (up > 0) { Triangles.Add(a); Triangles.Add(b); Triangles.Add(c); }
            else { Triangles.Add(a); Triangles.Add(c); Triangles.Add(b); }
        }

        /// <summary>Even-odd point-in-polygon test.</summary>
        public static bool Inside(V2[] poly, V2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                V2 a = poly[i], b = poly[j];
                if ((a.z > p.z) != (b.z > p.z))
                {
                    double x = a.x + (p.z - a.z) * (b.x - a.x) / (b.z - a.z);
                    if (p.x < x) inside = !inside;
                }
            }
            return inside;
        }

        public bool InsideAny(V2 p)
        {
            for (int i = 0; i < Outlines.Count; i++) if (Inside(Outlines[i], p)) return true;
            return false;
        }
    }
}
