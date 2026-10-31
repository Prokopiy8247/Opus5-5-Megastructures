using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Layout numbers of beat 7 that are not in <see cref="RingConstants"/> (each cites its spec section) and the C#
    /// twins of Beats/Beat07_Outside/Shaders/RingHullCore.hlsl (cap drop, ridge, gantry frames). The material defs
    /// (Editor/MaterialDefs/Beat7MaterialDefs.cs) write the same numbers into the materials, so mesh, shader and tests
    /// never drift.
    /// </summary>
    public static class OutsideLayout
    {
        // ---------------- deck (14.5.7 + ADR-011: a 12 x 72 x 1.2 m pier, top at the tunnel floor y 0, rails 1.0 m) -----
        // ADR-011: z 654..666 (edges 6 m from the walk line), lane 10.3, rail colliders at z 660 +- 5.5 (PathGuide). Every
        // number below derives from RingConstants.Platform*, so the pier follows any later arbitration.
        public const float DeckTopY = 0f;
        public static float DeckBottomY => DeckTopY - RingConstants.PlatformThick;   // -1.2
        public const float DeckChamfer = 0.025f;           // 8.7: 1-3 cm on everything seen within 3 m
        public static int DeckStrips => Mathf.Max(1, Mathf.CeilToInt(RingConstants.PlatformZ1 - RingConstants.PlatformZ0)); // 1 m strips (bend is a function of z)
        public const float RailInset = 0.5f;               // rails at z 660 +- 5.5 and x 305.5 (PathGuide colliders at +-5.5, front 305.5)
        public const float RailCornerR = 0.3f;             // rounded rail corners (no sharp mitre on a 0.10 m tube)
        public const float RailRadius = 0.05f;             // top and mid rails 0.10 m round (14.5.7 pitfalls: >= 0.10 m)
        public static float TopRailY => RingConstants.RailH - RailRadius;   // top of the hand rail at 1.0 m
        public const float MidRailY = 0.5f;
        public const int RailSegments = 12;
        public const float PostRadius = 0.04f;             // posts 0.08 m
        public const float PostFlangeR = 0.055f, PostEmbed = 0.01f;
        public const float PostPitch = 3f;                 // 14.5.7: posts every 3 m along the long sides and the front
        /// <summary>Posts per long side (corners shared): 71 m of rail at <= 3 m pitch = 25 (pitch 2.96 m).</summary>
        public static int PostsLongSide => 1 + Mathf.CeilToInt((RingConstants.PlatformX1 - RingConstants.WallOuterX - 2f * RailInset) / PostPitch - 1e-3f);
        /// <summary>Posts on the front (corners shared): 11 m of rail at <= 3 m pitch = 5 (pitch 2.75 m).</summary>
        public static int PostsFront => 1 + Mathf.CeilToInt((RingConstants.PlatformZ1 - RingConstants.PlatformZ0 - 2f * RailInset) / PostPitch - 1e-3f);
        public const float KickHeight = 0.13f, KickHalfT = 0.01f, KickChamfer = 0.005f;
        public const float KickInset = 0.43f;              // 7 cm outside the post line (post 4 cm + plate 1 cm + 2 cm gap)
        public const int Beams = 6;                        // 6 cantilever beams (2 m pitch under the 12 m pier)
        public const float BeamWidth = 0.5f, BeamRootDepth = 4.2f, BeamTipDepth = 0.6f, BeamTipX = 303f, BeamEmbed = 0.05f, BeamChamfer = 0.06f;
        public static readonly float[] BollardX = { 246f, 270f, 294f };   // 6 lamp bollards, 3 per side
        // From the rail line toward the lane centre: bollard collider 4.08..4.32 m from the walk line, so the wall-hugging
        // bot (0.40 m inside the 5.15 m lane edge, capsule 4.40..5.10) passes between bollard and rail (14.10 Autoplay).
        public const float BollardInset = 1.3f;
        public const float BollardHeight = 0.88f, BollardColliderHalf = 0.12f;
        public const float BollardPoolIntensity = 0.15f;   // WarmLamp x 0.15 pools, like the plain bollards (Beat56)
        public const float DeckBendDistance = 60f;         // bend inflation: deck content is seen with |dz| <= 30 m

        // ---------------- beacon (14.5.7: 1.3 m, base 0.35 -> 0.12, lens O0.14, base disc r 1.0, collider r 0.5) -------
        public const float BeaconBaseR = 0.35f, BeaconTopR = 0.12f, BeaconDiscR = 1.0f, BeaconColliderR = 0.5f;
        public const float BeaconLensR = 0.5f * RingConstants.BeaconLens;   // 0.07 m (diameter 0.14)
        public static float BeaconLensY => RingConstants.BeaconH - BeaconLensR; // lens centre 1.23, top of the lens at 1.30 m
        public const float BeaconLightRange = 6f, BeaconLightIntensity = 3.0f;
        public const float BeaconLightY = 1.55f;            // above the lens: the collar is not blown out by a 7 cm light distance
        public const float PulseBase = 0.775f, PulseAmp = 0.225f, PulseEaseIn = 1f;   // 0.775 + 0.225 sin, ease 1 s

        // ---------------- outer face (14.5.7: plane x 234, 120 km grid, dark by 55 km) ----------------
        public const float FaceX = RingConstants.WallOuterX;   // 234
        public const float FaceExtent = 60000f;                // +-60 km in y and z around the mouth
        public const float FaceDark0 = 30000f, FaceDark1 = 55000f;
        public const float MouthCentreY = 1.5f;            // centre of the polar face mesh (inside the tunnel profile)
        public const float FrameWidth = 0.14f, FrameProud = 0.06f, FrameReveal = 0.20f, FrameInset = 0.01f;
        // scale cues of the face (RingOuterFace): staggered 6.25 x 3.125 m plate courses (the doorway is one course high,
        // centred on a plate), 25 m sub-panels, 100 m seams 0.25 m wide, analytic ledges every 100 m (tops at y 50 + 100 k,
        // 2.5 m high, 4 m deep: dark undersides seen from the pier, 0.5 m key shadows below them under the 7 deg light)
        public const float FacePlateW = 6.25f, FacePlateH = 3.125f, FaceSubPanel = 25f, FaceSeamWidth = 0.25f;
        public const float FaceLedgePeriod = 100f, FaceLedgeTop0 = 50f, FaceLedgeHeight = 2.5f, FaceLedgeDepth = 4f;

        // ---------------- hull (14.5.7 + ADR-011: 7 levels 2..128 m, half-extent 8 192 m, cap R 30 000, 200 m down) ----
        // From the 1.7 m eye the cap top is 201.7 m down: horizon 3 485 m, dip 6.6 deg. Over a side edge 6 m away the
        // hull shows from 712 m out (201.7 x 6 / 1.7) to the horizon; over the front edge from 201.7 d / 1.7 (d = distance
        // to x 306: 1 186 m from the KS14 eye). Ridges, seams and gantries are placed and sized for that 0.7-3.5 km band.
        public const int HullLevels = 7, HullQuads = 128;
        public const float HullSpacing0 = 2f;
        public static float HullTopY => -RingConstants.HullDepth;          // -200
        // ridges: crests every 300 m along x, 14 m high, half-width 50 m (max slope 22 deg: lit faces NdotL ~0.5 under the
        // 7 deg key, 114 m shadows); 7-12 crests between the near hull edge (712 m) and the horizon
        public const float RidgePeriod = 300f, RidgeHeight = 14f, RidgeHalfWidth = 50f, RidgeCrestX = 560f;
        public const float RidgeGeomR0 = 380f, RidgeGeomR1 = 480f;          // geometric ridges only on the 2-8 m levels
        public const float HullSkirt = 3f;
        public const float HullFaceAoWidth = 40f;          // contact darkening of the hull next to the outer face (x 234)
        // minor ribs between the ridges (shading + self-shadow only): every 75 m, 2.5 m high, half-width 12 m, 20 m shadows
        public const float RibPeriod = 75f, RibHeight = 2.5f, RibHalfWidth = 12f, RibCrestX = RidgeCrestX + 37.5f;
        // 100 m seams 3 m wide (1.5-3 px across at 1-2 km: they read to the horizon where they run toward the eye),
        // a +-12 % plate patchwork and 25 m sub-panel joints; radiance eased toward 0.55 with range (e-folding 4 km)
        public const float HullSeamPeriod = 100f, HullSeamWidth = 3f, HullSeamOffsetX = 50f, HullSeamOffsetZ = 10f;
        public const float HullPlateTone = 0.12f, HullSubPanel = 25f, HullSubPanelWidth = 0.8f, HullSubPanelDarken = 0.82f;
        public const float HullDistDarkFloor = 0.55f, HullDistDarkRange = 4000f;

        // ---------------- gantries (14.5.7: 24 gantries 40 x 3 m) ----------------
        // ADR-011 placement: 6 columns along x (x 1 000..3 100) x 4 rows at -1 400 / -800 / +800 / +1 400 m lateral, i.e.
        // inside the visible band beyond the side rails (> 712 m lateral, < 3.5 km away); each casts a 40 x 73 m shadow
        // (9 m / tan 7 deg) toward -x. Row j: lateral = (j < 2 ? -1 : +1) x (j == 0 || j == 3 ? far : near).
        public const int GantryCols = 6, GantryRows = 4;   // the HLSL loops assume 6 x 4 (RingHullCore.hlsl)
        public const float GantryX0 = 1000f, GantryPitchX = 420f, GantryJitterX = 60f;
        public const float GantryRowNear = 800f, GantryRowFar = 1400f, GantryJitterZ = 120f;
        public const float GantryHalfLen = 20f, GantryHalfW = 1.5f, GantryBeamY0 = 6f, GantryBeamY1 = 9f;
        public const float GantryLegX = 1.2f, GantryLegZ = 18.5f, GantryLegHalf = 0.3f;

        // ---------------- swarm (14.5.7, 14.7 H6, ADR-008 as amended for ADR-011) ----------------
        public static readonly Vector3 StarEye = new Vector3(252f, RingConstants.Eye, 660f);   // KS12 eye
        public static Vector3 StarPos => StarEye + RingConstants.StarL7 * RingConstants.StarOutRange;
        public static float StarDiscRadiusM => RingConstants.StarOutRange * Mathf.Tan(RingConstants.StarRadiusDeg * Mathf.Deg2Rad); // 838 m
        public const float SwarmThickness = 0.12f;          // band thickness 0.12 r (triangular, normal and radial)
        // Radial structure (broad, diffuse, structured bands): 55 % of the panels in a Gaussian sheet sigma 0.09 r, 45 % in
        // three thin ringlets (radial sigma 0.012 r, a quarter of the thickness normal to the plane) at -0.12 / -0.02 /
        // +0.08 r (rotated per band); the sheet carries the full 0.12 r thickness; clamped to +-0.3 r.
        public const float SwarmRadialSigma = 0.09f, SwarmSheetFraction = 0.55f, SwarmRingletSigma = 0.012f, SwarmRadialClamp = 0.3f;
        public static readonly float[] SwarmRinglets = { -0.12f, -0.02f, 0.08f };
        public const float SwarmTiltSigmaDeg = 18f;
        public const float SwarmSpinMin = 0.02f, SwarmSpinMax = 0.08f;   // rad/s
        public const float SwarmLibAmp = 0.15f;             // libration amplitude (rad); peak normal speed = spin
        // soft round points: 1 px linear edge; SpaceBounce x 0.35 on the dark faces and a StarOutside x 0.06 forward-scatter
        // rim on back-lit plates keep the bands faintly visible against black space (dark silhouettes on the corona)
        public const float SwarmEdgePx = 1f, SwarmBounce = 0.35f, SwarmRim = 0.06f;
        public const float SwarmArcFraction = 0.08f, SwarmArcDeg = 10f;  // 8 % of each band in two 10 deg arcs
        public const double SwarmTimeWrap = 13200.0;        // LCM of 600 / 1 200 / 2 200 s (AtmosphereDriver wraps _Swarm.y)
        // ADR-008 remap (amended for ADR-011): true range 25-55 km drawn at 9 000-14 000 m along the same direction. The
        // hull now rises to 6.6 deg below the eye line (horizon 3.5 km), so the old 150-250 m shell drew panels in front
        // of it; 9-14 km lies beyond every hull hit (<= 3.5 km) and inside the 15 km far plane used before s 880.
        public const float SwarmRemapD0 = 25000f, SwarmRemapD1 = 55000f, SwarmRemapR0 = 9000f, SwarmRemapR1 = 14000f;
        public const float SwarmMinCrossDeg = 20f;
        public const int SwarmSeed = 1;
        // Fixed culling box of the three indirect draws: every camera of the outside walk (tunnel x 114 .. deck x 306,
        // z 654 .. 666) plus the 14 km remap shell (and the drawn splat) around it, so it always contains the drawn panels.
        public static readonly Vector3 SwarmBoundsCentre = new Vector3(210f, 1.7f, 660f);
        public static readonly Vector3 SwarmBoundsSize = new Vector3(28600f, 28400f, 28400f);

        /// <summary>Cap drop below its top at flat (x, z), stable form rho^2 / (R + sqrt(R^2 - rho^2)); s = sqrt(R^2 - rho^2).</summary>
        public static float CapDrop(float x, float z, out float s)
        {
            double dx = x - RingConstants.HullCentre.x, dz = z - RingConstants.HullCentre.y;
            double R = RingConstants.HullR;
            double rho2 = dx * dx + dz * dz;
            double sd = System.Math.Sqrt(System.Math.Max(R * R - rho2, 1.0));
            s = (float)sd;
            return (float)(rho2 / (R + sd));
        }

        public static float CapHeight(float x, float z) => HullTopY - CapDrop(x, z, out _);

        /// <summary>Unit outward cap normal at flat (x, z).</summary>
        public static Vector3 CapNormal(float x, float z)
        {
            CapDrop(x, z, out float s);
            return new Vector3(x - RingConstants.HullCentre.x, s, z - RingConstants.HullCentre.y).normalized;
        }

        /// <summary>Ridge height (twin of HullRidge) at x.</summary>
        public static float Ridge(float x)
        {
            float u = (x - RidgeCrestX) / RidgePeriod;
            float d = (u - Mathf.Floor(u + 0.5f)) * RidgePeriod;
            float q = Mathf.Clamp01(1f - d * d / (RidgeHalfWidth * RidgeHalfWidth));
            return RidgeHeight * q * q;
        }

        public static float GeomWeight(float x, float z)
        {
            float rho = new Vector2(x - RingConstants.HullCentre.x, z - RingConstants.HullCentre.y).magnitude;
            return 1f - RingMath.Smoothstep(RidgeGeomR0, RidgeGeomR1, rho);
        }

        /// <summary>Height of the displaced hull mesh (twin of HullGeomHeight).</summary>
        public static float GeomHeight(float x, float z) => CapHeight(x, z) + Ridge(x) * GeomWeight(x, z);

        /// <summary>Base point and local frame of gantry (k, j); twin of HullGantryFrame (same PCG hashes).</summary>
        public static void GantryFrame(int k, int j, out Vector3 G, out Vector3 ex, out Vector3 ey, out Vector3 ez)
        {
            float jx = RingMath.U01(RingMath.Pcg2(k + 701, j + 37)) - 0.5f;
            float jz = RingMath.U01(RingMath.Pcg2(k + 913, j + 59)) - 0.5f;
            float gx = GantryX0 + GantryPitchX * k + 2f * GantryJitterX * jx;
            float lateral = (j < 2 ? -1f : 1f) * (j == 0 || j == 3 ? GantryRowFar : GantryRowNear);
            float gz = RingConstants.HullCentre.y + lateral + 2f * GantryJitterZ * jz;
            float drop = CapDrop(gx, gz, out float s);
            G = new Vector3(gx, HullTopY - drop, gz);
            ey = new Vector3(gx - RingConstants.HullCentre.x, s, gz - RingConstants.HullCentre.y).normalized;
            ez = (Vector3.forward - ey * ey.z).normalized;
            ex = Vector3.Cross(ey, ez);
        }

        /// <summary>Horizon distance sqrt(2 R h + h^2) and dip acos(R / (R + h)) for an eye h metres above the cap top.</summary>
        public static void HullHorizon(float eyeAboveTop, out float distance, out float dipDeg)
        {
            double R = RingConstants.HullR, h = eyeAboveTop;
            distance = (float)System.Math.Sqrt(2.0 * R * h + h * h);
            dipDeg = (float)(System.Math.Acos(R / (R + h)) * 180.0 / System.Math.PI);
        }
    }
}
