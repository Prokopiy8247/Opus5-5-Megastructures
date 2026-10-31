// SoakRunner: the long-run test (MASTER_PROMPT 11.8, Part 10 item 31).
// Active with -soak S (seconds of real time). Not part of the scene: added at runtime next to
// GameBootstrap. Loops the autowalk (1.5 m/s, or -brisk / -speed) in real time with vSync on,
// teleporting back to s = 0 just before the beacon trigger so the ending never starts. Samples
// GC.GetTotalMemory, Profiler.GetTotalAllocatedMemoryLong, the Mono heap and GC.CollectionCount every
// 10 s into a preallocated array. Asserts: memory growth below 50 MB (baseline after a warm-up, final
// = minimum of the last three samples, so a pending collection does not count), zero error logs, no
// kill-volume events, finite progress and an ambient clock that kept pace with game time (no drift).
// Report: <report dir>/soak.json; exit code 0 / 1.
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Profiling;

namespace Megastructure.QA
{
    [DefaultExecutionOrder(-50)]
    public sealed class SoakRunner : QaRunnerBase
    {
        public const double SampleIntervalSeconds = 10.0;
        public const double GrowthLimitBytes = 50.0 * 1024 * 1024;
        const double LoopEndS = 857.0;                 // before the beacon trigger at 858
        const double WarmupFraction = 0.1, WarmupMaxSeconds = 60.0;
        const double DriftLimitSeconds = 1.0;
        const double HitchMs = 33.0;

        protected override QaRunMode Mode => QaRunMode.Soak;
        protected override string ReportName => "soak";
        protected override string ReportTag => "SOAK";

        struct Sample
        {
            public double t, s, ambient;
            public long gcBytes, totalAllocated, monoUsed;
            public int gc0, gc1, gc2, loops;
        }

        Sample[] m_samples;
        int m_count;
        double m_duration, m_t0, m_nextSample, m_loopStart, m_minLoop = double.MaxValue, m_maxLoop;
        double m_ambient0, m_time0, m_maxFrameMs;
        int m_loops, m_hitches, m_nonFinite;
        long m_frames;
        float m_speed;
        bool m_running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePresent() { EnsureRunner<SoakRunner>(QaRunMode.Soak); }

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            m_duration = Math.Max(1.0, CliArgs.SoakSeconds);
            m_speed = QaMode.DriveSpeed;
            m_samples = new Sample[(int)Math.Ceiling(m_duration / SampleIntervalSeconds) + 4];
        }

        IEnumerator Start()
        {
            yield return WaitForWorld();
            if (!WorldOk) { Checks.Add("worldReady", false, "GameBootstrap.Ready never became true"); Done("world not ready"); yield break; }
            QaDrive.Prepare(false, true);   // real time, vSync on (the shipped configuration)
            yield return null;
            RunStaticChecks();
            var p = ThreadServices.Player;
            if (p == null) { Checks.Add("playerService", false, "ThreadServices.Player is null"); Done("no player"); yield break; }
            p.Frozen = false;
            p.Teleport(0, 0);
            yield return null;
            m_t0 = Time.realtimeSinceStartupAsDouble;
            m_loopStart = 0;
            m_nextSample = 0;
            m_ambient0 = ThreadClock.Ambient;
            m_time0 = Time.timeAsDouble;
            m_running = true;
            Debug.Log("[THREAD] SOAK start " + m_duration.ToString("0", CultureInfo.InvariantCulture) + " s real time, speed " + m_speed.ToString("0.00", CultureInfo.InvariantCulture));
        }

        void Update()
        {
            if (!m_running || Finished) return;
            var p = ThreadServices.Player;
            if (p == null) { Done("player service lost"); return; }
            double t = Time.realtimeSinceStartupAsDouble - m_t0;
            double s = p.S;
            m_frames++;
            if (m_frames > 2)
            {
                double ms = Time.unscaledDeltaTime * 1000.0;
                if (ms > m_maxFrameMs) m_maxFrameMs = ms;
                if (ms > HitchMs) m_hitches++;
            }
            if (double.IsNaN(s) || double.IsInfinity(s) || double.IsNaN(p.EyePosition.y)) m_nonFinite++;

            p.SetDrive(true, m_speed, 0f);
            QaDrive.LookForward(s, (float)ThreadConstants.GazeDefaultPitch);
            if (s >= LoopEndS)
            {
                double lt = t - m_loopStart;
                if (lt < m_minLoop) m_minLoop = lt;
                if (lt > m_maxLoop) m_maxLoop = lt;
                m_loops++;
                m_loopStart = t;
                p.Teleport(0, 0);
            }

            if (t >= m_nextSample) { TakeSample(t, s); m_nextSample += SampleIntervalSeconds; }
            if (t >= m_duration) { TakeSample(t, s); Done(null); }
        }

        void TakeSample(double t, double s)
        {
            if (m_count >= m_samples.Length) return;
            m_samples[m_count++] = new Sample
            {
                t = t, s = s, ambient = ThreadClock.Ambient,
                gcBytes = GC.GetTotalMemory(false),
                totalAllocated = Profiler.GetTotalAllocatedMemoryLong(),
                monoUsed = Profiler.GetMonoUsedSizeLong(),
                gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2),
                loops = m_loops,
            };
        }

        /// <summary>Growth between the minimum over the warm-up window and the minimum of the last three samples.</summary>
        public static double Growth(long[] values, double[] times, int count, double warmup)
        {
            if (count < 2) return 0;
            long baseMin = long.MaxValue;
            for (int i = 0; i < count; i++)
                if (times[i] >= warmup && times[i] <= warmup + 3 * SampleIntervalSeconds && values[i] < baseMin) baseMin = values[i];
            if (baseMin == long.MaxValue) baseMin = values[0];
            long endMin = long.MaxValue;
            for (int i = Math.Max(0, count - 3); i < count; i++) if (values[i] < endMin) endMin = values[i];
            return endMin - baseMin;
        }

        void Done(string abort)
        {
            m_running = false;
            var inv = CultureInfo.InvariantCulture;
            double warm = Math.Min(WarmupMaxSeconds, m_duration * WarmupFraction);
            var times = new double[m_count];
            var gc = new long[m_count];
            var total = new long[m_count];
            var mono = new long[m_count];
            for (int i = 0; i < m_count; i++) { times[i] = m_samples[i].t; gc[i] = m_samples[i].gcBytes; total[i] = m_samples[i].totalAllocated; mono[i] = m_samples[i].monoUsed; }
            double gGc = Growth(gc, times, m_count, warm), gTotal = Growth(total, times, m_count, warm), gMono = Growth(mono, times, m_count, warm);
            double elapsed = m_count > 0 ? m_samples[m_count - 1].t : 0;

            Checks.Add("ranFullDuration", abort == null && elapsed >= m_duration - 1.0, abort ?? (elapsed.ToString("0.0", inv) + " s of " + m_duration.ToString("0", inv) + " s"));
            Checks.Add("managedGrowthUnder50MB", gGc < GrowthLimitBytes, MB(gGc) + " MB (GC.GetTotalMemory)");
            Checks.Add("totalGrowthUnder50MB", gTotal < GrowthLimitBytes, MB(gTotal) + " MB (Profiler.GetTotalAllocatedMemoryLong)");
            Checks.Add("monoGrowth", gMono < GrowthLimitBytes, MB(gMono) + " MB (Mono heap)", false);
            Checks.Add("finiteState", m_nonFinite == 0, m_nonFinite + " frames with NaN/Inf progress or eye position");
            if (!ThreadClock.Frozen && m_count > 0)
            {
                double drift = (ThreadClock.Ambient - m_ambient0) - (Time.timeAsDouble - m_time0);
                Checks.Add("ambientClockNoDrift", Math.Abs(drift) < DriftLimitSeconds, "ambient minus game time " + drift.ToString("0.000", inv) + " s", false);
            }
            Checks.Add("loopsCompleted", m_loops > 0 || m_duration < ThreadConstants.PathLength / m_speed, m_loops + " loops", false);
            Checks.Add("hitches", true, m_hitches + " frames above 33 ms, max " + m_maxFrameMs.ToString("0.0", inv) + " ms", false);
            AddCommonChecks();
            string summary = m_duration.ToString("0", inv) + " s, loops " + m_loops + ", growth managed " + MB(gGc) + " MB / total " + MB(gTotal) + " MB, errors " + QaLog.ErrorCount;
            Complete(summary, j => WriteBody(j, gGc, gTotal, gMono, warm));
        }

        static string MB(double bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.00", CultureInfo.InvariantCulture);

        void WriteBody(QaJson j, double gGc, double gTotal, double gMono, double warm)
        {
            j.Prop("durationSeconds", m_duration).Prop("speed", m_speed).Prop("warmupSeconds", warm);
            j.Prop("loops", m_loops).Prop("minLoopSeconds", m_minLoop == double.MaxValue ? double.NaN : m_minLoop).Prop("maxLoopSeconds", m_maxLoop);
            j.Prop("frames", m_frames).Prop("maxFrameMs", m_maxFrameMs).Prop("framesAbove33ms", m_hitches);
            j.Prop("growthManagedBytes", gGc).Prop("growthTotalBytes", gTotal).Prop("growthMonoBytes", gMono).Prop("limitBytes", GrowthLimitBytes);
            j.Key("samples").BeginArray();
            for (int i = 0; i < m_count; i++)
            {
                Sample sm = m_samples[i];
                j.BeginObject(true).Prop("t", sm.t).Prop("s", sm.s).Prop("ambient", sm.ambient).Prop("gc", sm.gcBytes).Prop("total", sm.totalAllocated)
                 .Prop("mono", sm.monoUsed).Prop("gc0", sm.gc0).Prop("gc1", sm.gc1).Prop("gc2", sm.gc2).Prop("loops", sm.loops).EndObject();
            }
            j.EndArray();
        }
    }
}
