using System.Collections;
using System.IO;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// -autotest (Part 11.8, 14.10 functional): the AutoWalker (non-cinematic, 1.5 m/s) walks from the start to the goal in
    /// virtual time (Time.captureDeltaTime = 1/60, VSync off; -realtime for wall-clock time) while QaMonitor watches the
    /// camera, beats and lift. Pass when: the walk finishes at 658.03 +/- 8 s on the scene clock; beats 1-7 fired once each,
    /// in order (BeatDirector.Fired) and the beat index never stepped back; the ride started at 308.36 +/- 1 s and ended at
    /// 428.36 +/- 1 s; the lift camera offset error stayed at most 0.5 mm; no Error / Exception / Assert was logged; the
    /// camera never overlapped a Corridor collider; no safety return happened. Writes autotest.json under -qa-out (QA/) three
    /// virtual seconds after the finish (so errors of the ending are counted) and exits 0 / 1.
    /// </summary>
    public sealed class QaAutoTest
    {
        public const double FinishTolerance = 8.0, LiftTolerance = 1.0;
        public const float MaxLiftOffsetMm = 0.5f;
        public const float TailSeconds = 3f;

        readonly QaHarness _h;
        double _walkStart = -1, _finish = -1;
        float _realStart;
        bool _begunByHarness;

        public QaAutoTest(QaHarness h) { _h = h; }

        public IEnumerator Run()
        {
            _h.AddFinalizer(Finish);
            yield return _h.WaitForLoad(false);
            _walkStart = SceneClock.Time;
            _realStart = Time.realtimeSinceStartup;

            // the AutoWalker begins by itself with -autotest (AutoBeginFromFlags); make sure it does
            for (int i = 0; i < 90; i++)
            {
                var w0 = AutoWalker.Instance;
                if (w0 != null && w0.IsRunning) break;
                yield return null;
            }
            var walker = AutoWalker.Instance;
            if (walker != null && !walker.IsRunning)
            {
                walker.Begin(false);
                _begunByHarness = true;
            }

            double timeout = AbyssConst.TOTAL_DURATION + 60.0;
            while (true)
            {
                walker = AutoWalker.Instance;
                if (walker != null && walker.Finished) { _finish = SceneClock.Time; break; }
                if (walker == null || SceneClock.Time - _walkStart > timeout) break;
                yield return null;
            }
            Debug.Log("[ABYSS] QA autotest: " + (_finish >= 0 ? "finished at t = " + QaJson.F(_finish) : "did not finish") + ", writing the report in " + TailSeconds + " s");
            _h.RequestQuit(TailSeconds);
        }

        int Finish(bool aborted)
        {
            var j = new QaJson();
            j.BeginObject();
            _h.WriteHeader(j, "autotest");
            QaMonitor m = _h.Monitor;
            var failures = new System.Collections.Generic.List<string>();

            // finish time
            bool finished = _finish >= 0;
            double expected = AbyssConst.TOTAL_DURATION;
            bool finishOk = finished && System.Math.Abs(_finish - expected) <= FinishTolerance;
            if (!finishOk) failures.Add(finished ? "finish time " + QaJson.F(_finish) + " outside " + expected + " +/- " + FinishTolerance : "walk did not finish");
            j.Obj("finish");
            j.Prop("finished", finished);
            j.Prop("time", _finish);
            j.Prop("walk_start", _walkStart);
            j.Prop("expected", expected);
            j.Prop("tolerance", FinishTolerance);
            j.Prop("walker_begun_by_harness", _begunByHarness);
            j.Prop("pass", finishOk);
            j.EndObject();

            // beats
            BeatDirector bd = BeatDirector.Instance;
            bool order = true;
            int nFired = 0;
            j.Obj("beats");
            j.Arr("fired");
            if (bd != null)
            {
                for (int i = 0; i < bd.Fired.Count; i++)
                {
                    var f = bd.Fired[i];
                    j.BeginObject().Prop("beat", f.beat).Prop("time", f.time).EndObject();
                    if (f.beat != i + 1) order = false;
                    nFired++;
                }
            }
            j.EndArray();
            bool beatsOk = bd != null && nFired == 7 && order && m.BeatDecreases == 0;
            if (!beatsOk) failures.Add("beats: fired " + nFired + "/7, in order " + order + ", backward steps " + m.BeatDecreases);
            j.Prop("in_order", order);
            j.Prop("count", nFired);
            j.Prop("backward_steps", m.BeatDecreases);
            j.Prop("pass", beatsOk);
            j.EndObject();

            // lift
            bool liftStartOk = m.LiftStart >= 0 && System.Math.Abs(m.LiftStart - AbyssConst.T_RIDE_START) <= LiftTolerance;
            bool liftEndOk = m.LiftEnd >= 0 && System.Math.Abs(m.LiftEnd - AbyssConst.T_RIDE_END) <= LiftTolerance;
            float offMm = m.LiftMaxOffsetErrorM >= 0f ? m.LiftMaxOffsetErrorM * 1000f : -1f;
            bool offsetOk = offMm >= 0f && offMm <= MaxLiftOffsetMm;
            if (!liftStartOk) failures.Add("lift start " + QaJson.F(m.LiftStart) + " outside " + AbyssConst.T_RIDE_START + " +/- " + LiftTolerance);
            if (!liftEndOk) failures.Add("lift end " + QaJson.F(m.LiftEnd) + " outside " + AbyssConst.T_RIDE_END + " +/- " + LiftTolerance);
            if (!offsetOk) failures.Add("lift camera offset error " + QaJson.F(offMm, 4) + " mm (limit " + MaxLiftOffsetMm + ", samples " + m.LiftOffsetSamples + ")");
            j.Obj("lift");
            j.Prop("dwell", m.LiftDwell);
            j.Prop("start", m.LiftStart);
            j.Prop("end", m.LiftEnd);
            j.Prop("start_expected", AbyssConst.T_RIDE_START);
            j.Prop("end_expected", AbyssConst.T_RIDE_END);
            j.Prop("tolerance", LiftTolerance);
            j.Prop("max_offset_error_mm", offMm);
            j.Prop("offset_samples", m.LiftOffsetSamples);
            j.Prop("offset_limit_mm", MaxLiftOffsetMm);
            j.Prop("rides", m.LiftRides);
            j.Prop("pass", liftStartOk && liftEndOk && offsetOk);
            j.EndObject();

            // errors, camera, safety
            QaLog.Write(j, "log");
            bool logOk = QaLog.Total == 0;
            if (!logOk) failures.Add(QaLog.Total + " error / exception / assert messages logged");
            m.Write(j);
            if (m.CameraInsideFrames > 0) failures.Add("camera inside a corridor collider for " + m.CameraInsideFrames + " frames (first at d " + QaJson.F(m.FirstInsideD) + ")");
            var pc = PlayerController.Instance;
            int safety = pc != null ? pc.SafetyReturns : -1;
            if (safety != 0) failures.Add("safety returns " + safety);
            j.Prop("player_safety_returns", safety);
            if (aborted) failures.Add("aborted by -quit-after before the end");

            bool pass = failures.Count == 0;
            j.Prop("frames", Time.frameCount);
            j.Prop("real_seconds", Time.realtimeSinceStartup - _realStart);
            j.Prop("aborted", aborted);
            j.Arr("failures");
            for (int i = 0; i < failures.Count; i++) j.Value(failures[i]);
            j.EndArray();
            j.Prop("pass", pass);
            j.EndObject();

            string path = Path.Combine(QaPaths.OutDir, "autotest.json");
            j.Save(path);
            Debug.Log("[ABYSS] QA autotest " + (pass ? "PASS" : "FAIL") + " -> " + path + (pass ? "" : " (" + string.Join("; ", failures) + ")"));
            return pass ? 0 : 1;
        }
    }
}
