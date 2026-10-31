using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Data tables of every beat's "Gaze." line (14.5.1-14.5.7) for the cinematic autowalk. Each table is a list of keys
    /// (time, yaw, pitch); between keys the look target is interpolated (smoothstep, or linear for long constant-rate pans),
    /// and the AutoWalker follows the target with a critically damped look (omega 2.5 rad/s) plus a tiny handheld sway.
    ///
    /// Time bases:
    /// - beats 1-4 and 6: seconds from the beat start at 1.5 m/s, i.e. (d - BEAT_START_D) / 1.5 (speed independent, so the
    ///   cinematic slowdowns shift the cues as the spec intends);
    /// - beat 5: <see cref="Beat5Walk"/> on d before the platform centre, then <see cref="Beat5Ride"/> on ride seconds
    ///   (the 2 s dwell maps to -2..0, the gate and exit continue past 120);
    /// - beat 7: the cinematic clock of <see cref="Beat7Clock"/> on z (the hold H7 of 14 s at z = -12 is inside it).
    ///
    /// Yaw modes: Relative (offset from the walked heading), Absolute (world yaw, unwrapped values such as 465 encode the
    /// direction of long pans), Target (direction to a named object, with an offset). Pitch: Absolute or Target (+ offset).
    /// Never roll. All evaluation is allocation-free.
    /// </summary>
    public static class GazeCues
    {
        public enum YawMode : byte { Relative = 0, Absolute = 1, Target = 2 }
        public enum PitchMode : byte { Absolute = 0, Target = 1 }

        /// <summary>
        /// Named look targets (positions from AbyssConst; CrossheadIR moves with the crank clock; Bolt is the falling bolt at
        /// the key's own fall second, see <see cref="BoltPosition"/>).
        /// </summary>
        public enum TargetId : byte { None = 0, Doorway, Hatch, Portal, RodIR, WheelIL, CrossheadIR, HallExit, Fan, Mouth, Door, Bolt }

        public struct Key
        {
            public float T;
            public YawMode YawMode;
            public PitchMode PitchMode;
            public TargetId Target;
            public float Yaw;     // offset (Relative / Target) or world yaw (Absolute), degrees
            public float Pitch;   // absolute pitch or offset to the target pitch, degrees
            public bool Linear;   // the segment arriving at this key is linear instead of smoothstep
        }

        /// <summary>What a key needs to resolve into a world yaw / pitch.</summary>
        public struct Context
        {
            public Vector3 Eye;
            public float HeadingYaw;
            public double SceneTime;
        }

        // ---------------------------------------------------------------- beat 7 hold H7 (14.5.7)
        public const float H7_Z = -12f;
        public const float H7_DURATION = 14f;
        public const float H7_T0 = 48f;
        public const float H7_T1 = H7_T0 + H7_DURATION;
        public const float H7_FOV_PUSH = -2f;           // 68 -> 66
        public const float H7_FOV_RETURN = 6f;          // seconds to ease back to 68 after the hold (62-68)
        public const float H7_PAN = 25f;

        // ---------------------------------------------------------------- key helpers
        static Key R(float t, float yaw, float pitch, bool linear = false) =>
            new Key { T = t, YawMode = YawMode.Relative, PitchMode = PitchMode.Absolute, Yaw = yaw, Pitch = pitch, Linear = linear };

        static Key A(float t, float yaw, float pitch, bool linear = false) =>
            new Key { T = t, YawMode = YawMode.Absolute, PitchMode = PitchMode.Absolute, Yaw = yaw, Pitch = pitch, Linear = linear };

        /// <summary>Look at a target (yaw and pitch toward it, plus offsets).</summary>
        static Key Tg(float t, TargetId id, float yawOff = 0f, float pitchOff = 0f, bool linear = false) =>
            new Key { T = t, YawMode = YawMode.Target, PitchMode = PitchMode.Target, Target = id, Yaw = yawOff, Pitch = pitchOff, Linear = linear };

        /// <summary>Yaw toward a target, absolute pitch.</summary>
        static Key TgP(float t, TargetId id, float yawOff, float absPitch, bool linear = false) =>
            new Key { T = t, YawMode = YawMode.Target, PitchMode = PitchMode.Absolute, Target = id, Yaw = yawOff, Pitch = absPitch, Linear = linear };

        // ---------------------------------------------------------------- tables (times in seconds, angles in degrees)

        /// <summary>Beat 1: 0-4 level; 4-8 pitch -15 to floor line and drain; 8-14 pan -20 to the wall fan and back; 28-40 doorway, pitch +2.</summary>
        public static readonly Key[] Beat1 =
        {
            R(0f, 0f, 0f), R(4f, 0f, 0f),
            R(6f, 0f, -15f), R(8f, 0f, -15f),
            R(11f, -20f, -2f), R(14f, 0f, 0f),
            R(28f, 0f, 0f),
            TgP(31f, TargetId.Doorway, 0f, 2f), TgP(40f, TargetId.Doorway, 0f, 2f),
        };

        // ---------------------------------------------------------------- beat 2 falling bolt (14.5.2, R2-09, lead decision D3)
        /// <summary>
        /// Beat-2 time (s, at 1.5 m/s: d = 60 + 1.5 t = 82.5) at which the falling bolt is released (D3: during the gaze's
        /// "pan right to the wall").
        /// </summary>
        public const float BOLT_RELEASE_T = 15f;
        /// <summary>
        /// Release point of the falling bolt (D3; Beat02_Bridge.BoltStart, which owns the bolt): just under a cold lamp
        /// bracketed 1.5 m in front of the inner face of the y -30 belt (k 20, inner apothem 330), 1 m below the belt centre, on
        /// the right-hand far flat whose outward normal is 30 degrees (AbyssConst.SHAFT_FACE_NORMAL_DEG[0], from +X toward +Z),
        /// 300 m from the bridge axis: x = 300 on the line 0.866 x + 0.5 (z - 410) = 328.5, so z = 547.4, y = -31.3. From the
        /// walker at the release (z 82.5) it sits at bearing 33 degrees, 3 degrees below the horizon, 555 m away.
        /// </summary>
        public const float BOLT_RELEASE_X = 300f, BOLT_LAMP_STANDOFF = 1.5f, BOLT_RELEASE_BELOW_BELT = 1.3f;
        public const int BOLT_BELT_K = 20;
        public const float BOLT_G = 9.81f;
        /// <summary>Fall seconds over which the bolt dissolves into the fog (14.5.2).</summary>
        public const float BOLT_FADE0 = 8f, BOLT_FADE1 = 12f;

        /// <summary>Release point of the bolt (in front of the belt of the 30-degree flat at x = BOLT_RELEASE_X).</summary>
        public static Vector3 BoltRelease
        {
            get
            {
                // the flat with outward normal n = (cos 30, sin 30) in (x, z): dot((x, z - 410), n) = apothem
                const float nx = 0.8660254f, nz = 0.5f;
                float a = AbyssConst.BELT_INNER_APOTHEM - BOLT_LAMP_STANDOFF;
                float z = AbyssConst.SHAFT_CENTRE.y + (a - nx * (BOLT_RELEASE_X - AbyssConst.SHAFT_CENTRE.x)) / nz;
                return new Vector3(BOLT_RELEASE_X, AbyssConst.RingY(BOLT_BELT_K) - BOLT_RELEASE_BELOW_BELT, z);
            }
        }

        /// <summary>The falling bolt's position at a fall second (free fall from <see cref="BoltRelease"/>; 0 before the release).</summary>
        public static Vector3 BoltPosition(float fallSeconds)
        {
            float t = Mathf.Max(0f, fallSeconds);
            Vector3 p = BoltRelease;
            p.y -= 0.5f * BOLT_G * t * t;
            return p;
        }

        /// <summary>Look at the falling bolt at the key's own fall second (yaw and pitch toward it, plus offsets).</summary>
        static Key Bolt(float t, float yawOff = 0f, float pitchOff = 0f, bool linear = false) =>
            new Key { T = t, YawMode = YawMode.Target, PitchMode = PitchMode.Target, Target = TargetId.Bolt, Yaw = yawOff, Pitch = pitchOff, Linear = linear };

        /// <summary>
        /// Beat 2: 0-6 level; 6-14 pitch -35 to the edge; 14-22 pan right to the wall and back; 22-48 level with a slow drift to
        /// +10 and back; 48-58 pitch -90 (held 5 s, clamped to -89.5 by the controller); 60-75 up +25 along C1; 80-104 level at the column.
        /// R2-09 / D3: the "pan right to the wall" lands on the belt lamp as the bolt is released (15 s) and then follows the
        /// bolt down (it sits 4-6 degrees above the frame centre, falling into the space below it) into its dissolve (fall
        /// seconds 8-12, beat time 23-27; tracked to fall second 10, about 45 degrees down, then it fades below the centre);
        /// the look is back level at 30 and the 22-48 drift runs over 30-48.
        /// </summary>
        public static readonly Key[] Beat2 =
        {
            R(0f, 0f, 2f), R(3f, 0f, 0f), R(6f, 0f, 0f),
            R(9f, 12f, -35f), R(13f, 12f, -35f),
            // tracking segments are linear so the look moves steadily with the fall (the damped follower rounds the joints)
            Bolt(16f, 0f, -4f), Bolt(18f, 0f, -5f, true), Bolt(20f, 0f, -6f, true), Bolt(22.5f, 0f, -6f, true),
            Bolt(25f, 0f, -4f, true),
            R(30f, 0f, 0f),
            R(40f, 10f, 0f, true), R(48f, 0f, 0f, true),
            R(51f, 0f, -90f), R(56f, 0f, -90f), R(59f, 0f, 0f),
            R(60f, 0f, 0f), R(64f, 0f, 25f), R(75f, 0f, 25f),
            R(80f, 0f, 0f), R(104f, 0f, 0f),
        };

        /// <summary>
        /// Beat 3: 0-6 toward the column (yaw -60); 6-16 up to pitch +50; 16-26 to the hatch (about 30 left, pitch -5, hold 2);
        /// 30-44 look back (yaw +80) at the bridge and C3; 44-54 forward.
        /// </summary>
        public static readonly Key[] Beat3 =
        {
            R(0f, 0f, 0f),
            R(6f, -60f, 5f), R(8f, -60f, 5f),
            R(14f, -60f, 50f), R(16f, -60f, 50f),
            Tg(21f, TargetId.Hatch), Tg(28f, TargetId.Hatch),
            // through the heading first, so the turn to the look-back is always to the right (never a shortest-path flip)
            R(31f, 0f, 2f), R(35f, 80f, 4f), R(44f, 80f, 4f),
            R(52f, 0f, 0f), R(54.36f, 0f, 0f),
        };

        /// <summary>
        /// Beat 4: 0-8 portal; 8-20 right to the inner-right rod; 20-32 left to the crank wheel (pitch +10); 32-44 up at the
        /// trusses (+35); 50-70 on a moving crosshead (inner-right bank); 80-95 forward at the exit.
        /// </summary>
        public static readonly Key[] Beat4 =
        {
            R(0f, 0f, 0f), Tg(3f, TargetId.Portal), Tg(8f, TargetId.Portal),
            Tg(13f, TargetId.RodIR), Tg(20f, TargetId.RodIR),
            TgP(25f, TargetId.WheelIL, 0f, 10f), TgP(30f, TargetId.WheelIL, 0f, 10f),
            R(37f, 0f, 35f), R(44f, 0f, 35f),
            R(48f, 0f, 0f),
            // the inner-right crosshead (u 46-70) is passed around beat second 60: follow it until it falls behind the
            // shoulder, then ease back to the heading over 8 s
            Tg(53f, TargetId.CrossheadIR), Tg(65f, TargetId.CrossheadIR),
            R(73f, 0f, 0f),
            Tg(80f, TargetId.HallExit), Tg(95f, TargetId.HallExit),
        };

        /// <summary>
        /// Beat 5 before the ride (d from W10 to the platform centre, heading -X): the shaft bursts into view. R1-35: the old
        /// up-right look (+20, +10) found a blank wall. The city cannot be in frame on the walk-on: its top row (y -60) lies 11-22
        /// degrees below the eye at 139-280 m, while the apron and deck edges mask everything below 4-10 degrees from the walk
        /// line (player_r03 t45_walk). The promise of the descent is the guide-rail lattice on the +Z flank (z 283 / 289),
        /// towering 40 m above the dock and plunging past the apron edge: over walk seconds 2.5-9.5 the look turns to it
        /// (relative yaw +52 = world 322: the lattice from its top down to where it drops past the dock edge, with the least
        /// deck), then back to the heading at the deck centre; the ride opens by looking up the same rails (Beat5Ride 0-10).
        /// R2-11: the rails now carry a cold marker lamp per 12 m segment (Beat05); from the walk line the nearest ones sit
        /// 2-3 m above the eye 14-20 m away (+7..+10 degrees), the next ones 15 m higher. Pitch +2 (was -4) centres that dotted
        /// line rising past the dock, with less deck in the lower frame.
        /// </summary>
        public const float WALKON_REL_YAW = 52f, WALKON_PITCH = 2f;

        public static readonly Key[] Beat5Walk =
        {
            R(0f, 0f, 3.8f), R(2.5f, 0f, 2f),
            R(6.5f, WALKON_REL_YAW, WALKON_PITCH), R(9.5f, WALKON_REL_YAW, WALKON_PITCH),
            R(13.33f, 0f, 5f),
        };

        /// <summary>KS5a framing (R1-25, the KeyShots pose at ride 60): yaw and pitch.</summary>
        public const float KS5A_YAW = 262f, KS5A_PITCH = 16f;
        /// <summary>
        /// Pitch of the 10-25 facade hold. At ride 10-25 the eye is at y -16..-77, level with the city's top row (y -60), so the
        /// KS5a pitch of +16 (made for ride 60, 187 m inside the city) frames 70 % bare wall above the city (player_r03 ride
        /// frame 220); +6 keeps the city band in the lower middle, rising over the rail as the lift sinks into it. The 25-45
        /// pan then rises to the KS5a pitch.
        /// </summary>
        public const float RIDE_FACADE_PITCH = 6f;
        /// <summary>
        /// The look down into the shaft from the -Z rail (KS5c; 14.5.5 "-55 for 6 s"). Lead decision D1 (R2-03): yaw 176, pitch
        /// -38 at the rail stand (world z 260.4). The walk clamp keeps the eye 1.4 m behind the -Z deck edge, which therefore
        /// sits atan(1.7 / 1.4) = 50.5 degrees below the horizon: at -55 the lower half of the frame was deck; at -38 the frame
        /// spans -72..-4 and the deck takes its lower ~20-30 %, the gantry below sits off-centre (yaw 176).
        /// </summary>
        public const float RIDE_DOWN_PITCH = -38f, RIDE_DOWN_YAW = 176f;

        /// <summary>
        /// Beat 5 on ride seconds (dwell -2..0): 0-10 up the guide rail (yaw 330, pitch +40); 10-25 to the left wall at the KS5a
        /// yaw (262 instead of the elevation view 240: the facade rakes away, the corner pylon sits at the left frame edge;
        /// R1-25) and pitch +6 (see <see cref="RIDE_FACADE_PITCH"/>); 25-45 slow pan along the facade to yaw 280, rising to +15
        /// as the city closes around the lift, so the window rows converge upward as in KS5a; the pan passes 270, where the two
        /// -X pylons (bearings 225 / 315) sit at the frame edges, and stops while the 315 pylon is still near the right edge
        /// (x 0.8), so the 45-65 sweep carries it across the frame quickly instead of holding it right of centre (player_r04
        /// ride 38-50); 45-65 right past
        /// C1 (one continuous clockwise sweep, unwrapped yaw); 65-75 to the open front while the walker goes out to the -Z rail
        /// (WalkPlanner.DeckStand, 64-75); 75-90 hold the fan from the rail (KS5b, the full blade ring above the deck edge);
        /// 90-110 down into the shaft over the rail (D1: yaw 176, pitch -38, held 6 s, KS5c at 100 s); 110-120 level while the
        /// walker steps back to the centre; exit at 180.
        /// </summary>
        public static readonly Key[] Beat5Ride =
        {
            A(-2f, 270f, 5f), A(-1f, 270f, 5f),
            A(3f, 318f, 30f), A(6.5f, 330f, 40f), A(10f, 330f, 40f),
            A(18f, KS5A_YAW, RIDE_FACADE_PITCH), A(25f, KS5A_YAW, RIDE_FACADE_PITCH),
            A(45f, 280f, KS5A_PITCH - 1f, true), A(65f, 480f, 2f, true),
            A(72f, 530f, -2f),
            Tg(76f, TargetId.Fan), Tg(90f, TargetId.Fan),
            A(95f, 360f + RIDE_DOWN_YAW, RIDE_DOWN_PITCH), A(101f, 360f + RIDE_DOWN_YAW, RIDE_DOWN_PITCH),
            A(108f, 538f, -8f), A(112f, 540f, 0f),
            A(140f, 540f, 0f),
        };

        /// <summary>
        /// Beat 6: 0-10 level; 10-22 tilt to the fan; 22-60 hold with a drift of +/-8; 60-95 on the portal frames, broken by
        /// one glance down-right onto the deck at 66-70 (z about 160, where a blade-tip shadow wipes across the gantry every
        /// 3.33 s for 1.3 s; 4 s hold) and an asymmetric drift of 13-14 degrees toward the -X city wall at 76-88 (R1-36);
        /// 95-120 tilt up as the fan overhead grows; 120-133 forward to the mouth. The tunnel stays strictly symmetric.
        /// </summary>
        public static readonly Key[] Beat6 =
        {
            R(0f, 0f, 0f), R(10f, 0f, 0f),
            Tg(18f, TargetId.Fan, 0f, -6f), Tg(22f, TargetId.Fan, 0f, -6f),
            Tg(31.5f, TargetId.Fan, -8f, -6f), Tg(50.5f, TargetId.Fan, 8f, -6f), Tg(60f, TargetId.Fan, 0f, -6f),
            R(66f, 8f, -24f), R(70f, 8f, -24f),
            R(76f, 14f, 4f), R(88f, 13f, 5f), R(95f, 0f, 5f),
            Tg(115f, TargetId.Fan, 0f, -12f), Tg(120f, TargetId.Fan, 0f, -12f),
            R(127f, 0f, 0f), R(133f, 0f, 0f),
        };

        // ---------------------------------------------------------------- beat 7 pier walk (R2-18 / R2-05)
        /// <summary>
        /// Vertigo callback on the pier (R2-18 (2), a rhyme of the KS2b look down): around z -45 the look turns 70 degrees to
        /// the left (+X) and tilts to -40, holds 3.5 s and returns to the door. Seen 70 degrees off the walk axis the pier's
        /// left edge (1.5 m from the centreline) sits atan(1.7 / (1.5 / sin 70)) = 46.8 degrees below the horizon, so with
        /// FOV 68 the slab and its edge line run diagonally through the lower ~40 % of the frame and the honeycomb 600 m below
        /// fills the rest. Turns take 4-4.5 s (peak target rate about 25 degrees per second before the look damping).
        /// </summary>
        public const float PIER_LOOK_T0 = 78f, PIER_LOOK_T1 = 82.5f, PIER_LOOK_T2 = 86f, PIER_LOOK_T3 = 90.5f;
        public const float PIER_LOOK_REL_YAW = -70f, PIER_LOOK_PITCH = -40f;
        /// <summary>
        /// D2 (R2-05): pitch of the stop facing the door (WalkPlanner.END_STOP_Z = -70.8, 3.2 m from the door plane), "about
        /// -4": with FOV 68 the lower frame edge meets the pier 2.1 m ahead, so 0.5 m of pier in front of the door frame (where
        /// the amber sliver lands) is in frame, and the frame head (2.6 m, +19 degrees) sits well inside the top edge (+29).
        /// </summary>
        public const float END_PITCH = -5f;

        /// <summary>
        /// Beat 7 on the cinematic clock (<see cref="Beat7Clock"/>): 0-44 level on the mouth; 44-48 to the balcony edge and
        /// tilt to -12; 48-62 hold H7 (pan left 25 to yaw 155, then right 25 past the axis to yaw 205, toward the sun azimuth
        /// where the exterior lobe lifts the upper right, and back onto the axis); 62-68 tilt to -5 and lock on the door; the
        /// pier walk with one vertigo look down the left edge at z about -45 (80-90); then the door, ending at pitch -4 at
        /// the stop (D2).
        /// </summary>
        public static readonly Key[] Beat7 =
        {
            A(0f, 180f, 0f), A(44f, 180f, 0f),
            // hold H7: pan left 25, dwell, pan right through the axis to +25, back onto the door axis. Keys at H7_T0 and H7_T1
            // are identical so the non-cinematic walker (no hold, clock jumps 48 -> 62) sees a continuous target.
            A(H7_T0, 180f, -12f), A(H7_T0 + 1f, 180f, -12f),
            A(H7_T0 + 5f, 180f - H7_PAN, -11f), A(H7_T0 + 6f, 180f - H7_PAN, -11f),
            A(H7_T0 + 11f, 180f + H7_PAN, -13f), A(H7_T0 + 11.5f, 180f + H7_PAN, -13f), A(H7_T1, 180f, -12f),
            A(68f, 180f, -5f),
            TgP(72f, TargetId.Door, 0f, -3f), TgP(PIER_LOOK_T0, TargetId.Door, 0f, -2f),
            R(PIER_LOOK_T1, PIER_LOOK_REL_YAW, PIER_LOOK_PITCH), R(PIER_LOOK_T2, PIER_LOOK_REL_YAW + 2f, PIER_LOOK_PITCH - 1.5f),
            TgP(PIER_LOOK_T3, TargetId.Door, 0f, -3f),
            TgP(96f, TargetId.Door, 0f, END_PITCH), TgP(101.5f, TargetId.Door, 0f, END_PITCH),
        };

        /// <summary>The table for a beat (beat 5 returns the walk table; the ride table is selected by the walker).</summary>
        public static Key[] ForBeat(int beat)
        {
            switch (beat)
            {
                case 1: return Beat1;
                case 2: return Beat2;
                case 3: return Beat3;
                case 4: return Beat4;
                case 5: return Beat5Walk;
                case 6: return Beat6;
                default: return Beat7;
            }
        }

        /// <summary>Beat time at 1.5 m/s from d (beats 1-6).</summary>
        public static float BeatTime(int beat, float d)
        {
            return (d - AbyssConst.BEAT_START_D[Mathf.Clamp(beat, 1, 7) - 1]) / AbyssConst.V_WALK;
        }

        /// <summary>d of the hold H7 (z = -12 on the balcony).</summary>
        public static float H7D => PathGuide.Beat7D(H7_Z);

        /// <summary>
        /// Beat-7 cinematic clock: (d - 668.54) / 1.5 up to the balcony edge (48 s), 48 + holdElapsed during the hold,
        /// 62 + (d - H7D) / 1.5 after it. Without a hold the clock jumps 48 -> 62 where both keys have the same pose.
        /// </summary>
        public static float Beat7Clock(float d, bool holding, float holdElapsed)
        {
            if (holding) return H7_T0 + Mathf.Clamp(holdElapsed, 0f, H7_DURATION);
            float h = H7D;
            if (d <= h) return Mathf.Max(0f, (d - AbyssConst.BEAT_START_D[6]) / AbyssConst.V_WALK);
            return H7_T1 + (d - h) / AbyssConst.V_WALK;
        }

        /// <summary>FOV offset of the hold H7 push on the beat-7 clock (68 -> 66 during the hold, back over 62-68). Cinematic only.</summary>
        public static float Beat7FovOffset(float tau7)
        {
            if (tau7 <= H7_T0) return 0f;
            if (tau7 <= H7_T1) return H7_FOV_PUSH * SmoothStep01((tau7 - H7_T0) / H7_DURATION);
            if (tau7 <= H7_T1 + H7_FOV_RETURN) return H7_FOV_PUSH * (1f - SmoothStep01((tau7 - H7_T1) / H7_FOV_RETURN));
            return 0f;
        }

        // ---------------------------------------------------------------- evaluation

        /// <summary>Resolve a table at time t into a world yaw / pitch (degrees, zero roll).</summary>
        public static void Evaluate(Key[] keys, float t, in Context ctx, out float yaw, out float pitch)
        {
            int n = keys.Length;
            if (n == 0) { yaw = ctx.HeadingYaw; pitch = 0f; return; }
            if (n == 1 || t <= keys[0].T) { Resolve(keys[0], ctx, out yaw, out pitch); return; }
            if (t >= keys[n - 1].T) { Resolve(keys[n - 1], ctx, out yaw, out pitch); return; }
            int i = 0;
            while (i < n - 2 && t >= keys[i + 1].T) i++;
            Key a = keys[i], b = keys[i + 1];
            float span = b.T - a.T;
            float u = span > 1e-6f ? Mathf.Clamp01((t - a.T) / span) : 1f;
            float s = b.Linear ? u : SmoothStep01(u);
            Resolve(a, ctx, out float ya, out float pa);
            Resolve(b, ctx, out float yb, out float pb);
            if (a.YawMode == YawMode.Absolute && b.YawMode == YawMode.Absolute) yaw = ya + (yb - ya) * s;  // authored sweep direction
            else yaw = ya + Mathf.DeltaAngle(ya, yb) * s;
            pitch = pa + (pb - pa) * s;
        }

        /// <summary>One key as a world yaw / pitch.</summary>
        public static void Resolve(in Key k, in Context ctx, out float yaw, out float pitch)
        {
            float ty = 0f, tp = 0f;
            if (k.YawMode == YawMode.Target || k.PitchMode == PitchMode.Target)
            {
                // the falling bolt is looked at where it is at the key's own fall second (beat-2 table time - release time)
                Vector3 target = k.Target == TargetId.Bolt ? BoltPosition(k.T - BOLT_RELEASE_T) : TargetPosition(k.Target, ctx.SceneTime);
                Vector3 dir = target - ctx.Eye;
                float h = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                ty = h > 1e-4f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : ctx.HeadingYaw;
                tp = Mathf.Atan2(dir.y, Mathf.Max(h, 1e-4f)) * Mathf.Rad2Deg;
            }
            switch (k.YawMode)
            {
                case YawMode.Relative: yaw = ctx.HeadingYaw + k.Yaw; break;
                case YawMode.Absolute: yaw = k.Yaw; break;
                default: yaw = ty + k.Yaw; break;
            }
            pitch = k.PitchMode == PitchMode.Target ? tp + k.Pitch : k.Pitch;
        }

        /// <summary>World position of a named look target at the given scene time.</summary>
        public static Vector3 TargetPosition(TargetId id, double sceneTime)
        {
            var W = AbyssConst.W;
            float hallZ = W[8].z;                                   // 269: the walk line through the hall (v = 0)
            switch (id)
            {
                case TargetId.Doorway:
                    // a point far beyond the exit doorway on its axis: same direction as the doorway, but the yaw stays stable
                    // while the walker passes through it (the pitch of the Doorway keys is absolute)
                    return new Vector3(W[3].x, W[3].y + AbyssConst.EXIT_DOOR_H * 0.5f, AbyssConst.EXIT_DOOR_Z + 100f);
                case TargetId.Hatch:
                    return AbyssConst.HATCH_POS + new Vector3(0f, AbyssConst.HATCH_H * 0.5f, 0f);
                case TargetId.Portal:
                    return new Vector3(AbyssConst.HALL_X0, AbyssConst.CATWALK_Y + 4f, hallZ);
                case TargetId.RodIR:
                    // middle of the visible rod of the inner-right bank (between the rear head at u 42 and the crosshead)
                    return new Vector3(AbyssConst.HALL_X0 - 52f, 2f, hallZ + AbyssConst.BANK_V[1]);
                case TargetId.WheelIL:
                    return new Vector3(AbyssConst.HALL_X0 - AbyssConst.CRANK_U_A, 1f, hallZ + AbyssConst.BANK_V[0]);
                case TargetId.CrossheadIR:
                {
                    // type B bank: crosshead at u = CRANK_U_B - x(theta), theta_IR = theta_IL + 45 degrees (14.5.4)
                    double th = (AbyssConst.CrankThetaIL(sceneTime) + AbyssConst.BANK_PHASE_DEG[1]) * System.Math.PI / 180.0;
                    double r = AbyssConst.CRANK_R, l = AbyssConst.CRANK_L, s = System.Math.Sin(th);
                    double x = r * System.Math.Cos(th) + System.Math.Sqrt(l * l - r * r * s * s);
                    float u = (float)(AbyssConst.CRANK_U_B - x);
                    return new Vector3(AbyssConst.HALL_X0 - u, 2f, hallZ + AbyssConst.BANK_V[1]);
                }
                case TargetId.HallExit:
                    // through the exit opening into the shaft (80 m beyond it), so the look stays level as the walker reaches it
                    return new Vector3(AbyssConst.HALL_X1 - 80f, AbyssConst.CATWALK_Y + 7f, hallZ);
                case TargetId.Fan:
                    return AbyssConst.FAN_CENTRE;
                case TargetId.Mouth:
                    return new Vector3(W[16].x, W[16].y + AbyssConst.EYE, W[16].z);
                case TargetId.Door:
                    return new Vector3(W[18].x, W[18].y + AbyssConst.GOAL_DOOR_H * 0.5f, W[18].z);
                case TargetId.Bolt:
                    return BoltRelease;
                default:
                    return Vector3.zero;
            }
        }

        public static float SmoothStep01(float x)
        {
            x = x < 0f ? 0f : (x > 1f ? 1f : x);
            return x * x * (3f - 2f * x);
        }
    }
}
