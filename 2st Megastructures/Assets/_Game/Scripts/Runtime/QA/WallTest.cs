using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Corridor tests of Part 11.8 / 14.10, active with -walltest:
    /// (1) ray-cast grid at 0.5 m over every lane proving floor coverage, plus lateral rays at 0.3 m and 1.5 m height
    ///     proving the side walls exist at the lane edge (14.4 colliders (7));
    /// (2) T1: a bot at x = -0.95 sweeps z -1..10 (porthole bay, hatch); the camera is never inside a collider;
    /// (3) wall-hugging bots along both lane edges 0.40 m inside at 1.5 m/s from s 0 to the goal: never leaves the
    ///     lane, never y &lt; -0.5, progress never drops more than 0.3 m, no safety snaps, camera never inside a collider.
    /// "Camera inside a collider" (14.4 Colliders (4), Part 10 #8) is checked twice per phase: the 0.2 m sphere at the
    /// un-pushed eye (feet + 1.7 m) must penetrate nothing by more than 1 mm, and the CameraRig's push-out counter
    /// (CameraRig.Violations through RingQaHooks.SafetyViolations) must not grow during the phase.
    /// The bot takes over the CharacterController (the PlayerController and AutoWalker behaviours are disabled).
    /// Lane widths come from PathGuide.LaneHalfWidth (wired or reflected) or the 14.4 fallback table.
    /// Flags: -walltest-side left|right|both (default both), -walltest-timescale X (default 1), -walltest-out FILE.
    /// Writes QA/walltest.json; exit 0 = pass, 1 = a check failed, 2 = timeout or harness error, 3 = no world.
    /// </summary>
    [DefaultExecutionOrder(30020)]
    public sealed class WallTest : MonoBehaviour
    {
        const float Inset = 0.40f, Speed = RingConstants.Walk, MinY = -0.5f, MaxDrop = 0.3f, LaneTol = 0.05f;
        const float Gravity = 9.81f, Grid = 0.5f, EndS = 958.5f;
        // 14.4 Colliders (4): a 0.2 m sphere at the camera must overlap nothing. The CameraRig pushes its sphere out and
        // counts a violation when the penetration exceeds 1 mm; the probe here tests the UN-pushed eye (feet + 1.7 m)
        // with the same sphere, shrunk by that tolerance (sphere r overlapping by more than t == sphere r - t overlapping).
        const float CamProbeRadius = 0.2f, CamPenetrationTol = 0.001f;

        enum Phase { WaitLoad, T1, Run, Done }
        Phase _phase = Phase.WaitLoad;
        CharacterController _cc;
        Transform _player;
        readonly Collider[] _hits = new Collider[16];
        float _startReal;
        string _laneSource;

        // current bot path
        Vector2[] _pts;
        float[] _cum;
        float _along, _vy, _stall, _runTime;
        Vector3 _lastPos;
        float _lastS, _maxS;
        int _seg;

        // T1
        int _t1Frames, _t1Probe, _t1Viol0 = -1, _t1RigViolations = -1;
        float _t1MaxZ = float.NegativeInfinity;
        Vector3 _t1FirstHit;
        string _violSource = "none";

        struct Run
        {
            public int Side, Frames, LaneViolations, YViolations, DropViolations, Snaps, Probe, Viol0, RigViolations;
            public float MaxLaneExcess, MinYSeen, MaxDropSeen, EndS, Duration;
            public bool Finished, Stuck;
            public Vector3 FirstLaneFail, FirstProbe;
        }
        readonly List<Run> _runs = new List<Run>();
        Run _cur;
        readonly List<int> _sides = new List<int>();
        int _sideIndex;

        // grid
        int _floorRays, _floorMiss, _wallRays, _wallMiss;
        string _gridSource = "";
        readonly List<string> _gridMisses = new List<string>();

        void Awake()
        {
            if (!CliArgs.WallTest) { enabled = false; return; }
            Application.runInBackground = true;
            _startReal = Time.realtimeSinceStartup;
            string side = CliArgs.Str("walltest-side", "both").ToLowerInvariant();
            if (side != "right") _sides.Add(-1);
            if (side != "left") _sides.Add(1);
        }

        void Update()
        {
            QaLog.MainFrame = Time.frameCount;
            switch (_phase)
            {
                case Phase.WaitLoad:
                    if (RingRig.WorldReady && RingRig.Player != null)
                    {
                        if (!Setup()) { Finish(2, "no CharacterController on the player"); return; }
                        RayGrid();
                        StartT1();
                    }
                    else if (Time.realtimeSinceStartup - _startReal > CliArgs.Float("walltest-load-timeout", 240f)) Finish(3, "world never became ready");
                    break;
                case Phase.T1: StepT1(); break;
                case Phase.Run: StepRun(); break;
            }
        }

        bool Setup()
        {
            _player = RingRig.Player;
            _cc = RingRig.Controller != null ? RingRig.Controller : _player.GetComponent<CharacterController>();
            if (_cc == null) return false;
            int n = QaUtil.DisableBehaviours("PlayerController") + QaUtil.DisableBehaviours("AutoWalker");
            Debug.Log("[Ring] WallTest: took over the CharacterController (" + n + " behaviours disabled)");
            Time.timeScale = Mathf.Clamp(CliArgs.Float("walltest-timescale", 1f), 0.1f, 8f);
            _laneSource = RingQaHooks.LaneSource;
            return true;
        }

        // ---------------- ray-cast grid ----------------
        void RayGrid()
        {
            bool was = _cc.enabled;
            _cc.enabled = false;
            var corners = QaPath.Corners;
            // floor coverage over the exact lane shapes (PathGuide.LaneSamplePoints, 0.5 m) when the runtime exposes them
            var samples = RingQaHooks.LaneSamplePoints(Grid, out _gridSource);
            if (samples != null)
            {
                foreach (var q in samples)
                {
                    _floorRays++;
                    var o = new Vector3(q.x, q.y + 1.0f, q.z);
                    if (!Physics.Raycast(o, Vector3.down, out RaycastHit hit, 2.0f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - q.y) > 0.35f)
                    {
                        _floorMiss++;
                        if (_gridMisses.Count < 80) _gridMisses.Add("floor at " + q.ToString("0.00") + (hit.collider != null ? " hit y " + QaUtil.F(hit.point.y) + " (" + hit.collider.name + ")" : " no hit"));
                    }
                }
            }
            else _gridSource = "polyline offsets (QaPath)";
            for (float s = 0.25f; s < RingConstants.PathLength - 0.25f; s += Grid)
            {
                float hw = QaPath.LaneHalfWidth(s);
                float fy = QaPath.FloorY(s);
                Vector2 c = RingWorld.PointAt(s), dir = RingWorld.DirAt(s), right = QaPath.RightOf(dir);
                // floor coverage (fallback grid across the lane at 0.5 m)
                if (samples == null)
                    for (float l = -hw + 0.01f; l <= hw - 0.01f + 1e-4f; l += Grid)
                    {
                        Vector2 p = c + right * l;
                        _floorRays++;
                        var o = new Vector3(p.x, fy + 1.0f, p.y);
                        if (!Physics.Raycast(o, Vector3.down, out RaycastHit hit, 2.0f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - fy) > 0.35f)
                        {
                            _floorMiss++;
                            if (_gridMisses.Count < 80) _gridMisses.Add("floor s " + QaUtil.F(s, "0.00") + " l " + QaUtil.F(l, "0.00") + (hit.collider != null ? " hit y " + QaUtil.F(hit.point.y) : " no hit"));
                        }
                    }
                // side walls at the lane edge (skip near corners, where the lateral ray runs along the next leg)
                bool nearCorner = false;
                foreach (var k in corners) if (Mathf.Abs(s - k.S) < QaPath.LaneHalfWidth(k.S) + 1.5f) nearCorner = true;
                if (nearCorner || s < 1f) continue;
                for (int side = -1; side <= 1; side += 2)
                    foreach (float hgt in new[] { 0.3f, 1.5f })
                    {
                        _wallRays++;
                        var o = new Vector3(c.x, fy + hgt, c.y);
                        var d = new Vector3(right.x * side, 0f, right.y * side);
                        float max = hw + RingConstants.PlayerRadius + 0.6f;
                        if (!Physics.Raycast(o, d, out RaycastHit hit, max, ~0, QueryTriggerInteraction.Ignore))
                        {
                            _wallMiss++;
                            if (_gridMisses.Count < 80) _gridMisses.Add("wall s " + QaUtil.F(s, "0.00") + " side " + side + " h " + QaUtil.F(hgt) + " (max " + QaUtil.F(max) + ")");
                        }
                    }
            }
            _cc.enabled = was;
            Debug.Log("[Ring] WallTest grid: floor " + _floorMiss + "/" + _floorRays + " misses, walls " + _wallMiss + "/" + _wallRays + " misses");
        }

        // ---------------- bot helpers ----------------
        void SetPath(List<Vector2> pts)
        {
            _pts = pts.ToArray();
            _cum = new float[_pts.Length];
            for (int i = 1; i < _pts.Length; i++) _cum[i] = _cum[i - 1] + (_pts[i] - _pts[i - 1]).magnitude;
            _along = 0f; _seg = 0; _vy = 0f; _stall = 0f; _runTime = 0f;
        }

        Vector2 AtArc(float a)
        {
            if (a <= 0f) return _pts[0];
            float total = _cum[_cum.Length - 1];
            if (a >= total) return _pts[_pts.Length - 1];
            while (_seg < _cum.Length - 2 && _cum[_seg + 1] < a) _seg++;
            while (_seg > 0 && _cum[_seg] > a) _seg--;
            float L = _cum[_seg + 1] - _cum[_seg];
            return L > 1e-6f ? Vector2.Lerp(_pts[_seg], _pts[_seg + 1], (a - _cum[_seg]) / L) : _pts[_seg + 1];
        }

        void Teleport(Vector2 p, float floorY)
        {
            _cc.enabled = false;
            _player.position = new Vector3(p.x, floorY + 0.02f, p.y);
            Physics.SyncTransforms();
            _cc.enabled = true;
            _lastPos = _player.position;
            _vy = 0f;
        }

        /// <summary>Moves the controller toward the carrot at 1.5 m/s; returns the horizontal distance to the path point.</summary>
        float Drive(float dt)
        {
            Vector3 pos = _player.position;
            Vector2 here = new Vector2(pos.x, pos.z);
            float lag = (AtArc(_along) - here).magnitude;
            if (lag < 1.0f) { _along = Mathf.Min(_along + Speed * dt, _cum[_cum.Length - 1]); _stall = 0f; }
            else _stall += dt;
            Vector2 carrot = AtArc(_along + 0.75f);
            Vector2 to = carrot - here;
            Vector2 v = to.magnitude > 0.02f ? to.normalized * Speed : Vector2.zero;
            if (_cc.isGrounded) _vy = -1f; else _vy -= Gravity * dt;
            _cc.Move(new Vector3(v.x, _vy, v.y) * dt);
            return lag;
        }

        /// <summary>True when the 0.2 m camera sphere at the UN-pushed eye (feet + 1.7 m, this frame's feet after the
        /// controller move) penetrates any collider by more than 1 mm. The camera transform itself is useless here: the
        /// CameraRig has already pushed it out of every collider in the previous LateUpdate.</summary>
        bool CameraProbe(out Vector3 at)
        {
            at = Vector3.zero;
            Vector3 p = _player.position + Vector3.up * RingConstants.Eye;
            int n = Physics.OverlapSphereNonAlloc(p, CamProbeRadius - CamPenetrationTol, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c == null || c == (Collider)_cc || c.transform.IsChildOf(_player)) continue;
                at = p;
                return true;
            }
            return false;
        }

        /// <summary>CameraRig violations (frames whose 0.2 m sphere had to be pushed out) since <paramref name="v0"/>;
        /// -1 when the runtime exposes no counter.</summary>
        int ViolationsSince(int v0)
        {
            int v = RingQaHooks.SafetyViolations(out _violSource);
            return v < 0 || v0 < 0 ? -1 : v - v0;
        }

        // ---------------- T1 ----------------
        void StartT1()
        {
            var pts = new List<Vector2> { new Vector2(-0.95f, 0f), new Vector2(-0.95f, -1f), new Vector2(-0.95f, 10f) };
            SetPath(pts);
            Teleport(new Vector2(-0.95f, 0f), RingConstants.CapsuleFloorY);
            _t1Viol0 = RingQaHooks.SafetyViolations(out _violSource);
            _phase = Phase.T1;
        }

        void StepT1()
        {
            float dt = Time.deltaTime;
            Drive(dt);
            _runTime += dt;
            _t1Frames++;
            _t1MaxZ = Mathf.Max(_t1MaxZ, _player.position.z);
            if (CameraProbe(out var at)) { if (_t1Probe == 0) _t1FirstHit = at; _t1Probe++; }
            bool end = _along >= _cum[_cum.Length - 1] - 0.01f || _player.position.z >= 10f || _stall > 3f || _runTime > 30f;
            if (end)
            {
                _t1RigViolations = ViolationsSince(_t1Viol0);
                StartRun();
            }
        }

        // ---------------- edge runs ----------------
        void StartRun()
        {
            if (_sideIndex >= _sides.Count) { Finish(-1, null); return; }
            int side = _sides[_sideIndex++];
            SetPath(QaPath.OffsetPath(side, Inset, 0f, EndS, 0.25f));
            Teleport(_pts[0], QaPath.FloorY(0f));
            _cur = new Run { Side = side, MinYSeen = float.PositiveInfinity, Viol0 = RingQaHooks.SafetyViolations(out _violSource), RigViolations = -1 };
            _lastS = 0f; _maxS = 0f;
            _phase = Phase.Run;
            Debug.Log("[Ring] WallTest: run " + (side < 0 ? "left" : "right") + " edge, " + _pts.Length + " path points");
        }

        void StepRun()
        {
            float dt = Time.deltaTime;
            Drive(dt);
            _runTime += dt;
            _cur.Frames++;
            Vector3 pos = _player.position;
            Vector2 xz = new Vector2(pos.x, pos.z);
            float d = QaPath.DistanceToPath(xz, _lastS, 25f, out float s);
            _lastS = s;
            // lane: within the widest half width around s (abrupt narrowings), plus a small tolerance
            float hw = Mathf.Max(QaPath.LaneHalfWidth(Mathf.Max(0f, s - 0.5f)), Mathf.Max(QaPath.LaneHalfWidth(s), QaPath.LaneHalfWidth(Mathf.Min(RingConstants.PathLength, s + 0.5f))));
            float excess = d - (hw + LaneTol);
            if (excess > 0f)
            {
                if (_cur.LaneViolations == 0) _cur.FirstLaneFail = pos;
                _cur.LaneViolations++;
                _cur.MaxLaneExcess = Mathf.Max(_cur.MaxLaneExcess, excess);
            }
            if (pos.y < _cur.MinYSeen) _cur.MinYSeen = pos.y;
            if (pos.y < MinY) _cur.YViolations++;
            if (s > _maxS) _maxS = s;
            float drop = _maxS - s;
            if (drop > _cur.MaxDropSeen) _cur.MaxDropSeen = drop;
            if (drop > MaxDrop) _cur.DropViolations++;
            if ((pos - _lastPos).magnitude > 0.5f) _cur.Snaps++;
            _lastPos = pos;
            if (CameraProbe(out var at)) { if (_cur.Probe == 0) _cur.FirstProbe = at; _cur.Probe++; }

            bool done = _along >= _cum[_cum.Length - 1] - 0.01f && (xz - _pts[_pts.Length - 1]).magnitude < 0.5f;
            bool stuck = _stall > 10f;
            bool timeout = _runTime > EndS / Speed * 1.5f + 60f;
            if (done || stuck || timeout)
            {
                _cur.Finished = done;
                _cur.Stuck = stuck || timeout;
                _cur.EndS = s;
                _cur.Duration = _runTime;
                _cur.RigViolations = ViolationsSince(_cur.Viol0);
                _runs.Add(_cur);
                StartRun();
            }
        }

        // ---------------- result ----------------
        void Finish(int forcedCode, string reason)
        {
            if (_phase == Phase.Done) return;
            _phase = Phase.Done;
            Time.timeScale = 1f;
            bool gridOk = _floorMiss == 0 && _wallMiss == 0 && _floorRays > 0;
            // camera never inside a collider = the un-pushed eye probe AND the CameraRig's own push-out counter (-1 = no
            // counter in the runtime: the probe alone decides, the source is reported)
            bool t1Ok = _t1Probe == 0 && _t1Frames > 0 && _t1RigViolations <= 0;
            bool runsOk = _runs.Count == _sides.Count && _runs.Count > 0;
            foreach (var r in _runs)
                runsOk &= r.Finished && r.LaneViolations == 0 && r.YViolations == 0 && r.DropViolations == 0 && r.Snaps == 0 && r.Probe == 0
                          && r.RigViolations <= 0;
            int code = forcedCode >= 0 ? forcedCode : (gridOk && t1Ok && runsOk ? 0 : 1);

            var j = new QaJson();
            j.Obj().KV("test", "walltest").KV("finished", QaUtil.Stamp()).KV("pass", code == 0).KV("exit_code", code).KV("reason", reason)
             .KV("lane_source", _laneSource).KV("inset", Inset).KV("speed", Speed).KV("timescale", Time.timeScale)
             .KV("safety_snaps", RingQaHooks.SafetySnaps(out string snapSrc)).KV("safety_snaps_source", snapSrc)
             .KV("camera_probe", "un-pushed eye (feet + " + QaUtil.F(RingConstants.Eye) + " m), sphere " + QaUtil.F(CamProbeRadius) + " m, tolerance 1 mm")
             .KV("rig_violation_source", _violSource)
             .Key("grid").Obj().KV("ok", gridOk).KV("source", _gridSource).KV("floor_rays", _floorRays).KV("floor_misses", _floorMiss).KV("wall_rays", _wallRays).KV("wall_misses", _wallMiss)
             .Key("misses").Arr();
            foreach (var m in _gridMisses) j.V(m);
            j.EndArr().EndObj()
             .Key("t1").Obj().KV("ok", t1Ok).KV("frames", _t1Frames).KV("camera_inside_frames", _t1Probe).KV("rig_violations", _t1RigViolations)
             .KV("max_z", _t1MaxZ).KV("first_hit", _t1FirstHit).EndObj()
             .Key("runs").Arr();
            foreach (var r in _runs)
                j.Obj().KV("side", r.Side < 0 ? "left" : "right").KV("finished", r.Finished).KV("stuck", r.Stuck).KV("frames", r.Frames).KV("duration", r.Duration)
                 .KV("end_s", r.EndS).KV("lane_violations", r.LaneViolations).KV("max_lane_excess", r.MaxLaneExcess).KV("first_lane_fail", r.FirstLaneFail)
                 .KV("min_y", r.MinYSeen).KV("y_violations", r.YViolations).KV("max_progress_drop", r.MaxDropSeen).KV("drop_violations", r.DropViolations)
                 .KV("snaps", r.Snaps).KV("camera_inside_frames", r.Probe).KV("rig_violations", r.RigViolations)
                 .KV("first_camera_inside", r.FirstProbe).EndObj();
            j.EndArr().Key("log"); QaLog.WriteJson(j);
            j.EndObj();
            string path = QaUtil.Resolve(CliArgs.Str("walltest-out", "QA/walltest.json"));
            try { QaUtil.WriteText(path, j.ToString()); }
            catch (Exception e) { Debug.LogError("[Ring] WallTest: cannot write " + path + ": " + e.Message); }
            Debug.Log("[Ring] WallTest " + (code == 0 ? "PASS" : "FAIL") + " -> " + path);
            QaUtil.Quit(code);
        }
    }
}
