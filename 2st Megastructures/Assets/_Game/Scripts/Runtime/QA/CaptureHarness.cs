using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// Capture harness (Part 11.2, 14.10), active with -capture DIR. After the world is loaded it switches to capture
    /// mode (RingState.Capture: time branches = progress branches, followers = targets, slew bypassed; the player
    /// ignores input), runs the still plan of -shots and the frame sequences of -sequence through a dedicated capture
    /// camera rendered into RenderTextures (CaptureRenderer), optionally proves window-vs-RT parity (-window-capture),
    /// writes report.json and quits with 0 only if every image was written at the expected size and passed the
    /// in-player sanity checks (not black, not white, no magenta).
    /// Every pose and vFOV of a key shot comes from <see cref="KeyShots"/> (the 14.10 table). Capture work runs at the
    /// end of LateUpdate (late execution order) so per-frame RenderMesh draws of this frame are alive, and
    /// RingCaptureHooks.BeforeRender re-submits camera-specific draws right before each render.
    /// Clock: every still is rendered with a frozen clock, RingRig.ForcedT = the shot's t (KS00: 3.0 s) or s / 1.5 m/s
    /// (the walk time at that progress), so animation phases (water, wind, swarm orbits, beacon) are reproducible.
    /// Sequences start from the same frozen value and then advance by Time.captureDeltaTime = 1 / fps.
    /// vFOV: the key-shot value is applied to the capture camera and written to AtmosphereDriver.FovOverride for the
    /// duration of the still plan (the gameplay camera then shows the same FOV); sequences use the FOV the
    /// AtmosphereDriver applied to the gameplay camera (schedule + slew), so they show what the player sees.
    /// Screen-space UI (title, hint, fade overlay) is not part of an RT capture (research 04 section 6): KS00 shows the
    /// scene behind the title; the window capture of -window-capture shows the composited frame. The boot fade, the
    /// first and the last frames are imaged by BootCapture (-boot-capture), which never enters capture mode.
    /// Extra flags: -capture-warmup N (renders before the kept one, default 1), -capture-workers N (default 3),
    /// -capture-timeout S (load timeout, default 180), -window-capture-wait S (default 14).
    /// Exit codes: 0 all images ok, 1 an image failed, 2 harness error, 3 the world never became ready.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    public sealed class CaptureHarness : MonoBehaviour
    {
        const float PreRoll = 20f;      // approach every still from 20 m below (> 8 m forward + 12 m back hysteresis, 14.6)
        const float VariantMinDiff = 0.5f;   // a debug variant closer than this to its base (levels) shows no override effect
        static readonly int IdForceOutside = Shader.PropertyToID("_RingForceOutside");
        static readonly int IdSkyParams = Shader.PropertyToID("_SkyParams");

        string _dir;
        CaptureRenderer _r;
        IEnumerator _job;
        bool _loadSeen, _done;
        int _cliSet;
        int _failures, _harnessErrors;
        readonly List<CaptureRecord> _records = new List<CaptureRecord>();
        readonly List<string> _notes = new List<string>();
        readonly Dictionary<string, string> _composition = new Dictionary<string, string>();
        readonly Dictionary<string, bool> _compositionOk = new Dictionary<string, bool>();
        readonly List<string> _seqSummaries = new List<string>();
        readonly HashSet<string> _variantBases = new HashSet<string>();
        readonly Dictionary<string, byte[]> _baseBuffers = new Dictionary<string, byte[]>();
        readonly Dictionary<string, string> _baseFiles = new Dictionary<string, string>();
        string _parityJson, _orientationJson;
        List<Renderer> _skyRenderers;
        float _startReal;
        Action _onLoad;
        Action<int> _onBeat;
        int _beatEventFrame = -1, _beatEventIndex = -1;

        void Awake()
        {
            if (!CliArgs.Capturing) { enabled = false; return; }
            _dir = QaUtil.Resolve(CliArgs.CaptureDir);
            _cliSet = CliArgs.SetOverride;
            _startReal = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            _onLoad = () => _loadSeen = true;
            _onBeat = b => { _beatEventIndex = b; _beatEventFrame = Time.frameCount; };
            // window-vs-RT parity is defined at 1920 x 1080 (Part 11.2)
            if (CliArgs.Has("window-capture") && (Screen.width != 1920 || Screen.height != 1080))
            {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                Debug.Log("[Ring] CaptureHarness: window set to 1920x1080 for the parity capture (was " + Screen.width + "x" + Screen.height + ")");
            }
        }

        void Start()
        {
            if (!enabled) return;
            RingEvents.LoadComplete += _onLoad;
            Debug.Log("[Ring] CaptureHarness: output " + _dir);
            _job = Job();
        }

        void OnDestroy()
        {
            RingEvents.LoadComplete -= _onLoad;
            RingEvents.BeatEntered -= _onBeat;
            if (_r != null) { _r.Dispose(); _r = null; }
        }

        // before every LateUpdate (directors log activations in theirs), so log lines carry the current frame index
        void Update() { QaLog.MainFrame = Time.frameCount; }

        void LateUpdate()
        {
            QaLog.MainFrame = Time.frameCount;
            if (_job == null) return;
            bool more;
            try { more = _job.MoveNext(); }
            catch (Exception e)
            {
                Debug.LogError("[Ring] CaptureHarness failed: " + e);
                _harnessErrors++;
                more = false;
                Finish();
            }
            if (!more) _job = null;
        }

        // ---------------- main job ----------------
        IEnumerator Job()
        {
            float timeout = CliArgs.Float("capture-timeout", 180f);
            while (!_loadSeen && !RingRig.WorldReady)
            {
                if (Time.realtimeSinceStartup - _startReal > timeout)
                {
                    _notes.Add("world not ready after " + timeout + " s");
                    WriteReport(3);
                    QaUtil.Quit(3);
                    _done = true;
                    yield break;
                }
                yield return null;
            }
            // listeners registered again after load (a bootstrap may have reset the events)
            RingEvents.BeatEntered -= _onBeat; RingEvents.BeatEntered += _onBeat;
            RingState.Capture = true;
            RingState.SequenceMode = false;
            for (int i = 0; i < 5; i++) yield return null;

            Vector2Int? res = null;
            if (CliArgs.Res(out int rw, out int rh)) res = new Vector2Int(rw, rh);
            _r = new CaptureRenderer(transform, CliArgs.Int("capture-workers", 3));
            _r.SyncFrom(RingRig.Camera);
            QaUtil.EnsureDir(_dir);
            int row = _r.OrientationSelfTest(_dir, out string odetail);
            _r.Flip = row == 0;
            _orientationJson = new QaJson().Obj().KV("readback_row", row).KV("flip", _r.Flip).KV("detail", odetail).EndObj().ToString();

            if (CliArgs.Has("window-capture"))
            {
                bool parityDone = false;
                StartCoroutine(Parity(() => parityDone = true));
                while (!parityDone) yield return null;
            }

            var unknown = new List<string>();
            bool stillsRequested = CliArgs.Has("shots") || !CliArgs.Has("sequence");
            var stills = stillsRequested ? CapturePlans.Parse(CliArgs.Shots, res, unknown) : new List<StillShot>();
            var seqs = CapturePlans.ParseSequences(CliArgs.Sequence, unknown);
            foreach (var u in unknown) _notes.Add("unknown plan token '" + u + "'");
            Debug.Log("[Ring] CaptureHarness: " + stills.Count + " stills, " + seqs.Count + " sequences");

            int warm = Mathf.Max(0, CliArgs.Int("capture-warmup", 1));
            // key shots a debug variant of this plan is compared with (same shot and resolution; the plan order puts the
            // base first: same resolution, earlier plan position)
            foreach (var st in stills) if (st.Kind == "ks_variant") _variantBases.Add(st.Shot + "_" + st.W + "x" + st.H);
            for (int i = 0; i < stills.Count; i++)
            {
                CaptureStill(stills[i], warm);
                yield return null;
            }
            _baseBuffers.Clear();
            RingQaHooks.SetFovOverride(float.NaN);

            foreach (var plan in seqs)
            {
                var baseRes = res ?? KeyShots.Res1080;
                var e = RunSequence(plan, new Vector2Int(baseRes.x * Mathf.Max(1, plan.Scale), baseRes.y * Mathf.Max(1, plan.Scale)));
                while (e.MoveNext()) yield return e.Current;
            }

            Finish();
        }

        void Finish()
        {
            if (_done) return;
            _done = true;
            try { _r?.Flush(); } catch (Exception e) { _notes.Add("flush: " + e.Message); _harnessErrors++; }
            RingRig.ForcedS = float.NaN;
            RingRig.ForcedT = double.NaN;
            RingState.SetOverride = _cliSet;
            RingQaHooks.SetFovOverride(float.NaN);
            Time.captureDeltaTime = 0f;
            foreach (var rec in _records) if (!rec.Ok) _failures++;
            int code = _harnessErrors > 0 ? 2 : (_failures > 0 ? 1 : 0);
            WriteReport(code);
            Debug.Log("[Ring] CaptureHarness done: " + _records.Count + " images, " + _failures + " failed, exit " + code);
            QaUtil.Quit(code);
        }

        // ---------------- settling and posing ----------------
        void Tick()
        {
            var main = RingRig.Camera;
            if (_r != null) RingRig.Camera = _r.Cam;   // camera-dependent director state follows the capture camera
            bool chain = RingRig.Directors.Count > 0;
            try
            {
                // the chain contains the ProgressDirector (s, t, phases), the BeatDirector (sets, then IRingAnimated at
                // its AnimatedOrder) and the AtmosphereDriver (globals, lights, volume); followers equal targets in capture
                if (chain) RingRig.TickDirectors(0f);
                else if (!float.IsNaN(RingRig.ForcedS)) RingState.S = RingRig.ForcedS;   // no ProgressDirector (early graybox)
            }
            catch (Exception e) { Debug.LogWarning("[Ring] CaptureHarness: director tick threw " + e.Message); }
            var ctx = RingRig.Context;
            if (!chain && ctx != null)
                foreach (var a in ctx.Animated)
                {
                    try { a.Tick(RingState.S, RingState.T, 0f); }
                    catch (Exception e) { Debug.LogWarning("[Ring] CaptureHarness: animated tick threw " + e.Message); }
                }
            RingRig.Camera = main;
        }

        /// <summary>Frozen capture clock: the shot's own t, else the walk time at s (s / 1.5 m/s).</summary>
        static double FrozenT(float s, double t) => double.IsNaN(t) ? s / (double)RingConstants.Walk : t;

        /// <summary>Synchronous settle for a still: capture mode, set override, frozen clock, approach from below.</summary>
        void Settle(float s, double t, int setOverride)
        {
            RingState.Capture = true;
            RingState.SequenceMode = false;
            RingState.SetOverride = setOverride >= 0 ? setOverride : _cliSet;
            RingRig.ForcedT = FrozenT(s, t);
            float pre = Mathf.Max(0f, s - PreRoll);
            if (pre < s) { RingRig.ForcedS = pre; Tick(); }
            RingRig.ForcedS = s;
            Tick();
        }

        void PoseCamera(Vector3 eye, float yaw, float pitch, float vfov169, int w, int h, float s)
        {
            _r.SyncFrom(RingRig.Camera);
            float aspect = w / (float)h;
            float vfov = RingQaHooks.VfovFor(vfov169, aspect);
            float far = s >= RingConstants.FarOutsideFromS ? RingConstants.FarOutside : RingConstants.FarDefault;
            _r.Pose(eye, yaw, pitch, vfov, aspect, RingConstants.NearPlane, far);
        }

        List<Renderer> SkyRenderers()
        {
            if (_skyRenderers != null) return _skyRenderers;
            _skyRenderers = new List<Renderer>();
            var ctx = RingRig.Context;
            if (ctx != null)
                foreach (var key in new[] { "Sky", "Sky.Dome", "SkyDome" })
                    if (ctx.Registry.TryGetValue(key, out var o))
                    {
                        var go = RegistryMeasure.AsGameObject(o);
                        if (go != null) _skyRenderers.AddRange(go.GetComponentsInChildren<Renderer>(true));
                    }
            if (_skyRenderers.Count == 0)
                foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                {
                    var m = mr.sharedMaterial;
                    if (m != null && m.shader != null && m.shader.name == "Megastructure/RingSky") _skyRenderers.Add(mr);
                }
            return _skyRenderers;
        }

        // ---------------- stills ----------------
        void CaptureStill(StillShot shot, int warm)
        {
            RingQaHooks.SetFovOverride(shot.Vfov169);   // the AtmosphereDriver writes the shot vFOV during the settle
            Settle(shot.S, shot.T, shot.SetOverride);
            PoseCamera(shot.Eye, shot.Yaw, shot.Pitch, shot.Vfov169, shot.W, shot.H, shot.S);

            // debug overrides for one render
            var hidden = new List<Renderer>();
            GameObject orbital = null;
            bool isOrbital = (shot.Flags & QaShotFlags.Orbital) != 0;
            if (isOrbital)
            {
                // the whole ring from space: only the orbital model and the sky dome (outside sky forced), no code-drawn
                // fields (grass, flowers, swarm are camera-relative), clip range for 100 km views
                orbital = OrbitalView.Ensure();
                if (orbital != null) orbital.SetActive(true);
                var skyRs = SkyRenderers();
                foreach (var rr in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                    if (rr != null && rr.enabled && !skyRs.Contains(rr) && !OrbitalView.IsOrbital(rr)) { rr.enabled = false; hidden.Add(rr); }
                _r.Cam.nearClipPlane = 2f;
                _r.Cam.farClipPlane = 400000f;
                RingCaptureHooks.SuppressFieldDraws = true;
                Shader.SetGlobalFloat(IdForceOutside, 1f);
            }
            Vector4 swarm = Vector4.zero;
            if ((shot.Flags & QaShotFlags.HideSky) != 0)
            {
                foreach (var r in SkyRenderers()) if (r != null && r.enabled) { r.enabled = false; hidden.Add(r); }
                if (hidden.Count == 0) _notes.Add(shot.File + ": no sky-dome renderer found to hide");
            }
            if ((shot.Flags & QaShotFlags.NoGlint) != 0)
            {
                swarm = Shader.GetGlobalVector("_Swarm");
                Shader.SetGlobalVector("_Swarm", new Vector4(0f, swarm.y, swarm.z, swarm.w));
            }
            // 14.5.2 (8) "the same render with hz forced to 0". No dedicated switch exists in the uniform contract, so the
            // override uses the contract global _SkyParams.w (arch night lift, read only by RingArchHaze):
            // hz *= 1 - w smoothstep(16, 32, e), so w = 1 gives hz = 0 exactly for e >= 32 deg (the 36-44 deg band the
            // acceptance measures) and a reduced hz between 16 and 32 deg. Restored right after the render (the
            // AtmosphereDriver rewrites it at its next tick anyway).
            Vector4 sky = Vector4.zero;
            if ((shot.Flags & QaShotFlags.NoHaze) != 0)
            {
                sky = Shader.GetGlobalVector(IdSkyParams);
                Shader.SetGlobalVector(IdSkyParams, new Vector4(sky.x, sky.y, sky.z, 1f));
            }

            var main = RingRig.Camera;
            RingRig.Camera = _r.Cam;
            bool ok = _r.Render(shot.W, shot.H, warm, out string err);
            RingRig.Camera = main;

            foreach (var r in hidden) if (r != null) r.enabled = true;
            if (isOrbital)
            {
                if (orbital != null) orbital.SetActive(false);
                RingCaptureHooks.SuppressFieldDraws = false;
                Shader.SetGlobalFloat(IdForceOutside, 0f);
            }
            if ((shot.Flags & QaShotFlags.NoGlint) != 0) Shader.SetGlobalVector("_Swarm", swarm);
            if ((shot.Flags & QaShotFlags.NoHaze) != 0) Shader.SetGlobalVector(IdSkyParams, sky);

            var rec = NewRecord(shot.File, shot.Kind, shot.Shot, shot.Tag, shot.Beat, shot.S, RingState.T, shot.Eye, shot.Yaw, shot.Pitch,
                                shot.Vfov169, shot.W, shot.H, shot.SetOverride);
            rec.Flags = shot.Flags.ToString();
            if ((shot.Flags & QaShotFlags.NoHaze) != 0) rec.Override = "_SkyParams.w = 1 (hz = 0 for e >= 32 deg)";
            else if ((shot.Flags & QaShotFlags.NoGlint) != 0) rec.Override = "_Swarm.x = 0 (glints off)";
            if (shot.IsKeyShot) rec.CompositionJson = Composition(shot.Ks);
            _records.Add(rec);
            if (!ok) { rec.Error = err; rec.Ok = false; Debug.LogWarning("[Ring] capture " + shot.File + ": " + err); return; }
            var buf = _r.Readback(out err);
            if (buf == null) { rec.Error = err; rec.Ok = false; Debug.LogWarning("[Ring] capture " + shot.File + ": " + err); return; }
            CompareWithBase(shot, rec, buf);
            _r.Write(buf, shot.W, shot.H, Path.Combine(_dir, shot.File), rec, _r.Flip);
        }

        /// <summary>
        /// Debug variants are only meaningful when the override changed the picture. A key shot that a variant of this plan
        /// needs keeps a copy of its readback; the variant is compared with it (mean absolute RGB difference, levels) before
        /// it is written. The no-haze variant fails when the difference is below 0.5 level (the override had no effect, so
        /// 14.5.2 (8) would pass trivially); the no-glint variant only records it (glints are sparse by design). Must run
        /// before Write: the writer thread flips the buffer and owns it afterwards.
        /// </summary>
        void CompareWithBase(StillShot shot, CaptureRecord rec, byte[] buf)
        {
            string key = shot.Shot + "_" + shot.W + "x" + shot.H;
            if (shot.Kind == "ks" && _variantBases.Contains(key))
            {
                var copy = new byte[buf.Length];
                Buffer.BlockCopy(buf, 0, copy, 0, buf.Length);
                _baseBuffers[key] = copy;
                _baseFiles[key] = shot.File;
                return;
            }
            if (shot.Kind != "ks_variant") return;
            if (!_baseBuffers.TryGetValue(key, out var baseBuf))
            {
                rec.OverrideEffect = "not verified (base " + key + " not in this plan)";
                _notes.Add(shot.File + ": override effect not verified, the base key shot " + key + " was not captured in this run");
                return;
            }
            rec.BaseFile = _baseFiles[key];
            rec.DiffFromBase = CaptureRenderer.MeanAbsDiff(baseBuf, buf, shot.W, shot.H, false);
            bool noHaze = (shot.Flags & QaShotFlags.NoHaze) != 0;
            if (noHaze && !(rec.DiffFromBase >= VariantMinDiff))
            {
                rec.OverrideEffect = "none";
                rec.Error = "debug override had no effect: mean abs diff from " + rec.BaseFile + " = " + QaUtil.F((float)rec.DiffFromBase, "0.000")
                            + " < " + QaUtil.F(VariantMinDiff) + " level";
                Debug.LogWarning("[Ring] capture " + shot.File + ": " + rec.Error);
            }
            else rec.OverrideEffect = rec.DiffFromBase > 0.0 ? "changed" : "identical";
            if (!noHaze && rec.DiffFromBase <= 0.0) _notes.Add(shot.File + ": identical to " + rec.BaseFile + " (the override changed nothing)");
        }

        CaptureRecord NewRecord(string file, string kind, string shot, string tag, int beat, float s, double t, Vector3 eye,
                                float yaw, float pitch, float vfov169, int w, int h, int setOverride)
        {
            float aspect = w / (float)h;
            return new CaptureRecord
            {
                File = file, Kind = kind, Shot = shot, Tag = tag, Beat = beat, S = s, T = t, Eye = eye, Yaw = yaw, Pitch = pitch,
                Vfov169 = vfov169, Vfov = RingQaHooks.VfovFor(vfov169, aspect), W = w, H = h, SetOverride = setOverride,
                Aspect = Mathf.Abs(aspect - 16f / 9f) < 0.01f ? "16:9" : Mathf.Abs(aspect - 1.6f) < 0.01f ? "16:10" : aspect > 2.2f ? "21:9" : QaUtil.F(aspect, "0.000")
            };
        }

        string Composition(KeyShot k)
        {
            if (_composition.TryGetValue(k.Id, out var js)) return js;
            var j = new QaJson();
            CompositionCheck.WriteJson(j, k, RingRig.Context, out bool ok);
            js = j.ToString();
            _composition[k.Id] = js;
            _compositionOk[k.Id] = ok;
            return js;
        }

        // ---------------- sequences ----------------
        IEnumerator RunSequence(SequencePlan p, Vector2Int res)
        {
            string folder = Path.Combine(_dir, "seq" + p.Name);
            QaUtil.EnsureDir(folder);
            int n = p.Frames;
            Debug.Log("[Ring] CaptureHarness: sequence " + p.Name + " " + n + " frames at " + p.Fps + " fps");
            var frames = new List<CaptureRecord>(n);
            var events = new List<string>();
            var ringEvents = new List<QaLog.Entry>();

            // settle at the start pose in capture mode (slew bypassed, clock frozen at s0 / 1.5), then switch the slew
            // limiters on; the clock is released right before frame 0 and advances by exactly 1 / fps per frame
            RingQaHooks.SetFovOverride(float.NaN);
            Settle(p.SAt(0), double.NaN, -1);
            double t0 = RingRig.ForcedT;
            Time.captureDeltaTime = 1f / p.Fps;
            for (int i = 0; i < 10; i++) { RingRig.ForcedS = p.SAt(0); RingRig.ForcedT = t0; yield return null; }
            RingState.SequenceMode = true;
            for (int i = 0; i < 5; i++) { RingRig.ForcedS = p.SAt(0); RingRig.ForcedT = t0; yield return null; }
            RingRig.ForcedT = p.FreezeClock ? t0 : double.NaN;

            int lastSet = RingState.Set, lastBeat = RingState.Beat;
            int ringFrom = QaLog.RingEventCount;
            int unityFrame0 = Time.frameCount;
            _beatEventIndex = -1;
            for (int i = 0; i < n; i++)
            {
                float s = p.SAt(i);
                Pose(p, i, s, out Vector3 eye, out float yaw, out float pitch);
                // the vFOV the AtmosphereDriver applied this frame (14.2.5 schedule through the slew limiter), else the schedule
                var gcam = RingRig.Camera;
                float vf = gcam != null && gcam.fieldOfView > 1f ? gcam.fieldOfView : RingSchedule.Vfov(s);
                PoseCamera(eye, yaw, pitch, vf, res.x, res.y, s);

                var main = RingRig.Camera;
                RingRig.Camera = _r.Cam;
                bool ok = _r.Render(res.x, res.y, 0, out string err);
                RingRig.Camera = main;

                string file = "frame_" + i.ToString("00000") + ".png";
                var rec = NewRecord("seq" + p.Name + "/" + file, "frame", p.Name, p.Name, RingConstants.BeatAt(s) + 1, s, RingState.T, eye, yaw, pitch,
                                    _r.Cam.fieldOfView, res.x, res.y, -1);
                rec.Frame = i;
                rec.Vfov = _r.Cam.fieldOfView;
                frames.Add(rec);
                _records.Add(rec);
                if (!ok) { rec.Error = err; rec.Ok = false; }
                else
                {
                    var buf = _r.Readback(out err);
                    if (buf == null) { rec.Error = err; rec.Ok = false; }
                    else _r.Write(buf, res.x, res.y, Path.Combine(folder, file), rec, _r.Flip);
                }

                // activation log: set and beat changes, BeatEntered events, "[Ring]" activation lines
                if (RingState.Set != lastSet) { events.Add(Ev(i, "set", lastSet + "->" + RingState.Set)); lastSet = RingState.Set; }
                if (RingState.Beat != lastBeat) { events.Add(Ev(i, "beat", lastBeat + "->" + RingState.Beat)); lastBeat = RingState.Beat; }
                if (_beatEventIndex >= 0) { events.Add(Ev(i, "beat_entered", _beatEventIndex.ToString())); _beatEventIndex = -1; }
                ringEvents.Clear();
                ringFrom = QaLog.RingEventsSince(ringFrom, ringEvents);
                foreach (var e in ringEvents) events.Add(Ev(Mathf.Clamp(e.Frame - unityFrame0, 0, n - 1), "log", e.Message));

                RingRig.ForcedS = p.SAt(i + 1);
                yield return null;
            }
            _r.Flush();
            RingState.SequenceMode = false;
            RingRig.ForcedS = float.NaN;
            RingRig.ForcedT = double.NaN;
            Time.captureDeltaTime = 0f;

            int bad = 0;
            foreach (var f in frames) if (!f.Ok) bad++;
            var j = new QaJson();
            j.Obj().KV("name", p.Name).KV("fps", p.Fps).KV("frames", n).KV("w", res.x).KV("h", res.y).KV("static", p.Static)
             .KV("s0", p.S0).KV("s1", p.S1).KV("speed", p.Static ? 0f : p.Speed).KV("checks", p.Checks).KV("failed_frames", bad)
             .KV("scale", p.Scale).KV("pair", p.Pair).KV("pan_deg_per_frame", p.PanDegPerFrame).KV("frozen_clock", p.FreezeClock)
             .Key("band_region").Arr().V(p.BandRegion.x).V(p.BandRegion.y).V(p.BandRegion.z).V(p.BandRegion.w).EndArr()
             .Key("events").Arr();
            foreach (var e in events) j.Raw(e);
            j.EndArr().Key("records").Arr();
            foreach (var f in frames)
                j.Obj().KV("i", f.Frame).KV("s", f.S).KV("t", f.T).KV("beat", f.Beat).KV("yaw", f.Yaw).KV("pitch", f.Pitch)
                 .KV("vfov", f.Vfov).KV("eye", f.Eye).KV("mean_luma", f.MeanLuma).KV("clipped_pct", f.ClippedPct).KV("ok", f.Ok).EndObj();
            j.EndArr().EndObj();
            QaUtil.WriteText(Path.Combine(folder, "sequence.json"), j.ToString());
            _seqSummaries.Add(new QaJson().Obj().KV("name", p.Name).KV("folder", "seq" + p.Name).KV("frames", n).KV("fps", p.Fps)
                                          .KV("failed_frames", bad).KV("ok", bad == 0).EndObj().ToString());
        }

        static string Ev(int i, string type, string detail) => new QaJson().Obj().KV("i", i).KV("type", type).KV("detail", detail).EndObj().ToString();

        /// <summary>Deterministic walker pose for sequence frame i (1.5 m/s along the rounded path, or a static camera).</summary>
        static void Pose(SequencePlan p, int i, float s, out Vector3 eye, out float yaw, out float pitch)
        {
            eye = p.FixedEye ? p.Eye : QaPath.EyeAt(s, out _);
            if (p.FixedGaze) { yaw = p.Yaw + p.PanDegPerFrame * i; pitch = p.Pitch; return; }
            QaPath.GazeAt(s, out yaw, out pitch);
            if (p.GazeToKs >= 0)
            {
                var k = KeyShots.All[p.GazeToKs];
                float w = RingMath.Smootherstep(p.S0, p.S1, s);
                yaw = yaw + Mathf.DeltaAngle(yaw, k.Yaw) * w;
                pitch = Mathf.Lerp(pitch, k.Pitch, w);
                if (s >= k.S - 1e-3f) eye = k.Eye;
            }
        }

        // ---------------- window-vs-RT parity ----------------
        IEnumerator Parity(Action done)
        {
            // wait until the title, hint and fade-in are gone (14.11: hint until about 12.6 s) so the window shows only the camera
            float t0 = Time.realtimeSinceStartup;
            float wait = CliArgs.Float("window-capture-wait", 14f);
            RingRig.ForcedS = float.NaN;
            while (Time.realtimeSinceStartup - t0 < wait) yield return null;
            yield return new WaitForEndOfFrame();
            var win = _r.CaptureWindow(GraphicsFormat.R8G8B8A8_SRGB, out int w, out int h, out string e1);
            var winU = _r.CaptureWindow(GraphicsFormat.R8G8B8A8_UNorm, out _, out _, out string e2);
            var main = RingRig.Camera;
            byte[] rt = null;
            string e3 = null;
            if (main != null)
            {
                _r.SyncFrom(main);
                _r.Cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                _r.Cam.fieldOfView = main.fieldOfView;
                _r.Cam.aspect = w / (float)h;
                if (_r.Render(w, h, 0, out e3)) rt = _r.Readback(out e3);
            }
            double[] d = new double[4];
            d[0] = CaptureRenderer.MeanAbsDiff(rt, win, w, h, false);
            d[1] = CaptureRenderer.MeanAbsDiff(rt, win, w, h, true);
            d[2] = CaptureRenderer.MeanAbsDiff(rt, winU, w, h, false);
            d[3] = CaptureRenderer.MeanAbsDiff(rt, winU, w, h, true);
            int best = -1;
            for (int i = 0; i < 4; i++) if (!double.IsNaN(d[i]) && (best < 0 || d[i] < d[best])) best = i;
            string[] names = { "srgb", "srgb_flipped", "unorm", "unorm_flipped" };
            bool pass = best >= 0 && d[best] <= 1.0;
            var j = new QaJson();
            j.Obj().KV("w", w).KV("h", h).KV("diff_srgb", d[0]).KV("diff_srgb_flipped", d[1]).KV("diff_unorm", d[2]).KV("diff_unorm_flipped", d[3])
             .KV("best", best >= 0 ? names[best] : "none").KV("best_diff", best >= 0 ? d[best] : double.NaN).KV("limit", 1.0).KV("pass", pass)
             .KV("errors", (e1 ?? "") + " " + (e2 ?? "") + " " + (e3 ?? "")).EndObj();
            _parityJson = j.ToString();
            string rs = w + "x" + h;
            if (rt != null)
            {
                var recRt = NewRecord("parity_rt_" + rs + ".png", "parity", "parity", "rt", RingState.Beat + 1, RingState.S, RingState.T,
                                      main != null ? main.transform.position : Vector3.zero, RingRig.Yaw, RingRig.Pitch, main != null ? main.fieldOfView : 0f, w, h, -1);
                _records.Add(recRt);
                _r.Write(rt, w, h, Path.Combine(_dir, recRt.File), recRt, _r.Flip);
            }
            var pick = best >= 2 ? winU : win;
            if (pick != null)
            {
                if (best == 1 || best == 3) CaptureRenderer.FlipRows(pick, w, h);
                var recW = NewRecord("parity_window_" + rs + ".png", "parity", "parity", "window", RingState.Beat + 1, RingState.S, RingState.T,
                                     Vector3.zero, RingRig.Yaw, RingRig.Pitch, 0f, w, h, -1);
                _records.Add(recW);
                _r.Write(pick, w, h, Path.Combine(_dir, recW.File), recW, _r.Flip);
            }
            if (!pass) _notes.Add("window-vs-RT parity above 1/255: " + _parityJson);
            done();
        }

        // ---------------- report ----------------
        void WriteReport(int code)
        {
            try
            {
                var j = new QaJson();
                j.Obj().KV("run", Path.GetFileName(_dir)).KV("finished", QaUtil.Stamp()).KV("unity", Application.unityVersion)
                 .KV("platform", Application.platform.ToString()).KV("editor", Application.isEditor)
                 .KV("gpu", SystemInfo.graphicsDeviceName).KV("api", SystemInfo.graphicsDeviceType.ToString())
                 .KV("reversed_z", SystemInfo.usesReversedZBuffer)
                 .KV("hdr_format", _r != null ? _r.HdrFormat.ToString() : "")
                 .KV("depth_format", _r != null ? _r.DepthFormat.ToString() : "")
                 .KV("msaa", _r != null ? _r.MsaaUsed : 0)
                 .KV("before_render_hook", RingCaptureHooks.Method)
                 .KV("fov_override", RingQaHooks.SetFovOverride(float.NaN) ? "AtmosphereDriver.FovOverride" : "capture camera only")
                 .KV("clock", "frozen: ForcedT = shot t or s / 1.5; sequences advance by 1 / fps")
                 .KV("ui_in_rt", false)
                 .KV("vfov_policy", RingQaHooks.VfovPolicy)
                 .KV("gaze_source", RingQaHooks.TryGaze(100f, out _, out _) ? "GazeSchedule" : "heading + pitch 4")
                 .KV("directors", RingRig.Directors.Count)
                 .KV("registry_keys", RingRig.Context != null ? RingRig.Context.Registry.Count : 0)
                 .KV("exit_code", code).KV("images", _records.Count).KV("failed", _failures).KV("harness_errors", _harnessErrors);
                j.Key("orientation_selftest"); if (_orientationJson != null) j.Raw(_orientationJson); else j.V((string)null);
                j.Key("parity"); if (_parityJson != null) j.Raw(_parityJson); else j.V((string)null);
                j.Key("composition_ok").Obj();
                foreach (var kv in _compositionOk) j.KV(kv.Key, kv.Value);
                j.EndObj();
                j.Key("notes").Arr(); foreach (var n in _notes) j.V(n); j.EndArr();
                j.Key("sequences").Arr(); foreach (var s in _seqSummaries) j.Raw(s); j.EndArr();
                j.Key("log"); QaLog.WriteJson(j);
                j.Key("images_detail").Arr();
                foreach (var r in _records) if (r.Kind != "frame") r.WriteJson(j);
                j.EndArr();
                j.Key("failed_frames").Arr();
                foreach (var r in _records) if (r.Kind == "frame" && !r.Ok) r.WriteJson(j);
                j.EndArr().EndObj();
                QaUtil.WriteText(Path.Combine(_dir, "report.json"), j.ToString());
            }
            catch (Exception e) { Debug.LogError("[Ring] CaptureHarness: report.json failed: " + e.Message); }
        }
    }
}
