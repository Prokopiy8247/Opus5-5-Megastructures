using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

namespace Megastructure
{
    /// <summary>
    /// -soak S (Part 11.8, Part 10 item 31, 14.10 functional): loops the autowalk (or the cinematic walk with -cinematic) for S
    /// seconds of wall-clock time (-soak-virtual: virtual seconds at 1/60, VSync off). The loop restarts in place before the
    /// goal-door trigger (d 798): lift back to the top dock, player to W0, walker restarted, so the scene clock keeps running for
    /// the whole soak (a scene reload would reset it). Checks:
    /// - memory every 10 s (system used, Mono heap, total allocated / reserved, graphics driver); growth from the first
    ///   sample after 60 s to the end within max(32 MB, 5 %) (-soak-mem-tol-mb);
    /// - clock drift: SceneClock.Time against a compensated (Kahan) sum of the frame deltas the ClockDriver applied, and the
    ///   piston (mod 120 s) and fan (mod 40 s) phases from SceneClock.Wrapped (double) against the reference: under 1 ms.
    ///   Reported, not judged: a float accumulator wrapped per frame (what a float clock would drift) and the quantum of the
    ///   float shader time _AbTime.x at the current clock;
    /// - zero errors in the log, zero safety returns, camera never inside a collider.
    /// Writes soak.json under -qa-out (QA/) and exits 0 / 1.
    /// </summary>
    public sealed class QaSoak
    {
        public const float SampleInterval = 10f;
        public const float WarmupSeconds = 60f;
        public const double DriftLimitMs = 1.0;
        public const float LoopD = 798f;

        struct MemSample { public float T; public double SceneT; public float SysMB, MonoMB, AllocMB, ReservedMB, GfxDriverMB, GcHeapMB; public int Loop; }

        readonly QaHarness _h;
        MemSample[] _mem;
        int _memN;
        int _loops;
        double _ref, _refC;          // Kahan sum of applied deltas
        double _prevScene;
        float _fPiston, _fFan;       // float accumulators (info)
        double _maxClockDrift, _maxPistonDrift, _maxFanDrift, _maxFloatPiston, _maxFloatFan, _maxShaderQuantum;
        long _frames;
        float _duration;
        bool _virtual;
        float _elapsed;
        ProfilerRecorder _sys;

        public QaSoak(QaHarness h) { _h = h; }

        public IEnumerator Run()
        {
            _h.AddFinalizer(Finish);
            _duration = Mathf.Max(1f, CliArgs.Soak);
            _virtual = Time.captureDeltaTime > 0f;
            _mem = new MemSample[(int)(_duration / SampleInterval) + 64];
            _sys = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            yield return _h.WaitForLoad(false);
            bool cinematic = CliArgs.Cinematic;
            var walker = AutoWalker.Instance;
            yield return QaHarness.WaitSeconds(0.5f);
            if (walker != null && !walker.IsRunning) walker.Begin(cinematic);
            _prevScene = SceneClock.Time;
            _ref = SceneClock.Time; _refC = 0;
            _fPiston = (float)SceneClock.Wrapped(AbyssConst.CRANK_PERIOD);
            _fFan = (float)SceneClock.Wrapped(AbyssConst.FAN_PERIOD);
            float nextSample = 0f;
            Debug.Log("[ABYSS] QA soak: " + QaJson.F(_duration, 0) + " s " + (_virtual ? "virtual" : "real") + ", " + (cinematic ? "cinematic" : "autowalk") + " loop");

            while (_elapsed < _duration)
            {
                yield return null;
                float dt = Time.deltaTime;
                _elapsed += _virtual ? dt : Time.unscaledDeltaTime;
                _frames++;
                TrackClock(dt);
                if (_elapsed >= nextSample) { Sample(); nextSample += SampleInterval; }

                var pc = PlayerController.Instance;
                walker = AutoWalker.Instance;
                if (pc != null && walker != null && (pc.PathD >= LoopD || walker.Finished) && !GameState.GoalReached)
                {
                    yield return Restart(pc, walker, cinematic);
                }
                else if (GameState.GoalReached || GameState.Ended)
                {
                    Debug.LogWarning("[ABYSS] QA soak: the goal was reached; the in-place loop cannot continue");
                    break;
                }
            }
            Sample();
            _h.RequestQuit(0.1f);
        }

        IEnumerator Restart(PlayerController pc, AutoWalker walker, bool cinematic)
        {
            _loops++;
            walker.Stop();
            var lift = LiftController.Instance;
            if (lift != null) lift.ResetToTop();
            pc.TeleportToD(0f, false);
            yield return null;
            _frames++;
            TrackClock(Time.deltaTime);     // the clock keeps running through the restart frame
            walker.Begin(cinematic);
            Debug.Log("[ABYSS] QA soak: loop " + _loops + " restarted at t = " + QaJson.F(SceneClock.Time) + " (elapsed " + QaJson.F(_elapsed, 0) + " s)");
        }

        void TrackClock(float dt)
        {
            double now = SceneClock.Time;
            if (now != _prevScene)
            {
                // ClockDriver added this frame's Time.deltaTime (double += float)
                double y = dt - _refC;
                double t = _ref + y;
                _refC = (t - _ref) - y;
                _ref = t;
                _fPiston += dt; if (_fPiston >= AbyssConst.CRANK_PERIOD) _fPiston -= AbyssConst.CRANK_PERIOD;
                _fFan += dt; if (_fFan >= AbyssConst.FAN_PERIOD) _fFan -= AbyssConst.FAN_PERIOD;
            }
            else
            {
                _ref = now; _refC = 0;
            }
            _prevScene = now;

            double clockDrift = Math.Abs(now - _ref) * 1000.0;
            if (clockDrift > _maxClockDrift) _maxClockDrift = clockDrift;
            double pRef = Wrap(_ref, AbyssConst.CRANK_PERIOD), fRef = Wrap(_ref, AbyssConst.FAN_PERIOD);
            double pd = PhaseDiff(SceneClock.Wrapped(AbyssConst.CRANK_PERIOD), pRef, AbyssConst.CRANK_PERIOD) * 1000.0;
            double fd = PhaseDiff(SceneClock.Wrapped(AbyssConst.FAN_PERIOD), fRef, AbyssConst.FAN_PERIOD) * 1000.0;
            if (pd > _maxPistonDrift) _maxPistonDrift = pd;
            if (fd > _maxFanDrift) _maxFanDrift = fd;
            double fp = PhaseDiff(_fPiston, pRef, AbyssConst.CRANK_PERIOD) * 1000.0;
            double ff = PhaseDiff(_fFan, fRef, AbyssConst.FAN_PERIOD) * 1000.0;
            if (fp > _maxFloatPiston) _maxFloatPiston = fp;
            if (ff > _maxFloatFan) _maxFloatFan = ff;
            double sh = now - 3600.0 * Math.Floor(now / 3600.0);
            double q = Math.Abs((float)sh - sh) * 1000.0;
            if (q > _maxShaderQuantum) _maxShaderQuantum = q;
        }

        static double Wrap(double t, double p) => t - p * Math.Floor(t / p);

        static double PhaseDiff(double a, double b, double p)
        {
            double d = Math.Abs(a - b);
            return Math.Min(d, p - d);
        }

        void Sample()
        {
            if (_memN >= _mem.Length) return;
            ref MemSample s = ref _mem[_memN++];
            const float MB = 1024f * 1024f;
            s.T = _elapsed;
            s.SceneT = SceneClock.Time;
            s.Loop = _loops;
            s.SysMB = _sys.Valid ? _sys.LastValue / MB : -1f;
            s.MonoMB = Profiler.GetMonoUsedSizeLong() / MB;
            s.AllocMB = Profiler.GetTotalAllocatedMemoryLong() / MB;
            s.ReservedMB = Profiler.GetTotalReservedMemoryLong() / MB;
            s.GfxDriverMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / MB;
            s.GcHeapMB = GC.GetTotalMemory(false) / MB;
        }

        static float Metric(in MemSample s) => s.SysMB > 0f ? s.SysMB : s.AllocMB + s.MonoMB;

        int Finish(bool aborted)
        {
            _sys.Dispose();
            var j = new QaJson();
            j.BeginObject();
            _h.WriteHeader(j, "soak");
            var failures = new System.Collections.Generic.List<string>();
            bool complete = _elapsed >= _duration - 0.5f;
            if (!complete) failures.Add("soak ended after " + QaJson.F(_elapsed, 0) + " of " + QaJson.F(_duration, 0) + " s");
            j.Prop("duration_requested", _duration);
            j.Prop("elapsed", _elapsed);
            j.Prop("virtual", _virtual);
            j.Prop("loops", _loops);
            j.Prop("frames", _frames);
            j.Prop("scene_time_end", SceneClock.Time);

            // memory growth
            int a = -1;
            for (int i = 0; i < _memN; i++) if (_mem[i].T >= WarmupSeconds) { a = i; break; }
            if (a < 0 && _memN > 0) a = 0;
            float tolMb = CliArgs.Float(QaHarness.SoakMemTolFlag, 32f);
            float growth = float.NaN, allowed = float.NaN;
            bool memOk = true;
            if (a >= 0 && _memN > 0)
            {
                float m0 = Metric(in _mem[a]), m1 = Metric(in _mem[_memN - 1]);
                growth = m1 - m0;
                allowed = Mathf.Max(tolMb, 0.05f * m0);
                memOk = growth <= allowed;
                if (!memOk) failures.Add("memory grew " + QaJson.F(growth, 1) + " MB (allowed " + QaJson.F(allowed, 1) + ")");
            }
            j.Obj("memory");
            j.Prop("metric", "system_used_mb (else total allocated + mono)");
            j.Prop("baseline_index", a);
            j.Prop("growth_mb", growth);
            j.Prop("allowed_mb", allowed);
            j.Prop("pass", memOk);
            j.Arr("samples");
            for (int i = 0; i < _memN; i++)
            {
                MemSample s = _mem[i];
                j.BeginObject().Prop("t", s.T).Prop("scene_t", s.SceneT).Prop("loop", s.Loop).Prop("system_mb", s.SysMB).Prop("mono_mb", s.MonoMB)
                 .Prop("allocated_mb", s.AllocMB).Prop("reserved_mb", s.ReservedMB).Prop("gfx_driver_mb", s.GfxDriverMB).Prop("gc_heap_mb", s.GcHeapMB).EndObject();
            }
            j.EndArray();
            j.EndObject();

            // clocks
            bool clockOk = _maxClockDrift < DriftLimitMs && _maxPistonDrift < DriftLimitMs && _maxFanDrift < DriftLimitMs;
            if (!clockOk) failures.Add("clock drift: clock " + QaJson.F(_maxClockDrift, 4) + " ms, piston " + QaJson.F(_maxPistonDrift, 4) + " ms, fan " + QaJson.F(_maxFanDrift, 4) + " ms");
            j.Obj("clock");
            j.Prop("limit_ms", DriftLimitMs);
            j.Prop("scene_clock_drift_ms", _maxClockDrift);
            j.Prop("piston_mod120_drift_ms", _maxPistonDrift);
            j.Prop("fan_mod40_drift_ms", _maxFanDrift);
            j.Prop("float_accumulator_piston_drift_ms", _maxFloatPiston);
            j.Prop("float_accumulator_fan_drift_ms", _maxFloatFan);
            j.Prop("shader_time_quantum_ms", _maxShaderQuantum);
            j.Prop("pass", clockOk);
            j.EndObject();

            QaLog.Write(j, "log");
            if (QaLog.Total > 0) failures.Add(QaLog.Total + " error / exception / assert messages logged");
            _h.Monitor.Write(j);
            if (_h.Monitor.CameraInsideFrames > 0) failures.Add("camera inside a corridor collider for " + _h.Monitor.CameraInsideFrames + " frames");
            var pc = PlayerController.Instance;
            int safety = pc != null ? pc.SafetyReturns : -1;
            if (safety != 0) failures.Add("safety returns " + safety);
            if (aborted) failures.Add("aborted by -quit-after");
            bool pass = failures.Count == 0;
            j.Arr("failures");
            for (int i = 0; i < failures.Count; i++) j.Value(failures[i]);
            j.EndArray();
            j.Prop("pass", pass);
            j.EndObject();
            string path = Path.Combine(QaPaths.OutDir, "soak.json");
            j.Save(path);
            Debug.Log("[ABYSS] QA soak " + (pass ? "PASS" : "FAIL") + " -> " + path);
            return pass ? 0 : 1;
        }
    }
}
