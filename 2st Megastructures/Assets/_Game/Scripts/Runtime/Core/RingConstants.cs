using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Single source of the numeric constants of MASTER_PROMPT Part 14.12 (code, tests and shaders).
    /// Units: metres, seconds, degrees unless a name says otherwise. Unity left-handed, Y up,
    /// +Z spinward (first walk direction), +X ring axis toward the right rim wall.
    /// </summary>
    public static class RingConstants
    {
        // Ring
        public const float RingR = 16000f;
        public const double RingRD = 16000.0;
        public const float ZenithRange = 32000f;

        // Star (inside)  az -68 / el 26, L = toward the star
        public const float StarAzimuth = -68f;
        public const float StarElevation = 26f;
        public const float StarDiameterDeg = 2.4f;
        public const float StarRadiusDeg = 1.2f;
        public static readonly Vector3 StarL = DirFromAzEl(StarAzimuth, StarElevation); // (-0.833, 0.438, 0.337)

        // Star (outside) yaw 96 / el 7
        public const float StarOutYaw = 96f;
        public const float StarOutElevation = 7f;
        public static readonly Vector3 StarL7 = DirFromAzEl(StarOutYaw, StarOutElevation); // (0.987, 0.122, -0.104)
        public const float StarOutRange = 40000f;

        // Player
        public const float Eye = 1.7f;
        public const float Walk = 1.5f;
        public const float Brisk = 2.5f;
        public const float Accel = 0.2f;
        public const float PlayerRadius = 0.35f;
        public const float PlayerHeight = 1.8f;
        public const float StepOffset = 0.3f;
        public const float SlopeLimit = 30f;
        public const float StrideWalk = 0.75f;
        public const float StrideBrisk = 0.85f;
        public const float HeadBob = 0.008f;
        public const float NearPlane = 0.05f;

        // Beats: start s of beats 1..7 and the path end
        public static readonly float[] BeatS = { 0f, 24f, 276f, 411f, 594f, 774f, 894f, 960f };
        public static readonly float[] BeatT = { 16f, 168f, 90f, 122f, 120f, 80f, 44f };
        public static readonly string[] BeatNames = { "Capsule", "Valley", "Ark", "Sunset", "RimWall", "Tunnel", "Outside" };
        public const float PathLength = 960f;
        public const float GoalS = 959f;
        public const float TotalTime = 640f;

        // Waypoints (x, z) of the sharp polyline
        public static readonly Vector2[] Waypoints =
        {
            new Vector2(0, 0), new Vector2(0, 24), new Vector2(0, 276), new Vector2(0, 411), new Vector2(0, 558),
            new Vector2(0, 576), new Vector2(18, 576), new Vector2(100, 576), new Vector2(100, 660), new Vector2(114, 660),
            new Vector2(234, 660), new Vector2(300, 660)
        };

        // Capsule (beat 1)
        public const float CapsuleInnerW = 2.6f, CapsuleInnerH = 2.5f, CapsuleZ0 = -1.5f, CapsuleZ1 = 9.5f;
        public const float CapsuleFloorY = 0.9f, CapsuleCeilY = 3.4f, CapsuleWall = 0.3f, CapsuleCornerR = 0.45f;
        public static readonly Vector3 PortholeCentre = new Vector3(-1.46f, 2.98f, 5.65f);
        public const float PortholeOpening = 0.55f, PortholeFrame = 0.80f, PortholeYaw = -45f, PortholePitch = 25f, PortholeBay = 0.35f;
        public const float HatchW = 2.0f, HatchH = 1.9f, HatchTriggerS = 6.5f, HatchOpenTime = 2.0f, HatchZ0 = 9.5f, HatchZ1 = 10.0f;
        public const float RampZ0 = 10f, RampZ1 = 24f, RampY0 = 0.90f, RampY1 = 0.03f, RampW = 2.4f;
        public const float PadZ0 = 24f, PadZ1 = 30f, PadHalfX = 3f, PadTopY = 0.03f;

        // Bridge / river (beat 2)
        public const float BridgeZ0 = 120f, BridgeZ1 = 180f, BridgeCrestZ = 150f, BridgeCrest = 3.0f, BridgeDeckW = 5.0f, BridgeSpan = 36f;
        public const float RailH = 1.0f, LampH = 3.2f;
        public const float RiverWidth = 36f, RiverSurfaceY = -2.2f, RiverCarve = 3.2f, RiverFlow = 0.6f;
        public static readonly Vector2[] RiverCentre =
        {
            new Vector2(-900, 232), new Vector2(-420, 205), new Vector2(-260, 178), new Vector2(-120, 160), new Vector2(0, 150),
            new Vector2(110, 151), new Vector2(240, 166), new Vector2(420, 196), new Vector2(900, 238)
        };

        // Clipmap
        public const int ClipLevels = 9, ClipQuads = 128;
        public const float ClipSpacing0 = 0.5f, ClipExtent = 8192f, ClipEdgeElevation = 14.67f;

        // Hill / belvedere / pavilion
        public static readonly Vector2 HillCentre = new Vector2(-95, 205);
        public const float HillRadius = 70f, HillHeight = 20f;
        public static readonly Vector3 PavilionP2 = new Vector3(-110, 0, 500);
        public const float PavilionH = 11f, BelvedereH = 14f;

        // Aqueduct
        public const float AqueductZ = 1150f, AqueductX0 = -1400f, AqueductX1 = 200f, AqueductSpan = 20f, AqueductH = 24f;

        // Ark (beat 3)
        public const float ArkLength = 3000f, ArkBeam = 420f, ArkHeight = 260f, ArkAltitude = 1800f, ArkTrackZ = 3180f;
        public const float ArkX0 = -9000f, ArkX1 = 6000f, ArkT = 190f, ArkD = 285f, ArkSpeed = 78.9f, ArkTriggerS = 231f;
        public const float ShipShadowL = 3000f, ShipShadowW = 620f, ShipShadowPenumbra = 172f, ShipShadowStrength = 0.62f;
        public static readonly Vector2 ShipShadowOffset = new Vector2(3422f, -1383f);
        public const float FootbridgeS0 = 396f, FootbridgeS1 = 404f, FootbridgeW = 4.2f, FootbridgeH = 3.2f, FootbridgeL = 8.0f;

        // Cloud shadows (beat 2)
        public const float CloudShadowFromS = 90f, CloudShadowL = 900f, CloudShadowW = 350f, CloudShadowPenumbra = 600f, CloudShadowStrength = 0.25f, CloudShadowSpeed = 12f;
        public static readonly Vector2 CloudShadowA0 = new Vector2(-3400f, 1900f);
        public static readonly Vector2 CloudShadowB0 = new Vector2(-5200f, 3100f);

        // Sunset (beat 4)
        public const float SunsetT = 70f, SunsetD = 105f, SunsetS0 = 411f, KeyFloor = 0.03f, NightKey = 0.09f;
        public const float SquareA = 22f, SquareB = 16f, SquareC = 12f, SquareRot = 8f, SquareEdge = 0.12f;
        public const float TerminatorMax = 70f, TerminatorSoft = 6f;

        // Levee / underpass
        public const float LeveeX0 = -70f, LeveeX1 = 18f, LeveeZ0 = 558f, LeveeZ1 = 640f, LeveeH = 14f;
        public const float UnderpassW = 2.8f, UnderpassH = 2.9f, UnderpassLeg = 18f, SwapS = 576f;
        // ADR-009: the sealed set swap fires at s 572 in the first underpass leg (the exit portal is hidden behind the corner)
        // and reverts below s 568; SwapS = 576 stays the corner of the polyline.
        public const float SwapTriggerS = 572f, SwapRevertS = 568f;

        // Wall (beat 5) and tunnel (beat 6)
        public const float WallX = 114f, WallOuterX = 234f, WallThickness = 120f, WallSeam = 100f, WallTop = 12000f;
        public const float DoorW = 2.6f, DoorH = 3.0f, DoorStraight = 1.7f, DoorCrownR = 1.3f, DoorRecess = 0.8f, DoorFrame = 0.12f, DoorLintel = 0.5f;
        public const float LaneCapX = 111.5f;
        public const float TunnelLen = 120f, TunnelRibX0 = 117f, TunnelRibPitch = 6f; public const int TunnelRibs = 20;
        public const float ClimberZ = 700f;

        // Platform (beat 7). Arbitration (DEVELOPMENT.md ADR-011): from a 1.7 m eye the hull, 11.4 deg below the horizon
        // with the spec's 600 m drop, is only visible within 1.7 / tan 11.4 = 8.4 m of a deck edge, so on a 30 m deck the
        // 'underside of the world far below' never appears. The deck is a 12 m pier (z 654..666, edges 6 m from the walk)
        // and the hull top sits 200 m below it (dip acos(30 000 / 30 200) = 6.6 deg, horizon sqrt(2 R h + h^2) = 3 470 m):
        // the hull shows beyond both rails wherever the edge is within 1.7 / tan 6.6 = 14.7 m (azimuth >= 24 deg off the walk).
        public const float PlatformX0 = 234f, PlatformX1 = 306f, PlatformZ0 = 654f, PlatformZ1 = 666f, PlatformThick = 1.2f, PlatformLane = 10.3f;
        public const float HullR = 30000f, HullDepth = 200f, HullHorizon = 3470f, HullDip = 6.6f;
        public static readonly Vector2 HullCentre = new Vector2(300f, 660f);
        public static readonly int[] SwarmCounts = { 2600, 6600, 14800 };
        public static readonly float[] SwarmRadii = { 5000f, 8000f, 12000f };
        public static readonly float[] SwarmPeriods = { 600f, 1200f, 2200f };
        public static readonly float[] SwarmTilt = { 10f, 58f, 75f };
        public static readonly float[] SwarmNode = { 20f, -35f, 75f };
        public static readonly float[] SwarmSense = { 1f, -1f, 1f };
        public const float SwarmPlate = 70f, SwarmMinPx = 2.2f, SwarmGlint = 0.09f, SwarmGlintAmp = 6f, SwarmClamp = 8f;
        public static readonly Vector2 BeaconXZ = new Vector2(301f, 660f);
        public const float BeaconH = 1.3f, BeaconLens = 0.14f, BeaconPeriod = 4.0f, BeaconFirstPingS = 905f;

        // Fog sigma per beat (1..7); beat 4 ramps to 3.6e-4 over u 0.30-0.90.
        // Arbitration (DEVELOPMENT.md ADR-012): with sigma 2.5e-4 the land is 63 % fogged at 4 km (7 deg) and 98.5 % at the
        // 8 km terrain edge, so in KS02 a featureless haze band ran from 6 to 19 deg and the land never visibly climbed into
        // the sky (critics run04, S1: money shot 1). Beats 1-3 use 1.0e-4 (with the 0.8 valley mist: 72 % at the terrain
        // edge, 27 % at 4 km); the arch keeps the ArchHaze shape through the haze-layer model of RingAtmosphere.hlsl.
        public static readonly float[] FogSigma = { 1.0e-4f, 1.0e-4f, 1.0e-4f, 1.0e-4f, 5.0e-4f, 4.0e-3f, 0f };
        public const float FogSigmaNight4 = 3.6e-4f;
        public const float FogSigmaDay = 1.0e-4f;          // the arch haze sigma (all beats): _FogExtra.w
        public const float FogAerialLength = 120000f;      // weak linear aerial term (1/m in _FogExtra.x)
        public const float FogScaleHeight = 2000f;         // haze thins with height (beats 1-4): _FogExtra.y = 1 / H
        public const float AerialDesat = 0.35f;            // distance desaturation (Part 8.4), 0 in space
        public static readonly float[] Vfov = { 60f, 64f, 66f, 64f, 66f, 60f, 60f };

        // Camera far plane. Arbitration (DEVELOPMENT.md ADR-007): beat 6 keeps 15 000 m instead of 300 m.
        public const float FarDefault = 15000f, FarOutside = 60000f, FarOutsideFromS = 880f;

        // Light calibration (14.2.3 'tune light intensities, not post exposure'): every light, the ambient trilight and the
        // arch's analytic lighting are multiplied by this so a meadow pixel of albedo ~0.3 lands at sRGB 110-135 in beat 2.
        // Twin: RING_LIGHT_CAL in Shaders/Include/RingCommon.hlsl. Measured 2026-10-30 (run03 KS02 meadow luma 178 at 1.0).
        public const float LightCal = 0.45f;

        // Exposure slew
        public const float EvSlew = 0.30f, WbSlew = 6f, BloomSlew = 0.1f;

        // Cinematic holds (s, seconds) = 49 s
        public static readonly Vector2[] CinematicHolds =
        {
            new Vector2(5.1f, 5f), new Vector2(150f, 8f), new Vector2(440f, 6f), new Vector2(505f, 6f),
            new Vector2(606f, 6f), new Vector2(888f, 4f), new Vector2(912f, 8f), new Vector2(960f, 6f)
        };

        // UI timing
        public const float FadeInStart = 0.6f, FadeInEnd = 3.1f, TitleIn0 = 0.8f, TitleIn1 = 2.0f, TitleOut0 = 4.0f, TitleOut1 = 5.4f, HintAt = 6.0f, HintDur = 6.0f;
        public const float EndDwell = 3.0f, EndFade = 4.0f, EndBlack = 1.5f, EndTextIn = 2.0f, EndTextHold = 6.0f, EndTextOut = 2.0f;
        public const string Title = "THE RING";
        public const string EndLine = "Someone built all of this to be home.";
        public const string Hint = "WASD  move    Mouse  look    Shift  faster    C  cinematic";

        /// <summary>Unit direction from yaw (deg, 0 = +Z, +90 = +X) and elevation (deg).</summary>
        public static Vector3 DirFromAzEl(float yawDeg, float elDeg)
        {
            float y = yawDeg * Mathf.Deg2Rad, e = elDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(y) * Mathf.Cos(e));
        }

        /// <summary>Beat index 0..6 for progress s.</summary>
        public static int BeatAt(float s)
        {
            for (int i = 6; i >= 0; i--) if (s >= BeatS[i]) return i;
            return 0;
        }
    }
}
