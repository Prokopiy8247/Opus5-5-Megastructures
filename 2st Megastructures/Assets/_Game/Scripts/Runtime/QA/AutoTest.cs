using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Autoplay completability test (Part 11.8, 14.10), active with -autotest. Waits for the world, starts the
    /// AutoWalker in QA mode (1.5 m/s, seed 1, from s 0; -speed brisk uses 2.5 m/s), records the beat triggers
    /// (RingEvents.BeatEntered, with RingState.Beat polling as a backup), the goal and ending times, the maximum camera
    /// roll, camera-inside-collider frames (own 0.12 m overlap probe after the CameraRig plus the rig's counter when it
    /// has one) and every logged error, then writes QA/autotest.json (or -autotest-out FILE) and quits with 0 only if:
    /// goal at 636.8 +- 3 s, ending complete at 655.3 +- 3 s, exit by 662 s, beats 0..6 once and in order at
    /// s 0, 24, 276, 411, 594, 774, 894, zero errors, roll 0.00 +- 0.01 deg. Exit 1 = a check failed, 2 = timeout,
    /// 3 = the world never became ready. Zero per-frame allocations while walking.
    /// </summary>
    [DefaultExecutionOrder(30010)]
    public sealed class AutoTest : MonoBehaviour
    {
        // 14.10 autoplay numbers (the corner fillets save 3.9 m, 14.1; the ending lasts 18.5 s after the goal, 14.11)
        const float GoalTimeWalk = 636.8f, EndingAfterGoal = 18.5f, ExitMargin = 6.7f, TimeTol = 3f;
        const float BeatSTol = 1.0f, RollTol = 0.01f, ProbeRadius = 0.12f;

        enum Phase { WaitLoad, Walk, Done }
        Phase _phase = Phase.WaitLoad;
        float _speed;
        double _t0 = -1, _goalT = double.NaN, _endT = double.NaN;
        float _goalS = float.NaN;
        float _startReal;
        string _walkHow = "";
        bool _walkStarted;
        float _maxRoll;
        int _probeHits, _frames, _maxFrameAt;
        float _maxDt;
        Vector3 _firstHitPos;
        int _lastBeatPolled = -1;
        readonly Collider[] _hits = new Collider[16];

        struct BeatRec { public int Beat; public float S; public double T; public string Src; }
        readonly BeatRec[] _beats = new BeatRec[32];
        int _beatCount;
        readonly int[] _beatSeen = new int[7];

        Action<int> _onBeat;
        Action _onGoal, _onEnding, _onLoad;
        bool _loadSeen;

        void Awake()
        {
            if (!CliArgs.AutoTest) { enabled = false; return; }
            Application.runInBackground = true;
            _speed = string.Equals(CliArgs.Str("speed", "walk"), "brisk", StringComparison.OrdinalIgnoreCase) ? RingConstants.Brisk : RingConstants.Walk;
            _startReal = Time.realtimeSinceStartup;
            _onBeat = OnBeat;
            _onGoal = OnGoal;
            _onEnding = OnEnding;
            _onLoad = () => _loadSeen = true;
            RingState.AutoTest = true;
        }

        void Start()
        {
            if (!enabled) return;
            Subscribe();
        }

        void Subscribe()
        {
            RingEvents.BeatEntered -= _onBeat; RingEvents.BeatEntered += _onBeat;
            RingEvents.GoalReached -= _onGoal; RingEvents.GoalReached += _onGoal;
            RingEvents.EndingComplete -= _onEnding; RingEvents.EndingComplete += _onEnding;
            RingEvents.LoadComplete -= _onLoad; RingEvents.LoadComplete += _onLoad;
        }

        void OnDestroy()
        {
            RingEvents.BeatEntered -= _onBeat;
            RingEvents.GoalReached -= _onGoal;
            RingEvents.EndingComplete -= _onEnding;
            RingEvents.LoadComplete -= _onLoad;
        }

        double Now => _t0 < 0 ? 0.0 : Time.timeAsDouble - _t0;

        void OnBeat(int b) { AddBeat(b, "event"); }

        void AddBeat(int b, string src)
        {
            if (b < 0 || b > 6) return;
            if (src == "poll" && _beatSeen[b] > 0) return;   // the poll only fills beats the event missed
            _beatSeen[b]++;
            if (_beatCount < _beats.Length) _beats[_beatCount++] = new BeatRec { Beat = b, S = RingState.S, T = Now, Src = src };
        }

        void OnGoal()
        {
            if (double.IsNaN(_goalT)) { _goalT = Now; _goalS = RingState.S; }
        }

        void OnEnding()
        {
            if (double.IsNaN(_endT)) _endT = Now;
        }

        void Update()
        {
            QaLog.MainFrame = Time.frameCount;
            switch (_phase)
            {
                case Phase.WaitLoad:
                    if (_loadSeen || RingRig.WorldReady)
                    {
                        Subscribe();
                        RingState.AutoTest = true;
                        _walkStarted = RingQaHooks.TryStartAutoWalk(_speed, CliArgs.Seed, 0f, out _walkHow);
                        Debug.Log("[Ring] AutoTest: walker " + (_walkStarted ? "started via " : "not started: ") + _walkHow);
                        _t0 = Time.timeAsDouble;
                        _phase = Phase.Walk;   // beat 0 may have fired during the load (t 0); otherwise the poll adds it
                    }
                    else if (Time.realtimeSinceStartup - _startReal > CliArgs.Float("autotest-load-timeout", 240f))
                    {
                        Finish(3, "world never became ready");
                    }
                    break;
                case Phase.Walk:
                {
                    _frames++;
                    if (Time.unscaledDeltaTime > _maxDt) { _maxDt = Time.unscaledDeltaTime; _maxFrameAt = _frames; }
                    int b = RingState.Beat;
                    if (b != _lastBeatPolled) { AddBeat(b, "poll"); _lastBeatPolled = b; }
                    if (RingState.GoalReached && double.IsNaN(_goalT)) { _goalT = Now; _goalS = RingState.S; }
                    double scale = RingConstants.Walk / _speed;
                    double limit = (GoalTimeWalk * scale) + EndingAfterGoal + ExitMargin;
                    if (!double.IsNaN(_endT)) Finish(-1, null);
                    else if (Now > limit + 30.0) Finish(2, "timeout: ending not complete by t = " + (limit + 30.0).ToString("0.0"));
                    break;
                }
            }
        }

        void LateUpdate()
        {
            if (_phase != Phase.Walk) return;
            var cam = RingRig.Camera;
            if (cam == null) return;
            float roll = Mathf.Abs(QaUtil.Roll(cam.transform));
            if (roll > _maxRoll) _maxRoll = roll;
            int n = Physics.OverlapSphereNonAlloc(cam.transform.position, ProbeRadius, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c == null || IsPlayer(c)) continue;
                if (_probeHits == 0) _firstHitPos = cam.transform.position;
                _probeHits++;
                break;
            }
        }

        static bool IsPlayer(Collider c)
        {
            if (RingRig.Controller != null && c == (Collider)RingRig.Controller) return true;
            return RingRig.Player != null && c.transform.IsChildOf(RingRig.Player);
        }

        void Finish(int forcedCode, string reason)
        {
            if (_phase == Phase.Done) return;
            _phase = Phase.Done;
            double scale = RingConstants.Walk / _speed;
            double expGoal = GoalTimeWalk * scale, expEnd = expGoal + EndingAfterGoal, exitBy = expEnd + ExitMargin;
            double exitT = Now;
            int rigViol = RingQaHooks.SafetyViolations(out string rigSrc);
            int snaps = RingQaHooks.SafetySnaps(out string snapSrc);
            // cross-checks from the runtime's own statics (reflection; NaN / -1 when absent)
            double walkerGoalT = QaUtil.TryReadStaticNumber(QaUtil.FindType("AutoWalker"), new[] { "TimeAtGoal" }, out double wg, out _) ? wg : double.NaN;
            double rigRoll = QaUtil.TryReadStaticNumber(QaUtil.FindType("CameraRig"), new[] { "MaxRollDeg" }, out double rr, out _) ? rr : double.NaN;
            float rollAll = double.IsNaN(rigRoll) ? _maxRoll : Mathf.Max(_maxRoll, (float)rigRoll);

            var checks = new List<(string name, bool pass, string value, string expected)>();
            checks.Add(("walker_started", _walkStarted || _beatCount > 1, _walkHow, "AutoWalker QA entry point"));
            checks.Add(("goal_time", !double.IsNaN(_goalT) && Math.Abs(_goalT - expGoal) <= TimeTol, Fmt(_goalT), Fmt(expGoal) + " +- 3"));
            checks.Add(("goal_s", !float.IsNaN(_goalS) && _goalS >= RingConstants.GoalS - 0.5f, Fmt(_goalS), ">= 959"));
            checks.Add(("ending_time", !double.IsNaN(_endT) && Math.Abs(_endT - expEnd) <= TimeTol, Fmt(_endT), Fmt(expEnd) + " +- 3"));
            checks.Add(("exit_by", exitT <= exitBy, Fmt(exitT), "<= " + Fmt(exitBy)));
            bool order = true, once = true, sOk = true;
            int last = -1;
            for (int i = 0; i < _beatCount; i++)
            {
                var r = _beats[i];
                if (r.Beat <= last) order = false;
                last = Math.Max(last, r.Beat);
                if (Mathf.Abs(r.S - RingConstants.BeatS[r.Beat]) > BeatSTol) sOk = false;
            }
            for (int b = 0; b < 7; b++) if (_beatSeen[b] != 1) once = false;
            checks.Add(("beats_once", once, BeatsSeen(), "each of 0..6 exactly once"));
            checks.Add(("beats_in_order", order, BeatsSeen(), "ascending"));
            checks.Add(("beats_at_s", sOk, BeatsS(), "0, 24, 276, 411, 594, 774, 894 +- 1"));
            int errors = QaLog.ErrorCount;
            checks.Add(("log_errors", errors == 0, errors.ToString(), "0"));
            checks.Add(("camera_roll", rollAll <= RollTol, rollAll.ToString("0.0000"), "0.00 +- 0.01 deg"));
            checks.Add(("camera_inside_collider", _probeHits == 0 && rigViol <= 0, _probeHits + " probe frames, rig " + rigViol + " (" + rigSrc + ")", "0"));
            checks.Add(("safety_snaps", snaps <= 0, snaps + " (" + snapSrc + ")", "0 (the walker never leaves the lane union)"));

            bool pass = forcedCode < 0;
            foreach (var c in checks) pass &= c.pass;
            int code = forcedCode >= 0 ? forcedCode : (pass ? 0 : 1);

            var j = new QaJson();
            j.Obj().KV("test", "autotest").KV("finished", QaUtil.Stamp()).KV("pass", code == 0).KV("exit_code", code).KV("reason", reason)
             .KV("speed", _speed).KV("seed", CliArgs.Seed).KV("walker", _walkHow)
             .KV("goal_t", _goalT).KV("goal_s", _goalS).KV("ending_t", _endT).KV("exit_t", exitT)
             .KV("expected_goal_t", expGoal).KV("expected_ending_t", expEnd).KV("exit_by", exitBy)
             .KV("frames", _frames).KV("avg_fps", exitT > 0 ? _frames / exitT : 0.0).KV("max_frame_ms", _maxDt * 1000f).KV("max_frame_at", _maxFrameAt)
             .KV("roll_max_deg", _maxRoll).KV("rig_roll_max_deg", rigRoll).KV("probe_hits", _probeHits).KV("probe_first_pos", _firstHitPos)
             .KV("rig_violations", rigViol).KV("rig_violation_source", rigSrc).KV("safety_snaps", snaps)
             .KV("walker_time_at_goal", walkerGoalT).KV("ring_t_at_exit", RingState.T)
             .Key("beats").Arr();
            for (int i = 0; i < _beatCount; i++)
                j.Obj().KV("beat", _beats[i].Beat).KV("s", _beats[i].S).KV("t", _beats[i].T).KV("source", _beats[i].Src).EndObj();
            j.EndArr().Key("checks").Arr();
            foreach (var c in checks) j.Obj().KV("name", c.name).KV("pass", c.pass).KV("value", c.value).KV("expected", c.expected).EndObj();
            j.EndArr().Key("log"); QaLog.WriteJson(j);
            j.EndObj();
            string path = QaUtil.Resolve(CliArgs.Str("autotest-out", "QA/autotest.json"));
            try { QaUtil.WriteText(path, j.ToString()); }
            catch (Exception e) { Debug.LogError("[Ring] AutoTest: cannot write " + path + ": " + e.Message); }
            Debug.Log("[Ring] AutoTest " + (code == 0 ? "PASS" : "FAIL") + " -> " + path);
            QaUtil.Quit(code);
        }

        string BeatsSeen()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _beatCount; i++) { if (i > 0) sb.Append(','); sb.Append(_beats[i].Beat); }
            return sb.ToString();
        }

        string BeatsS()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _beatCount; i++) { if (i > 0) sb.Append(','); sb.Append(_beats[i].S.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)); }
            return sb.ToString();
        }

        static string Fmt(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
