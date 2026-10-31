using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Cinematic gaze data of Part 14.5.1-14.5.7 ("Gaze" lines) and the 8 holds of 14.5 / 14.12 (49 s).
    /// Three layers, all pure data + pure functions (allocation-free):
    ///  1. <see cref="Keys"/>: s-based keyframes (yaw / pitch), smootherstep between consecutive keys. Rel = yaw relative
    ///     to the walker's path heading ("forward"), otherwise absolute world yaw (0 = +Z, 90 = +X).
    ///  2. <see cref="Holds"/>: the walker stops at s and plays time-based sub-steps (each step blends from the previous
    ///     pose to its own with smootherstep and ends at TEnd seconds), then releases back to the key schedule.
    ///  3. <see cref="Glances"/>: the same sub-steps without stopping (triggered when s is crossed).
    /// A NaN step component means "keep the live key value". The AutoWalker's critically damped follower (omega 3 rad/s)
    /// and the handheld sway are applied on top. Key-shot poses (14.10) are used where a gaze line ends on a key shot.
    /// </summary>
    public static class GazeSchedule
    {
        public struct Key
        {
            public float S, Yaw, Pitch; public bool Rel;
            public Key(float s, float yaw, float pitch, bool rel) { S = s; Yaw = yaw; Pitch = pitch; Rel = rel; }
        }

        public struct Step
        {
            public float TEnd, Yaw, Pitch;
            public Step(float tEnd, float yaw, float pitch) { TEnd = tEnd; Yaw = yaw; Pitch = pitch; }
        }

        public struct Hold
        {
            public float S, Duration, Release; public Step[] Steps;
        }

        const bool F = true;   // forward / relative to the path heading
        const bool A = false;  // absolute world yaw

        /// <summary>s-based gaze keys. Comments quote the 14.5 gaze line each key transcribes.</summary>
        public static readonly Key[] Keys =
        {
            // 14.5.1 beat 1: "t 0-2.5 s forward; toward s 5.1 turn to KS01 pose (2.5 s), hold 5 s; back to yaw 0 over
            // 2.5 s; s 9.5 pitch +6; gangway pitch +4"
            new Key(0f, 0f, 0f, F),
            new Key(2.4f, 0f, 0f, F),
            new Key(5.1f, -45f, 25f, A),          // KS01 (hold 5 s)
            new Key(5.15f, 0f, 4f, F),            // back to yaw 0 (the 2.5 s release blends this)
            new Key(9.5f, 0f, 6f, F),             // s 9.5 pitch +6 (through the hatch)
            new Key(10.5f, 0f, 4f, F),            // gangway pitch +4
            // 14.5.2 beat 2: "s 24 pitch +4 hold 2.5 s; s 40-110 pitch 4 -> 10; crest hold 8 s: KS02 2.5 s, pitch to 38
            // over 2.5 s, hold 1.5 s, back to 10 over 1.5 s; s 190-205 yaw -38 to the belvedere (2 s, 1 s, 2 s);
            // s 240-276 pitch +16"
            new Key(27.75f, 0f, 4f, F),           // s 24 pitch +4 held 2.5 s (3.75 m)
            new Key(40f, 0f, 4f, F),
            new Key(110f, 0f, 10f, F),
            new Key(142f, 0f, 10f, F),
            new Key(150f, 0f, 14f, A),            // KS02 at the crest (hold 8 s)
            new Key(150.05f, 0f, 10f, F),         // the hold ends at pitch 10
            new Key(240f, 0f, 10f, F),
            new Key(246f, 0f, 16f, F),            // s 240-276 pitch +16
            new Key(272f, 0f, 16f, F),
            // 14.5.3 beat 3: "s 276 pitch +8; s 280 follow the shadow to yaw -30; s 312 pitch +30 / yaw -45, track the
            // hull, relax to KS04 pose at 364; then to yaw 0 / pitch +4 by s 380 (looking through the portal); under the
            // arch (s 396-404) pitch +4; from s 404 rise to pitch +10 by s 411"
            new Key(278f, 0f, 8f, F),
            new Key(280f, 0f, 8f, F),
            new Key(292f, -30f, 10f, A),          // follow the shadow to yaw -30
            new Key(312f, -45f, 30f, A),
            new Key(364f, -12f, 14f, A),          // KS04
            new Key(380f, 0f, 4f, F),
            new Key(404f, 0f, 4f, F),             // under the arch pitch +4
            new Key(411f, 0f, 10f, F),            // the release: ship centre ahead
            // 14.5.4 beat 4: "s 411 yaw 0 / pitch +10; s 420-440 to yaw -58 / pitch +20 (3 s), hold 6 s; s 446-470 back
            // to yaw 0 / pitch +25; s 485 up to +28 over 3 s, hold 6 s at s 505 (KS06), back to +10 by s 530;
            // s 545-558 the lit mouth (pitch +2)"
            new Key(434f, 0f, 10f, F),
            new Key(440f, -58f, 20f, A),          // KS05 pose (hold 6 s)
            new Key(446f, -58f, 20f, A),
            new Key(470f, 0f, 25f, F),
            new Key(485f, 0f, 25f, F),
            new Key(489.5f, 0f, 28f, F),          // up to +28 over 3 s (4.5 m)
            new Key(505f, 0f, 28f, A),            // KS06 (hold 6 s)
            new Key(530f, 0f, 10f, F),
            new Key(538f, 0f, 10f, F),
            new Key(545f, 0f, 2f, F),             // the lit mouth
            new Key(558f, 0f, 2f, F),
            new Key(590f, 0f, 3f, F),             // underpass: forward through the dog-leg
            // 14.5.5 beat 5: "s 594 yaw 90 / pitch +4; hold 6 s at 606: pitch 16 -> 45 over 3 s, 1 s, back over 2 s;
            // s 676-700 follow the turn to yaw 20 / pitch +30; s 740 yaw 60; s 760 yaw 90"
            new Key(594f, 0f, 4f, F),             // heading is +X here: yaw 90
            new Key(606f, 80f, 16f, A),           // KS07 (hold 6 s)
            new Key(606.05f, 80f, 16f, A),
            new Key(620f, 0f, 12f, F),
            new Key(676f, 0f, 12f, F),
            new Key(700f, 20f, 30f, A),           // KS08: follow the turn to yaw 20 / pitch +30
            new Key(740f, 60f, 8f, A),            // s 740 yaw 60
            new Key(752f, 60f, 2f, A),            // KS09
            new Key(760f, 90f, 2f, A),            // s 760 yaw 90 (the door)
            // 14.5.6 beat 6: "s 774-830 forward; s 830-840 sway +-2 degrees; s 866-888 hold the light, pitch +2; hold 4 s
            // at 888" (the sway is SwayOverlay)
            new Key(774f, 0f, 0f, F),
            new Key(866f, 0f, 0f, F),
            new Key(870f, 0f, 2f, F),
            new Key(888f, 90f, 2f, A),            // hold 4 s at the mouth
            // 14.5.7 beat 7: "s 894-906 star ahead (pitch +4); hold 8 s at 912: KS12 3 s, drift left 6 degrees, pitch down
            // to -24 over 2.5 s, 1 s, back to +4 over 1.5 s; s 930-956 settle on the beacon; hold 6 s at 960"
            new Key(894f, 0f, 4f, F),
            new Key(900f, RingConstants.StarOutYaw, 4f, A),   // star ahead (yaw 96)
            new Key(906f, RingConstants.StarOutYaw, 4f, A),
            new Key(912f, 92f, 9f, A),            // KS12 (hold 8 s)
            new Key(912.05f, 86f, 4f, A),         // the hold ends 6 degrees left, pitch +4
            new Key(930f, 90f, 3f, A),            // settle on the beacon
            new Key(956f, 90f, 3f, A),            // KS14
            new Key(960f, 90f, -6f, A),           // the beacon 1 m ahead (hold 6 s while the ending fades)
        };

        static readonly Step[][] HoldSteps =
        {
            new[] { new Step(5f, -45f, 25f) },                                                                  // s 5.1: KS01 5 s
            new[] { new Step(2.5f, 0f, 14f), new Step(5f, 6f, 38f), new Step(6.5f, 6f, 38f), new Step(8f, 0f, 10f) }, // s 150: KS02, KS03, back
            new[] { new Step(6f, -58f, 20f) },                                                                  // s 440: sunset (KS05)
            new[] { new Step(6f, 0f, 28f) },                                                                    // s 505: night arc (KS06)
            new[] { new Step(3f, 80f, 45f), new Step(4f, 80f, 45f), new Step(6f, 80f, 16f) },                   // s 606: wall reveal
            new[] { new Step(4f, 90f, 2f) },                                                                    // s 888: exit
            new[] { new Step(3f, 92f, 9f), new Step(5.5f, 86f, -24f), new Step(6.5f, 86f, -24f), new Step(8f, 86f, 4f) }, // s 912: swarm
            new[] { new Step(6f, 90f, -6f) },                                                                   // s 960: beacon
        };

        static readonly float[] HoldRelease = { 2.5f, 0.8f, 1.5f, 1.5f, 1.5f, 1.0f, 1.5f, 0f };

        /// <summary>The 8 cinematic holds; s and duration come from RingConstants.CinematicHolds (49 s in total).</summary>
        public static readonly Hold[] Holds = BuildHolds();

        static Hold[] BuildHolds()
        {
            var src = RingConstants.CinematicHolds;
            int n = Mathf.Min(src.Length, HoldSteps.Length);
            var h = new Hold[n];
            for (int i = 0; i < n; i++)
            {
                var steps = HoldSteps[i];
                // the last step always ends exactly at the hold duration
                var last = steps[steps.Length - 1];
                steps[steps.Length - 1] = new Step(src[i].y, last.Yaw, last.Pitch);
                h[i] = new Hold { S = src[i].x, Duration = src[i].y, Release = HoldRelease[i], Steps = steps };
            }
            return h;
        }

        /// <summary>Glances without stopping. Beat 2: "s 190-205 yaw -38 to the belvedere (2 s, 1 s, 2 s)", centred in
        /// the range (5 s = 7.5 m at 1.5 m/s, so it starts at s 193.75); pitch keeps the live key value.</summary>
        public static readonly Hold[] Glances =
        {
            new Hold { S = 193.75f, Duration = 3f, Release = 2f, Steps = new[] { new Step(2f, -38f, float.NaN), new Step(3f, -38f, float.NaN) } },
        };

        /// <summary>Beat 6 "s 830-840 sway +-2 degrees": one slow left-right look over the 10 m, faded in and out.</summary>
        public static float SwayOverlay(float s)
        {
            if (s <= 830f || s >= 840f) return 0f;
            float env = RingMath.Smoothstep(830f, 832.5f, s) * (1f - RingMath.Smoothstep(837.5f, 840f, s));
            return 2f * Mathf.Sin(2f * Mathf.PI * (s - 830f) / 10f) * env;
        }

        static float Resolve(in Key k, float heading) => k.Rel ? heading + k.Yaw : k.Yaw;

        /// <summary>Key-schedule gaze at progress s; heading = the walker's path heading (deg).</summary>
        public static void Evaluate(float s, float heading, out float yaw, out float pitch)
        {
            var k = Keys;
            if (s <= k[0].S) { yaw = Resolve(k[0], heading); pitch = k[0].Pitch; return; }
            int last = k.Length - 1;
            if (s >= k[last].S) { yaw = Resolve(k[last], heading); pitch = k[last].Pitch; return; }
            int i = 0;
            while (i < last - 1 && s >= k[i + 1].S) i++;
            float u = RingMath.Smootherstep(k[i].S, k[i + 1].S, s);
            float y0 = Resolve(k[i], heading), y1 = Resolve(k[i + 1], heading);
            yaw = y0 + Mathf.DeltaAngle(y0, y1) * u + SwayOverlay(s);
            pitch = Mathf.Lerp(k[i].Pitch, k[i + 1].Pitch, u);
        }

        /// <summary>Key-schedule gaze at progress s with the walker's heading on the filleted walk path (1.5 m
        /// look-ahead), for QA cameras (RingQaHooks.Gaze by reflection). Holds and glances are time based and not included;
        /// the keys already end on the key-shot pose at every hold point.</summary>
        public static bool Evaluate(float s, out float yaw, out float pitch)
        {
            var path = AutoWalker.Path;
            float d = path.DAtSharp(Mathf.Clamp(s, 0f, RingConstants.PathLength));
            float heading = path.HeadingAt(Mathf.Min(d + 1.5f, path.Length));
            Evaluate(s, heading, out yaw, out pitch);
            return true;
        }

        /// <summary>Pose of a hold / glance at time tau (s since its start). start = the pose when it began; live = the
        /// current key-schedule pose (used for NaN components).</summary>
        public static void StepPose(Hold h, float tau, float startYaw, float startPitch, float liveYaw, float livePitch, out float yaw, out float pitch)
        {
            var st = h.Steps;
            float py = startYaw, pp = startPitch, t0 = 0f;
            for (int i = 0; i < st.Length; i++)
            {
                float sy = float.IsNaN(st[i].Yaw) ? liveYaw : st[i].Yaw;
                float sp = float.IsNaN(st[i].Pitch) ? livePitch : st[i].Pitch;
                if (tau < st[i].TEnd || i == st.Length - 1)
                {
                    float u = RingMath.Smootherstep(t0, st[i].TEnd, tau);
                    yaw = py + Mathf.DeltaAngle(py, sy) * u;
                    pitch = Mathf.Lerp(pp, sp, u);
                    return;
                }
                py = sy; pp = sp; t0 = st[i].TEnd;
            }
            yaw = startYaw; pitch = startPitch;
        }

        /// <summary>Cinematic lateral offset in the capsule (14.5.1): x(s) = -0.88 smootherstep((s - 3.0) / 2.1) for
        /// s 3.0-5.1 (the eye reaches KS01), held during the 5 s window, back to 0 by s 9.0. Positive = right of travel.</summary>
        public static float LateralOffset(float s)
        {
            const float x = -0.88f, s0 = 3.0f, s1 = 5.1f, s2 = 9.0f;
            if (s <= s0 || s >= s2) return 0f;
            if (s <= s1) return x * RingMath.Smootherstep(s0, s1, s);
            return x * (1f - RingMath.Smootherstep(s1, s2, s));
        }
    }
}
