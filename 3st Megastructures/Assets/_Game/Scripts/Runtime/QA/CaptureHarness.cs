// CaptureHarness: deterministic stills and frame sequences through a RenderTexture
// (MASTER_PROMPT 11.2, 11.6, 11.12, 14.10.2, 14.10.3; Part 10 items 6, 12, 21, 22, 35, 36).
//
// Active only with -capture <dir>. After GameBootstrap.Ready it freezes the player, hides every
// Canvas, holds the grade fade at 0 and, for each shot of the ShotPlan:
//   ThreadClock.Set(t) (deterministic ambient), ProgressDirector.OverrideActive / OverrideS = shot.s,
//   ICameraControl.SetPoseOverride(pos, yaw, pitch, fov) (and the camera transform itself),
//   ProgressDirector.Apply() for the final pose, then RenderPipeline.SubmitRenderRequest into a cached
//   R16G16B16A16_SFloat RT (MSAA 4, depth 32), Graphics.Blit into an R8G8B8A8_SRGB RT,
//   AsyncGPUReadback.WaitForCompletion, metrics on the main thread, PNG on a writer thread.
// The render happens in this component's LateUpdate (execution order 32000, after the camera rig and
// ProgressDirector); the coroutine only yields null between shots (no WaitForEndOfFrame in the RT path).
// Output: <dir>/<tag>.png, <dir>/seq_<name>/frame_NNNNN.png + sequence.json, <dir>/report.json.
// Exit code 0 when every image was rendered, written at the requested size and is not black; else 1.
//
// Extra flags: -capture-set <list> (see ShotPlan), -capture-only <substrings>, -capture-warmup N,
// -capture-settle N, -seq-res WxH, -capture-4k (4K encode clips), -capture-flip (flip PNG rows),
// -capture-threads N, -capture-queue-mb N.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace Megastructure.QA
{
    [DefaultExecutionOrder(32000)]
    public sealed class CaptureHarness : MonoBehaviour
    {
        public static CaptureHarness Instance { get; private set; }
        /// <summary>True while the harness owns the camera (other systems may skip UI, input, auto-walk).</summary>
        public static bool Active { get; private set; }

        // ------------------------------------------------------------------ settings
        const int DefaultWarmupFrames = 30;
        const int DefaultSettleFrames = 3;
        const double WorldTimeoutSeconds = 600.0;
        const double StallSeconds = 300.0;
        const double CompileWaitSeconds = 120.0;
        const float StillDeltaTime = 1f / 60f;
        const GraphicsFormat ColorFormat = GraphicsFormat.R16G16B16A16_SFloat;   // 64-bit HDR (14.12, R1 decision 3)
        const int SpikeWindow = 15;
        /// <summary>Parity proof: mean difference at most 1/255 (11.2).</summary>
        public const double ParityMaxMean = 1.0;

        // 14.10.3 / 14.10.1 sequence thresholds (reported per sequence; the lead's analysis decides)
        public const double LumaStepMax = 0.04, SpikeFactor = 3.0, SpikeFloor = 0.05;
        public const double SwapDiffMax = 1.5, SwapLumaStepMax = 0.01;
        public const double StaticDiffFrozen = 0.3, StaticDiffTemporal = 0.8, StaticDiffTemporalDark = 1.2, StaticDarkD = 0.5;

        string m_dir;
        int m_warmup, m_settle;
        bool m_flip;
        CapturePlan m_plan;
        CaptureWriter m_writer;
        bool m_running, m_done;
        double m_lastProgress;
        int m_failures, m_framingFailures, m_lumaOutOfRange, m_progressIndex, m_progressTotal;
        string m_started;
        readonly List<string> m_failureNotes = new List<string>(32);
        readonly Stopwatch m_total = new Stopwatch();

        // render targets and readback
        RenderTexture m_target, m_srgb;
        Texture2D m_readTex;
        RenderPipeline.StandardRequest m_request;
        int m_msaa = 1;
        GraphicsFormat m_depthFormat = GraphicsFormat.None;
        readonly int[] m_hist = new int[256];
        byte[] m_prevRgba;      // previous sequence frame (frame differences)
        byte[] m_parityRgba;
        bool m_warnedPose;

        // camera
        Transform m_camT;
        Vector3 m_camLocalPos;
        Quaternion m_camLocalRot;
        bool m_camLocalSaved;
        readonly List<Canvas> m_hiddenCanvases = new List<Canvas>(8);

        // pose applied in every LateUpdate while active
        bool m_poseActive, m_forceNoFade, m_setAmbient;
        double m_poseS, m_poseAmbient;
        Vector3 m_posePos;
        float m_poseYaw, m_posePitch, m_poseFov;

        // pending render (consumed by LateUpdate of the same frame)
        sealed class RenderJob
        {
            public int width, height;
            public string path;
            public CaptureResult result;
            public FrameRecord frame;      // sequence frame (pose filled at render time for real-flow clips)
            public bool diff, keepForParity, poseFromCamera, gradeProbe;
        }
        RenderJob m_pending;

        // records
        sealed class ShotRecord
        {
            public StillShot shot;
            public readonly CaptureResult r = new CaptureResult();
            public List<QaSubject> subjects;
            public bool framingOk = true;
            public bool lumaOk = true;
        }

        public sealed class FrameRecord
        {
            public int index;
            public double s, x, y, z, yaw, pitch, fov, ambient, fade;
            public readonly CaptureResult r = new CaptureResult();
        }

        sealed class SeqRecord
        {
            public SequenceSpec spec;
            public string dir;
            public readonly List<FrameRecord> frames = new List<FrameRecord>(256);
            public int triggerFrame = -1;
            public string error;
            // summary
            public int written, black, magentaFrames, failed, spikes;
            public double maxLumaStep, maxDiff, meanDiff = double.NaN, medianDiff = double.NaN;
            public int maxLumaStepFrame = -1, maxDiffFrame = -1;
            public string verdict = "";
        }

        readonly List<ShotRecord> m_shots = new List<ShotRecord>(256);
        readonly List<SeqRecord> m_seqs = new List<SeqRecord>(64);
        // parity result
        bool m_parityRan, m_parityOk;
        double m_parityMean = double.NaN, m_parityMeanFlipped = double.NaN;
        int m_parityMax = -1, m_parityW, m_parityH;
        string m_parityNote = "";

        // ------------------------------------------------------------------ lifecycle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePresent()
        {
            if (QaMode.Current != QaRunMode.Capture) return;
            if (FindFirstObjectByType<CaptureHarness>() != null) return;
            var host = GameBootstrap.Instance != null ? GameBootstrap.Instance.gameObject : new GameObject("~QA");
            host.AddComponent<CaptureHarness>();
            Debug.Log("[THREAD] capture harness added at runtime (not in the scene)");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Active = false; }

        void Awake()
        {
            if (QaMode.Current != QaRunMode.Capture || (Instance != null && Instance != this)) { enabled = false; return; }
            Instance = this;
            Active = true;
            QaLog.Install();
            Application.runInBackground = true;
        }

        void Start()
        {
            if (!enabled) return;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Active = false; }
            ReleaseTargets();
            if (m_writer != null) { m_writer.Dispose(); m_writer = null; }
        }

        void Update()
        {
            if (!m_running || m_done) return;
            if (Time.realtimeSinceStartupAsDouble - m_lastProgress > StallSeconds)
            {
                m_done = true;
                Note("stall: no progress for " + StallSeconds + " s (coroutine died?)");
                m_failures++;
                try { if (m_writer != null) m_writer.WaitIdle(60); WriteReport(); } catch (Exception e) { Debug.LogError("[THREAD-FAIL] capture report: " + e.Message); }
                Finish(1);
            }
        }

        void LateUpdate()
        {
            if (!m_running || m_done) return;
            if (m_poseActive)
            {
                try { ApplyPose(); }
                catch (Exception e) { Debug.LogError("[THREAD-FAIL] capture pose: " + e); }
            }
            RenderJob job = m_pending;
            if (job == null) return;
            m_pending = null;
            try { Execute(job); }
            catch (Exception e)
            {
                job.result.Fail(e.GetType().Name + ": " + e.Message);
                Debug.LogError("[THREAD-FAIL] capture " + job.result.file + ": " + e);
            }
        }

        void Touch() { m_lastProgress = Time.realtimeSinceStartupAsDouble; }

        // ------------------------------------------------------------------ main coroutine

        IEnumerator Run()
        {
            m_running = true;
            m_total.Start();
            m_started = QaFiles.IsoNow();
            Touch();
            double t0 = Time.realtimeSinceStartupAsDouble;
            while (!GameBootstrap.Ready)
            {
                if (Time.realtimeSinceStartupAsDouble - t0 > WorldTimeoutSeconds)
                {
                    Note("world not ready after " + WorldTimeoutSeconds + " s");
                    m_failures++;
                    Setup(false);
                    WriteReport();
                    Finish(1);
                    yield break;
                }
                Touch();
                yield return null;
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Note("null graphics device (do not run captures with -batchmode in the player or -nographics)");
                m_failures++;
                Setup(false);
                WriteReport();
                Finish(1);
                yield break;
            }

            Setup(true);
            m_progressTotal = m_plan.stills.Count + m_plan.sequences.Count + (m_plan.parity != null ? 1 : 0);
            Debug.Log("[THREAD] capture plan: " + m_plan.stills.Count + " stills, " + m_plan.sequences.Count + " sequences, parity " + (m_plan.parity != null) + " -> " + m_dir);

            // warm-up behind the first pose (shader variants, shadow maps, pipeline creation)
            StillShot first = m_plan.parity ?? (m_plan.stills.Count > 0 ? m_plan.stills[0] : null);
            if (first != null) BeginStill(first);
            for (int i = 0; i < m_warmup; i++) { Touch(); yield return null; }
#if UNITY_EDITOR
            double tc = Time.realtimeSinceStartupAsDouble;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartupAsDouble - tc < CompileWaitSeconds) { Touch(); yield return null; }
#endif

            yield return RunGradeProbe();
            if (m_plan.parity != null)
            {
                if (!Application.isBatchMode) yield return RunParity(m_plan.parity);
                else { m_parityNote = "skipped: WaitForEndOfFrame does not run in batch mode"; Debug.Log("[THREAD] capture parity skipped (batch mode)"); }
            }
            for (int i = 0; i < m_plan.stills.Count; i++) yield return RunStill(m_plan.stills[i]);
            for (int i = 0; i < m_plan.sequences.Count; i++) yield return RunSequence(m_plan.sequences[i]);

            Touch();
            if (!m_writer.WaitIdle()) { Note("writer did not finish"); m_failures++; }
            EvaluateStills();
            WriteReport();
            m_done = true;
            Debug.Log("[THREAD] capture done: " + m_shots.Count + " stills, " + m_seqs.Count + " sequences, failures " + m_failures
                      + ", framing failures " + m_framingFailures + ", luma out of range " + m_lumaOutOfRange + ", errors logged " + QaLog.ErrorCount
                      + ", " + m_total.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s");
            Finish(m_failures == 0 ? 0 : 1);
        }

        void Setup(bool full)
        {
            m_dir = QaPaths.Resolve(CliArgs.CaptureDir);
            try { Directory.CreateDirectory(m_dir); } catch (Exception e) { Note("cannot create " + m_dir + ": " + e.Message); m_failures++; }
            m_warmup = Math.Max(0, CliArgs.GetInt("-capture-warmup", DefaultWarmupFrames));
            m_settle = Math.Max(1, CliArgs.GetInt("-capture-settle", DefaultSettleFrames));
            m_flip = CliArgs.Has("-capture-flip");

            var opt = new CapturePlanOptions { encode4k = CliArgs.Has("-capture-4k") };
            if (TryParseRes(CliArgs.Get("-seq-res"), out int sw, out int sh)) { opt.seqWidth = sw; opt.seqHeight = sh; }
            string only = CliArgs.Get("-capture-only");
            if (!string.IsNullOrEmpty(only)) opt.only = only.Split(',');
            m_plan = ShotPlan.Build(CliArgs.CaptureSet, opt);
            for (int i = 0; i < m_plan.unknownSets.Count; i++) { Note("unknown capture set '" + m_plan.unknownSets[i] + "'"); m_failures++; }

            int threads = CliArgs.GetInt("-capture-threads", Math.Max(1, Math.Min(4, SystemInfo.processorCount / 2)));
            long queueBytes = (long)CliArgs.GetInt("-capture-queue-mb", 768) << 20;
            m_writer = new CaptureWriter(threads, queueBytes);
            m_writer.Probe();
            if (!full) return;

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = StillDeltaTime;
            ThreadClock.Stepped = true;
            GradeState.FadeBlack = 0f;
            if (ThreadServices.Fade != null) ThreadServices.Fade.SetBlackImmediate(0f);
            var p = ThreadServices.Player;
            if (p != null) { p.SetDrive(false, 0f, 0f); p.Frozen = true; }
            else Debug.LogWarning("[THREAD] capture: no IPlayerState service; the camera is posed directly");
            if (ThreadServices.CameraControl == null) Debug.LogWarning("[THREAD] capture: no ICameraControl service; the camera transform is posed directly");
            if (ProgressDirector.Instance == null) Debug.LogWarning("[THREAD] capture: no ProgressDirector; uniforms follow the player");
            HideUi();
            SaveCameraLocal();
            m_request = new RenderPipeline.StandardRequest();
            m_depthFormat = SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat_S8_UInt, GraphicsFormatUsage.Render)
                ? GraphicsFormat.D32_SFloat_S8_UInt : CoreUtils.GetDefaultDepthStencilFormat();
            var asset = UniversalRenderPipeline.asset;
            if (asset != null && asset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits)
                Debug.LogWarning("[THREAD] capture: URP HDR precision is " + asset.hdrColorBufferPrecision + "; the capture RT is RGBA16F, the screen path differs (parity)");
            if (asset != null && asset.msaaSampleCount != ThreadConstants.Msaa)
                Debug.LogWarning("[THREAD] capture: URP asset MSAA is " + asset.msaaSampleCount + "x; captures use " + ThreadConstants.Msaa + "x (parity)");
            Camera cam = ResolveCamera();
            if (cam == null) Debug.LogWarning("[THREAD] capture: no camera found");
            else if (!cam.allowMSAA) Debug.LogWarning("[THREAD] capture: camera.allowMSAA is off; URP ignores the RT's MSAA (UniversalRenderPipelineCore GetIntermediateTexturesMSAA)");
        }

        void Finish(int code)
        {
            m_running = false;
            Active = false;
            ThreadClock.Stepped = false;
            Time.captureDeltaTime = 0f;
            if (m_writer != null) { m_writer.Dispose(); m_writer = null; }
            ReleaseTargets();
            QaExit.Quit(code, "capture");
        }

        void Note(string msg)
        {
            if (m_failureNotes.Count < 200) m_failureNotes.Add(msg);
            Debug.LogWarning("[THREAD] capture: " + msg);
        }

        void Progress(string tag)
        {
            m_progressIndex++;
            Debug.Log("[THREAD] capture " + m_progressIndex + "/" + m_progressTotal + " " + tag);
        }

        // ------------------------------------------------------------------ stills

        IEnumerator RunStill(StillShot shot)
        {
            Progress(shot.tag);
            var rec = new ShotRecord { shot = shot };
            rec.r.file = shot.File;
            rec.r.width = shot.width;
            rec.r.height = shot.height;
            m_shots.Add(rec);
            if (shot.framing)
            {
                try { rec.subjects = QaFraming.Evaluate(shot.baseTag, shot.s, shot.View); }
                catch (Exception e) { Note(shot.tag + " framing: " + e.Message); }
            }
            BeginStill(shot);
            for (int i = 0; i < m_settle; i++) { Touch(); yield return null; }
            m_pending = new RenderJob { width = shot.width, height = shot.height, path = Path.Combine(m_dir, shot.File), result = rec.r };
            yield return null;   // rendered in this frame's LateUpdate
            if (m_pending != null) { m_pending = null; rec.r.Fail("render did not run"); }
            Touch();
        }

        void BeginStill(StillShot shot)
        {
            m_poseActive = true;
            m_forceNoFade = true;
            m_setAmbient = true;
            m_poseS = shot.s;
            m_poseAmbient = shot.ambient;
            m_posePos = new Vector3((float)shot.x, (float)shot.y, (float)shot.z);
            m_poseYaw = (float)shot.yaw;
            m_posePitch = (float)shot.pitch;
            m_poseFov = (float)shot.fov;
            Time.captureDeltaTime = StillDeltaTime;
            TeleportPlayer(shot.s);
            ApplyPose();
        }

        /// <summary>Moves the (frozen) player next to the shot so s-driven systems agree; never onto the beacon trigger.</summary>
        static void TeleportPlayer(double s)
        {
            var p = ThreadServices.Player;
            if (p == null) return;
            double safe = Math.Min(s, ThreadConstants.BeaconTriggerS - 1.0);
            try { p.Teleport(safe, 0); } catch (Exception e) { Debug.LogWarning("[THREAD] capture teleport: " + e.Message); }
            p.Frozen = true;
        }

        void ApplyPose()
        {
            var pd = ProgressDirector.Instance;
            if (pd != null) { pd.OverrideActive = true; pd.OverrideS = m_poseS; }
            if (m_setAmbient) ThreadClock.Set(m_poseAmbient);
            var cc = ThreadServices.CameraControl;
            if (cc != null) cc.SetPoseOverride(m_posePos, m_poseYaw, m_posePitch, m_poseFov);
            Camera cam = ResolveCamera();
            if (cam != null)
            {
                Quaternion q = Quaternion.Euler(-m_posePitch, m_poseYaw, 0f);
                Transform t = cam.transform;
                if ((t.position - m_posePos).sqrMagnitude > 1e-6f || Quaternion.Angle(t.rotation, q) > 0.01f)
                {
                    if (!m_warnedPose && cc != null && Time.frameCount > 2)
                    {
                        m_warnedPose = true;
                        Debug.LogWarning("[THREAD] capture: the camera rig had not applied the pose override by LateUpdate(32000); the camera transform is set directly");
                    }
                    t.SetPositionAndRotation(m_posePos, q);
                }
                cam.fieldOfView = m_poseFov;
            }
            if (m_forceNoFade) GradeState.FadeBlack = 0f;
            if (pd != null) pd.Apply();
        }

        void EndPoseOverride()
        {
            m_poseActive = false;
            m_forceNoFade = false;
            m_setAmbient = false;
            var pd = ProgressDirector.Instance;
            if (pd != null) pd.OverrideActive = false;
            var cc = ThreadServices.CameraControl;
            if (cc != null) cc.ClearPoseOverride();
            RestoreCameraLocal();
        }

        void EvaluateStills()
        {
            for (int i = 0; i < m_shots.Count; i++)
            {
                ShotRecord rec = m_shots[i];
                CaptureResult r = rec.r;
                bool ok = r.SizeOk && !r.Black && r.error == null;
                if (!ok)
                {
                    m_failures++;
                    Note(rec.shot.tag + ": " + (r.error ?? (!r.rendered ? "not rendered" : !r.written ? "not written" : r.Black ? "black frame" : "size " + r.pngWidth + "x" + r.pngHeight + " != " + r.width + "x" + r.height)));
                }
                if (r.magentaPixels > 0) Note(rec.shot.tag + ": " + r.magentaPixels + " magenta pixels");
                if (rec.subjects != null)
                {
                    for (int k = 0; k < rec.subjects.Count; k++)
                        if (rec.subjects[k].required && !rec.subjects[k].ok) rec.framingOk = false;
                    if (!rec.framingOk && rec.shot.gameFov)
                    {
                        m_framingFailures++;
                        Debug.LogWarning("[THREAD] capture framing: " + rec.shot.tag + " has a named subject outside its acceptance");
                    }
                }
                if (!double.IsNaN(rec.shot.lumaMin) && !double.IsNaN(r.meanLuma))
                {
                    rec.lumaOk = r.meanLuma >= rec.shot.lumaMin && r.meanLuma <= rec.shot.lumaMax;
                    if (!rec.lumaOk) m_lumaOutOfRange++;
                }
            }
        }

        // ------------------------------------------------------------------ grade probe (11.2: captures include the post-processing)

        const int ProbeW = 480, ProbeH = 270;
        const double ProbeMinDiff = 0.5;   // grey levels: the grade (exposure, bloom, vignette, dither) visibly changes the frame
        double m_probeDiff = double.NaN;
        string m_probeNote = "not run";

        /// <summary>Renders the KF1 pose twice at 480x270, with GradeState.Enabled on and off, in one frame.</summary>
        IEnumerator RunGradeProbe()
        {
            KeyShot k = ThreadConstants.KeyShots[0];
            var shot = new StillShot { tag = "grade_probe", s = k.s, x = k.x, y = k.y, z = k.z, yaw = k.yaw, pitch = k.pitch, fov = k.fov, ambient = ShotPlan.AmbientForS(k.s), width = ProbeW, height = ProbeH };
            BeginStill(shot);
            for (int i = 0; i < m_settle; i++) { Touch(); yield return null; }
            m_pending = new RenderJob { width = ProbeW, height = ProbeH, result = new CaptureResult { file = "grade_probe" }, gradeProbe = true };
            yield return null;
            m_pending = null;
            if (double.IsNaN(m_probeDiff) || m_probeDiff < ProbeMinDiff)
                Debug.LogWarning("[THREAD] capture: the grade probe found no difference with GradeState.Enabled off (" + m_probeNote + "); check that ThreadGrade runs for render requests");
            Touch();
        }

        void ExecuteGradeProbe(RenderJob job)
        {
            bool old = GradeState.Enabled;
            byte[] a = null, b = null;
            try
            {
                GradeState.Enabled = true;
                if (!RenderToBuffer(job.width, job.height, out a, out string ea, out _)) { m_probeNote = "render failed: " + ea; return; }
                GradeState.Enabled = false;
                if (!RenderToBuffer(job.width, job.height, out b, out string eb, out _)) { m_probeNote = "render failed: " + eb; return; }
                long sum = 0;
                for (int i = 0; i < a.Length; i += 4)
                    sum += Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]);
                m_probeDiff = sum / (3.0 * job.width * job.height);
                m_probeNote = m_probeDiff >= ProbeMinDiff ? "grade active in the RT path" : "no visible difference (GradeState.Enabled may be ignored, or the grade is missing from render requests)";
            }
            finally
            {
                GradeState.Enabled = old;
                if (a != null) m_writer.Return(a);
                if (b != null) m_writer.Return(b);
            }
        }

        // ------------------------------------------------------------------ parity (11.2: window vs RT, mean <= 1/255)

        IEnumerator RunParity(StillShot shot)
        {
            Progress(shot.tag);
            m_parityRan = true;
            int w = Screen.width, h = Screen.height;
            shot.width = w;
            shot.height = h;
            var rec = new ShotRecord { shot = shot };
            rec.r.file = "parity_rt.png";
            rec.r.width = w;
            rec.r.height = h;
            m_shots.Add(rec);
            BeginStill(shot);
            for (int i = 0; i < m_settle; i++) { Touch(); yield return null; }
            m_pending = new RenderJob { width = w, height = h, path = Path.Combine(m_dir, "parity_rt.png"), result = rec.r, keepForParity = true };
            yield return new WaitForEndOfFrame();   // this frame's LateUpdate rendered the RT; the backbuffer now holds the same frame
            Texture2D tex = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                CompareParity(tex, w, h);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(m_dir, "parity_window.png"), png);
            }
            catch (Exception e) { m_parityOk = false; m_parityNote += "exception: " + e.Message; }
            finally { if (tex != null) Destroy(tex); }
            if (!m_parityOk) { m_failures++; Note("parity failed: " + m_parityNote); }
            Debug.Log("[THREAD] capture parity " + (m_parityOk ? "PASS" : "FAIL") + " mean=" + m_parityMean.ToString("0.000", CultureInfo.InvariantCulture)
                      + " flippedMean=" + m_parityMeanFlipped.ToString("0.000", CultureInfo.InvariantCulture) + " max=" + m_parityMax + " " + m_parityNote);
            if (m_parityRgba != null) { m_writer.Return(m_parityRgba); m_parityRgba = null; }
            yield return null;
            Touch();
        }

        void CompareParity(Texture2D tex, int w, int h)
        {
            m_parityW = tex.width;
            m_parityH = tex.height;
            if (m_parityRgba == null) { m_parityOk = false; m_parityNote += "RT capture missing; "; return; }
            if (tex.width != w || tex.height != h) { m_parityOk = false; m_parityNote += "window " + tex.width + "x" + tex.height + " != " + w + "x" + h + "; "; return; }
            Color32[] bb = tex.GetPixels32();
            m_parityMean = MeanAbs(m_parityRgba, bb, w, h, false, out int max0);
            m_parityMeanFlipped = MeanAbs(m_parityRgba, bb, w, h, true, out int max1);
            bool flipped = m_parityMeanFlipped < m_parityMean;
            m_parityMax = flipped ? max1 : max0;
            m_parityOk = !flipped && m_parityMean <= ParityMaxMean;
            if (flipped) m_parityNote += "RT rows are flipped relative to the window; ";
        }

        static double MeanAbs(byte[] rt, Color32[] bb, int w, int h, bool flipY, out int max)
        {
            long sum = 0;
            max = 0;
            for (int y = 0; y < h; y++)
            {
                int row = (flipY ? h - 1 - y : y) * w;
                int ia = y * w * 4;
                for (int x = 0; x < w; x++, ia += 4)
                {
                    Color32 c = bb[row + x];
                    int a = Math.Abs(rt[ia] - c.r), b = Math.Abs(rt[ia + 1] - c.g), d = Math.Abs(rt[ia + 2] - c.b);
                    sum += a + b + d;
                    if (a > max) max = a;
                    if (b > max) max = b;
                    if (d > max) max = d;
                }
            }
            return sum / (3.0 * Math.Max(1, (long)w * h));
        }

        // ------------------------------------------------------------------ sequences

        IEnumerator RunSequence(SequenceSpec spec)
        {
            Progress(spec.name + " (" + spec.frames + " frames)");
            var rec = new SeqRecord { spec = spec, dir = Path.Combine(m_dir, spec.name) };
            m_seqs.Add(rec);
            try { Directory.CreateDirectory(rec.dir); } catch (Exception e) { rec.error = e.Message; }
            ReleasePrev();
            Time.captureDeltaTime = 1f / Math.Max(1, spec.fps);

            if (spec.mode == SeqMode.RealStart) yield return RunRealStart(spec, rec);
            else if (spec.mode == SeqMode.RealEnd) yield return RunRealEnd(spec, rec);
            else
            {
                BeginSeqFrame(spec, 0);
                for (int i = 0; i < m_settle; i++) { Touch(); yield return null; }
                for (int n = 0; n < spec.frames; n++)
                {
                    FrameRecord fr = BeginSeqFrame(spec, n);
                    QueueFrame(rec, fr, false);
                    yield return null;
                    if (m_pending != null) { m_pending = null; fr.r.Fail("render did not run"); }
                    if ((n & 63) == 63) Debug.Log("[THREAD] capture " + spec.name + " frame " + (n + 1) + "/" + spec.frames);
                    Touch();
                }
            }

            if (!m_writer.WaitIdle()) rec.error = (rec.error ?? "") + "writer timeout; ";
            ReleasePrev();
            Summarize(rec);
            WriteSequenceJson(rec);
            Time.captureDeltaTime = StillDeltaTime;
            Touch();
        }

        FrameRecord BeginSeqFrame(SequenceSpec spec, int n)
        {
            spec.FramePose(n, out double s, out double x, out double y, out double z, out double yaw, out double pitch, out double fov);
            m_poseActive = true;
            m_forceNoFade = true;
            m_setAmbient = true;
            m_poseS = s;
            m_poseAmbient = ThreadClock.Frozen ? spec.ambient0 : spec.AmbientAt(n);
            m_posePos = new Vector3((float)x, (float)y, (float)z);
            m_poseYaw = (float)yaw;
            m_posePitch = (float)pitch;
            m_poseFov = (float)fov;
            if (n == 0) TeleportPlayer(s);
            return new FrameRecord { index = n, s = s, x = x, y = y, z = z, yaw = yaw, pitch = pitch, fov = fov, ambient = m_poseAmbient };
        }

        void QueueFrame(SeqRecord rec, FrameRecord fr, bool poseFromCamera)
        {
            string file = "frame_" + fr.index.ToString("D5", CultureInfo.InvariantCulture) + ".png";
            fr.r.file = rec.spec.name + "/" + file;
            fr.r.width = rec.spec.width;
            fr.r.height = rec.spec.height;
            rec.frames.Add(fr);
            m_pending = new RenderJob
            {
                width = rec.spec.width, height = rec.spec.height, path = Path.Combine(rec.dir, file),
                result = fr.r, frame = fr, diff = true, poseFromCamera = poseFromCamera,
            };
        }

        /// <summary>The real start of the game: the fade controller's intro from black, the player standing at s = 0.</summary>
        IEnumerator RunRealStart(SequenceSpec spec, SeqRecord rec)
        {
            EndPoseOverride();
            var p = ThreadServices.Player;
            if (p != null) { p.SetDrive(false, 0f, 0f); p.Frozen = false; p.Teleport(0, 0); p.Frozen = true; }
            ThreadClock.Set(0);
            QaDrive.LookForward(0, (float)spec.pitch);
            var fade = ThreadServices.Fade;
            if (fade != null) { fade.SetBlackImmediate(1f); fade.BeginIntro(false); }
            else { rec.error = "no IFadeUI service: the start fade is not exercised; "; GradeState.FadeBlack = 0f; }
            HideUi();
            float dt = 1f / Math.Max(1, spec.fps);
            for (int n = 0; n < spec.frames; n++)
            {
                if (n > 0) ThreadClock.Advance(dt);
                QaDrive.LookForward(0, (float)spec.pitch);
                var fr = new FrameRecord { index = n, ambient = ThreadClock.Ambient };
                QueueFrame(rec, fr, true);
                yield return null;
                if (m_pending != null) { m_pending = null; fr.r.Fail("render did not run"); }
                Touch();
            }
        }

        /// <summary>The real ending: walk at 1.5 m/s from 3 m before the beacon trigger, then the stop ramp and the 14 s ending.</summary>
        IEnumerator RunRealEnd(SequenceSpec spec, SeqRecord rec)
        {
            EndPoseOverride();
            var p = ThreadServices.Player;
            if (p == null) { rec.error = "no IPlayerState service"; yield break; }
            p.Frozen = false;
            p.Teleport(spec.s0, 0);
            ThreadClock.Set(ShotPlan.AmbientForS(spec.s0));
            float dt = 1f / Math.Max(1, spec.fps);
            int afterTrigger = (int)Math.Ceiling(ShotPlan.EndSecondsAfterTrigger * spec.fps);
            int noTriggerLimit = (int)Math.Ceiling((ShotPlan.EndLeadIn / ThreadConstants.WalkSpeed + 6.0) * spec.fps);
            for (int n = 0; n < spec.frames; n++)
            {
                if (!p.Finished) p.SetDrive(true, ThreadConstants.WalkSpeed, 0f);
                else if (rec.triggerFrame < 0) rec.triggerFrame = n;
                QaDrive.LookForward(p.S, (float)spec.pitch);
                if (n > 0) ThreadClock.Advance(dt);
                var fr = new FrameRecord { index = n, ambient = ThreadClock.Ambient };
                QueueFrame(rec, fr, true);
                yield return null;
                if (m_pending != null) { m_pending = null; fr.r.Fail("render did not run"); }
                Touch();
                if (rec.triggerFrame >= 0 && n - rec.triggerFrame >= afterTrigger) break;
                if (rec.triggerFrame < 0 && n >= noTriggerLimit) { rec.error = (rec.error ?? "") + "beacon trigger never fired; "; break; }
            }
        }

        void Summarize(SeqRecord rec)
        {
            int n = rec.frames.Count;
            bool edges = rec.spec.mode == SeqMode.RealStart || rec.spec.mode == SeqMode.RealEnd;
            var diffs = new List<double>(n);
            var window = new List<double>(SpikeWindow);
            double prevLuma = double.NaN, sumDiff = 0;
            for (int i = 0; i < n; i++)
            {
                CaptureResult r = rec.frames[i].r;
                if (r.SizeOk && r.error == null) rec.written++;
                else rec.failed++;
                if (r.Black) rec.black++;
                if (r.magentaPixels > 0) rec.magentaFrames++;
                if (!double.IsNaN(prevLuma) && !double.IsNaN(r.meanLuma))
                {
                    double step = Math.Abs(r.meanLuma - prevLuma);
                    if (step > rec.maxLumaStep) { rec.maxLumaStep = step; rec.maxLumaStepFrame = i; }
                }
                prevLuma = r.meanLuma;
                if (!double.IsNaN(r.meanAbsDiff))
                {
                    double d = r.meanAbsDiff;
                    if (window.Count >= 3)
                    {
                        double med = Median(window);
                        if (d > SpikeFactor * Math.Max(SpikeFloor, med)) rec.spikes++;
                    }
                    window.Add(d);
                    if (window.Count > SpikeWindow) window.RemoveAt(0);
                    diffs.Add(d);
                    sumDiff += d;
                    if (d > rec.maxDiff) { rec.maxDiff = d; rec.maxDiffFrame = i; }
                }
            }
            if (diffs.Count > 0) { rec.meanDiff = sumDiff / diffs.Count; rec.medianDiff = Median(diffs); }
            if (rec.failed > 0) { m_failures++; Note(rec.spec.name + ": " + rec.failed + " frames failed"); }
            if (!edges && rec.black > 0) { m_failures++; Note(rec.spec.name + ": " + rec.black + " black frames"); }
            if (rec.error != null) { m_failures++; Note(rec.spec.name + ": " + rec.error); }

            // threshold verdicts (reported; the analysis tools and the lead decide)
            var v = new System.Text.StringBuilder();
            if (rec.maxLumaStep > LumaStepMax) v.Append("luma step ").Append(rec.maxLumaStep.ToString("0.0000", CultureInfo.InvariantCulture)).Append(" > ").Append(LumaStepMax.ToString(CultureInfo.InvariantCulture)).Append("; ");
            if (rec.spikes > 0) v.Append(rec.spikes).Append(" difference spikes; ");
            if (rec.spec.name == "seq_swap")
            {
                if (rec.maxDiff >= SwapDiffMax) v.Append("T-SWAP frame difference ").Append(rec.maxDiff.ToString("0.000", CultureInfo.InvariantCulture)).Append("; ");
                if (rec.maxLumaStep > SwapLumaStepMax) v.Append("T-SWAP luma step; ");
            }
            if (rec.spec.mode == SeqMode.Static && !double.IsNaN(rec.meanDiff))
            {
                double lim = StaticLimit(rec.spec.s0);
                if (rec.meanDiff >= lim) v.Append("static mean difference ").Append(rec.meanDiff.ToString("0.000", CultureInfo.InvariantCulture)).Append(" >= ").Append(lim.ToString(CultureInfo.InvariantCulture)).Append("; ");
            }
            if (rec.magentaFrames > 0) v.Append(rec.magentaFrames).Append(" frames with magenta pixels; ");
            rec.verdict = v.Length == 0 ? "ok" : v.ToString().TrimEnd(' ', ';');
            if (v.Length > 0) Debug.LogWarning("[THREAD] capture " + rec.spec.name + ": " + rec.verdict);
        }

        /// <summary>14.10.3: static clip limit 0.3 with -freeze-dither and a frozen ambient clock, else 0.8 (1.2 where D > 0.5).</summary>
        public static double StaticLimit(double s)
        {
            if (ThreadClock.FreezeDither && ThreadClock.Frozen) return StaticDiffFrozen;
            return ThreadMath.Darkness(s) > StaticDarkD ? StaticDiffTemporalDark : StaticDiffTemporal;
        }

        static double Median(List<double> values)
        {
            var tmp = new List<double>(values);
            tmp.Sort();
            int m = tmp.Count / 2;
            return tmp.Count == 0 ? 0 : (tmp.Count % 2 == 1 ? tmp[m] : 0.5 * (tmp[m - 1] + tmp[m]));
        }

        void ReleasePrev()
        {
            if (m_prevRgba != null && m_writer != null) m_writer.Return(m_prevRgba);
            m_prevRgba = null;
        }

        // ------------------------------------------------------------------ rendering

        void Execute(RenderJob job)
        {
            CaptureResult r = job.result;
            if (job.gradeProbe) { ExecuteGradeProbe(job); return; }
            if (job.frame != null && job.poseFromCamera) FillPoseFromCamera(job.frame);
            if (!RenderToBuffer(job.width, job.height, out byte[] rgba, out string err, out double ms))
            {
                r.Fail(err);
                if (rgba != null) m_writer.Return(rgba);
                return;
            }
            r.rendered = true;
            r.renderMs = ms;

            var sw = Stopwatch.StartNew();
            int pixels = job.width * job.height;
            bool rgb = m_writer.Rgb;
            byte[] packed = m_writer.Rent(pixels * (rgb ? 3 : 4));
            CaptureMetrics.Compute(rgba, job.diff ? m_prevRgba : null, job.width, job.height, rgb ? packed : null, rgb ? null : packed, m_flip, m_hist, r);
            r.statsMs = sw.Elapsed.TotalMilliseconds;

            if (job.keepForParity) { if (m_parityRgba != null) m_writer.Return(m_parityRgba); m_parityRgba = rgba; }
            else if (job.diff) { if (m_prevRgba != null) m_writer.Return(m_prevRgba); m_prevRgba = rgba; }
            else m_writer.Return(rgba);

            m_writer.Enqueue(new WriteJob { data = packed, width = job.width, height = job.height, length = packed.Length, path = job.path, result = r });
        }

        void FillPoseFromCamera(FrameRecord fr)
        {
            var p = ThreadServices.Player;
            fr.s = p != null ? p.S : ProgressDirector.S;
            Camera cam = ResolveCamera();
            if (cam != null)
            {
                Vector3 pos = cam.transform.position;
                Vector3 f = cam.transform.forward;
                fr.x = pos.x; fr.y = pos.y; fr.z = pos.z;
                fr.yaw = Math.Atan2(f.x, f.z) / ThreadMath.Deg;
                fr.pitch = Math.Asin(Mathf.Clamp(f.y, -1f, 1f)) / ThreadMath.Deg;
                fr.fov = cam.fieldOfView;
            }
            fr.fade = ThreadServices.Fade != null ? ThreadServices.Fade.Black : GradeState.FadeBlack;
        }

        bool RenderToBuffer(int w, int h, out byte[] rgba, out string error, out double ms)
        {
            rgba = null;
            error = null;
            ms = 0;
            var sw = Stopwatch.StartNew();
            Camera cam = ResolveCamera();
            if (cam == null) { error = "no camera"; return false; }
            if (w <= 0 || h <= 0) { error = "bad size " + w + "x" + h; return false; }
            EnsureTargets(w, h);
            if (RenderPipelineManager.currentPipeline == null) cam.Render();   // pipeline may not exist yet (R4 1.1)
            m_request.destination = m_target;
            if (!RenderPipeline.SupportsRenderRequest(cam, m_request)) { error = "StandardRequest not supported by the active pipeline"; return false; }
            cam.ResetAspect();
            Submit(cam);
            Graphics.Blit(m_target, m_srgb);   // MSAA resolve + linear -> sRGB encode on write

            rgba = m_writer.Rent(w * h * 4);
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadbackRequest rb = AsyncGPUReadback.Request(m_srgb, 0, GraphicsFormat.R8G8B8A8_SRGB);
                rb.WaitForCompletion();
                if (rb.hasError) { error = "AsyncGPUReadback failed"; return false; }
                NativeArray<byte> data = rb.GetData<byte>();
                if (data.Length != rgba.Length) { error = "readback size " + data.Length + " != " + rgba.Length; return false; }
                data.CopyTo(rgba);
            }
            else
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = m_srgb;
                if (m_readTex == null || m_readTex.width != w || m_readTex.height != h)
                {
                    if (m_readTex != null) Destroy(m_readTex);
                    m_readTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                }
                m_readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                m_readTex.Apply(false, false);
                RenderTexture.active = prev;
                NativeArray<byte> data = m_readTex.GetRawTextureData<byte>();
                if (data.Length != rgba.Length) { error = "ReadPixels size mismatch"; return false; }
                data.CopyTo(rgba);
            }
            ms = sw.Elapsed.TotalMilliseconds;
            return true;
        }

        void Submit(Camera cam)
        {
#if UNITY_EDITOR
            bool oldAsync = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;   // no cyan placeholder shaders in captures
            try { RenderPipeline.SubmitRenderRequest(cam, m_request); }
            finally { UnityEditor.ShaderUtil.allowAsyncCompilation = oldAsync; }
#else
            RenderPipeline.SubmitRenderRequest(cam, m_request);
#endif
        }

        void EnsureTargets(int w, int h)
        {
            if (m_target != null && m_target.width == w && m_target.height == h && m_target.IsCreated()) return;
            int oldPixels = m_target != null ? m_target.width * m_target.height : 0;
            ReleaseTargets();
            var desc = new RenderTextureDescriptor(w, h, ColorFormat, m_depthFormat != GraphicsFormat.None ? m_depthFormat : CoreUtils.GetDefaultDepthStencilFormat(), 0)
            {
                msaaSamples = Math.Max(1, ThreadConstants.Msaa),
                useMipMap = false,
                autoGenerateMips = false,
                useDynamicScale = false,
            };
            int supported = SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc);
            desc.msaaSamples = Math.Max(1, Math.Min(desc.msaaSamples, supported));
            m_msaa = desc.msaaSamples;
            m_target = new RenderTexture(desc) { name = "ThreadCapture_HDR" };
            m_target.Create();
            m_srgb = new RenderTexture(w, h, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None) { name = "ThreadCapture_sRGB" };
            m_srgb.Create();
            if (m_writer != null && oldPixels > 0 && oldPixels != w * h) m_writer.TrimPool(w * h * 4, w * h * (m_writer.Rgb ? 3 : 4));
        }

        void ReleaseTargets()
        {
            if (m_target != null) { m_target.Release(); Destroy(m_target); m_target = null; }
            if (m_srgb != null) { m_srgb.Release(); Destroy(m_srgb); m_srgb = null; }
            if (m_readTex != null) { Destroy(m_readTex); m_readTex = null; }
        }

        // ------------------------------------------------------------------ camera, UI

        static Camera ResolveCamera()
        {
            var cc = ThreadServices.CameraControl;
            if (cc != null && cc.Camera != null) return cc.Camera;
            var gb = GameBootstrap.Instance;
            if (gb != null && gb.mainCamera != null) return gb.mainCamera;
            return Camera.main;
        }

        void SaveCameraLocal()
        {
            Camera cam = ResolveCamera();
            if (cam == null || cam.transform.parent == null) return;
            m_camT = cam.transform;
            m_camLocalPos = m_camT.localPosition;
            m_camLocalRot = m_camT.localRotation;
            m_camLocalSaved = true;
        }

        void RestoreCameraLocal()
        {
            if (m_camLocalSaved && m_camT != null) m_camT.SetLocalPositionAndRotation(m_camLocalPos, m_camLocalRot);
        }

        /// <summary>No UI in captures (screen-space overlay UI is not in RT captures anyway, but it is in the parity window capture).</summary>
        void HideUi()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null || !canvases[i].enabled) continue;
                canvases[i].enabled = false;
                m_hiddenCanvases.Add(canvases[i]);
            }
        }

        static bool TryParseRes(string v, out int w, out int h)
        {
            w = h = 0;
            if (string.IsNullOrEmpty(v)) return false;
            string[] p = v.ToLowerInvariant().Split('x');
            return p.Length == 2 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out w)
                && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out h) && w > 0 && h > 0;
        }

        // ------------------------------------------------------------------ reports

        void WriteSequenceJson(SeqRecord rec)
        {
            var j = new QaJson();
            j.BeginObject();
            WriteSeqHeader(j, rec);
            j.Key("frames").BeginArray();
            for (int i = 0; i < rec.frames.Count; i++)
            {
                FrameRecord f = rec.frames[i];
                CaptureResult r = f.r;
                j.BeginObject(true);
                j.Prop("i", f.index).Prop("file", Path.GetFileName(r.file)).Prop("s", f.s).Prop("pos", f.x, f.y, f.z);
                j.Prop("yaw", f.yaw).Prop("pitch", f.pitch).Prop("fov", f.fov).Prop("ambient", f.ambient);
                if (rec.spec.mode == SeqMode.RealStart || rec.spec.mode == SeqMode.RealEnd) j.Prop("fade", f.fade);
                j.Prop("meanLuma", r.meanLuma).Prop("meanAbsDiff", r.meanAbsDiff).Prop("clipped", r.clippedFraction).Prop("magenta", r.magentaPixels);
                j.Prop("ok", r.SizeOk && r.error == null);
                if (r.error != null) j.Prop("error", r.error);
                j.EndObject();
            }
            j.EndArray();
            j.EndObject();
            QaFiles.WriteText(Path.Combine(rec.dir, "sequence.json"), j.ToString());
        }

        static void WriteSeqHeader(QaJson j, SeqRecord rec)
        {
            SequenceSpec sp = rec.spec;
            j.Prop("name", sp.name).Prop("set", sp.set).Prop("purpose", sp.purpose).Prop("mode", sp.mode.ToString());
            j.Prop("dir", sp.name).Prop("fps", sp.fps).Prop("width", sp.width).Prop("height", sp.height);
            j.Prop("framesPlanned", sp.frames).Prop("framesCaptured", rec.frames.Count).Prop("framesWritten", rec.written);
            j.Prop("s0", sp.s0).Prop("s1", sp.s1);
            if (sp.mode == SeqMode.PanPitch || sp.mode == SeqMode.PanYaw) j.Prop("panStepDeg", sp.panStepDeg);
            if (sp.mode == SeqMode.RealEnd) j.Prop("triggerFrame", rec.triggerFrame);
            j.Prop("maxLumaStep", rec.maxLumaStep).Prop("maxLumaStepFrame", rec.maxLumaStepFrame);
            j.Prop("maxMeanAbsDiff", rec.maxDiff).Prop("maxMeanAbsDiffFrame", rec.maxDiffFrame);
            j.Prop("meanMeanAbsDiff", rec.meanDiff).Prop("medianMeanAbsDiff", rec.medianDiff).Prop("spikes", rec.spikes);
            j.Prop("blackFrames", rec.black).Prop("magentaFrames", rec.magentaFrames).Prop("failedFrames", rec.failed);
            if (sp.mode == SeqMode.Static) j.Prop("staticLimit", StaticLimit(sp.s0));
            j.Prop("verdict", rec.verdict);
            if (rec.error != null) j.Prop("error", rec.error);
        }

        void WriteReport()
        {
            var j = new QaJson();
            j.BeginObject();
            j.Prop("tool", "Megastructure.QA.CaptureHarness").Prop("version", 1);
            j.Prop("started", m_started).Prop("finished", QaFiles.IsoNow());
            j.Prop("seconds", m_total.Elapsed.TotalSeconds);
            j.Prop("dir", m_dir);
            j.Prop("captureSet", CliArgs.CaptureSet);
            if (m_plan != null) { j.Prop("sets", m_plan.sets); j.Prop("unknownSets", m_plan.unknownSets); }
            QaEnvironment.Write(j);

            j.Key("render").BeginObject();
            j.Prop("colorFormat", ColorFormat.ToString()).Prop("depthFormat", m_depthFormat.ToString()).Prop("msaa", m_msaa);
            j.Prop("path", "SubmitRenderRequest(StandardRequest) -> Blit R8G8B8A8_SRGB -> AsyncGPUReadback");
            j.Prop("asyncReadback", SystemInfo.supportsAsyncGPUReadback);
            var asset = UniversalRenderPipeline.asset;
            if (asset != null)
            {
                j.Prop("urpHdr", asset.supportsHDR).Prop("urpHdrPrecision", asset.hdrColorBufferPrecision.ToString()).Prop("urpMsaa", asset.msaaSampleCount);
            }
            Camera cam = ResolveCamera();
            if (cam != null) j.Prop("cameraAllowMsaa", cam.allowMSAA).Prop("cameraAllowHdr", cam.allowHDR).Prop("cameraNear", cam.nearClipPlane).Prop("cameraFar", cam.farClipPlane);
            if (m_writer != null) j.Prop("pngRgb", m_writer.Rgb).Prop("writerThreads", m_writer.Threads).Prop("writerProbe", m_writer.ProbeNote);
            j.Prop("flipRows", m_flip).Prop("warmupFrames", m_warmup).Prop("settleFrames", m_settle);
            j.Prop("pixelOrigin", "top-left (PNG rows); subject boxes use the same origin");
            j.Prop("luma", "Rec.709 weights on sRGB-encoded values, 0..1");
            j.EndObject();

            j.Key("gradeProbe").BeginObject(true).Prop("meanAbsDiff", m_probeDiff).Prop("minDiff", ProbeMinDiff).Prop("note", m_probeNote).EndObject();

            j.Key("parity").BeginObject();
            j.Prop("ran", m_parityRan).Prop("ok", m_parityOk).Prop("meanAbsDiff", m_parityMean).Prop("meanAbsDiffFlipped", m_parityMeanFlipped);
            j.Prop("maxAbsDiff", m_parityMax).Prop("width", m_parityW).Prop("height", m_parityH).Prop("limit", ParityMaxMean).Prop("note", m_parityNote);
            j.EndObject();

            j.Prop("failures", m_failures).Prop("framingFailures", m_framingFailures).Prop("lumaOutOfRange", m_lumaOutOfRange);
            j.Prop("errorsLogged", QaLog.ErrorCount).Prop("warningsLogged", QaLog.WarningCount);
            j.Prop("errorMessages", QaLog.Messages());
            j.Prop("notes", m_failureNotes);

            j.Key("shots").BeginArray();
            for (int i = 0; i < m_shots.Count; i++) WriteShot(j, m_shots[i]);
            j.EndArray();

            j.Key("sequences").BeginArray();
            for (int i = 0; i < m_seqs.Count; i++) { j.BeginObject(); WriteSeqHeader(j, m_seqs[i]); j.EndObject(); }
            j.EndArray();
            j.EndObject();
            QaFiles.WriteText(Path.Combine(m_dir, "report.json"), j.ToString());
        }

        static void WriteShot(QaJson j, ShotRecord rec)
        {
            StillShot sh = rec.shot;
            CaptureResult r = rec.r;
            j.BeginObject();
            j.Prop("tag", sh.tag).Prop("file", r.file).Prop("set", sh.set).Prop("baseTag", sh.baseTag).Prop("variant", sh.variant);
            j.Prop("width", r.width).Prop("height", r.height).Prop("pngWidth", r.pngWidth).Prop("pngHeight", r.pngHeight);
            j.Prop("s", sh.s).Prop("pos", sh.x, sh.y, sh.z).Prop("yaw", sh.yaw).Prop("pitch", sh.pitch).Prop("fov", sh.fov);
            j.Prop("fovGame", ThreadMath.Fov(sh.s)).Prop("gameFov", sh.gameFov).Prop("ambient", sh.ambient);
            j.Prop("meanLuma", r.meanLuma);
            if (!double.IsNaN(sh.lumaMin)) { j.Key("lumaRange").BeginArray(true).Value(sh.lumaMin).Value(sh.lumaMax).EndArray(); j.Prop("lumaOk", rec.lumaOk); }
            j.Prop("lumaP01", r.lumaP01).Prop("lumaP50", r.lumaP50).Prop("lumaP99", r.lumaP99);
            j.Prop("clippedFraction", r.clippedFraction).Prop("magentaPixels", r.magentaPixels).Prop("black", r.Black);
            j.Key("timing").BeginObject(true).Prop("renderMs", r.renderMs).Prop("statsMs", r.statsMs).Prop("encodeMs", r.encodeMs).EndObject();
            j.Prop("fileBytes", r.fileBytes);
            j.Prop("ok", r.SizeOk && !r.Black && r.error == null);
            if (r.error != null) j.Prop("error", r.error);
            if (rec.subjects != null)
            {
                j.Prop("framingOk", rec.framingOk);
                j.Key("subjects").BeginArray();
                for (int k = 0; k < rec.subjects.Count; k++) rec.subjects[k].Write(j);
                j.EndArray();
            }
            j.EndObject();
        }
    }
}
