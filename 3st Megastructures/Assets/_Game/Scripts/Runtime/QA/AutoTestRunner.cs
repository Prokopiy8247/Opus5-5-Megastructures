// AutoTestRunner: autoplay completability (MASTER_PROMPT 11.8, 14.10.4).
// Active with -autotest (and not -capture / -soak / -wallbot, which have their own runners). After
// GameBootstrap.Ready it sets Time.captureDeltaTime = 1/60 (virtual time, deterministic), teleports to
// s = 0 and walks at 1.5 m/s (2.5 with -brisk, -speed X) through IPlayerState.SetDrive with a forward
// look. Asserts: the goal is reached and the player comes to rest at s = 860 within 573.33 +- 8 s of
// virtual time (344 +- 5 with -brisk), beats 1..7 entered once and in order, the goal fired once,
// zero error log messages, PathGuide KillCount 0 and CoverageCheck misses 0. Writes
// <report dir>/autotest_<mode>.json, logs "[THREAD] AUTOTEST PASS/FAIL ..." and exits 0 / 1.
// Zero allocations per frame while walking.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Megastructure.QA
{
    [DefaultExecutionOrder(-50)]
    public sealed class AutoTestRunner : QaRunnerBase
    {
        public static AutoTestRunner Instance { get; private set; }

        // 14.10.4: 573 +- 8 s at 1.5 m/s, 344 +- 5 s at 2.5 m/s
        public const double WalkToleranceSeconds = 8.0, BriskToleranceSeconds = 5.0;
        const double RestSpeed = 0.01;            // m/s: at rest
        const double RestHoldSeconds = 0.5;
        const double GoalRestTolerance = 0.1;     // comes to rest at s = 860 (14.11)
        const double StallSeconds = 15.0, StallDistance = 0.1;
        const double RestTimeoutSeconds = 20.0;
        const double TimeoutMarginSeconds = 60.0;

        protected override QaRunMode Mode => QaRunMode.AutoTest;
        protected override string ReportName => "autotest_" + m_mode;
        protected override string ReportTag => "AUTOTEST";

        float m_speed;
        string m_mode = "walk";
        bool m_walking;
        double m_t0, m_expected, m_tolerance;
        double m_tFinished = -1, m_sFinished = double.NaN, m_tRest = -1, m_sRest = double.NaN, m_restSince = -1;
        double m_lastS, m_lastMoveT, m_maxSpeed;
        long m_frames;
        readonly double[] m_beatS = new double[ThreadConstants.BeatCount];
        readonly double[] m_beatT = new double[ThreadConstants.BeatCount];
        readonly int[] m_beatHits = new int[ThreadConstants.BeatCount];
        Action<int, double> m_onBeat;

        /// <summary>Expected virtual seconds from s = 0 at rest to rest at s = 860 for a drive speed.</summary>
        public static double ExpectedSeconds(float speed)
        {
            if (Math.Abs(speed - ThreadConstants.WalkSpeed) < 1e-4) return ThreadConstants.TotalTimeWalk;
            return ThreadConstants.PathLength / Math.Max(0.05, speed);
        }

        public static double ToleranceSeconds(float speed) => speed >= ThreadConstants.BriskSpeed - 1e-4 ? BriskToleranceSeconds : WalkToleranceSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePresent() { EnsureRunner<AutoTestRunner>(QaRunMode.AutoTest); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            Instance = this;
            m_speed = QaMode.DriveSpeed;
            m_mode = QaMode.SpeedName(m_speed);
            m_expected = ExpectedSeconds(m_speed);
            m_tolerance = ToleranceSeconds(m_speed);
            for (int i = 0; i < m_beatS.Length; i++) { m_beatS[i] = double.NaN; m_beatT[i] = double.NaN; }
            m_onBeat = OnBeatEntered;
            BeatDirector.BeatEntered += m_onBeat;
        }

        void OnDestroy()
        {
            if (m_onBeat != null) BeatDirector.BeatEntered -= m_onBeat;
            if (Instance == this) Instance = null;
        }

        void OnBeatEntered(int beat, double s)
        {
            if (beat < 0 || beat >= m_beatS.Length) return;
            m_beatHits[beat]++;
            if (double.IsNaN(m_beatS[beat])) { m_beatS[beat] = s; m_beatT[beat] = m_walking ? Time.timeAsDouble - m_t0 : 0; }
        }

        IEnumerator Start()
        {
            yield return WaitForWorld();
            if (!WorldOk) { Checks.Add("worldReady", false, "GameBootstrap.Ready never became true"); Finish("world not ready"); yield break; }
            QaDrive.Prepare(true, false);
            yield return null;
            yield return null;
            RunStaticChecks();
            var p = ThreadServices.Player;
            if (p == null) { Checks.Add("playerService", false, "ThreadServices.Player is null"); Finish("no player"); yield break; }
            p.Frozen = false;
            p.Teleport(0, 0);
            yield return null;
            m_t0 = Time.timeAsDouble;
            m_lastS = p.S;
            m_lastMoveT = 0;
            m_walking = true;
            Debug.Log("[THREAD] AUTOTEST start mode=" + m_mode + " speed=" + m_speed.ToString("0.00", CultureInfo.InvariantCulture)
                      + " expected=" + m_expected.ToString("0.00", CultureInfo.InvariantCulture) + " +- " + m_tolerance + " s virtual");
        }

        void Update()
        {
            if (!m_walking || Finished) return;
            var p = ThreadServices.Player;
            if (p == null) { Finish("player service lost"); return; }
            double t = Time.timeAsDouble - m_t0;
            double s = p.S;
            m_frames++;
            if (p.Speed > m_maxSpeed) m_maxSpeed = p.Speed;

            if (!p.Finished) p.SetDrive(true, m_speed, 0f);
            else if (m_tFinished < 0)
            {
                m_tFinished = t;
                m_sFinished = s;
                Debug.Log("[THREAD] AUTOTEST goal trigger at s=" + s.ToString("0.000", CultureInfo.InvariantCulture) + " t=" + t.ToString("0.00", CultureInfo.InvariantCulture));
            }
            QaDrive.LookForward(s, (float)ThreadConstants.GazeDefaultPitch);

            if (s > m_lastS + StallDistance) { m_lastS = s; m_lastMoveT = t; }
            if (!p.Finished && t - m_lastMoveT > StallSeconds) { Finish("stalled at s=" + s.ToString("0.00", CultureInfo.InvariantCulture)); return; }

            if (m_tFinished >= 0)
            {
                if (Math.Abs(p.Speed) < RestSpeed)
                {
                    if (m_restSince < 0) m_restSince = t;
                    if (t - m_restSince >= RestHoldSeconds) { m_tRest = m_restSince; m_sRest = s; Finish(null); return; }
                }
                else m_restSince = -1;
                if (t - m_tFinished > RestTimeoutSeconds) { Finish("never came to rest after the trigger"); return; }
            }
            if (t > m_expected + m_tolerance + TimeoutMarginSeconds) Finish("timeout at s=" + s.ToString("0.00", CultureInfo.InvariantCulture));
        }

        void Finish(string abort)
        {
            m_walking = false;
            var inv = CultureInfo.InvariantCulture;
            bool reached = abort == null && m_tRest >= 0;
            Checks.Add("reachedGoal", reached, abort ?? ("rest at t=" + m_tRest.ToString("0.00", inv) + " s, s=" + m_sRest.ToString("0.000", inv)));
            Checks.Add("walkTime", reached && Math.Abs(m_tRest - m_expected) <= m_tolerance,
                       m_tRest.ToString("0.00", inv) + " s vs " + m_expected.ToString("0.00", inv) + " +- " + m_tolerance + " s");
            Checks.Add("triggerAt858", m_tFinished >= 0 && m_sFinished >= ThreadConstants.BeaconTriggerS - 1e-6, "trigger s=" + m_sFinished.ToString("0.000", inv));
            Checks.Add("restAt860", reached && Math.Abs(m_sRest - ThreadConstants.PathLength) <= GoalRestTolerance, "rest s=" + m_sRest.ToString("0.000", inv));
            var bd = BeatDirector.Instance;
            if (bd != null)
            {
                Checks.Add("beatsInOrder", bd.EnteredCount == ThreadConstants.BeatCount && bd.OrderOk, "entered " + bd.EnteredCount + ", orderOk " + bd.OrderOk);
                Checks.Add("goalOnce", bd.GoalCount == 1, "goal count " + bd.GoalCount);
            }
            else Checks.Add("beatDirector", false, "BeatDirector.Instance is null");
            bool once = true;
            for (int i = 0; i < m_beatHits.Length; i++) if (m_beatHits[i] != 1) once = false;
            Checks.Add("beatEventsOnce", once, "BeatEntered events per beat: " + string.Join(",", Array.ConvertAll(m_beatHits, h => h.ToString(inv))));
            AddCommonChecks();

            string summary = "mode=" + m_mode + " t=" + m_tRest.ToString("0.00", inv) + "s (expected " + m_expected.ToString("0.00", inv) + " +- " + m_tolerance + ")"
                             + " beats=" + (bd != null ? bd.EnteredCount : -1) + " errors=" + QaLog.ErrorCount;
            Complete(summary, WriteBody);
        }

        void WriteBody(QaJson j)
        {
            j.Prop("mode", m_mode).Prop("speed", m_speed).Prop("virtualFps", QaDrive.VirtualFps);
            j.Prop("expectedSeconds", m_expected).Prop("toleranceSeconds", m_tolerance);
            j.Prop("triggerSeconds", m_tFinished).Prop("triggerS", m_sFinished);
            j.Prop("restSeconds", m_tRest).Prop("restS", m_sRest);
            j.Prop("frames", m_frames).Prop("maxSpeed", m_maxSpeed);
            var gb = GameBootstrap.Instance;
            if (gb != null) j.Prop("loadSeconds", gb.LoadSeconds);
            j.Key("beats").BeginArray();
            for (int i = 0; i < m_beatS.Length; i++)
            {
                j.BeginObject(true).Prop("beat", i + 1).Prop("name", ThreadConstants.BeatNames[i]).Prop("boundary", ThreadConstants.BeatBoundaries[i])
                 .Prop("enteredS", m_beatS[i]).Prop("enteredT", m_beatT[i]).Prop("events", m_beatHits[i]).EndObject();
            }
            j.EndArray();
            j.Prop("coverage", CoverageNote).Prop("coverageMisses", CoverageMisses);
            j.Prop("devices", Devices);
        }
    }
}
