using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Lofted fan blade of beat 6 (14.5.6): chord c(R) = 14 + 16 (R - 40) / 98, pitch from the rotation plane
    /// p(R) = acos(0.2094 R / c(R)) (exactly 12 degrees of arc: 0.2094395 R), absolute thickness 3 m (NACA 4-digit thickness law
    /// with a closed trailing edge, so every section is convex and 3.0 m thick at 30 % chord).
    ///
    /// Each section is laid out in the unrolled cylinder coordinates (s = arc length along +theta, z = axial) and WRAPPED onto
    /// its own cylinder of radius R, so its projection on the rotation plane is an exact circular arc. Because a 3 m thick
    /// section overshoots its chord's projection near the rounded leading edge (about 0.2 m at the root), the chord is scaled
    /// by k (0.97-1.0, solved per section by bisection) so that the projected extent of the real outline is exactly
    /// 0.2094395 R: the blade then covers exactly 12 degrees at every section radius and the free gap is exactly 18 degrees
    /// (pitch, thickness and the 12 degree silhouette are exact; the chord is the quantity that absorbs the leading-edge
    /// rounding). Sections are centred on the blade's radial line, the pitch axis (mid chord) lies in the fan plane (z = 0).
    /// Sections sit at the knots 39 (inside the hub), 40, 48, 90, 130 and the tip 137.95 (5 cm inside the R 138 housing
    /// liner), subdivided at most 3.5 m apart, so the test radii 48 / 90 / 130 are exact section rings.
    ///
    /// Leading edge toward +theta (the direction of rotation) and +Z (upstream: the fan exhausts the shaft through the Face).
    /// Vertex colour: R = AO (darker toward the hub), G = edge wear (leading-edge erosion strip, trailing edge, tip band).
    /// Build-time only; no per-vertex allocation (scratch arrays are reused).
    /// </summary>
    internal static class FanBlade
    {
        public const int SIDE = 12;                       // outline points per side (outline = 2 SIDE points, closed)
        public const float THICKNESS = 3f;
        public const float ROOT_R = 39f;                  // loft starts 1 m inside the hub drum
        public const float TIP_CLEARANCE = 0.05f;
        public const float TIP_R = AbyssConst.FAN_R_TIP - TIP_CLEARANCE;   // tip R 138 +/- 0.2, 4 cm clear of the 256-gon liner
        public const float DESIGN_ROOT_R = AbyssConst.FAN_R_HUB;   // sections inside the hub reuse the R 40 section
        public const float MAX_STEP = 3.5f;
        /// <summary>Largest edge wear (vertex G) on a blade: below the 0.97 chamfer mark of AbBevelResolve (R2-17).</summary>
        public const float MAX_WEAR = 0.9f;
        public const float ARC_DEG = 360f / AbyssConst.FAN_BLADES - GAP_DEG;   // 12 degrees of projected arc per blade
        public const float GAP_DEG = 18f;                                       // free gap between blades (14.5.6)
        public static readonly double ArcRad = ARC_DEG * System.Math.PI / 180.0;

        static readonly float[] Knots = { ROOT_R, DESIGN_ROOT_R, 48f, 90f, 130f, TIP_R };   // 48 / 90 / 130: the acceptance radii

        static readonly double[] s_u = new double[2 * SIDE];   // chord fraction, +0.5 at the leading edge
        static readonly double[] s_y = new double[2 * SIDE];   // signed half thickness (m), + upper surface
        static readonly double[] s_xn = new double[2 * SIDE];  // x / c from the leading edge (0..1)

        /// <summary>c(R) = 14 + 16 (R - 40) / 98: 14 m at the root (R 40), 30 m at the tip (R 138).</summary>
        public static double Chord(double r) => 14.0 + 16.0 * (r - AbyssConst.FAN_R_HUB) / (AbyssConst.FAN_R_TIP - AbyssConst.FAN_R_HUB);

        public static double Pitch(double r)
        {
            double c = ArcRad * r / Chord(r);
            return System.Math.Acos(c > 1.0 ? 1.0 : c);
        }

        /// <summary>NACA 00xx half thickness with a closed trailing edge, for absolute thickness THICKNESS (m).</summary>
        static double HalfThickness(double xn)
        {
            if (xn <= 0.0 || xn >= 1.0) return 0.0;
            double x2 = xn * xn;
            return 5.0 * THICKNESS * (0.2969 * System.Math.Sqrt(xn) - 0.1260 * xn - 0.3516 * x2 + 0.2843 * x2 * xn - 0.1036 * x2 * x2);
        }

        static void Outline()
        {
            int m = SIDE, n = 2 * SIDE;
            for (int j = 0; j < n; j++)
            {
                // j = 0 trailing edge, 1..m-1 upper surface, m leading edge, m+1..2m-1 lower surface (cosine spacing)
                double xn = j <= m ? 0.5 * (1.0 + System.Math.Cos(System.Math.PI * j / m)) : 0.5 * (1.0 + System.Math.Cos(System.Math.PI * (n - j) / m));
                if (j == 0) xn = 1.0;
                if (j == m) xn = 0.0;
                double h = HalfThickness(xn);
                s_xn[j] = xn;
                s_u[j] = 0.5 - xn;
                s_y[j] = j <= m ? h : -h;
            }
        }

        static double Extent(double chord, double cp, double sp, out double sMin, out double sMax)
        {
            sMin = double.MaxValue; sMax = double.MinValue;
            for (int j = 0; j < 2 * SIDE; j++)
            {
                double s = chord * s_u[j] * cp - s_y[j] * sp;
                if (s < sMin) sMin = s;
                if (s > sMax) sMax = s;
            }
            return sMax - sMin;
        }

        /// <summary>
        /// Section at design radius rd in unrolled coordinates, centred on the radial line: s (arc length, + = rotation
        /// direction) and z (axial). Returns the chord scale k that makes the projected extent exactly ArcRad * rd.
        /// </summary>
        public static double Section(double rd, double[] s, double[] z)
        {
            Outline();
            double c = Chord(rd), p = Pitch(rd), cp = System.Math.Cos(p), sp = System.Math.Sin(p);
            double target = ArcRad * rd;
            double lo = 0.5, hi = 1.5;
            for (int it = 0; it < 64; it++)
            {
                double mid = 0.5 * (lo + hi);
                if (Extent(c * mid, cp, sp, out _, out _) < target) lo = mid; else hi = mid;
            }
            double k = 0.5 * (lo + hi);
            Extent(c * k, cp, sp, out double sMin, out double sMax);
            double sMid = 0.5 * (sMin + sMax);
            for (int j = 0; j < 2 * SIDE; j++)
            {
                double u = c * k * s_u[j];
                s[j] = u * cp - s_y[j] * sp - sMid;
                z[j] = u * sp + s_y[j] * cp;
            }
            return k;
        }

        public static void Radii(List<float> radii)
        {
            radii.Clear();
            radii.Add(Knots[0]);
            for (int i = 0; i + 1 < Knots.Length; i++)
            {
                float a = Knots[i], b = Knots[i + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt((b - a) / MAX_STEP - 1e-4f));
                for (int k = 1; k <= steps; k++) radii.Add(k == steps ? b : a + (b - a) * k / steps);
            }
        }

        /// <summary>
        /// One blade centred on the rotor +X axis (angle 0) in rotor-local coordinates (Z = fan axis, z = 0 the fan plane),
        /// appended to mb. Returns the tip radius.
        /// </summary>
        public static float Build(MeshBuilder mb)
        {
            var radii = new List<float>(40);
            Radii(radii);
            int S = radii.Count, n = 2 * SIDE, C = n + 1;
            var P = new Vector3[S * n];
            var ss = new double[S * n];
            var zz = new double[S * n];
            var sTmp = new double[n];
            var zTmp = new double[n];
            float tipR = 0f;
            for (int i = 0; i < S; i++)
            {
                double r = radii[i];
                Section(System.Math.Max(r, DESIGN_ROOT_R), sTmp, zTmp);
                for (int j = 0; j < n; j++)
                {
                    double a = sTmp[j] / r;
                    P[i * n + j] = new Vector3((float)(r * System.Math.Cos(a)), (float)(r * System.Math.Sin(a)), (float)zTmp[j]);
                    ss[i * n + j] = sTmp[j];
                    zz[i * n + j] = zTmp[j];
                }
                if (radii[i] > tipR) tipR = radii[i];
            }

            // outline orientation in (s, z): outward normal of a tangent (ts, tz) is (tz, -ts) for CCW, (-tz, ts) for CW
            double area = 0.0;
            for (int j = 0; j < n; j++)
            {
                int q = (j + 1) % n;
                area += ss[j] * zz[q] - ss[q] * zz[j];
            }
            float orient = area >= 0.0 ? 1f : -1f;

            Color32 saved = mb.Color;
            int first = mb.VertexCount;
            for (int i = 0; i < S; i++)
            {
                float r = radii[i];
                int ip = Mathf.Max(0, i - 1), inx = Mathf.Min(S - 1, i + 1);
                byte ao = (byte)Mathf.RoundToInt(255f * Mathf.Lerp(0.55f, 1f, BeatKit.Smoothstep((r - 40f) / 16f)));
                float tipBand = BeatKit.Smoothstep((r - (TIP_R - 2.5f)) / 2.5f);
                for (int c = 0; c < C; c++)
                {
                    int j = c % n;
                    int jPrev, jNext;
                    if (c == 0) { jPrev = 0; jNext = 1; }                  // trailing edge, upper side (hard)
                    else if (c == n) { jPrev = n - 1; jNext = 0; }         // trailing edge, lower side (hard)
                    else { jPrev = j - 1; jNext = (j + 1) % n; }
                    Vector3 p = P[i * n + j];
                    Vector3 tu = P[i * n + jNext] - P[i * n + jPrev];
                    Vector3 tv = P[inx * n + j] - P[ip * n + j];
                    Vector3 nrm = Vector3.Cross(tu, tv);
                    float ts = (float)(ss[i * n + jNext] - ss[i * n + jPrev]), tz = (float)(zz[i * n + jNext] - zz[i * n + jPrev]);
                    float ns = orient * tz, nz = -orient * ts;
                    float a = (float)(ss[i * n + j] / r);
                    var outward = new Vector3(-Mathf.Sin(a) * ns, Mathf.Cos(a) * ns, nz);
                    if (Vector3.Dot(nrm, outward) < 0f) nrm = -nrm;
                    nrm = nrm.sqrMagnitude > 1e-20f ? nrm.normalized : outward.normalized;

                    // wear: leading-edge erosion shield (bare steel over the nose, fading out by 7-15 % of the chord, so the
                    // rounded edge catches the haze behind the fan as a thin highlight on the dark silhouette), a lighter
                    // trailing edge, the tip band
                    int fromLe = Mathf.Abs(j - SIDE);
                    float wear = fromLe <= 1 ? 1f : fromLe == 2 ? 0.55f : fromLe == 3 ? 0.12f : 0f;
                    if (j == 0) wear = 0.55f;
                    wear = Mathf.Max(wear, 0.6f * tipBand);
                    // R2-17: G >= 0.97 marks a 45-degree box chamfer for AbBevelResolve, which bends a sub-pixel chamfer's
                    // normal onto the object-axis face that looks most toward the camera; on the round blade nose that
                    // turned the leading edge into a sun-lit 1-px line seen through the bore (KS7d). The erosion strip stays
                    // just below the chamfer mark.
                    wear = Mathf.Min(wear, MAX_WEAR);
                    mb.Color = new Color32(ao, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(wear)), saved.b, saved.a);
                    mb.Vertex(p, nrm, new Vector2((float)ss[i * n + j], r));
                }
            }
            for (int i = 0; i + 1 < S; i++)
            {
                for (int c = 0; c + 1 < C; c++)
                {
                    int a0 = first + i * C + c, a1 = a0 + 1, b0 = a0 + C, b1 = b0 + 1;
                    BeatKit.Tri(mb, a0, a1, b1);
                    BeatKit.Tri(mb, a0, b1, b0);
                }
            }

            // tip cap: a fan on the tip cylinder, facing outward (radially)
            {
                int i = S - 1;
                float r = radii[i];
                double sc = 0.0, zc = 0.0;
                for (int j = 0; j < n; j++) { sc += ss[i * n + j]; zc += zz[i * n + j]; }
                sc /= n; zc /= n;
                float ac = (float)(sc / r);
                mb.Color = new Color32(235, 150, saved.b, saved.a);
                int centre = mb.Vertex(new Vector3(r * Mathf.Cos(ac), r * Mathf.Sin(ac), (float)zc), new Vector3(Mathf.Cos(ac), Mathf.Sin(ac), 0f),
                    new Vector2((float)sc, (float)zc));
                int ring = mb.VertexCount;
                for (int j = 0; j < n; j++)
                {
                    float a = (float)(ss[i * n + j] / r);
                    mb.Vertex(P[i * n + j], new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), new Vector2((float)ss[i * n + j], (float)zz[i * n + j]));
                }
                for (int j = 0; j < n; j++) BeatKit.Tri(mb, centre, ring + j, ring + (j + 1) % n);
            }
            mb.Color = saved;
            return tipR;
        }

        /// <summary>
        /// Angular coverage (degrees, atan2 from +X toward +Y) of the triangles idx[triFirst..triEnd) projected on the XY plane
        /// at radius R: min and max angle of every edge-circle crossing. Valid for a blade that spans less than 180 degrees and
        /// does not straddle +/-180 degrees (blade 0 is centred on angle 0). Returns false when nothing crosses R.
        /// </summary>
        public static bool Coverage(List<Vector3> pos, List<int> idx, int triFirst, int triEnd, float r, out float minDeg, out float maxDeg)
        {
            double lo = double.MaxValue, hi = double.MinValue, r2 = (double)r * r;
            for (int t = triFirst; t < triEnd; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    Vector3 pa = pos[idx[t + e]], pb = pos[idx[t + (e + 1) % 3]];
                    double ax = pa.x, ay = pa.y, dx = pb.x - pa.x, dy = pb.y - pa.y;
                    double qa = dx * dx + dy * dy, qb = 2.0 * (ax * dx + ay * dy), qc = ax * ax + ay * ay - r2;
                    if (qa < 1e-18) continue;
                    double disc = qb * qb - 4.0 * qa * qc;
                    if (disc < 0.0) continue;
                    double sq = System.Math.Sqrt(disc);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        double tt = (-qb + sgn * sq) / (2.0 * qa);
                        if (tt < 0.0 || tt > 1.0) continue;
                        double ang = System.Math.Atan2(ay + tt * dy, ax + tt * dx) * (180.0 / System.Math.PI);
                        if (ang < lo) lo = ang;
                        if (ang > hi) hi = ang;
                    }
                }
            }
            minDeg = (float)lo; maxDeg = (float)hi;
            return hi >= lo;
        }
    }
}
