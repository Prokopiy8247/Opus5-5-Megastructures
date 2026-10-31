// WallBot: the wall-hugging bot (MASTER_PROMPT 11.8, 14.4.3, 14.10.4; Part 10 item 9).
// Active with -wallbot. Walks the whole path twice in virtual time (1/60 s frames): first hugging the
// left edge, then the right edge, with the lateral target pushed 0.5 m beyond +-HalfWidth(s), so the
// player's clamp has to hold everywhere, including every transition window of 14.6 and the funnel.
// Every frame (after settling) it asserts:
//   |Lateral| <= HalfWidth(S) + 1 mm (the clamp holds),
//   the bot really touches the wall (|Lateral| within 5 cm of the edge on at least 90 percent of frames),
//   the eye is within 2 cm (+ head bob) of PathY(S) + 1.7 where the floor grade has been constant for
//   3 m, either directly or after the steady camera-follow lag grade * speed * 0.12 s (14.12 smoothing),
//   and the player never falls (eye more than 0.5 m below the floor).
// It also reports the lateral offset measured independently from the eye position. Ends with PathGuide
// KillCount 0, CoverageCheck misses 0, zero error logs. Report: <report dir>/wallbot_<mode>.json.
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace Megastructure.QA
{
    [DefaultExecutionOrder(-50)]
    public sealed class WallBot : QaRunnerBase
    {
        public const double LateralTolerance = 0.001;   // |lateral| <= HalfWidth + 1 mm
        public const double EyeTolerance = 0.02;        // eye within 2 cm of PathY + 1.7
        public const double PushBeyond = 0.5;           // lateral target 0.5 m beyond the edge
        const double SettleSeconds = 1.0;
        const double HugSlack = 0.05;
        const double MinHugFraction = 0.9;
        const double IndependentTolerance = 0.02;
        const double FallDrop = 0.5;
        const double GradeLookBack = 3.0;
        const double PassEndS = 857.5;                  // pass 1 ends before the beacon trigger (858)
        const double RestSpeed = 0.01, RestHoldSeconds = 0.5;
        const double StallSeconds = 15.0, StallDistance = 0.1, TimeoutMarginSeconds = 60.0;

        protected override QaRunMode Mode => QaRunMode.WallBot;
        protected override string ReportName => "wallbot_" + m_mode;
        protected override string ReportTag => "WALLBOT";

        sealed class PassStats
        {
            public string side;
            public bool completed;
            public double sStart = double.NaN, sEnd = double.NaN, seconds;
            public long frames, checkedFrames, hugFrames;
            public int lateralViolations, eyeViolations, falls, independentViolations;
            public double maxExcess = double.NegativeInfinity, maxExcessS = double.NaN;
            public double maxIndependentExcess = double.NegativeInfinity, maxIndependentS = double.NaN;
            public double maxEyeErr, maxEyeErrS = double.NaN, maxEyeDevRaw;
            public double HugFraction => checkedFrames > 0 ? (double)hugFrames / checkedFrames : 0;
        }

        // per transition window (14.6) plus the funnel s 350-400 (14.4.3)
        struct WindowStats { public double s0, s1, maxExcess, minGap, maxEyeErr; public long frames; }

        float m_speed;
        string m_mode = "walk";
        readonly PassStats[] m_passes = { new PassStats { side = "left" }, new PassStats { side = "right" } };
        WindowStats[] m_windows;
        int m_pass = -1;
        bool m_walking;
        double m_passT0, m_settleUntil, m_lastS, m_lastMoveT, m_restSince = -1, m_t;

        protected override void Awake()
        {
            base.Awake();
            if (!enabled) return;
            m_speed = QaMode.DriveSpeed;
            m_mode = QaMode.SpeedName(m_speed);
            int n = ShotPlan.TransitionWindows.GetLength(0);
            m_windows = new WindowStats[n + 1];
            for (int i = 0; i < n; i++) m_windows[i] = NewWindow(ShotPlan.TransitionWindows[i, 0], ShotPlan.TransitionWindows[i, 1]);
            m_windows[n] = NewWindow(FunnelS0, FunnelS1);
        }

        // 14.4.3: the 10 -> 1.15 m funnel between s = 350 and 400
        const double FunnelS0 = 350.0, FunnelS1 = 400.0;

        static WindowStats NewWindow(double a, double b) => new WindowStats { s0 = a, s1 = b, maxExcess = double.NegativeInfinity, minGap = double.PositiveInfinity };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsurePresent() { EnsureRunner<WallBot>(QaRunMode.WallBot); }

        IEnumerator Start()
        {
            yield return WaitForWorld();
            if (!WorldOk) { Checks.Add("worldReady", false, "GameBootstrap.Ready never became true"); Done("world not ready"); yield break; }
            QaDrive.Prepare(true, false);
            yield return null;
            yield return null;
            RunStaticChecks();
            if (ThreadServices.Player == null) { Checks.Add("playerService", false, "ThreadServices.Player is null"); Done("no player"); yield break; }
            BeginPass(0);
            m_walking = true;
            Debug.Log("[THREAD] WALLBOT start speed=" + m_speed.ToString("0.00", CultureInfo.InvariantCulture));
        }

        void BeginPass(int pass)
        {
            m_pass = pass;
            var p = ThreadServices.Player;
            double side = pass == 0 ? -1.0 : 1.0;
            p.Frozen = false;
            p.Teleport(0, side * ThreadMath.HalfWidth(0));
            m_passT0 = Time.timeAsDouble;
            m_settleUntil = SettleSeconds;
            m_lastS = 0;
            m_lastMoveT = 0;
            m_restSince = -1;
            m_passes[pass].sStart = 0;
            Debug.Log("[THREAD] WALLBOT pass " + (pass + 1) + " (" + m_passes[pass].side + " edge)");
        }

        void Update()
        {
            if (!m_walking || Finished || m_pass < 0) return;
            var p = ThreadServices.Player;
            if (p == null) { Done("player service lost"); return; }
            PassStats ps = m_passes[m_pass];
            m_t = Time.timeAsDouble - m_passT0;
            double s = p.S;
            ps.frames++;

            if (m_t >= m_settleUntil) Check(p, ps, s);

            // drive: push beyond the edge of this pass
            double side = m_pass == 0 ? -1.0 : 1.0;
            if (!p.Finished) p.SetDrive(true, m_speed, (float)(side * (ThreadMath.HalfWidth(s) + PushBeyond)));
            QaDrive.LookForward(s, (float)ThreadConstants.GazeDefaultPitch);

            if (s > m_lastS + StallDistance) { m_lastS = s; m_lastMoveT = m_t; }
            if (!p.Finished && m_t - m_lastMoveT > StallSeconds) { Done("stalled at s=" + s.ToString("0.00", CultureInfo.InvariantCulture) + " (pass " + (m_pass + 1) + ")"); return; }
            if (m_t > ThreadConstants.PathLength / m_speed + TimeoutMarginSeconds) { Done("timeout in pass " + (m_pass + 1)); return; }

            if (m_pass == 0 && s >= PassEndS)
            {
                ps.completed = true; ps.sEnd = s; ps.seconds = m_t;
                BeginPass(1);
                return;
            }
            if (m_pass == 1 && p.Finished)
            {
                if (Math.Abs(p.Speed) < RestSpeed)
                {
                    if (m_restSince < 0) m_restSince = m_t;
                    if (m_t - m_restSince >= RestHoldSeconds) { ps.completed = true; ps.sEnd = s; ps.seconds = m_t; Done(null); }
                }
                else m_restSince = -1;
            }
        }

        void Check(IPlayerState p, PassStats ps, double s)
        {
            ps.checkedFrames++;
            double hw = ThreadMath.HalfWidth(s);
            double lat = p.Lateral;
            double excess = Math.Abs(lat) - hw;
            if (excess > ps.maxExcess) { ps.maxExcess = excess; ps.maxExcessS = s; }
            if (excess > LateralTolerance) ps.lateralViolations++;
            if (excess >= -HugSlack) ps.hugFrames++;

            // lateral measured independently from the eye position (informational)
            Vector3 eye = p.EyePosition;
            var e = new D3(eye.x, eye.y - ThreadConstants.EyeHeight, eye.z);
            double sp = ThreadMath.ProjectToPath(e, s);
            D3 pp = ThreadMath.PathPoint(sp);
            ThreadMath.Right(sp, out double rx, out double rz);
            double latI = (e.x - pp.x) * rx + (e.z - pp.z) * rz;
            double exI = Math.Abs(latI) - ThreadMath.HalfWidth(sp);
            if (exI > ps.maxIndependentExcess) { ps.maxIndependentExcess = exI; ps.maxIndependentS = s; }
            if (exI > IndependentTolerance) ps.independentViolations++;

            // eye height
            double dev = eye.y - (ThreadMath.PathY(s) + ThreadConstants.EyeHeight);
            if (Math.Abs(dev) > Math.Abs(ps.maxEyeDevRaw)) ps.maxEyeDevRaw = dev;
            QaDrive.GradeRangeBehind(s, GradeLookBack, out double gMin, out double gMax);
            double v = Math.Abs(p.Speed), tau = ThreadConstants.CameraYFollowTime;
            double err, allowed;
            if (gMax - gMin < 1e-6)
            {
                double lag = ThreadMath.PathGrade(s) * v * tau;   // steady lag of a critically damped follow
                err = Math.Min(Math.Abs(dev), Math.Abs(dev + lag));
                allowed = EyeTolerance + ThreadConstants.HeadBobMax;
            }
            else
            {
                err = Math.Abs(dev);
                allowed = EyeTolerance + ThreadConstants.HeadBobMax + 2.0 * gMax * v * tau;
            }
            if (err > ps.maxEyeErr) { ps.maxEyeErr = err; ps.maxEyeErrS = s; }
            if (err > allowed) ps.eyeViolations++;
            if (dev < -FallDrop) ps.falls++;

            for (int i = 0; i < m_windows.Length; i++)
            {
                if (s < m_windows[i].s0 || s > m_windows[i].s1) continue;
                m_windows[i].frames++;
                if (excess > m_windows[i].maxExcess) m_windows[i].maxExcess = excess;
                if (-excess < m_windows[i].minGap) m_windows[i].minGap = -excess;
                if (err > m_windows[i].maxEyeErr) m_windows[i].maxEyeErr = err;
            }
        }

        void Done(string abort)
        {
            m_walking = false;
            var inv = CultureInfo.InvariantCulture;
            Checks.Add("completedBothPasses", abort == null && m_passes[0].completed && m_passes[1].completed, abort ?? "both passes completed");
            for (int i = 0; i < m_passes.Length; i++)
            {
                PassStats ps = m_passes[i];
                string n = ps.side;
                Checks.Add(n + "ClampHolds", ps.lateralViolations == 0 && ps.checkedFrames > 0,
                           ps.lateralViolations + " frames beyond HalfWidth + 1 mm, max excess " + Mm(ps.maxExcess) + " mm at s=" + ps.maxExcessS.ToString("0.0", inv));
                Checks.Add(n + "HugsWall", ps.HugFraction >= MinHugFraction, "at the wall on " + (100.0 * ps.HugFraction).ToString("0.0", inv) + " % of checked frames");
                Checks.Add(n + "EyeHeight", ps.eyeViolations == 0, ps.eyeViolations + " frames off, max error " + Mm(ps.maxEyeErr) + " mm at s=" + ps.maxEyeErrS.ToString("0.0", inv));
                Checks.Add(n + "NeverFalls", ps.falls == 0, ps.falls + " frames with the eye more than 0.5 m below the floor");
                Checks.Add(n + "IndependentLateral", ps.independentViolations == 0, ps.independentViolations + " frames, max excess " + Mm(ps.maxIndependentExcess) + " mm (eye-position measurement)", false);
            }
            var bd = BeatDirector.Instance;
            if (bd != null) Checks.Add("goalOnce", bd.GoalCount == 1, "goal count " + bd.GoalCount, false);
            AddCommonChecks();
            string summary = "mode=" + m_mode + " left max excess " + Mm(m_passes[0].maxExcess) + " mm, right max excess " + Mm(m_passes[1].maxExcess) + " mm, errors " + QaLog.ErrorCount;
            Complete(summary, WriteBody);
        }

        static string Mm(double m) => double.IsInfinity(m) || double.IsNaN(m) ? "n/a" : (m * 1000.0).ToString("0.0", CultureInfo.InvariantCulture);

        void WriteBody(QaJson j)
        {
            j.Prop("mode", m_mode).Prop("speed", m_speed).Prop("pushBeyond", PushBeyond).Prop("lateralTolerance", LateralTolerance).Prop("eyeTolerance", EyeTolerance);
            j.Key("passes").BeginArray();
            for (int i = 0; i < m_passes.Length; i++)
            {
                PassStats ps = m_passes[i];
                j.BeginObject();
                j.Prop("side", ps.side).Prop("completed", ps.completed).Prop("sStart", ps.sStart).Prop("sEnd", ps.sEnd).Prop("seconds", ps.seconds);
                j.Prop("frames", ps.frames).Prop("checkedFrames", ps.checkedFrames).Prop("hugFraction", ps.HugFraction);
                j.Prop("lateralViolations", ps.lateralViolations).Prop("maxExcessM", ps.maxExcess).Prop("maxExcessS", ps.maxExcessS);
                j.Prop("independentViolations", ps.independentViolations).Prop("maxIndependentExcessM", ps.maxIndependentExcess).Prop("maxIndependentS", ps.maxIndependentS);
                j.Prop("eyeViolations", ps.eyeViolations).Prop("maxEyeErrM", ps.maxEyeErr).Prop("maxEyeErrS", ps.maxEyeErrS).Prop("maxEyeDevRawM", ps.maxEyeDevRaw);
                j.Prop("falls", ps.falls);
                j.EndObject();
            }
            j.EndArray();
            j.Key("windows").BeginArray();
            for (int i = 0; i < m_windows.Length; i++)
            {
                WindowStats w = m_windows[i];
                j.BeginObject(true).Prop("s0", w.s0).Prop("s1", w.s1).Prop("frames", w.frames).Prop("maxExcessM", w.maxExcess).Prop("minGapM", w.minGap).Prop("maxEyeErrM", w.maxEyeErr).EndObject();
            }
            j.EndArray();
            j.Prop("coverage", CoverageNote).Prop("coverageMisses", CoverageMisses);
        }
    }
}
