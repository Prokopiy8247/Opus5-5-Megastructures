using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megastructure
{
    /// <summary>
    /// Player performance log (Part 11.7) and soak test (Part 11.8), active with -perf-log FILE, -perf or -soak SECONDS.
    /// Per frame (zero allocations, preallocated growing arrays): unscaled frame time, GPU frame time (FrameTimingManager;
    /// needs Frame Timing Stats in the player settings), ProfilerRecorders Draw Calls Count, SetPass Calls Count,
    /// Triangles Count (Render), GC Allocated In Frame and System Used Memory (Memory; some counters exist only in
    /// development players and are then reported as unavailable), progress s and beat. At quit it writes FILE (CSV) and
    /// FILE.summary.json: average, 1 % low and 0.1 % low, max frame after the load fade, frames above 33 ms, GC count
    /// delta, maxima in the pre-warm windows s 570-580, 774-782, 806-812 (14.6). -soak runs the cinematic loop
    /// (restart from s 0 when the ending completes; start the player with -cinematic) and samples memory every 10 s,
    /// then quits with 0 (1 if memory grew more than -soak-growth-mb, default 256 MB, or an error was logged).
    /// Storage is static so a scene reload (every restart path reloads or rebuilds) does not lose the samples. Each
    /// restart re-arms the load fade before it runs: the frames until the next LoadComplete plus its 3.1 s fade-in are
    /// recorded with post_fade 0 and counted in restarts_excluded_frames. Without Frame Timing Stats in the player
    /// settings the summary says gpu_frame_time "unavailable" (and a warning is logged once).
    /// </summary>
    [DefaultExecutionOrder(30030)]
    public sealed class PerfMonitor : MonoBehaviour
    {
        // 14.2.5 / 14.11 timing: the picture is fully faded in at 3.1 s after the load
        const float FadeEnd = RingConstants.FadeInEnd;

        static class Store
        {
            public static int N;
            public static float[] T = new float[0], Dt = new float[0], Gpu = new float[0], S = new float[0];
            public static int[] Beat = new int[0];
            public static long[] Draw = new long[0], SetPass = new long[0], Tris = new long[0], GcAlloc = new long[0], SysMem = new long[0];
            public static bool[] PostFade = new bool[0];
            public static double StartReal = -1, LoadReal = -1;
            public static int Gc0 = -1;
            public static int Restarts;
            /// <summary>True from a soak restart until the next LoadComplete: every frame of the reload / rebuild is
            /// recorded as pre-fade (excluded from the post-fade statistics), whichever restart path ran.</summary>
            public static bool Reloading;
            /// <summary>Frames recorded as pre-fade after the first load fade had ended (the restart reloads and their fades).</summary>
            public static int RestartExcluded;
            public static bool EverPostFade;
            public static float NextMem;
            public static bool GpuWarned;
            public static int MemN;
            public static float[] MemT = new float[0];
            public static long[] MemSys = new long[0], MemMono = new long[0];
            public static string RestartHow = "";
            public static bool Written;

            public static void Ensure(int need)
            {
                if (need <= T.Length) return;
                int cap = Math.Max(need, Math.Max(65536, T.Length * 2));
                Array.Resize(ref T, cap); Array.Resize(ref Dt, cap); Array.Resize(ref Gpu, cap); Array.Resize(ref S, cap);
                Array.Resize(ref Beat, cap); Array.Resize(ref Draw, cap); Array.Resize(ref SetPass, cap); Array.Resize(ref Tris, cap);
                Array.Resize(ref GcAlloc, cap); Array.Resize(ref SysMem, cap); Array.Resize(ref PostFade, cap);
            }

            public static void EnsureMem(int need)
            {
                if (need <= MemT.Length) return;
                int cap = Math.Max(need, Math.Max(512, MemT.Length * 2));
                Array.Resize(ref MemT, cap); Array.Resize(ref MemSys, cap); Array.Resize(ref MemMono, cap);
            }
        }

        ProfilerRecorder _draw, _setPass, _tris, _gcAlloc, _sysMem;
        readonly FrameTiming[] _timing = new FrameTiming[1];
        string _path;
        float _soak = -1f;
        bool _loaded, _restartPending, _started, _cinematicAsked, _quitting, _sawUnready;
        Action _onLoad, _onEnding;

        void Awake()
        {
            bool active = CliArgs.PerfMode || CliArgs.Soak > 0f;
            if (!active) { enabled = false; return; }
            _path = QaUtil.Resolve(CliArgs.PerfLog ?? (CliArgs.Soak > 0f ? "QA/soak.csv" : "QA/perf.csv"));
            _soak = CliArgs.Soak;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            if (Store.StartReal < 0)
            {
                Store.StartReal = Time.realtimeSinceStartupAsDouble;
                // preallocate (about 200 fps over the soak) so the per-frame path never grows the arrays
                Store.Ensure(65536 + (_soak > 0f ? (int)Mathf.Min(_soak * 200f, 4e6f) : 0));
                Store.EnsureMem(512 + (_soak > 0f ? (int)(_soak / 10f) : 0));
            }
            // Part 11.7 needs the GPU frame time in the built player: FrameTimingManager returns nothing in a release
            // player unless Player Settings > Frame Timing Stats is on (development players and the Editor always have it)
            if (!Store.GpuWarned && !FrameTimingManager.IsFeatureEnabled())
            {
                Store.GpuWarned = true;
                Debug.LogWarning("[Ring] PerfMonitor: Frame Timing Stats is off (PlayerSettings.enableFrameTimingStats); the GPU frame time is unavailable in this player");
            }
            _onLoad = OnLoad;
            _onEnding = OnEnding;
        }

        void OnEnable()
        {
            if (_path == null) return;
            _draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _sysMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _started = true;
            RingEvents.LoadComplete -= _onLoad; RingEvents.LoadComplete += _onLoad;
            RingEvents.EndingComplete -= _onEnding; RingEvents.EndingComplete += _onEnding;
        }

        void OnDisable()
        {
            if (!_started) return;
            _started = false;
            _draw.Dispose(); _setPass.Dispose(); _tris.Dispose(); _gcAlloc.Dispose(); _sysMem.Dispose();
            RingEvents.LoadComplete -= _onLoad;
            RingEvents.EndingComplete -= _onEnding;
        }

        void OnLoad()
        {
            // the first load, and every load after a soak restart (Restart() re-arms LoadReal and sets Reloading)
            if (Store.LoadReal < 0 || Store.Reloading) Store.LoadReal = Time.realtimeSinceStartupAsDouble;
            Store.Reloading = false;
            if (Store.Gc0 < 0) Store.Gc0 = GC.CollectionCount(0);
            if (_soak > 0f && !_cinematicAsked)
            {
                // the soak runs the cinematic loop from s 0 (Part 11.8); after a restart the reloaded scene starts in play mode
                _cinematicAsked = true;
                RingQaHooks.TryStartCinematic(out string how);
                if (Store.Restarts == 0) Store.RestartHow = "cinematic via " + how;
            }
        }

        void OnEnding()
        {
            if (_soak > 0f) _restartPending = true;
        }

        void Update()
        {
            if (_path == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!_loaded && (RingRig.WorldReady || Store.LoadReal >= 0)) { _loaded = true; OnLoad(); RingEvents.EndingComplete -= _onEnding; RingEvents.EndingComplete += _onEnding; }
            // backup for a restart whose LoadComplete this instance did not receive: WorldReady went false (the reloaded
            // bootstrap reset it) and is true again
            if (Store.Reloading)
            {
                if (!RingRig.WorldReady) _sawUnready = true;
                else if (_sawUnready) { _sawUnready = false; OnLoad(); }
            }

            int i = Store.N;
            Store.Ensure(i + 1);
            Store.T[i] = (float)(now - Store.StartReal);
            Store.Dt[i] = Time.unscaledDeltaTime * 1000f;
            FrameTimingManager.CaptureFrameTimings();
            Store.Gpu[i] = FrameTimingManager.GetLatestTimings(1, _timing) > 0 ? (float)_timing[0].gpuFrameTime : float.NaN;
            Store.S[i] = RingState.S;
            Store.Beat[i] = RingState.Beat;
            Store.Draw[i] = _draw.Valid ? _draw.LastValue : -1;
            Store.SetPass[i] = _setPass.Valid ? _setPass.LastValue : -1;
            Store.Tris[i] = _tris.Valid ? _tris.LastValue : -1;
            Store.GcAlloc[i] = _gcAlloc.Valid ? _gcAlloc.LastValue : -1;
            Store.SysMem[i] = _sysMem.Valid ? _sysMem.LastValue : -1;
            bool post = !Store.Reloading && Store.LoadReal >= 0 && now - Store.LoadReal > FadeEnd;
            Store.PostFade[i] = post;
            if (post) Store.EverPostFade = true;
            else if (Store.EverPostFade) Store.RestartExcluded++;
            Store.N = i + 1;

            if (_soak > 0f)
            {
                float t = Store.T[i];
                if (t >= Store.NextMem)
                {
                    Store.NextMem = t + 10f;   // static: the 10 s cadence survives the restart's scene reload
                    int m = Store.MemN;
                    Store.EnsureMem(m + 1);
                    Store.MemT[m] = t;
                    Store.MemSys[m] = _sysMem.Valid ? _sysMem.LastValue : -1;
                    Store.MemMono[m] = GC.GetTotalMemory(false);
                    Store.MemN = m + 1;
                }
                if (_restartPending)
                {
                    _restartPending = false;
                    Restart();
                }
                if (t >= _soak && !_quitting) { _quitting = true; WriteAll(); QaUtil.Quit(SoakCode()); }
            }
        }

        void Restart()
        {
            // re-arm the load fade BEFORE any restart path runs (GameBootstrap.Restart, a wired hook, an in-place
            // rebuild or the scene-reload fallback): every frame until the next LoadComplete, and its 3.1 s fade-in,
            // is recorded as pre-fade, so reload hitches never reach the post-fade statistics (Part 11.7 / 11.8)
            Store.LoadReal = -1;
            Store.Reloading = true;
            Store.Restarts++;
            if (RingQaHooks.TryRestart(out string how))
            {
                Store.RestartHow = how;
                RingQaHooks.TryStartCinematic(out _);
                Debug.Log("[Ring] PerfMonitor soak: restart " + Store.Restarts + " via " + how);
                return;
            }
            // fallback: reload the scene like the pause-menu restart (the statics of this monitor survive; the game rebuilds
            // behind its black fade)
            Store.RestartHow = "SceneManager.LoadScene (as the pause-menu restart)";
            Debug.Log("[Ring] PerfMonitor soak: restart " + Store.Restarts + " by scene reload");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        int SoakCode()
        {
            if (QaLog.ErrorCount > 0) return 1;
            return MemoryGrowthMb() > CliArgs.Float("soak-growth-mb", 256f) ? 1 : 0;
        }

        static double MemoryGrowthMb()
        {
            // growth from the first sample after 60 s (after load and the first warm-up) to the last sample
            int a = -1;
            for (int k = 0; k < Store.MemN; k++) if (Store.MemT[k] >= 60f && Store.MemSys[k] > 0) { a = k; break; }
            int b = Store.MemN - 1;
            if (a < 0 || b <= a) return 0;
            return (Store.MemSys[b] - Store.MemSys[a]) / (1024.0 * 1024.0);
        }

        void OnApplicationQuit() { WriteAll(); }

        void WriteAll()
        {
            if (Store.Written || _path == null) return;
            Store.Written = true;
            try
            {
                var ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder(Store.N * 64 + 256);
                sb.Append("frame,time_s,dt_ms,gpu_ms,s,beat,draw_calls,setpass_calls,triangles,gc_alloc_bytes,sys_mem_mb,post_fade\n");
                for (int i = 0; i < Store.N; i++)
                {
                    sb.Append(i).Append(',').Append(Store.T[i].ToString("0.0000", ci)).Append(',').Append(Store.Dt[i].ToString("0.000", ci)).Append(',')
                      .Append(float.IsNaN(Store.Gpu[i]) ? "" : Store.Gpu[i].ToString("0.000", ci)).Append(',').Append(Store.S[i].ToString("0.000", ci)).Append(',')
                      .Append(Store.Beat[i]).Append(',').Append(Store.Draw[i]).Append(',').Append(Store.SetPass[i]).Append(',').Append(Store.Tris[i]).Append(',')
                      .Append(Store.GcAlloc[i]).Append(',').Append(Store.SysMem[i] < 0 ? "" : (Store.SysMem[i] / (1024.0 * 1024.0)).ToString("0.0", ci)).Append(',')
                      .Append(Store.PostFade[i] ? 1 : 0).Append('\n');
                }
                QaUtil.WriteText(_path, sb.ToString());
                QaUtil.WriteText(_path + ".summary.json", Summary());
                Debug.Log("[Ring] PerfMonitor: wrote " + _path + " (" + Store.N + " frames)");
            }
            catch (Exception e) { Debug.LogError("[Ring] PerfMonitor: write failed: " + e.Message); }
        }

        static string Summary()
        {
            int n = 0, over33 = 0;
            double sum = 0, gpuSum = 0; int gpuN = 0;
            float max = 0f; int maxAt = -1;
            long drawMax = 0, setMax = 0, trisMax = 0, gcMax = 0; double drawSum = 0, setSum = 0, trisSum = 0, gcSum = 0; int rn = 0, gcN = 0, gcFrames = 0;
            var post = new float[Math.Max(1, Store.N)];
            for (int i = 0; i < Store.N; i++)
            {
                if (!Store.PostFade[i]) continue;
                float dt = Store.Dt[i];
                post[n++] = dt;
                sum += dt;
                if (dt > 33.333f) over33++;
                if (dt > max) { max = dt; maxAt = i; }
                if (!float.IsNaN(Store.Gpu[i]) && Store.Gpu[i] > 0f) { gpuSum += Store.Gpu[i]; gpuN++; }
                if (Store.Draw[i] >= 0) { drawSum += Store.Draw[i]; setSum += Store.SetPass[i]; trisSum += Store.Tris[i]; rn++; drawMax = Math.Max(drawMax, Store.Draw[i]); setMax = Math.Max(setMax, Store.SetPass[i]); trisMax = Math.Max(trisMax, Store.Tris[i]); }
                if (Store.GcAlloc[i] >= 0) { gcSum += Store.GcAlloc[i]; gcN++; gcMax = Math.Max(gcMax, Store.GcAlloc[i]); if (Store.GcAlloc[i] > 0) gcFrames++; }
            }
            Array.Sort(post, 0, n);
            double avg = n > 0 ? sum / n : double.NaN;
            double p99 = n > 0 ? post[Math.Min(n - 1, (int)Math.Ceiling(0.99 * n) - 1)] : double.NaN;
            double p999 = n > 0 ? post[Math.Min(n - 1, (int)Math.Ceiling(0.999 * n) - 1)] : double.NaN;
            double low1 = WorstMean(post, n, 0.01), low01 = WorstMean(post, n, 0.001);

            var j = new QaJson();
            j.Obj().KV("finished", QaUtil.Stamp()).KV("frames_total", Store.N).KV("frames_post_fade", n)
             .KV("resolution", Screen.width + "x" + Screen.height).KV("vsync", QualitySettings.vSyncCount).KV("target_fps", Application.targetFrameRate)
             .KV("quality", QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "")
             .KV("gpu", SystemInfo.graphicsDeviceName).KV("frame_timing_stats", FrameTimingManager.IsFeatureEnabled())
             .KV("gpu_frame_time", gpuN > 0 ? "FrameTimingManager"
                 : FrameTimingManager.IsFeatureEnabled() ? "unavailable (FrameTimingManager returned no GPU timings on this device / API)"
                 : "unavailable (Frame Timing Stats off: PlayerSettings.enableFrameTimingStats)")
             .KV("restarts_excluded_frames", Store.RestartExcluded)
             .KV("avg_ms", avg).KV("avg_fps", avg > 0 ? 1000.0 / avg : double.NaN)
             .KV("p99_ms", p99).KV("p999_ms", p999)
             .KV("low1_fps", low1 > 0 ? 1000.0 / low1 : double.NaN).KV("low01_fps", low01 > 0 ? 1000.0 / low01 : double.NaN)
             .KV("low1_definition", "1000 / mean of the slowest 1 % (0.1 %) frame times after the load fade")
             .KV("max_ms", max).KV("max_at_frame", maxAt).KV("max_at_s", maxAt >= 0 ? Store.S[maxAt] : float.NaN)
             .KV("frames_over_33ms", over33)
             .KV("gpu_avg_ms", gpuN > 0 ? gpuSum / gpuN : double.NaN)
             .KV("draw_calls_avg", rn > 0 ? drawSum / rn : double.NaN).KV("draw_calls_max", drawMax)
             .KV("setpass_avg", rn > 0 ? setSum / rn : double.NaN).KV("setpass_max", setMax)
             .KV("triangles_avg", rn > 0 ? trisSum / rn : double.NaN).KV("triangles_max", trisMax)
             .KV("gc_alloc_counter_valid", gcN > 0).KV("gc_alloc_avg_bytes", gcN > 0 ? gcSum / gcN : double.NaN).KV("gc_alloc_max_bytes", gcMax).KV("gc_alloc_frames", gcFrames)
             .KV("gc_collections_delta", Store.Gc0 >= 0 ? GC.CollectionCount(0) - Store.Gc0 : -1)
             .KV("errors", QaLog.ErrorCount);
            j.Key("prewarm_windows").Arr();
            foreach (var w in new[] { new Vector2(570f, 580f), new Vector2(774f, 782f), new Vector2(806f, 812f) })
            {
                float wm = 0f; int wn = 0;
                for (int i = 0; i < Store.N; i++)
                    if (Store.PostFade[i] && Store.S[i] >= w.x && Store.S[i] <= w.y) { wm = Mathf.Max(wm, Store.Dt[i]); wn++; }
                j.Obj().KV("s0", w.x).KV("s1", w.y).KV("frames", wn).KV("max_ms", wn > 0 ? wm : float.NaN).KV("limit_ms", 20f).KV("pass", wn == 0 || wm <= 20f).EndObj();
            }
            j.EndArr();
            if (Store.MemN > 0)
            {
                j.Key("soak").Obj().KV("restarts", Store.Restarts).KV("restart_method", Store.RestartHow).KV("memory_growth_mb", MemoryGrowthMb()).Key("memory").Arr();
                for (int k = 0; k < Store.MemN; k++)
                    j.Obj().KV("t", Store.MemT[k]).KV("sys_mb", Store.MemSys[k] < 0 ? double.NaN : Store.MemSys[k] / (1024.0 * 1024.0)).KV("mono_mb", Store.MemMono[k] / (1024.0 * 1024.0)).EndObj();
                j.EndArr().EndObj();
            }
            j.Key("log"); QaLog.WriteJson(j);
            j.EndObj();
            return j.ToString();
        }

        static double WorstMean(float[] sorted, int n, double frac)
        {
            if (n == 0) return double.NaN;
            int k = Math.Max(1, (int)Math.Ceiling(n * frac));
            double s = 0;
            for (int i = n - k; i < n; i++) s += sorted[i];
            return s / k;
        }
    }
}
