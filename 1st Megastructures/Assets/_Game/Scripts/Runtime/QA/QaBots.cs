using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// -bot wallhug | raygrid | all (Part 10 item 9, 11.8, 14.10 functional). Virtual time (1/60, VSync off) unless -realtime.
    ///
    /// wallhug: a <see cref="QaWallHugDriver"/> walks along both edges of every walkable region (gallery, landing, both
    /// bridge edges, ledge outer and inner edge, apron, catwalk, dock apron, gantry, tunnel, balcony sides and front edge,
    /// both pier edges) and through every width change (doorway, landing to bridge, ledge to apron, hall portal, dock,
    /// gantry to tunnel, mouth, balcony to pier), pushing outward at 45 degrees at walking and brisk speed. Ahead of a
    /// narrowing of PathGuide.HalfWidth that the run must pass (landing to bridge, apron to catwalk, balcony to pier) the
    /// driver steers toward the narrower edge instead and slides inward along the wide region's transverse wall to the
    /// entrance (<see cref="QaWallHugDriver"/>). A stall (under 5 cm of progress in 0.75 s; progress = d advance plus the
    /// inward slide toward a narrowing entrance) turns the push inward for 1 s (door frames); more than 40 stalls or the
    /// timeout fail the run, with the lateral, the limit and the narrowing in the reason. Per frame it asserts:
    /// no safety return, feet never more than 0.5 m below the path floor at the player's d, capsule centre within the
    /// walkable half-width minus the capsule radius (largest half-width within 1 m of d, +6 cm skin tolerance), and the run
    /// reaches its end in time. Lift deck excluded (kinematic, clamped in PlayerController).
    ///
    /// raygrid: a 0.25 m grid (along d and across the corridor) of downward raycasts on layer Corridor over every walkable
    /// surface (PathGuide.FloorPoint, PathGuide.HalfWidth): every point the capsule centre can reach must hit a collider within
    /// 0.30 m of the true floor height under that point (<see cref="ExpectedFloorY"/>: the centreline height, except from W5
    /// to W8 where the floor is the C1 collar surface at the sample's own angle around C1, the helicoid
    /// y = -3 - 4 alpha / pi across the whole annulus width; points in the outer 0.35 m edge band are reported, not judged); upward raycasts in the
    /// gallery and the tunnel must find a ceiling clamp between 1.85 m and the visible height + 0.6 m; sideways raycasts at
    /// 1 m height report gaps in the edge walls (advisory). Every failing point is listed with its world position, d, offset,
    /// expected floor y and hit y (and the collider hit) so the owners can fix the colliders.
    /// Writes bot_wallhug.json / bot_raygrid.json under -qa-out (QA/) and exits 0 / 1.
    /// </summary>
    public sealed class QaBots
    {
        const int CorridorMask = 1 << CorridorKit.LayerCorridor;
        public const float GridStep = 0.25f;
        public const float FloorTolerance = 0.30f;
        public const float FallLimit = 0.5f;
        public const float LateralTolerance = 0.06f;
        public const float StallWindow = 0.75f, StallDistance = 0.05f, FlipTime = 1.0f;
        public const int MaxStalls = 40;

        sealed class HugRun
        {
            public string Name;
            public float D0, D1;
            public int Side;
            public bool After;
            public bool Segment;
            public Vector3 Start, Dir, Out;
            public float Length;
        }

        sealed class RunResult
        {
            public string Name; public float Speed; public int Side;
            public bool Pass; public string Reason;
            public float Seconds, MaxLateral, Limit, MinDy = float.MaxValue;
            public int Stalls, SafetyReturns;
            public Vector3 FailPos; public float FailD = -1f;
            public float FailLateral = float.NaN, FailNarrowD = -1f, LastLateral = float.NaN;
            public float Progress, InwardTravel;
        }

        readonly QaHarness _h;
        readonly QaWallHugDriver _bot = new QaWallHugDriver();
        int _wallCode = -1, _gridCode = -1;
        readonly List<RunResult> _results = new List<RunResult>(128);

        public QaBots(QaHarness h) { _h = h; }

        public IEnumerator Run()
        {
            string name = (CliArgs.Bot ?? "").Trim().ToLowerInvariant();
            bool grid = name == "raygrid" || name == "all";
            bool wall = name == "wallhug" || name == "all";
            if (!grid && !wall)
            {
                Debug.LogWarning("[ABYSS] QA bot: unknown -bot '" + name + "' (wallhug | raygrid | all)");
                _h.RequestQuit(0f);
                _h.AddFinalizer(a => 1);
                yield break;
            }
            _h.AddFinalizer(aborted =>
            {
                if (wall && _wallCode < 0) { WriteWall(aborted ? "aborted by -quit-after" : "not run"); _wallCode = 1; }
                if (grid && _gridCode < 0) _gridCode = 1;
                return Math.Max(Math.Max(_gridCode, 0), Math.Max(_wallCode, 0)) | (aborted ? 1 : 0);
            });
            yield return _h.WaitForLoad(false);
            yield return QaHarness.WaitSeconds(0.25f);
            if (grid)
            {
                _gridCode = RayGrid();
                yield return null;
            }
            if (wall) yield return WallHug();
            _h.RequestQuit(0.1f);
        }

        // ================================================================ wall-hug

        static void Add(List<HugRun> list, string name, float d0, float d1, bool after)
        {
            list.Add(new HugRun { Name = name + "_right", D0 = d0, D1 = d1, Side = 1, After = after });
            list.Add(new HugRun { Name = name + "_left", D0 = d0, D1 = d1, Side = -1, After = after });
        }

        static List<HugRun> Runs()
        {
            var r = new List<HugRun>(48);
            Add(r, "gallery", 1f, 58.5f, false);
            Add(r, "x_doorway_landing", 56f, 64.5f, false);
            Add(r, "landing", 60.6f, 65.4f, false);
            Add(r, "x_landing_bridge", 61f, 70f, false);
            Add(r, "bridge", 66.5f, 215.5f, false);
            Add(r, "x_bridge_ledge", 212f, 223f, false);
            Add(r, "ledge", 220f, 296.5f, false);
            Add(r, "x_ledge_apron", 293f, 302f, false);
            Add(r, "apron", 298.5f, 329f, false);
            Add(r, "x_apron_catwalk", 325f, 335f, false);
            Add(r, "catwalk", 330.5f, 438.5f, false);
            Add(r, "x_catwalk_dock", 435f, 444f, false);
            Add(r, "dock", 440.5f, 449f, false);
            Add(r, "gantry", 470.5f, 667.5f, true);
            Add(r, "x_gantry_tunnel", 664f, 673f, true);
            Add(r, "tunnel", 669.5f, 727.5f, true);
            Add(r, "x_tunnel_balcony", 724f, 733f, true);
            Add(r, "balcony", 729.5f, 741.5f, true);
            Add(r, "x_balcony_pier", 738f, 747f, true);
            Add(r, "pier", 743.5f, 799.5f, true);

            // balcony front edge (z = -14 + 0.6 inset; capsule centre limit -13.05), from each side corner toward the pier
            float zLimit = AbyssConst.W[17].z + PathGuide.BALCONY_EDGE_INSET + AbyssConst.CAPSULE_R;
            float xc = AbyssConst.W[17].x, y = AbyssConst.W[17].y;
            float xEdge = AbyssConst.BALCONY_W * 0.5f - PathGuide.BALCONY_EDGE_INSET - AbyssConst.CAPSULE_R - 0.3f;
            float xStop = AbyssConst.PIER_W * 0.5f - PathGuide.PIER_EDGE_INSET + AbyssConst.CAPSULE_R + 0.25f;
            r.Add(new HugRun { Name = "balcony_front_east", Segment = true, After = true, Side = 1, Start = new Vector3(xc + xEdge, y, zLimit),
                            Dir = Vector3.left, Out = Vector3.back, Length = xEdge - xStop });
            r.Add(new HugRun { Name = "balcony_front_west", Segment = true, After = true, Side = -1, Start = new Vector3(xc - xEdge, y, zLimit),
                            Dir = Vector3.right, Out = Vector3.back, Length = xEdge - xStop });
            return r;
        }

        static float MaxHalf(float d)
        {
            float h = PathGuide.HalfWidth(d);
            h = Mathf.Max(h, PathGuide.HalfWidth(Mathf.Max(0f, d - 1f)));
            h = Mathf.Max(h, PathGuide.HalfWidth(Mathf.Min(AbyssConst.POLYLINE_LENGTH, d + 1f)));
            return h;
        }

        IEnumerator WallHug()
        {
            var pc = PlayerController.Instance;
            var lift = LiftController.Instance;
            if (pc == null) { _wallCode = 1; WriteWall("no PlayerController"); yield break; }
            string filter = CliArgs.String(QaHarness.BotFilterFlag, null);
            string speeds = (CliArgs.String(QaHarness.BotSpeedsFlag, "both") ?? "both").ToLowerInvariant();
            var speedList = new List<float>(2);
            if (speeds != "brisk") speedList.Add(AbyssConst.V_WALK);
            if (speeds != "walk") speedList.Add(AbyssConst.V_BRISK);
            List<HugRun> runs = Runs();

            for (int si = 0; si < speedList.Count; si++)
            {
                float speed = speedList[si];
                for (int ri = 0; ri < runs.Count; ri++)
                {
                    HugRun run = runs[ri];
                    if (!string.IsNullOrEmpty(filter) && run.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var res = new RunResult { Name = run.Name, Speed = speed, Side = run.Side };
                    _results.Add(res);

                    if (lift != null)
                    {
                        lift.Frozen = true;
                        if (run.After) lift.SetLeft(); else lift.ResetToTop();
                    }
                    Vector3 start; float yaw;
                    if (run.Segment) { start = run.Start; yaw = Mathf.Atan2(run.Dir.x, run.Dir.z) * Mathf.Rad2Deg; }
                    else { start = PathGuide.WalkPoint(run.D0, run.After); yaw = PathGuide.HeadingYaw(run.D0, run.After); }
                    pc.Teleport(start, yaw, 0f);
                    _h.InstallHold(yaw, 0f);
                    yield return null;
                    yield return null;

                    int returns0 = pc.SafetyReturns;
                    _bot.ResetRun();
                    _bot.Speed = speed; _bot.Side = run.Side; _bot.AfterRide = run.After;
                    _bot.Segment = run.Segment; _bot.SegDir = run.Dir; _bot.SegOut = run.Out;
                    _bot.EndD = run.Segment ? AbyssConst.POLYLINE_LENGTH : run.D1;
                    _bot.Running = true;
                    pc.Driver = _bot;

                    float len = run.Segment ? run.Length : run.D1 - run.D0;
                    float timeout = len / (speed * 0.45f) + 15f;
                    float t = 0f, windowT = 0f, windowP = 0f, flipT = 0f;
                    float progress = 0f;
                    bool done = false;
                    while (!done)
                    {
                        yield return null;
                        float dt = Time.deltaTime;
                        t += dt;
                        Vector3 f = pc.FeetPosition;
                        float d = pc.PathD;

                        // progress (along the run); the stall test also counts the inward slide toward a narrowing entrance
                        progress = run.Segment ? Vector3.Dot(f - run.Start, run.Dir) : d - run.D0;
                        float moved = progress + _bot.InwardTravel;
                        if (progress >= len) done = true;

                        // assertions
                        if (pc.SafetyReturns != returns0)
                        {
                            res.SafetyReturns = pc.SafetyReturns - returns0;
                            Fail(res, "safety return (fall) at d " + QaJson.F(d), f, d);
                            break;
                        }
                        float floorY = PathGuide.FloorPoint(d, run.After).y;
                        float dy = f.y - floorY;
                        if (dy < res.MinDy) res.MinDy = dy;
                        if (dy < -FallLimit) { Fail(res, "feet " + QaJson.F(-dy) + " m below the floor at d " + QaJson.F(d), f, d); break; }

                        float lateral, limit;
                        if (run.Segment)
                        {
                            lateral = Vector3.Dot(f - run.Start, run.Out);
                            limit = 0f;
                        }
                        else
                        {
                            Vector3 c = PathGuide.FloorPoint(d, run.After);
                            Vector3 tan = PathGuide.Tangent(d, run.After);
                            var right = new Vector3(tan.z, 0f, -tan.x);
                            lateral = Mathf.Abs(Vector3.Dot(f - c, right));
                            limit = MaxHalf(d) - AbyssConst.CAPSULE_R;
                        }
                        if (lateral > res.MaxLateral) { res.MaxLateral = lateral; res.Limit = limit; }
                        res.LastLateral = lateral;
                        if (lateral > limit + LateralTolerance)
                        {
                            Fail(res, "capsule centre " + QaJson.F(lateral, 3) + " m from the centreline (limit " + QaJson.F(limit, 3) + ") at d " + QaJson.F(d), f, d);
                            break;
                        }

                        // stall recovery
                        if (flipT > 0f)
                        {
                            flipT -= dt;
                            if (flipT <= 0f) { _bot.Flip = false; windowT = 0f; windowP = moved; }
                        }
                        else
                        {
                            windowT += dt;
                            if (windowT >= StallWindow)
                            {
                                if (moved - windowP < StallDistance)
                                {
                                    res.Stalls++;
                                    _bot.Flip = true;
                                    flipT = FlipTime;
                                    if (res.Stalls > MaxStalls) { Fail(res, "stuck (" + res.Stalls + " stalls) at d " + QaJson.F(d) + Context(run, lateral, limit), f, d); break; }
                                }
                                windowT = 0f;
                                windowP = moved;
                            }
                        }
                        if (t > timeout) { Fail(res, "timeout after " + QaJson.F(t, 1) + " s at d " + QaJson.F(d) + Context(run, lateral, limit), f, d); break; }
                    }
                    res.Seconds = t;
                    res.Progress = progress;
                    res.InwardTravel = _bot.InwardTravel;
                    if (res.Reason == null) res.Pass = true;
                    _bot.Running = false;
                    _h.InstallHold(pc.Yaw, 0f);
                }
            }
            bool all = true;
            for (int i = 0; i < _results.Count; i++) all &= _results[i].Pass;
            _wallCode = all && _results.Count > 0 ? 0 : 1;
            WriteWall(null);
        }

        /// <summary>Where the bot was when a run gave up: lateral vs limit, and the narrowing it was steering for (if any).</summary>
        string Context(HugRun run, float lateral, float limit)
        {
            if (run.Segment) return " (offset " + QaJson.F(lateral, 3) + " m toward the edge)";
            string s = " (lateral " + QaJson.F(lateral, 3) + " m, limit " + QaJson.F(limit, 3) + " m";
            if (_bot.NarrowD >= 0f)
                s += "; steering into the narrowing at d " + QaJson.F(_bot.NarrowD) + ", target lateral " + QaJson.F(_bot.TargetLateral, 3)
                   + " m, half-width " + QaJson.F(PathGuide.HalfWidth(_bot.NarrowD + 0.01f), 2) + " m";
            return s + ")";
        }

        void Fail(RunResult r, string reason, Vector3 pos, float d)
        {
            r.Pass = false;
            r.Reason = reason;
            r.FailPos = pos;
            r.FailD = d;
            r.FailLateral = r.LastLateral;
            r.FailNarrowD = _bot.NarrowD;
            Debug.LogWarning("[ABYSS] QA wallhug " + r.Name + " @" + QaJson.F(r.Speed, 1) + " m/s: " + reason);
        }

        void WriteWall(string error)
        {
            var j = new QaJson();
            j.BeginObject();
            _h.WriteHeader(j, "bot_wallhug");
            if (error != null) j.Prop("error", error);
            int pass = 0, fail = 0;
            for (int i = 0; i < _results.Count; i++) { if (_results[i].Pass) pass++; else fail++; }
            j.Prop("runs", _results.Count);
            j.Prop("passed", pass);
            j.Prop("failed", fail);
            j.Prop("lateral_tolerance", LateralTolerance);
            j.Prop("fall_limit", FallLimit);
            j.Arr("results");
            for (int i = 0; i < _results.Count; i++)
            {
                RunResult r = _results[i];
                j.BeginObject();
                j.Prop("name", r.Name);
                j.Prop("speed", r.Speed);
                j.Prop("side", r.Side > 0 ? "right" : "left");
                j.Prop("pass", r.Pass);
                if (r.Reason != null)
                {
                    j.Prop("reason", r.Reason); j.Prop("fail_pos", r.FailPos); j.Prop("fail_d", r.FailD);
                    j.Prop("fail_lateral", r.FailLateral);
                    if (r.FailNarrowD >= 0f) j.Prop("fail_narrowing_d", r.FailNarrowD);
                }
                j.Prop("seconds", r.Seconds);
                j.Prop("progress", r.Progress);
                j.Prop("inward_travel", r.InwardTravel);
                j.Prop("max_lateral", r.MaxLateral);
                j.Prop("limit_at_max", r.Limit);
                j.Prop("min_feet_minus_floor", r.MinDy < float.MaxValue ? r.MinDy : float.NaN);
                j.Prop("stalls", r.Stalls);
                j.Prop("safety_returns", r.SafetyReturns);
                j.EndObject();
            }
            j.EndArray();
            QaLog.Write(j, "log");
            j.Prop("pass", error == null && fail == 0 && _results.Count > 0);
            j.EndObject();
            string path = Path.Combine(QaPaths.OutDir, "bot_wallhug.json");
            j.Save(path);
            Debug.Log("[ABYSS] QA wallhug " + pass + "/" + _results.Count + " runs passed -> " + path);
        }

        // ================================================================ ray grid

        static string RegionName(float d)
        {
            var WD = AbyssConst.WD;
            if (d < WD[3]) return "gallery";
            if (d < WD[4]) return "landing";
            if (d < WD[5]) return "bridge";
            if (d < WD[8]) return "ledge";
            if (d < WD[9]) return "apron";
            if (d < WD[10]) return "catwalk";
            if (d < WD[11]) return "dock";
            if (d < WD[14]) return "deck";
            if (d < WD[15]) return "gantry";
            if (d < WD[16]) return "tunnel";
            if (d < WD[17]) return "balcony";
            return "pier";
        }

        sealed class RegionStat { public int Points, Judged, Fail, Edge, EdgeMiss; public float MaxDy; }

        /// <summary>
        /// Expected floor height under a ray-grid sample p (world xz) at path distance d. Level corridors: the centreline
        /// height (PathGuide.FloorPoint). From the bridge end W5 to W8 (the W5-W6 straight and the ledge arc, d 216-297.54)
        /// the walking surface is the C1 collar, whose height depends only on the angle around C1, so the sample's own angle
        /// decides (<see cref="LedgeSurfaceY"/>), not the centreline's: across the W5-W6 straight the pad side and the
        /// centreline sit at y -3 while the +X side is already on the helicoid.
        /// </summary>
        public static float ExpectedFloorY(float d, Vector3 p, float centreY)
        {
            var WD = AbyssConst.WD;
            if (d < WD[5] || d > WD[8]) return centreY;
            return LedgeSurfaceY(p.x, p.z);
        }

        /// <summary>
        /// The ledge walking surface (14.4, 14.5.3) at world (x, z): alpha = atan2(x - C1.x, C1.z - z) around C1
        /// (AbyssConst.ARC_CENTRE; alpha 0 at W6 toward -Z, pi/2 at W7, pi at W8), flat at W6.y (-3) for alpha &lt;= 0 (the
        /// W5-W6 straight and the pad left of the bridge landing), the helicoid y = W6.y + (W8.y - W6.y) alpha / pi
        /// (-3 - 4 alpha / pi) for 0..pi, flat at W8.y (-7, the apron) past pi.
        /// </summary>
        public static float LedgeSurfaceY(float x, float z)
        {
            Vector2 c1 = AbyssConst.ARC_CENTRE;
            float alpha = Mathf.Atan2(x - c1.x, c1.y - z);
            if (alpha < -0.5f * Mathf.PI) alpha += 2f * Mathf.PI;   // x < 0 beyond C1 (+Z side): just past W8, alpha above pi
            float y0 = AbyssConst.W[6].y, y1 = AbyssConst.W[8].y;
            return y0 + (y1 - y0) * Mathf.Clamp01(alpha / Mathf.PI);
        }

        /// <summary>Most failing points listed per kind in bot_raygrid.json (the counts are always complete).</summary>
        public const int MaxListedPoints = 400;
        const int MaxClusters = 100;

        /// <summary>One listed ray of the grid (QA-only, allocates).</summary>
        sealed class GridPoint
        {
            public string Region, Reason, Collider;
            public float D, Offset;
            public Vector3 Pos;            // probe column at the expected floor height (down, ceiling); expected wall point at 1 m (sideways)
            public float FloorY;           // expected floor y (PathGuide.FloorPoint)
            public bool Hit;
            public float HitY = float.NaN; // y of the hit point (NaN = nothing hit)
            public float HitDistance = float.NaN, ExpectMin = float.NaN, ExpectMax = float.NaN;
        }

        /// <summary>Adjacent judged floor misses (same region, neighbouring grid cells): the box an owner has to cover.</summary>
        sealed class MissCluster
        {
            public string Region;
            public int Count, NoCollider;
            public float D0, D1, O0, O1;
            public Vector3 Min, Max;
        }

        static GridPoint MakePoint(string region, float d, float offset, Vector3 pos, float floorY, bool hit, in RaycastHit h, string reason)
        {
            var p = new GridPoint { Region = region, D = d, Offset = offset, Pos = pos, FloorY = floorY, Hit = hit, Reason = reason };
            if (hit)
            {
                p.HitY = h.point.y;
                p.HitDistance = h.distance;
                p.Collider = h.collider != null ? h.collider.name : null;
            }
            return p;
        }

        static void AddToCluster(List<MissCluster> clusters, GridPoint p)
        {
            const float Near = GridStep * 1.5f + 1e-3f;
            for (int i = clusters.Count - 1; i >= 0; i--)
            {
                MissCluster c = clusters[i];
                if (p.D - c.D1 > Near) break;   // points arrive in increasing d; older clusters are closed
                if (c.Region != p.Region || p.Offset < c.O0 - Near || p.Offset > c.O1 + Near) continue;
                c.Count++;
                if (!p.Hit) c.NoCollider++;
                c.D1 = Mathf.Max(c.D1, p.D);
                c.O0 = Mathf.Min(c.O0, p.Offset); c.O1 = Mathf.Max(c.O1, p.Offset);
                c.Min = Vector3.Min(c.Min, p.Pos); c.Max = Vector3.Max(c.Max, p.Pos);
                return;
            }
            if (clusters.Count >= MaxClusters) return;
            clusters.Add(new MissCluster
            {
                Region = p.Region, Count = 1, NoCollider = p.Hit ? 0 : 1, D0 = p.D, D1 = p.D, O0 = p.Offset, O1 = p.Offset,
                Min = p.Pos, Max = p.Pos
            });
        }

        static string SideName(float offset) => offset > 1e-4f ? "right" : (offset < -1e-4f ? "left" : "centre");

        static void WritePoints(QaJson j, string key, List<GridPoint> list, bool sideways)
        {
            j.Arr(key);
            for (int i = 0; i < list.Count; i++)
            {
                GridPoint p = list[i];
                j.BeginObject();
                j.Prop("d", p.D);
                j.Prop("offset", p.Offset);
                j.Prop("side", SideName(p.Offset));
                j.Prop("region", p.Region);
                j.Prop("x", p.Pos.x);
                j.Prop("y", p.Pos.y);
                j.Prop("z", p.Pos.z);
                j.Prop("floor_y", p.FloorY);
                if (sideways)
                {
                    j.Prop("hit_distance", p.HitDistance);
                    j.Key("expected_distance").Inline(p.ExpectMin, p.ExpectMax);
                }
                else
                {
                    j.Prop("hit_y", p.HitY);
                    j.Prop("dy", p.Hit ? p.HitY - p.FloorY : float.NaN);
                    if (!float.IsNaN(p.ExpectMin)) j.Key("expected_clearance").Inline(p.ExpectMin, p.ExpectMax);
                }
                if (p.Collider != null) j.Prop("collider", p.Collider);
                j.Prop("reason", p.Reason);
                j.EndObject();
            }
            j.EndArray();
        }

        int RayGrid()
        {
            Physics.SyncTransforms();
            var WD = AbyssConst.WD;
            float end = AbyssConst.POLYLINE_LENGTH;
            var regions = new Dictionary<string, RegionStat>();
            var downPts = new List<GridPoint>(64);
            var edgePts = new List<GridPoint>(64);
            var ceilPts = new List<GridPoint>(64);
            var gapPts = new List<GridPoint>(64);
            var clusters = new List<MissCluster>(16);
            int downTotal = 0, downJudged = 0, downFail = 0, edgeTotal = 0, edgeMiss = 0;
            int ceilTotal = 0, ceilFail = 0, sideTotal = 0, sideOk = 0, sideNarrow = 0, sideGap = 0;
            float maxDy = 0f;

            for (float d = 0.05f; d <= end - 0.05f; d += GridStep)
            {
                bool deck = d > WD[11] - 0.1f && d < WD[14] + 0.1f;
                if (deck) continue;
                bool after = d > AbyssConst.D_LIFT;
                Vector3 c = PathGuide.FloorPoint(d, after);
                Vector3 tan = PathGuide.Tangent(d, after);
                var right = new Vector3(tan.z, 0f, -tan.x);
                float half = PathGuide.HalfWidth(d);
                // judged: every point the capsule centre can reach; on the ledge the inner limit is the column collider
                // (COLLIDER_INNER_R) plus the capsule radius, on the side toward C1
                float judgedRight = half - AbyssConst.CAPSULE_R, judgedLeft = judgedRight;
                if (d > WD[6] && d < WD[8])
                {
                    var c1 = new Vector3(AbyssConst.ARC_CENTRE.x, c.y, AbyssConst.ARC_CENTRE.y);
                    float inner = AbyssConst.ARC_RADIUS - (AbyssConst.COLLIDER_INNER_R + AbyssConst.CAPSULE_R);
                    if (Vector3.Dot(c1 - c, right) > 0f) judgedRight = Mathf.Min(judgedRight, inner);
                    else judgedLeft = Mathf.Min(judgedLeft, inner);
                }
                string rn = RegionName(d);
                if (!regions.TryGetValue(rn, out RegionStat st)) { st = new RegionStat(); regions[rn] = st; }

                // ---- downward coverage
                int n = Mathf.FloorToInt((half - 0.02f) / GridStep);
                for (int k = -n - 1; k <= n + 1; k++)
                {
                    float o = k * GridStep;
                    if (k == -n - 1) o = -(half - 0.02f);
                    if (k == n + 1) o = half - 0.02f;
                    Vector3 p = c + right * o;
                    float floorY = ExpectedFloorY(d, p, c.y);   // the true surface under this sample (helicoid on the ledge)
                    p.y = floorY;
                    bool judged = o >= 0f ? o <= judgedRight + 1e-4f : -o <= judgedLeft + 1e-4f;
                    st.Points++;
                    downTotal++;
                    bool hit = Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out RaycastHit h, 3.0f, CorridorMask, QueryTriggerInteraction.Ignore);
                    float dy = hit ? h.point.y - floorY : float.NaN;
                    bool ok = hit && Mathf.Abs(dy) <= FloorTolerance;
                    if (hit && Mathf.Abs(dy) > maxDy && ok) maxDy = Mathf.Abs(dy);
                    if (hit && ok && Mathf.Abs(dy) > st.MaxDy) st.MaxDy = Mathf.Abs(dy);
                    if (ok)
                    {
                        if (judged) { downJudged++; st.Judged++; }
                        else { edgeTotal++; st.Edge++; }
                        continue;
                    }
                    string reason = hit ? "floor " + QaJson.F(dy, 3) + " m off (tolerance " + QaJson.F(FloorTolerance, 2) + ")" : "no collider within 2 m below the floor";
                    if (judged)
                    {
                        downJudged++; st.Judged++;
                        downFail++; st.Fail++;
                        GridPoint gp = MakePoint(rn, d, o, p, floorY, hit, in h, reason);
                        AddToCluster(clusters, gp);
                        if (downPts.Count < MaxListedPoints) downPts.Add(gp);
                    }
                    else
                    {
                        edgeTotal++; st.Edge++;
                        edgeMiss++; st.EdgeMiss++;
                        if (edgePts.Count < MaxListedPoints) edgePts.Add(MakePoint(rn, d, o, p, floorY, hit, in h, reason));
                    }
                }

                // ---- ceiling clamps indoors (gallery, tunnel)
                float ceilH = d < WD[3] ? AbyssConst.GALLERY_H : (d >= WD[15] && d < WD[16] ? AbyssConst.TUNNEL_H : -1f);
                if (ceilH > 0f && d > 0.5f && Mathf.Abs(d - WD[3]) > 0.6f && Mathf.Abs(d - WD[15]) > 0.6f && Mathf.Abs(d - WD[16]) > 0.6f)
                {
                    float lim = half - AbyssConst.CAPSULE_R;
                    bool stairs = d > WD[1] - 0.5f && d < WD[2] + 0.5f;   // the ceiling may follow the upper floor over the stairs
                    float upper = ceilH + 0.6f + (stairs ? AbyssConst.STAIR_DROP : 0f);
                    for (int k = -1; k <= 1; k++)
                    {
                        Vector3 pf = c + right * (k * lim);
                        ceilTotal++;
                        bool hit = Physics.Raycast(pf + Vector3.up * 0.5f, Vector3.up, out RaycastHit h, upper + 1.0f, CorridorMask, QueryTriggerInteraction.Ignore);
                        float hh = hit ? h.point.y - c.y : float.NaN;
                        bool ok = hit && hh >= 1.85f && hh <= upper;
                        if (!ok)
                        {
                            ceilFail++;
                            if (ceilPts.Count < MaxListedPoints)
                            {
                                GridPoint gp = MakePoint(rn, d, k * lim, pf, c.y, hit, in h,
                                    hit ? "ceiling " + QaJson.F(hh, 3) + " m above the floor" : "no ceiling collider within " + QaJson.F(upper + 1.5f, 2) + " m");
                                gp.ExpectMin = 1.85f; gp.ExpectMax = upper;
                                ceilPts.Add(gp);
                            }
                        }
                    }
                }

                // ---- sideways (advisory): edge walls at the walkable half-width, skipping width changes
                bool nearChange = false;
                for (int w = 1; w < WD.Length; w++) if (Mathf.Abs(d - WD[w]) < 0.75f) { nearChange = true; break; }
                if (!nearChange)
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 p = c + Vector3.up * 1.0f;
                        sideTotal++;
                        bool hit = Physics.Raycast(p, right * s, out RaycastHit h, half + 1.0f, CorridorMask, QueryTriggerInteraction.Ignore);
                        if (hit && h.distance >= half - 0.05f && h.distance <= half + 0.15f) sideOk++;
                        else if (hit && h.distance < half - 0.05f) sideNarrow++;
                        else
                        {
                            sideGap++;
                            if (gapPts.Count < MaxListedPoints)
                            {
                                GridPoint gp = MakePoint(rn, d, s * half, p + right * (s * half), c.y, hit, in h,
                                    hit ? "wall at " + QaJson.F(h.distance, 3) + " m" : "no wall within " + QaJson.F(half + 1f, 2) + " m");
                                gp.ExpectMin = half - 0.05f; gp.ExpectMax = half + 0.15f;
                                gapPts.Add(gp);
                            }
                        }
                    }
                }
            }

            bool pass = downFail == 0 && ceilFail == 0 && downJudged > 0;
            var j = new QaJson();
            j.BeginObject();
            _h.WriteHeader(j, "bot_raygrid");
            j.Prop("grid_step", GridStep);
            j.Prop("floor_tolerance", FloorTolerance);
            j.Prop("excluded", "lift deck d " + QaJson.F(WD[11]) + "-" + QaJson.F(WD[14]) + " (kinematic)");
            j.Prop("points_listed_max", MaxListedPoints);
            j.Prop("point_fields", "x, y, z = probe column on the expected floor (sideways: expected wall point at 1 m); floor_y = expected floor y under the sample (PathGuide.FloorPoint height; d 216-297.54: the C1 collar surface at the sample's own angle around C1, y = -3 - 4 alpha/pi, flat -3 for alpha <= 0); hit_y = y of the collider hit (null = none); dy = hit_y - floor_y; offset = metres right of the centreline");
            j.Prop("floor_model", "level across the corridor except d 216-297.54 (W5-W8): the ledge collar surface at each sample's own angle around C1 (ARC_CENTRE)");
            j.Obj("down");
            j.Prop("points", downTotal);
            j.Prop("judged", downJudged);
            j.Prop("failures", downFail);
            j.Prop("edge_band_points", edgeTotal);
            j.Prop("edge_band_misses", edgeMiss);
            j.Prop("max_abs_dy_ok", maxDy);
            j.Prop("failures_listed", downPts.Count);
            WritePoints(j, "failing_points", downPts, false);
            j.Arr("failure_clusters");
            for (int i = 0; i < clusters.Count; i++)
            {
                MissCluster cl = clusters[i];
                j.BeginObject().Prop("region", cl.Region).Prop("count", cl.Count).Prop("no_collider", cl.NoCollider);
                j.Key("d_range").Inline(cl.D0, cl.D1);
                j.Key("offset_range").Inline(cl.O0, cl.O1);
                j.Prop("bounds_min", cl.Min).Prop("bounds_max", cl.Max).EndObject();
            }
            j.EndArray();
            WritePoints(j, "edge_band_miss_points", edgePts, false);
            j.EndObject();
            j.Obj("ceiling");
            j.Prop("points", ceilTotal);
            j.Prop("failures", ceilFail);
            j.Prop("failures_listed", ceilPts.Count);
            WritePoints(j, "failing_points", ceilPts, false);
            j.EndObject();
            j.Obj("sideways_advisory");
            j.Prop("points", sideTotal);
            j.Prop("at_half_width", sideOk);
            j.Prop("narrower", sideNarrow);
            j.Prop("gaps", sideGap);
            WritePoints(j, "gap_points", gapPts, true);
            j.EndObject();
            j.Arr("regions");
            foreach (var kv in regions)
            {
                RegionStat st = kv.Value;
                j.BeginObject().Prop("region", kv.Key).Prop("points", st.Points).Prop("judged", st.Judged).Prop("failures", st.Fail)
                 .Prop("edge_band", st.Edge).Prop("edge_band_misses", st.EdgeMiss).Prop("max_abs_dy", st.MaxDy).EndObject();
            }
            j.EndArray();
            QaLog.Write(j, "log");
            j.Prop("pass", pass);
            j.EndObject();
            string path = Path.Combine(QaPaths.OutDir, "bot_raygrid.json");
            j.Save(path);
            Debug.Log("[ABYSS] QA raygrid " + (pass ? "PASS" : "FAIL") + ": " + downFail + " floor misses of " + downJudged + ", " + ceilFail + " ceiling misses -> " + path);
            for (int i = 0; i < clusters.Count && i < 8; i++)
            {
                MissCluster cl = clusters[i];
                Debug.LogWarning("[ABYSS] QA raygrid floor miss (" + cl.Region + ", " + cl.Count + " points, " + cl.NoCollider + " without collider): d "
                                 + QaJson.F(cl.D0) + "-" + QaJson.F(cl.D1) + ", offset " + QaJson.F(cl.O0) + ".." + QaJson.F(cl.O1)
                                 + ", x " + QaJson.F(cl.Min.x) + ".." + QaJson.F(cl.Max.x) + ", y " + QaJson.F(cl.Min.y) + ".." + QaJson.F(cl.Max.y)
                                 + ", z " + QaJson.F(cl.Min.z) + ".." + QaJson.F(cl.Max.z));
            }
            for (int i = 0; i < ceilPts.Count && i < 4; i++)
            {
                GridPoint p = ceilPts[i];
                Debug.LogWarning("[ABYSS] QA raygrid ceiling miss at d " + QaJson.F(p.D) + " (" + QaJson.F(p.Pos.x) + ", " + QaJson.F(p.Pos.y) + ", " + QaJson.F(p.Pos.z) + "): " + p.Reason);
            }
            return pass ? 0 : 1;
        }
    }
}
