using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Capture mode (-capture DIR, Part 11.2, 14.10): stills, key-shot aspect variants, composition lint, special tests (wrap
    /// seams, tier crossovers, backdrop parallax), the window parity test and frame sequences (QaSequences.cs, seq:NAME).
    ///
    /// Per still: lift frozen in the shot's state (top dock; riding at the shot's ride seconds with the player aboard; or
    /// parked at the bottom after the ride), player teleported (PlayerController.Teleport, hold driver), scene clock set and
    /// frozen, capture pose first far away for one frame (every detail tier starts from the far state, as when approaching,
    /// so a shot does not depend on the previous one) and then exact (CameraRig.SetCapturePose), every beat's
    /// PrepareCapture(t), ProgressDirector.SnapAll and BeatDirector.SnapBeat, 3 settle frames (-capture-settle), then the render
    /// in LateUpdate through <see cref="QaRenderer"/> and a PNG. report.json (format of docs/QA_TOOLS.md) is rewritten after
    /// every shot. Exit code 0 when every requested image was written, 1 otherwise; lint and test verdicts are in the report.
    /// </summary>
    public sealed partial class QaCaptureRunner : IDisposable
    {
        public static readonly Vector3 FarPoint = new Vector3(0f, 9000f, 0f);
        public const float ParityLimitLevels = 1f;

        enum Req { None, Still, Parity, SeqFrame }

        struct Snapshot
        {
            public double SceneT, RealT;
            public float D, E, RideS, RideU, Fov, LiftOffsetMm;
            public int Beat;
            public LiftPhase Lift;
            public Vector3 CamPos, HeadOffset;
            public Quaternion CamRot;
            public double Luma;
            /// <summary>Warm pixels of the frame below SeqDef.WarmFromRow (-1 when not counted) and their share of the frame in %.</summary>
            public long WarmPx;
            public double WarmPct;
        }

        /// <summary>Verdicts of the cue sequences of this run (R2-05, R2-20), added to report.json tests_summary.</summary>
        readonly List<KeyValuePair<string, bool?>> _cueTests = new List<KeyValuePair<string, bool?>>(4);
        readonly List<string> _cueTestJson = new List<string>(4);

        sealed class ShotRecord
        {
            public ShotSpec Spec;
            public bool Ok;
            public string Error;
            public double MeanLuma = double.NaN;
            public string Readback;
            public float RenderMs;
            public long PngBytes;
            public float D, E;
            public int GameBeat;
            public string LintJson;
            public string LintStatus;
            /// <summary>Names of the waived known-conflict subjects (CompositionLint.KnownConflictSuffix), or null.</summary>
            public List<string> Waived;
            public string AnalysisJson;
            public bool? AnalysisPass;
            public string DepthStatus;
            public float DepthTop, DepthBottom;
        }

        bool _depthWarned;

        readonly QaHarness _h;
        readonly QaRenderer _r = new QaRenderer();
        readonly List<ShotRecord> _records = new List<ShotRecord>(160);
        readonly Dictionary<string, byte[]> _pairBase = new Dictionary<string, byte[]>();
        readonly List<KeyShotSubject> _subjects = new List<KeyShotSubject>(16);
        readonly List<CompositionLint.SubjectResult> _lint = new List<CompositionLint.SubjectResult>(16);
        string _dir, _selector, _parityJson;
        bool _lintOnly;
        int _settle = 3;
        int _failures, _hookErrors;
        Req _req;
        int _reqW, _reqH;
        bool _reqDone, _reqOk;
        string _reqError;
        Snapshot _snap;

        public QaCaptureRunner(QaHarness h)
        {
            _h = h;
            string flip = CliArgs.String(QaHarness.FlipFlag, null);
            _r.Flip = flip != null && (flip.Equals("on", StringComparison.OrdinalIgnoreCase) || flip == "1" || flip.Equals("true", StringComparison.OrdinalIgnoreCase));
            _settle = Mathf.Clamp(CliArgs.Int(QaHarness.SettleFlag, 3), 1, 120);
            _lintOnly = CliArgs.Has(QaHarness.LintOnlyFlag);
        }

        public void Dispose() => _r.Dispose();

        // ---------------------------------------------------------------- LateUpdate (after CameraRig +100 and the draws +200)

        public void LateTick()
        {
            if (_req == Req.None) return;
            Camera cam = GameState.MainCamera;
            _reqOk = _r.Capture(cam, _reqW, _reqH, out _reqError);
            _snap.SceneT = SceneClock.Time;
            _snap.RealT = SceneClock.RealTime;
            _snap.D = GameState.D;
            _snap.E = GameState.E;
            _snap.Beat = GameState.Beat;
            _snap.Lift = GameState.Lift;
            _snap.RideS = GameState.RideSeconds;
            _snap.RideU = GameState.RideU;
            _snap.CamPos = GameState.CameraPos;
            _snap.CamRot = GameState.CameraRot;
            var rig = CameraRig.Instance;
            _snap.Fov = rig != null ? rig.CurrentFov : (cam != null ? cam.fieldOfView : 0f);
            _snap.HeadOffset = rig != null ? rig.LastHeadOffset : Vector3.zero;
            var lift = LiftController.Instance;
            _snap.LiftOffsetMm = lift != null ? lift.MaxOffsetError * 1000f : -1f;
            _snap.Luma = _r.MeanLuma;
            _reqDone = true;
            _req = Req.None;
        }

        void Request(Req kind, int w, int h)
        {
            _req = kind;
            _reqW = w;
            _reqH = h;
            _reqDone = false;
            _reqOk = false;
            _reqError = null;
        }

        // ---------------------------------------------------------------- run

        public IEnumerator Run()
        {
            string capDir = CliArgs.CaptureDir;
            _dir = QaPaths.Resolve(string.IsNullOrEmpty(capDir) ? Path.Combine("QA", "captures", _h.RunId) : capDir);
            QaPaths.EnsureDir(_dir);
            _selector = CliArgs.Shots ?? "all";
            _h.AddFinalizer(Finish);
            yield return _h.WaitForLoad(true);
            Time.captureDeltaTime = 1f / 60f;
            _h.InstallHold(0f, 0f);
            Debug.Log("[ABYSS] QA capture: selector '" + _selector + "' -> " + _dir + (_lintOnly ? " (lint only)" : "")
                      + ", MSAA " + QaRenderer.AssetMsaa + ", flip " + _r.Flip);
            if (QaRenderer.AssetMsaa != 4)
                Debug.LogWarning("[ABYSS] QA capture: the URP asset uses MSAA " + QaRenderer.AssetMsaa + "x, not the 4x of Part 8.1; captures follow the asset so they match the screen");
            Camera mc = GameState.MainCamera;
            if (mc != null && !mc.allowMSAA) Debug.LogWarning("[ABYSS] QA capture: the main camera has allowMSAA off");

            if (CliArgs.Has(QaHarness.ParityFlag)) yield return Parity();

            if (_selector.StartsWith("seq:", StringComparison.OrdinalIgnoreCase))
            {
                yield return RunSequences(_selector.Substring(4));
            }
            else if (CliArgs.Has(CliArgs.ShotsFlag) || !CliArgs.Has(QaHarness.ParityFlag))
            {
                bool haveRes = CliArgs.Res(out int rw, out int rh);
                // R2-36 (e): the title card first, while the run state is the fresh state after the load (as the player sees it)
                bool cueRun = WantsCueSequences(_selector);
                if (cueRun) yield return CaptureTitle();
                List<ShotSpec> shots = KeyShots.Build(_selector, haveRes, rw, rh);
                if (shots.Count == 0) Debug.LogWarning("[ABYSS] QA capture: selector '" + _selector + "' matches no shot");
                for (int i = 0; i < shots.Count; i++)
                {
                    yield return CaptureShot(shots[i]);
                    WriteReport(false);
                }
                // R2-36 (e): the "cues" selector (and "all") also records the cue sequences (falling bolt, honeycomb ignition,
                // ending) into the same folder and, last, the title card at 2 s and 4 s, so one f-run cue capture holds every cue
                if (cueRun)
                {
                    Restore();
                    yield return RunSequences("cues");
                    Restore();
                    WriteReport(false);
                }
            }
            Restore();
            _h.RequestQuit(0.1f);
        }

        /// <summary>True when the selector has the token "cues" or "all" (the cue stills plus the cue sequences and the title).</summary>
        public static bool WantsCueSequences(string selector)
        {
            if (string.IsNullOrEmpty(selector)) return true;   // empty = all
            string[] tokens = selector.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                if (t.Equals("cues", StringComparison.OrdinalIgnoreCase) || t.Equals("all", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Title-card window stills of the cue run (14.11; R1-40 / R2-36 e): DIR/cue_title/title_2s.png and title_4s.png.</summary>
        public static readonly float[] TitleMarks = { 2f, 4f };

        /// <summary>
        /// The title card as the player sees it (window capture: post, dither and overlay UI): the player at W0 looking down the
        /// gallery, the clock at 0 and running, the 14.11 title timeline restarted (capture mode skips it), window grabs at title
        /// seconds 2 and 4, then the title switched off again (picture on). Any ending state of a preceding cue_ending is cleared
        /// first, or the ending fade would keep the picture black.
        /// </summary>
        IEnumerator CaptureTitle()
        {
            FadeController fc = FadeController.Instance;
            string dir = QaPaths.EnsureDir(Path.Combine(_dir, "cue_title"));
            var j = new QaJson();
            j.BeginObject();
            j.Prop("name", "cue_title");
            j.Prop("note", "14.11 title card at title seconds 2 and 4 (window captures: post, dither and overlay UI)");
            if (fc == null)
            {
                j.Prop("error", "no FadeController");
                j.Prop("ok", false);
                j.EndObject();
                j.Save(Path.Combine(dir, "title.json"));
                _failures++;
                yield break;
            }
            GameState.GoalReached = false;
            GameState.GoalRealTime = -1;
            GameState.Ended = false;
            var pc = PlayerController.Instance;
            var rig = CameraRig.Instance;
            if (rig != null) rig.ClearCapturePose();
            LiftFor(-1f, false, true);
            _h.InstallHold(0f, 0f);
            if (pc != null) pc.TeleportToD(0f, false);
            SetClock(0.0, false);
            PrepareBeats(0.0);
            Snap();
            yield return null;
            yield return null;
            fc.BeginTitle(false);
            double real0 = SceneClock.RealTime;
            yield return null;   // let FadeController.Update recompute TitleTime from the new start
            yield return null;
            j.Arr("frames");
            int written = 0;
            for (int m = 0; m < TitleMarks.Length; m++)
            {
                int guard = 0;
                // TitleTime follows SceneClock.RealTime (capture delta per frame); the frame guard covers 30 s at 60 fps
                while (fc.TitleTime < TitleMarks[m] && guard < 1800)
                {
                    yield return null;
                    guard++;
                }
                yield return new WaitForEndOfFrame();
                string file = "title_" + TitleMarks[m].ToString("0", SInv) + "s.png";
                Texture2D t = ScreenCapture.CaptureScreenshotAsTexture();
                j.BeginObject();
                j.Prop("file", file);
                j.Prop("title_time", fc.TitleTime);
                j.Prop("real_time_since_start", SceneClock.RealTime - real0);
                j.Prop("frames_waited", guard);
                j.Prop("picture_fade", GameState.PictureFade);
                if (t != null)
                {
                    try
                    {
                        File.WriteAllBytes(Path.Combine(dir, file), t.EncodeToPNG());
                        written++;
                        j.Prop("width", t.width);
                        j.Prop("height", t.height);
                    }
                    catch (Exception ex) { j.Prop("error", ex.Message); }
                    finally { UnityEngine.Object.Destroy(t); }
                }
                else j.Prop("error", "window capture failed");
                j.EndObject();
            }
            j.EndArray();
            fc.BeginTitle(true);   // capture mode again: picture on, no title
            bool ok = written == TitleMarks.Length;
            if (!ok) _failures++;
            j.Prop("ok", ok);
            j.EndObject();
            j.Save(Path.Combine(dir, "title.json"));
            yield return null;
        }

        void Restore()
        {
            var rig = CameraRig.Instance;
            if (rig != null) rig.ClearCapturePose();
            QaHooks.TierOverride = -1f;
            SceneClock.Frozen = CliArgs.FreezeClock;
        }

        int Finish(bool aborted)
        {
            WriteReport(!aborted);
            int code = (_failures > 0 || aborted) ? 1 : 0;
            Debug.Log("[ABYSS] QA capture done: " + _records.Count + " shots, " + _failures + " failures -> " + Path.Combine(_dir ?? "", "report.json"));
            return code;
        }

        // ---------------------------------------------------------------- preparation shared with the sequences

        static void SetClock(double t, bool frozen)
        {
            SceneClock.Time = t;
            SceneClock.Frozen = frozen;
            ClockDriver.Publish();
        }

        void PrepareBeats(double t)
        {
            BeatDirector bd = BeatDirector.Instance;
            BeatBase[] beats = bd != null ? bd.Beats : null;
            if (beats == null) return;
            for (int i = 0; i < beats.Length; i++)
            {
                if (beats[i] == null) continue;
                try { beats[i].PrepareCapture(t); }
                catch (Exception ex)
                {
                    _hookErrors++;
                    Debug.LogWarning("[ABYSS] QA: beat " + (i + 1) + " PrepareCapture threw " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        static void Snap()
        {
            var pd = ProgressDirector.Instance;
            if (pd != null && pd.Active) pd.SnapAll();
            var bd = BeatDirector.Instance;
            if (bd != null && bd.Running) bd.SnapBeat();
        }

        /// <summary>Lift in the state of a shot: riding (frozen) at ride seconds, docked at the top, or parked after the ride.</summary>
        static void LiftFor(float rideSeconds, bool afterRide, bool frozen)
        {
            var lift = LiftController.Instance;
            if (lift == null) return;
            lift.Frozen = true;
            if (rideSeconds >= 0f || !afterRide) lift.SetRideSecondsForCapture(-1.0);
            else lift.SetLeft();
            if (!frozen && rideSeconds < 0f) lift.Frozen = false;
        }

        /// <summary>
        /// Board the player at the top-dock deck centre and move the platform to ride seconds t: two frames (the
        /// PlayerController becomes kinematic on the docked deck, then rides with the platform).
        /// </summary>
        IEnumerator BoardAt(float rideSeconds, float yaw, float pitch) => BoardAt(rideSeconds, yaw, pitch, Vector2.zero);

        /// <summary>Board at the top dock at a deck position (platform-local x, z; the deck is axis-aligned), then set the ride second.</summary>
        IEnumerator BoardAt(float rideSeconds, float yaw, float pitch, Vector2 deck)
        {
            var pc = PlayerController.Instance;
            var lift = LiftController.Instance;
            LiftFor(-1f, false, true);
            _h.InstallHold(yaw, pitch);
            if (pc != null) pc.Teleport(AbyssConst.LIFT_TOP + new Vector3(deck.x, 0f, deck.y), yaw, Mathf.Clamp(pitch, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT));
            yield return null;
            if (pc != null && !pc.OnLift) Debug.LogWarning("[ABYSS] QA: the player did not board the lift deck (kinematic mode)");
            if (lift != null) lift.SetRideSecondsForCapture(rideSeconds);
            yield return null;
        }

        /// <summary>Everything before the render of a still (see the class summary).</summary>
        IEnumerator Prepare(ShotSpec s)
        {
            var pc = PlayerController.Instance;
            var rig = CameraRig.Instance;
            QaHooks.TierOverride = s.TierOverride;
            SetClock(s.Clock, true);
            if (rig != null) rig.SetCapturePose(FarPoint, s.Rot, s.Fov);
            if (s.RideSeconds >= 0f)
            {
                yield return BoardAt(s.RideSeconds, s.Yaw, s.Pitch);
            }
            else
            {
                LiftFor(-1f, s.AfterRide, true);
                _h.InstallHold(s.Yaw, s.Pitch);
                if (pc != null) pc.Teleport(s.Feet, s.Yaw, Mathf.Clamp(s.Pitch, -AbyssConst.PITCH_LIMIT, AbyssConst.PITCH_LIMIT));
                yield return null;
            }
            SetClock(s.Clock, true);
            PrepareBeats(s.Clock);
            if (rig != null) rig.SetCapturePose(s.Eye, s.Rot, s.Fov);
            Snap();
            for (int i = 0; i < _settle; i++) yield return null;
            SetClock(s.Clock, true);
            PrepareBeats(s.Clock);
            Snap();
        }

        // ---------------------------------------------------------------- stills

        IEnumerator CaptureShot(ShotSpec s)
        {
            var rec = new ShotRecord { Spec = s };
            _records.Add(rec);
            GameObject wall = null;
            Mesh wallMesh = null;
            bool rowPlane = false;
            if (s.Special != null && s.Special.StartsWith("seam:", StringComparison.Ordinal))
            {
                KeyShots.SeamPose(s.Special.Substring(5), out Vector3 c, out Vector3 size, out _, out _, out rowPlane);
                wall = MakeWall(c, size, out wallMesh);
            }

            var pd = ProgressDirector.Instance;
            bool postWas = pd != null && pd.PostProcessingEnabled;
            if (s.NoPost && pd != null) pd.PostProcessingEnabled = false;

            yield return Prepare(s);

            rec.D = GameState.D;
            rec.E = GameState.E;
            rec.GameBeat = GameState.Beat;
            Camera cam = GameState.MainCamera;
            float near = cam != null ? cam.nearClipPlane : AbyssConst.CAMERA_NEAR;

            if (!_lintOnly)
            {
                // render first (with the depth grab for key shots), so the lint can depth-test its subjects (R1-29)
                _r.WantDepth = s.KeyIndex >= 0 || s.Special == "cue:lookback";   // R2-28: the look-back test masks the far wall by depth
                Request(Req.Still, s.Width, s.Height);
                while (!_reqDone) yield return null;
                _r.WantDepth = false;
                rec.Ok = _reqOk;
                rec.Error = _reqError;
                rec.Readback = _r.LastReadback;
                rec.RenderMs = _r.LastMs;
                if (_reqOk)
                {
                    rec.MeanLuma = _r.MeanLuma;
                    try { rec.PngBytes = _r.WritePng(Path.Combine(_dir, s.File)); }
                    catch (Exception ex) { rec.Ok = false; rec.Error = "png write failed: " + ex.Message; }
                    Analyse(rec, rowPlane);
                }
                if (!rec.Ok)
                {
                    _failures++;
                    Debug.LogWarning("[ABYSS] QA capture " + s.Tag + " failed: " + rec.Error);
                }
            }
            else rec.Ok = true;

            if (s.KeyIndex >= 0)
            {
                float[] depth = !_lintOnly && _reqOk ? _r.Depth : null;
                if (!_lintOnly && depth == null && !_depthWarned)
                {
                    _depthWarned = true;
                    Debug.LogWarning("[ABYSS] QA lint: no depth buffer for the occlusion test (" + _r.DepthStatus + "); visibility is reported as unknown");
                }
                CompositionLint.Run(s.BaseTag, s.Eye, s.Rot, s.Fov, s.AspectRatio, near, _subjects, _lint, out int he, depth, _r.Width, _r.Height);
                _hookErrors += he;
                var lj = new QaJson();
                lj.BeginObject();
                CompositionLint.Write(lj, _lint, s.AspectRatio, he, out string status, depth != null);
                lj.EndObject();
                rec.LintJson = ExtractInner(lj.ToString());
                rec.LintStatus = status;
                rec.DepthStatus = _lintOnly ? "lint only" : _r.DepthStatus;
                rec.DepthTop = _r.DepthTopMedian;
                rec.DepthBottom = _r.DepthBottomMedian;
                for (int i = 0; i < _lint.Count; i++)
                {
                    if (!_lint[i].Waived) continue;
                    if (rec.Waived == null) rec.Waived = new List<string>(2);
                    rec.Waived.Add(_lint[i].Name);
                }
            }

            if (s.NoPost && pd != null) pd.PostProcessingEnabled = postWas;
            QaHooks.TierOverride = -1f;
            if (wall != null) UnityEngine.Object.Destroy(wall);
            if (wallMesh != null) UnityEngine.Object.Destroy(wallMesh);
        }

        static GameObject MakeWall(Vector3 centre, Vector3 size, out Mesh mesh)
        {
            var mb = new MeshBuilder();
            mb.Box(Vector3.zero, size);
            mesh = mb.ToMesh("QA_SeamWall");
            var go = new GameObject("QA_SeamWall");
            go.transform.position = centre;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            MaterialLibrary lib = MaterialLibrary.Instance;
            if (lib != null) mr.sharedMaterial = lib[MatId.ConcreteBoard];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>"{ "lint": {...} }" -> "{...}" (the inner object of a single-key wrapper).</summary>
        static string ExtractInner(string json)
        {
            int c = json.IndexOf(':');
            int e = json.LastIndexOf('}');
            if (c < 0 || e <= c) return "null";
            return json.Substring(c + 1, e - c - 1).Trim();
        }

        // ---------------------------------------------------------------- C# analyses of the special tests

        void Analyse(ShotRecord rec, bool rowPlane)
        {
            ShotSpec s = rec.Spec;
            if (s.Special == null) return;
            int w = _r.Width, h = _r.Height;
            byte[] rgb = _r.Rgb;
            var j = new QaJson();
            j.BeginObject();
            if (s.Special.StartsWith("seam:", StringComparison.Ordinal))
            {
                SeamMetric(rgb, w, h, rowPlane, out double jump, out double median, out double p90, out double excess);
                bool pass = excess <= 0.02;
                j.Prop("test", "14.10a wrap seam");
                j.Prop("plane", s.Special.Substring(5));
                j.Prop("axis", rowPlane ? "row" : "column");
                j.Prop("jump", jump);
                j.Prop("neighbour_median", median);
                j.Prop("neighbour_p90", p90);
                j.Prop("excess", excess);
                j.Prop("limit", 0.02);
                j.Prop("pass", pass);
                rec.AnalysisPass = pass;
            }
            else if (s.PairOf == null)
            {
                _pairBase[s.Tag] = _r.CopyRgb();
                j.Prop("role", "base");
            }
            else if (_pairBase.TryGetValue(s.PairOf, out byte[] a) && a != null && a.Length == rgb.Length)
            {
                j.Prop("pair_of", s.PairOf);
                if (s.Special.StartsWith("tier:", StringComparison.Ordinal))
                {
                    bool facade = s.Special == "tier:facade";
                    float x0 = facade ? 0.25f : 0.20f, y0 = facade ? 0.25f : 0.42f, x1 = facade ? 0.75f : 0.80f, y1 = facade ? 0.75f : 0.58f;
                    double la = QaRenderer.MeanLumaRect(a, w, h, x0, y0, x1, y1);
                    double lb = QaRenderer.MeanLumaRect(rgb, w, h, x0, y0, x1, y1);
                    double diff = Math.Abs(la - lb);
                    double limit = facade ? 0.015 : 0.02;
                    bool identical = Identical(a, rgb);
                    j.Prop("test", facade ? "14.10b facade tier crossover" : "14.10c honeycomb tier 1 vs 2");
                    j.PropRect("region", x0, y0, x1, y1);
                    j.Prop("mean_luma_near", la);
                    j.Prop("mean_luma_far", lb);
                    j.Prop("difference", diff);
                    j.Prop("limit", limit);
                    j.Prop("identical_images", identical);
                    j.Prop("status", identical ? "inconclusive (QaHooks.TierOverride not wired?)" : (diff <= limit ? "pass" : "fail"));
                    j.Prop("pass", diff <= limit && !identical);
                    rec.AnalysisPass = identical ? (bool?)null : diff <= limit;
                }
                else if (s.Special == "parallax:move10")
                {
                    float v = KeyShots.HorizonRow(s.Pitch, s.Fov);
                    float yTop = 0.03f, yBot = Mathf.Clamp(v - 0.05f, 0.05f, 0.95f);
                    double diff = QaRenderer.MeanAbsDiffRect(a, rgb, w, h, 0f, yTop, 1f, yBot);
                    j.Prop("test", "Part 10.30 backdrop parallax: move 10 m");
                    j.PropRect("sky_band", 0f, yTop, 1f, yBot);
                    j.Prop("mean_abs_diff_levels", diff);
                    j.Prop("limit_levels", 2.0);
                    j.Prop("pass", diff <= 2.0);
                    rec.AnalysisPass = diff <= 2.0;
                }
                else if (s.Special == "cue:freight")
                {
                    // 14.5.5 freight lights: the only thing that moves on the facade in 1 s with the platform frozen
                    int blobs = ChangedBlobs(a, rgb, w, h, FreightDiffLevels, out int changed);
                    // R2-36 (j): analysis images go to QA/analysis/<run>/, never into the capture folder (only listed shots live there)
                    string diffFile = Path.Combine(AnalysisDir, "test_" + s.Tag.Replace("_t31", "") + "_diff.png");
                    try { WriteDiffPng(a, rgb, w, h, diffFile); }
                    catch (Exception ex) { Debug.LogWarning("[ABYSS] QA: diff image not written: " + ex.Message); }
                    j.Prop("test", "R1-40 freight lights climbing face 210 (ride 30 s, 1 s apart, platform frozen)");
                    j.Prop("changed_pixels", changed);
                    j.Prop("moving_blobs", blobs);
                    j.Prop("diff_threshold_levels", FreightDiffLevels);
                    j.Prop("diff_image", diffFile.Replace('\\', '/'));
                    j.Prop("limit", "at least 1 moving light");
                    j.Prop("pass", blobs >= 1);
                    rec.AnalysisPass = blobs >= 1;
                }
                else if (s.Special == "cue:lookback")
                {
                    // 14.5.6 far-wall lighthouse patches: they turn with the fan (12-fold, 3.33 s period). R2-28: judged on the
                    // far wall only (eye depth over 500 m), so the blade shadows sweeping the near portal frames cannot pass it,
                    // and the patches must lift the wall by the 14.5.6 +0.08 (p95 of the per-pixel luma change between phases)
                    PatchRect(s, out float px0, out float py0, out float px1, out float py1);
                    double diff = QaRenderer.MeanAbsDiffRect(a, rgb, w, h, px0, py0, px1, py1);
                    float[] depth = _r.Depth;
                    FarWallChange(a, rgb, depth, w, h, px0, py0, px1, py1, PatchMinDepth, out int farPx, out double farDiff, out double lift95, out double lift99);
                    bool depthOk = depth != null && farPx >= 64;
                    bool pass = depthOk && farDiff >= PatchMinDiffLevels && lift95 >= PatchMinLift;
                    j.Prop("test", "R1-40 / R2-28 far-wall lighthouse patches turning (quarter-period phase steps), far wall only");
                    j.PropRect("patch_region", px0, py0, px1, py1);
                    j.Prop("mean_abs_diff_levels_unmasked", diff);
                    j.Prop("depth", depth != null ? "ok" : _r.DepthStatus);
                    j.Prop("far_wall_min_depth_m", PatchMinDepth);
                    j.Prop("far_wall_pixels", farPx);
                    j.Prop("mean_abs_diff_levels", farDiff);
                    j.Prop("limit_levels", PatchMinDiffLevels);
                    j.Prop("patch_lift_p95", lift95);
                    j.Prop("patch_lift_p99", lift99);
                    j.Prop("lift_limit", PatchMinLift);
                    j.Prop("pass", pass);
                    rec.AnalysisPass = depthOk ? pass : (bool?)null;
                    if (!depthOk) j.Prop("status", "inconclusive (no depth buffer or no far-wall pixels in the patch region)");
                }
                else if (s.Special == "parallax:yaw2")
                {
                    float v = KeyShots.HorizonRow(s.Pitch, s.Fov);
                    float tanH = Mathf.Tan(0.5f * s.Fov * Mathf.Deg2Rad) * s.AspectRatio;
                    int dx = Mathf.RoundToInt(0.5f * w * Mathf.Tan(2f * Mathf.Deg2Rad) / tanH);
                    float yTop = Mathf.Clamp(v - 0.15f, 0.02f, 0.9f), yBot = Mathf.Clamp(v - 0.03f, 0.05f, 0.95f);
                    double comp = QaRenderer.MeanAbsDiffRect(a, rgb, w, h, 0.3f, yTop, 0.7f, yBot, dx);
                    double raw = QaRenderer.MeanAbsDiffRect(a, rgb, w, h, 0.3f, yTop, 0.7f, yBot, 0);
                    j.Prop("test", "Part 10.30 backdrop parallax: turn 2 degrees");
                    j.Prop("shift_px", dx);
                    j.PropRect("band", 0.3f, yTop, 0.7f, yBot);
                    j.Prop("compensated_levels", comp);
                    j.Prop("uncompensated_levels", raw);
                    j.Prop("limit_levels", 2.0);
                    j.Prop("pass", comp <= 2.0);
                    rec.AnalysisPass = comp <= 2.0;
                }
            }
            else
            {
                j.Prop("error", "base shot " + s.PairOf + " missing (select both shots of the pair)");
            }
            j.EndObject();
            rec.AnalysisJson = j.ToString();
        }

        /// <summary>Folder for analysis images of this run (QA/analysis/&lt;run&gt;), outside the capture folder (R2-36 j).</summary>
        string AnalysisDir => Path.Combine(QaPaths.OutDir, "analysis", Path.GetFileName((_dir ?? "run").TrimEnd('\\', '/')));

        /// <summary>Freight cue: a pixel changed by at least this many levels (any channel) between the two stills.</summary>
        public const int FreightDiffLevels = 12;
        /// <summary>Look-back cue: mean change (levels) of the far-wall pixels of the patch region between fan phases that shows the patches turn.</summary>
        public const double PatchMinDiffLevels = 0.3;
        /// <summary>R2-28: only pixels at least this far (eye depth, m) belong to the far wall (z 760, about 600 m from the gantry eye).</summary>
        public const float PatchMinDepth = 500f;
        /// <summary>R2-28 / 14.5.6: the patches lift the wall by +0.08 luma (judged on the p95 of the per-pixel change between phases).</summary>
        public const double PatchMinLift = 0.08;

        static readonly List<float> s_lift = new List<float>(1 << 16);

        /// <summary>
        /// Change between two captures over the far-wall pixels (eye depth at least <paramref name="minDepth"/>) of a frame rectangle
        /// (top-left fractions): pixel count, mean absolute difference (levels, all channels) and the 95th / 99th percentile of the
        /// absolute luma change (0-1). Depth and pixels are in texture order (row 0 = bottom).
        /// </summary>
        static void FarWallChange(byte[] a, byte[] b, float[] depth, int w, int h, float fx0, float fy0, float fx1, float fy1, float minDepth,
                                  out int count, out double meanLevels, out double p95, out double p99)
        {
            count = 0; meanLevels = 0; p95 = 0; p99 = 0;
            if (depth == null || depth.Length < w * h) return;
            int x0 = Mathf.Clamp(Mathf.RoundToInt(fx0 * w), 0, w), x1 = Mathf.Clamp(Mathf.RoundToInt(fx1 * w), 0, w);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(fy0 * h), 0, h), y1 = Mathf.Clamp(Mathf.RoundToInt(fy1 * h), 0, h);
            s_lift.Clear();
            double s = 0;
            for (int y = y0; y < y1; y++)
            {
                int row = (h - 1 - y) * w;
                for (int x = x0; x < x1; x++)
                {
                    if (depth[row + x] < minDepth) continue;
                    int i = (row + x) * 3;
                    s += Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]);
                    float la = 0.2126f * a[i] + 0.7152f * a[i + 1] + 0.0722f * a[i + 2];
                    float lb = 0.2126f * b[i] + 0.7152f * b[i + 1] + 0.0722f * b[i + 2];
                    s_lift.Add(Mathf.Abs(la - lb) / 255f);
                    count++;
                }
            }
            if (count == 0) return;
            meanLevels = s / (3.0 * count);
            s_lift.Sort();
            p95 = s_lift[Mathf.Min(count - 1, (int)(0.95 * count))];
            p99 = s_lift[Mathf.Min(count - 1, (int)(0.99 * count))];
        }

        /// <summary>Frame rectangle of the 12 far-wall patches (R 130 wedges around FarWallPatchCentre, stretched 1.05 vertically).</summary>
        static void PatchRect(ShotSpec s, out float x0, out float y0, out float x1, out float y1)
        {
            var b = new Bounds(KeyShots.FarWallPatchCentre, new Vector3(2f * 130f, 2f * 130f * 1.05f, 1f));
            if (!CompositionLint.ProjectBounds(b, s.Eye, s.Rot, s.Fov, s.AspectRatio, AbyssConst.CAMERA_NEAR, out x0, out y0, out x1, out y1, out _))
            {
                x0 = 0.3f; y0 = 0.3f; x1 = 0.7f; y1 = 0.7f;
            }
            x0 = Mathf.Clamp01(x0); y0 = Mathf.Clamp01(y0); x1 = Mathf.Clamp01(x1); y1 = Mathf.Clamp01(y1);
        }

        /// <summary>4-connected blobs of pixels whose largest channel change is at least <paramref name="levels"/>.</summary>
        static int ChangedBlobs(byte[] a, byte[] b, int w, int h, int levels, out int changed)
        {
            var m = new bool[w * h];
            changed = 0;
            for (int i = 0; i < w * h; i++)
            {
                int k = 3 * i;
                int d = Math.Max(Math.Abs(a[k] - b[k]), Math.Max(Math.Abs(a[k + 1] - b[k + 1]), Math.Abs(a[k + 2] - b[k + 2])));
                if (d >= levels) { m[i] = true; changed++; }
            }
            int blobs = 0;
            var stack = new Stack<int>(256);
            for (int i = 0; i < w * h; i++)
            {
                if (!m[i]) continue;
                blobs++;
                m[i] = false;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int x = p % w, y = p / w;
                    if (x > 0 && m[p - 1]) { m[p - 1] = false; stack.Push(p - 1); }
                    if (x < w - 1 && m[p + 1]) { m[p + 1] = false; stack.Push(p + 1); }
                    if (y > 0 && m[p - w]) { m[p - w] = false; stack.Push(p - w); }
                    if (y < h - 1 && m[p + w]) { m[p + w] = false; stack.Push(p + w); }
                }
            }
            return blobs;
        }

        /// <summary>Difference image (largest channel change x 4, grey) of two captures, as a PNG.</summary>
        static void WriteDiffPng(byte[] a, byte[] b, int w, int h, string path)
        {
            var px = new byte[w * h * 3];
            for (int i = 0; i < w * h; i++)
            {
                int k = 3 * i;
                int d = Math.Max(Math.Abs(a[k] - b[k]), Math.Max(Math.Abs(a[k + 1] - b[k + 1]), Math.Abs(a[k + 2] - b[k + 2])));
                byte v = (byte)Math.Min(255, 4 * d);
                px[k] = v; px[k + 1] = v; px[k + 2] = v;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false, true);
            tex.SetPixelData(px, 0);
            tex.Apply(false, false);
            QaPaths.EnsureParent(path);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static bool Identical(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Luma profile across the wrap plane at the frame centre (rows for Y planes, columns otherwise), averaged over the
        /// middle 60 % of the other axis; jump = step across the plane, excess = jump minus the median neighbouring step.
        /// </summary>
        static void SeamMetric(byte[] rgb, int w, int h, bool rowPlane, out double jump, out double median, out double p90, out double excess)
        {
            const int Half = 24;
            int n = rowPlane ? h : w;
            int c = n / 2;
            var prof = new double[2 * Half];
            for (int k = 0; k < 2 * Half; k++)
            {
                int idx = Mathf.Clamp(c - Half + k, 0, n - 1);
                double s = 0; int cnt = 0;
                if (rowPlane)
                {
                    for (int x = (int)(0.2f * w); x < (int)(0.8f * w); x++) { s += QaRenderer.Luma(rgb, w, h, x, idx); cnt++; }
                }
                else
                {
                    for (int y = (int)(0.2f * h); y < (int)(0.8f * h); y++) { s += QaRenderer.Luma(rgb, w, h, idx, y); cnt++; }
                }
                prof[k] = cnt > 0 ? s / cnt : 0;
            }
            jump = Math.Abs(prof[Half] - prof[Half - 1]);
            var steps = new List<double>(2 * Half);
            for (int k = 0; k + 1 < 2 * Half; k++) if (k != Half - 1) steps.Add(Math.Abs(prof[k + 1] - prof[k]));
            steps.Sort();
            median = steps.Count > 0 ? steps[steps.Count / 2] : 0;
            p90 = steps.Count > 0 ? steps[Mathf.Min(steps.Count - 1, (int)(0.9 * steps.Count))] : 0;
            excess = Math.Max(0, jump - median);
        }

        // ---------------------------------------------------------------- window parity (11.2)

        IEnumerator Parity()
        {
            string tag = CliArgs.String(QaHarness.ParityFlag, null);
            int k = string.IsNullOrEmpty(tag) ? -1 : KeyShots.IndexOf(tag);
            if (k < 0) k = 2;   // KS2a_bridge_ahead
            int w = Screen.width, h = Screen.height;
            var list = new List<ShotSpec>(1);
            KeyShots.AddKeyVariants(list, k, true, w, h);
            ShotSpec s = list[0];
            if (!GameState.CaptureMode)
            {
                // without -capture the title card runs: wait until it has gone (14.11: title out by 6.5 s, hint from 7 s)
                yield return QaHarness.WaitSeconds(7f);
            }
            yield return Prepare(s);
            Request(Req.Parity, w, h);
            yield return new WaitForEndOfFrame();
            var j = new QaJson();
            j.BeginObject();
            j.Prop("test", "11.2 window vs RenderTexture parity (same frame)");
            j.Prop("pose", s.BaseTag);
            j.Prop("window", w + "x" + h);
            j.Prop("window_is_1920x1080", w == 1920 && h == 1080);
            if (!_reqDone || !_reqOk)
            {
                j.Prop("error", "RenderTexture capture failed: " + (_reqError ?? "not rendered"));
                j.Prop("pass", false);
                _failures++;
            }
            else
            {
                Texture2D win = ScreenCapture.CaptureScreenshotAsTexture();
                if (win == null || win.width != w || win.height != h)
                {
                    j.Prop("error", "window capture " + (win == null ? "null" : win.width + "x" + win.height) + " does not match " + w + "x" + h);
                    j.Prop("pass", false);
                    _failures++;
                }
                else
                {
                    _r.CompareWithWindow(win.GetPixels32(), out double direct, out double flipped);
                    bool flipBetter = flipped < direct;
                    double best = Math.Min(direct, flipped);
                    bool pass = best <= ParityLimitLevels;
                    j.Prop("mean_abs_diff_levels", best);
                    j.Prop("mean_abs_diff", best / 255.0);
                    j.Prop("limit_levels", ParityLimitLevels);
                    j.Prop("diff_rows_as_read", direct);
                    j.Prop("diff_rows_reversed", flipped);
                    j.Prop("orientation", flipBetter ? "reversed: run captures with -capture-flip on" : "as read");
                    j.Prop("capture_flip_flag", _r.Flip);
                    j.Prop("rt_mean_luma", _r.MeanLuma);
                    j.Prop("readback", _r.LastReadback);
                    j.Prop("msaa", _r.Msaa);
                    j.Prop("note", "the window includes overlay UI, the RenderTexture never does; capture mode hides the title");
                    j.Prop("pass", pass);
                    if (!pass) _failures++;
                    File.WriteAllBytes(Path.Combine(_dir, "parity_window.png"), win.EncodeToPNG());
                    _r.WritePng(Path.Combine(_dir, "parity_rt.png"));
                    Debug.Log("[ABYSS] QA parity: mean abs diff " + QaJson.F(best, 3) + " levels (" + (flipBetter ? "rows reversed" : "rows as read") + ") -> " + (pass ? "PASS" : "FAIL"));
                }
                if (win != null) UnityEngine.Object.Destroy(win);
            }
            j.EndObject();
            _parityJson = j.ToString();
            File.WriteAllText(Path.Combine(_dir, "parity.json"), _parityJson + "\n");
            yield return null;
        }

        // ---------------------------------------------------------------- report.json (docs/QA_TOOLS.md)

        void WriteReport(bool complete)
        {
            if (_dir == null) return;
            var pdir = ProgressDirector.Instance;
            var j = new QaJson();
            j.BeginObject();
            j.Prop("run", Path.GetFileName(_dir.TrimEnd('\\', '/')));
            j.Prop("run_stamp", _h.RunId);
            j.Prop("date", QaPaths.IsoNow());
            j.Prop("build", QaPaths.BuildKind);
            j.Prop("editor", Application.isEditor);
            j.Prop("development", Debug.isDebugBuild);
            j.Prop("unity", Application.unityVersion);
            j.Prop("seed", (long)GameState.Seed);
            j.Prop("nopost", CliArgs.NoPost);
            j.Prop("selector", _selector);
            j.Prop("lint_only", _lintOnly);
            j.Prop("complete", complete);
            j.Obj("capture");
            j.Prop("color_format", "R16G16B16A16_SFloat");
            j.Prop("depth_format", "D32_SFloat_S8_UInt");
            j.Prop("msaa", QaRenderer.AssetMsaa);
            j.Prop("encode", "linear to sRGB 8-bit on the CPU (exact piecewise, rounded)");
            j.Prop("readback", _r.LastReadback);
            j.Prop("flip_rows", _r.Flip);
            j.Prop("settle_frames", _settle);
            j.Prop("ui_included", false);
            j.Prop("screen", Screen.width + "x" + Screen.height);
            j.Prop("graphics", SystemInfo.graphicsDeviceType.ToString());
            if (pdir != null) { j.Prop("dither", pdir.Dither.ToString()); j.Prop("dither_amp_lsb", pdir.DitherAmplitude); j.Prop("post_processing", pdir.PostProcessingEnabled); }
            j.EndObject();
            if (_parityJson != null) j.Key("parity").Raw(Indent(_parityJson));

            int lintPass = 0, lintFail = 0, lintNone = 0, lintWaived = 0, testPass = 0, testFail = 0, testInc = 0;
            var lintFailures = new List<string>();
            j.Arr("shots");
            for (int i = 0; i < _records.Count; i++)
            {
                ShotRecord r = _records[i];
                ShotSpec s = r.Spec;
                j.BeginObject();
                j.Prop("tag", s.Tag);
                j.Prop("base_tag", s.BaseTag);
                j.Prop("kind", s.Kind);
                if (s.Beat >= 1 && s.Beat <= 7) j.Prop("beat", s.Beat); else j.Key("beat").Null();
                j.Prop("file", s.File);
                j.Prop("width", s.Width);
                j.Prop("height", s.Height);
                j.Prop("aspect", s.Aspect);
                j.Prop("variant", s.Variant);
                j.Obj("pose");
                j.Prop("pos", s.Eye);
                j.Prop("yaw", s.Yaw);
                j.Prop("pitch", s.Pitch);
                j.Prop("fov", s.Fov);
                j.EndObject();
                j.Prop("clock", s.Clock);
                if (s.RideSeconds >= 0f) j.Prop("ride_seconds", s.RideSeconds);
                j.Prop("d", r.D);
                j.Prop("e", r.E);
                j.Prop("game_beat", r.GameBeat);
                if (s.Zone != null) j.Prop("zone", s.Zone);
                j.Prop("nopost", CliArgs.NoPost || s.NoPost);
                KeyShots.WriteRegions(j, s, AbyssConst.CAMERA_NEAR);
                if (s.Kind == "freelook" || s.Kind == "parallax" || s.Kind == "tier" || s.Kind == "seam" || s.Kind == "cue")
                {
                    j.Arr("advisory").Value("mean_luma").Value("p99_luma").Value("clipped_pct").EndArray();
                }
                if (r.DepthStatus != null)
                {
                    j.Obj("depth");
                    j.Prop("status", r.DepthStatus);
                    // orientation check: the upper rows of a level or raised view are farther than the lower rows
                    j.Prop("top_rows_median_m", r.DepthTop);
                    j.Prop("bottom_rows_median_m", r.DepthBottom);
                    j.EndObject();
                }
                if (r.LintJson != null)
                {
                    j.Key("lint").Raw(Indent(r.LintJson));
                    if (r.LintStatus == CompositionLint.StatusPass) lintPass++;
                    else if (r.LintStatus == CompositionLint.StatusWaived) lintWaived++;
                    else if (r.LintStatus == CompositionLint.StatusFail) { lintFail++; lintFailures.Add(s.Tag); }
                    else lintNone++;
                }
                if (r.AnalysisJson != null)
                {
                    j.Key("analysis").Raw(Indent(r.AnalysisJson));
                    if (r.AnalysisPass == true) testPass++;
                    else if (r.AnalysisPass == false) testFail++;
                    else if (s.PairOf != null || s.Kind == "seam") testInc++;
                }
                j.Prop("ok", r.Ok);
                if (r.Error != null) j.Prop("error", r.Error);
                j.Prop("mean_luma", r.MeanLuma);
                if (r.Readback != null) j.Prop("readback", r.Readback);
                j.Prop("render_ms", r.RenderMs);
                j.Prop("png_bytes", r.PngBytes);
                j.EndObject();
            }
            j.EndArray();
            j.Obj("lint_summary");
            j.Prop("pass", lintPass);
            j.Prop("pass_with_known_conflict", lintWaived);
            j.Prop("fail", lintFail);
            j.Prop("no_subjects", lintNone);
            j.Arr("failed_shots");
            for (int i = 0; i < lintFailures.Count; i++) j.Value(lintFailures[i]);
            j.EndArray();
            // documented spec conflicts (subjects named "... [spec-conflict]", DEVELOPMENT.md): waived, never failures
            j.Arr("known_conflicts");
            for (int i = 0; i < _records.Count; i++)
            {
                ShotRecord r = _records[i];
                if (r.Waived == null) continue;
                for (int k = 0; k < r.Waived.Count; k++)
                    j.BeginObject().Prop("tag", r.Spec.Tag).Prop("subject", r.Waived[k]).Prop("status", CompositionLint.StatusWaived).EndObject();
            }
            j.EndArray();
            j.EndObject();
            // cue-sequence verdicts of this run (R2-05 ending sliver, R2-20 ignition sweep; the "cues" selector)
            if (_cueTests.Count > 0)
            {
                j.Arr("cue_tests");
                for (int i = 0; i < _cueTests.Count; i++)
                {
                    j.BeginObject().Prop("name", _cueTests[i].Key);
                    j.Key("result").Raw(Indent(Indent(_cueTestJson[i])));
                    j.EndObject();
                    if (_cueTests[i].Value == true) testPass++;
                    else if (_cueTests[i].Value == false) testFail++;
                    else testInc++;
                }
                j.EndArray();
            }
            j.Obj("tests_summary");
            j.Prop("pass", testPass);
            j.Prop("fail", testFail);
            j.Prop("inconclusive", testInc);
            j.EndObject();
            j.Prop("capture_failures", _failures);
            j.Prop("hook_errors", _hookErrors);
            QaLog.Write(j, "log");
            j.EndObject();
            try { j.Save(Path.Combine(_dir, "report.json")); }
            catch (Exception ex) { Debug.LogWarning("[ABYSS] QA: report.json not written: " + ex.Message); }
        }

        /// <summary>Indent a nested JSON fragment by two levels so the report stays readable (cosmetic only).</summary>
        static string Indent(string json) => json.Replace("\n", "\n    ");
    }
}
