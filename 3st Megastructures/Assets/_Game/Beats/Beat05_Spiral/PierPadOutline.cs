// PierPadOutline: the plan outline of the beat-7 pier and pad as ONE welded curve (MASTER_PROMPT 14.5.7):
// the 3.0 m pier rectangle from P5 (z = 400) along +Z united with the pad circle of radius 3.0 at
// (0, 424), the two concave corners filleted with 0.8 m. The curve is sampled as an open polyline from the
// right pier edge at z = 400 around the far side of the pad to the left pier edge at z = 400; the left half
// is the exact mirror of the right half (so the top and bottom faces triangulate as symmetric trapezoid
// strips with no T-junctions, and every sample is shared by all strips of the slab). Each sample carries its
// inward normal; offsets at a distance d are exact for lines and arcs (concentric arcs).
// Used by Beat07_Top (slab mesh), Beat05_Spiral (handrail loop and posts) and the EditMode tests.
using System;
using System.Collections.Generic;

namespace Megastructure.Beats
{
    public struct OutlineSample
    {
        public double x, z;     // point on the outline (plan)
        public double nx, nz;   // unit inward normal
        public double u;        // arc length along the outline from the right pier start
    }

    public static class PierPadOutline
    {
        public const double W = WalkwayLayout.HalfWidth;   // pier half-width (deck width 3.0, 14.5.7)
        public const int FilletSegments = 8;
        public const int PadCircleSegments = 96;            // angular resolution of the pad arc (3.75 deg)
        // Pad underside boss: the pad is 0.6 m thick (14.5.7) under the beacon while its rim keeps the 0.4 m deck
        // section of the pier, so the welded slab stays one constant section (no step in the outline walls).
        public const double BossRadius = 2.4;
        public const double BossEmbed = 0.05;               // the boss top sits 5 cm inside the slab (no co-planar faces)
        public const double BossChamfer = 0.03;

        static double Sq(double v) => v * v;

        /// <summary>z where the straight pier edge meets the fillet (fillet centre at x = +-(W + F)).</summary>
        public static double FilletZ =>
            ThreadConstants.PadCentreZ - Math.Sqrt(Sq(ThreadConstants.PadRadius + ThreadConstants.PierFillet) - Sq(W + ThreadConstants.PierFillet));

        /// <summary>Tangent point of the right fillet on the pad circle.</summary>
        public static void TangentPoint(out double x, out double z)
        {
            double k = ThreadConstants.PadRadius / (ThreadConstants.PadRadius + ThreadConstants.PierFillet);
            x = (W + ThreadConstants.PierFillet) * k;
            z = ThreadConstants.PadCentreZ + (FilletZ - ThreadConstants.PadCentreZ) * k;
        }

        static double FilletPhiStart => Math.PI;
        static double FilletPhiEnd { get { TangentPoint(out double tx, out double tz); return Math.Atan2(tz - FilletZ, tx - (W + ThreadConstants.PierFillet)); } }
        static double PadPhiStart { get { TangentPoint(out double tx, out double tz); return Math.Atan2(tz - ThreadConstants.PadCentreZ, tx); } }
        static double PadPhiEnd => 0.5 * Math.PI;

        /// <summary>Last straight-run station z (posts every 2.0 m from the pier start).</summary>
        public static int StraightPostCount => (int)Math.Floor((FilletZ - ThreadConstants.PierStartZ) / ThreadConstants.PostSpacing + 1e-9) + 1;

        static List<OutlineSample> s_right, s_full;

        /// <summary>Right half: from (W, 400) to the far point (0, 427) inclusive.</summary>
        public static List<OutlineSample> RightHalf()
        {
            if (s_right != null) return s_right;
            var list = new List<OutlineSample>(64);
            double zf = FilletZ, z0 = ThreadConstants.PierStartZ;
            int nStraight = StraightPostCount;
            for (int k = 0; k < nStraight; k++)
            {
                double z = z0 + ThreadConstants.PostSpacing * k;
                list.Add(new OutlineSample { x = W, z = z, nx = -1, nz = 0, u = z - z0 });
            }
            if (zf - (z0 + ThreadConstants.PostSpacing * (nStraight - 1)) > 1e-6)
                list.Add(new OutlineSample { x = W, z = zf, nx = -1, nz = 0, u = zf - z0 });
            double u = zf - z0;

            // concave fillet, traversed clockwise about its centre: inward = away from the centre
            double F = ThreadConstants.PierFillet, fcx = W + F, fcz = zf;
            double pa = FilletPhiStart, pb = FilletPhiEnd;
            for (int k = 1; k <= FilletSegments; k++)
            {
                double phi = pa + (pb - pa) * k / FilletSegments;
                double c = Math.Cos(phi), s = Math.Sin(phi);
                u += F * Math.Abs(pb - pa) / FilletSegments;
                list.Add(new OutlineSample { x = fcx + F * c, z = fcz + F * s, nx = c, nz = s, u = u });
            }

            // pad arc, counter-clockwise about the pad centre: inward = toward the centre
            double R = ThreadConstants.PadRadius, zc = ThreadConstants.PadCentreZ;
            double p0 = PadPhiStart, p1 = PadPhiEnd;
            int nPad = (int)Math.Ceiling((p1 - p0) / (2.0 * Math.PI / PadCircleSegments) - 1e-9);
            for (int k = 1; k <= nPad; k++)
            {
                double phi = p0 + (p1 - p0) * k / nPad;
                double c = Math.Cos(phi), s = Math.Sin(phi);
                u += R * (p1 - p0) / nPad;
                if (k == nPad) { c = 0; s = 1; }   // exact far point on the mirror axis
                list.Add(new OutlineSample { x = R * c, z = zc + R * s, nx = -c, nz = -s, u = u });
            }
            s_right = list;
            return list;
        }

        /// <summary>Index of the far point (0, 427) in Samples().</summary>
        public static int FarIndex => RightHalf().Count - 1;

        /// <summary>The whole open outline, right pier start to left pier start (mirror-symmetric, 2 * FarIndex + 1 samples).</summary>
        public static List<OutlineSample> Samples()
        {
            if (s_full != null) return s_full;
            var right = RightHalf();
            int m = right.Count - 1;
            var list = new List<OutlineSample>(2 * m + 1);
            for (int i = 0; i <= m; i++) list.Add(right[i]);
            double uFar = right[m].u;
            for (int i = m - 1; i >= 0; i--)
            {
                var r = right[i];
                list.Add(new OutlineSample { x = -r.x, z = r.z, nx = -r.nx, nz = r.nz, u = 2.0 * uFar - r.u });
            }
            s_full = list;
            return list;
        }

        /// <summary>Appends the outline offset inward by d at height y (handrail centre line); skipEnds drops the two z = 400 points.</summary>
        public static void AppendInset(List<D3> pts, double d, double y, bool skipEnds)
        {
            var smp = Samples();
            int i0 = skipEnds ? 1 : 0, i1 = skipEnds ? smp.Count - 2 : smp.Count - 1;
            for (int i = i0; i <= i1; i++)
            {
                var s = smp[i];
                pts.Add(new D3(s.x + s.nx * d, y, s.z + s.nz * d));
            }
        }

        /// <summary>Point-in-outline (even-odd) for the polygon of the samples closed by the z = 400 edge.</summary>
        public static bool Contains(double x, double z)
        {
            var smp = Samples();
            bool inside = false;
            int n = smp.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = smp[i].x, zi = smp[i].z, xj = smp[j].x, zj = smp[j].z;
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>Distance from (x, z) to the free edge of the slab (the outline polyline; the z = 400 line is the join with the helix deck, not an edge).</summary>
        public static double DistanceToEdge(double x, double z)
        {
            var smp = Samples();
            double best = double.MaxValue;
            int n = smp.Count;
            for (int i = 1; i < n; i++)
            {
                double ax = smp[i - 1].x, az = smp[i - 1].z, bx = smp[i].x, bz = smp[i].z;
                double ex = bx - ax, ez = bz - az, l2 = ex * ex + ez * ez;
                double t = l2 > 1e-18 ? Math.Max(0, Math.Min(1, ((x - ax) * ex + (z - az) * ez) / l2)) : 0;
                double dx = ax + ex * t - x, dz = az + ez * t - z;
                best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        /// <summary>Length of the right half of the curved rail run, from the last straight post to the far point, at inset d.</summary>
        public static double CurveHalfLength(double d)
        {
            double zLast = ThreadConstants.PierStartZ + ThreadConstants.PostSpacing * (StraightPostCount - 1);
            return (FilletZ - zLast) + (ThreadConstants.PierFillet + d) * Math.Abs(FilletPhiEnd - FilletPhiStart)
                 + (ThreadConstants.PadRadius - d) * (PadPhiEnd - PadPhiStart);
        }

        /// <summary>Point and unit tangent at arc length u (from the last straight post) on the right half of the inset curve.</summary>
        public static void EvalInsetRight(double u, double d, out double x, out double z, out double tx, out double tz)
        {
            double zLast = ThreadConstants.PierStartZ + ThreadConstants.PostSpacing * (StraightPostCount - 1);
            double lLine = FilletZ - zLast;
            if (u <= lLine) { x = W - d; z = zLast + u; tx = 0; tz = 1; return; }
            u -= lLine;
            double rf = ThreadConstants.PierFillet + d, lf = rf * Math.Abs(FilletPhiEnd - FilletPhiStart);
            if (u <= lf)
            {
                double phi = FilletPhiStart - u / rf;   // clockwise
                x = W + ThreadConstants.PierFillet + rf * Math.Cos(phi);
                z = FilletZ + rf * Math.Sin(phi);
                tx = Math.Sin(phi); tz = -Math.Cos(phi);
                return;
            }
            u -= lf;
            double rp = ThreadConstants.PadRadius - d;
            double ph = Math.Min(PadPhiEnd, PadPhiStart + u / rp);
            x = rp * Math.Cos(ph); z = ThreadConstants.PadCentreZ + rp * Math.Sin(ph);
            tx = -Math.Sin(ph); tz = Math.Cos(ph);
        }

        /// <summary>Rail posts of the pier and pad (14.5.7): every 2.0 m along both pier edges, then evenly around the far arc.</summary>
        public static void AppendPosts(List<PostPlacement> list, double d)
        {
            int nStraight = StraightPostCount;
            for (int k = 0; k < nStraight; k++)
            {
                double z = ThreadConstants.PierStartZ + ThreadConstants.PostSpacing * k;
                list.Add(new PostPlacement { basePos = new D3(W + (-1.0) * d, ThreadConstants.TopY, z), yaw = 0 });
                list.Add(new PostPlacement { basePos = new D3(-W + d, ThreadConstants.TopY, z), yaw = 0 });
            }
            double lh = CurveHalfLength(d), L = 2.0 * lh;
            int nInt = Math.Max(2, (int)Math.Round(L / ThreadConstants.PostSpacing));
            for (int j = 1; j < nInt; j++)
            {
                double u = L * j / nInt;
                bool mirror = u > lh;
                EvalInsetRight(mirror ? L - u : u, d, out double x, out double z, out double tx, out double tz);
                if (mirror) { x = -x; tx = -tx; }
                list.Add(new PostPlacement { basePos = new D3(x, ThreadConstants.TopY, z), yaw = Math.Atan2(tx, tz) });
            }
        }
    }
}
