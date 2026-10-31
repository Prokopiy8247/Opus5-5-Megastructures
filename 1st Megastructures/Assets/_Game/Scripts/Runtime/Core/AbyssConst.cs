using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Single source of truth for every number of MASTER_PROMPT Part 14 (14.4 layout, 14.5 beats, 14.12 constants).
    /// Units: metres, seconds, degrees. Unity left-handed, Y up. Yaw runs from +Z toward +X; pitch positive upward.
    /// Generators, colliders, tests and the QA harness all read from here so they cannot drift apart.
    /// </summary>
    public static class AbyssConst
    {
        // ---------------------------------------------------------------- player
        public const float V_WALK = 1.5f;
        public const float V_BRISK = 2.5f;
        public const float EYE = 1.7f;
        public const float CAPSULE_R = 0.35f;
        public const float CAPSULE_H = 1.8f;
        public const float ACCEL_SMOOTH = 0.2f;
        public const float EYE_SMOOTH = 0.15f;
        public const float PITCH_LIMIT = 89.5f;
        public const float FOV_DEFAULT = 64f;
        public const float CAMERA_NEAR = 0.1f;

        // ---------------------------------------------------------------- path (14.4)
        public const float POLYLINE_LENGTH = 802.54f;
        /// <summary>
        /// Walked length with the W6 fillet. The spec's 801.25 assumed 2r - pi r/2 for a straight-to-straight corner; the W6 corner
        /// joins a line to the r 25 ledge arc, and the exact r 3 S-fillet tangent to both shortens the path by 1.132 m, giving 801.408
        /// (Part 1.6.2 arbitration, logged in DEVELOPMENT.md).
        /// </summary>
        public const float WALK_LENGTH = 801.41f;
        public const float TOTAL_DURATION = 658.03f;
        public const float FILLET_R = 3f;

        /// <summary>Waypoint floor positions W0..W18 (y = floor height; eye = floor + EYE).</summary>
        public static readonly Vector3[] W =
        {
            new Vector3(0f, 0f, 0f),          // W0 start
            new Vector3(0f, 0f, 40f),         // W1 stair top
            new Vector3(0f, -3f, 44.48f),     // W2 stair bottom
            new Vector3(0f, -3f, 60f),        // W3 exit doorway
            new Vector3(0f, -3f, 66f),        // W4 bridge start (end of landing)
            new Vector3(0f, -3f, 216f),       // W5 bridge end
            new Vector3(0f, -3f, 219f),       // W6 ledge centreline, alpha 0
            new Vector3(25f, -5f, 244f),      // W7 alpha pi/2
            new Vector3(0f, -7f, 269f),       // W8 alpha pi, heading -X
            new Vector3(-32f, -7f, 269f),     // W9 hall portal
            new Vector3(-142f, -7f, 269f),    // W10 hall exit
            new Vector3(-152f, -7f, 269f),    // W11 platform edge (top dock)
            new Vector3(-162f, -7f, 269f),    // W12 platform centre, top dock
            new Vector3(-45f, -487f, 269f),   // W13 platform centre, bottom dock (time driven)
            new Vector3(-45f, -487f, 259f),   // W14 platform -Z edge, gate
            new Vector3(-45f, -487f, 60f),    // W15 tunnel mouth (shaft side)
            new Vector3(-45f, -487f, 0f),     // W16 the Face
            new Vector3(-45f, -487f, -14f),   // W17 balcony edge
            new Vector3(-45f, -487f, -74f),   // W18 goal door
        };

        /// <summary>Path distance d (PolylineLength parametrisation) at each waypoint.</summary>
        public static readonly float[] WD =
        {
            0f, 40f, 44.48f, 60f, 66f, 216f, 219f, 258.27f, 297.54f, 329.54f, 439.54f,
            449.54f, 459.54f, 459.54f, 469.54f, 668.54f, 728.54f, 742.54f, 802.54f
        };

        // ledge arc W6..W8 around C1
        public static readonly Vector2 ARC_CENTRE = new Vector2(0f, 244f); // (x, z)
        public const float ARC_RADIUS = 25f;
        public const float ARC_LENGTH = 78.54f;  // 25 * pi

        // beats: start d, nominal start time, end time
        public static readonly float[] BEAT_START_D = { 0f, 60f, 216f, 297.54f, 439.54f, 469.54f, 668.54f };
        public static readonly float[] BEAT_START_T = { 0f, 40f, 144f, 198.36f, 293.03f, 436.03f, 568.70f };
        public static readonly string[] BEAT_NAMES = { "Gallery", "Bridge", "Ledge", "Hall", "Lift", "Fan", "Reveal" };
        public const float D_LIFT = 459.54f;            // d frozen during dwell and ride
        public const float T_PLATFORM_CENTRE = 306.36f;
        public const float T_RIDE_START = 308.36f;
        public const float T_RIDE_END = 428.36f;
        public const float T_GATE_OPEN = 429.36f;
        public const float BEAT_HYSTERESIS = 20f;

        /// <summary>Nominal time at path distance d at 1.5 m/s including dwell and ride (used to set the clock for captures).</summary>
        public static double NominalTimeAtD(float d, bool afterRide)
        {
            if (!afterRide && d <= D_LIFT) return d / V_WALK;
            return T_GATE_OPEN + (Mathf.Max(d, D_LIFT) - D_LIFT) / V_WALK;
        }

        // ---------------------------------------------------------------- beat 1 gallery
        public const float GALLERY_L = 60f, GALLERY_W = 2.4f, GALLERY_H = 2.5f;
        public const int STAIR_COUNT = 16;
        public const float STAIR_RISE = 0.1875f, STAIR_RUN = 0.28f, STAIR_Z0 = 40f, STAIR_Z1 = 44.48f, STAIR_DROP = 3f;
        public const float EXIT_DOOR_H = 2.0f, EXIT_DOOR_W = 1.2f, EXIT_DOOR_Z = 60f, EXIT_JAMB_W = 0.6f, EXIT_DEPTH = 0.8f;
        public const float PRESSURE_DOOR_H = 2.0f, PRESSURE_DOOR_W = 1.1f, PRESSURE_DOOR_Z = 22f;
        public const float DRAIN = 0.3f, DRAIN_Z = 12f;

        // ---------------------------------------------------------------- shaft S0 and lattice
        public static readonly Vector2 SHAFT_CENTRE = new Vector2(0f, 410f);   // (x, z)
        public const float SHAFT_APOTHEM = 350f, SHAFT_FLAT_TO_FLAT = 700f, SHAFT_R_CIRC = 404.15f;
        public const float SHAFT_NEAR_Z = 60f, SHAFT_FAR_Z = 760f;
        public const float SHAFT_TOP_Y = 1200f, SHAFT_BOTTOM_Y = -3200f;
        public const float WEB = 60f, LATTICE_PITCH = 760f;
        public static readonly float[] SHAFT_FACE_NORMAL_DEG = { 30f, 90f, 150f, 210f, 270f, 330f }; // from +X toward +Z
        public const float FACE_WIDTH = 473.4f;
        public const float TERRACE_Y = -1087f;
        public static readonly Vector2 LATTICE_B1 = new Vector2(0f, 760f);
        public static readonly Vector2 LATTICE_B2 = new Vector2(658.18f, 380f);

        public const float RIB_SPACING = 60f, BELT_SPACING = 240f;
        public const int RIB_LEVELS = 74, RIB_COUNT = 55, BELT_COUNT = 19;

        /// <summary>
        /// Ring level k (0..73) sits at RING_Y0 - 60 k; level k is a belt when k % 4 == 0 (19 belts), otherwise a rib (55 ribs).
        /// Arbitration (Part 1.6.2, DEVELOPMENT.md): the spec's y = 1200 - 60 k puts a ring (a belt, k = 20) at y = 0, through the
        /// gallery doorway, the landing and the bridge deck at y = -3. Shifting every level by -30 m keeps the 60/240 m rhythm and
        /// the counts, and leaves 27 m of clear wall above and below the walkway. Levels crossing the fan bore on the near flat are gapped.
        /// </summary>
        public const float RING_Y0 = 1170f;
        public static float RingY(int k) => RING_Y0 - RIB_SPACING * k;
        public static bool RingIsBelt(int k) => k % 4 == 0;
        public const float RIB_INNER_APOTHEM = 344f, RIB_HEIGHT = 6f, RIB_BEVEL = 0.03f;
        public const float BELT_INNER_APOTHEM = 330f, BELT_HEIGHT = 12f, BELT_BEVEL = 0.05f;

        // columns (x, z, radius)
        public static readonly Vector3[] COLUMNS =
        {
            new Vector3(0f, 244f, 22f),     // C1
            new Vector3(-170f, 520f, 36f),  // C2
            new Vector3(180f, 600f, 40f),   // C3
            new Vector3(-60f, 700f, 28f),   // C4
        };
        public const float COLUMN_TOP_Y = 1200f, COLUMN_BOTTOM_Y = -3000f;
        public const float COLUMN_BAND_SPACING = 120f, COLUMN_BAND_H = 2f, COLUMN_BAND_PROTRUSION = 0.4f;

        // ---------------------------------------------------------------- beat 2 bridge
        public const float BRIDGE_L = 150f, BRIDGE_W = 1.2f, BRIDGE_TRUSS_DEPTH = 6f, BRIDGE_BAY = 6f;
        public const float BRIDGE_Z0 = 66f, BRIDGE_Z1 = 216f, BRIDGE_Y = -3f, BRIDGE_LAMP_SPACING = 37.5f;
        public static readonly float[] BRIDGE_LAMP_Z = { 66f, 103.5f, 141f, 178.5f, 216f };
        public const float BRIDGE_LAMP_OUTSIDE = 0.9f, BRIDGE_LAMP_POST_H = 1.1f;
        public const float LANDING_W = 8f, LANDING_L = 6f;
        /// <summary>Hoist block centre at y(0). Spec z = 758 would sink the 10 m cube 3 m into the far flat (z = 760); 754.5 leaves a 0.5 m gap to its guide.</summary>
        public static readonly Vector3 HOIST_POS = new Vector3(140f, 150f, 754.5f); // x 140 (spec 60): at x 60 C1 hides the hoist from the whole bridge
        public const float HOIST_SIZE = 10f, HOIST_PERIOD = 583.3f, HOIST_U0 = 0.2166f;
        public const float BOLT_DROP_D = 72f;

        // ---------------------------------------------------------------- beat 3 ledge
        public const float LEDGE_W = 6f, LEDGE_R_IN = 22f, LEDGE_R_OUT = 28f, LEDGE_R_CENTRE = 25f, LEDGE_KERB = 0.15f;
        public const float LEDGE_DROP = 4f, LEDGE_COLLAR_THICK = 2.5f, LEDGE_CORBEL = 8f;
        public const float HATCH_ALPHA = 1.85f, HATCH_W = 0.9f, HATCH_H = 2.0f;
        public static readonly Vector3 HATCH_POS = new Vector3(21.1f, -5.36f, 250.1f);
        public static readonly float[] LEDGE_LAMP_ALPHA = { 0.6f, 1.4f, 2.2f, 2.9f };
        public const float APRON_L = 32f, APRON_W = 14f, APRON_Z0 = 262f, APRON_Z1 = 276f;
        public const float COLLIDER_INNER_R = 22.4f;

        // ---------------------------------------------------------------- beat 4 hall
        public const float HALL_X0 = -32f, HALL_X1 = -142f, HALL_Z0 = 199f, HALL_Z1 = 339f;
        public const float HALL_FLOOR_Y = -21f, HALL_ROOF_Y = 39f, HALL_HULL_BOTTOM_Y = -45f, HALL_CHAMFER = 12f;
        public const float HALL_OPENING_W = 20f, HALL_OPENING_H = 24f;
        public const float CATWALK_W = 1.5f, CATWALK_Y = -7f, RAIL_H = 1.1f;
        public const float PISTON_BORE = 12f, PISTON_ROD = 1.6f, PISTON_STROKE = 24f;
        public const float CRANK_R = 12f, CRANK_L = 32f;
        public const double CRANK_OMEGA = System.Math.PI / 60.0;
        public const float WHEEL_D = 30f, CRANK_U_A = 20f, CRANK_U_B = 90f;
        public static readonly float[] BANK_V = { -18f, 18f, 48f, -48f };           // IL(A), IR(B), OR(A), OL(B)
        public static readonly bool[] BANK_TYPE_A = { true, false, true, false };
        public static readonly float[] BANK_PHASE_DEG = { 0f, 45f, 90f, 135f };
        public const float CRANK_PERIOD = 120f, STROKE_TIME = 60f, DEAD_CENTRE_INTERVAL = 15f;
        public const double PORTAL_T = 219.69;

        /// <summary>Crank angle of the inner-left bank (degrees) from the global scene clock.</summary>
        public static double CrankThetaIL(double t) => 60.0 + 3.0 * (t - PORTAL_T);

        // ---------------------------------------------------------------- beat 5 lift and city
        public const float LIFT_SIZE = 20f, LIFT_THICK = 1.2f, LIFT_WALKABLE = 18f;
        public static readonly Vector3 LIFT_TOP = new Vector3(-162f, -7f, 269f);
        public static readonly Vector3 LIFT_BOTTOM = new Vector3(-45f, -487f, 269f);
        public const float LIFT_DROP = 480f, LIFT_DURATION = 120f, LIFT_CRUISE = 4.8f, LIFT_RAMP = 20f;
        public const float LIFT_DWELL = 2f, LIFT_GATE_DELAY = 1f;
        public const float GUIDE_RAIL_Z0 = 283f, GUIDE_RAIL_Z1 = 289f;
        public const int GUIDE_RAIL_SEGMENTS = 48;

        /// <summary>Lift travel s(t) in metres for ride seconds t in [0, 120] (14.5.5).</summary>
        public static double LiftS(double t)
        {
            // exact double constants (the float LIFT_* fields are not exactly representable: 4.8f = 4.80000019)
            const double drop = 480.0, duration = 120.0, ramp = 20.0, cruise = 4.8;
            if (t <= 0) return 0;
            if (t >= duration) return drop;
            if (t < ramp) { double x = t / ramp; return 96.0 * (x * x * x - x * x * x * x / 2.0); }
            if (t <= duration - ramp) return 48.0 + cruise * (t - ramp);
            double y = (duration - t) / ramp;
            return drop - 96.0 * (y * y * y - y * y * y * y / 2.0);
        }

        public const float CITY_CELL_W = 3.0f, CITY_CELL_H = 3.5f, CITY_WINDOW_W = 1.2f, CITY_WINDOW_H = 1.6f;
        public const float CITY_MODULE_W = 24f, CITY_MODULE_H = 21f;
        public const int CITY_COLUMNS = 17, CITY_ROWS = 24;
        public const float CITY_TOP_Y = -60f, CITY_BOTTOM_Y = -564f;
        public const float CITY_LIT = 0.06f, CITY_NEAR_TIER = 170f;
        public static readonly float[] CITY_FACES_DEG = { 210f, 150f, 270f };
        public const float CITY_FAN_EXCLUSION_R = 156f;

        // ---------------------------------------------------------------- beat 6 fan
        public static readonly Vector3 FAN_CENTRE = new Vector3(-45f, -317f, 50f);
        public const float FAN_R_OUT = 150f, FAN_R_INNER = 138f, FAN_R_TIP = 138f, FAN_R_HUB = 40f;
        public const int FAN_BLADES = 12;
        public const float FAN_PERIOD = 40f, FAN_BLADE_PASS = 40f / 12f;
        public const int BEAM_COUNT = 12;
        public const float BEAM_WIDTH_DEG = 12f, BEAM_R0 = 70f, BEAM_R1 = 130f, BEAM_LENGTH = 300f, BEAM_PITCH_DEG = 18f;
        public const float GANTRY_L = 199f, GANTRY_W = 3.0f, GANTRY_Y = -487f;
        public const float GANTRY_Z0 = 259f, GANTRY_Z1 = 60f;

        // ---------------------------------------------------------------- beat 7 reveal
        public const float TUNNEL_L = 60f, TUNNEL_W = 3.6f, TUNNEL_H = 3.4f;
        public const float BALCONY_W = 20f, BALCONY_D = 14f, BALCONY_THICK = 2.5f, BALCONY_ALTITUDE = 600f;
        public const float PIER_L = 60f, PIER_W = 3.0f;
        public const float GOAL_DOOR_H = 2.0f, GOAL_DOOR_W = 1.0f, GOAL_DOOR_Z = -74f;
        public const float HONEY_TIER0 = 1600f, HONEY_TIER1 = 6000f, HONEY_TIER2 = 28000f, HONEY_TIER2_START = 5400f;
        public const int WORLD_CELLS_28KM = 4920;
        public const float D_EXTERIOR_ON = 640f, D_SKY_BLEND_END = 668.54f, D_DARK_CARD = 735f;
        public const float DOOR_TRIGGER_Z = -72.8f, LAMP_IGNITE_Z = -2f;

        // ---------------------------------------------------------------- rendering
        public const float MIN_LINE_PX = 1.5f;
        public const float NOISE_WRAP = 1332f;
        public const float MAX_EXPOSURE_RATE = 0.35f, FOG_RATE = 0.15f, REVERB_RATE = 6f;
        public const float NEAR_TIER = 150f;

        /// <summary>Camera far plane per beat index 0..6 (beat 7 tunnel), plus index 7 = beat 7 outside.</summary>
        public static readonly float[] FAR_PLANE = { 200f, 4500f, 4500f, 600f, 4500f, 4500f, 300f, 30000f };
        public static readonly float[] FOV_PER_BEAT = { 62f, 66f, 64f, 62f, 66f, 66f, 62f, 68f };

        /// <summary>Unity rotation for a yaw/pitch pair in the spec convention (yaw from +Z toward +X, pitch up positive).</summary>
        public static Quaternion YawPitch(float yawDeg, float pitchDeg) => Quaternion.Euler(-pitchDeg, yawDeg, 0f);

        /// <summary>Direction for a yaw/pitch pair (for lights: the direction of travel).</summary>
        public static Vector3 Dir(float yawDeg, float pitchDeg) => YawPitch(yawDeg, pitchDeg) * Vector3.forward;
    }
}
