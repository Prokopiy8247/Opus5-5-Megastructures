// ThreadConstants: the single home of every numeric constant of MASTER_PROMPT Part 14 (table 14.12).
// No other file may contain a literal copy of these values (colours live in ThreadPalette).
// Pure C#, no UnityEngine dependency, so EditMode tests and the offline audio renderer can use it.
namespace Megastructure
{
    public static class ThreadConstants
    {
        // ---------------------------------------------------------------- player (Part 9, 14.12)
        public const float EyeHeight = 1.7f;
        public const float WalkSpeed = 1.5f;
        public const float BriskSpeed = 2.5f;
        public const float AccelSmoothTime = 0.2f;
        public const float PlayerRadius = 0.35f;
        public const float CameraYFollowTime = 0.12f;
        public const float StepLength = 0.78f;          // one footstep per 0.78 m of plan distance
        public const float StairStepLength = 0.80f;     // one stone sound per two treads
        public const float HeadBobMax = 0.015f;
        public const float HeadBobDeck = 0.008f;

        // ---------------------------------------------------------------- path (14.4)
        public const double PathLength = 860.0;
        public const double TotalTimeWalk = 573.333333;
        public static readonly double[] BeatBoundaries = { 0, 90, 240, 320, 400, 580, 750, 860 };
        public static readonly string[] BeatNames = { "salt", "avenue", "stairs", "tower", "spiral", "clouds", "top" };
        public const int BeatCount = 7;
        public const double Hysteresis = 12.0;
        public const double BlendWindow = 24.0;
        public const double ExposureWindow = 9.0;

        public const double StairStartZ = 240.0;
        public const double StairEndZ = 320.0;
        public const double StairSlope = 0.45;
        public const double StairRise = 36.0;
        public const int StairTreads = 200;
        public const double TreadDepth = 0.40;
        public const double RiserHeight = 0.18;
        public const double StairWidth = 40.0;
        public const double StairSkirtY = -4.0;

        public const double TerraceY = 36.0;
        public const double TerraceXMin = -60.0, TerraceXMax = 190.0, TerraceZMin = 320.0, TerraceZMax = 520.0;
        public const double TerraceBottomY = -4.0;
        public static readonly double[] MandalaRings = { 60, 80, 100, 120 };
        public const double MandalaRingWidth = 0.5;
        public const int MandalaRadials = 36;
        public const double MandalaRadialWidth = 0.3;
        public const double MandalaRadialMin = 55.0, MandalaRadialMax = 120.0;

        public const double HelixStartS = 400.0;
        public const double HelixEndS = 840.0;
        public const double HelixRadius = 70.02817496043395;   // exactly 440 / (2 pi) (closes the turn; 14.4.2)
        public const double HelixCentreX = 70.02817496043395;
        public const double HelixCentreZ = 400.0;
        public const double HelixBaseY = 36.0;
        public const double HelixRisePerRad = 17.507043740108488;  // exactly 110 / (2 pi)
        public const double HelixGrade = 0.25;
        public const double HelixRise = 110.0;
        public const double GradeBlendStart = 396.0;
        public const double GradeBlendEnd = 404.0;
        public const double PierStartZ = 400.0;
        public const double PierEndZ = 420.0;            // goal point P6
        public const double TopY = 146.0;
        public const double GoalS = 860.0;
        public const double BeaconTriggerS = 858.0;

        // ---------------------------------------------------------------- walkable half-widths (14.4.3)
        public const double HalfWidthSalt = 12.0;
        public const double HalfWidthAvenue = 14.0;
        public const double HalfWidthStairs = 10.0;
        public const double HalfWidthDeck = 1.15;
        public const double DeckWidth = 3.0;
        public const double DeckThickness = 0.4;
        public const double DeckChamfer = 0.02;
        public const double DeckEdgeStripe = 0.10;
        public const double RailHeight = 1.0;
        public const double PostSpacing = 2.0;
        public const double PostSize = 0.06;
        public const double HandrailDiameter = 0.07;
        public const double ArmSpacing = 11.0;
        public const int ArmCountHelix = 40;
        public const int ArmCountPier = 3;
        public const double ArmDepth = 0.8;

        // ---------------------------------------------------------------- colossi (14.5.2)
        public static readonly double[] ColossusPairZ = { 144, 192, 240, 288 };
        public const double ColossusCentreX = 50.0;
        public const double ColossusInnerEdgeX = 28.0;
        public const double ColossusFootprintX = 22.0, ColossusFootprintZ = 18.0;
        public const double ColossusHeightMin = 282.0, ColossusHeightMax = 318.0;
        public const double ColossusNominalHeight = 300.0;
        public const double DoorHeight = 2.0, DoorWidth = 1.1, DoorRecess = 0.8;
        public const double LedgeRiser = 0.18, LedgeTread = 0.4, LedgeWidth = 1.6;
        public const int LedgeSteps = 3;
        public const double BollardHeight = 1.0, BollardDiameter = 0.35;
        public const double ColossusShadowLength = 674.0;  // 300 / tan(24 deg)
        public const double ColossusShadowY = 0.03;
        public const double ColossusShadowFadeZ0 = 210.0, ColossusShadowFadeZ1 = 240.0;
        public const double ShadeBandZ0 = 42.0, ShadeBandZ1 = 250.0;
        public const double HemBand = 6.0;
        public const double HemSink = 0.3;

        // ---------------------------------------------------------------- bank (14.5.3, 14.7.6)
        public const double BankDensity = 0.11;   // spec value (D-030 reverted: 0.16 did not reach the s = 305 acceptance either and hid the stair bollards)
        public const double BankZ0 = 225, BankZ1 = 265, BankZ2 = 305, BankZ3 = 345;
        public const double BankSigmaX = 220.0;   // D-025: flat-topped exp(-(x/220)^4) instead of exp(-(x/90)^2) (the terrace corner at x = 190 showed through)
        public static readonly double[] BankSlabTops = { 60, 70, 80, 90 };
        public const double BankSlabTop0 = 60.0, BankSlabTopStep = 10.0, BankSlabHalfEdge = 4.0;   // 8 m transitions (T-BANK remedy of 14.7.6)
        public const double DeckBottomSkin = 100.0, DeckTopSkinStep = 200.0;   // D-031: 3 x 200 m top skin
        public const double DeckGateA0 = 1200.0, DeckGateA1 = 1800.0;   // the deck term fades in as the camera nears it
        public const double GroundOffS = 590.0;
        public const double BankFadeS0 = 350.0, BankFadeS1 = 430.0;      // D-036: the mist clears behind the walker after the reveal                          // inside the deck white-out (see DEVELOPMENT.md)
        public const double SkyStopM = 25.0;                             // elevation of the middle sky stop (deg)
        public const double BankBoxXMin = -300, BankBoxXMax = 300, BankBoxZMin = 200, BankBoxZMax = 380, BankBoxYMin = 0, BankBoxYMax = 100;
        public const int BankSamples = 16;
        public const double BankSunPath = 30.0, BankDiffuseLit = 0.5;   // D-052: inside the bank the sun is diffused: lit weight -> 0.5 with exp(-density x 30 m)
        public const double BankCoreS0 = 268.0, BankCoreS1 = 302.0;
        public const double BankDriftAmp = 6.0, BankDriftPeriod = 40.0, BankNoiseAmp = 0.15, BankNoisePeriod = 30.0;

        // ---------------------------------------------------------------- thread law (14.7.3)
        public const double ThreadTheta0Deg = 0.124;
        public const double ThreadS = 320.0;
        public const double ThreadQ = 1.47;
        public const double SwapS = 285.0;
        public const double ThreadSwapFadeS0 = 268.0;   // D-049: the far thread fades out over s 268-285 inside the bank core (no step at the swap)
        public const double NearCableOnS = 262.0;
        public const double ThreadHazeCap = 0.74;
        public const double ThreadBearingOffsetDeg = 8.6, ThreadBearingOffsetS0 = 90.0, ThreadBearingOffsetS1 = 240.0;   // D-026
        public const double ThreadMinWidthPx = 1.75;   // at 1080p, scaled by H / 1080
        public const double ThreadMinWidthFloorPx = 1.5;
        public const double ThreadAlphaFloor = 0.35;

        // ---------------------------------------------------------------- cable (14.5.4)
        public const double CableAxisX = 70.02817496043395, CableAxisZ = 400.0;
        public const double CableRadius = 50.0;
        public const double CableRidgeCrest = 51.2;
        public const int CableStrands = 36;
        public const double CablePitch = 600.0;
        public const double CableRidgeRelief = 1.2;
        public const double CableBandSpacing = 50.0, CableBandHeight = 10.0;
        public const double CollarRadius = 52.5, CollarTube = 1.4, CollarSpacing = 100.0, CollarEmbed = 0.5;
        public const double CableLod0YMin = -300, CableLod0YMax = 600;
        public const double CableLod1YMin = -3300, CableLod1YMax = 3500;
        public const double CableLod2YMin = -20000, CableLod2YMax = 20000;
        public const double CableRibbonReach = 200000.0;
        public const double CableReliefFade0 = 140.0, CableReliefFade1 = 320.0;
        public const double CableLayLineScale = 50.0;    // u = 50 * theta - 0.5236 * y
        public const double CableLayLineRise = 0.5236;
        public const double CableLayLinePeriod = 8.727;

        // ---------------------------------------------------------------- beads (14.5.6)
        public const int BeadCount = 9;
        public const double BeadY0 = 230.0, BeadYStep = 40.0;   // D-035 (supersedes D-023): the bead column starts above the pier so it never caps the cable reveal or the plunge
        public const double BeadRadius = 62.0, BeadHalfHeight = 12.0;
        public const double WindowW = 0.9, WindowH = 1.2;
        public static readonly double[] WindowRingOffsets = { -4.0, 1.7, 5.5 };
        public const int WindowsPerRing = 48;
        public const double BeadDoorRecess = 0.4;

        // ---------------------------------------------------------------- pier, pad, beacon (14.5.7)
        public const double PadCentreZ = 424.0, PadRadius = 3.0, PadThickness = 0.6, PierFillet = 0.8;
        public const double BeaconPillarHeight = 1.6, BeaconPillarR0 = 0.25, BeaconPillarR1 = 0.12;   // D-051: pillar 1.6 m (spec 1.2) lifts the crystal off the far rail line in KF7b
        public const double BeaconCrystalHeight = 0.5;
        public const double BeaconEmissive = 6.0;
        public const double BeaconHaloR0 = 3.0, BeaconHaloR1 = 12.0;
        public const double BeaconPulseHz = 0.25, BeaconPulseDepth = 0.06;

        // ---------------------------------------------------------------- ascent (14.7.5)
        public const double AscentMaxAltitude = 600000.0;   // metres
        public const double AscentExp = 9.0;
        public const double ScaleHeight = 8500.0;
        public const double EarthRadius = 6371000.0;
        public const double CloudTop = 3000.0, CloudBottom = 1800.0;
        public const double DeckDensity = 0.09;
        public const double DeckSkin = 600.0;   // D-031 (spec 150 m: the emergence took ~1 s)
        public const double GroundDeactivateAltitude = 3200.0;
        public const double GroundDeactivateS = 597.0;
        public const double SeaOnS = 590.0;
        public const double CloudCoverageA0 = 40.0, CloudCoverageA1 = 200.0;   // km
        public const double CloudDriftSpeed = 0.4, CloudDriftAzimuthDeg = 60.0;

        // ---------------------------------------------------------------- sun / planet (14.2.4, 14.7.7)
        public const double SunAzimuthDeg = -35.0;
        public const double SunDiscRadiusDeg = 0.9;
        public const double SunDiscGain = 1.3;
        public const double PlanetAzimuthDeg = -32.0, PlanetElevationDeg = 12.0, PlanetRadiusDeg = 14.0;
        public const double PlanetTiltDeg = 18.0, PlanetTiltDriftDegPerS = 0.004;
        public const double PlanetAlphaA0 = 150.0, PlanetAlphaA1 = 300.0;   // km
        public const double PlanetBandWobbleAmp = 0.04;
        public const int PlanetBandWobbleCycles = 3;
        public const double EarthCloudDriftDegPerS = 0.02;
        public const double GlintAzimuthDeg = 0.0, GlintElevationDeg = 20.0, GlintPeriod = 18.0;

        // ---------------------------------------------------------------- shadow band (14.7.7)
        public const double ShadowBandHalfWidth = 50.0, ShadowBandOpacity = 0.8;   // D-045 (spec 0.62)
        public const double ShadowBandFade0 = 12000.0, ShadowBandFade1 = 30000.0;
        public const double ShadowBandPenumbra = 0.0033;
        public const double ShadowBandColourGain = 0.78;
        public const double DeckShadowN0 = 42.0, DeckShadowN1 = 58.0;

        // ---------------------------------------------------------------- camera / render (14.7.4, 14.12)
        public const float CameraNear = 0.1f;
        public const float CameraFar = 250000f;
        public const int Msaa = 4;
        public const double DitherLsb = 1.0;
        public const double DitherDarkGain = 1.5;
        public const int DitherTile = 64;
        public const double DitherSigma = 1.5;

        // ---------------------------------------------------------------- per-beat look tables (14.2.4, 14.2.5)
        // index 0..6 = beat 1..7; values are the beat's own (start) value; ProgressDirector blends.
        public static readonly double[] SunElevationDeg = { 24, 24, 24, 22, 22, 16, 12 };
        public static readonly double[] SunIntensity = { 1.00, 1.00, 0.90, 1.00, 1.00, 1.05, 1.10 };
        public static readonly double[] FogVisibility = { 1600, 2400, 2400, 3000, 6000, 40000, 0 }; // 0 = no distance fog
        public static readonly double[] FogHeight = { 900, 900, 900, 900, 8500, 8500, 8500 };
        public static readonly double[] ExposureEV = { 0.0, 0.0, -0.2, -0.1, 0.0, 0.1, -0.1 };
        public static readonly double[] BloomThreshold = { 1.0, 1.0, 1.0, 1.0, 1.0, 0.9, 0.8 };
        public static readonly double[] BloomIntensity = { 0.10, 0.10, 0.14, 0.10, 0.10, 0.12, 0.18 };
        public static readonly double[] BloomScatter = { 0.60, 0.60, 0.65, 0.60, 0.60, 0.65, 0.70 };
        public static readonly double[] Vignette = { 0.10, 0.12, 0.10, 0.10, 0.10, 0.10, 0.12 };
        public static readonly double[] Saturation = { 4, 4, 0, 4, 4, 4, 2 };      // URP-style -100..100
        public static readonly double[] Contrast = { 4, 4, 0, 4, 6, 8, 8 };

        // ambient (linear) up / side / down per beat
        public static readonly double[,] AmbientUp = { { 0.34, 0.40, 0.46 }, { 0.34, 0.40, 0.46 }, { 0.40, 0.40, 0.42 }, { 0.34, 0.40, 0.46 }, { 0.34, 0.40, 0.46 }, { 0.12, 0.14, 0.28 }, { 0.07, 0.035, 0.025 } };
        public static readonly double[,] AmbientSide = { { 0.40, 0.37, 0.32 }, { 0.40, 0.37, 0.32 }, { 0.40, 0.38, 0.35 }, { 0.40, 0.37, 0.32 }, { 0.40, 0.37, 0.32 }, { 0.16, 0.14, 0.20 }, { 0.03, 0.03, 0.05 } };
        public static readonly double[,] AmbientDown = { { 0.30, 0.25, 0.19 }, { 0.30, 0.25, 0.19 }, { 0.30, 0.27, 0.22 }, { 0.30, 0.25, 0.19 }, { 0.30, 0.25, 0.19 }, { 0.34, 0.30, 0.30 }, { 0.03, 0.06, 0.10 } };

        // ---------------------------------------------------------------- shading model (14.2.2)
        public const double TerminatorLo = -0.05, TerminatorHi = 0.35;
        public const double AmbientWeight = 0.40, SunWeight = 0.70;
        public const double RimPower = 3.0, RimStrength = 0.16;
        public const double AoMin = 0.78;

        // ---------------------------------------------------------------- timing of title / ending (14.11)
        public const double FadeInStart = 0.5, FadeInEnd = 3.0;
        public const double TitleIn0 = 1.0, TitleIn1 = 2.0, TitleOut0 = 4.0, TitleOut1 = 5.0;
        public const double HintStart = 6.0, HintDuration = 6.0;
        public const double EndFade = 4.0, EndAudioFade = 6.0, EndLineIn0 = 5.5, EndLineIn1 = 7.0, EndLineOut0 = 12.0, EndLineOut1 = 14.0;
        public const string TitleText = "THREAD";
        public const string EndText = "the thread goes on";
        public const string HintText = "W A S D walk   Shift hurry   C cinematic   Esc pause";
        public const double TitleHeightFrac = 0.045, TitleSpacingEm = 0.3, TitleYFrac = 0.42;
        public const double HintHeightFrac = 0.016, HintYFracFromBottom = 0.08;
        public const double EndHeightFrac = 0.035, EndSpacingEm = 0.1;

        // ---------------------------------------------------------------- audio (14.3)
        public const double AudioSampleRate = 48000.0;
        public const double DroneF1 = 36.71, DroneF2 = 55.00, DroneDetune = 0.17;
        public const double HumBase = 27.5;
        public static readonly double[] PadFreqs = { 146.83, 220.00, 329.63, 369.99 };
        public static readonly double[] StarNoteFreqs = { 587.3, 659.3, 740.0, 880.0, 987.8 };
        public const double LimiterCeiling = 0.84;   // -1.51 dBFS (14.12 asks -1.5; the 0.89 of 14.3.1 is -1.01 dBFS)
        public static readonly double[] TargetRmsDb = { -38, -32, -28, -26, -29, -30, -36 };
        public const double InhaleS0 = 268, InhaleS1 = 302, ExhaleS0 = 330, ExhaleS1 = 336, SwellWindowEnd = 345;
        public const double DeckPassS0 = 578, DeckPassS1 = 596;
        public const double WindOffS = 752;

        // ---------------------------------------------------------------- key shots (14.10.2)
        // s = progress used for the beat parameters; position / world yaw / pitch / vertical FOV of the camera.
        public static readonly KeyShot[] KeyShots =
        {
            new KeyShot("beat01_salt_KF1",      0,   0,     1.7,   0,     0,     12, 62, 0.70, 0.85),
            new KeyShot("beat02_avenue_KF2a",   96,  0,     1.7,   96,    0,     20, 64, 0.48, 0.80),
            new KeyShot("beat02_avenue_KF2b",   206, 0,     1.7,   206,   0,     65, 64, 0.45, 0.80),
            new KeyShot("beat02_avenue_KF2c",   100, 0,     1.7,   100,   32,    6,  64, 0.40, 0.75),
            new KeyShot("beat03_stairs_KF3",    270, 0,     15.2,  270,   0,     6,  64, 0.80, 0.95),
            new KeyShot("beat04_tower_KF4a",    336, 0,     37.7,  336,   40,    8,  68, 0.45, 0.75),
            new KeyShot("beat04_tower_KF4b",    372, 0,     37.7,  372,   64,    34, 68, 0.30, 0.65, true),
            new KeyShot("beat05_spiral_KF5a",   468, 29.85, 54.7, 458.76, -34.36, -48, 64, 0.40, 0.80),
            new KeyShot("beat05_spiral_KF5b",   520, 80.0,  67.7,  469.3, 188.2, 6,  64, 0.35, 0.70),
            new KeyShot("beat05_spiral_KF5c",   488, 48.03, 59.7, 467.69, -28.0, -46, 64, 0.40, 0.75),
            new KeyShot("beat05_spiral_KF5d",   440, 11.96, 47.7, 437.32, 182.7, -14, 64, 0.40, 0.75),
            new KeyShot("beat06_clouds_KF6a",   624, 141.09, 93.7, 395.94, 157,  -12, 66, 0.45, 0.75),
            new KeyShot("beat06_clouds_KF6a2",  664, 126.7, 103.7, 358.8, 135,   -8, 66, 0.45, 0.75),
            new KeyShot("beat06_clouds_KF6b",   690, 107.9, 110.2, 341.1, 327.3, 70, 66, 0.12, 0.40),
            new KeyShot("beat07_top_KF7a",      800, 11.1,  137.7, 362.2, -32.7, -4, 64, 0.08, 0.35),
            new KeyShot("beat07_top_KF7b",      858, 0,     147.7, 418,   -1,    -0.5, 66, 0.08, 0.30),
            new KeyShot("beat07_top_KF7c",      858, 1.15,  147.7, 418,   98,    -46, 68, 0.30, 0.65, true),
        };

        // ---------------------------------------------------------------- cinematic gaze cues (14.5.x "Gaze")
        // (plan s, yaw relative to the path heading (+ right), pitch (+ up), hold seconds, pitch at the end of the hold)
        public static readonly GazeCue[] GazeCues =
        {
            new GazeCue(0, 0, 10, 8), new GazeCue(0.5, 2, 12, 10, 24), new GazeCue(30, 4, 4, 8), new GazeCue(60, 0, 2, 6), new GazeCue(80, 0, 12, 6),
            new GazeCue(100, 30, 5, 5), new GazeCue(125, 0, 10, 8, 55, speed: 0.8), new GazeCue(160, -35, 10, 4), new GazeCue(198, 0, 25, 8, 65, speed: 0.7), new GazeCue(207, 8, 60, 6, 35), new GazeCue(225, 0, 8, 4),
            new GazeCue(245, 0, 8, 5), new GazeCue(255, -50, -10, 4), new GazeCue(275, 0, 14, 6), new GazeCue(300, 15, 0, 3),
            new GazeCue(330, 45, 8, 6, speed: 0.7), new GazeCue(336, 50, 8, 8, 32, speed: 0.6), new GazeCue(345, 58, 32, 10, 40, speed: 0.5), new GazeCue(356, 62, 40, 6, 20, speed: 0.6), new GazeCue(385, 30, 12, 4), new GazeCue(396, 8, -2, 2),
            new GazeCue(405, 60, 10, 5), new GazeCue(420, 160, -8, 10), new GazeCue(431, 150, -12, 8, -14), new GazeCue(460, -90, -30, 8, -48), new GazeCue(476, 146, 24, 6), new GazeCue(484, -100, -40, 6, -46), new GazeCue(518, 90, 35, 6), new GazeCue(555, -60, 0, 5),
            new GazeCue(585, 0, 6, 6), new GazeCue(592, -30, -12, 8, speed: 0.5), new GazeCue(624, -26, -12, 8, speed: 0.5), new GazeCue(648, 90, 25, 6, speed: 0.7), new GazeCue(690, 90, 40, 8, 70, speed: 0.6), new GazeCue(735, -20, 0, 4),
            new GazeCue(758, 36, 14, 7, speed: 0.5), new GazeCue(775, 20, 8, 4, speed: 0.7), new GazeCue(800, 0, 12, 5, speed: 0.5), new GazeCue(815, 50, -45, 6, speed: 0.7), new GazeCue(828, -10, -18, 5, speed: 0.7), new GazeCue(840, 0, 4, 4, speed: 0.5), new GazeCue(850, -1, -0.5, 1e9),
        };
        public const double GazeSmoothTime = 1.2;
        public const double GazeSwayMaxDeg = 0.3;
        public const double GazeDefaultPitch = 3.0;
        public const double CinematicExtraMax = 60.0;   // seconds of slow-down allowed in total
    }

    public struct KeyShot
    {
        public string tag;
        public double s, x, y, z, yaw, pitch, fov, lumaMin, lumaMax;
        public bool wide;
        public KeyShot(string tag, double s, double x, double y, double z, double yaw, double pitch, double fov, double lumaMin, double lumaMax, bool wide = false)
        { this.tag = tag; this.s = s; this.x = x; this.y = y; this.z = z; this.yaw = yaw; this.pitch = pitch; this.fov = fov; this.lumaMin = lumaMin; this.lumaMax = lumaMax; this.wide = wide; }
    }

    /// <summary>A cinematic look target: from plan s, hold (s) a yaw relative to the path heading and a pitch, eased to
    /// yawEnd / pitchEnd over the hold (slow pans), while the walk runs at speed x the walking speed (1 = no slow-down).</summary>
    public struct GazeCue
    {
        public double s, yaw, pitch, hold, pitchEnd, yawEnd, speed;
        public GazeCue(double s, double yaw, double pitch, double hold, double pitchEnd = double.NaN, double yawEnd = double.NaN, double speed = 1.0)
        {
            this.s = s; this.yaw = yaw; this.pitch = pitch; this.hold = hold;
            this.pitchEnd = double.IsNaN(pitchEnd) ? pitch : pitchEnd;
            this.yawEnd = double.IsNaN(yawEnd) ? yaw : yawEnd;
            this.speed = speed;
        }
    }
}
