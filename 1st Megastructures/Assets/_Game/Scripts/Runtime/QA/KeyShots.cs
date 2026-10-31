using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Megastructure
{
    /// <summary>One key shot of the 14.10 table: pose (eye, yaw, pitch, vertical FOV), 4K flag, ride seconds for the lift shots.</summary>
    public struct KeyShot
    {
        public string Tag;
        public int Beat;
        public Vector3 Eye;
        public float Yaw, Pitch, Fov;
        public bool FourK;
        /// <summary>Ride seconds for the lift shots (KS5a/b/c), -1 otherwise.</summary>
        public float RideSeconds;

        public KeyShot(string tag, int beat, Vector3 eye, float yaw, float pitch, float fov, bool fourK, float rideSeconds = -1f)
        {
            Tag = tag; Beat = beat; Eye = eye; Yaw = yaw; Pitch = pitch; Fov = fov; FourK = fourK; RideSeconds = rideSeconds;
        }

        /// <summary>Unity rotation with zero roll (pitch -90 is exact: AbyssConst.YawPitch builds it as a quaternion).</summary>
        public Quaternion Rotation => AbyssConst.YawPitch(Yaw, Pitch);

        public bool OnLift => RideSeconds >= 0f;

        /// <summary>Beat 6 and 7 shots are after the ride (lower level).</summary>
        public bool AfterRide => Beat >= 6;

        /// <summary>Feet below the eye (eye height 1.7 m).</summary>
        public Vector3 Feet => Eye - new Vector3(0f, AbyssConst.EYE, 0f);

        /// <summary>Path distance of the pose (D_LIFT on the lift).</summary>
        public float PathD => OnLift ? AbyssConst.D_LIFT : PathGuide.Project(Feet, -1f);

        /// <summary>Scene clock of the shot: nominal time at the pose's d, or ride start + ride seconds on the lift.</summary>
        public double Clock => OnLift ? AbyssConst.T_RIDE_START + RideSeconds : AbyssConst.NominalTimeAtD(PathD, AfterRide);

        /// <summary>Eye relative to the deck centre (deck-top height) for the lift shots.</summary>
        public Vector3 LiftOffset => OnLift ? Eye - LiftController.PositionAtS(AbyssConst.LiftS(RideSeconds)) : Vector3.zero;
    }

    /// <summary>One image to capture (key shot variant, beat still, free-look still, or a special test shot).</summary>
    public sealed class ShotSpec
    {
        public string Tag, BaseTag, Kind, File, Variant, Aspect, Zone;
        public int Beat;
        public int Width = 1920, Height = 1080;
        public Vector3 Eye;
        public float Yaw, Pitch, Fov;
        public Quaternion Rot = Quaternion.identity;
        public double Clock;
        public float RideSeconds = -1f;
        public bool AfterRide;
        /// <summary>Feet position for PlayerController.Teleport (ignored for the lift shots, which board at the top dock).</summary>
        public Vector3 Feet;
        /// <summary>Index into KeyShots.All, or -1.</summary>
        public int KeyIndex = -1;
        /// <summary>Special test: "seam:&lt;plane&gt;", "tier:&lt;name&gt;", "parallax:&lt;base&gt;:&lt;move|yaw|base&gt;" or null.</summary>
        public string Special;
        public float TierOverride = -1f;
        /// <summary>Tag of the base shot of a parallax or tier pair.</summary>
        public string PairOf;
        /// <summary>Render this shot with post-processing disabled (the -nopost horizon-row test as a per-shot cue, R1-40).</summary>
        public bool NoPost;

        public float AspectRatio => (float)Width / Height;
    }

    /// <summary>
    /// The 14.10 key-shot table and the Part 11.2 shot lists (per-beat start / middle / end stills along PathGuide.WalkPoint and
    /// three deterministic free-look stills from the middle), with the aspect variants of 11.2 / Part 10 item 21, the 14.10
    /// regions for the Python image metrics, and the tag selection of <c>-shots</c>. Pure data: usable from tests.
    /// </summary>
    public static class KeyShots
    {
        // Pose arbitrations of fix round R1 (Part 1.6.2; the lead logs them in DEVELOPMENT.md):
        // KS4  (-30, -5.3, 269), yaw 274, pitch +8 (spec (-60, -5.3, 269), 270, +4; critique R1-20). At the spec pose (u 28) the
        //      left crank wheel (u 5-35) is 8 m behind the eye and cylinder B (u 14-42) runs past it, so neither can be framed.
        //      From u -2 (the apron, 2 m before the portal plane; the 20 x 24 opening is wider than the frustum there) the wheel
        //      centre sits upper left (in-frame part centred at about x 0.22, y 0.26), cylinder B on the right (x 0.79, y 0.28),
        //      cylinder A ahead (x 0.35), the outer banks, trusses and the exit opening in frame at 16:9, 16:10 and 21:9
        //      (searched u -2..4, yaw 264-276, pitch 4-12 with the lint; tools: the in-frame lint rule for subjects larger than
        //      the frame, CompositionLint).
        // KS5a yaw 262, pitch +16 (spec 240, +3; critique R1-25): yaw 240 is the inward normal of face 210, a flat elevation
        //      split by the (-7, -7) pylon 15 degrees left of centre; at 262 / +16 the pylon sits at the left edge (partly
        //      framed), the facade rakes away into the fog with converging window rows and the deck drops out of frame.
        // KS5b eye (-85.95, -317.3, 260.5) (spec z 278; critique R1-04): at z 278 the eye is 19 m from the -Z deck edge, which
        //      masks everything below about -5 degrees, i.e. the lower half of the blade ring. 1.5 m from the -Z rail the deck
        //      masks only below -40 to -48 degrees and the 66.3 degree ring fits the 68 degree frame.
        // KS5c eye z 260.4, yaw 176, pitch -38 (spec 261.0, 180, -55; critique R1-05 / R2-03, lead decision D1 of fix round R2):
        //      the walk clamp keeps the eye 1.4 m behind the -Z deck edge, which sits atan(1.7 / 1.4) = 50.5 degrees below the
        //      horizon, so at -55 the lower half of the frame was always deck (f03 'gantry below' 23 % visible). At -38 the frame
        //      spans about -72 to -4 degrees, the edge falls to y 0.64-0.70 and the drop, the gantry and the fan housing fill the
        //      upper two thirds; yaw 176 sets the gantry axis off-centre. The ride gaze 90-110 s (GazeCues) uses the same pitch.
        public static readonly KeyShot[] All =
        {
            new KeyShot("KS1_gallery_start", 1, new Vector3(0f, 1.7f, 8f), 0f, 0f, 62f, false),
            new KeyShot("KS1b_doorway", 1, new Vector3(0f, -1.3f, 55f), 0f, 0f, 62f, false),
            new KeyShot("KS2a_bridge_ahead", 2, new Vector3(0f, -1.3f, 120f), 0f, -10f, 66f, true),
            new KeyShot("KS2b_bridge_down", 2, new Vector3(0f, -1.3f, 141f), 0f, -90f, 66f, true),
            new KeyShot("KS3_column_up", 3, new Vector3(25f, -3.3f, 244f), 328f, 16f, 66f, true),
            new KeyShot("KS4_hall_pistons", 4, new Vector3(KS4_X, -5.3f, 269f), KS4_YAW, KS4_PITCH, 62f, false),
            new KeyShot("KS5a_lift_city", 5, new Vector3(-103.5f, -245.3f, 269f), KS5A_YAW, KS5A_PITCH, 66f, true, 60f),
            new KeyShot("KS5b_lift_fan", 5, new Vector3(-85.95f, -317.3f, KS5B_Z), 169.8f, 0f, 68f, true, 75f),
            new KeyShot("KS5c_lift_down", 5, new Vector3(-56.7f, -437.3f, KS5C_Z), KS5C_YAW, KS5C_PITCH, 68f, false, 100f),
            new KeyShot("KS6_gantry_fan", 6, new Vector3(-45f, -485.3f, 255f), 180f, 30f, 68f, true),
            new KeyShot("KS7a_tunnel_mouth", 7, new Vector3(-45f, -485.3f, 14f), 180f, 0f, 62f, false),
            new KeyShot("KS7b_honeycomb", 7, new Vector3(-45f, -485.3f, -12f), 180f, -12f, 68f, true),
            new KeyShot("KS7c_goal_door", 7, new Vector3(-45f, -485.3f, -44f), 180f, -2f, 64f, true),
            new KeyShot("KS7d_look_back", 7, new Vector3(-45f, -485.3f, -70f), 0f, 24f, 68f, false),
        };

        /// <summary>KS4 arbitration (R1-20): eye x (hall u = -32 - x = -2), yaw, pitch. Spec: x -60 (u 28), 270, +4.</summary>
        public const float KS4_X = -30f, KS4_YAW = 274f, KS4_PITCH = 8f;
        /// <summary>The 14.10 KS4 pose (u 28): kept for the piston sequence, whose camera impulse needs the eye inside the hall.</summary>
        public static readonly Vector3 KS4_SPEC_EYE = new Vector3(-60f, -5.3f, 269f);
        /// <summary>KS5a arbitration (R1-25): yaw, pitch. Spec: 240, +3.</summary>
        public const float KS5A_YAW = 262f, KS5A_PITCH = 16f;
        /// <summary>KS5b arbitration (R1-04): eye world z, 1.5 m from the -Z rail. Spec: 278.</summary>
        public const float KS5B_Z = 260.5f;
        /// <summary>KS5c arbitration (R1-05, lead decision D1 / R2-03): eye world z, yaw, pitch. Spec: 261.0, 180, -55.</summary>
        public const float KS5C_Z = 260.4f, KS5C_YAW = 176f, KS5C_PITCH = -38f;

        /// <summary>Key shot used for the per-beat static and thin-line sequences (beats 1..7).</summary>
        public static readonly int[] BeatReference = { 0, 2, 4, 5, 6, 9, 11 };

        public const int W1080 = 1920, H1080 = 1080;
        public const int W4K = 3840, H4K = 2160;
        public const int W1610 = 1680, H1610 = 1050;
        public const int W219 = 2560, H219 = 1080;

        /// <summary>World bounds every QA pose must lie in (near field of 14.4 plus the shaft height).</summary>
        public static readonly Bounds WorldBounds = new Bounds(new Vector3(0f, -1000f, 350f), new Vector3(1000f, 4400f, 1000f));

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static int IndexOf(string tag)
        {
            for (int i = 0; i < All.Length; i++) if (string.Equals(All[i].Tag, tag, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>"beat02_bridge" for beat 2.</summary>
        public static string BeatPrefix(int beat)
        {
            int b = Mathf.Clamp(beat, 1, 7);
            return "beat" + b.ToString("00", Inv) + "_" + AbyssConst.BEAT_NAMES[b - 1].ToLowerInvariant();
        }

        /// <summary>Tag suffix and aspect label of a capture size.</summary>
        public static void VariantOf(int w, int h, out string suffix, out string aspect)
        {
            if (w == W1080 && h == H1080) { suffix = ""; aspect = "16:9"; return; }
            if (w == W4K && h == H4K) { suffix = "_4k"; aspect = "16:9"; return; }
            if (w == W1610 && h == H1610) { suffix = "_16x10"; aspect = "16:10"; return; }
            if (w == W219 && h == H219) { suffix = "_21x9"; aspect = "21:9"; return; }
            suffix = "_" + w.ToString(Inv) + "x" + h.ToString(Inv);
            aspect = w.ToString(Inv) + ":" + h.ToString(Inv);
        }

        /// <summary>Beat-7 threshold zone of 14.10: tunnel while z &gt; 0 (before the mouth), outside after it.</summary>
        public static string ZoneFor(int beat, Vector3 eye) => beat == 7 ? (eye.z > 0f ? "tunnel" : "outside") : null;

        /// <summary>Per-beat FOV (FOV_PER_BEAT; beat 7 tunnel index 6, outside index 7).</summary>
        public static float FovFor(int beat, Vector3 eye)
        {
            int i = Mathf.Clamp(beat, 1, 7) - 1;
            if (beat == 7 && eye.z <= 0f) i = 7;
            return AbyssConst.FOV_PER_BEAT[i];
        }

        // ---------------------------------------------------------------- spec construction

        static ShotSpec FromKey(int k, int w, int h, string tagSuffix = "")
        {
            KeyShot ks = All[k];
            VariantOf(w, h, out string suffix, out string aspect);
            string tag = ks.Tag + tagSuffix + suffix;
            var s = new ShotSpec
            {
                Tag = tag, BaseTag = ks.Tag, Kind = "keyshot", Beat = ks.Beat, Width = w, Height = h,
                Variant = suffix.Length == 0 ? "1080p" : suffix.Substring(1), Aspect = aspect,
                Eye = ks.Eye, Yaw = ks.Yaw, Pitch = ks.Pitch, Fov = ks.Fov, Rot = ks.Rotation,
                Clock = ks.Clock, RideSeconds = ks.RideSeconds, AfterRide = ks.AfterRide, Feet = ks.Feet, KeyIndex = k,
                Zone = ZoneFor(ks.Beat, ks.Eye),
            };
            s.File = BeatPrefix(ks.Beat) + "_" + tag + ".png";
            return s;
        }

        /// <summary>The key-shot variants of 11.2: 1920x1080, 3840x2160 where marked, 1680x1050 and 2560x1080.</summary>
        public static void AddKeyVariants(List<ShotSpec> list, int k, bool haveRes, int resW, int resH)
        {
            if (haveRes) { list.Add(FromKey(k, resW, resH)); return; }
            list.Add(FromKey(k, W1080, H1080));
            if (All[k].FourK) list.Add(FromKey(k, W4K, H4K));
            list.Add(FromKey(k, W1610, H1610));
            list.Add(FromKey(k, W219, H219));
        }

        /// <summary>Start / middle / end d of a beat's stills (2 m inside the beat; beat 7 ends 3 m before the door).</summary>
        public static void BeatStillD(int beat, out float d0, out float dMid, out float d1)
        {
            float[] s = AbyssConst.BEAT_START_D;
            d0 = s[beat - 1] + 2f;
            d1 = beat < 7 ? s[beat] - 2f : AbyssConst.POLYLINE_LENGTH - 3f;
            dMid = 0.5f * (d0 + d1);
        }

        /// <summary>Ride seconds of the beat-5 middle still (on the deck, mid ride).</summary>
        public const float BEAT5_MID_RIDE = 60f;

        static ShotSpec Still(int beat, string name, float d, bool after, float yawOffset, float pitch, int w, int h, string kind)
        {
            Vector3 feet = PathGuide.WalkPoint(d, after);
            float heading = PathGuide.HeadingYaw(d, after);
            Vector3 eye = feet + new Vector3(0f, AbyssConst.EYE, 0f);
            float ride = -1f;
            bool beat5Ride = beat == 5 && (name == "middle" || name.StartsWith("freelook", StringComparison.Ordinal));
            if (beat5Ride)
            {
                // the middle of beat 5 is the ride: on the deck centre at ride second 60, facing the path heading (-X)
                ride = BEAT5_MID_RIDE;
                eye = LiftController.PositionAtS(AbyssConst.LiftS(ride)) + new Vector3(0f, AbyssConst.EYE, 0f);
                feet = eye - new Vector3(0f, AbyssConst.EYE, 0f);
                heading = 270f;
                after = false;
            }
            float yaw = Mathf.Repeat(heading + yawOffset, 360f);
            VariantOf(w, h, out string suffix, out string aspect);
            string tag = "B" + beat.ToString(Inv) + "_" + name + suffix;
            double clock = ride >= 0f ? AbyssConst.T_RIDE_START + ride : AbyssConst.NominalTimeAtD(d, after);
            var s = new ShotSpec
            {
                Tag = tag, BaseTag = "B" + beat.ToString(Inv) + "_" + name, Kind = kind, Beat = beat, Width = w, Height = h,
                Variant = suffix.Length == 0 ? "1080p" : suffix.Substring(1), Aspect = aspect,
                Eye = eye, Yaw = yaw, Pitch = pitch, Fov = FovFor(beat, eye), Rot = AbyssConst.YawPitch(yaw, pitch),
                Clock = clock, RideSeconds = ride, AfterRide = after, Feet = feet, Zone = ZoneFor(beat, eye),
            };
            s.File = BeatPrefix(beat) + "_" + tag + ".png";
            return s;
        }

        /// <summary>Part 11.2 per beat: start, middle, end along the walked curve, plus back / left-up / right-down from the middle.</summary>
        public static void AddBeatStills(List<ShotSpec> list, int beat, int w, int h)
        {
            BeatStillD(beat, out float d0, out float dm, out float d1);
            bool after0 = beat >= 6;
            bool after1 = beat >= 5;   // beat 5 ends on the bottom deck, after the ride
            list.Add(Still(beat, "start", d0, after0, 0f, 0f, w, h, "still"));
            list.Add(Still(beat, "middle", dm, beat >= 6, 0f, 0f, w, h, "still"));
            list.Add(Still(beat, "end", d1, after1, 0f, 0f, w, h, "still"));
            list.Add(Still(beat, "freelook_back", dm, beat >= 6, 180f, 0f, w, h, "freelook"));
            list.Add(Still(beat, "freelook_leftup", dm, beat >= 6, -90f, 30f, w, h, "freelook"));
            list.Add(Still(beat, "freelook_rightdown", dm, beat >= 6, 90f, -30f, w, h, "freelook"));
        }

        /// <summary>
        /// Part 10 item 30 (backdrop parallax): for the beat-7 outside views (KS7b levelled to pitch 0, KS7c) a base frame, the
        /// same pose moved 10 m to the right, and the same pose turned 2 degrees right. The harness measures the sky band.
        /// </summary>
        public static void AddParallax(List<ShotSpec> list, int w, int h)
        {
            int[] keys = { 11, 12 };
            for (int i = 0; i < keys.Length; i++)
            {
                KeyShot ks = All[keys[i]];
                float pitch = keys[i] == 11 ? 0f : ks.Pitch;
                string baseTag = ks.Tag + "_px_base";
                for (int m = 0; m < 3; m++)
                {
                    var s = FromKey(keys[i], w, h);
                    s.Kind = "parallax";
                    s.Pitch = pitch;
                    string mode = m == 0 ? "base" : (m == 1 ? "move10" : "yaw2");
                    if (m == 2) s.Yaw = Mathf.Repeat(ks.Yaw + 2f, 360f);
                    s.Rot = AbyssConst.YawPitch(s.Yaw, s.Pitch);
                    if (m == 1) s.Eye = ks.Eye + AbyssConst.YawPitch(ks.Yaw, 0f) * Vector3.right * 10f;
                    VariantOf(w, h, out string suffix, out _);
                    s.Tag = ks.Tag + "_px_" + mode + suffix;
                    s.BaseTag = ks.Tag + "_px_" + mode;
                    s.Special = "parallax:" + mode;
                    s.PairOf = m == 0 ? null : baseTag + suffix;
                    s.KeyIndex = -1;
                    s.File = "test_" + s.Tag + ".png";
                    list.Add(s);
                }
            }
        }

        /// <summary>Planes of the 14.10 (a) wrap-seam test.</summary>
        public static readonly string[] SeamPlanes = { "Y0", "Y-1332", "X0", "X1332", "Z0", "Z-1332" };

        /// <summary>
        /// 14.10 (a): a 40 x 40 m concrete wall straight across each wrap plane, seen from 12 m. Y planes: wall in XY at
        /// x = 300, z = -1000, camera looking +Z, plane = the centre row. X planes: wall in XY at y = 1500, z = -1000 (plane =
        /// centre column). Z planes: wall in YZ at x = -300, y = 1500, camera looking +X (plane = centre column).
        /// </summary>
        public static void SeamPose(string plane, out Vector3 wallCentre, out Vector3 wallSize, out Vector3 eye, out float yaw, out bool rowPlane)
        {
            const float L = 40f, T = 0.5f, Dist = 12f;
            switch (plane)
            {
                case "Y0": wallCentre = new Vector3(300f, 0f, -1000f); rowPlane = true; break;
                case "Y-1332": wallCentre = new Vector3(300f, -AbyssConst.NOISE_WRAP, -1000f); rowPlane = true; break;
                case "X0": wallCentre = new Vector3(0f, 1500f, -1000f); rowPlane = false; break;
                case "X1332": wallCentre = new Vector3(AbyssConst.NOISE_WRAP, 1500f, -1000f); rowPlane = false; break;
                case "Z0": wallCentre = new Vector3(-300f, 1500f, 0f); rowPlane = false; break;
                default: wallCentre = new Vector3(-300f, 1500f, -AbyssConst.NOISE_WRAP); rowPlane = false; break;
            }
            bool zPlane = plane.StartsWith("Z", StringComparison.Ordinal);
            if (zPlane)
            {
                wallSize = new Vector3(T, L, L);
                eye = wallCentre - new Vector3(Dist + T * 0.5f, 0f, 0f);
                yaw = 90f;
            }
            else
            {
                wallSize = new Vector3(L, L, T);
                eye = wallCentre - new Vector3(0f, 0f, Dist + T * 0.5f);
                yaw = 0f;
            }
        }

        public static void AddSeams(List<ShotSpec> list, int w, int h)
        {
            Vector3 feet = PathGuide.WalkPoint(120f);
            for (int i = 0; i < SeamPlanes.Length; i++)
            {
                SeamPose(SeamPlanes[i], out _, out _, out Vector3 eye, out float yaw, out _);
                VariantOf(w, h, out string suffix, out string aspect);
                var s = new ShotSpec
                {
                    Tag = "seam_" + SeamPlanes[i] + suffix, BaseTag = "seam_" + SeamPlanes[i], Kind = "seam", Beat = 0,
                    Width = w, Height = h, Variant = suffix.Length == 0 ? "1080p" : suffix.Substring(1), Aspect = aspect,
                    Eye = eye, Yaw = yaw, Pitch = 0f, Fov = 60f, Rot = AbyssConst.YawPitch(yaw, 0f),
                    Clock = AbyssConst.NominalTimeAtD(120f, false), Feet = feet, Special = "seam:" + SeamPlanes[i],
                };
                s.File = "test_" + s.Tag + ".png";
                list.Add(s);
            }
        }

        /// <summary>Pose of the 14.10 (b) facade crossover: 150 m from the 210-degree city face, looking along its normal, mid ride.</summary>
        public static readonly Vector3 FacadeTierEye = new Vector3(-138.1f, -245.3f, 249f);

        /// <summary>
        /// 14.10 (b) facade tier crossover (near tier only vs shader tier only at 150 m) and (c) honeycomb tier 1 vs tier 2 at
        /// 6 km (from the KS7b position, pitched down so the frame centre meets the lattice 6 km away). Needs QaHooks.TierOverride
        /// to be read by DetailTier and by beat 7.
        /// </summary>
        public static void AddTiers(List<ShotSpec> list, int w, int h)
        {
            VariantOf(w, h, out string suffix, out string aspect);
            for (int m = 0; m < 2; m++)
            {
                string mode = m == 0 ? "near" : "far";
                var s = new ShotSpec
                {
                    Tag = "tier_facade_" + mode + suffix, BaseTag = "tier_facade_" + mode, Kind = "tier", Beat = 5,
                    Width = w, Height = h, Variant = suffix.Length == 0 ? "1080p" : suffix.Substring(1), Aspect = aspect,
                    Eye = FacadeTierEye, Yaw = 240f, Pitch = 0f, Fov = 66f, Rot = AbyssConst.YawPitch(240f, 0f),
                    Clock = AbyssConst.T_RIDE_START + 60.0, RideSeconds = 60f, Special = "tier:facade", TierOverride = m == 0 ? 1f : 0f,
                    PairOf = m == 0 ? null : "tier_facade_near" + suffix,
                };
                s.File = "test_" + s.Tag + ".png";
                list.Add(s);
            }
            KeyShot k7 = All[11];
            float pitch = -Mathf.Atan2(k7.Eye.y - AbyssConst.TERRACE_Y, 6000f) * Mathf.Rad2Deg;
            for (int m = 0; m < 2; m++)
            {
                string mode = m == 0 ? "tier1" : "tier2";
                var s = FromKey(11, w, h);
                s.Kind = "tier";
                s.Pitch = pitch;
                s.Rot = AbyssConst.YawPitch(s.Yaw, pitch);
                s.Tag = "tier_honeycomb_" + mode + suffix;
                s.BaseTag = "tier_honeycomb_" + mode;
                s.Special = "tier:honeycomb";
                s.TierOverride = m == 0 ? 1f : 0f;
                s.PairOf = m == 0 ? null : "tier_honeycomb_tier1" + suffix;
                s.KeyIndex = -1;
                s.File = "test_" + s.Tag + ".png";
                list.Add(s);
            }
        }

        // ---------------------------------------------------------------- R1-40: stills of specified visible cues

        /// <summary>Ride second of the freight-light pair (14.5.5: freight lights climb face 210, the left wall).</summary>
        public const float FreightRide = 30f;
        /// <summary>Beat-6 look-back (14.5.6: the 12 lighthouse patches on the far wall around (-45, -548, 760)): eye on the gantry.</summary>
        public static readonly Vector3 Beat6LookBackEye = new Vector3(-45f, -485.3f, 160f);
        public static readonly Vector3 FarWallPatchCentre = new Vector3(-45f, -548f, AbyssConst.SHAFT_FAR_Z);

        static ShotSpec Cue(string tag, int beat, Vector3 eye, float yaw, float pitch, float fov, double clock, int w, int h, bool after)
        {
            VariantOf(w, h, out string suffix, out string aspect);
            var s = new ShotSpec
            {
                Tag = tag + suffix, BaseTag = tag, Kind = "cue", Beat = beat, Width = w, Height = h,
                Variant = suffix.Length == 0 ? "1080p" : suffix.Substring(1), Aspect = aspect,
                Eye = eye, Yaw = yaw, Pitch = pitch, Fov = fov, Rot = AbyssConst.YawPitch(yaw, pitch), Clock = clock,
                AfterRide = after, Feet = eye - new Vector3(0f, AbyssConst.EYE, 0f), Zone = ZoneFor(beat, eye),
            };
            s.File = BeatPrefix(beat) + "_" + s.Tag + ".png";
            return s;
        }

        /// <summary>
        /// Stills of cues that a single key frame cannot show (critique R1-40): the climbing freight lights (a pair 1 s apart
        /// at ride second 30 looking at face 210, with a difference analysis), the far-wall lighthouse patches seen looking
        /// back from the gantry at four fan phases (a quarter of the 3.33 s pattern period apart) and KS7b with
        /// post-processing off (the 14.10 horizon row equals OUT_HORIZ within 2 levels). The falling bolt, the honeycomb
        /// ignition and the ending are sequences (seq:cues); the title card is captured by -firstlast.
        /// </summary>
        public static void AddCues(List<ShotSpec> list, int w, int h)
        {
            Vector3 deck = LiftController.PositionAtS(AbyssConst.LiftS(FreightRide)) + new Vector3(0f, AbyssConst.EYE, 0f);
            for (int i = 0; i < 2; i++)
            {
                var s = Cue(i == 0 ? "cue_freight_t30" : "cue_freight_t31", 5, deck, 240f, 3f, 66f, AbyssConst.T_RIDE_START + FreightRide + i, w, h, false);
                s.RideSeconds = FreightRide;
                s.Feet = deck - new Vector3(0f, AbyssConst.EYE, 0f);
                s.Special = "cue:freight";
                s.PairOf = i == 0 ? null : "cue_freight_t30" + Suffix(w, h);
                list.Add(s);
            }
            Vector3 eye = Beat6LookBackEye;
            Vector3 to = FarWallPatchCentre - eye;
            float pitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            float dLook = PathGuide.Project(eye - new Vector3(0f, AbyssConst.EYE, 0f), -1f);
            double t0 = AbyssConst.NominalTimeAtD(dLook, true);
            for (int k = 0; k < 4; k++)
            {
                var s = Cue("cue_b6_lookback_p" + k.ToString(Inv), 6, eye, 0f, pitch, FovFor(6, eye), t0 + k * AbyssConst.FAN_BLADE_PASS * 0.25, w, h, true);
                s.Special = "cue:lookback";
                s.PairOf = k == 0 ? null : "cue_b6_lookback_p0" + Suffix(w, h);
                list.Add(s);
            }
            var np = FromKey(11, w, h, "_nopost");
            np.Kind = "cue";
            np.KeyIndex = -1;
            np.NoPost = true;
            np.BaseTag = All[11].Tag + "_nopost";
            np.File = BeatPrefix(7) + "_" + np.Tag + ".png";
            list.Add(np);
        }

        static string Suffix(int w, int h)
        {
            VariantOf(w, h, out string suffix, out _);
            return suffix;
        }

        // ---------------------------------------------------------------- selection (-shots)

        /// <summary>
        /// Build the shot list for a <c>-shots</c> selector: <c>all</c> (keys + beats + parallax + seams + tiers), <c>keys</c>,
        /// <c>look</c> (the 14 key shots at 1920x1080 only, no 4K / 16:10 / 21:9 variants: the fast look-dev loop),
        /// <c>beats</c>, <c>parallax</c>, <c>seams</c>, <c>tiers</c>, or comma-separated tokens. A token selects every shot whose tag
        /// or base tag equals it, whose base tag starts with "token_" (KS2a selects KS2a_bridge_ahead and its variants; B3 selects
        /// the beat-3 stills), or every shot of beat N for "beatN". With -res every selected shot is captured once at that size
        /// (<c>look</c> then gives the 14 key shots at the -res size).
        /// </summary>
        public static List<ShotSpec> Build(string selector, bool haveRes, int resW, int resH)
        {
            var everything = new List<ShotSpec>(160);
            int w = haveRes ? resW : W1080, h = haveRes ? resH : H1080;
            for (int k = 0; k < All.Length; k++) AddKeyVariants(everything, k, haveRes, resW, resH);
            for (int b = 1; b <= 7; b++) AddBeatStills(everything, b, w, h);
            AddParallax(everything, w, h);
            AddSeams(everything, w, h);
            AddTiers(everything, w, h);
            AddCues(everything, w, h);

            if (string.IsNullOrEmpty(selector)) selector = "all";
            string[] tokens = selector.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var outList = new List<ShotSpec>(everything.Count);
            for (int t = 0; t < tokens.Length; t++)
            {
                if (TryPose(tokens[t].Trim(), w, h, out ShotSpec custom)) outList.Add(custom);
            }
            for (int i = 0; i < everything.Count; i++)
            {
                ShotSpec s = everything[i];
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (Matches(s, tokens[t].Trim(), haveRes)) { outList.Add(s); break; }
                }
            }
            return outList;
        }

        /// <summary>Selector of the look-dev loop: the 14 key shots once each, at 1920x1080 (or at the -res size).</summary>
        public const string LookSelector = "look";

        /// <summary>
        /// <c>pose:NAME:x:y:z:yaw:pitch:fov</c>: one free camera (thumbnails, look-dev) at the -res size. The player stands at
        /// the walk point nearest to the camera, kept 17 m short of the goal door so the ending never triggers; the scene
        /// clock is the nominal time there.
        /// </summary>
        static bool TryPose(string tok, int w, int h, out ShotSpec s)
        {
            s = null;
            if (!tok.StartsWith("pose:", StringComparison.OrdinalIgnoreCase)) return false;
            string[] p = tok.Substring(5).Split(':');
            if (p.Length < 7) return false;
            var v = new float[6];
            for (int i = 0; i < 6; i++)
            {
                if (!float.TryParse(p[i + 1], NumberStyles.Float, Inv, out v[i])) return false;
            }
            var eye = new Vector3(v[0], v[1], v[2]);
            float d = Mathf.Min(PathGuide.Project(eye - new Vector3(0f, AbyssConst.EYE, 0f), -1f), AbyssConst.POLYLINE_LENGTH - 17f);
            bool after = d > AbyssConst.D_LIFT;
            int beat = 1;
            for (int b = 1; b < AbyssConst.BEAT_START_D.Length; b++)
            {
                if (d >= AbyssConst.BEAT_START_D[b]) beat = b + 1;
            }
            s = Cue("pose_" + p[0], beat, eye, v[3], v[4], v[5], AbyssConst.NominalTimeAtD(d, after), w, h, after);
            s.Kind = "custom";
            s.Feet = PathGuide.WalkPoint(d, after);
            return true;
        }

        static bool Matches(ShotSpec s, string tok, bool haveRes)
        {
            if (tok.Length == 0) return false;
            if (Eq(tok, "all")) return true;
            if (Eq(tok, "keys")) return s.Kind == "keyshot";
            if (Eq(tok, LookSelector)) return s.Kind == "keyshot" && (haveRes || (s.Width == W1080 && s.Height == H1080));
            if (Eq(tok, "beats")) return s.Kind == "still" || s.Kind == "freelook";
            if (Eq(tok, "stills")) return s.Kind == "still";
            if (Eq(tok, "freelook")) return s.Kind == "freelook";
            if (Eq(tok, "parallax")) return s.Kind == "parallax";
            if (Eq(tok, "seams")) return s.Kind == "seam";
            if (Eq(tok, "tiers")) return s.Kind == "tier";
            if (Eq(tok, "cues")) return s.Kind == "cue";
            // every shot with a C#-side analysis (tests_summary): parallax, seams, tiers and the cue pairs (R1-29)
            if (Eq(tok, "tests")) return s.Kind == "parallax" || s.Kind == "seam" || s.Kind == "tier" || s.Kind == "cue";
            if (Eq(tok, "4k")) return s.Variant == "4k";
            if (tok.Length == 5 && tok.StartsWith("beat", StringComparison.OrdinalIgnoreCase) && char.IsDigit(tok[4]))
                return s.Beat == tok[4] - '0' && (s.Kind == "keyshot" || s.Kind == "still" || s.Kind == "freelook");
            if (Eq(s.Tag, tok) || Eq(s.BaseTag, tok)) return true;
            return s.BaseTag.StartsWith(tok + "_", StringComparison.OrdinalIgnoreCase);
        }

        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- regions for the Python image metrics (14.10)

        /// <summary>
        /// Base tag of the only key shot that sees the gallery exit doorway. At KS1 (eye z 8, level) the sloped stair ceiling
        /// hides the exit section, the amber lamp and the doorway (verified geometry, Beat01 CollectSubjects), so the doorway
        /// glow region and the 14.5.1 doorway luma check (0.65-0.90, under 0.3 % clipped) belong to KS1b only.
        /// </summary>
        public const string DoorwayShot = "KS1b_doorway";

        public static bool IsDoorwayShot(ShotSpec s) => s.Kind == "keyshot" && string.Equals(s.BaseTag, DoorwayShot, StringComparison.Ordinal);

        /// <summary>The exit doorway opening (EXIT_DOOR_W x EXIT_DOOR_H on the floor W3.y) as a box centred on the plane z.</summary>
        static Bounds DoorwayOpening(float z, float depth) =>
            new Bounds(new Vector3(0f, AbyssConst.W[3].y + AbyssConst.EXIT_DOOR_H * 0.5f, z), new Vector3(AbyssConst.EXIT_DOOR_W, AbyssConst.EXIT_DOOR_H, depth));

        /// <summary>
        /// Default banding regions of the 14.10 table as interpreted by Tools/qa/abyss_qa.py (fractions [x0, y0, x1, y1],
        /// top-left origin), refined where the harness knows the subject: the KS1b doorway glow (and the "doorway" luma region
        /// of the clear aperture) and the KS7a mouth glow from the projected openings; every other beat-1 shot gets the plain
        /// centre region under the name "gallery_centre" (KS1 cannot see the doorway). The beat-7 outside key shots looking out
        /// (KS7b, KS7c) also get the horizon regions (<see cref="WriteHorizonRegions"/>).
        /// </summary>
        public static void WriteRegions(QaJson j, ShotSpec s, float near)
        {
            if (s.Beat < 1 || s.Beat > 7) return;
            j.Obj("regions");
            j.Obj("banding");
            string baseTag = s.BaseTag ?? "";
            bool doorway = IsDoorwayShot(s);
            switch (s.Beat)
            {
                case 1:
                    if (doorway && ProjectedGlow(s, near, DoorwayOpening(AbyssConst.EXIT_DOOR_Z, 0.2f), out float a0, out float b0, out float a1, out float b1))
                        j.PropRect("doorway_glow", a0, b0, a1, b1);
                    else j.PropRect(doorway ? "doorway_glow" : "gallery_centre", 0.30f, 0.10f, 0.70f, 0.90f);
                    break;
                case 2: j.PropRect("fog_top30", 0f, 0f, 1f, 0.30f); j.PropRect("void_bottom40", 0f, 0.60f, 1f, 1f); break;
                case 3: j.PropRect("column_fog_top25", 0f, 0f, 1f, 0.25f); break;
                case 4: j.PropRect("hall_haze", 0.25f, 0.20f, 0.75f, 0.60f); break;
                case 5: j.PropRect("shaft_fog_top", 0f, 0f, 1f, 0.35f); j.PropRect("shaft_fog_bottom", 0f, 0.65f, 1f, 1f); break;
                case 6: j.PropRect("beam_region", 0.20f, 0f, 0.80f, 0.60f); break;
                default:
                    // R1-38: KS7d looks back at the Face and the fan bore, no sky or haze gradient is in frame: no banding region
                    if (s.Kind == "keyshot" && baseTag == "KS7d_look_back") break;
                    if (s.Zone == "tunnel")
                    {
                        Vector3 mouth = AbyssConst.W[16];
                        var mb = new Bounds(new Vector3(mouth.x, mouth.y + AbyssConst.TUNNEL_H * 0.5f, mouth.z), new Vector3(AbyssConst.TUNNEL_W, AbyssConst.TUNNEL_H, 0.2f));
                        if (s.Kind == "keyshot" && ProjectedGlow(s, near, mb, out float c0, out float d0, out float c1, out float d1))
                            j.PropRect("mouth_glow", c0, d0, c1, d1);
                        else j.PropRect("mouth_glow", 0.35f, 0.30f, 0.65f, 0.70f);
                    }
                    else j.PropRect("sky_haze_top40", 0f, 0f, 1f, 0.40f);
                    break;
            }
            j.EndObject();

            // 14.5.1 acceptance: the clear doorway aperture of KS1b (luma 0.65-0.90, under 0.3 % clipped; abyss_qa.py)
            if (doorway && DoorwayAperture(s, near, out float dx0, out float dy0, out float dx1, out float dy1))
                j.PropRect("doorway", dx0, dy0, dx1, dy1);

            // R1-29 image acceptance regions of 14.5 (judged by Tools/qa/image_metrics.py)
            if (s.Kind == "keyshot") WriteAcceptanceRegions(j, s, near, baseTag);

            // horizon regions (beat 7 outside, looking out over the honeycomb)
            bool lookingOut = s.Beat == 7 && s.Zone == "outside" && (baseTag.StartsWith("KS7b", StringComparison.Ordinal) || baseTag.StartsWith("KS7c", StringComparison.Ordinal))
                              && (s.Kind == "keyshot" || s.NoPost);
            if (lookingOut)
            {
                float v = HorizonRow(s.Pitch, s.Fov);
                if (v > 0.06f && v < 0.94f) WriteHorizonRegions(j, s, v);
            }
            j.EndObject();
        }

        // ---------------------------------------------------------------- R1-29: image acceptance regions and QA subjects

        /// <summary>KS2b side strips of 14.5.2 (the two 30 % strips beyond the deck): mean &lt;= 0.06, no pixel above 0.12, no edge above 0.03.</summary>
        public const float SideStripWidth = 0.30f;

        /// <summary>
        /// The KS1 far glow (critique R1-16): the underside of the sloped ceiling over the stairs (z 40-44.48, ceiling y 2.5 down
        /// to -0.5) and the stair-head jambs, the only exit-section surfaces the KS1 eye can see. A QA subject of the KS1 lint
        /// (in frame, at least 30 % unoccluded) and the region of the far-glow luma check.
        /// </summary>
        public static readonly Bounds Ks1FarGlow = new Bounds(
            new Vector3(0f, 0.5f * (AbyssConst.GALLERY_H + AbyssConst.GALLERY_H - AbyssConst.STAIR_DROP), 0.5f * (AbyssConst.STAIR_Z0 + AbyssConst.STAIR_Z1)),
            new Vector3(AbyssConst.GALLERY_W, AbyssConst.STAIR_DROP, AbyssConst.STAIR_Z1 - AbyssConst.STAIR_Z0));

        /// <summary>QA-owned lint subjects added to the beats' subjects (CompositionLint.Run): the KS1 far glow.</summary>
        public static void CollectQaSubjects(string baseTag, List<KeyShotSubject> list)
        {
            if (baseTag == "KS1_gallery_start") list.Add(new KeyShotSubject("far glow (stair-head ceiling) [qa]", Ks1FarGlow, 0.02f));
        }

        /// <summary>Lattice hex size (flat-top, centre to corner of the 760 m pitch hexagon), as Beat07_Reveal.HexSize.</summary>
        static readonly double LatticeHex = AbyssConst.LATTICE_PITCH / Math.Sqrt(3.0);

        /// <summary>Centre (x, z) of lattice cell (q, r): S0 centre + flat-top axial offset (same formula as Beat07_Reveal.CellCentre).</summary>
        public static Vector2 LatticeCell(int q, int r)
        {
            double s = LatticeHex, r3 = Math.Sqrt(3.0);
            return new Vector2((float)(AbyssConst.SHAFT_CENTRE.x + 1.5 * s * q), (float)(AbyssConst.SHAFT_CENTRE.y + s * (0.5 * r3 * q + r3 * r)));
        }

        /// <summary>Interior and web sample radii of the 14.5.7 readable-cell test (hole apothem 350, web 350-380).</summary>
        public const float CellInteriorR = 175f, CellWebR = 365f;
        public const float CellMinDistance = 500f, CellMaxDistance = 12000f;

        static readonly float[] s_cellVals = new float[25];

        /// <summary>
        /// 14.5.7 KS7b "readable cells": every lattice cell (S0 excluded) 0.5-12 km out whose centre projects into the lower
        /// 66 % of the frame, as [cu, cv, row_height, 5 interior points (centre and +/-175 m along x and z), 6 web points (at
        /// apothem 365 along the face normals)], all on the terrace plane, as frame fractions (row_height in frame heights).
        /// </summary>
        static void WriteCells(QaJson j, ShotSpec s, float near)
        {
            float y = AbyssConst.TERRACE_Y;
            j.Arr("cells");
            for (int q = -40; q <= 40; q++)
            {
                for (int r = -40; r <= 40; r++)
                {
                    if (q == 0 && r == 0) continue;
                    Vector2 c = LatticeCell(q, r);
                    var cw = new Vector3(c.x, y, c.y);
                    float dist = new Vector2(c.x - s.Eye.x, c.y - s.Eye.z).magnitude;
                    if (dist < CellMinDistance || dist > CellMaxDistance) continue;
                    if (!CompositionLint.ProjectPoint(cw, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out float cu, out float cv)) continue;
                    if (cu < 0f || cu > 1f || cv < 0.34f || cv > 1f) continue;
                    if (!CompositionLint.ProjectPoint(cw + new Vector3(0f, 0f, AbyssConst.SHAFT_APOTHEM), s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out _, out float va)
                        || !CompositionLint.ProjectPoint(cw - new Vector3(0f, 0f, AbyssConst.SHAFT_APOTHEM), s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out _, out float vb))
                        continue;
                    float[] o = s_cellVals;
                    o[0] = cu; o[1] = cv; o[2] = Mathf.Abs(vb - va);
                    bool ok = true;
                    for (int k = 0; k < 5 && ok; k++)
                    {
                        Vector3 p = cw;
                        if (k == 1) p.x += CellInteriorR; else if (k == 2) p.x -= CellInteriorR;
                        else if (k == 3) p.z += CellInteriorR; else if (k == 4) p.z -= CellInteriorR;
                        ok = CompositionLint.ProjectPoint(p, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out o[3 + 2 * k], out o[4 + 2 * k]);
                    }
                    for (int k = 0; k < 6 && ok; k++)
                    {
                        float a = AbyssConst.SHAFT_FACE_NORMAL_DEG[k] * Mathf.Deg2Rad;
                        Vector3 p = cw + new Vector3(Mathf.Cos(a) * CellWebR, 0f, Mathf.Sin(a) * CellWebR);
                        ok = CompositionLint.ProjectPoint(p, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out o[13 + 2 * k], out o[14 + 2 * k]);
                    }
                    if (ok) j.Inline(o);
                }
            }
            j.EndArray();
        }

        /// <summary>Height step of C1's pour lifts (14.2 M_ConcreteBoard: 3 m lifts; the joints lie at y = 3k).</summary>
        public const float LiftPitch = 3f;
        /// <summary>Lowest and highest joint written for KS3 (below -3 the ledge floor hides the face; above 45 it leaves the frame).</summary>
        public const int LiftJointMin = -1, LiftJointMax = 16;
        /// <summary>Angular step of the curve sampling around C1 (degrees) and the minimum spacing of kept points (frame fraction).</summary>
        const float LiftCurveStepDeg = 0.25f, LiftCurveMinGap = 0.005f;
        const int LiftCurveMaxPoints = 64;
        /// <summary>A joint curve needs at least this many in-frame points (the y 9 joint shows only near the C1 silhouette in KS3).</summary>
        public const int LiftCurveMinPoints = 4;
        static readonly List<float> s_curve = new List<float>(2 * LiftCurveMaxPoints + 1);

        /// <summary>
        /// R2-02 KS3 "lift_curves": for each pour-lift joint of C1 (y = 3k, the y 0 collar band excluded) the projected iso-y
        /// curve on the visible side of the cylinder, as [y, u0, v0, u1, v1, ...] (frame fractions, top-left origin; at most 64
        /// points at least 0.005 of the frame apart, at least 4, front-facing and inside the frame). image_metrics.py averages the luma
        /// profile across the curve at every point and counts the joints that stand out by at least 4 levels.
        /// </summary>
        public static void WriteLiftCurves(QaJson j, ShotSpec s, float near)
        {
            Vector3 c1 = AbyssConst.COLUMNS[0];   // (x, z, radius)
            j.Arr("lift_curves");
            for (int k = LiftJointMin; k <= LiftJointMax; k++)
            {
                if (k == 0) continue;   // y 0 is the collar band, not a lift joint
                float y = LiftPitch * k;
                if (!LiftCurve(c1, y, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, s_curve)) continue;
                j.Inline(s_curve.ToArray());
            }
            j.EndArray();
        }

        /// <summary>The visible, in-frame part of the iso-y circle of a column (see <see cref="WriteLiftCurves"/>) into <paramref name="o"/> as [y, u, v, ...].</summary>
        public static bool LiftCurve(Vector3 column, float y, Vector3 eye, Quaternion rot, float fov, float aspect, float near, List<float> o)
        {
            o.Clear();
            o.Add(y);
            float lu = -1f, lv = -1f;
            // start on the far side (the direction away from the eye) so the visible arc is one contiguous run
            Vector2 away = new Vector2(column.x - eye.x, column.y - eye.z);
            float a0 = Mathf.Atan2(away.y, away.x);
            int n = Mathf.RoundToInt(360f / LiftCurveStepDeg);
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * LiftCurveStepDeg * Mathf.Deg2Rad;
                var nrm = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = new Vector3(column.x, y, column.y) + nrm * column.z;
                if (Vector3.Dot(nrm, eye - p) <= 0f) continue;    // back side of the cylinder
                if (!CompositionLint.ProjectPoint(p, eye, rot, fov, aspect, near, out float u, out float v)) continue;
                if (u < 0.01f || u > 0.99f || v < 0.01f || v > 0.99f) continue;
                if (lu >= 0f && Mathf.Abs(u - lu) < LiftCurveMinGap && Mathf.Abs(v - lv) < LiftCurveMinGap) continue;
                o.Add(u); o.Add(v);
                lu = u; lv = v;
            }
            int pts = (o.Count - 1) / 2;
            if (pts > LiftCurveMaxPoints)
            {
                // keep LiftCurveMaxPoints evenly spread along the arc
                for (int k = 0; k < LiftCurveMaxPoints; k++)
                {
                    int src = Mathf.RoundToInt(k * (pts - 1f) / (LiftCurveMaxPoints - 1f));
                    o[1 + 2 * k] = o[1 + 2 * src];
                    o[2 + 2 * k] = o[2 + 2 * src];
                }
                o.RemoveRange(1 + 2 * LiftCurveMaxPoints, o.Count - 1 - 2 * LiftCurveMaxPoints);
            }
            return (o.Count - 1) / 2 >= LiftCurveMinPoints;
        }

        /// <summary>
        /// Regions and data of the 14.5 image acceptance checks the Python metrics judge (critique R1-29): KS2b side strips,
        /// the KS1 far glow, KS5a window grid, KS7b amber dots and readable cells, KS6 fan ring (spoke contrast), KS3 column face
        /// (pour-lift count).
        /// </summary>
        static void WriteAcceptanceRegions(QaJson j, ShotSpec s, float near, string baseTag)
        {
            switch (baseTag)
            {
                case "KS1_gallery_start":
                    if (CompositionLint.ProjectBounds(Ks1FarGlow, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out float g0, out float g1, out float g2, out float g3, out _))
                        j.PropRect("far_glow", Mathf.Clamp01(g0), Mathf.Clamp01(g1), Mathf.Clamp01(g2), Mathf.Clamp01(g3));
                    // R2-02: the left gallery wall at 1-2 m must show its board rows (per-board tone steps of at least 3 levels)
                    j.PropRect("wall_board", 0.03f, 0.28f, 0.10f, 0.93f);
                    break;
                case "KS2a_bridge_ahead":
                    // R2-02: the right shaft wall at 100-300 m (above and below the rib lines) must carry its macro noise
                    j.Obj("wall_macro");
                    j.Arr("rects");
                    j.Inline(0.75f, 0.20f, 0.98f, 0.27f);
                    j.Inline(0.75f, 0.45f, 0.98f, 0.52f);
                    j.EndArray();
                    j.EndObject();
                    break;
                case "KS2b_bridge_down":
                    j.Obj("side_strips");
                    j.PropRect("left", 0f, 0f, SideStripWidth, 1f);
                    j.PropRect("right", 1f - SideStripWidth, 0f, 1f, 1f);
                    j.EndObject();
                    break;
                case "KS3_column_up":
                    // the C1 face above the collar band (14.5.3: count the 3 m pour lifts up the wall)
                    j.PropRect("column_face", 0.16f, 0.0f, 0.57f, 0.50f);
                    // R2-02: the lift joints as projected iso-y curves of C1 (a row mean undercounts lifts that curve on the cylinder)
                    WriteLiftCurves(j, s, near);
                    break;
                case "KS5a_lift_city":
                    j.PropRect("window_grid", 0f, 0f, 1f, 1f);
                    break;
                case "KS6_gantry_fan":
                {
                    Vector3 c = AbyssConst.FAN_CENTRE;
                    if (CompositionLint.ProjectPoint(c, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out float cu, out float cv)
                        && CompositionLint.ProjectPoint(c + new Vector3(0f, AbyssConst.FAN_R_OUT, 0f), s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out _, out float tv))
                    {
                        j.Obj("fan_ring");
                        j.Key("centre").Inline(cu, cv);
                        j.Prop("ring_radius", Mathf.Abs(cv - tv));
                        j.EndObject();
                    }
                    break;
                }
                case "KS7b_honeycomb":
                    j.PropRect("amber_dots", 0f, 0.34f, 1f, 1f);
                    WriteCells(j, s, near);
                    break;
            }
        }

        /// <summary>Screen row (fraction from the top) of the elevation-0 direction (a straight row: zero roll).</summary>
        public static float HorizonRow(float pitchDeg, float vfovDeg)
        {
            return 0.5f + 0.5f * Mathf.Tan(pitchDeg * Mathf.Deg2Rad) / Mathf.Tan(0.5f * vfovDeg * Mathf.Deg2Rad);
        }

        // ---------------------------------------------------------------- horizon regions (10.13, 14.5.7 pitfalls, 14.10)

        /// <summary>
        /// Column ranges of every horizon sample: both sides, symmetric about the centre column, so the vignette weighs the
        /// sky, fog and backdrop samples alike and no centred subject (the KS7c door and its lamp at columns 0.477-0.523, the
        /// pier) enters them.
        /// </summary>
        public const float HorizonSideX0 = 0.02f, HorizonSideX1 = 0.25f;
        /// <summary>Sub-strips per side of the backdrop sample (it follows the curved image of the backdrop's far edge).</summary>
        public const int BackdropStrips = 4;
        /// <summary>Fog residual (1 - F) accepted in the backdrop sample: the honeycomb is 99.7 % fogged beyond this distance.</summary>
        public const float BackdropFogResidual = 0.003f;

        /// <summary>Distance beyond which the Exterior fog (D50 EXT_D50, F = 1 - exp(-(0.8326 d / D50)^2)) leaves at most the residual (20.3 km).</summary>
        public static float BackdropFoggedDistance => Mathf.Sqrt(-Mathf.Log(BackdropFogResidual)) * AbyssPalette.EXT_D50 / 0.8326f;

        /// <summary>
        /// The horizon regions of KS7b / KS7c (fractions; multi-rectangle regions are {"rects": [[x0, y0, x1, y1], ...]}), all
        /// over the same side columns (<see cref="HorizonSideX0"/>..<see cref="HorizonSideX1"/> and the mirror range):
        /// <list type="bullet">
        /// <item>horizon_row: v +/- 0.005 around the horizon row v (the -nopost OUT_HORIZ test);</item>
        /// <item>horizon_sky: v - 0.035 .. v - 0.015 (the sky just above the horizon);</item>
        /// <item>horizon_fog: v + 0.003 .. v + 0.015, below the horizon and above the far edge of the camera-following tier-2
        /// backdrop (28 km, TERRACE_Y): the clear colour, FogSky below the horizon, that all fogged geometry converges to;</item>
        /// <item>horizon_backdrop: the tier-2 backdrop between its far edge (+0.002 margin) and the distance where it is 99.7 %
        /// fogged (<see cref="BackdropFoggedDistance"/>), as <see cref="BackdropStrips"/> strips per side because a cone of
        /// constant elevation bends away from the horizon toward the frame sides.</item>
        /// </list>
        /// </summary>
        static void WriteHorizonRegions(QaJson j, ShotSpec s, float v)
        {
            float a = s.AspectRatio;
            float depth = Mathf.Max(1f, s.Eye.y - AbyssConst.TERRACE_Y);
            float eEdge = -Mathf.Atan2(depth, AbyssConst.HONEY_TIER2) * Mathf.Rad2Deg;
            float eFogged = -Mathf.Atan2(depth, BackdropFoggedDistance) * Mathf.Rad2Deg;
            SideRects(j, "horizon_row", v - 0.005f, v + 0.005f);
            SideRects(j, "horizon_sky", v - 0.035f, v - 0.015f);
            // the backdrop's far edge is highest (nearest the horizon) at the inner column of the side range
            float edgeInner = RowAtElevation(eEdge, HorizonSideX1, s.Pitch, s.Fov, a);
            SideRects(j, "horizon_fog", v + 0.003f, Mathf.Min(v + 0.015f, edgeInner - 0.003f));
            j.Obj("horizon_backdrop");
            j.Arr("rects");
            for (int side = 0; side < 2; side++)
            {
                for (int k = 0; k < BackdropStrips; k++)
                {
                    float uOut = HorizonSideX0 + (HorizonSideX1 - HorizonSideX0) * k / BackdropStrips;
                    float uIn = HorizonSideX0 + (HorizonSideX1 - HorizonSideX0) * (k + 1) / BackdropStrips;
                    float top = RowAtElevation(eEdge, uOut, s.Pitch, s.Fov, a) + 0.002f;   // the edge is lowest at the strip's outer column
                    float bottom = RowAtElevation(eFogged, uIn, s.Pitch, s.Fov, a);     // the fogged limit is highest at its inner column
                    if (bottom - top < 0.002f) continue;
                    if (side == 0) j.Inline(uOut, top, uIn, bottom);
                    else j.Inline(1f - uIn, top, 1f - uOut, bottom);
                }
            }
            j.EndArray();
            j.EndObject();
        }

        /// <summary>{"rects": [[x0, y0, x1, y1], [1 - x1, y0, 1 - x0, y1]]} over the two horizon side ranges.</summary>
        static void SideRects(QaJson j, string key, float y0, float y1)
        {
            j.Obj(key);
            j.Arr("rects");
            j.Inline(HorizonSideX0, y0, HorizonSideX1, y1);
            j.Inline(1f - HorizonSideX1, y0, 1f - HorizonSideX0, y1);
            j.EndArray();
            j.EndObject();
        }

        /// <summary>
        /// Screen row (fraction from the top, may lie outside 0..1) at which the ray through column u (fraction from the left)
        /// has the elevation elevDeg, for a zero-roll pinhole camera with the shot's pitch, vertical FOV and aspect. Elevation
        /// 0 is the straight row <see cref="HorizonRow"/>; any other elevation is a cone whose image bends away from the
        /// horizon toward the frame sides. Bisection: the elevation falls monotonically down a column.
        /// </summary>
        public static float RowAtElevation(float elevDeg, float u, float pitchDeg, float vfovDeg, float aspect)
        {
            double t = Math.Tan(0.5 * vfovDeg * Mathf.Deg2Rad), p = pitchDeg * Mathf.Deg2Rad;
            double cp = Math.Cos(p), sp = Math.Sin(p), target = Math.Tan(elevDeg * Mathf.Deg2Rad);
            double x = (2.0 * u - 1.0) * t * Math.Max(1e-4, aspect);
            double lo = -0.5, hi = 1.5;
            for (int i = 0; i < 48; i++)
            {
                double v = 0.5 * (lo + hi);
                double y = (1.0 - 2.0 * v) * t;            // camera-space ray (x, y, 1), y up
                double up = y * cp + sp, ahead = cp - y * sp;
                double tanE = up / Math.Sqrt(x * x + ahead * ahead);
                if (tanE > target) lo = v; else hi = v;
            }
            return (float)(0.5 * (lo + hi));
        }

        /// <summary>
        /// The clear aperture of the KS1b doorway: the intersection of the projected inner (z EXIT_DOOR_Z - EXIT_DEPTH) and
        /// outer (z EXIT_DOOR_Z) planes of the 1.2 x 2.0 opening, inset by 5 % of its size per side (no jamb or lintel pixels).
        /// </summary>
        static bool DoorwayAperture(ShotSpec s, float near, out float x0, out float y0, out float x1, out float y1) =>
            DoorwayAperture(s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out x0, out y0, out x1, out y1);

        /// <summary>
        /// Clear aperture of the gallery exit doorway seen from any pose (fractions, top-left origin), for the KS1b check and
        /// the per-frame aperture region of the t12_ev sequences (R1-07). False when the eye is not in front of the inner plane
        /// or the opening is out of frame.
        /// </summary>
        public static bool DoorwayAperture(Vector3 eye, Quaternion rot, float fov, float aspect, float near,
                                           out float x0, out float y0, out float x1, out float y1)
            => DoorwayAperture(eye, rot, fov, aspect, near, ApertureInset, out x0, out y0, out x1, out y1);

        /// <summary>Inset of the KS1b doorway aperture per side (fraction of its size): no jamb or lintel pixels.</summary>
        public const float ApertureInset = 0.05f;
        /// <summary>R2-01: the t12_ev sequences sample 10 % inside the jambs, so no jamb or lintel enters the region as it grows.</summary>
        public const float SequenceApertureInset = 0.10f;

        /// <summary>The clear aperture with a given inset per side (fraction of the aperture size).</summary>
        public static bool DoorwayAperture(Vector3 eye, Quaternion rot, float fov, float aspect, float near, float inset,
                                           out float x0, out float y0, out float x1, out float y1)
        {
            x0 = y0 = x1 = y1 = 0f;
            float zOut = AbyssConst.EXIT_DOOR_Z, zIn = zOut - AbyssConst.EXIT_DEPTH;
            if (eye.z >= zOut - near) return false;
            // inside the 0.8 m reveal the inner plane is behind the eye: the outer plane alone bounds the view out
            bool inReveal = eye.z >= zIn - near;
            if (!CompositionLint.ProjectBounds(DoorwayOpening(zOut, 0.001f), eye, rot, fov, aspect, near, out float bx0, out float by0, out float bx1, out float by1, out bool pb) || pb)
                return false;
            float ax0 = bx0, ay0 = by0, ax1 = bx1, ay1 = by1;
            // a few centimetres before the inner plane its lower corners fall behind the near plane (pitched view): then, as
            // inside the reveal, the outer plane alone bounds the view (qa_r22 t12_ev_walk frame 179 fell back to the full frame)
            if (!inReveal && (!CompositionLint.ProjectBounds(DoorwayOpening(zIn, 0.001f), eye, rot, fov, aspect, near, out ax0, out ay0, out ax1, out ay1, out bool pa) || pa))
            {
                ax0 = bx0; ay0 = by0; ax1 = bx1; ay1 = by1;
            }
            float l = Mathf.Max(ax0, bx0), r = Mathf.Min(ax1, bx1), t = Mathf.Max(ay0, by0), b = Mathf.Min(ay1, by1);
            float ix = inset * (r - l), iy = inset * (b - t);
            x0 = Mathf.Clamp01(l + ix); x1 = Mathf.Clamp01(r - ix);
            y0 = Mathf.Clamp01(t + iy); y1 = Mathf.Clamp01(b - iy);
            return x1 - x0 > 0.01f && y1 - y0 > 0.01f;
        }

        /// <summary>Projected opening expanded by half its size on every side (at least 0.06 of the frame), clamped to the frame.</summary>
        static bool ProjectedGlow(ShotSpec s, float near, Bounds opening, out float x0, out float y0, out float x1, out float y1)
        {
            if (!CompositionLint.ProjectBounds(opening, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, out x0, out y0, out x1, out y1, out _))
                return false;
            float mx = Mathf.Max(0.06f, 0.5f * (x1 - x0)), my = Mathf.Max(0.06f, 0.5f * (y1 - y0));
            x0 = Mathf.Clamp01(x0 - mx); x1 = Mathf.Clamp01(x1 + mx);
            y0 = Mathf.Clamp01(y0 - my); y1 = Mathf.Clamp01(y1 + my);
            return x1 - x0 > 0.01f && y1 - y0 > 0.01f;
        }
    }
}
