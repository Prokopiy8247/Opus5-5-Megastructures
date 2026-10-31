// ShotPlan: the capture lists of the harness as pure data (MASTER_PROMPT 11.2, 11.6, 11.12, 14.10.2,
// 14.10.3, 14.6, Part 10 items 6, 12, 21, 22, 35, 36). No UnityEngine dependency, so EditMode tests
// check the plan (every key shot at every resolution, tag format, wide shots, window lengths).
//
// Capture sets (-capture-set, comma list):
//   keyshots   every ThreadConstants.KeyShots at 1920x1080 and 3840x2160, the in-game FOV(s) version of
//              the _wide shots, framing variants at 1680x1050 (_1610) and 2560x1080 (_219), plus the two
//              extra stills the table names (KF3 at s = 305, KF7a at s = 750)
//   beats      per beat start / mid / end stills and the free-look stills from the middle of the beat
//   sequences  = transitions + statics + pans + edges (PNG frame sequences at 30 fps virtual time)
//   transitions, statics, pans, edges   sub-sets of "sequences"
//   encode     10 s (30 s for the two heaviest transitions) at 60 fps of the gradient-heavy views (11.12)
//   parity     one window capture and one RenderTexture capture of the same frame (player only)
//   stills     = parity + keyshots + beats;  all = stills + sequences;  everything = all + encode
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Megastructure.QA
{
    /// <summary>One still image of the plan.</summary>
    public sealed class StillShot
    {
        public string tag;            // unique; also the file name without ".png"
        public string set;            // keyshots, beats, parity
        public string baseTag;        // key-shot tag of ThreadConstants (key shots), else the tag
        public string variant;        // "1080", "4k", "1610", "219", "window"
        public int width, height;
        public double s, x, y, z, yaw, pitch, fov;
        public double ambient;        // ambient clock value used for the shot (deterministic)
        public int keyShotIndex = -1;
        public bool gameFov = true;   // FOV equals the in-game FOV(s) (framing acceptances use these)
        public bool framing;          // evaluate the projected subjects (key shots)
        public double lumaMin = double.NaN, lumaMax = double.NaN;

        public string File => tag + ".png";
        public QaView View => new QaView(x, y, z, yaw, pitch, fov, width, height);
    }

    public enum SeqMode { Walk = 0, Static = 1, PanPitch = 2, PanYaw = 3, RealStart = 4, RealEnd = 5 }

    /// <summary>One frame sequence of the plan.</summary>
    public sealed class SequenceSpec
    {
        public string name;           // directory name (seq_*, enc_*)
        public string set;            // transitions, statics, pans, edges, encode
        public string purpose;
        public SeqMode mode;
        public int fps, width, height;
        public int frames;            // fixed frame count (RealEnd: upper bound)
        public double s0, s1;         // Walk: plan distance at frame 0 and at the last frame
        public double speed = ThreadConstants.WalkSpeed;
        public bool yawWorld;         // Walk: yaw is a world yaw, else an offset to the path heading
        public double yaw, pitch;     // Walk: yaw (offset) and pitch; Static / Pan: the base pose
        public double x, y, z, fov;   // Static / Pan pose
        public double panStepDeg;     // Pan: angle per frame
        public bool advanceAmbient = true;
        public double ambient0;

        public double Seconds => fps > 0 ? frames / (double)fps : 0;

        /// <summary>Analytic camera pose of frame n (Walk / Static / Pan).</summary>
        public void FramePose(int n, out double s, out double px, out double py, out double pz, out double yawDeg, out double pitchDeg, out double fovDeg)
        {
            switch (mode)
            {
                case SeqMode.Walk:
                {
                    s = Math.Min(ThreadConstants.PathLength, s0 + speed * n / fps);
                    D3 p = ThreadMath.CorridorToWorld(s, 0, ThreadConstants.EyeHeight);
                    px = p.x; py = p.y; pz = p.z;
                    yawDeg = yawWorld ? yaw : ThreadMath.Heading(s) / ThreadMath.Deg + yaw;
                    pitchDeg = pitch;
                    fovDeg = ThreadMath.Fov(s);
                    return;
                }
                case SeqMode.PanPitch:
                    s = s0; px = x; py = y; pz = z; yawDeg = yaw; pitchDeg = pitch + panStepDeg * n; fovDeg = fov;
                    return;
                case SeqMode.PanYaw:
                    s = s0; px = x; py = y; pz = z; yawDeg = yaw + panStepDeg * (n - frames / 2); pitchDeg = pitch; fovDeg = fov;
                    return;
                default:
                    s = s0; px = x; py = y; pz = z; yawDeg = yaw; pitchDeg = pitch; fovDeg = fov;
                    return;
            }
        }

        public double AmbientAt(int n) => advanceAmbient ? ambient0 + n / (double)fps : ambient0;
    }

    public sealed class CapturePlan
    {
        public readonly List<StillShot> stills = new List<StillShot>();
        public readonly List<SequenceSpec> sequences = new List<SequenceSpec>();
        public StillShot parity;
        public readonly List<string> sets = new List<string>();
        public readonly List<string> unknownSets = new List<string>();
    }

    public sealed class CapturePlanOptions
    {
        public int seqWidth = 1920, seqHeight = 1080;
        public bool encode4k;
        public string[] only;         // keep only tags / sequence names containing one of these
    }

    public static class ShotPlan
    {
        // ------------------------------------------------------------------ resolutions (11.2, 14.10.2)
        public const int W1080 = 1920, H1080 = 1080, W4K = 3840, H4K = 2160;
        public const int W1610 = 1680, H1610 = 1050, W219 = 2560, H219 = 1080;

        // ------------------------------------------------------------------ timings (11.6, 11.12, 14.10.3)
        public const int SeqFps = 30;                  // frame sequences at 30 fps virtual time
        public const int EncodeFps = 60;               // encode clips at 60 fps
        public const double EncodeSeconds = 10.0, EncodeTransitionSeconds = 30.0;
        public const double MinWindowSeconds = 20.0;   // every boundary window at least 20 s (11.6)
        public const double StaticClipSeconds = 3.0;   // T-BANK / 14.10.3 static clip per beat
        public const double SlowPanPx = 0.1;           // 14.10.3 aliasing: centroid jitter under a 0.1 px/frame pan
        public const int SlowPanFrames = 5;
        public const double MoirePanPx = 1.0;          // T moire: 1 px/frame pan over the stairs
        public const int MoirePanFrames = 60;
        public const int EdgeFrames = 60;              // defect 22: first and last 60 frames
        public const double EndLeadIn = 3.0;           // seq_end starts 3 m before the beacon trigger (2 s at 1.5 m/s)
        public const double EndTail = 0.5;             // frames kept after the 14 s ending
        public const double LookUpPitch = 89.0, LookDownPitch = -89.0;
        public const double FreeLookYaw = 90.0;

        // 14.6 transition table (s windows, lines 766-771); not in ThreadConstants as an array.
        public static readonly double[,] TransitionWindows = { { 78, 102 }, { 225, 265 }, { 262, 345 }, { 388, 412 }, { 566, 606 }, { 738, 768 } };
        // T-SWAP window (14.10.1 line 934) and the deck pass window of the task (14.6: deck 578-595, extended).
        public const double SwapWindow0 = 280.0, SwapWindow1 = 290.0;
        public const double DeckWindow0 = 570.0, DeckWindow1 = 610.0;
        // T-BANK static-camera positions (14.10.1 line 939).
        public static readonly double[] BankStaticS = { 0, 120, 255, 270, 340 };
        // T moire: camera z on the avenue axis and the stair point it aims at (three distances to the stairs).
        public static readonly double[,] MoireCamAim = { { 160, 280 }, { 215, 265 }, { 238, 256 } };
        public static readonly string[] MoireNames = { "far", "mid", "near" };

        static readonly string[] s_freeLookTags = { "look_r", "look_l", "look_back" };
        static readonly double[] s_freeLookYaw = { FreeLookYaw, -FreeLookYaw, 2 * FreeLookYaw };

        public static string BeatPrefix(int beatIndex) => "beat" + (beatIndex + 1).ToString("D2", CultureInfo.InvariantCulture) + "_" + ThreadConstants.BeatNames[beatIndex];

        // ------------------------------------------------------------------ plan

        public static CapturePlan Build(string setList, CapturePlanOptions opt = null)
        {
            if (opt == null) opt = new CapturePlanOptions();
            var plan = new CapturePlan();
            bool parity = false, keys = false, beats = false, trans = false, statics = false, pans = false, edges = false, encode = false;
            string[] parts = (string.IsNullOrEmpty(setList) ? "all" : setList).Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim().ToLowerInvariant();
                if (p.Length == 0) continue;
                switch (p)
                {
                    case "keyshots": keys = true; break;
                    case "beats": beats = true; break;
                    case "parity": parity = true; break;
                    case "sequences": trans = statics = pans = edges = true; break;
                    case "transitions": trans = true; break;
                    case "statics": statics = true; break;
                    case "pans": pans = true; break;
                    case "edges": edges = true; break;
                    case "encode": encode = true; break;
                    case "stills": parity = keys = beats = true; break;
                    case "all": parity = keys = beats = trans = statics = pans = edges = true; break;
                    case "everything": parity = keys = beats = trans = statics = pans = edges = encode = true; break;
                    default: plan.unknownSets.Add(p); continue;
                }
                plan.sets.Add(p);
            }

            if (parity) plan.parity = ParityShot();
            if (keys) plan.stills.AddRange(KeyShotStills());
            if (beats) plan.stills.AddRange(BeatStills());

            // sequences: the start first, the ending last (it finishes the game)
            if (edges) plan.sequences.Add(StartSequence(opt.seqWidth, opt.seqHeight));
            if (trans) plan.sequences.AddRange(TransitionSequences(opt.seqWidth, opt.seqHeight));
            if (statics) plan.sequences.AddRange(StaticSequences(opt.seqWidth, opt.seqHeight));
            if (pans) plan.sequences.AddRange(PanSequences(opt.seqWidth, opt.seqHeight));
            if (encode) plan.sequences.AddRange(EncodeClips(opt.encode4k));
            if (edges) plan.sequences.Add(EndSequence(opt.seqWidth, opt.seqHeight));

            if (opt.only != null && opt.only.Length > 0)
            {
                plan.stills.RemoveAll(st => !Matches(st.tag, opt.only));
                plan.sequences.RemoveAll(sq => !Matches(sq.name, opt.only));
                if (plan.parity != null && !Matches(plan.parity.tag, opt.only)) plan.parity = null;
            }
            return plan;
        }

        static bool Matches(string name, string[] only)
        {
            for (int i = 0; i < only.Length; i++)
            {
                string o = only[i] != null ? only[i].Trim() : "";
                if (o.Length > 0 && name.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ key shots (14.10.2)

        public static List<StillShot> KeyShotStills()
        {
            var list = new List<StillShot>(160);
            var ks = ThreadConstants.KeyShots;
            // resolution-major order: one render-target size at a time (fewer reallocations)
            int[,] res = { { W1080, H1080 }, { W4K, H4K }, { W1610, H1610 }, { W219, H219 } };
            string[] suffix = { "", "_4k", "_1610", "_219" };
            string[] variant = { "1080", "4k", "1610", "219" };
            for (int r = 0; r < suffix.Length; r++)
            {
                for (int i = 0; i < ks.Length; i++)
                {
                    KeyShot k = ks[i];
                    if (k.wide)
                    {
                        // the table FOV is the picture (_wide); the in-game FOV(s) version carries the plain tag
                        list.Add(KeyStill(k, i, k.tag + "_wide" + suffix[r], k.fov, false, res[r, 0], res[r, 1], variant[r]));
                        list.Add(KeyStill(k, i, k.tag + suffix[r], ThreadMath.Fov(k.s), true, res[r, 0], res[r, 1], variant[r]));
                    }
                    else list.Add(KeyStill(k, i, k.tag + suffix[r], k.fov, true, res[r, 0], res[r, 1], variant[r]));
                }
                if (r < 2) list.AddRange(ExtraKeyStills(res[r, 0], res[r, 1], suffix[r], variant[r]));
            }
            return list;
        }

        static StillShot KeyStill(KeyShot k, int index, string tag, double fov, bool gameFov, int w, int h, string variant)
        {
            return new StillShot
            {
                tag = tag, set = "keyshots", baseTag = k.tag, variant = variant, width = w, height = h,
                s = k.s, x = k.x, y = k.y, z = k.z, yaw = k.yaw, pitch = k.pitch, fov = fov,
                ambient = AmbientForS(k.s), keyShotIndex = index, gameFov = gameFov, framing = true,
                lumaMin = k.lumaMin, lumaMax = k.lumaMax,
            };
        }

        /// <summary>14.10.2 extras: KF3 "also a capture at s = 305" and KF7a "planet half-faded in at s = 750".</summary>
        static List<StillShot> ExtraKeyStills(int w, int h, string suffix, string variant)
        {
            var list = new List<StillShot>(2);
            int i3 = FindKeyShot("beat03_stairs_KF3"), i7 = FindKeyShot("beat07_top_KF7a");
            if (i3 >= 0)
            {
                KeyShot k = ThreadConstants.KeyShots[i3];
                const double s305 = 305.0;
                D3 p = ThreadMath.CorridorToWorld(s305, 0, ThreadConstants.EyeHeight);
                list.Add(new StillShot
                {
                    tag = k.tag + "_s305" + suffix, set = "keyshots", baseTag = k.tag, variant = variant, width = w, height = h,
                    s = s305, x = p.x, y = p.y, z = p.z, yaw = QaDriveMath.HeadingDeg(s305), pitch = k.pitch, fov = ThreadMath.Fov(s305),
                    ambient = AmbientForS(s305), keyShotIndex = i3, gameFov = true, framing = false,
                });
            }
            if (i7 >= 0)
            {
                KeyShot k = ThreadConstants.KeyShots[i7];
                double s750 = ThreadConstants.BeatBoundaries[6];
                list.Add(new StillShot
                {
                    tag = k.tag + "_s750" + suffix, set = "keyshots", baseTag = k.tag, variant = variant, width = w, height = h,
                    s = s750, x = k.x, y = k.y, z = k.z, yaw = k.yaw, pitch = k.pitch, fov = k.fov,
                    ambient = AmbientForS(s750), keyShotIndex = i7, gameFov = true, framing = true,
                });
            }
            return list;
        }

        public static int FindKeyShot(string tag)
        {
            var ks = ThreadConstants.KeyShots;
            for (int i = 0; i < ks.Length; i++) if (ks[i].tag == tag) return i;
            return -1;
        }

        /// <summary>Deterministic ambient clock for a still: the time a walker at 1.5 m/s reaches s.</summary>
        public static double AmbientForS(double s) => s / ThreadConstants.WalkSpeed;

        // ------------------------------------------------------------------ beat stills (11.2, 14.10.2 last paragraph)

        public static List<StillShot> BeatStills()
        {
            var list = new List<StillShot>(56);
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 0; i < ThreadConstants.BeatCount; i++)
            {
                string pre = BeatPrefix(i);
                double s0 = b[i], s1 = b[i + 1], mid = 0.5 * (s0 + s1);
                list.Add(PathStill(pre + "_start", s0 + 2, 0, ThreadConstants.GazeDefaultPitch));
                list.Add(PathStill(pre + "_mid", mid, 0, ThreadConstants.GazeDefaultPitch));
                list.Add(PathStill(pre + "_end", s1 - 2, 0, ThreadConstants.GazeDefaultPitch));
                for (int f = 0; f < s_freeLookTags.Length; f++) list.Add(PathStill(pre + "_" + s_freeLookTags[f], mid, s_freeLookYaw[f], 0));
                if (i >= 4)
                {
                    list.Add(PathStill(pre + "_look_up", mid, 0, LookUpPitch));
                    list.Add(PathStill(pre + "_look_down", mid, 0, LookDownPitch));
                }
            }
            return list;
        }

        static StillShot PathStill(string tag, double s, double yawOffset, double pitch)
        {
            D3 p = ThreadMath.CorridorToWorld(s, 0, ThreadConstants.EyeHeight);
            return new StillShot
            {
                tag = tag, set = "beats", baseTag = tag, variant = "1080", width = W1080, height = H1080,
                s = s, x = p.x, y = p.y, z = p.z, yaw = QaDriveMath.HeadingDeg(s) + yawOffset, pitch = pitch, fov = ThreadMath.Fov(s),
                ambient = AmbientForS(s),
            };
        }

        static StillShot ParityShot()
        {
            KeyShot k = ThreadConstants.KeyShots[0];
            return new StillShot
            {
                tag = "parity", set = "parity", baseTag = k.tag, variant = "window",
                s = k.s, x = k.x, y = k.y, z = k.z, yaw = k.yaw, pitch = k.pitch, fov = k.fov, ambient = AmbientForS(k.s),
            };
        }

        // ------------------------------------------------------------------ sequences (11.6, 14.6, 14.10)

        public static List<SequenceSpec> TransitionSequences(int w, int h)
        {
            var list = new List<SequenceSpec>(9);
            int n = TransitionWindows.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                double a = TransitionWindows[i, 0], b = TransitionWindows[i, 1];
                double v = ThreadConstants.WalkSpeed;
                if ((b - a) / v < MinWindowSeconds)
                {
                    double c = 0.5 * (a + b), half = 0.5 * MinWindowSeconds * v;
                    a = c - half; b = c + half;
                }
                list.Add(WalkSeq("seq_b" + (i + 1) + (i + 2), "transitions", "boundary " + (i + 1) + " -> " + (i + 2) + " (14.6)", a, b, SeqFps, w, h));
            }
            list.Add(WalkSeq("seq_swap", "transitions", "T-SWAP s 280-290", SwapWindow0, SwapWindow1, SeqFps, w, h));
            list.Add(WalkSeq("seq_bankexit", "transitions", "bank exit / cable emerges", ThreadConstants.ExhaleS0, ThreadConstants.SwellWindowEnd, SeqFps, w, h));
            list.Add(WalkSeq("seq_deck", "transitions", "cloud-deck pass", DeckWindow0, DeckWindow1, SeqFps, w, h));
            return list;
        }

        public static SequenceSpec WalkSeq(string name, string set, string purpose, double s0, double s1, int fps, int w, int h,
                                           double yaw = 0, bool yawWorld = false, double pitch = ThreadConstants.GazeDefaultPitch)
        {
            double v = ThreadConstants.WalkSpeed;
            return new SequenceSpec
            {
                name = name, set = set, purpose = purpose, mode = SeqMode.Walk, fps = fps, width = w, height = h,
                s0 = s0, s1 = s1, speed = v, frames = (int)Math.Round((s1 - s0) / v * fps) + 1,
                yaw = yaw, yawWorld = yawWorld, pitch = pitch, ambient0 = AmbientForS(s0),
            };
        }

        public static List<SequenceSpec> StaticSequences(int w, int h)
        {
            var list = new List<SequenceSpec>(12);
            var b = ThreadConstants.BeatBoundaries;
            int frames = (int)Math.Round(StaticClipSeconds * SeqFps);
            for (int i = 0; i < ThreadConstants.BeatCount; i++)
                list.Add(StaticSeq("seq_static_" + BeatPrefix(i), "static camera, middle of the beat", 0.5 * (b[i] + b[i + 1]), frames, w, h));
            for (int i = 0; i < BankStaticS.Length; i++)
                list.Add(StaticSeq("seq_bank_s" + ((int)BankStaticS[i]).ToString("D3", CultureInfo.InvariantCulture), "T-BANK static camera", BankStaticS[i], frames, w, h));
            return list;
        }

        static SequenceSpec StaticSeq(string name, string purpose, double s, int frames, int w, int h)
        {
            D3 p = ThreadMath.CorridorToWorld(s, 0, ThreadConstants.EyeHeight);
            return new SequenceSpec
            {
                name = name, set = "statics", purpose = purpose, mode = SeqMode.Static, fps = SeqFps, width = w, height = h, frames = frames,
                s0 = s, s1 = s, x = p.x, y = p.y, z = p.z, yaw = QaDriveMath.HeadingDeg(s), pitch = ThreadConstants.GazeDefaultPitch, fov = ThreadMath.Fov(s),
                ambient0 = AmbientForS(s), advanceAmbient = true,
            };
        }

        public static List<SequenceSpec> PanSequences(int w, int h)
        {
            var list = new List<SequenceSpec>(9);
            var b = ThreadConstants.BeatBoundaries;
            for (int i = 4; i < ThreadConstants.BeatCount; i++)
            {
                double s = 0.5 * (b[i] + b[i + 1]);
                D3 p = ThreadMath.CorridorToWorld(s, 0, ThreadConstants.EyeHeight);
                double fov = ThreadMath.Fov(s);
                double step = QaProjection.PanStepDeg(SlowPanPx, fov, h);
                for (int dir = 0; dir < 2; dir++)
                {
                    bool up = dir == 0;
                    list.Add(new SequenceSpec
                    {
                        name = "seq_pan_" + BeatPrefix(i) + (up ? "_up" : "_down"), set = "pans", purpose = "0.1 px/frame pan, looking straight " + (up ? "up" : "down"),
                        mode = SeqMode.PanPitch, fps = SeqFps, width = w, height = h, frames = SlowPanFrames,
                        s0 = s, s1 = s, x = p.x, y = p.y, z = p.z, yaw = QaDriveMath.HeadingDeg(s), pitch = up ? LookUpPitch : LookDownPitch, fov = fov,
                        panStepDeg = up ? -step : step, ambient0 = AmbientForS(s), advanceAmbient = false,
                    });
                }
            }
            for (int m = 0; m < MoireCamAim.GetLength(0); m++)
            {
                double camZ = MoireCamAim[m, 0], aimZ = MoireCamAim[m, 1];
                D3 p = ThreadMath.CorridorToWorld(camZ, 0, ThreadConstants.EyeHeight);
                D3 aim = ThreadMath.PathPoint(aimZ);
                double pitch = Math.Atan2(aim.y - p.y, aimZ - camZ) / ThreadMath.Deg;
                double fov = ThreadMath.Fov(camZ);
                list.Add(new SequenceSpec
                {
                    name = "seq_moire_" + MoireNames[m], set = "pans", purpose = "1 px/frame pan over the stairs (T moire)",
                    mode = SeqMode.PanYaw, fps = SeqFps, width = w, height = h, frames = MoirePanFrames,
                    s0 = camZ, s1 = camZ, x = p.x, y = p.y, z = p.z, yaw = QaDriveMath.HeadingDeg(camZ), pitch = pitch, fov = fov,
                    panStepDeg = QaProjection.PanStepDeg(MoirePanPx, fov, h), ambient0 = AmbientForS(camZ), advanceAmbient = false,
                });
            }
            return list;
        }

        public static SequenceSpec StartSequence(int w, int h)
        {
            return new SequenceSpec
            {
                name = "seq_start", set = "edges", purpose = "first 60 frames: start fade (defect 22)", mode = SeqMode.RealStart,
                fps = SeqFps, width = w, height = h, frames = EdgeFrames, s0 = 0, s1 = 0, pitch = ThreadConstants.GazeDefaultPitch,
            };
        }

        public static SequenceSpec EndSequence(int w, int h)
        {
            double s0 = ThreadConstants.BeaconTriggerS - EndLeadIn;
            double ending = ThreadConstants.EndLineOut1;   // total ending 14 s (14.11)
            int maxFrames = (int)Math.Ceiling((EndLeadIn / ThreadConstants.WalkSpeed + 2.0 + ending + EndTail) * SeqFps);
            return new SequenceSpec
            {
                name = "seq_end", set = "edges", purpose = "ending: trigger, stop ramp, fade, last frames (defect 22)", mode = SeqMode.RealEnd,
                fps = SeqFps, width = w, height = h, frames = maxFrames, s0 = s0, s1 = ThreadConstants.PathLength,
                pitch = ThreadConstants.GazeDefaultPitch,
            };
        }

        /// <summary>Seconds of frames kept after the beacon trigger in seq_end (the 14 s ending plus a margin).</summary>
        public static double EndSecondsAfterTrigger => ThreadConstants.EndLineOut1 + EndTail;

        // ------------------------------------------------------------------ encode clips (11.12)

        public static List<SequenceSpec> EncodeClips(bool also4k)
        {
            var list = new List<SequenceSpec>(24);
            AddEncode(list, also4k, "enc_beat01_sky", "salt-flat sky above the bank (KF1 view)", 0.0, EncodeSeconds, KeyYaw("beat01_salt_KF1", out double p1), false, p1);
            AddEncode(list, also4k, "enc_beat02_fog", "fog depth down the avenue", 110.0, EncodeSeconds, 0, false, ThreadConstants.GazeDefaultPitch);
            AddEncode(list, also4k, "enc_beat03_bank", "bright bank (brightest gradient of beat 3)", 250.0, EncodeSeconds, 0, false, KeyPitch("beat03_stairs_KF3"));
            AddEncode(list, also4k, "enc_beat04_shade", "cable shade (KF4a view)", ThreadConstants.ExhaleS0, EncodeSeconds, KeyYaw("beat04_tower_KF4a", out double p4), true, p4);
            AddEncode(list, also4k, "enc_beat05_sky", "dusk sky away from the cable", 500.0, EncodeSeconds, -FreeLookYaw, false, 15.0);
            AddEncode(list, also4k, "enc_beat06_sea", "cloud sea and shadow band (KF6a view)", 615.0, EncodeSeconds, KeyYaw("beat06_clouds_KF6a", out double p6), true, p6);
            AddEncode(list, also4k, "enc_beat06_sky", "night sky gradient", 700.0, EncodeSeconds, -FreeLookYaw, false, 35.0);
            AddEncode(list, also4k, "enc_beat07_limb", "planet limb and Earth limb (KF7a view)", 790.0, EncodeSeconds, KeyYaw("beat07_top_KF7a", out double p7), true, p7);
            AddEncode(list, also4k, "enc_beat07_dark", "darkest gradient: space above the summit", 842.0, EncodeSeconds, 0, false, 40.0);
            AddEncode(list, also4k, "enc_trans_bank", "30 s through the bank core", ThreadConstants.NearCableOnS, EncodeTransitionSeconds, 0, false, KeyPitch("beat03_stairs_KF3"));
            AddEncode(list, also4k, "enc_trans_deck", "30 s through the cloud-deck pass", 572.0, EncodeTransitionSeconds, 0, false, ThreadConstants.GazeDefaultPitch);
            return list;
        }

        static void AddEncode(List<SequenceSpec> list, bool also4k, string name, string purpose, double s0, double seconds, double yaw, bool yawWorld, double pitch)
        {
            double s1 = s0 + seconds * ThreadConstants.WalkSpeed;
            var a = WalkSeq(name, "encode", purpose, s0, s1, EncodeFps, W1080, H1080, yaw, yawWorld, pitch);
            a.frames = (int)Math.Round(seconds * EncodeFps);
            list.Add(a);
            if (also4k)
            {
                var b = WalkSeq(name + "_4k", "encode", purpose + " (4K)", s0, s1, EncodeFps, W4K, H4K, yaw, yawWorld, pitch);
                b.frames = a.frames;
                list.Add(b);
            }
        }

        static double KeyYaw(string tag, out double pitch)
        {
            int i = FindKeyShot(tag);
            if (i < 0) { pitch = ThreadConstants.GazeDefaultPitch; return 0; }
            pitch = ThreadConstants.KeyShots[i].pitch;
            return ThreadConstants.KeyShots[i].yaw;
        }

        static double KeyPitch(string tag)
        {
            int i = FindKeyShot(tag);
            return i >= 0 ? ThreadConstants.KeyShots[i].pitch : ThreadConstants.GazeDefaultPitch;
        }
    }

    /// <summary>Pure heading helper (degrees) shared by the plan and the bots.</summary>
    public static class QaDriveMath
    {
        public static double HeadingDeg(double s) => ThreadMath.Heading(s) / ThreadMath.Deg;
    }
}
