using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// First / last frame flashes (Part 10 #22, 14.5.1 acceptance (4)), active with -boot-capture [DIR]. Unlike the
    /// capture harness this never sets capture mode, so the real intro plays: every frame is captured from the WINDOW
    /// (ScreenCapture.CaptureScreenshotIntoRenderTexture at WaitForEndOfFrame, so the UI Toolkit black overlay, the title
    /// and the fades are in the picture), its mean luma (Rec.709 on the 8-bit sRGB values) is recorded, and these frames
    /// are written as PNGs:
    ///   boot/frame_NNNNN.png  the first 60 frames from startup (frame 0 is the very first rendered frame);
    ///   boot/load_NNNNN.png   the first 60 frames from RingEvents.LoadComplete (the 0.6 s black hold and the fade-in);
    ///   end/fade_NNNNN.png    60 frames from the start of the ending fade (goal + 3.0 s, 14.11);
    ///   end/last_NNNNN.png    the 60 frames after RingEvents.EndingComplete (the game stays on black): the last frames.
    /// The ending part runs with -autotest (the autotest walks; start it with -progress 955 for a short run) or with
    /// -boot-capture-ending (this behaviour then starts the AutoWalker in QA mode itself at LoadComplete). Without either,
    /// the run ends after the fade-in (LoadComplete + 4.1 s).
    /// Checks (exit 0 only if all pass): frame 0 mean luma &lt; 2; every frame before the fade-in starts (intro clock
    /// &lt; 0.6 s, the load included) below 2 with no frame-to-frame rise above 1.2 levels (white flash, scene visible
    /// before the fade, wrong clear colour); ending: every frame of the full-black hold (goal + 7.0 .. 8.5 s) and every
    /// last frame below 2, the final frame included. Informational: the picture level at 2.5 s against the level after
    /// the fade (14.5.1 (4) "full image by 2.5 s"; the 14.11 fade ends at 3.1 s) and the largest rise during the ending
    /// fade. Writes DIR/boot.json (DIR default QA/boot/&lt;stamp&gt;). Exit codes: 0 pass, 1 a check failed, 2 timeout or
    /// capture error, 3 the world never became ready; combined with the autotest's code through QaUtil's quit hold.
    /// Orientation: the window capture is stored top-down where SystemInfo.graphicsUVStartsAtTop (D3D), so those rows
    /// are flipped before encoding (-boot-capture-flip 0|1 overrides; the luma checks do not depend on it).
    /// Flags: -boot-capture-timeout S (default 300, 1500 with the ending), -boot-capture-ending.
    /// </summary>
    [DefaultExecutionOrder(30040)]
    public sealed class BootCapture : MonoBehaviour
    {
        public static bool Requested => CliArgs.Has("boot-capture");

        const int KeepFrames = 60;         // Part 10 #22: the first 60 and the last 60 frames
        const float BlackLuma = 2f;        // 14.5.1 (4): frame 0 mean luma < 2
        const float MaxRise = 1.2f;        // frame-to-frame rise allowed while the picture must be black
        const float FullImageT = 2.5f;     // 14.5.1 (4): "full image by 2.5 s" (informational)
        const float AfterFade = 1.0f;      // the boot part ends 1 s after the fade-in (3.1 s + 1 s)
        const int LumaStride = 2;          // every 2nd pixel in x and y for the per-frame mean
        // 14.11 ending timeline from the goal: fade 3.0 -> 7.0 s, black hold to 8.5 s, then the end line
        const float EndFadeStart = RingConstants.EndDwell;
        const float EndFadeEnd = RingConstants.EndDwell + RingConstants.EndFade;
        const float EndLineIn0 = EndFadeEnd + RingConstants.EndBlack;

        struct FrameRec { public int Index, Frame; public float Real, TLoad, TEnd, Luma; public string File; public bool IsLast; }

        readonly List<FrameRec> _boot = new List<FrameRec>(4096);
        readonly List<FrameRec> _end = new List<FrameRec>(4096);
        readonly List<string> _notes = new List<string>();
        string _dir;
        bool _endingMode, _bootDone, _finished, _loadSeen, _endSeen, _flip, _walkTried;
        double _startReal, _loadReal = -1, _goalReal = -1, _endDoneReal = -1, _abortAt = -1;
        int _index, _loadKept, _fadeKept, _lastKept, _captureErrors;
        string _walkHow = "";
        PngWriter _writer;
        RenderTexture _rt;
        Action _onLoad, _onEnd;
        Func<float> _introTime, _endingTime;
        Func<bool> _endingDone;

        void Awake()
        {
            if (!Requested) { enabled = false; return; }
            Application.runInBackground = true;
            _startReal = Time.realtimeSinceStartupAsDouble;
            _dir = QaUtil.Resolve(CliArgs.Str("boot-capture", "QA/boot/" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)));
            _endingMode = CliArgs.AutoTest || CliArgs.Has("boot-capture-ending");
            int fl = CliArgs.Int("boot-capture-flip", -1);
            _flip = fl < 0 ? SystemInfo.graphicsUVStartsAtTop : fl != 0;
            if (CliArgs.Capturing || CliArgs.NoFade) _notes.Add("-capture / -nofade skip the intro: the fade checks are not meaningful in this run");
            _onLoad = () => { if (_loadReal < 0) _loadReal = Time.realtimeSinceStartupAsDouble; _loadSeen = true; };
            _onEnd = () => { _endSeen = true; };
            BindUi();
            QaUtil.HoldQuit();
        }

        void Start()
        {
            if (!enabled) return;
            // GameBootstrap.Awake (order -1000) resets RingEvents; every Awake runs before any Start
            RingEvents.LoadComplete -= _onLoad; RingEvents.LoadComplete += _onLoad;
            RingEvents.EndingComplete -= _onEnd; RingEvents.EndingComplete += _onEnd;
            _writer = new PngWriter(2);
            QaUtil.EnsureDir(Path.Combine(_dir, "boot"));
            if (_endingMode) QaUtil.EnsureDir(Path.Combine(_dir, "end"));
            Debug.Log("[Ring] BootCapture: window frames -> " + _dir + (_endingMode ? " (boot and ending)" : " (boot)"));
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            // never leave the quit hold behind (a scene reload or another quit path destroyed this before the end)
            if (Requested && _writer != null && !_finished) Finish(2, "destroyed before the capture completed");
            RingEvents.LoadComplete -= _onLoad;
            RingEvents.EndingComplete -= _onEnd;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_writer != null) { try { _writer.Flush(); } catch (Exception) { } _writer.Stop(); _writer = null; }
        }

        /// <summary>The UiDirector's own intro / ending clocks (static properties, bound by reflection: no compile
        /// dependency on the UI module, no boxing per frame). Without them the clocks are real time since the events.</summary>
        void BindUi()
        {
            var t = QaUtil.FindType("UiDirector");
            if (t == null) return;
            _introTime = Getter<float>(t, "IntroTime");
            _endingTime = Getter<float>(t, "EndingTime");
            _endingDone = Getter<bool>(t, "EndingDone");
        }

        static Func<T> Getter<T>(Type t, string name)
        {
            var p = t.GetProperty(name, QaUtil.AnyStatic);
            var g = p != null && p.PropertyType == typeof(T) ? p.GetGetMethod(true) : null;
            if (g == null) return null;
            try { return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), g); }
            catch (Exception) { return null; }
        }

        float IntroClock(double now)
        {
            if (_introTime != null) { float v = _introTime(); if (v >= 0f) return v; }
            return _loadReal >= 0 ? (float)(now - _loadReal) : -1f;
        }

        float EndingClock(double now)
        {
            if (_endingTime != null) { float v = _endingTime(); if (v >= 0f) return v; }
            return _goalReal >= 0 ? (float)(now - _goalReal) : -1f;
        }

        IEnumerator Run()
        {
            var eof = new WaitForEndOfFrame();
            float timeout = CliArgs.Float("boot-capture-timeout", _endingMode ? 1500f : 300f);
            while (!_finished)
            {
                yield return eof;
                double now = Time.realtimeSinceStartupAsDouble;
                QaLog.MainFrame = Time.frameCount;
                if (_loadReal < 0 && RingRig.WorldReady) _loadReal = now;   // a LoadComplete raised before Start
                if (_loadReal >= 0 && _endingMode && !_walkTried) StartWalk();
                if (_goalReal < 0 && RingState.GoalReached) _goalReal = now;
                if (!_endSeen && _endingDone != null && _endingDone()) _endSeen = true;
                if (_endSeen && _endDoneReal < 0) _endDoneReal = now;

                float tLoad = IntroClock(now), tEnd = EndingClock(now);
                bool inBoot = !_bootDone;
                bool inEnd = _endingMode && tEnd >= 0f;
                if (inBoot || inEnd)
                {
                    // which PNG (if any) this frame becomes; a first-60 frame after LoadComplete also counts as a load frame
                    string file = null;
                    bool isLast = false;
                    if (inBoot)
                    {
                        bool loadFrame = tLoad >= 0f && _loadKept < KeepFrames;
                        if (_index < KeepFrames) file = "boot/frame_" + _index.ToString("00000") + ".png";
                        else if (loadFrame) file = "boot/load_" + _loadKept.ToString("00000") + ".png";
                        if (loadFrame) _loadKept++;
                    }
                    if (inEnd)
                    {
                        if (_endDoneReal >= 0 && _lastKept < KeepFrames)
                        {
                            isLast = true;
                            if (file == null) file = "end/last_" + _lastKept.ToString("00000") + ".png";
                            _lastKept++;
                        }
                        else if (_endDoneReal < 0 && tEnd >= EndFadeStart && _fadeKept < KeepFrames)
                        {
                            if (file == null) file = "end/fade_" + _fadeKept.ToString("00000") + ".png";
                            _fadeKept++;
                        }
                    }

                    float l = CaptureFrame(file, out string err);
                    if (err != null) { _captureErrors++; if (_captureErrors <= 5) _notes.Add("frame " + _index + ": " + err); }
                    var rec = new FrameRec
                    {
                        Index = _index, Frame = Time.frameCount, Real = (float)(now - _startReal), TLoad = tLoad, TEnd = tEnd, Luma = l,
                        File = file, IsLast = isLast
                    };
                    if (inBoot) _boot.Add(rec);
                    if (inEnd) _end.Add(rec);
                }
                _index++;

                if (!_bootDone && _index >= KeepFrames && _loadKept >= KeepFrames && tLoad >= RingConstants.FadeInEnd + AfterFade) _bootDone = true;
                bool endDone = !_endingMode || (_endDoneReal >= 0 && _lastKept >= KeepFrames);
                if (_bootDone && endDone) { Finish(-1, null); yield break; }
                // another QA behaviour already quit (deferred) although the ending never completed: e.g. the autotest
                // timed out. Give the ending 2 s, then stop instead of waiting for the long timeout.
                if (_endingMode && _endDoneReal < 0 && QaUtil.QuitDeferred)
                {
                    if (_abortAt < 0) _abortAt = now + 2.0;
                    else if (now >= _abortAt) { Finish(2, "another QA behaviour quit before the ending completed"); yield break; }
                }
                if (now - _startReal > timeout)
                {
                    Finish(_loadReal < 0 ? 3 : 2, _loadReal < 0 ? "world never became ready" : "timeout after " + QaUtil.F(timeout) + " s");
                    yield break;
                }
            }
        }

        void StartWalk()
        {
            _walkTried = true;
            if (CliArgs.AutoTest) { _walkHow = "AutoTest"; return; }   // the autotest drives the walk
            RingState.AutoTest = true;                                 // as the autotest: QA walker, no hint
            bool ok = RingQaHooks.TryStartAutoWalk(RingConstants.Walk, CliArgs.Seed, RingState.S, out _walkHow);
            if (!ok) _notes.Add("walker not started: " + _walkHow);
            Debug.Log("[Ring] BootCapture: walker " + (ok ? "started via " : "not started: ") + _walkHow);
        }

        /// <summary>Captures the window into an sRGB RT (same bytes whether the capture copies or blits), reads it back,
        /// returns the mean luma and queues the PNG when <paramref name="file"/> is set. NaN on failure.</summary>
        float CaptureFrame(string file, out string error)
        {
            error = null;
            int w = Screen.width, h = Screen.height;
            if (w <= 0 || h <= 0) { error = "no window"; return float.NaN; }
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) { _rt.Release(); Destroy(_rt); }
                _rt = new RenderTexture(new RenderTextureDescriptor(w, h) { graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB, depthStencilFormat = GraphicsFormat.None, msaaSamples = 1 })
                { name = "RingBootCapture" };
                _rt.Create();
            }
            byte[] buf = null;
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
                var r = AsyncGPUReadback.Request(_rt, 0);
                r.WaitForCompletion();
                if (r.hasError) { error = "window readback error"; return float.NaN; }
                var data = r.GetData<byte>();
                int size = w * h * 4;
                if (data.Length != size) { error = "window readback size " + data.Length + " != " + size; return float.NaN; }
                buf = _writer.Rent(size);
                data.CopyTo(buf);
            }
            catch (Exception e) { error = e.Message; if (buf != null) _writer.Return(buf); return float.NaN; }

            float l = MeanLuma(buf, w, h);
            if (file == null) { _writer.Return(buf); return l; }
            var rec = new CaptureRecord { File = file, Kind = "boot", Shot = "boot", Tag = file, W = w, H = h, S = RingState.S, T = RingState.T, Frame = _index };
            _writer.Enqueue(new PngWriter.Job { Buf = buf, W = w, H = h, Path = Path.Combine(_dir, file), Rec = rec, Flip = _flip });
            return l;
        }

        static float MeanLuma(byte[] b, int w, int h)
        {
            long sum = 0, n = 0;
            int row = w * 4;
            for (int y = 0; y < h; y += LumaStride)
            {
                int o = y * row;
                for (int x = 0; x < w; x += LumaStride, o += 4 * LumaStride)
                {
                    sum += 2126 * b[o] + 7152 * b[o + 1] + 722 * b[o + 2];
                    n++;
                }
            }
            return n > 0 ? (float)(sum / (10000.0 * n)) : float.NaN;
        }

        // ---------------- verdict ----------------
        void Finish(int forcedCode, string reason)
        {
            if (_finished) return;
            _finished = true;
            try { _writer?.Flush(); } catch (Exception e) { _notes.Add("flush: " + e.Message); _captureErrors++; }

            // boot: frame 0, and every frame before the fade-in starts (the load included) black without any rise
            bool frame0Ok = _boot.Count > 0 && _boot[0].Index == 0 && _boot[0].Luma < BlackLuma;
            float preMax = 0f, preRise = 0f; int preN = 0, preFirst = -1;
            float prev = float.NaN;
            foreach (var r in _boot)
            {
                if (r.TLoad >= RingConstants.FadeInStart) break;
                preN++;
                if (!(r.Luma < BlackLuma) && preFirst < 0) preFirst = r.Index;
                if (r.Luma > preMax || float.IsNaN(r.Luma)) preMax = float.IsNaN(r.Luma) ? float.PositiveInfinity : r.Luma;
                if (!float.IsNaN(prev) && r.Luma - prev > preRise) preRise = r.Luma - prev;
                prev = r.Luma;
            }
            bool preOk = preN > 0 && preMax < BlackLuma && preRise <= MaxRise;
            // informational: picture level at 2.5 s against the level after the fade-in
            float at25 = float.NaN, full = 0f; int fullN = 0;
            foreach (var r in _boot)
            {
                if (float.IsNaN(at25) && r.TLoad >= FullImageT) at25 = r.Luma;
                if (r.TLoad >= RingConstants.FadeInEnd + 0.3f && !float.IsNaN(r.Luma)) { full += r.Luma; fullN++; }
            }
            full = fullN > 0 ? full / fullN : float.NaN;
            float fullRatio = fullN > 0 && full > 1e-3f ? at25 / full : float.NaN;

            // ending: the full-black hold and the last frames below 2; the largest rise during the fade (informational)
            bool holdOk = true, lastOk = true; int holdN = 0, lastN = 0; float holdMax = 0f, lastMax = 0f, fadeRise = 0f, finalLuma = float.NaN;
            prev = float.NaN;
            foreach (var r in _end)
            {
                if (r.TEnd >= EndFadeEnd + 0.1f && r.TEnd <= EndLineIn0 - 0.1f) { holdN++; holdMax = Mathf.Max(holdMax, r.Luma); holdOk &= r.Luma < BlackLuma; }
                if (r.IsLast) { lastN++; lastMax = Mathf.Max(lastMax, r.Luma); lastOk &= r.Luma < BlackLuma; finalLuma = r.Luma; }
                if (r.TEnd >= EndFadeStart && r.TEnd <= EndFadeEnd && !float.IsNaN(prev) && r.Luma - prev > fadeRise) fadeRise = r.Luma - prev;
                if (r.TEnd >= EndFadeStart) prev = r.Luma;
            }
            bool endOk = !_endingMode || (holdN > 0 && holdOk && lastN >= KeepFrames && lastOk && finalLuma < BlackLuma);

            bool pass = frame0Ok && preOk && endOk && _captureErrors == 0;
            int code = forcedCode >= 0 ? forcedCode : (pass ? 0 : (_captureErrors > 0 && frame0Ok && preOk && endOk ? 2 : 1));

            var j = new QaJson();
            j.Obj().KV("test", "boot_capture").KV("finished", QaUtil.Stamp()).KV("pass", code == 0).KV("exit_code", code).KV("reason", reason)
             .KV("dir", _dir).KV("window", Screen.width + "x" + Screen.height).KV("api", SystemInfo.graphicsDeviceType.ToString())
             .KV("flip_rows", _flip).KV("ending_mode", _endingMode).KV("walker", _walkHow)
             .KV("clock", _introTime != null ? "UiDirector.IntroTime / EndingTime" : "real time since LoadComplete / goal")
             .KV("load_real_s", _loadReal >= 0 ? _loadReal - _startReal : double.NaN)
             .KV("goal_real_s", _goalReal >= 0 ? _goalReal - _startReal : double.NaN)
             .KV("ending_complete_real_s", _endDoneReal >= 0 ? _endDoneReal - _startReal : double.NaN)
             .KV("capture_errors", _captureErrors);
            j.Key("checks").Arr();
            Check(j, "frame0_black", frame0Ok, _boot.Count > 0 ? _boot[0].Luma : float.NaN, "frame 0 mean luma < 2 (14.5.1 (4))");
            Check(j, "black_before_fade", preOk, preMax, "every frame before the fade-in (intro t < 0.6 s, " + preN + " frames) < 2, rise <= 1.2 (max rise " + QaUtil.F(preRise, "0.00") + ", first bad frame " + preFirst + ")");
            if (_endingMode)
            {
                Check(j, "ending_black_hold", holdN > 0 && holdOk, holdMax, "goal + 7.0 .. 8.5 s all < 2 (" + holdN + " frames)");
                Check(j, "last_frames_black", lastN >= KeepFrames && lastOk, lastMax, "the " + KeepFrames + " frames after EndingComplete all < 2 (" + lastN + " captured)");
                Check(j, "final_frame_black", finalLuma < BlackLuma, finalLuma, "last frame < 2");
            }
            j.EndArr();
            j.Key("info").Obj().KV("luma_at_2_5s", at25).KV("luma_after_fade", full).KV("ratio_at_2_5s", fullRatio)
             .KV("ratio_note", "14.5.1 (4) asks for the full image by 2.5 s; the 14.11 fade (smoothstep 0.6 -> 3.1 s) gives about 0.85 there")
             .KV("ending_fade_max_rise", fadeRise).EndObj();
            j.Key("notes").Arr(); foreach (var n in _notes) j.V(n); j.EndArr();
            WriteFrames(j, "boot_frames", _boot);
            WriteFrames(j, "end_frames", _end);
            j.Key("log"); QaLog.WriteJson(j);
            j.EndObj();
            string path = Path.Combine(_dir, "boot.json");
            try { QaUtil.WriteText(path, j.ToString()); }
            catch (Exception e) { Debug.LogError("[Ring] BootCapture: cannot write " + path + ": " + e.Message); }
            Debug.Log("[Ring] BootCapture " + (code == 0 ? "PASS" : "FAIL") + " -> " + path);

            // the other QA modes that end the process themselves (the autotest after the ending, a capture, the wall
            // test, a perf run) exit later with the larger code; alone, this quits now
            bool others = CliArgs.AutoTest || CliArgs.Capturing || CliArgs.WallTest || CliArgs.PerfMode || CliArgs.Soak > 0f;
            QaUtil.ReleaseQuit(code, !others);
        }

        static void Check(QaJson j, string name, bool pass, float value, string expected)
        {
            j.Obj().KV("name", name).KV("pass", pass).KV("value", value).KV("expected", expected).EndObj();
        }

        static void WriteFrames(QaJson j, string key, List<FrameRec> list)
        {
            j.Key(key).Arr();
            foreach (var r in list)
            {
                j.Obj().KV("i", r.Index).KV("frame", r.Frame).KV("real_s", r.Real).KV("t_load", r.TLoad).KV("t_end", r.TEnd).KV("mean_luma", r.Luma);
                if (r.File != null) j.KV("file", r.File);
                j.EndObj();
            }
            j.EndArr();
        }
    }
}
