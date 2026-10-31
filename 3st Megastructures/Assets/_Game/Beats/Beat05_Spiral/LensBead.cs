// LensBead: the nine station beads of beat 6 (MASTER_PROMPT 14.5.6, M08; D-035): oblate lens lathes centred on
// the cable axis at y_k = BeadY0 + BeadYStep (k - 1) (230 .. 550 m, all above the pier top at 146 m), horizontal
// radius 62.0, half-height 12.0, 288 x 48 segments, their window rings (3 rows of 48 dots, 0.9 x 1.2 m at y
// offsets -4, +1.7, +5.5) and the (layout-dependent) doors of beads 1-3.
//
// Geometry notes:
//  * Tessellation (critique run06 B1, A9, C-13, E9): the spec's 64 x 24 lathe drew polygonal silhouettes (6 m
//    chords, kinks of 20-230 px in KF6b / KF7b). 288 azimuth segments (6 facets per window step) leave a chord
//    sagitta of 3.7 mm at the rim and 48 profile segments a profile sagitta of 3.5 mm. From the closest eye point
//    of the walkway (77 m from S1's underside under D-035) the summed deviation (7.2 mm) stays below 0.17 px at 4K
//    in the image centre, and at every key-shot pose below 0.43 px in the 16:9 frame corner
//    (Beat05_07Tests.Beads_SilhouetteChordBelowHalfPixelAt4K: < 0.25 px centre, < 0.5 px corner). 27.6 k
//    triangles per bead, 249 k for the nine (budget 7.4: 20 M visible).
//  * Profile: the ellipse (r/62)^2 + (y/12)^2 = 1. Its rim has a radius of curvature of only 2.3 m, so the 48
//    profile segments are spaced for a uniform chord sagitta (equal increments of the integral of
//    sqrt(curvature) ds), which concentrates the rings at the rim, instead of uniform angle. The lens is cut at
//    r = 48 m, 2 m inside the r = 50 cable core: the polar caps are never visible and all segments go to the
//    visible part. The equator ring is exact (r = 62.0, y = 0).
//  * Windows: one window mesh per row, a strip bent along the faceted profile (0.02 m outside the facets
//    everywhere, never co-planar, never floating). 48 windows on 288 facets: a window step is exactly 6 facets
//    and the staggered rows are offset by 3, so with the facet lattice phased half a facet from the reference
//    azimuth (WindowResidue = Delta / 2) every window is centred on its facet column (t = 0): no window straddles
//    an azimuthal facet edge, and each lies in one facet column, where the faceted surface is an extruded
//    polyline. Each bead's window rows get that bead's palette colour through a MaterialPropertyBlock
//    (Beat06_Clouds).
//  * Doors (14.5.6, D-023): a door is built only on a bead whose equator the deck passes (HasDoor). Under D-035
//    every bead equator lies above the pier top, so HasDoor is false for all nine beads: no porch or leaf is
//    built, no window is omitted for a door, and every support arm ends in the cable core at r = 49.7
//    (WalkwayLayout.RootRadius finds no lens at deck height). The porch code is kept for a layout that brings a
//    bead equator back to helix height: the door then sits at the azimuth where the deck passes the equator;
//    where a support arm lands within reach of the porch it slides along the path by the smallest multiple of
//    0.25 m that clears it, its sill flush with the deck at that station.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    public struct DoorSpec
    {
        public int bead;
        public double s;        // path station the door faces
        public double azimuth;  // lathe azimuth of the door (from +Z toward +X, about the cable axis)
        public double sillY;    // = deck top at s (sill flush with the deck)
    }

    public struct WindowPlacement
    {
        public Matrix4x4 matrix;   // window-row mesh space (x tangential, y up, z radial) to world
        public double azimuth;     // lathe azimuth of the window centre
        public double t;           // tangential offset inside its facet column (m)
        public int column;         // facet column index
    }

    public static class LensBead
    {
        /// <summary>Facets per window step: 288 / 48. Must be even so the staggered rows (half a step) also sit on facet centres.</summary>
        public const int FacetsPerWindowStep = 6;
        /// <summary>Lathe azimuth segments: 288 (critique run06 B1 / A9 / C-13 / E9 replace the spec's 64; see the header).</summary>
        public const int SegmentsAzimuth = FacetsPerWindowStep * ThreadConstants.WindowsPerRing;
        /// <summary>Profile segments: 48 (critique run06 B1 / C-13 replace the spec's 24), uniform chord sagitta.</summary>
        public const int SegmentsProfile = 48;
        public const double CutRadius = 48.0;                   // lens cut 2 m inside the r = 50 cable core
        public const double WindowOffset = 0.02;                // windows 2 cm outside the facets
        public const double WindowArmMargin = 0.25;             // windows this close to an arm root are omitted
        public const double DoorArmClearance = 0.25;            // free gap between a door porch and an arm root
        public const double DoorShiftStep = 0.25, DoorShiftMax = 4.0;

        // door porch, local frame: x tangential, y up from the sill, z = radial distance from the cable axis
        public const double DoorLeafFront = ThreadConstants.BeadRadius + 0.04;                 // door plane just outside the rim
        public const double PorchFront = DoorLeafFront + ThreadConstants.BeadDoorRecess;        // leaf recessed 0.4 m (14.5.6)
        public const double DoorLeafThickness = 0.04;
        public const double JambThickness = 0.15, JambTopInLintel = 0.09, JambBottomInFloor = 0.05;
        public const double LintelThickness = 0.18, LintelProud = 0.03, LintelOverhang = 0.04;
        public const double FloorThickness = 0.16, LedgeDepth = 0.30, FloorOverhang = 0.08;
        public const double PorchBack = ThreadConstants.BeadRadius - 1.50;                      // buried in the lens
        public const double LintelBack = ThreadConstants.BeadRadius - 1.55;
        public const double FloorBack = ThreadConstants.BeadRadius - 0.40;
        public const double PorchChamfer = 0.015, LeafInset = 0.01;
        public const int DoorCount = 3;                         // 14.5.6: doors on beads 1 to 3 at most (only where HasDoor)

        public static double Delta => 2.0 * Math.PI / SegmentsAzimuth;
        public static double WindowStep => 2.0 * Math.PI / ThreadConstants.WindowsPerRing;
        /// <summary>Window lattice phase from the facet edges: every window (eye row on multiples of 6 facets, staggered rows 3 facets on) sits on a facet centre.</summary>
        public static double WindowResidue => 0.5 * Delta;
        public static double PorchHalfWidth => 0.5 * ThreadConstants.DoorWidth + JambThickness + FloorOverhang;   // floor / ledge half-width

        public static double BeadY(int k) => ThreadConstants.BeadY0 + ThreadConstants.BeadYStep * k;
        public static Pal BeadPal(int k) { int m = k % 3; return m == 0 ? Pal.STATION_MINT : (m == 1 ? Pal.STATION_PEACH : Pal.STATION_LILAC); }
        /// <summary>A door only where the deck passes the bead equator (14.5.6, D-023); false for every bead under D-035.</summary>
        public static bool HasDoor(int k) => k >= 0 && k < DoorCount && EquatorS(k) >= ThreadConstants.HelixStartS && EquatorS(k) <= ThreadConstants.HelixEndS;

        /// <summary>Number of beads that carry a door (0 under D-035: the column starts above the pier).</summary>
        public static int DoorBeadCount
        {
            get { int n = 0; for (int k = 0; k < DoorCount; k++) if (HasDoor(k)) n++; return n; }
        }

        /// <summary>Index of the eye-level window row (+1.7 m), the row the doors replace a window in.</summary>
        public static int EyeRow
        {
            get
            {
                var o = ThreadConstants.WindowRingOffsets;
                int best = 0;
                for (int i = 1; i < o.Length; i++) if (Math.Abs(o[i] - ThreadConstants.EyeHeight) < Math.Abs(o[best] - ThreadConstants.EyeHeight)) best = i;
                return best;
            }
        }

        // ---------------------------------------------------------------- profile
        static double[] s_theta, s_y, s_r;

        static double SagittaWeight(double th)
        {
            double a = ThreadConstants.BeadRadius, b = ThreadConstants.BeadHalfHeight, sn = Math.Sin(th), cs = Math.Cos(th);
            return Math.Pow(a * a * sn * sn + b * b * cs * cs, -0.25);
        }

        static void EnsureProfile()
        {
            if (s_theta != null) return;
            int n = SegmentsProfile, half = n / 2;
            double a = ThreadConstants.BeadRadius, b = ThreadConstants.BeadHalfHeight;
            double thMax = Math.Acos(CutRadius / a);
            const int G = 8192;
            var F = new double[G + 1];
            for (int g = 1; g <= G; g++)
            {
                double t0 = thMax * (g - 1) / G, t1 = thMax * g / G;
                F[g] = F[g - 1] + 0.5 * (SagittaWeight(t0) + SagittaWeight(t1)) * (t1 - t0);
            }
            var th = new double[n + 1];
            th[half] = 0.0;
            int gi = 1;
            for (int j = 1; j <= half; j++)
            {
                double t;
                if (j == half) t = thMax;
                else
                {
                    double target = F[G] * j / half;
                    while (gi < G && F[gi] < target) gi++;
                    double f = (target - F[gi - 1]) / Math.Max(1e-300, F[gi] - F[gi - 1]);
                    t = thMax * (gi - 1 + f) / G;
                }
                th[half + j] = t;
                th[half - j] = -t;
            }
            var y = new double[n + 1];
            var r = new double[n + 1];
            for (int i = 0; i <= n; i++) { y[i] = b * Math.Sin(th[i]); r[i] = a * Math.Cos(th[i]); }
            y[half] = 0.0; r[half] = a;
            s_y = y; s_r = r; s_theta = th;
        }

        /// <summary>Profile ring heights (relative to the bead centre) and radii, bottom to top (SegmentsProfile + 1 rings).</summary>
        public static double RingY(int i) { EnsureProfile(); return s_y[i]; }
        public static double RingR(int i) { EnsureProfile(); return s_r[i]; }
        public static double RingTheta(int i) { EnsureProfile(); return s_theta[i]; }

        // ---------------------------------------------------------------- doors and phase
        static double[] s_doorS;

        /// <summary>Lathe azimuth (about the cable axis) of the helix point at plan s (extrapolated beyond the helix).</summary>
        public static double AzimuthAtS(double s)
        {
            double psi = (s - ThreadConstants.HelixStartS) / ThreadConstants.HelixRadius;
            return Math.Atan2(-Math.Cos(psi), Math.Sin(psi));
        }

        /// <summary>Path station where the deck (extrapolated on the 0.25 grade) would pass bead k's equator; beyond HelixEndS for every bead under D-035.</summary>
        public static double EquatorS(int k) => ThreadConstants.HelixStartS + (BeadY(k) - ThreadConstants.HelixBaseY) / ThreadConstants.HelixGrade;

        /// <summary>Door station of bead k: the equator crossing, slid along the path just enough to clear the arms (HasDoor beads); the bare equator station otherwise.</summary>
        public static double DoorS(int k)
        {
            if (!HasDoor(k)) return EquatorS(k);
            if (s_doorS == null)
            {
                var ds = new double[DoorCount];
                double need = PorchHalfWidth + 0.5 * WalkwayLayout.ArmRootWidth + DoorArmClearance;   // tangential, at the rim
                double scale = ThreadConstants.BeadRadius / ThreadConstants.HelixRadius;              // plan metres -> rim metres
                int steps = (int)Math.Round(DoorShiftMax / DoorShiftStep);
                for (int d = 0; d < DoorCount; d++)
                {
                    double sn = EquatorS(d), chosen = sn;
                    if (HasDoor(d))
                        for (int st = 0; st <= 2 * steps; st++)
                        {
                            int m = (st + 1) / 2;
                            double cand = sn + (st % 2 == 1 ? -m : m) * DoorShiftStep;
                            bool ok = true;
                            for (int i = 0; i < WalkwayLayout.ArmCount && ok; i++)
                                if (Math.Abs(cand - WalkwayLayout.ArmStation(i)) * scale < need) ok = false;
                            if (ok) { chosen = cand; break; }
                        }
                    ds[d] = chosen;
                }
                s_doorS = ds;
            }
            return s_doorS[k];
        }

        /// <summary>
        /// Reference azimuth of bead k: its door (HasDoor beads) or the azimuth of the extrapolated equator crossing
        /// (every bead under D-035; only a deterministic phase). The eye-level window row is centred on it.
        /// </summary>
        public static double ReferenceAzimuth(int k) => AzimuthAtS(DoorS(k));

        /// <summary>Azimuth of the first lathe column of bead k (half a facet before the reference azimuth).</summary>
        public static double Alpha0(int k) => ReferenceAzimuth(k) - WindowResidue;

        /// <summary>The door of bead k. Meaningful only where HasDoor(k); the builders and tests never ask for another.</summary>
        public static DoorSpec Door(int k)
        {
            double s = DoorS(k);
            return new DoorSpec { bead = k, s = s, azimuth = AzimuthAtS(s), sillY = ThreadMath.PathY(s) };
        }

        // ---------------------------------------------------------------- surface queries
        /// <summary>Radius of the faceted lens surface of a bead at a height relative to its centre and an azimuth; 0 outside the lens.</summary>
        public static double FacetRadius(double yRel, double azimuth, double alpha0)
        {
            EnsureProfile();
            int n = SegmentsProfile;
            if (yRel < s_y[0] || yRel > s_y[n]) return 0.0;
            int i = 0;
            while (i < n - 1 && yRel > s_y[i + 1]) i++;
            double t = (yRel - s_y[i]) / (s_y[i + 1] - s_y[i]);
            double rho = (s_r[i] + (s_r[i + 1] - s_r[i]) * t) * Math.Cos(0.5 * Delta);
            double rel = BeatMeshUtil.Wrap2Pi(azimuth - alpha0);
            double frac = rel - Math.Floor(rel / Delta) * Delta;
            return rho / Math.Cos(frac - 0.5 * Delta);
        }

        /// <summary>Largest faceted lens radius of all beads at world height y and azimuth (0 where no lens exists); bead = its index.</summary>
        public static double SurfaceRadius(double y, double azimuth, out int bead)
        {
            double best = 0.0;
            bead = -1;
            for (int k = 0; k < ThreadConstants.BeadCount; k++)
            {
                double yr = y - BeadY(k);
                if (yr < -ThreadConstants.BeadHalfHeight || yr > ThreadConstants.BeadHalfHeight) continue;
                double r = FacetRadius(yr, azimuth, Alpha0(k));
                if (r > best) { best = r; bead = k; }
            }
            return best;
        }

        // ---------------------------------------------------------------- lens mesh
        /// <summary>Lens lathe of bead k (cut at r = 48 inside the cable), outward normals of the true ellipsoid, seam column duplicated.</summary>
        public static void BuildLens(MeshBuilder mb, int k)
        {
            EnsureProfile();
            int n = SegmentsProfile, segs = SegmentsAzimuth, cols = segs + 1;
            double a = ThreadConstants.BeadRadius, b = ThreadConstants.BeadHalfHeight;
            double a0 = Alpha0(k), yb = BeadY(k);
            double cx = ThreadConstants.CableAxisX, cz = ThreadConstants.CableAxisZ;
            int start = mb.VertexCount;
            double v = 0;
            var sa = new double[segs];
            var ca = new double[segs];
            for (int j = 0; j < segs; j++) { double al = a0 + j * Delta; sa[j] = Math.Sin(al); ca[j] = Math.Cos(al); }
            for (int i = 0; i <= n; i++)
            {
                if (i > 0) { double dr = s_r[i] - s_r[i - 1], dy = s_y[i] - s_y[i - 1]; v += Math.Sqrt(dr * dr + dy * dy); }
                double r = s_r[i], y = s_y[i], cth = Math.Cos(s_theta[i]), sth = Math.Sin(s_theta[i]);
                // AO: the concave junction where the lens meets the cable
                Color col = BeatMeshUtil.Ao(0.72 + 0.28 * ThreadMath.Smoothstep(ThreadConstants.CableRidgeCrest - 0.7, ThreadConstants.CableRidgeCrest + 4.8, r));
                for (int j = 0; j < cols; j++)
                {
                    int jj = j % segs;
                    D3 p = new D3(cx + r * sa[jj], yb + y, cz + r * ca[jj]);
                    D3 nr = new D3(cth * sa[jj] / a, sth / b, cth * ca[jj] / a).Normalized;
                    mb.Add(BeatMeshUtil.V(p), BeatMeshUtil.V(nr), new Vector2((float)(2.0 * Math.PI * r * j / segs), (float)v), col);
                }
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < segs; j++)
                {
                    int ia = start + i * cols + j, ib = start + (i + 1) * cols + j, ic = ib + 1, id = ia + 1;
                    mb.Quad(id, ic, ib, ia);   // MeshKit.Lathe winding (outside view clockwise)
                }
        }

        // ---------------------------------------------------------------- doors
        static D3 DoorLocal(DoorSpec d, double x, double y, double z)
        {
            double c = Math.Cos(d.azimuth), s = Math.Sin(d.azimuth);
            return new D3(ThreadConstants.CableAxisX + x * c + z * s, d.sillY + y, ThreadConstants.CableAxisZ - x * s + z * c);
        }

        static void LocalBox(MeshBuilder mb, DoorSpec d, double x0, double x1, double y0, double y1, double z0, double z1, double chamfer, Color col)
        {
            D3 c = DoorLocal(d, 0.5 * (x0 + x1), 0.5 * (y0 + y1), 0.5 * (z0 + z1));
            var rot = Quaternion.Euler(0f, (float)(d.azimuth / ThreadMath.Deg), 0f);
            MeshKit.ChamferedBox(mb, BeatMeshUtil.V(c), new Vector3((float)(x1 - x0), (float)(y1 - y0), (float)(z1 - z0)), (float)chamfer, rot, col);
        }

        /// <summary>The porch of a door (jambs, lintel, floor with ledge) in the bead's own material.</summary>
        public static void BuildDoorPorch(MeshBuilder mb, DoorSpec d)
        {
            double hw = 0.5 * ThreadConstants.DoorWidth, H = ThreadConstants.DoorHeight;
            double jo = hw + JambThickness;
            Color jamb = BeatMeshUtil.Ao(0.82), lintel = BeatMeshUtil.Ao(0.80), floor = BeatMeshUtil.Ao(0.90);
            LocalBox(mb, d, hw, jo, -JambBottomInFloor, H + JambTopInLintel, PorchBack, PorchFront, PorchChamfer, jamb);
            LocalBox(mb, d, -jo, -hw, -JambBottomInFloor, H + JambTopInLintel, PorchBack, PorchFront, PorchChamfer, jamb);
            LocalBox(mb, d, -jo - LintelOverhang, jo + LintelOverhang, H, H + LintelThickness, LintelBack, PorchFront + LintelProud, PorchChamfer, lintel);
            LocalBox(mb, d, -PorchHalfWidth, PorchHalfWidth, -FloorThickness, 0.0, FloorBack, PorchFront + LedgeDepth, PorchChamfer, floor);
        }

        /// <summary>The INK door leaf: its front face is the door plane, 0.4 m behind the porch front; its edges run into jambs, lintel and floor.</summary>
        public static void BuildDoorLeaf(MeshBuilder mb, DoorSpec d)
        {
            double hw = 0.5 * ThreadConstants.DoorWidth + LeafInset, H = ThreadConstants.DoorHeight;
            LocalBox(mb, d, -hw, hw, -LeafInset, H + LeafInset, DoorLeafFront - DoorLeafThickness, DoorLeafFront, 0.008, BeatMeshUtil.Ao(1.0));
        }

        // ---------------------------------------------------------------- windows
        /// <summary>Column-frame polyline of the faceted profile: rho_i = r_i cos(Delta / 2), y_i.</summary>
        static void ColumnPolyline(out double[] rho, out double[] y, out double[] u)
        {
            EnsureProfile();
            int n = SegmentsProfile;
            rho = new double[n + 1]; y = new double[n + 1]; u = new double[n + 1];
            double c = Math.Cos(0.5 * Delta);
            for (int i = 0; i <= n; i++) { rho[i] = s_r[i] * c; y[i] = s_y[i]; }
            for (int i = 1; i <= n; i++) { double dr = rho[i] - rho[i - 1], dy = y[i] - y[i - 1]; u[i] = u[i - 1] + Math.Sqrt(dr * dr + dy * dy); }
        }

        static void SegNormal(double[] rho, double[] y, int i, out double nr, out double ny)
        {
            double dr = rho[i + 1] - rho[i], dy = y[i + 1] - y[i], l = Math.Sqrt(dr * dr + dy * dy);
            nr = dy / l; ny = -dr / l;   // outward (to +rho, up on the upper half)
        }

        /// <summary>Column-frame radius of the faceted surface at a height relative to the bead centre.</summary>
        public static double ColumnRho(double yRel)
        {
            ColumnPolyline(out double[] rho, out double[] y, out double[] u);
            int n = SegmentsProfile, i = 0;
            while (i < n - 1 && yRel > y[i + 1]) i++;
            double t = (yRel - y[i]) / (y[i + 1] - y[i]);
            return rho[i] + (rho[i + 1] - rho[i]) * t;
        }

        /// <summary>
        /// Window mesh of a row in the facet-column frame (x tangential, y up from the bead centre, z = radial):
        /// a 0.9 m wide strip, 1.2 m long along the faceted profile, offset 0.02 m outward along the (mitered)
        /// facet normals. uv0 in [-1, 1] across and along the window.
        /// </summary>
        public static void BuildWindowRow(MeshBuilder mb, int row, out double yMin, out double yMax)
        {
            ColumnPolyline(out double[] rho, out double[] y, out double[] u);
            int n = SegmentsProfile;
            double yc = ThreadConstants.WindowRingOffsets[row];
            int ic = 0;
            while (ic < n - 1 && yc > y[ic + 1]) ic++;
            double uc = u[ic] + (u[ic + 1] - u[ic]) * (yc - y[ic]) / (y[ic + 1] - y[ic]);
            double hl = 0.5 * ThreadConstants.WindowH, hw = 0.5 * ThreadConstants.WindowW;
            double u0 = uc - hl, u1 = uc + hl;
            // strip stations: both ends plus every polyline vertex strictly inside
            var su = new List<double> { u0 };
            for (int i = 1; i < n; i++) if (u[i] > u0 + 1e-6 && u[i] < u1 - 1e-6) su.Add(u[i]);
            su.Add(u1);
            yMin = double.MaxValue; yMax = double.MinValue;
            int start = mb.VertexCount;
            for (int k = 0; k < su.Count; k++)
            {
                double uk = su[k];
                int seg = 0;
                while (seg < n - 1 && uk > u[seg + 1] + 1e-9) seg++;
                double f = (uk - u[seg]) / (u[seg + 1] - u[seg]);
                double pr = rho[seg] + (rho[seg + 1] - rho[seg]) * f, py = y[seg] + (y[seg + 1] - y[seg]) * f;
                double onr, ony, vnr, vny;
                bool vertex = k > 0 && k < su.Count - 1;
                if (vertex)
                {
                    // interior polyline vertex: the strip point sits at vertex index seg + 1 (f == 1) or seg (f == 0)
                    int vi = f > 0.5 ? seg + 1 : seg;
                    pr = rho[vi]; py = y[vi];
                    SegNormal(rho, y, vi - 1, out double ar, out double ay);
                    SegNormal(rho, y, vi, out double br, out double by);
                    double dot = ar * br + ay * by;
                    onr = (ar + br) / (1.0 + dot); ony = (ay + by) / (1.0 + dot);   // miter: exactly WindowOffset from both facets
                    double l = Math.Sqrt((ar + br) * (ar + br) + (ay + by) * (ay + by));
                    vnr = (ar + br) / l; vny = (ay + by) / l;
                }
                else
                {
                    SegNormal(rho, y, seg, out onr, out ony);
                    vnr = onr; vny = ony;
                }
                double ox = pr + onr * WindowOffset, oy = py + ony * WindowOffset;
                yMin = Math.Min(yMin, oy); yMax = Math.Max(yMax, oy);
                var nrm = new Vector3(0f, (float)vny, (float)vnr);
                float vv = (float)((uk - uc) / hl);
                mb.Add(new Vector3((float)-hw, (float)oy, (float)ox), nrm, new Vector2(-1f, vv), BeatMeshUtil.Ao(1.0));
                mb.Add(new Vector3((float)hw, (float)oy, (float)ox), nrm, new Vector2(1f, vv), BeatMeshUtil.Ao(1.0));
            }
            for (int k = 0; k < su.Count - 1; k++)
            {
                int a0 = start + 2 * k, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                BeatMeshUtil.QuadFacing(mb, a0, a1, b1, b0, mb.Normals[a0] + mb.Normals[b0]);
            }
        }

        /// <summary>
        /// Window placements of bead k, row `row` (48 positions minus those overlapped by a door porch or by an
        /// arm root; under D-035 there is neither, so every row keeps all 48). The eye-level row is centred on the
        /// reference azimuth (where a door exists it replaces that window), the other rows are staggered by half a
        /// step (3 facets); every window sits on the centre of its facet column.
        /// </summary>
        public static List<WindowPlacement> Windows(int k, int row, double rowYMin, double rowYMax)
        {
            var list = new List<WindowPlacement>(ThreadConstants.WindowsPerRing);
            double aRef = ReferenceAzimuth(k), a0 = Alpha0(k), yb = BeadY(k);
            double yc = ThreadConstants.WindowRingOffsets[row];
            double rhoC = ColumnRho(yc);
            double stagger = row == EyeRow ? 0.0 : 0.5 * WindowStep;
            double wHalfAng = 0.5 * ThreadConstants.WindowW / rhoC;
            double wy0 = yb + rowYMin, wy1 = yb + rowYMax;
            var arms = WalkwayLayout.Arms();
            DoorSpec door = HasDoor(k) ? Door(k) : default(DoorSpec);
            for (int m = 0; m < ThreadConstants.WindowsPerRing; m++)
            {
                double aw = aRef + stagger + m * WindowStep;
                if (HasDoor(k))
                {
                    double py0 = door.sillY - FloorThickness, py1 = door.sillY + ThreadConstants.DoorHeight + LintelThickness;
                    double pHalf = (PorchHalfWidth + WindowArmMargin) / ThreadConstants.BeadRadius;
                    if (Math.Abs(BeatMeshUtil.AngDiff(aw, door.azimuth)) < pHalf + wHalfAng && wy1 > py0 - WindowArmMargin && wy0 < py1 + WindowArmMargin)
                        continue;
                }
                bool hit = false;
                for (int i = 0; i < arms.Length && !hit; i++)
                {
                    var a = arms[i];
                    double ay1 = a.yTop, ay0 = a.yTop - a.depthRoot;
                    if (wy1 < ay0 - WindowArmMargin || wy0 > ay1 + WindowArmMargin) continue;
                    double armAz = BeatMeshUtil.Azimuth(a.dirX, a.dirZ);
                    double aHalf = (0.5 * a.widthRoot + WindowArmMargin) / Math.Max(1.0, a.rRoot);
                    if (Math.Abs(BeatMeshUtil.AngDiff(aw, armAz)) < aHalf + wHalfAng) hit = true;
                }
                if (hit) continue;
                double rel = BeatMeshUtil.Wrap2Pi(aw - a0);
                int col = (int)Math.Floor(rel / Delta) % SegmentsAzimuth;
                double am = a0 + (col + 0.5) * Delta;
                double dl = BeatMeshUtil.AngDiff(aw, am);
                double t = rhoC * Math.Tan(dl);
                double c = Math.Cos(am), s = Math.Sin(am);
                var pos = new Vector3((float)(ThreadConstants.CableAxisX + t * c), (float)yb, (float)(ThreadConstants.CableAxisZ - t * s));
                var m4 = Matrix4x4.TRS(pos, Quaternion.Euler(0f, (float)(am / ThreadMath.Deg), 0f), Vector3.one);
                list.Add(new WindowPlacement { matrix = m4, azimuth = aw, t = t, column = col });
            }
            return list;
        }
    }
}
