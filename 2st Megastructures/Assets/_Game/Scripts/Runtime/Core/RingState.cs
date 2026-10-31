using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Per-frame world state. Single writer: <see cref="ProgressDirector"/> (and the CaptureHarness through it).
    /// Everything that animates reads these values, never Time.time directly (capture mode freezes or steps them).
    /// </summary>
    public static class RingState
    {
        /// <summary>Progress along the sharp polyline (m).</summary>
        public static float S;
        /// <summary>Game time since the walk started (s), frozen/stepped in capture mode. Double for long-run safety.</summary>
        public static double T;
        /// <summary>Animation time wrapped to [0, 7200) s for shaders.</summary>
        public static float AnimT;
        /// <summary>Unscaled delta of the current frame (0 when paused or frozen).</summary>
        public static float Dt;
        /// <summary>Capture mode: time branches equal progress branches, followers equal targets, slew limiters bypassed.</summary>
        public static bool Capture;
        /// <summary>Frame sequences keep the slew limiters on even in capture mode.</summary>
        public static bool SequenceMode;
        public static bool Paused;
        public static bool Cinematic;
        public static bool AutoTest;
        /// <summary>Ark phase u (0..1 after the follower) and the sunset phase u (unclamped, &lt; 0 before s 411).</summary>
        public static float ArkU;
        public static float SunsetU;
        /// <summary>Night amount N(u), star visible fraction V(u).</summary>
        public static float Night;
        public static float StarV = 1f;
        /// <summary>Current world set (0 valley, 1 plain, 2 tunnel, 3 outside) after hysteresis.</summary>
        public static int Set;
        /// <summary>Optional debug override of the set (-set N); -1 = none.</summary>
        public static int SetOverride = -1;
        /// <summary>Beat index 0..6.</summary>
        public static int Beat;
        /// <summary>Wind gust 0..1 published by the AudioDirector (the W1 gust gain).</summary>
        public static float Gust = 0.5f;
        /// <summary>Felt-cue envelope of beat 3 (0..1).</summary>
        public static float ArkCue;
        /// <summary>Goal reached (s &gt;= 959) and the ending started.</summary>
        public static bool GoalReached;
        public static int FrameIndex;

        public static void Reset()
        {
            S = 0f; T = 0.0; AnimT = 0f; Dt = 0f; Capture = false; SequenceMode = false; Paused = false; Cinematic = false; AutoTest = false;
            ArkU = 0f; SunsetU = -4f; Night = 0f; StarV = 1f; Set = 0; SetOverride = -1; Beat = 0; Gust = 0.5f; ArkCue = 0f; GoalReached = false; FrameIndex = 0;
        }
    }
}
