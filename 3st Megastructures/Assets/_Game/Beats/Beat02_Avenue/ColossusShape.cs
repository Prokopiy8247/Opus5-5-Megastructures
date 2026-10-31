// ColossusShape: the pure, double-precision definition of one colossus of the avenue (MASTER_PROMPT 14.5.2 a-e).
// No UnityEngine dependency: the mesh builder (ColossusBuilder), the ground-shadow generator (ColossusShadowShape)
// and the EditMode tests evaluate the same functions, so the robe, its ground shadow, the sleeve clearance and
// the door frame can never drift apart.
//
// Frames
//  * local : colossus frame before the yaw. Origin on the robe axis at ground level, +Y up, +Z the way the figure
//            faces (toward the cable), +X its left-to-right axis.
//  * root  : local rotated by the yaw about +Y. The GameObject root sits at (Cx, 0, Cz) with identity rotation.
//  * world : root + (Cx, 0, Cz).
// Heights. Profile height eta (0..300, the nominal 300 m figure of 14.5.2 a) maps to the world height y = Y(eta):
// the 6 m hem band is never scaled (it is a scale cue like the door), above it heights scale by
// Kk = (h - 6) / (300 - 6), so the unbent hood apex sits exactly at the seeded height h (14.5.2 d).
// Angles. theta is the lathe angle measured from +Z toward +X; a robe point is
// ((r + d) Sx sin theta, y, (r + d) Sz cos theta) with Sx = 1 and Sz = 18 / 22 (footprint 22 x 18, x : z).
using System;
using System.Collections.Generic;

namespace Megastructure.Beats
{
    /// <summary>
    /// Numbers of 14.5.2 that ThreadConstants does not carry yet (listed as a contract request in the B04 report).
    /// Unless noted, each value is from MASTER_PROMPT.md line 660 (robe, hood, arms, asymmetry, foot detail) or
    /// line 661 (ground shadow); everything that ThreadConstants does carry is read from there.
    /// </summary>
    public static class ColossusSpec
    {
        public const int Segments = 72;                       // "72 radial segments" (660 a)
        public const int DoorColumn = Segments / 2;           // the door facet lies between columns 36 and 37, the seam (column 0) faces away from the path

        // robe profile (eta, r) from the hem top to the shoulder crease (660 a); the base point (0, 22) is ThreadConstants.ColossusFootprintX
        public static readonly double[] BodyEta = { 6, 40, 80, 120, 160, 190, 215 };
        public static readonly double[] BodyR = { 21.2, 19.5, 17.2, 15.5, 14.0, 15.5, 18.5 };
        // shoulder slope to the neck ring (660 a: shoulders at y 215, neck at y 238)
        public static readonly double[] ShoulderEta = { 215, 228, 238 };
        public static readonly double[] ShoulderR = { 18.5, 14.0, 8.0 };
        // hood egg profile, welded on the neck ring (660 b)
        public static readonly double[] HoodEta = { 238, 246, 256, 275, 290, 300 };
        public static readonly double[] HoodR = { 8, 11.5, 12, 10, 6, 0 };

        public const int FluteCount = 24;                     // 24 vertical flutes (660 a)
        public const double FluteAmp = 0.9;                   // amplitude at the hem (660 a)
        public const double FluteTopEta = 150;                // flutes fade to 0 at y 150 (660 a)
        // the flutes and the footprint wobble are faded out around the door so the inner foot edge is exactly
        // ThreadConstants.ColossusInnerEdgeX and the doorway sits in a plain panel (design choice, 660 e)
        public const double FluteWindow0Deg = 12, FluteWindow1Deg = 30;
        public const double WobbleAmp = 1.5;                  // "+-1.5 m seeded wobble" of the footprint (660 a)
        public const double WobbleTopY = 40;                  // the footprint wobble pools at the base and fades out by 40 m (design choice)
        public const double WobbleWindow0Deg = 15, WobbleWindow1Deg = 60;
        public const double ProfileJitter = 0.5;              // seeded variation of the waist / shoulder knots (14.5.2 d asymmetry; below eta 80 nothing varies)
        // light raised frame around the doorway (critique run07 A15; design, never scaled): face width and how far it
        // stands proud of the robe wall (m)
        public const double DoorFrameWidth = 0.14, DoorFrameDepth = 0.12;

        // 12 sections (660 c); 48 sides instead of 24: critique run07 A-r2-3 / B-r2-6 / E-r2-4 (the 24-gon outlines of the
        // sleeve domes and cuffs showed corners in the look-ups). From anywhere on the walkable corridor the closest cuff
        // is >= 66 m away (<= 185 px radius at 4K), so the chord sagitta of a 48-gon stays under 0.4 px.
        public const int SleeveSides = 48, SleeveSections = 12;
        public const double SleeveLength = 150, SleeveR0 = 9, SleeveR1 = 6;     // (660 c)
        public const double SleeveAttachOffset = 17.5, SleeveCapSink = 3;      // (660 c)
        public const double SleeveGap = 1.0, SleeveGapMargin = 0.2;            // 1.0 m gap below the shoulder (660 c) + safety margin between sections
        public const double SleeveBlendEta = 200;                              // blend from the attachment offset to c(y) by y 200 (660 c)
        public const double ElbowEta = 140, ElbowBendDeg = 25;                 // inner sleeve bent forward at the elbow (660 c)
        public const double SleeveDomeFlat = 0.6;                              // flattened dome closing the sleeve top inside the shoulder (design)
        /// <summary>Dome rings above the top section (polar angle, deg; 90 = the closed apex). Shared by the mesh and its ground shadow.</summary>
        public static readonly double[] SleeveDomeDeg = { 90, 72, 50, 25 };

        // Closed cloth cuff (critique run06 C-10, E7, A6, PAC-05: the open loft end with its INK interior read as a
        // pipe mouth / cannon bore). Rings beyond the last loft section along its tangent: axial distance (m, not
        // scaled with k, like the sleeve radius) and radius as a multiple of the cuff radius: a slightly flared hem
        // (x 1.16), a lip rolling under it into a soft groove, then a deep gathered dome closing the end (no hole,
        // robe material only). Critique run07 A-r2-3 / B-r2-6: the 1.35 m deep dome of D-040 read end-on as a flat
        // pipe cap; the dome is now 2.4 m deep (0.41 of its base radius, a half ellipse sampled at 12-82 deg) and
        // gathered into CuffFolds pleats that converge on the pole, so it shades as cloth from every side.
        public static readonly double[] CuffAxial = { 1.2, 2.3, 2.85, 3.2, 3.35, 3.85, 4.48, 5.05, 5.43, 5.63, 5.73, 5.75 };
        public static readonly double[] CuffRadius = { 1.07, 1.16, 1.15, 1.07, 0.97, 0.95, 0.86, 0.69, 0.49, 0.30, 0.13, 0.0 };
        /// <summary>Pleat height per cuff ring (m, along the profile normal): 0 on the hem and the lip, highest mid-dome, 0 at the pole.</summary>
        public static readonly double[] CuffFold = { 0, 0, 0, 0, 0, 0.07, 0.14, 0.17, 0.14, 0.07, 0.02, 0 };
        /// <summary>Baked AO per cuff ring: the groove where the hem rolls under, the dome a little darker toward the gathered pole.</summary>
        public static readonly double[] CuffAo = { 1.0, 1.0, 0.95, 0.80, 0.62, 0.74, 0.84, 0.88, 0.86, 0.82, 0.78, 0.76 };
        public const int CuffFolds = 12;            // pleats around the dome (4 loft columns each)
        public const double CuffFoldAo = 0.12;      // extra AO in a pleat valley at the highest pleat
        /// <summary>Index of the widest cuff ring; the outward shift that keeps the gap to the robe is complete from here on.</summary>
        public const int CuffHemRing = 1;

        public const double YawMinDeg = 4, YawMaxDeg = 9;      // (660 d)
        public const double TiltMinDeg = 2, TiltMaxDeg = 5;    // (660 d)
        // Forward bow of the head (critique run06 E6, PAC-05: the hoods must read from below); composed with the
        // avenue-ward tilt of 660 d, which keeps its 2-5 deg in the X-Y plane. Critique run07 (A6, E6, C-04, PAC-05):
        // with the 4-6 deg bow of D-040 no face plate read from the avenue floor. The watchers now bow 19-21 deg and
        // turn their faces 28-32 deg toward the path (local facing azimuth, before the 4-9 deg yaw), so the blank
        // plate of a pair the walker has just passed looks down at the path (look-up from s 206: the pair-2 plates and
        // INK walls cover ~3 000 px at 1080p, 6.6 times the D-040 bow and 1.7 times a pure 16 deg bow along the lean).
        // Both drawn last from the seed, so every earlier seeded value is unchanged.
        public const double NodMinDeg = 19, NodMaxDeg = 21;
        public const double FaceTurnMinDeg = 28, FaceTurnMaxDeg = 32;

        public const double FaceRecess = 1.2;                  // face plate recessed 1.2 m behind the hood surface (660 b)
        // oval hood opening (design; enlarged after critique run06 E6 / PAC-05 so the blank face reads)
        public const double FaceEta = 262, FaceHalfEta = 13, FaceHalfAngleDeg = 44;
        /// <summary>Margins of the hood-grid window around the opening (deg, profile m): the band the brim rises over.</summary>
        public const double FaceMarginDeg = 6, FaceMarginEta = 2.5;
        // Brim (critique run06 E6, PAC-05, C-04): the edge of the opening stands forward of the hood surface by
        // BrimBase + BrimSin sin(w) + BrimSin2 sin^2(w) metres (w = angle around the oval, +90 deg at the top): 4 m over
        // the brow, 2 m at the sides, 1 m at the chin. The lip rolls back by BrimLipRoll into the opening; the INK
        // recess walls run from there to the SAND_SHADE plate, which is BrimPlateScale of the opening, so a dark ring
        // frames the lighter blank plate from every angle.
        public const double BrimBase = 2.0, BrimSin = 1.5, BrimSin2 = 0.5;
        public const double BrimCrestFrac = 0.65, BrimLipRoll = 0.5, BrimLipScale = 0.93, BrimPlateScale = 0.78;

        public const double ShadowPenumbra0 = 0.35, ShadowPenumbra1 = 6.0;   // soft edge 0.35 m at the foot, 6 m at 674 m (661 f)
        // Critique run06 C-05 / E8 (the far shadow edges read as a second horizon): the soft edge also grows with the
        // square of the distance (+24 m at 674 m, 30 m in all), and the shade fades to zero over the last 40 percent of
        // each shadow's length, so no shadow ends in a hard line near the horizon.
        public const double ShadowPenumbraFar = 24.0;
        public const double ShadowLengthFade0 = 0.6;

        // seeded asymmetry tables: heights and yaws are drawn as a seeded permutation of evenly spread values so
        // all eight differ (no two robes alike) and the ranges of 660 d are covered
        public const uint AvenueSeed = 0x7A11E5u;
        public const double HeightJitter = 0.8, YawJitterDeg = 0.2;
        /// <summary>Upper bound of the crown drop caused by the 19-21 deg head bow (m; measured 3.4-4.4 m over the eight figures).</summary>
        public const double BowCrownDrop = 5.0;

        /// <summary>Per-colossus seed (index = pair * 2 + side); all eight differ (660 d: no two robes share a seed).</summary>
        public static uint Seed(int index) => ((uint)(index + 1) * 0x9E3779B1u) ^ 0x0C0105B4u;

        /// <summary>Plan distance at which the ground-shadow sun is sampled: the middle of beat 2 (elevation 24 deg, 661 f).</summary>
        public static double ShadowSunS => 0.5 * (ThreadConstants.BeatBoundaries[1] + ThreadConstants.BeatBoundaries[2]);

        static int[] s_heightSlot, s_yawSlot;

        /// <summary>Base height (m) and yaw magnitude (deg) of colossus index 0..7 before its own seeded jitter.</summary>
        public static void SlotValues(int index, out double height, out double yawDeg)
        {
            if (s_heightSlot == null)
            {
                var rng = new ColossusRng(AvenueSeed);
                s_heightSlot = Permutation(ref rng, 8);
                s_yawSlot = Permutation(ref rng, 8);
            }
            int i = ((index % 8) + 8) % 8;
            // the bowed head lowers the crown below the unbent apex by up to BowCrownDrop, so the lowest slot sits that
            // much higher: the generated figures' tops stay inside 282-318 m (acceptance 1)
            double hMin = ThreadConstants.ColossusHeightMin + 1.0 + BowCrownDrop, hMax = ThreadConstants.ColossusHeightMax - 3.0;
            double yMin = YawMinDeg + 0.25, yMax = YawMaxDeg - 0.25;
            height = hMin + (hMax - hMin) * s_heightSlot[i] / 7.0;
            yawDeg = yMin + (yMax - yMin) * s_yawSlot[i] / 7.0;
        }

        static int[] Permutation(ref ColossusRng rng, int n)
        {
            var p = new int[n];
            for (int i = 0; i < n; i++) p[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = (int)(rng.Next() % (uint)(i + 1));
                int t = p[i]; p[i] = p[j]; p[j] = t;
            }
            return p;
        }
    }

    /// <summary>xorshift32 (deterministic, platform independent).</summary>
    public struct ColossusRng
    {
        uint m_s;
        public ColossusRng(uint seed)
        {
            m_s = seed != 0 ? seed : 0x9E3779B9u;
            for (int i = 0; i < 4; i++) Next();
        }
        public uint Next()
        {
            uint x = m_s;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            m_s = x;
            return x;
        }
        public double U01() => (Next() >> 8) * (1.0 / 16777216.0);
        public double Range(double a, double b) => a + (b - a) * U01();
    }

    /// <summary>Doorway frame on the inner face (world coordinates, 14.5.2 e).</summary>
    public struct DoorFrame
    {
        public D3 O;            // chord midpoint of the door facet at ground level
        public D3 H;            // horizontal chord direction (from column DoorColumn toward DoorColumn + 1)
        public D3 Nh;           // horizontal outward normal (toward the path)
        public D3 U;            // in-plane up direction (leans with the hem wall)
        public D3 N;            // outward facet normal
        public double Sill;     // threshold height (3 ledge risers)
        public double Lintel;   // Sill + DoorHeight
        public D3 DL0, DR0, DL1, DR1;   // opening corners; L = column DoorColumn side, 0 = sill, 1 = lintel

        /// <summary>Point of the facet plane at a lateral offset along H and a world height y.</summary>
        public D3 At(double lateral, double y) => O + H * lateral + U * (y / U.y);
        /// <summary>Horizontal offset of the wall plane at height y relative to O along Nh (negative: the wall leans in).</summary>
        public double WallOffset(double y) => D3.Dot(At(0, y) - O, Nh);
    }

    /// <summary>Centre line, frames and radii of one sleeve (local frame, world heights).</summary>
    public sealed class SleeveShape
    {
        public bool Bent;          // inner sleeve, bent forward at the elbow
        public double SideX;       // local x sign of the sleeve
        public D3[] C;             // section centres (index 0 = top inside the shoulder, last = cuff)
        public D3[] T;             // unit tangents, pointing from the shoulder toward the cuff
        public D3[] N, B;          // section frames (N in the local XZ-ish plane, B = T x N)
        public double[] R;         // section radii
        public double[] Gap;       // clearance of each section to the robe envelope at its height (m)
        public int Count => C.Length;

        /// <summary>Closed cuff rings beyond the last section (ColossusSpec.CuffAxial; the last one is the dome pole, radius 0).</summary>
        public D3[] CuffC;
        public double[] CuffR;
        /// <summary>Smallest clearance of each cuff ring (surface samples) to the robe envelope at their own heights (m).</summary>
        public double[] CuffGap;
        /// <summary>Horizontal outward shift of the hem rings that keeps the flared cuff clear of the robe (m).</summary>
        public double CuffShift;
        /// <summary>Pleat displacement per cuff ring along the cuff axis and along the ring radius (m, times cos(CuffFolds phase)).</summary>
        public double[] CuffFoldA, CuffFoldR;

        /// <summary>Index of the first cuff ring in the loft ring numbering.</summary>
        public int CuffStart => ColossusSpec.SleeveDomeDeg.Length + C.Length;

        /// <summary>Loft rings: the dome rings (ColossusSpec.SleeveDomeDeg, ring 0 = the apex point), the sections, then the closed cuff (last ring = its pole).</summary>
        public int RingCount => CuffStart + (CuffC != null ? CuffC.Length : 0);

        /// <summary>Centre, radius and cross-section axes of loft ring i (local frame); the mesh and the shadow both use it.</summary>
        public void Ring(int i, out D3 centre, out double radius, out D3 n, out D3 b)
        {
            int nd = ColossusSpec.SleeveDomeDeg.Length;
            if (i < nd)
            {
                double a = ColossusSpec.SleeveDomeDeg[i] * ThreadMath.Deg;
                centre = C[0] - T[0] * (R[0] * Math.Sin(a) * ColossusSpec.SleeveDomeFlat);
                radius = i == 0 ? 0.0 : R[0] * Math.Cos(a);
                n = N[0]; b = B[0];
                return;
            }
            int k = i - nd;
            if (k >= C.Length)
            {
                int q = k - C.Length, last = C.Length - 1;
                centre = CuffC[q]; radius = CuffR[q]; n = N[last]; b = B[last];
                return;
            }
            centre = C[k]; radius = R[k]; n = N[k]; b = B[k];
        }

        /// <summary>Surface point of loft ring i at the angle phase around its centre (local frame); the cuff dome carries its pleats.</summary>
        public D3 RingPoint(int i, double phase)
        {
            Ring(i, out D3 c, out double r, out D3 n, out D3 b);
            if (r <= 0.0) return c;
            D3 radial = n * Math.Cos(phase) + b * Math.Sin(phase);
            int q = i - CuffStart;
            if (q < 0 || CuffFoldA == null) return c + radial * r;
            double w = FoldWave(phase);
            return c + radial * (r + CuffFoldR[q] * w) + T[C.Length - 1] * (CuffFoldA[q] * w);
        }

        /// <summary>Pleat wave around the cuff dome in [-1, 1]: ridges at 1, valleys at -1 (crests fall on loft columns).</summary>
        public static double FoldWave(double phase) => Math.Cos(ColossusSpec.CuffFolds * phase);
    }

    public sealed class ColossusShape
    {
        public readonly int Pair, Side, Index;
        public readonly uint Seed;
        /// <summary>+1 for the left row (inner face toward +X), -1 for the right row.</summary>
        public readonly double InnerSign;
        public readonly double Height;     // world height of the unbent hood apex
        public readonly double Kk;         // vertical scale above the hem band
        public readonly double Yaw;        // radians (signed, toward the path)
        public readonly double Tilt;       // radians (unsigned, toward the avenue; measured in the local X-Y plane)
        public readonly double Nod;        // radians (forward bow toward +Z local; measured in the local Y-Z plane)
        public readonly double FaceTurn;   // radians (unsigned): horizontal facing of the face opening toward the path, local frame
        /// <summary>Lathe angle of the face opening's centre (radians): the angle whose outward normal has the FaceTurn azimuth.</summary>
        public readonly double FaceTheta;
        public readonly double Sx, Sz;
        public readonly double ThetaDoor, Theta0, DTheta;
        public readonly double Cx, Cz;
        public readonly DoorFrame Door;
        /// <summary>[0] outer straight sleeve, [1] inner sleeve bent forward (14.5.2 c).</summary>
        public readonly SleeveShape[] Sleeves;

        readonly double[] m_bodyR, m_bodyM, m_shR, m_shM;
        readonly double[] m_wc = new double[3], m_wp = new double[3];
        readonly double m_wNorm, m_cosYaw, m_sinYaw;
        readonly double[] m_hoodMe = new double[6], m_hoodMr = new double[6];
        // head bend: one rotation by m_bendTotal about the horizontal axis (m_kx, 0, m_kz) through the neck pivot
        readonly double m_bendTotal, m_kx, m_kz;
        readonly D3 m_faceDir;

        public ColossusShape(int pair, int side) : this(pair, side, ColossusSpec.Seed(pair * 2 + side)) { }

        public ColossusShape(int pair, int side, uint seed)
        {
            Pair = pair; Side = side; Index = pair * 2 + side; Seed = seed;
            InnerSign = side == 0 ? 1.0 : -1.0;
            var rng = new ColossusRng(seed);

            ColossusSpec.SlotValues(Index, out double hSlot, out double yawSlot);
            Height = Clamp(hSlot + rng.Range(-ColossusSpec.HeightJitter, ColossusSpec.HeightJitter),
                           ThreadConstants.ColossusHeightMin + 1.0, ThreadConstants.ColossusHeightMax - 1.0);
            Kk = (Height - ThreadConstants.HemBand) / (ThreadConstants.ColossusNominalHeight - ThreadConstants.HemBand);
            double yawDeg = Clamp(yawSlot + rng.Range(-ColossusSpec.YawJitterDeg, ColossusSpec.YawJitterDeg), ColossusSpec.YawMinDeg, ColossusSpec.YawMaxDeg);
            Yaw = InnerSign * yawDeg * ThreadMath.Deg;
            Tilt = rng.Range(ColossusSpec.TiltMinDeg + 0.3, ColossusSpec.TiltMaxDeg - 0.3) * ThreadMath.Deg;
            Sx = 1.0;
            Sz = ThreadConstants.ColossusFootprintZ / ThreadConstants.ColossusFootprintX;

            // profile with seeded waist / shoulder variation (knots at eta >= 120 only)
            m_bodyR = (double[])ColossusSpec.BodyR.Clone();
            for (int k = 3; k < m_bodyR.Length; k++) m_bodyR[k] += rng.Range(-ColossusSpec.ProfileJitter, ColossusSpec.ProfileJitter);
            m_bodyM = Tangents(ColossusSpec.BodyEta, m_bodyR);
            m_shR = new[] { m_bodyR[m_bodyR.Length - 1], ColossusSpec.ShoulderR[1], ColossusSpec.ShoulderR[2] };
            m_shM = Tangents(ColossusSpec.ShoulderEta, m_shR);

            // footprint wobble: harmonics 2..4 with seeded weights and phases
            double norm = 0;
            for (int k = 0; k < 3; k++)
            {
                double w = rng.Range(0.35, 1.0) * (rng.U01() < 0.5 ? -1.0 : 1.0);
                m_wc[k] = w; m_wp[k] = rng.Range(0, 2 * Math.PI);
                norm += Math.Abs(w);
            }
            m_wNorm = norm > 1e-9 ? 1.0 / norm : 0.0;

            // forward bow, drawn last so every earlier seeded value is unchanged. The lean direction l (horizontal) has
            // tan(tilt) across and tan(nod) forward, so the apex keeps exactly the tilt in X-Y and the nod in Y-Z.
            Nod = rng.Range(ColossusSpec.NodMinDeg, ColossusSpec.NodMaxDeg) * ThreadMath.Deg;
            double lx = InnerSign * Math.Tan(Tilt), lz = Math.Tan(Nod), ll = Math.Sqrt(lx * lx + lz * lz);
            m_bendTotal = Math.Atan(ll);
            m_kx = lz / ll; m_kz = -lx / ll;     // k = Y x l
            // the face turns toward the path: the lathe angle whose (elliptical) outward normal has the azimuth
            // FaceTurn, and the face direction is that horizontal normal bent with the head (HeadForward)
            FaceTurn = rng.Range(ColossusSpec.FaceTurnMinDeg, ColossusSpec.FaceTurnMaxDeg) * ThreadMath.Deg;
            double fx = InnerSign * Math.Sin(FaceTurn), fz = Math.Cos(FaceTurn);
            FaceTheta = Math.Atan2(fx * Sx, fz * Sz);
            m_faceDir = new D3(fx, 0.0, fz);

            // hood curve tangents (2D Catmull-Rom; horizontal approach at the apex for a round egg top)
            var ke = ColossusSpec.HoodEta; var kr = ColossusSpec.HoodR;
            m_hoodMe[0] = ke[1] - ke[0]; m_hoodMr[0] = kr[1] - kr[0];
            for (int i = 1; i < 5; i++) { m_hoodMe[i] = 0.5 * (ke[i + 1] - ke[i - 1]); m_hoodMr[i] = 0.5 * (kr[i + 1] - kr[i - 1]); }
            m_hoodMe[5] = 0.0;
            m_hoodMr[5] = -Math.Sqrt((ke[5] - ke[4]) * (ke[5] - ke[4]) + (kr[5] - kr[4]) * (kr[5] - kr[4]));

            m_cosYaw = Math.Cos(Yaw); m_sinYaw = Math.Sin(Yaw);
            DTheta = 2.0 * Math.PI / ColossusSpec.Segments;
            // lathe angle whose outward normal points at the path after the yaw (the inner foot extreme)
            ThetaDoor = Math.Atan2(InnerSign * m_cosYaw / Sz, InnerSign * m_sinYaw / Sx);
            Theta0 = ThetaDoor - (ColossusSpec.DoorColumn + 0.5) * DTheta;

            // centre: the door facet passes through x = -/+ InnerEdgeX at ground level (T-SCALE-2)
            Cz = ThreadConstants.ColossusPairZ[pair];
            Cx = 0.0;
            D3 pa = LocalToRoot(RobeLocal(ColumnTheta(ColossusSpec.DoorColumn), 0.0, false));
            D3 pb = LocalToRoot(RobeLocal(ColumnTheta(ColossusSpec.DoorColumn + 1), 0.0, false));
            Cx = -InnerSign * ThreadConstants.ColossusInnerEdgeX - 0.5 * (pa.x + pb.x);

            Door = BuildDoor();
            Sleeves = new[] { BuildSleeve(false, -InnerSign), BuildSleeve(true, InnerSign) };
        }

        // ------------------------------------------------------------------ helpers
        public static double Clamp(double x, double a, double b) => x < a ? a : (x > b ? b : x);

        /// <summary>Wraps an angle difference into (-pi, pi].</summary>
        public static double AngDiff(double a, double b)
        {
            double d = (a - b) % (2.0 * Math.PI);
            if (d > Math.PI) d -= 2.0 * Math.PI;
            if (d <= -Math.PI) d += 2.0 * Math.PI;
            return d;
        }

        static double[] Tangents(double[] xs, double[] ys)
        {
            int n = xs.Length;
            var m = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (i == 0) m[i] = (ys[1] - ys[0]) / (xs[1] - xs[0]);
                else if (i == n - 1) m[i] = (ys[n - 1] - ys[n - 2]) / (xs[n - 1] - xs[n - 2]);
                else m[i] = (ys[i + 1] - ys[i - 1]) / (xs[i + 1] - xs[i - 1]);
            }
            return m;
        }

        static double Hermite(double[] xs, double[] ys, double[] ms, double x)
        {
            int n = xs.Length;
            if (x <= xs[0]) return ys[0] + ms[0] * (x - xs[0]);
            if (x >= xs[n - 1]) return ys[n - 1] + ms[n - 1] * (x - xs[n - 1]);
            int i = 0;
            while (i < n - 2 && x > xs[i + 1]) i++;
            double h = xs[i + 1] - xs[i], t = (x - xs[i]) / h, t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * ys[i] + (t3 - 2 * t2 + t) * h * ms[i] + (-2 * t3 + 3 * t2) * ys[i + 1] + (t3 - t2) * h * ms[i + 1];
        }

        // ------------------------------------------------------------------ heights
        public double Y(double eta) => eta <= ThreadConstants.HemBand ? eta : ThreadConstants.HemBand + (eta - ThreadConstants.HemBand) * Kk;
        public double Eta(double y) => y <= ThreadConstants.HemBand ? y : ThreadConstants.HemBand + (y - ThreadConstants.HemBand) / Kk;
        public double ColumnTheta(int j) => Theta0 + j * DTheta;

        // ------------------------------------------------------------------ robe profile and deformation
        /// <summary>Profile radius r(eta). run 0 = hem band, 1 = body, 2 = shoulder slope; -1 selects by eta.</summary>
        public double ProfileR(double eta, int run = -1)
        {
            if (run < 0) run = eta <= ThreadConstants.HemBand ? 0 : (eta <= ColossusSpec.ShoulderEta[0] ? 1 : 2);
            if (run == 0)
            {
                double r0 = ThreadConstants.ColossusFootprintX, r1 = ColossusSpec.BodyR[0];
                return r0 + (r1 - r0) * eta / ThreadConstants.HemBand;
            }
            if (run == 1) return Hermite(ColossusSpec.BodyEta, m_bodyR, m_bodyM, eta);
            return Hermite(ColossusSpec.ShoulderEta, m_shR, m_shM, Math.Min(eta, ColossusSpec.ShoulderEta[2]));
        }

        /// <summary>Flute amplitude at world height y: 0.9 m on the hem band, smoothly 0 at y(150).</summary>
        public double FluteAmplitude(double y) =>
            ColossusSpec.FluteAmp * (1.0 - ThreadMath.Smoothstep(ThreadConstants.HemBand, Y(ColossusSpec.FluteTopEta), y));

        /// <summary>0 in a plain panel around the door, 1 elsewhere.</summary>
        public double FluteWindow(double theta) =>
            ThreadMath.Smoothstep(ColossusSpec.FluteWindow0Deg * ThreadMath.Deg, ColossusSpec.FluteWindow1Deg * ThreadMath.Deg, Math.Abs(AngDiff(theta, ThetaDoor)));

        /// <summary>Flute wave in [-1, 1]; crests fall on lathe columns (3 columns per flute: a ridge and a flat valley).</summary>
        public double FluteWave(double theta) =>
            Math.Cos(ColossusSpec.FluteCount * (theta - ThetaDoor) + ColossusSpec.FluteCount * DTheta * 0.5);

        public double Wobble(double theta, double y)
        {
            double f = 1.0 - ThreadMath.Smoothstep(0.0, ColossusSpec.WobbleTopY, y);
            if (f <= 0.0) return 0.0;
            double dth = AngDiff(theta, ThetaDoor);
            double win = ThreadMath.Smoothstep(ColossusSpec.WobbleWindow0Deg * ThreadMath.Deg, ColossusSpec.WobbleWindow1Deg * ThreadMath.Deg, Math.Abs(dth));
            if (win <= 0.0) return 0.0;
            double s = m_wc[0] * Math.Cos(2 * dth + m_wp[0]) + m_wc[1] * Math.Cos(3 * dth + m_wp[1]) + m_wc[2] * Math.Cos(4 * dth + m_wp[2]);
            return ColossusSpec.WobbleAmp * f * win * s * m_wNorm;
        }

        /// <summary>Radial deformation of the mesh (flutes + wobble).</summary>
        public double Deform(double theta, double y) => FluteAmplitude(y) * FluteWindow(theta) * FluteWave(theta) + Wobble(theta, y);

        /// <summary>Outer envelope of the deformation (flute crests + wobble): used by the shadow outline and the sleeve clearance.</summary>
        public double DeformEnvelope(double theta, double y) => FluteAmplitude(y) * FluteWindow(theta) + Wobble(theta, y);

        /// <summary>Robe surface point (local frame) at lathe angle theta and profile height eta.</summary>
        public D3 RobeLocal(double theta, double eta, bool envelope, int run = -1)
        {
            double y = Y(eta);
            double r = ProfileR(eta, run) + (envelope ? DeformEnvelope(theta, y) : Deform(theta, y));
            return new D3(r * Sx * Math.Sin(theta), y, r * Sz * Math.Cos(theta));
        }

        // ------------------------------------------------------------------ frames
        public D3 LocalToRoot(D3 p) => new D3(p.x * m_cosYaw + p.z * m_sinYaw, p.y, -p.x * m_sinYaw + p.z * m_cosYaw);
        public D3 RootToLocal(D3 p) => new D3(p.x * m_cosYaw - p.z * m_sinYaw, p.y, p.x * m_sinYaw + p.z * m_cosYaw);
        public D3 LocalToWorld(D3 p) { D3 r = LocalToRoot(p); return new D3(r.x + Cx, r.y, r.z + Cz); }
        public D3 WorldToLocal(D3 p) => RootToLocal(new D3(p.x - Cx, p.y, p.z - Cz));
        public D3 RootPosition => new D3(Cx, 0, Cz);

        // ------------------------------------------------------------------ hood
        /// <summary>Hood egg profile at curve parameter t in [0, 5] (one unit per knot interval).</summary>
        public void HoodCurve(double t, out double eta, out double r)
        {
            var ke = ColossusSpec.HoodEta; var kr = ColossusSpec.HoodR;
            int i = (int)Math.Floor(t);
            if (i < 0) i = 0;
            if (i > 4) i = 4;
            double u = Clamp(t - i, 0.0, 1.0), u2 = u * u, u3 = u2 * u;
            double h00 = 2 * u3 - 3 * u2 + 1, h10 = u3 - 2 * u2 + u, h01 = -2 * u3 + 3 * u2, h11 = u3 - u2;
            eta = h00 * ke[i] + h10 * m_hoodMe[i] + h01 * ke[i + 1] + h11 * m_hoodMe[i + 1];
            r = Math.Max(0.0, h00 * kr[i] + h10 * m_hoodMr[i] + h01 * kr[i + 1] + h11 * m_hoodMr[i + 1]);
            if (t >= 5.0) { eta = ke[5]; r = 0.0; }
        }

        /// <summary>Inverse of the (monotone) hood curve height.</summary>
        public double HoodT(double eta)
        {
            double lo = 0.0, hi = 5.0;
            for (int it = 0; it < 60; it++)
            {
                double mid = 0.5 * (lo + hi);
                HoodCurve(mid, out double e, out _);
                if (e < eta) lo = mid; else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>Hood surface point before the head tilt (local frame).</summary>
        public D3 HoodLocalUnbent(double theta, double t)
        {
            HoodCurve(t, out double eta, out double r);
            return new D3(r * Sx * Math.Sin(theta), Y(eta), r * Sz * Math.Cos(theta));
        }

        /// <summary>Outward unit normal of the unbent hood surface (central differences in theta and t).</summary>
        public D3 HoodNormalUnbent(double theta, double t)
        {
            const double e = 1e-4;
            D3 dTh = HoodLocalUnbent(theta + e, t) - HoodLocalUnbent(theta - e, t);
            double t0 = Math.Max(0.0, t - e), t1 = Math.Min(5.0, t + e);
            D3 dT = HoodLocalUnbent(theta, t1) - HoodLocalUnbent(theta, t0);
            D3 n = D3.Cross(dTh, dT);
            if (n.Length < 1e-12) return new D3(0, 1, 0);
            n = n.Normalized;
            D3 p = HoodLocalUnbent(theta, t);
            D3 radial = new D3(p.x, 0, p.z);
            if (D3.Dot(n, radial) < 0 && radial.Length > 1e-6) n = n * -1.0;
            return n;
        }

        /// <summary>
        /// Head bend (tilt toward the avenue composed with the forward bow) as a smooth bend between the neck ring and
        /// eta 246 (keeps the weld exact): the unsigned rotation angle at unbent height y.
        /// </summary>
        public double BendAngle(double yUnbent) =>
            m_bendTotal * ThreadMath.Smoothstep(Y(ColossusSpec.HoodEta[0]), Y(ColossusSpec.HoodEta[1]), yUnbent);

        public double BendPivotY => Y(ColossusSpec.HoodEta[0]);

        // Rodrigues rotation of v by angle b about the unit axis k = (m_kx, 0, m_kz)
        D3 Rotate(D3 v, double b)
        {
            double c = Math.Cos(b), s = Math.Sin(b);
            double kv = m_kx * v.x + m_kz * v.z;
            var kxv = new D3(-m_kz * v.y, m_kz * v.x - m_kx * v.z, m_kx * v.y);
            return new D3(v.x * c + kxv.x * s + m_kx * kv * (1.0 - c), v.y * c + kxv.y * s, v.z * c + kxv.z * s + m_kz * kv * (1.0 - c));
        }

        public D3 Bend(D3 p)
        {
            double yp = BendPivotY;
            D3 r = Rotate(new D3(p.x, p.y - yp, p.z), BendAngle(p.y));
            return new D3(r.x, r.y + yp, r.z);
        }

        public D3 BendVector(D3 v, double yUnbent) => Rotate(v, BendAngle(yUnbent));

        /// <summary>The face plate's facing after the head bend (local frame): turned toward the path and bowed down.</summary>
        public D3 HeadForward => BendVector(m_faceDir, Y(ColossusSpec.HoodEta[5]));

        /// <summary>Hood apex after the tilt (world).</summary>
        public D3 ApexWorld => LocalToWorld(Bend(new D3(0, Y(ColossusSpec.HoodEta[5]), 0)));

        // ------------------------------------------------------------------ door (14.5.2 e)
        DoorFrame BuildDoor()
        {
            int j = ColossusSpec.DoorColumn;
            double tA = ColumnTheta(j), tB = ColumnTheta(j + 1);
            D3 a0 = LocalToWorld(RobeLocal(tA, 0.0, false, 0)), b0 = LocalToWorld(RobeLocal(tB, 0.0, false, 0));
            D3 a1 = LocalToWorld(RobeLocal(tA, ThreadConstants.HemBand, false, 0)), b1 = LocalToWorld(RobeLocal(tB, ThreadConstants.HemBand, false, 0));
            var d = new DoorFrame();
            d.O = (a0 + b0) * 0.5;
            d.H = (b0 - a0).Normalized;
            d.Nh = new D3(d.H.z, 0, -d.H.x);
            D3 radial = new D3(d.O.x - Cx, 0, d.O.z - Cz);
            if (D3.Dot(d.Nh, radial) < 0) d.Nh = d.Nh * -1.0;
            D3 g = (a1 + b1) * 0.5 - d.O;
            d.U = (g - d.H * D3.Dot(g, d.H)).Normalized;
            d.N = D3.Cross(d.U, d.H).Normalized;
            if (D3.Dot(d.N, d.Nh) < 0) d.N = d.N * -1.0;
            d.Sill = ThreadConstants.LedgeSteps * ThreadConstants.LedgeRiser;
            d.Lintel = d.Sill + ThreadConstants.DoorHeight;
            double hw = 0.5 * ThreadConstants.DoorWidth;
            d.DL0 = d.At(-hw, d.Sill); d.DR0 = d.At(hw, d.Sill);
            d.DL1 = d.At(-hw, d.Lintel); d.DR1 = d.At(hw, d.Lintel);
            return d;
        }

        // ------------------------------------------------------------------ sleeves (14.5.2 c)
        /// <summary>Envelope ring of the robe at world height y (local XZ), sampled densely.</summary>
        void EnvelopeRing(double y, double[] xs, double[] zs)
        {
            double eta = Eta(y);
            int n = xs.Length;
            for (int i = 0; i < n; i++)
            {
                double th = 2.0 * Math.PI * i / n;
                D3 p = RobeLocal(th, eta, true);
                xs[i] = p.x; zs[i] = p.z;
            }
        }

        static double MinDist(double px, double pz, double[] xs, double[] zs)
        {
            double best = double.MaxValue;
            int n = xs.Length;
            for (int i = 0; i < n; i++)
            {
                int k = (i + 1) % n;
                double ax = xs[i], az = zs[i], bx = xs[k] - ax, bz = zs[k] - az;
                double l2 = bx * bx + bz * bz;
                double t = l2 > 1e-12 ? Clamp(((px - ax) * bx + (pz - az) * bz) / l2, 0.0, 1.0) : 0.0;
                double dx = px - (ax + bx * t), dz = pz - (az + bz * t);
                double d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Math.Sqrt(best);
        }

        /// <summary>Smallest lateral offset at which a sleeve section of radius rs keeps the target gap to the robe envelope.</summary>
        double SolveLateral(double side, double y, double z, double rs, double target, double[] xs, double[] zs)
        {
            EnvelopeRing(y, xs, zs);
            double maxLat = 0;
            for (int i = 0; i < xs.Length; i++) maxLat = Math.Max(maxLat, side * xs[i]);
            double L = maxLat + rs + target;
            for (int it = 0; it < 40; it++)
            {
                double g = MinDist(side * L, z, xs, zs) - rs;
                if (g >= target - 1e-4) break;
                L += (target - g) + 1e-3;
            }
            return L;
        }

        double SleeveRadius(double frac) => ColossusSpec.SleeveR0 + (ColossusSpec.SleeveR1 - ColossusSpec.SleeveR0) * Clamp(frac, 0.0, 1.0);

        SleeveShape BuildSleeve(bool bent, double side)
        {
            var xs = new double[720]; var zs = new double[720];
            double target = ColossusSpec.SleeveGap + ColossusSpec.SleeveGapMargin;
            double shoulder = ColossusSpec.ShoulderEta[0];
            double top = shoulder - ColossusSpec.SleeveCapSink;
            double blend = ColossusSpec.SleeveBlendEta;
            // the loft sections span the sleeve minus its closed cuff, so the cuff's dome ends exactly 150 m (profile)
            // from the shoulder (660 c); the radius law 9 -> 6 runs over the sections
            double loft = ColossusSpec.SleeveLength - ColossusSpec.CuffAxial[ColossusSpec.CuffAxial.Length - 1] / Kk;
            var etas = new List<double>();
            for (int k = 0; k < 4; k++) etas.Add(top + (blend - top) * k / 3.0);
            int upperExtra;
            if (!bent)
            {
                double bottom = shoulder - loft;
                upperExtra = ColossusSpec.SleeveSections - 4;
                for (int k = 1; k <= upperExtra; k++) etas.Add(blend + (bottom - blend) * k / upperExtra);
            }
            else
            {
                for (int k = 1; k <= 3; k++) etas.Add(blend + (ColossusSpec.ElbowEta - blend) * k / 4.0);
                etas.Add(ColossusSpec.ElbowEta);
            }

            // Attachment blend (660 c): from the 17.5 m offset at the shoulder to c(y) at y 200. A quadratic ease
            // L = Lb + (17.5 - Lb) u^2 (u = 1 at the shoulder, 0 at y 200) joins the hanging part with zero slope and
            // keeps the centre-line curvature (2 (Lb - 17.5) / 15^2 ~ 0.08 / m at most) below 1 / r_sleeve, so the
            // 9 m thick tube never folds over itself on the inside of the bend (a smoothstep blend would reach
            // ~0.24 / m at both ends and fold the loft rings next to the robe).
            double rsBlend = SleeveRadius((shoulder - blend) / loft);
            double Lb = SolveLateral(side, Y(blend), 0.0, rsBlend, target, xs, zs);
            var c = new List<D3>(); var r = new List<double>(); var gap = new List<double>();
            for (int k = 0; k < etas.Count; k++)
            {
                double eta = etas[k];
                double y = Y(eta);
                double rs = SleeveRadius((shoulder - eta) / loft);
                double L;
                if (eta > blend)
                {
                    double u = Clamp((eta - blend) / (shoulder - blend), 0.0, 1.0);
                    L = Lb + (ColossusSpec.SleeveAttachOffset - Lb) * u * u;
                }
                else L = SolveLateral(side, y, 0.0, rs, target, xs, zs);   // c(y): the 1.0 m gap (+ margin) to the robe
                c.Add(new D3(side * L, y, 0.0));
                r.Add(rs);
            }
            if (bent)
            {
                // forearm: 25 deg forward (+Z local) from the elbow, length (shoulder - elbow) in profile units
                D3 e = c[c.Count - 1];
                double Le = Math.Abs(e.x);
                double bend = ColossusSpec.ElbowBendDeg * ThreadMath.Deg;
                var fd = new D3(0, -Math.Cos(bend), Math.Sin(bend));
                double fore = loft - (shoulder - ColossusSpec.ElbowEta);
                int n = ColossusSpec.SleeveSections - c.Count;
                for (int k = 1; k <= n; k++)
                {
                    double a = fore * k / n;
                    D3 p = e + fd * (a * Kk);
                    double rs = SleeveRadius((shoulder - ColossusSpec.ElbowEta + a) / loft);
                    double L = Math.Max(Le, SolveLateral(side, p.y, p.z, rs, target, xs, zs));
                    c.Add(new D3(side * L, p.y, p.z));
                    r.Add(rs);
                }
            }
            for (int k = 0; k < c.Count; k++)
            {
                EnvelopeRing(c[k].y, xs, zs);
                gap.Add(MinDist(c[k].x, c[k].z, xs, zs) - r[k]);
            }

            int m = c.Count;
            var s = new SleeveShape { Bent = bent, SideX = side, C = c.ToArray(), R = r.ToArray(), Gap = gap.ToArray(), T = new D3[m], N = new D3[m], B = new D3[m] };
            var X = new D3(1, 0, 0);
            for (int k = 0; k < m; k++)
            {
                D3 a = s.C[Math.Max(0, k - 1)], b = s.C[Math.Min(m - 1, k + 1)];
                D3 t = (b - a).Normalized;
                D3 nn = (X - t * D3.Dot(X, t)).Normalized;
                s.T[k] = t; s.N[k] = nn; s.B[k] = D3.Cross(t, nn).Normalized;
            }
            BuildCuff(s, target);
            return s;
        }

        const int CuffRingSamples = 48, CuffEnvelopeSamples = 240, CuffShiftIterations = 30;
        const double CuffEnvelopeDy = 0.05;   // re-sample the robe envelope when a ring sample's height moved by more (m)

        /// <summary>
        /// Closed cuff (ColossusSpec.CuffAxial / CuffRadius) beyond the last section. The flared hem may not come
        /// closer to the robe than the sections do, so the rings are shifted horizontally away from the robe axis by the
        /// smallest amount that restores the target gap on every ring (the shift ramps in up to the hem ring and stays;
        /// the side facing the robe keeps its line and the hem flares outward, like cloth hanging off the arm).
        /// </summary>
        void BuildCuff(SleeveShape s, double target)
        {
            int last = s.Count - 1, nc = ColossusSpec.CuffAxial.Length;
            D3 ce = s.C[last], te = s.T[last], ne = s.N[last], be = s.B[last];
            double rc = s.R[last];
            D3 away = new D3(ce.x, 0.0, ce.z);
            away = away.Length > 1e-9 ? away.Normalized : new D3(s.SideX, 0.0, 0.0);
            s.CuffC = new D3[nc]; s.CuffR = new double[nc]; s.CuffGap = new double[nc];
            // pleats along the profile normal of the (axial, radius) curve, central differences (the last section is ring -1)
            s.CuffFoldA = new double[nc]; s.CuffFoldR = new double[nc];
            for (int q = 0; q < nc; q++)
            {
                double a0 = q > 0 ? ColossusSpec.CuffAxial[q - 1] : 0.0, r0 = q > 0 ? ColossusSpec.CuffRadius[q - 1] : 1.0;
                int q1 = Math.Min(nc - 1, q + 1);
                double da = ColossusSpec.CuffAxial[q1] - a0, dr = rc * (ColossusSpec.CuffRadius[q1] - r0);
                double len = Math.Sqrt(da * da + dr * dr);
                s.CuffFoldA[q] = ColossusSpec.CuffFold[q] * -dr / len;
                s.CuffFoldR[q] = ColossusSpec.CuffFold[q] * da / len;
            }
            var xs = new double[CuffEnvelopeSamples]; var zs = new double[CuffEnvelopeSamples];
            double aHem = ColossusSpec.CuffAxial[ColossusSpec.CuffHemRing];
            double shift = 0.0;
            for (int it = 0; it < CuffShiftIterations; it++)
            {
                double need = 0.0;
                for (int q = 0; q < nc; q++)
                {
                    double w = ThreadMath.Smoothstep(0.0, aHem, ColossusSpec.CuffAxial[q]);
                    s.CuffC[q] = ce + te * ColossusSpec.CuffAxial[q] + away * (shift * w);
                    s.CuffR[q] = rc * ColossusSpec.CuffRadius[q];
                    // the pleat ridges reach |CuffFoldR| beyond the ring (their axial lift moves a sample < 0.25 m)
                    s.CuffGap[q] = RingClearance(s.CuffC[q], s.CuffR[q] + Math.Abs(s.CuffFoldR[q]), ne, be, xs, zs);
                    if (w > 1e-6) need = Math.Max(need, (target - s.CuffGap[q]) / w);
                }
                if (need <= 1e-4) break;
                shift += need + 1e-3;
            }
            s.CuffShift = shift;
        }

        /// <summary>Smallest clearance of a ring (centre, radius, axes; local frame) to the robe envelope at each sample's own height.</summary>
        double RingClearance(D3 centre, double radius, D3 n, D3 b, double[] xs, double[] zs)
        {
            int samples = radius > 0.0 ? CuffRingSamples : 1;
            double best = double.MaxValue, ringY = double.NaN;
            for (int k = 0; k < samples; k++)
            {
                double ph = 2.0 * Math.PI * k / CuffRingSamples;
                D3 p = centre + (n * Math.Cos(ph) + b * Math.Sin(ph)) * radius;
                double eta = Eta(p.y);
                if (eta < 0.0 || eta > ColossusSpec.ShoulderEta[2]) continue;
                double th = Math.Atan2(p.x / Sx, p.z / Sz);
                double rho = Math.Sqrt((p.x / Sx) * (p.x / Sx) + (p.z / Sz) * (p.z / Sz));
                if (rho < ProfileR(eta) + DeformEnvelope(th, p.y)) return -1.0;
                // the envelope changes by < 1 cm per CuffEnvelopeDy of height here: re-sample it only when the height moved
                if (double.IsNaN(ringY) || Math.Abs(p.y - ringY) > CuffEnvelopeDy) { EnvelopeRing(p.y, xs, zs); ringY = p.y; }
                best = Math.Min(best, MinDist(p.x, p.z, xs, zs));
            }
            return best;
        }

        // ------------------------------------------------------------------ queries for tests and logs
        /// <summary>
        /// Clearance of a world point to the robe envelope at the point's height: negative inside the robe,
        /// otherwise the horizontal distance to the envelope ring (valid between the sunk hem and the neck).
        /// </summary>
        public double RobeClearance(D3 world)
        {
            D3 p = WorldToLocal(world);
            double eta = Eta(p.y);
            if (eta < -ThreadConstants.HemSink || eta > ColossusSpec.ShoulderEta[2]) return double.MaxValue;
            double th = Math.Atan2(p.x / Sx, p.z / Sz);
            double rho = Math.Sqrt((p.x / Sx) * (p.x / Sx) + (p.z / Sz) * (p.z / Sz));
            double renv = ProfileR(eta) + DeformEnvelope(th, p.y);
            if (rho < renv) return -1.0;
            var xs = new double[720]; var zs = new double[720];
            EnvelopeRing(p.y, xs, zs);
            return MinDist(p.x, p.z, xs, zs);
        }

        /// <summary>Highest point of the bent hood (local y = world y): the figure's top after the head bow (dense samples).</summary>
        public double CrownY()
        {
            double best = double.MinValue;
            for (int i = 0; i <= 100; i++)
                for (int j = 0; j < 360; j++)
                    best = Math.Max(best, Bend(HoodLocalUnbent(2.0 * Math.PI * j / 360, 3.0 + 0.02 * i)).y);
            return best;
        }

        public string Describe() =>
            $"colossus {Index} (pair {Pair}, {(Side == 0 ? "left" : "right")}): seed 0x{Seed:X8}, h {Height:F2} m, yaw {Yaw / ThreadMath.Deg:F2} deg, tilt {Tilt / ThreadMath.Deg:F2} deg, bow {Nod / ThreadMath.Deg:F2} deg, face turn {FaceTurn / ThreadMath.Deg:F2} deg, centre ({Cx:F3}, {Cz:F1})";
    }
}
