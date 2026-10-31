using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Megastructure
{
    [Flags]
    public enum QaShotFlags { None = 0, HideSky = 1, NoGlint = 2, NoHaze = 4, Orbital = 8 }

    /// <summary>One still capture: pose, progress, resolution and optional debug overrides.</summary>
    public sealed class StillShot
    {
        public string File, Kind, Shot, Tag;
        public int Beat, W, H, SetOverride = -1, Order;
        public float S, Yaw, Pitch, Vfov169;
        public Vector3 Eye;
        public double T = double.NaN;
        public QaShotFlags Flags;
        public bool IsKeyShot;
        public KeyShot Ks;
    }

    /// <summary>One frame-sequence plan (Part 11.6, 14.10): a deterministic walker at Speed m/s from S0 to S1, or a
    /// static camera, rendered at Fps in virtual time (Time.captureDeltaTime). Checks are hints for
    /// Tools/sequence_metrics.py and Tools/encode_test.py.</summary>
    public sealed class SequencePlan
    {
        public string Name;
        public float S0, S1, Speed = RingConstants.Walk;
        public int Fps = 30, StaticFrames = 120;
        public bool Static;
        public bool FixedGaze; public float Yaw, Pitch;
        public bool FixedEye; public Vector3 Eye;
        public int GazeToKs = -1;          // blend the gaze toward this key shot over the clip
        public string Checks = "";         // comma list: step1.2, activation0.5, nonincreasing1.5, monotone1.5, clip12, static, encode, shimmer2.0
        public Vector4 BandRegion = new Vector4(0.1f, 0.05f, 0.9f, 0.35f);   // x0, y0, x1, y1 (fractions, y from the top)
        /// <summary>Resolution multiplier of the harness resolution (2 = the 4 x supersampled shimmer reference).</summary>
        public int Scale = 1;
        /// <summary>Yaw pan per frame in degrees (the shimmer test pans 0.5 px/frame at 1080p).</summary>
        public float PanDegPerFrame;
        /// <summary>Keep the clock frozen at s0 / 1.5 during the clip (pans: only the camera moves).</summary>
        public bool FreezeClock;
        /// <summary>Name of the paired plan (shimmer: the supersampled reference of a 1080p pan, or the reverse).</summary>
        public string Pair = "";

        public int Frames => Static ? StaticFrames : Mathf.Max(2, Mathf.CeilToInt((S1 - S0) / Mathf.Max(1e-3f, Speed) * Fps) + 1);

        public float SAt(int i) => Static ? S0 : Mathf.Min(S1, S0 + Speed * i / (float)Fps);

        /// <summary>Degrees of yaw that move the image centre by px pixels at 1080 rows and the given vFOV.</summary>
        public static float DegPerPx1080(float vfov, float px) => px * Mathf.Rad2Deg * 2f * Mathf.Tan(0.5f * vfov * Mathf.Deg2Rad) / 1080f;
    }

    /// <summary>
    /// Capture plans of Part 11.2 and 14.10: key shots (14.10 table only), per-beat stills (start, middle, end and the
    /// free-look directions yaw +90 / -90 / 180 at pitch 0 and +60 from the mid pose), the beat-5 extras of 14.5.5
    /// (6)-(8), the defect tests T2, T3, T5, T7 and H10, and the frame-sequence plans.
    /// </summary>
    public static class CapturePlans
    {
        static string R(int w, int h) => w.ToString(CultureInfo.InvariantCulture) + "x" + h.ToString(CultureInfo.InvariantCulture);
        static string BeatName(int beat) => RingConstants.BeatNames[beat - 1].ToLowerInvariant();
        static string BeatPrefix(int beat) => "beat" + beat.ToString("00", CultureInfo.InvariantCulture) + "_" + BeatName(beat);
        /// <summary>Free-look suffix: +90, -90, 180 (the half turn has no sign).</summary>
        static string Sign(float yawOffset)
        {
            if (Mathf.Abs(Mathf.Abs(yawOffset) - 180f) < 1e-3f) return "180";
            return yawOffset > 0f ? "+" + QaUtil.F(yawOffset, "0") : QaUtil.F(yawOffset, "0");
        }

        // ---------------- key shots ----------------
        public static List<StillShot> KeyShotStills(KeyShot k, Vector2Int? res)
        {
            var list = new List<StillShot>();
            var rs = res.HasValue ? new[] { res.Value } : KeyShots.ResolutionsFor(k);
            foreach (var r in rs) list.Add(FromKs(k, r.x, r.y, null, QaShotFlags.None));
            return list;
        }

        static StillShot FromKs(KeyShot k, int w, int h, string suffix, QaShotFlags flags)
        {
            return new StillShot
            {
                File = KeyShots.FileName(k, w, h, suffix), Kind = suffix == null ? "ks" : "ks_variant", Shot = k.Id,
                Tag = k.Tag + (suffix ?? ""), Beat = k.Beat, S = k.S, Eye = k.Eye, Yaw = k.Yaw, Pitch = k.Pitch, Vfov169 = k.Vfov,
                W = w, H = h, T = k.T, Flags = flags, IsKeyShot = true, Ks = k
            };
        }

        /// <summary>Debug variants of key shots used by acceptance tests: KS03 with ArchHaze forced off (14.5.2 (8); hz = 0
        /// for e >= 32 deg through _SkyParams.w = 1, see CaptureHarness) and KS12 with the swarm glints disabled
        /// (14.5.7 (2)). Each is compared with its base key shot at the same resolution (Tools/qa_metrics.py computes the
        /// KS03 arch contrast ratio).</summary>
        public static List<StillShot> KeyShotVariants(Vector2Int? res)
        {
            var list = new List<StillShot>();
            var r1 = res ?? KeyShots.Res1080;
            var r4 = res ?? KeyShots.Res4K;
            list.Add(FromKs(KeyShots.All[3], r1.x, r1.y, "_nohaze", QaShotFlags.NoHaze));
            list.Add(FromKs(KeyShots.All[12], r4.x, r4.y, "_noglint", QaShotFlags.NoGlint));
            return list;
        }

        // ---------------- per-beat stills ----------------
        public static float BeatStartS(int beat) => RingConstants.BeatS[beat - 1] + 1f;
        public static float BeatEndS(int beat) => RingConstants.BeatS[beat] - 1f;
        public static float BeatMidS(int beat) => 0.5f * (RingConstants.BeatS[beat - 1] + RingConstants.BeatS[beat]);

        public static List<StillShot> BeatStills(int beat, Vector2Int? res)
        {
            var r = res ?? KeyShots.Res1080;
            string rs = (r.x == 1920 && r.y == 1080) ? "" : "_" + R(r.x, r.y);
            var list = new List<StillShot>();
            void Add(string tag, float s, float yaw, float pitch)
            {
                var eye = QaPath.EyeAt(s, out _);
                list.Add(new StillShot
                {
                    File = BeatPrefix(beat) + "_" + tag + rs + ".png", Kind = "still", Shot = "beat" + beat, Tag = tag, Beat = beat,
                    S = s, Eye = eye, Yaw = yaw, Pitch = pitch, Vfov169 = RingSchedule.Vfov(s), W = r.x, H = r.y
                });
            }
            foreach (var (tag, s) in new[] { ("start", BeatStartS(beat)), ("mid", BeatMidS(beat)), ("end", BeatEndS(beat)) })
            {
                QaPath.GazeAt(s, out float y, out float p);
                Add(tag, s, y, p);
            }
            float sm = BeatMidS(beat);
            QaPath.GazeAt(sm, out float ym, out _);
            foreach (float pitch in new[] { 0f, 60f })
                foreach (float dy in new[] { 90f, -90f, 180f })
                    Add("free_yaw" + Sign(dy) + "_p" + QaUtil.F(pitch, "0"), sm, Mathf.Repeat(ym + dy, 360f), pitch);
            return list;
        }

        // ---------------- thumbnail stills (beat 2, for the video's thumbnail; not part of the QA plan) ----------------
        /// <summary>Wide, dramatic views of the valley where the land curves up into the sky (-shots thumbs). Eyes on the
        /// terrain are placed at the terrain height (RingWorld.Height, set 0) plus the eye height; vFOV is per shot.</summary>
        public static List<StillShot> ThumbStills(Vector2Int? res)
        {
            var list = new List<StillShot>();
            var r = res ?? KeyShots.Res4K;
            void Add(string tag, float s, Vector3 eye, float yaw, float pitch, float vfov)
            {
                list.Add(new StillShot
                {
                    File = "thumb_" + tag + "_" + R(r.x, r.y) + ".png", Kind = "thumb", Shot = "thumb", Tag = tag, Beat = 2,
                    S = s, Eye = eye, Yaw = yaw, Pitch = pitch, Vfov169 = vfov, W = r.x, H = r.y
                });
            }
            Vector3 Ground(float x, float z, float above) => new Vector3(x, RingWorld.Height(x, z, 0f) + above, z);
            var crest = new Vector3(0f, RingConstants.BridgeCrest + RingConstants.Eye, RingConstants.BridgeCrestZ);
            // 1 bridge crest, wide: the path and rails lead into land that climbs into the sky overhead
            Add("crest_wide", 150f, crest, 0f, 24f, 78f);
            // 2 bridge crest, toward the golden star side: warm glow, hill and belvedere, the arch above
            Add("crest_golden", 150f, crest, -40f, 20f, 72f);
            // 3 belvedere hill flank, high over the valley: river, bridge and path below, the far side hanging overhead
            Add("hill_overlook", 205f, Ground(-74f, 182f, 2.2f), 28f, 16f, 74f);
            // 4 low in the meadow beside the path, looking up: grass and flowers against the ceiling of land
            Add("meadow_up", 95f, Ground(4.2f, 95f, 0.45f), 8f, 38f, 82f);
            // 5 looking back toward the capsule: the ring rises behind the walker too
            Add("look_back", 150f, crest, 180f, 18f, 76f);
            return list;
        }

        // ---------------- star stills (beat 7: the star inside its swarm, for the video) ----------------
        public static List<StillShot> StarStills(Vector2Int? res)
        {
            var list = new List<StillShot>();
            var r = res ?? KeyShots.Res4K;
            void Add(string tag, float s, Vector3 eye, float yaw, float pitch, float vfov)
            {
                list.Add(new StillShot
                {
                    File = "star_" + tag + "_" + R(r.x, r.y) + ".png", Kind = "thumb", Shot = "star", Tag = tag, Beat = 7,
                    S = s, Eye = eye, Yaw = yaw, Pitch = pitch, Vfov169 = vfov, W = r.x, H = r.y
                });
            }
            // 1 telephoto: the star and the three glittering bands fill the frame, the pier's rails at the bottom edge
            Add("tele", 950f, new Vector3(290f, 1.7f, 660f), 96f, 6.5f, 38f);
            // 2 the beacon lens against the star (1.6 m behind it, at lens height)
            Add("beacon", 958f, new Vector3(299.2f, 1.2f, 660.5f), 96f, 7f, 46f);
            // 3 low by the right rail: rails leading to the star, the hull falling away below
            Add("rail_low", 930f, new Vector3(262f, 0.7f, 664.4f), 99f, 8f, 66f);
            // 4 from the pier's edge: the star to the left, the hull 200 m below to the right
            Add("edge_hull", 958f, new Vector3(300f, 1.7f, 664.6f), 116f, -4f, 70f);
            return list;
        }

        // ---------------- orbital stills (the whole ring from space; capture-only model QA/OrbitalView.cs) ----------------
        public static List<StillShot> OrbitStills(Vector2Int? res)
        {
            var list = new List<StillShot>();
            var r = res ?? KeyShots.Res4K;
            void Look(string tag, Vector3 eye, Vector3 target, float vfov)
            {
                Vector3 d = (target - eye).normalized;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
                list.Add(new StillShot
                {
                    File = "orbit_" + tag + "_" + R(r.x, r.y) + ".png", Kind = "thumb", Shot = "orbit", Tag = tag, Beat = 7,
                    S = 950f, Eye = eye, Yaw = yaw, Pitch = pitch, Vfov169 = vfov, W = r.x, H = r.y, Flags = QaShotFlags.Orbital
                });
            }
            Vector3 cap = OrbitalView.RightCapCentre, mid = OrbitalView.Centre;
            Vector3 L7 = RingConstants.StarL7;
            Vector3 star = new Vector3(300f, 0f, 660f) + RingConstants.StarOutRange * L7;
            // 1 from near the star, looking back at the face of the ring the walk came out of (the pier glows at its bottom)
            Look("from_star", cap + 27000f * ((star - cap).normalized + new Vector3(0f, -0.2f, 0.32f)).normalized, cap + new Vector3(-5000f, 0f, 0f), 64f);
            // 2 three-quarter view: the lit face and the dark drum with its ribs catching the star
            Look("three_quarter", cap + new Vector3(30000f, -9000f, 27000f), mid + new Vector3(0f, 0f, 0f), 48f);
            // 3 side view: the whole 20 km drum, its ribs lit edge-on by the star
            Look("side", mid + new Vector3(4000f, -7000f, 62000f), mid, 52f);
            // 4 close to the face: the rim wall rising 12 km into space above the tiny glowing pier
            Look("face_rise", new Vector3(OrbitalView.RightOut + 7000f, 1500f, 660f - 4000f), new Vector3(OrbitalView.RightOut, 7000f, 660f), 70f);
            // 5 backlit: from behind the ring toward the star, which hangs just beyond the rim
            Vector3 up = Vector3.up - Vector3.Dot(Vector3.up, L7) * L7;
            Vector3 e5 = mid - 52000f * (L7 + 0.33f * up.normalized).normalized;
            Vector3 aim = e5 + ((mid - e5).normalized + 0.9f * L7).normalized;
            Look("backlit", e5, aim, 66f);
            return list;
        }

        // ---------------- beat 5 extras (14.5.5 (6)-(8)) ----------------
        public static List<StillShot> Beat5Extras(Vector2Int? res)
        {
            var list = new List<StillShot>();
            var r4 = res ?? KeyShots.Res4K;
            var r1 = res ?? KeyShots.Res1080;
            string p = BeatPrefix(5);
            void Add(string tag, float s, Vector3 eye, float yaw, float pitch, Vector2Int r, int set)
            {
                list.Add(new StillShot
                {
                    File = p + "_" + tag + "_" + R(r.x, r.y) + ".png", Kind = "extra", Shot = "beat5x", Tag = tag, Beat = 5,
                    S = s, Eye = eye, Yaw = yaw, Pitch = pitch, Vfov169 = RingSchedule.Vfov(s), W = r.x, H = r.y, SetOverride = set
                });
            }
            // (6) no ground colour beyond the wall base line: s 700 and 760, yaw 90, pitch 0 and +60 (4K ground classifier)
            foreach (float s in new[] { 700f, 760f })
                foreach (float pitch in new[] { 0f, 60f })
                    Add("x6_s" + QaUtil.F(s, "0") + "_yaw90_p" + QaUtil.F(pitch, "0"), s, QaPath.EyeAt(s, out _), 90f, pitch, r4, -1);
            // (7) far rim: s 700, yaw 0 and 90, pitch +60 and +80
            foreach (float yaw in new[] { 0f, 90f })
                foreach (float pitch in new[] { 60f, 80f })
                    Add("x7_s700_yaw" + QaUtil.F(yaw, "0") + "_p" + QaUtil.F(pitch, "0"), 700f, QaPath.EyeAt(700f, out _), yaw, pitch, r4, -1);
            // (8) looking back from (60, 1.7, 576) yaw 180 pitch 0 with -set 0 and -set 1
            var back = new Vector3(60f, RingConstants.Eye, 576f);
            float sBack = RingWorld.Project(new Vector2(back.x, back.z));
            Add("x8_back_set0", sBack, back, 180f, 0f, r1, 0);
            Add("x8_back_set1", sBack, back, 180f, 0f, r1, 1);
            return list;
        }

        // ---------------- defect tests T2, T3, T5, T7 and H10 (14.10, 14.7) ----------------
        public static List<StillShot> DefectStills(Vector2Int? res)
        {
            var list = new List<StillShot>();
            var r1 = res ?? KeyShots.Res1080;
            var r4 = res ?? KeyShots.Res4K;
            void Add(string tag, int beat, float s, Vector3 eye, float yaw, float pitch, Vector2Int r, QaShotFlags flags, float vfov = float.NaN)
            {
                list.Add(new StillShot
                {
                    File = tag + "_" + R(r.x, r.y) + ".png", Kind = "defect", Shot = tag.Substring(0, tag.IndexOf('_') > 0 ? tag.IndexOf('_') : tag.Length),
                    Tag = tag, Beat = beat, S = s, Eye = eye, Yaw = yaw, Pitch = pitch, Vfov169 = float.IsNaN(vfov) ? RingSchedule.Vfov(s) : vfov,
                    W = r.x, H = r.y, Flags = flags
                });
            }
            var crest = new Vector3(0f, RingConstants.BridgeCrest + RingConstants.Eye, RingConstants.BridgeCrestZ);
            Add("t2_bridge_down", 2, 150f, crest, 0f, -60f, r1, QaShotFlags.None);
            if (!res.HasValue) Add("t2_bridge_down", 2, 150f, crest, 0f, -60f, r4, QaShotFlags.None);
            // T3: ceramic bollard at s 480 and s 520, with and without the sky-dome mesh (H10: reflections from RingSkyColor only)
            foreach (float s in new[] { 480f, 520f })
            {
                var eye = QaPath.EyeAt(s, out float yaw);
                Add("t3_bollard_s" + QaUtil.F(s, "0") + "_sky", 4, s, eye, yaw, -8f, r1, QaShotFlags.None);
                Add("t3_bollard_s" + QaUtil.F(s, "0") + "_nosky", 4, s, eye, yaw, -8f, r1, QaShotFlags.HideSky);
            }
            // T5: wall-foot close-up (100, 1.7, 700) yaw 90 pitch -10 at 1080p (s = the projection of the eye onto the path)
            var t5 = new Vector3(100f, RingConstants.Eye, 700f);
            Add("t5_wall_foot", 5, RingWorld.Project(new Vector2(t5.x, t5.z)), t5, 90f, -10f, r1, QaShotFlags.None);
            // T7: free-look at the platform (252, 1.7, 660) yaw 270 pitch -45: no hull / outer-face intersection line
            var t7 = new Vector3(252f, RingConstants.Eye, 660f);
            Add("t7_platform_back", 7, 912f, t7, 270f, -45f, r1, QaShotFlags.None);
            if (!res.HasValue) Add("t7_platform_back", 7, 912f, t7, 270f, -45f, r4, QaShotFlags.None);
            // H10: ceramic wall luma in beat 5 identical (<= 1 level) with the sky-dome mesh disabled (KS07 pose)
            var k7 = KeyShots.All[7];
            Add("h10_wall_sky", 5, k7.S, k7.Eye, k7.Yaw, k7.Pitch, r1, QaShotFlags.None, k7.Vfov);
            Add("h10_wall_nosky", 5, k7.S, k7.Eye, k7.Yaw, k7.Pitch, r1, QaShotFlags.HideSky, k7.Vfov);
            return list;
        }

        // ---------------- plan parsing ----------------
        /// <summary>
        /// Builds the still plan from -shots: all | keys | stills | beats | extras | defects | variants | none, or a comma
        /// list of key-shot ids or tags (KS02, valley_crest), beatN, t2, t3, t5, t7, h10. -res WxH restricts every shot
        /// to that resolution.
        /// </summary>
        public static List<StillShot> Parse(string spec, Vector2Int? res, List<string> unknown)
        {
            var list = new List<StillShot>();
            if (string.IsNullOrEmpty(spec)) spec = "all";
            foreach (var raw in spec.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw.Trim().ToLowerInvariant();
                switch (t)
                {
                    case "none": break;
                    case "all":
                        foreach (var k in KeyShots.All) list.AddRange(KeyShotStills(k, res));
                        list.AddRange(KeyShotVariants(res));
                        for (int b = 1; b <= 7; b++) list.AddRange(BeatStills(b, res));
                        list.AddRange(Beat5Extras(res));
                        list.AddRange(DefectStills(res));
                        break;
                    case "keys": foreach (var k in KeyShots.All) list.AddRange(KeyShotStills(k, res)); break;
                    case "variants": list.AddRange(KeyShotVariants(res)); break;
                    case "stills":
                        for (int b = 1; b <= 7; b++) list.AddRange(BeatStills(b, res));
                        list.AddRange(Beat5Extras(res));
                        list.AddRange(DefectStills(res));
                        break;
                    case "beats": for (int b = 1; b <= 7; b++) list.AddRange(BeatStills(b, res)); break;
                    case "extras": list.AddRange(Beat5Extras(res)); break;
                    case "defects": list.AddRange(DefectStills(res)); break;
                    case "thumbs": list.AddRange(ThumbStills(res)); break;
                    case "star": list.AddRange(StarStills(res)); break;
                    case "orbit": list.AddRange(OrbitStills(res)); break;
                    case "t2": case "t3": case "t5": case "t7": case "h10":
                        foreach (var d in DefectStills(res)) if (d.Tag.StartsWith(t + "_")) list.Add(d);
                        break;
                    default:
                        if (t.StartsWith("beat") && int.TryParse(t.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bn) && bn >= 1 && bn <= 7)
                        { list.AddRange(BeatStills(bn, res)); if (bn == 5) list.AddRange(Beat5Extras(res)); }
                        else if (KeyShots.TryFind(raw.Trim(), out var ks)) list.AddRange(KeyShotStills(ks, res));
                        else unknown?.Add(raw);
                        break;
                }
            }
            // de-duplicate by file name, keep the first; order by resolution (fewer RT reallocations), then debug variants
            // after the key shots of the same resolution (a variant is compared with its base), then plan order
            var seen = new HashSet<string>();
            var outList = new List<StillShot>();
            foreach (var s in list) if (seen.Add(s.File)) { s.Order = outList.Count; outList.Add(s); }
            outList.Sort((a, b) =>
            {
                int c = (a.W * a.H).CompareTo(b.W * b.H);
                if (c == 0) c = a.W.CompareTo(b.W);
                if (c == 0) c = (a.Kind == "ks_variant").CompareTo(b.Kind == "ks_variant");
                return c != 0 ? c : a.Order.CompareTo(b.Order);
            });
            return outList;
        }

        // ---------------- sequences ----------------
        static SequencePlan Walk(string name, float s0, float s1, string checks) => new SequencePlan { Name = name, S0 = s0, S1 = s1, Checks = checks };

        static SequencePlan StaticAt(string name, float s, string checks, int frames = 120)
        {
            return new SequencePlan { Name = name, S0 = s, S1 = s, Static = true, StaticFrames = frames, Checks = checks };
        }

        static SequencePlan FromKs(string name, KeyShot k, float s0, float s1, int fps, bool stat, int frames, string checks, Vector4 band)
        {
            return new SequencePlan
            {
                Name = name, S0 = s0, S1 = s1, Fps = fps, Static = stat, StaticFrames = frames, FixedGaze = true, Yaw = k.Yaw, Pitch = k.Pitch,
                FixedEye = stat, Eye = k.Eye, Checks = checks, BandRegion = band
            };
        }

        /// <summary>Every named sequence plan.</summary>
        public static List<SequencePlan> AllSequences()
        {
            var list = new List<SequencePlan>();
            // +-10 s (15 m at 1.5 m/s) around every beat boundary of 14.6
            for (int b = 1; b <= 6; b++)
            {
                float sb = RingConstants.BeatS[b];
                list.Add(Walk("b" + b + (b + 1), sb - 15f, sb + 15f, "step1.2,activation0.5"));
            }
            // set swaps and activations (14.6): sealed swap at 576, SetOutside on at 778, NearPlain/FarValley off at 810
            list.Add(Walk("swap576", RingConstants.SwapS - 15f, RingConstants.SwapS + 15f, "step1.2,activation0.5,swap0.5"));
            list.Add(Walk("set778", 763f, 793f, "step1.2,activation0.5"));
            list.Add(Walk("set810", 795f, 825f, "step1.2,activation0.5"));
            // long segments of 14.10
            list.Add(Walk("ark", 270f, 330f, "step1.2,shadowedge3"));
            list.Add(Walk("sunset", 411f, 516f, "step1.2,nonincreasing1.5"));
            list.Add(Walk("exit", 860f, 900f, "step1.2,monotone1.5,clip12"));
            // approaches to the money shots (Part 11.6)
            foreach (int ki in new[] { 2, 5, 6, 7, 12 })
            {
                var k = KeyShots.All[ki];
                list.Add(new SequencePlan { Name = "approach_" + k.Id.ToLowerInvariant(), S0 = k.S - 15f, S1 = k.S, GazeToKs = ki, Checks = "step1.2" });
            }
            // 120 static frames per beat at the mid pose (shimmer, shadow flicker, z-fighting)
            for (int b = 1; b <= 7; b++) list.Add(StaticAt("static" + b, BeatMidS(b), "static"));
            // T4 light pool beside the path at night (s 520, looking at the pool at s 526), T6 tunnel mouth hold at s 888
            var pool = StaticAt("t4_pool", 520f, "static");
            pool.FixedGaze = true; pool.Yaw = 0f; pool.Pitch = -15f;
            list.Add(pool);
            list.Add(FromKs("t6_hold888", KeyShots.All[11], 888f, 888f, 30, true, 120, "clip12", new Vector4(0.3f, 0.3f, 0.7f, 0.7f)));
            // encode-test clips (11.12): 10 s at 60 fps of the dusk sky, the night arc, the tunnel exit, the swarm; 30 s around totality
            list.Add(FromKs("enc_dusk", KeyShots.All[5], 437f, 452f, 60, false, 0, "encode", new Vector4(0.45f, 0.03f, 0.97f, 0.40f)));
            list.Add(FromKs("enc_dusk30", KeyShots.All[5], 430f, 475f, 60, false, 0, "encode", new Vector4(0.45f, 0.03f, 0.97f, 0.40f)));
            list.Add(FromKs("enc_night", KeyShots.All[6], 500f, 515f, 60, false, 0, "encode", new Vector4(0.1f, 0.02f, 0.9f, 0.38f)));
            var exit = FromKs("enc_exit", KeyShots.All[11], 879f, 894f, 60, false, 0, "encode,clip12", new Vector4(0.05f, 0.05f, 0.35f, 0.95f));
            exit.Pitch = 2f;
            list.Add(exit);
            list.Add(FromKs("enc_swarm", KeyShots.All[12], 912f, 912f, 60, true, 600, "encode,static", new Vector4(0.55f, 0.15f, 0.75f, 0.45f)));
            // shimmer (14.10): pan 0.5 px/frame for 30 frames at a key shot with thin repeated structure, frozen clock,
            // at the harness resolution and at 2 x 2 supersampling (the reference); beat 3 window rows: 0.25 px/frame for
            // 60 frames (14.5.3 acceptance 4)
            AddPan(list, "pan_ks02", KeyShots.All[2], 0.5f, 30);    // rails, aqueduct, terrain edge
            AddPan(list, "pan_ks04", KeyShots.All[4], 0.25f, 60);   // ark window rows
            AddPan(list, "pan_ks07", KeyShots.All[7], 0.5f, 30);    // wall seams
            AddPan(list, "pan_ks10", KeyShots.All[10], 0.5f, 30);   // tunnel ribs
            AddPan(list, "pan_ks12", KeyShots.All[12], 0.5f, 30);   // panels, rails, hull ridges
            return list;
        }

        static void AddPan(List<SequencePlan> list, string name, KeyShot k, float px, int frames)
        {
            for (int scale = 1; scale <= 2; scale++)
            {
                var p = FromKs(scale == 1 ? name : name + "_ss", k, k.S, k.S, 30, true, frames, "shimmer2.0", new Vector4(0f, 0f, 1f, 1f));
                p.Scale = scale;
                p.FreezeClock = true;
                p.PanDegPerFrame = SequencePlan.DegPerPx1080(k.Vfov, px);
                p.Pair = scale == 1 ? name + "_ss" : name;
                list.Add(p);
            }
        }

        /// <summary>Plans for -sequence: a name, a comma list, or a group (boundaries, statics, encode, approaches, long,
        /// defects, shimmer, all).</summary>
        public static List<SequencePlan> ParseSequences(string spec, List<string> unknown)
        {
            var all = AllSequences();
            var list = new List<SequencePlan>();
            if (string.IsNullOrEmpty(spec)) return list;
            foreach (var raw in spec.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw.Trim().ToLowerInvariant();
                int before = list.Count;
                foreach (var p in all)
                {
                    bool take = t == "all" || p.Name == t
                        || (t == "boundaries" && (p.Name.Length == 3 && p.Name[0] == 'b' || p.Name.StartsWith("swap") || p.Name.StartsWith("set")))
                        || (t == "statics" && p.Name.StartsWith("static"))
                        || (t == "encode" && p.Name.StartsWith("enc_"))
                        || (t == "approaches" && p.Name.StartsWith("approach_"))
                        || (t == "long" && (p.Name == "ark" || p.Name == "sunset" || p.Name == "exit"))
                        || (t == "shimmer" && p.Name.StartsWith("pan_"))
                        || (t == "defects" && (p.Name == "t4_pool" || p.Name == "t6_hold888"));
                    if (take && !list.Contains(p)) list.Add(p);
                }
                if (list.Count == before && t != "none") unknown?.Add(raw);
            }
            return list;
        }
    }
}
