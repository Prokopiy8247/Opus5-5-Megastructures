using UnityEngine;

namespace Megastructure
{
    public enum LiftPhase
    {
        DockedTop = 0,   // waiting at the top dock, player not yet aboard
        Dwell = 1,       // player aboard, 2 s dwell, boarding rail closing
        Riding = 2,      // 120 s ride
        Arrived = 3,     // at the bottom dock, gate opens after 1 s
        Left = 4,        // player has stepped off at the bottom
    }

    /// <summary>
    /// Shared per-frame runtime state, written by the systems that own each value (noted per field) and read by everyone else.
    /// Plain static fields: zero allocation, trivially readable from beats, audio (main-thread copy) and QA.
    /// </summary>
    public static class GameState
    {
        /// <summary>Path distance d in metres (PolylineLength parametrisation). Owner: ProgressDirector. Frozen at D_LIFT during dwell and ride.</summary>
        public static float D;

        /// <summary>Extended progress E: d before the ride, D_LIFT + rideSeconds during dwell/ride, d + 121 after the ride. Monotonic in the nominal walk. Owner: ProgressDirector.</summary>
        public static float E;

        /// <summary>Current beat index 1..7 (with hysteresis). Owner: BeatDirector.</summary>
        public static int Beat = 1;

        /// <summary>Lift state. Owner: LiftController.</summary>
        public static LiftPhase Lift = LiftPhase.DockedTop;

        /// <summary>Ride seconds: -1 before departure, 0..120 during the ride, &gt;120 after (keeps counting). Owner: LiftController.</summary>
        public static float RideSeconds = -1f;

        /// <summary>s / 480 in [0, 1]. Owner: LiftController.</summary>
        public static float RideU;

        /// <summary>True once the ride has started (selects the post-ride branch of the timeline). Owner: LiftController.</summary>
        public static bool RideStarted;

        /// <summary>Main camera world position and rotation this frame (after CameraRig.LateUpdate of the previous frame). Owner: CameraRig.</summary>
        public static Vector3 CameraPos;
        public static Quaternion CameraRot = Quaternion.identity;
        public static Camera MainCamera;

        /// <summary>Player feet position (world). Owner: PlayerController.</summary>
        public static Vector3 PlayerPos;

        /// <summary>Modes. Owner: GameBootstrap / CliArgs.</summary>
        public static bool Cinematic;
        public static bool CaptureMode;
        public static bool AutoTest;
        public static bool Paused;
        public static bool Ended;

        /// <summary>Set when the player reaches the door; drives the ending. Owner: Beat07.</summary>
        public static bool GoalReached;

        /// <summary>SceneClock.RealTime at which GoalReached became true (-1 before). Owner: Beat07.</summary>
        public static double GoalRealTime = -1;

        /// <summary>True while the cinematic autowalk holds H7 on the balcony (WIND +4 dB). Owner: AutoWalker.</summary>
        public static bool HoldH7;

        /// <summary>Master audio/picture fade 0..1 of the title timeline (0 = black/silent). Owner: FadeController.</summary>
        public static float PictureFade;

        /// <summary>Seed for every deterministic random choice (-seed N). Owner: CliArgs.</summary>
        public static uint Seed = 1;
    }
}
