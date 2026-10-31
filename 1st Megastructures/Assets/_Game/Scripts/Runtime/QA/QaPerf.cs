using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Megastructure
{
    /// <summary>
    /// -perf-log FILE (Part 11.7, Part 10 items 23 and 24): per-frame CPU / GPU frame time (FrameTimingManager), real frame
    /// delta, draw calls, SetPass calls, batches, triangles, GC allocation in the frame, system and graphics memory
    /// (ProfilerRecorder), scene time, d and beat, recorded allocation-free into a preallocated buffer from the end of the load.
    /// At the end it writes FILE (CSV) and FILE_summary.json: average / 1 % low / 0.1 % low FPS, frame-time percentiles,
    /// max frame time and frames above 33 ms after the load fade (the first frame with the picture fully on), GC allocation per
    /// frame in steady state (5 s after the fade), per-beat breakdown, and the 11.7 targets (1080p: avg 90, 1 % low 60, no frame
    /// above 33 ms; 2160p: avg 60). VSync is off (QualityBootstrap perf mode). -res WxH larger than the window renders the
    /// camera into a RenderTexture of that size for the whole run (4K on a smaller monitor).
    /// FrameTimingManager needs Player Settings "Frame Timing Stats"; without it the GPU columns are 0 (reported).
    /// </summary>
    public sealed class QaPerf : IDisposable
    {
        struct Sample
        {
            public float Dt, Cpu, Gpu, CpuMain, CpuRender, SceneT, D;
            public int Draw, SetPass, Batches;
            public long Tris, Gc;
            /// <summary>Managed heap in use (GC.GetTotalMemory(false)) and gen-0 collection count after the frame: the GC measure of
            /// non-development players, where the profiler counter "GC Allocated In Frame" is not available (Gc = -1).</summary>
            public long Heap;
            public int Gcc;
            public float SysMB, GfxMB;
            public byte Beat;
            public bool AfterFade, Steady;
        }

        public const int Capacity = 400000;
        public const float FrameLimitMs = 33f;
        public const float SteadyDelay = 5f;

        readonly QaHarness _h;
        readonly Sample[] _s = new Sample[Capacity];
        readonly FrameTiming[] _ft = new FrameTiming[1];
        int _n;
        bool _truncated, _started, _fadeSeen;
        float _fadeReal = -1f;
        ProfilerRecorder _draw, _setPass, _batches, _tris, _gc, _sys, _gfx;
        RenderTexture _rt;
        int _renderW, _renderH;
        bool _viaRt;
        bool _timingAvailable;

        public QaPerf(QaHarness h) { _h = h; }

        void Begin()
        {
            _started = true;
            _draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _sys = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gfx = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            _timingAvailable = FrameTimingManager.IsFeatureEnabled();

            _renderW = Screen.width; _renderH = Screen.height;
            if (CliArgs.Res(out int w, out int hgt) && (Screen.width < w || Screen.height < hgt))
            {
                Camera cam = GameState.MainCamera;
                if (cam != null)
                {
                    var desc = new RenderTextureDescriptor(w, hgt, GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormat.D32_SFloat_S8_UInt)
                    {
                        msaaSamples = QaRenderer.AssetMsaa, useMipMap = false, autoGenerateMips = false,
                    };
                    _rt = new RenderTexture(desc) { name = "QA_PerfTarget" };
                    _rt.Create();
                    cam.targetTexture = _rt;
                    _viaRt = true;
                    _renderW = w; _renderH = hgt;
                    Debug.Log("[ABYSS] QA perf: window " + Screen.width + "x" + Screen.height + " is smaller than -res " + w + "x" + hgt + ", rendering into a RenderTexture");
                }
            }
            Debug.Log("[ABYSS] QA perf: recording at " + _renderW + "x" + _renderH + ", frame timing " + (_timingAvailable ? "on" : "OFF (enable Frame Timing Stats)"));
        }

        /// <summary>One sample per frame (harness LateUpdate). Allocation-free.</summary>
        public void LateTick()
        {
            if (!GameBootstrap.Loaded) return;
            if (!_started) Begin();
            if (_n >= Capacity) { _truncated = true; return; }

            if (!_fadeSeen && GameState.PictureFade >= 0.999f) { _fadeSeen = true; _fadeReal = Time.realtimeSinceStartup; }

            float cpu = 0f, gpu = 0f, cpuMain = 0f, cpuRender = 0f;
            if (_timingAvailable)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _ft) > 0)
                {
                    cpu = (float)_ft[0].cpuFrameTime;
                    gpu = (float)_ft[0].gpuFrameTime;
                    cpuMain = (float)_ft[0].cpuMainThreadFrameTime;
                    cpuRender = (float)_ft[0].cpuRenderThreadFrameTime;
                }
            }
            ref Sample s = ref _s[_n++];
            s.Dt = Time.unscaledDeltaTime * 1000f;
            s.Cpu = cpu; s.Gpu = gpu; s.CpuMain = cpuMain; s.CpuRender = cpuRender;
            s.SceneT = (float)SceneClock.Time;
            s.D = GameState.D;
            s.Beat = (byte)GameState.Beat;
            s.Draw = _draw.Valid ? (int)_draw.LastValue : -1;
            s.SetPass = _setPass.Valid ? (int)_setPass.LastValue : -1;
            s.Batches = _batches.Valid ? (int)_batches.LastValue : -1;
            s.Tris = _tris.Valid ? _tris.LastValue : -1;
            s.Gc = _gc.Valid ? _gc.LastValue : -1;
            s.Heap = GC.GetTotalMemory(false);   // allocation-free: reads the heap counters
            s.Gcc = GC.CollectionCount(0);
            s.SysMB = _sys.Valid ? _sys.LastValue / (1024f * 1024f) : -1f;
            s.GfxMB = _gfx.Valid ? _gfx.LastValue / (1024f * 1024f) : -1f;
            s.AfterFade = _fadeSeen;
            s.Steady = _fadeSeen && Time.realtimeSinceStartup - _fadeReal >= SteadyDelay;
        }

        /// <summary>Finalizer: write the CSV and the summary; exit code 1 when an 11.7 target is missed.</summary>
        public int Finish(bool aborted)
        {
            string csv = QaPaths.Resolve(CliArgs.PerfLog ?? Path.Combine(QaPaths.OutDir, "perf.csv"));
            if (string.IsNullOrEmpty(Path.GetExtension(csv))) csv += ".csv";
            QaPaths.EnsureParent(csv);
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(_n * 96 + 256);
            sb.Append("frame,scene_time,d,beat,dt_ms,cpu_ms,gpu_ms,cpu_main_ms,cpu_render_ms,draw_calls,setpass_calls,batches,triangles,gc_alloc_bytes,system_mb,gfx_mb,after_fade,steady,heap_bytes,gc_collections\n");
            for (int i = 0; i < _n; i++)
            {
                ref Sample s = ref _s[i];
                sb.Append(i).Append(',').Append(s.SceneT.ToString("F3", inv)).Append(',').Append(s.D.ToString("F2", inv)).Append(',').Append(s.Beat).Append(',')
                  .Append(s.Dt.ToString("F3", inv)).Append(',').Append(s.Cpu.ToString("F3", inv)).Append(',').Append(s.Gpu.ToString("F3", inv)).Append(',')
                  .Append(s.CpuMain.ToString("F3", inv)).Append(',').Append(s.CpuRender.ToString("F3", inv)).Append(',')
                  .Append(s.Draw).Append(',').Append(s.SetPass).Append(',').Append(s.Batches).Append(',').Append(s.Tris).Append(',').Append(s.Gc).Append(',')
                  .Append(s.SysMB.ToString("F1", inv)).Append(',').Append(s.GfxMB.ToString("F1", inv)).Append(',')
                  .Append(s.AfterFade ? 1 : 0).Append(',').Append(s.Steady ? 1 : 0).Append(',')
                  .Append(s.Heap).Append(',').Append(s.Gcc).Append('\n');
            }
            File.WriteAllText(csv, sb.ToString(), new UTF8Encoding(false));

            // ---- statistics over the frames after the load fade
            int first = 0;
            while (first < _n && !_s[first].AfterFade) first++;
            int m = _n - first;
            var dts = new float[Math.Max(0, m)];
            double sumDt = 0, sumGpu = 0, sumCpu = 0; int gpuN = 0;
            float maxDt = 0f; int maxIdx = -1, over = 0;
            long drawSum = 0, drawMax = 0, setSum = 0, setMax = 0, batSum = 0, batMax = 0, triSum = 0, triMax = 0; int rN = 0;
            float gpuMax = 0f;
            for (int i = 0; i < m; i++)
            {
                ref Sample s = ref _s[first + i];
                dts[i] = s.Dt;
                sumDt += s.Dt;
                if (s.Dt > maxDt) { maxDt = s.Dt; maxIdx = first + i; }
                if (s.Dt > FrameLimitMs) over++;
                if (s.Gpu > 0f) { sumGpu += s.Gpu; gpuN++; if (s.Gpu > gpuMax) gpuMax = s.Gpu; }
                sumCpu += s.Cpu;
                if (s.Draw >= 0)
                {
                    rN++;
                    drawSum += s.Draw; if (s.Draw > drawMax) drawMax = s.Draw;
                    setSum += s.SetPass; if (s.SetPass > setMax) setMax = s.SetPass;
                    batSum += s.Batches; if (s.Batches > batMax) batMax = s.Batches;
                    triSum += s.Tris; if (s.Tris > triMax) triMax = s.Tris;
                }
            }
            Array.Sort(dts);
            double avgMs = m > 0 ? sumDt / m : double.NaN;
            double avgFps = m > 0 && sumDt > 0 ? 1000.0 * m / sumDt : double.NaN;
            double low1 = LowFps(dts, 0.01), low01 = LowFps(dts, 0.001);
            double p50 = Pct(dts, 0.50), p99 = Pct(dts, 0.99), p999 = Pct(dts, 0.999);

            // GC in steady state
            int steadyN = 0, allocFrames = 0; long gcSum = 0, gcMax = 0;
            // fallback for non-development players (the profiler counter is -1 there): managed heap growth between consecutive
            // steady frames without a collection, the frames with growth, and gen-0 collections over the steady state
            int heapN = 0, heapGrowFrames = 0, gccFirst = -1, gccLast = -1, prevGcc = -1;
            long heapGrow = 0, heapGrowMax = 0, prevHeap = -1;
            float memFirst = -1f, memLast = -1f, memMax = -1f, gfxLast = -1f, gfxMax = -1f;
            for (int i = 0; i < _n; i++)
            {
                ref Sample s = ref _s[i];
                if (s.SysMB >= 0f)
                {
                    if (memFirst < 0f && s.AfterFade) memFirst = s.SysMB;
                    memLast = s.SysMB;
                    if (s.SysMB > memMax) memMax = s.SysMB;
                }
                if (s.GfxMB >= 0f) { gfxLast = s.GfxMB; if (s.GfxMB > gfxMax) gfxMax = s.GfxMB; }
                if (s.Steady)
                {
                    heapN++;
                    if (gccFirst < 0) gccFirst = s.Gcc;
                    gccLast = s.Gcc;
                    if (prevHeap >= 0 && s.Gcc == prevGcc && s.Heap > prevHeap)
                    {
                        long g = s.Heap - prevHeap;
                        heapGrow += g;
                        heapGrowFrames++;
                        if (g > heapGrowMax) heapGrowMax = g;
                    }
                    prevHeap = s.Heap;
                    prevGcc = s.Gcc;
                }
                if (!s.Steady || s.Gc < 0) continue;
                steadyN++;
                gcSum += s.Gc;
                if (s.Gc > 0) allocFrames++;
                if (s.Gc > gcMax) gcMax = s.Gc;
            }

            bool is4K = _renderH >= 2160;
            double avgTarget = is4K ? 60.0 : 90.0;
            bool avgOk = avgFps >= avgTarget;
            bool lowOk = is4K || low1 >= 60.0;
            bool overOk = is4K || over == 0;
            bool pass = m > 0 && avgOk && lowOk && overOk && !aborted;

            var j = new QaJson();
            j.BeginObject();
            _h.WriteHeader(j, "perf");
            j.Prop("csv", csv);
            j.Prop("render_size", _renderW + "x" + _renderH);
            j.Prop("render_via_rt", _viaRt);
            j.Prop("vsync_count", QualitySettings.vSyncCount);
            j.Prop("target_frame_rate", Application.targetFrameRate);
            j.Prop("frame_timing_available", _timingAvailable);
            j.Prop("frames_total", _n);
            j.Prop("frames_after_fade", m);
            j.Prop("truncated", _truncated);
            j.Prop("aborted", aborted);
            j.Obj("fps");
            j.Prop("avg", avgFps);
            j.Prop("low_1pct", low1);
            j.Prop("low_0_1pct", low01);
            j.Prop("from_p99", p99 > 0 ? 1000.0 / p99 : double.NaN);
            j.EndObject();
            j.Obj("frame_ms");
            j.Prop("avg", avgMs);
            j.Prop("p50", p50);
            j.Prop("p99", p99);
            j.Prop("p99_9", p999);
            j.Prop("max_after_fade", maxDt);
            if (maxIdx >= 0)
            {
                j.Prop("max_at_frame", maxIdx);
                j.Prop("max_at_scene_time", _s[maxIdx].SceneT);
                j.Prop("max_at_d", _s[maxIdx].D);
                j.Prop("max_at_beat", (int)_s[maxIdx].Beat);
            }
            j.Prop("frames_over_33ms", over);
            j.Prop("cpu_avg", m > 0 ? sumCpu / m : double.NaN);
            j.Prop("gpu_avg", gpuN > 0 ? sumGpu / gpuN : double.NaN);
            j.Prop("gpu_max", gpuMax);
            j.EndObject();
            j.Obj("render");
            j.Prop("samples", rN);
            j.Prop("draw_calls_avg", rN > 0 ? (double)drawSum / rN : double.NaN);
            j.Prop("draw_calls_max", drawMax);
            j.Prop("setpass_avg", rN > 0 ? (double)setSum / rN : double.NaN);
            j.Prop("setpass_max", setMax);
            j.Prop("batches_avg", rN > 0 ? (double)batSum / rN : double.NaN);
            j.Prop("batches_max", batMax);
            j.Prop("triangles_avg", rN > 0 ? (double)triSum / rN : double.NaN);
            j.Prop("triangles_max", triMax);
            j.EndObject();
            j.Obj("gc_steady");
            j.Prop("frames", steadyN);
            j.Prop("frames_with_alloc", allocFrames);
            j.Prop("bytes_per_frame_avg", steadyN > 0 ? (double)gcSum / steadyN : double.NaN);
            j.Prop("bytes_max", gcMax);
            // a non-development player has no "GC Allocated In Frame" counter (gc_alloc_bytes -1): zero GC is then judged on the
            // managed heap: no growth between steady frames and no gen-0 collection over the steady state (the heap counter moves
            // in allocator chunks, so per-frame garbage shows as growth steps or collections, not on every frame)
            bool profilerGc = steadyN > 0;
            int collections = gccFirst >= 0 ? gccLast - gccFirst : -1;
            j.Prop("method", profilerGc ? "ProfilerRecorder 'GC Allocated In Frame' (development player)"
                : "managed heap: GC.GetTotalMemory(false) growth between steady frames and GC.CollectionCount(0) (no profiler GC counter in this player)");
            j.Prop("heap_frames", heapN);
            j.Prop("heap_growth_bytes", heapGrow);
            j.Prop("heap_growth_frames", heapGrowFrames);
            j.Prop("heap_growth_max_step_bytes", heapGrowMax);
            j.Prop("heap_bytes_per_frame_avg", heapN > 1 ? (double)heapGrow / (heapN - 1) : double.NaN);
            j.Prop("gen0_collections", collections);
            j.Prop("pass", profilerGc ? allocFrames == 0 : heapN > 0 && heapGrowFrames == 0 && collections == 0);
            j.EndObject();
            j.Obj("memory_mb");
            j.Prop("system_after_fade", memFirst);
            j.Prop("system_end", memLast);
            j.Prop("system_max", memMax);
            j.Prop("gfx_end", gfxLast);
            j.Prop("gfx_max", gfxMax);
            j.EndObject();
            WriteBeats(j, first);
            j.Obj("targets");
            j.Prop("resolution_class", is4K ? "2160p" : "1080p");
            j.Prop("avg_fps_min", avgTarget);
            j.Prop("avg_pass", avgOk);
            j.Prop("low_1pct_min", is4K ? double.NaN : 60.0);
            j.Prop("low_1pct_pass", lowOk);
            j.Prop("max_frame_ms", is4K ? double.NaN : FrameLimitMs);
            j.Prop("no_frame_over_33ms_pass", overOk);
            j.EndObject();
            j.Prop("pass", pass);
            j.EndObject();
            string summary = Path.Combine(Path.GetDirectoryName(csv) ?? QaPaths.OutDir, Path.GetFileNameWithoutExtension(csv) + "_summary.json");
            j.Save(summary);
            Debug.Log("[ABYSS] QA perf " + (pass ? "PASS" : "FAIL") + ": avg " + QaJson.F(avgFps, 1) + " fps, 1% low " + QaJson.F(low1, 1)
                      + ", max " + QaJson.F(maxDt, 1) + " ms, " + over + " frames > 33 ms -> " + summary);
            return pass ? 0 : 1;
        }

        void WriteBeats(QaJson j, int first)
        {
            j.Arr("beats");
            for (int b = 1; b <= 7; b++)
            {
                double sum = 0; int n = 0, over = 0; float mx = 0f; long draw = 0; int dn = 0;
                for (int i = first; i < _n; i++)
                {
                    ref Sample s = ref _s[i];
                    if (s.Beat != b) continue;
                    n++; sum += s.Dt;
                    if (s.Dt > mx) mx = s.Dt;
                    if (s.Dt > FrameLimitMs) over++;
                    if (s.Draw >= 0) { draw += s.Draw; dn++; }
                }
                j.BeginObject();
                j.Prop("beat", b);
                j.Prop("frames", n);
                j.Prop("avg_fps", n > 0 && sum > 0 ? 1000.0 * n / sum : double.NaN);
                j.Prop("max_frame_ms", mx);
                j.Prop("frames_over_33ms", over);
                j.Prop("draw_calls_avg", dn > 0 ? (double)draw / dn : double.NaN);
                j.EndObject();
            }
            j.EndArray();
        }

        /// <summary>FPS from the mean of the slowest fraction of frames (sorted ascending frame times).</summary>
        static double LowFps(float[] sorted, double fraction)
        {
            int n = sorted.Length;
            if (n == 0) return double.NaN;
            int k = Math.Max(1, (int)Math.Ceiling(n * fraction));
            double s = 0;
            for (int i = n - k; i < n; i++) s += sorted[i];
            return s > 0 ? 1000.0 * k / s : double.NaN;
        }

        static double Pct(float[] sorted, double q)
        {
            int n = sorted.Length;
            if (n == 0) return double.NaN;
            int i = Math.Min(n - 1, Math.Max(0, (int)Math.Floor(q * (n - 1) + 0.5)));
            return sorted[i];
        }

        public void Dispose()
        {
            if (_started)
            {
                _started = false;
                _draw.Dispose(); _setPass.Dispose(); _batches.Dispose(); _tris.Dispose(); _gc.Dispose(); _sys.Dispose(); _gfx.Dispose();
            }
            if (_rt != null)
            {
                Camera cam = GameState.MainCamera;
                if (cam != null && cam.targetTexture == _rt) cam.targetTexture = null;
                _rt.Release();
                UnityEngine.Object.Destroy(_rt);
                _rt = null;
            }
        }
    }
}
