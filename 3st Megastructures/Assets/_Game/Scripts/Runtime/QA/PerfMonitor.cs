// PerfMonitor: per-frame performance log in the player (MASTER_PROMPT 11.7, 14.10.1 T-LOAD; Part 10 items 23, 24).
// Active with -perf-log <file> (CSV) and/or -quit-after S. After the load fade it records per frame:
// the real frame time, CPU main / render / total frame time and GPU frame time (FrameTimingManager,
// needs PlayerSettings.enableFrameTimingStats in release players; ProfilerRecorder fallback), draw calls,
// SetPass calls, triangles, vertices, GC allocation in the frame (ProfilerRecorder "GC Allocated In Frame",
// Mono-heap delta fallback) and graphics memory. Rows live in a preallocated array (zero allocations
// per frame); the CSV and <file>.summary.json (average, 1 % and 0.1 % low FPS, max frame, frames above
// 33 ms, GC allocation total) are written at quit (QaExit, OnApplicationQuit or OnDestroy).
// Measures with vSyncCount 0 and targetFrameRate -1 (11.7) unless -soak keeps vSync on.
// -perf-rt WxH renders the camera into an offscreen RT of that size (4K on a smaller monitor, 11.7).
// -quit-after S: quits after S seconds of real time (exit 0; exit 1 when a QA runner had not finished).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Megastructure.QA
{
    /// <summary>Frame-time statistics (pure; tested in EditMode).</summary>
    public struct PerfSummary
    {
        public int frames;
        public double totalSeconds, avgMs, avgFps, p99Ms, p999Ms, low1Fps, low01Fps, worst1AvgMs, worst01AvgMs, maxMs;
        public int framesOverBudget;

        /// <summary>Summary of frame times in ms. "1 % low FPS" = 1000 / mean of the slowest 1 % of frames.</summary>
        public static PerfSummary From(float[] ms, int count, double budgetMs)
        {
            var r = new PerfSummary();
            if (ms == null || count <= 0) return r;
            var sorted = new float[count];
            Array.Copy(ms, sorted, count);
            Array.Sort(sorted);
            double sum = 0;
            for (int i = 0; i < count; i++) { sum += sorted[i]; if (sorted[i] > budgetMs) r.framesOverBudget++; }
            r.frames = count;
            r.totalSeconds = sum / 1000.0;
            r.avgMs = sum / count;
            r.avgFps = sum > 0 ? 1000.0 * count / sum : 0;
            r.maxMs = sorted[count - 1];
            r.p99Ms = sorted[Math.Min(count - 1, (int)Math.Floor(0.99 * (count - 1) + 0.5))];
            r.p999Ms = sorted[Math.Min(count - 1, (int)Math.Floor(0.999 * (count - 1) + 0.5))];
            r.worst1AvgMs = TailMean(sorted, count, 0.01);
            r.worst01AvgMs = TailMean(sorted, count, 0.001);
            r.low1Fps = r.worst1AvgMs > 0 ? 1000.0 / r.worst1AvgMs : 0;
            r.low01Fps = r.worst01AvgMs > 0 ? 1000.0 / r.worst01AvgMs : 0;
            return r;
        }

        static double TailMean(float[] sorted, int count, double fraction)
        {
            int n = Math.Max(1, (int)Math.Ceiling(count * fraction));
            double s = 0;
            for (int i = count - n; i < count; i++) s += sorted[i];
            return s / n;
        }
    }

    [DefaultExecutionOrder(32100)]
    public sealed class PerfMonitor : MonoBehaviour
    {
        public static PerfMonitor Instance { get; private set; }

        /// <summary>11.7: no frame above 33 ms after the load fade.</summary>
        public const double FrameBudgetMs = 33.0;
        // 11.7 targets at 1920x1080 (reported, not enforced here)
        public const double TargetAvgFps = 90.0, TargetLow1Fps = 60.0, Target4kAvgFps = 60.0;
        const int DefaultCapacity = 300000;
        const double FadeWaitSeconds = 8.0;
        const float FadeDoneBlack = 0.001f;
        const int GfxMemFallbackEvery = 60;

        struct Row
        {
            public float tReal, tVirt, s, frameMs, cpuMainMs, cpuRenderMs, cpuTotalMs, gpuMs, gfxMemMB;
            public int draws, setPass, tris, verts, gcAlloc;
        }

        string m_path;
        double m_quitAfter = -1;
        bool m_recordRequested, m_recording, m_flushed, m_skipFirst;
        double m_readySince = -1, m_recStartReal, m_recStartVirt;
        Row[] m_rows;
        int m_count;
        long m_seen, m_gcTotal, m_overBudget;
        double m_maxMs;
        int m_gcCount0;
        long m_lastMono;
        bool m_gcRecorderValid, m_gpuFromFtm;
        float m_lastGfxMB;
        readonly FrameTiming[] m_ft = new FrameTiming[1];
        ProfilerRecorder m_draw, m_setPass, m_tris, m_verts, m_gc, m_cpuMain, m_cpuRender, m_cpuTotal, m_gpu, m_gfxMem;
        RenderTexture m_perfRt;
        Camera m_perfCam;
        string m_perfRtSize;
        readonly List<string> m_notes = new List<string>(8);

        public static void FlushActive() { if (Instance != null) Instance.Flush(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePresent()
        {
            if (!QaMode.PerfRequested && !(CliArgs.QuitAfter > 0)) return;
            if (FindFirstObjectByType<PerfMonitor>() != null) return;
            var host = GameBootstrap.Instance != null ? GameBootstrap.Instance.gameObject : new GameObject("~QA");
            host.AddComponent<PerfMonitor>();
        }

        void Awake()
        {
            bool perf = QaMode.PerfRequested;
            m_quitAfter = CliArgs.QuitAfter;
            if (!perf && !(m_quitAfter > 0)) { enabled = false; return; }
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            QaLog.Install();
            if (perf && QaMode.Current == QaRunMode.Capture)
            {
                perf = false;
                Debug.LogWarning("[THREAD] perf: -perf-log ignored during -capture (render requests distort the frame times)");
            }
            m_recordRequested = perf;
            if (perf)
            {
                m_path = QaPaths.Resolve(CliArgs.PerfLog);
                m_rows = new Row[Math.Max(1000, CliArgs.GetInt("-perf-capacity", DefaultCapacity))];
                Application.runInBackground = true;
            }
        }

        void OnDestroy()
        {
            Flush();
            DisposeRecorders();
            ReleasePerfRt();
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit() { Flush(); }

        void Update()
        {
            if (m_quitAfter > 0 && Time.realtimeSinceStartupAsDouble >= m_quitAfter && !QaExit.Quitting)
            {
                bool runner = QaMode.Current != QaRunMode.None;
                if (runner) Debug.LogError("[THREAD] -quit-after " + m_quitAfter + " s reached before the " + QaMode.Current + " run finished");
                QaExit.Quit(runner ? 1 : 0, "quit-after");
                return;
            }
            if (!m_recordRequested || m_flushed) return;
            if (!m_recording) { TryStart(); return; }
            if (m_skipFirst) { m_skipFirst = false; return; }   // that frame's delta still contains the start-up
            Sample();
        }

        void TryStart()
        {
            if (!GameBootstrap.Ready) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (m_readySince < 0) m_readySince = now;
            var fade = ThreadServices.Fade;
            bool fadeDone = fade == null ? GradeState.FadeBlack <= FadeDoneBlack : fade.Black <= FadeDoneBlack;
            if (!fadeDone && now - m_readySince < FadeWaitSeconds) return;
            if (!fadeDone) m_notes.Add("fade not finished after " + FadeWaitSeconds + " s; recording anyway");

            if (QaMode.Current != QaRunMode.Soak) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            StartRecorders();
            SetupPerfRt();
            m_recording = true;
            m_skipFirst = true;
            m_recStartReal = now;
            m_recStartVirt = Time.timeAsDouble;
            m_gcCount0 = GC.CollectionCount(0);
            m_lastMono = Profiler.GetMonoUsedSizeLong();
            Debug.Log("[THREAD] perf recording started (vSync " + QualitySettings.vSyncCount + ", frame timing " + (FrameTimingManager.IsFeatureEnabled() ? "on" : "off") + ") -> " + m_path);
        }

        void StartRecorders()
        {
            var handles = new List<ProfilerRecorderHandle>(4096);
            ProfilerRecorderHandle.GetAvailable(handles);
            var byName = new Dictionary<string, ProfilerRecorderHandle>(handles.Count);
            for (int i = 0; i < handles.Count; i++)
            {
                string n = ProfilerRecorderHandle.GetDescription(handles[i]).Name;
                if (n != null && !byName.ContainsKey(n)) byName.Add(n, handles[i]);
            }
            m_draw = Make(byName, "Draw Calls Count", ProfilerCategory.Render);
            m_setPass = Make(byName, "SetPass Calls Count", ProfilerCategory.Render);
            m_tris = Make(byName, "Triangles Count", ProfilerCategory.Render);
            m_verts = Make(byName, "Vertices Count", ProfilerCategory.Render);
            m_gc = Make(byName, "GC Allocated In Frame", ProfilerCategory.Memory);
            m_cpuMain = Make(byName, "CPU Main Thread Frame Time", ProfilerCategory.Internal);
            m_cpuRender = Make(byName, "CPU Render Thread Frame Time", ProfilerCategory.Internal);
            m_cpuTotal = Make(byName, "CPU Total Frame Time", ProfilerCategory.Internal);
            m_gpu = Make(byName, "GPU Frame Time", ProfilerCategory.Internal);
            m_gfxMem = Make(byName, "Gfx Used Memory", ProfilerCategory.Memory);
        }

        ProfilerRecorder Make(Dictionary<string, ProfilerRecorderHandle> byName, string name, ProfilerCategory fallback)
        {
            ProfilerRecorder r;
            if (byName.TryGetValue(name, out ProfilerRecorderHandle h))
            {
                r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default);
                r.Start();
            }
            else r = ProfilerRecorder.StartNew(fallback, name, 1);
            if (!r.Valid) m_notes.Add("profiler counter unavailable: " + name);
            return r;
        }

        void DisposeRecorders()
        {
            if (!m_recording) return;   // recorders exist only once recording started
            SafeDispose(ref m_draw); SafeDispose(ref m_setPass); SafeDispose(ref m_tris); SafeDispose(ref m_verts); SafeDispose(ref m_gc);
            SafeDispose(ref m_cpuMain); SafeDispose(ref m_cpuRender); SafeDispose(ref m_cpuTotal); SafeDispose(ref m_gpu); SafeDispose(ref m_gfxMem);
            m_recording = false;
        }

        static void SafeDispose(ref ProfilerRecorder r)
        {
            try { r.Dispose(); } catch (Exception) { }
            r = default;
        }

        void SetupPerfRt()
        {
            string v = CliArgs.Get("-perf-rt");
            if (string.IsNullOrEmpty(v)) return;
            string[] p = v.ToLowerInvariant().Split('x');
            if (p.Length != 2 || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) || w <= 0 || h <= 0)
            { m_notes.Add("bad -perf-rt " + v); return; }
            Camera cam = ThreadServices.CameraControl != null && ThreadServices.CameraControl.Camera != null ? ThreadServices.CameraControl.Camera : Camera.main;
            if (cam == null) { m_notes.Add("-perf-rt: no camera"); return; }
            var desc = new RenderTextureDescriptor(w, h, GraphicsFormat.R16G16B16A16_SFloat, CoreUtils.GetDefaultDepthStencilFormat(), 0)
            {
                msaaSamples = Math.Max(1, ThreadConstants.Msaa), useMipMap = false, autoGenerateMips = false, useDynamicScale = false,
            };
            m_perfRt = new RenderTexture(desc) { name = "ThreadPerf_RT" };
            m_perfRt.Create();
            m_perfCam = cam;
            cam.targetTexture = m_perfRt;
            m_perfRtSize = w + "x" + h;
            m_notes.Add("camera rendered offscreen at " + m_perfRtSize + " (the window shows no picture)");
        }

        void ReleasePerfRt()
        {
            if (m_perfCam != null && m_perfCam.targetTexture == m_perfRt) m_perfCam.targetTexture = null;
            if (m_perfRt != null) { m_perfRt.Release(); Destroy(m_perfRt); m_perfRt = null; }
        }

        void Sample()
        {
            m_seen++;
            float frameMs = Time.unscaledDeltaTime * 1000f;
            if (frameMs > m_maxMs) m_maxMs = frameMs;
            if (frameMs > FrameBudgetMs) m_overBudget++;

            FrameTimingManager.CaptureFrameTimings();
            float gpuMs = -1f, cpuMain = -1f;
            if (FrameTimingManager.GetLatestTimings(1, m_ft) > 0)
            {
                gpuMs = (float)m_ft[0].gpuFrameTime;
                cpuMain = (float)m_ft[0].cpuMainThreadFrameTime;
                if (gpuMs > 0) m_gpuFromFtm = true;
            }
            if (!(gpuMs > 0) && m_gpu.Valid) gpuMs = m_gpu.LastValue * 1e-6f;
            if (m_cpuMain.Valid) cpuMain = m_cpuMain.LastValue * 1e-6f;

            long gc;
            if (m_gc.Valid) { gc = m_gc.LastValue; m_gcRecorderValid = true; }
            else
            {
                long mono = Profiler.GetMonoUsedSizeLong();
                gc = Math.Max(0, mono - m_lastMono);
                m_lastMono = mono;
            }
            m_gcTotal += gc;

            float gfxMB;
            if (m_gfxMem.Valid) gfxMB = m_gfxMem.LastValue / (1024f * 1024f);
            else
            {
                if (m_seen % GfxMemFallbackEvery == 1) m_lastGfxMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);
                gfxMB = m_lastGfxMB;
            }

            if (m_count >= m_rows.Length) return;
            m_rows[m_count++] = new Row
            {
                tReal = (float)(Time.realtimeSinceStartupAsDouble - m_recStartReal),
                tVirt = (float)(Time.timeAsDouble - m_recStartVirt),
                s = (float)ProgressDirector.S,
                frameMs = frameMs,
                cpuMainMs = cpuMain,
                cpuRenderMs = m_cpuRender.Valid ? m_cpuRender.LastValue * 1e-6f : -1f,
                cpuTotalMs = m_cpuTotal.Valid ? m_cpuTotal.LastValue * 1e-6f : -1f,
                gpuMs = gpuMs,
                gfxMemMB = gfxMB,
                draws = m_draw.Valid ? (int)m_draw.LastValue : -1,
                setPass = m_setPass.Valid ? (int)m_setPass.LastValue : -1,
                tris = m_tris.Valid ? (int)Math.Min(int.MaxValue, m_tris.LastValue) : -1,
                verts = m_verts.Valid ? (int)Math.Min(int.MaxValue, m_verts.LastValue) : -1,
                gcAlloc = (int)Math.Min(int.MaxValue, gc),
            };
        }

        /// <summary>Writes the CSV and the summary once (idempotent).</summary>
        public void Flush()
        {
            if (m_flushed || !m_recordRequested) return;
            m_flushed = true;
            if (!m_recording || m_count == 0)
            {
                Debug.LogWarning("[THREAD] perf: nothing recorded (the load fade never finished?)");
                WriteSummary(new PerfSummary());
                return;
            }
            try { WriteCsv(); }
            catch (Exception e) { Debug.LogWarning("[THREAD] perf CSV failed: " + e.Message); }
            var ms = new float[m_count];
            for (int i = 0; i < m_count; i++) ms[i] = m_rows[i].frameMs;
            PerfSummary sum = PerfSummary.From(ms, m_count, FrameBudgetMs);
            WriteSummary(sum);
            ReleasePerfRt();
            var inv = CultureInfo.InvariantCulture;
            Debug.Log("[THREAD] PERF frames=" + sum.frames + " avgFps=" + sum.avgFps.ToString("0.0", inv) + " low1=" + sum.low1Fps.ToString("0.0", inv)
                      + " low0.1=" + sum.low01Fps.ToString("0.0", inv) + " maxMs=" + sum.maxMs.ToString("0.00", inv) + " over33=" + m_overBudget
                      + " gcAlloc=" + m_gcTotal + "B" + (m_perfRtSize != null ? " rt=" + m_perfRtSize : "") + " -> " + m_path);
        }

        void WriteCsv()
        {
            string dir = Path.GetDirectoryName(m_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var inv = CultureInfo.InvariantCulture;
            using (var w = new StreamWriter(m_path, false, new UTF8Encoding(false), 1 << 16))
            {
                w.WriteLine("frame,t_real,t_virtual,s,frame_ms,cpu_main_ms,cpu_render_ms,cpu_total_ms,gpu_ms,draw_calls,setpass_calls,triangles,vertices,gc_alloc_bytes,gfx_mem_mb");
                var sb = new StringBuilder(256);
                for (int i = 0; i < m_count; i++)
                {
                    Row r = m_rows[i];
                    sb.Length = 0;
                    sb.Append(i).Append(',').Append(r.tReal.ToString("0.0000", inv)).Append(',').Append(r.tVirt.ToString("0.0000", inv)).Append(',')
                      .Append(r.s.ToString("0.000", inv)).Append(',').Append(r.frameMs.ToString("0.000", inv)).Append(',')
                      .Append(r.cpuMainMs.ToString("0.000", inv)).Append(',').Append(r.cpuRenderMs.ToString("0.000", inv)).Append(',')
                      .Append(r.cpuTotalMs.ToString("0.000", inv)).Append(',').Append(r.gpuMs.ToString("0.000", inv)).Append(',')
                      .Append(r.draws).Append(',').Append(r.setPass).Append(',').Append(r.tris).Append(',').Append(r.verts).Append(',')
                      .Append(r.gcAlloc).Append(',').Append(r.gfxMemMB.ToString("0.0", inv));
                    w.WriteLine(sb.ToString());
                }
            }
        }

        void WriteSummary(PerfSummary sum)
        {
            double gpuSum = 0, gpuMax = 0, drawSum = 0, setSum = 0, triSum = 0, cpuSum = 0, gfxMax = 0;
            int gpuN = 0, drawN = 0, cpuN = 0, drawMax = 0, setMax = 0, triMax = 0;
            for (int i = 0; i < m_count; i++)
            {
                Row r = m_rows[i];
                if (r.gpuMs > 0) { gpuSum += r.gpuMs; gpuN++; if (r.gpuMs > gpuMax) gpuMax = r.gpuMs; }
                if (r.cpuMainMs > 0) { cpuSum += r.cpuMainMs; cpuN++; }
                if (r.draws >= 0) { drawSum += r.draws; setSum += r.setPass; triSum += r.tris; drawN++; drawMax = Math.Max(drawMax, r.draws); setMax = Math.Max(setMax, r.setPass); triMax = Math.Max(triMax, r.tris); }
                if (r.gfxMemMB > gfxMax) gfxMax = r.gfxMemMB;
            }
            var j = new QaJson();
            j.BeginObject();
            j.Prop("tool", "Megastructure.QA.PerfMonitor").Prop("csv", m_path).Prop("finished", QaFiles.IsoNow());
            QaEnvironment.Write(j);
            j.Prop("mode", QaMode.Current.ToString()).Prop("offscreenRt", m_perfRtSize);
            j.Prop("frameTimingEnabled", FrameTimingManager.IsFeatureEnabled()).Prop("gpuFromFrameTimingManager", m_gpuFromFtm).Prop("gcFromRecorder", m_gcRecorderValid);
            j.Prop("framesSeen", m_seen).Prop("framesStored", m_count).Prop("truncated", m_seen > m_count);
            j.Prop("seconds", sum.totalSeconds).Prop("avgMs", sum.avgMs).Prop("avgFps", sum.avgFps);
            j.Prop("p99Ms", sum.p99Ms).Prop("p999Ms", sum.p999Ms).Prop("low1Fps", sum.low1Fps).Prop("low01Fps", sum.low01Fps);
            j.Prop("maxFrameMs", Math.Max(sum.maxMs, m_maxMs)).Prop("framesAbove33ms", m_overBudget).Prop("budgetMs", FrameBudgetMs);
            j.Prop("gcAllocTotalBytes", m_gcTotal).Prop("gcCollections", GC.CollectionCount(0) - m_gcCount0);
            j.Prop("gpuAvgMs", gpuN > 0 ? gpuSum / gpuN : double.NaN).Prop("gpuMaxMs", gpuN > 0 ? gpuMax : double.NaN);
            j.Prop("cpuMainAvgMs", cpuN > 0 ? cpuSum / cpuN : double.NaN);
            j.Prop("drawCallsAvg", drawN > 0 ? drawSum / drawN : double.NaN).Prop("drawCallsMax", drawMax);
            j.Prop("setPassAvg", drawN > 0 ? setSum / drawN : double.NaN).Prop("setPassMax", setMax);
            j.Prop("trianglesAvg", drawN > 0 ? triSum / drawN : double.NaN).Prop("trianglesMax", triMax);
            j.Prop("gfxMemMaxMB", gfxMax);
            bool k4 = m_perfRtSize != null && m_perfRtSize.StartsWith("3840", StringComparison.Ordinal);
            j.Key("targets").BeginObject(true);
            j.Prop("avgFps", k4 ? Target4kAvgFps : TargetAvgFps).Prop("low1Fps", k4 ? double.NaN : TargetLow1Fps).Prop("maxFrameMs", FrameBudgetMs);
            j.Prop("avgOk", sum.avgFps >= (k4 ? Target4kAvgFps : TargetAvgFps)).Prop("low1Ok", k4 || sum.low1Fps >= TargetLow1Fps).Prop("noFrameAbove33ms", m_overBudget == 0);
            j.EndObject();
            j.Prop("notes", m_notes);
            j.EndObject();
            string path = string.IsNullOrEmpty(m_path) ? QaPaths.ReportPath("perf.summary.json") : Path.Combine(Path.GetDirectoryName(m_path) ?? "", Path.GetFileNameWithoutExtension(m_path) + ".summary.json");
            QaFiles.WriteText(path, j.ToString());
        }
    }
}
