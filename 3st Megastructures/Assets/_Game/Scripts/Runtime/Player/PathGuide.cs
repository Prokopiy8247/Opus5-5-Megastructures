// PathGuide: the invisible corridor (MASTER_PROMPT 14.4.2 - 14.4.3, Part 9 "corridor guarantee", 11.8).
// The path itself is ThreadMath (PathPoint / PathY / Heading / HalfWidth, the single source of truth of 14.4.2);
// PathGuide turns it into redundant physical safety: BoxColliders on layer 30 (no renderers) under a root named
// "Corridor": per segment (at most 10 m, 2 m where the corridor curves or changes width, each extended to overlap
// its neighbours by 0.5 m) a floor slab under the walkable surface and a wall 2 m thick and 6 m tall on both sides
// at half-width + player radius (+3 cm), end caps behind the spawn and beyond the goal, and kill-volume triggers 5 m
// below the path. A kill returns the player to the last safe point with a short black blink and logs
// "[THREAD-FAIL] kill volume" (it must never fire). CoverageCheck runs the ray-cast grid of 14.4.3 / 11.8.
using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Megastructure.Player
{
    [DefaultExecutionOrder(-20)]
    public class PathGuide : MonoBehaviour
    {
        public static PathGuide Instance { get; private set; }

        // 14.4.3 collider strategy (MASTER_PROMPT line 617; not in ThreadConstants yet, see the B07 contract requests)
        public const float WallThickness = 2.0f;
        public const float WallHeight = 6.0f;
        public const float WallBelowFloor = 1.0f;       // walls reach into the slab: no gap under them
        public const float SlabThickness = 1.0f;
        public const float SegmentOverlap = 0.5f;
        public const double MaxSegment = 10.0;
        // Chord deviation of a straight box from the curved wall line is h'' L^2 / 8 (or L^2 / 8R on the helix):
        public const double CurvedSegment = 2.0;        // helix (7 mm), the 350-400 funnel (h'' 0.021: 1.1 cm), the grade blend
        public const double WideningSegment = 1.0;      // s 80-100, 12 -> 14 m (h'' 0.03: 4 mm)
        public const double NarrowSegment = 0.5;        // D-053 stair walk: s 230-255 (14 -> 4 m) and 322-342 (4 -> 10 m), h'' <= 0.1: 3 mm
        public const float KillDepth = 5.0f;            // kill volume top, below the path
        public const float KillThickness = 20.0f;
        public const float KillLateralMargin = 10.0f;
        // 14.4.3 ray-cast grid (line 617) and the wall-ray rule of task B07 ("outside the half-width + 0.5 m")
        public const double GridStepS = 1.0;
        public const double GridStepLateral = 0.5;
        public const float GridRayHeight = 1.0f;
        public const float WallRayMargin = 0.5f;
        const int ShiftSamples = 16;

        // Spans: [s0, s1, max segment length]. Breakpoints sit on every grade kink and width change of 14.4.3 so a
        // straight box never crosses one.
        static readonly double[] s_spans =
        {
            0, 80, MaxSegment,
            80, 100, WideningSegment,
            100, 230, MaxSegment,
            230, 240, NarrowSegment,
            240, 255, NarrowSegment,
            255, 320, MaxSegment,
            320, 322, MaxSegment,
            322, 342, NarrowSegment,
            342, 350, MaxSegment,
            350, 396, CurvedSegment,
            396, 400, CurvedSegment,
            400, 404, CurvedSegment,
            404, 840, CurvedSegment,
            840, 860, MaxSegment,
        };

        Transform m_root, m_killRoot;
        bool m_built;
        int m_colliders, m_segments;
        readonly Collider[] m_overlap = new Collider[16];
        double m_safeS, m_safeLat;
        bool m_haveSafe;
        int m_killCount;
        bool m_subscribed;
        double m_maxShift;

        public bool Built => m_built;
        public int ColliderCount => m_colliders;
        public int SegmentCount => m_segments;
        /// <summary>Kill-volume events (must stay 0 in every test).</summary>
        public int KillCount => m_killCount;
        public Transform CorridorRoot => m_root;

        void Awake() { Instance = this; }

        void OnEnable()
        {
            if (!m_subscribed) { GameBootstrap.WorldReady += OnWorldReady; m_subscribed = true; }
        }

        void OnDisable()
        {
            if (m_subscribed) { GameBootstrap.WorldReady -= OnWorldReady; m_subscribed = false; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // The corridor depends only on ThreadMath, so it is built at Start (before the first move); WorldReady is a
        // second chance if the component was enabled late.
        void Start() { Build(); }
        void OnWorldReady() { Build(); }

        // ---------------------------------------------------------------- build
        public void Build()
        {
            if (m_built) return;
            var sw = Stopwatch.StartNew();
            var rootGo = new GameObject("Corridor");
            rootGo.layer = PlayerTuning.CorridorLayer;
            m_root = rootGo.transform;
            m_root.SetParent(transform, false);
            var killGo = new GameObject("KillVolume");
            killGo.layer = PlayerTuning.CorridorLayer;
            m_killRoot = killGo.transform;
            m_killRoot.SetParent(m_root, false);

            for (int k = 0; k < s_spans.Length; k += 3)
            {
                double a = s_spans[k], b = s_spans[k + 1], maxLen = s_spans[k + 2];
                int n = Math.Max(1, (int)Math.Ceiling((b - a) / maxLen - 1e-9));
                for (int i = 0; i < n; i++)
                {
                    double s0 = a + (b - a) * i / n, s1 = a + (b - a) * (i + 1) / n;
                    BuildSegment(s0, s1);
                    m_segments++;
                }
            }
            BuildEndCap(0.0, -1.0);
            BuildEndCap(ThreadConstants.PathLength, +1.0);

            Physics.SyncTransforms();
            m_built = true;
            Debug.Log($"[THREAD] corridor: {m_segments} segments, {m_colliders} colliders on layer {PlayerTuning.CorridorLayer}, max wall shift {m_maxShift * 100.0:F1} cm, built in {sw.Elapsed.TotalMilliseconds:F1} ms");
        }

        /// <summary>
        /// Lateral distance of the wall face from the centre line at s: half-width + (player radius + gap) measured
        /// perpendicular to the walkable edge, i.e. scaled by sqrt(1 + h'^2) where the corridor narrows or widens
        /// (at s 235-245 the edges converge at 31 degrees; a purely lateral offset would leave 1.6 cm of clearance).
        /// </summary>
        public static double WallOffset(double s)
        {
            const double e = 0.01;
            double dh = (ThreadMath.HalfWidth(s + e) - ThreadMath.HalfWidth(Math.Max(0.0, s - e))) / (s - e < 0 ? s + e : 2.0 * e);
            double clear = ThreadConstants.PlayerRadius + PlayerTuning.WallGap;
            return ThreadMath.HalfWidth(s) + clear * Math.Sqrt(1.0 + dh * dh);
        }

        void BuildSegment(double s0, double s1)
        {
            double d0 = WallOffset(s0), d1 = WallOffset(s1);

            // floor slab: top face SlabTopBelowFloor under the centre-line chord, laterally flat (the deck is flat radially)
            Vector3 c0 = ToV(ThreadMath.PathPoint(s0)) - Vector3.up * PlayerTuning.SlabTopBelowFloor;
            Vector3 c1 = ToV(ThreadMath.PathPoint(s1)) - Vector3.up * PlayerTuning.SlabTopBelowFloor;
            float halfW = (float)Math.Max(d0, d1) + WallThickness;
            AddBox("slab", m_root, c0, c1, 2f * halfW, -SlabThickness * 0.5f, SlabThickness, 0f, false);

            // walls: level (yaw-only) boxes whose face passes through the corridor points at +-WallOffset, spanning
            // from 1 m below the lower floor end to 6 m above the higher one. A pitched wall would reach backward at
            // head height on the stairs; a level one covers exactly its own plan range. Each face is then pushed
            // outward by the largest clearance deficit over the box's extended range (chord sagitta on the helix,
            // the curvature of the funnels, the 0.25 m overlap extensions), so every point of the walkable edge keeps
            // at least PlayerRadius + WallGap to every wall.
            for (int side = -1; side <= 1; side += 2)
            {
                D3 f0 = ThreadMath.CorridorToWorld(s0, side * d0, 0.0);
                D3 f1 = ThreadMath.CorridorToWorld(s1, side * d1, 0.0);
                double dx = f1.x - f0.x, dz = f1.z - f0.z, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6) continue;
                double fx = dx / len, fz = dz / len;
                double rx = fz, rz = -fx;                       // plan right of the wall direction (Unity LookRotation)
                double nx = -side * rx, nz = -side * rz;        // face normal, pointing into the corridor
                double need = ThreadConstants.PlayerRadius + PlayerTuning.WallGap, shift = 0.0;
                double ext = 0.5 * SegmentOverlap + 0.05;
                for (int k = 0; k <= ShiftSamples; k++)
                {
                    double sk = PlayerMath.ClampS(s0 - ext + (s1 - s0 + 2.0 * ext) * k / ShiftSamples);
                    D3 e = ThreadMath.CorridorToWorld(sk, side * ThreadMath.HalfWidth(sk), 0.0);
                    double dist = (e.x - f0.x) * nx + (e.z - f0.z) * nz;
                    if (need - dist > shift) shift = need - dist;
                }
                double yLo = Math.Min(f0.y, f1.y) - WallBelowFloor, yHi = Math.Max(f0.y, f1.y) + WallHeight;
                double off = side * (0.5 * WallThickness + shift);
                var centre = new Vector3((float)(0.5 * (f0.x + f1.x) + rx * off), (float)(0.5 * (yLo + yHi)), (float)(0.5 * (f0.z + f1.z) + rz * off));
                var go = new GameObject(side < 0 ? "wallL" : "wallR");
                go.layer = PlayerTuning.CorridorLayer;
                go.transform.SetParent(m_root, false);
                go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(new Vector3((float)fx, 0f, (float)fz), Vector3.up));
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(WallThickness, (float)(yHi - yLo), (float)len + SegmentOverlap);
                m_colliders++;
                if (shift > m_maxShift) m_maxShift = shift;
            }

            // kill volume: a trigger slab KillDepth below the path, wider than the corridor
            Vector3 k0 = ToV(ThreadMath.PathPoint(s0)) - Vector3.up * KillDepth;
            Vector3 k1 = ToV(ThreadMath.PathPoint(s1)) - Vector3.up * KillDepth;
            AddBox("kill", m_killRoot, k0, k1, 2f * (halfW + KillLateralMargin), -KillThickness * 0.5f, KillThickness, 0f, true);
        }

        void BuildEndCap(double s, double dir)
        {
            // a wall across the corridor, its inner face PlayerRadius + gap beyond the end of the path
            ThreadMath.Tangent(s, out double tx, out double tz);
            D3 p = ThreadMath.PathPoint(s);
            double off = ThreadConstants.PlayerRadius + PlayerTuning.WallGap + WallThickness * 0.5;
            var centre = new Vector3((float)(p.x + tx * dir * off), (float)p.y + WallHeight * 0.5f - WallBelowFloor, (float)(p.z + tz * dir * off));
            var go = new GameObject(dir < 0 ? "capStart" : "capEnd");
            go.layer = PlayerTuning.CorridorLayer;
            go.transform.SetParent(m_root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(new Vector3((float)tx, 0f, (float)tz), Vector3.up));
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(2f * ((float)WallOffset(s) + WallThickness), WallHeight, WallThickness);
            m_colliders++;
        }

        /// <summary>
        /// A box along a0 -> a1 (3D, so it follows the slope). Local X is horizontal (perpendicular in plan), local Y
        /// tilts with the slope, local Z runs along the segment (extended by the overlap). The box centre is offset by
        /// (lateralOffset along X, upOffset along Y) from the midpoint.
        /// </summary>
        void AddBox(string name, Transform parent, Vector3 a0, Vector3 a1, float width, float upOffset, float height, float lateralOffset, bool trigger)
        {
            Vector3 dir = a1 - a0;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            Vector3 fwd = dir / len;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
            Vector3 centre = 0.5f * (a0 + a1) + right * lateralOffset + up * upOffset;
            var go = new GameObject(name);
            go.layer = PlayerTuning.CorridorLayer;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, rot);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, height, len + SegmentOverlap);
            bc.isTrigger = trigger;
            m_colliders++;
        }

        static Vector3 ToV(D3 d) => new Vector3((float)d.x, (float)d.y, (float)d.z);

        // ---------------------------------------------------------------- kill volume (runtime)
        void LateUpdate()
        {
            if (!m_built) return;
            var p = ThreadServices.Player;
            if (p == null) return;
            bool killed = false;
            Vector3 eye = p.EyePosition;
            double floorY = ThreadMath.PathY(p.S);
            if (float.IsNaN(eye.x) || float.IsNaN(eye.y) || float.IsNaN(eye.z) || eye.y < floorY - KillDepth) killed = true;
            else
            {
                PlayerController.Capsule(p.S, p.Lateral, out Vector3 c0, out Vector3 c1, out float r);
                int n = Physics.OverlapCapsuleNonAlloc(c0, c1, r, m_overlap, PlayerTuning.CorridorMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    Collider c = m_overlap[i];
                    if (c != null && c.isTrigger && c.transform.parent == m_killRoot) { killed = true; break; }
                }
            }

            if (!killed)
            {
                m_safeS = p.S; m_safeLat = p.Lateral; m_haveSafe = true;
                return;
            }
            m_killCount++;
            double s = m_haveSafe ? m_safeS : CliArgs.StartS;
            double lat = m_haveSafe ? m_safeLat : 0.0;
            Debug.LogError($"[THREAD-FAIL] kill volume at s={p.S:F2} lateral={p.Lateral:F2} eye={eye}; returned to s={s:F2} lateral={lat:F2}");
            Megastructure.UI.FadeController.Blink();
            p.Teleport(s, lat);
        }

        // ---------------------------------------------------------------- coverage (11.8 ray-cast grid)
        public bool CoverageCheck(out int misses) => CoverageCheck(out misses, out _);

        /// <summary>
        /// Every 1 m along s and every 0.5 m across the walkable width (plus the exact edges): a ray from 1 m above the
        /// floor straight down must hit a floor slab within 1.5 m; on both sides a horizontal ray from the centre line
        /// at 1 m must hit a wall within half-width + 0.5 m; at both ends a ray along the path must hit the end cap.
        /// </summary>
        public bool CoverageCheck(out int misses, out int rays)
        {
            misses = 0; rays = 0;
            if (!m_built) Build();
            Physics.SyncTransforms();
            int mask = PlayerTuning.CorridorMask;
            int firstMissLogs = 0;
            int steps = (int)Math.Round(ThreadConstants.PathLength / GridStepS);
            for (int k = 0; k <= steps; k++)
            {
                double s = Math.Min(ThreadConstants.PathLength, k * GridStepS);
                double h = ThreadMath.HalfWidth(s);
                int nLat = (int)Math.Floor(2.0 * h / GridStepLateral + 1e-9);
                for (int j = 0; j <= nLat + 1; j++)
                {
                    double lat = j <= nLat ? -h + j * GridStepLateral : h;   // the last sample is exactly the right edge
                    Vector3 o = ToV(ThreadMath.CorridorToWorld(s, lat, GridRayHeight));
                    rays++;
                    if (!Physics.Raycast(o, Vector3.down, GridRayHeight + 0.5f, mask, QueryTriggerInteraction.Ignore))
                    {
                        misses++;
                        if (firstMissLogs++ < 10) Debug.LogError($"[THREAD-FAIL] corridor floor ray miss at s={s:F1} lateral={lat:F2}");
                    }
                }
                ThreadMath.Right(s, out double rx, out double rz);
                Vector3 c = ToV(ThreadMath.CorridorToWorld(s, 0.0, GridRayHeight));
                for (int side = -1; side <= 1; side += 2)
                {
                    var d = new Vector3((float)(rx * side), 0f, (float)(rz * side));
                    rays++;
                    if (!Physics.Raycast(c, d, (float)h + WallRayMargin, mask, QueryTriggerInteraction.Ignore))
                    {
                        misses++;
                        if (firstMissLogs++ < 10) Debug.LogError($"[THREAD-FAIL] corridor wall ray miss at s={s:F1} side={side}");
                    }
                }
            }
            for (int e = 0; e < 2; e++)
            {
                double s = e == 0 ? GridStepS * 0.5 : ThreadConstants.PathLength - GridStepS * 0.5;
                ThreadMath.Tangent(s, out double tx, out double tz);
                float sign = e == 0 ? -1f : 1f;
                Vector3 o = ToV(ThreadMath.CorridorToWorld(s, 0.0, GridRayHeight));
                rays++;
                if (!Physics.Raycast(o, new Vector3((float)tx * sign, 0f, (float)tz * sign), 1.5f, mask, QueryTriggerInteraction.Ignore))
                {
                    misses++;
                    Debug.LogError($"[THREAD-FAIL] corridor end cap ray miss at s={s:F1}");
                }
            }
            Debug.Log($"[THREAD] corridor coverage: {rays} rays, {misses} misses");
            return misses == 0;
        }

        /// <summary>True if the walker capsule at (s, lateral) intersects a solid corridor collider (WallBot probe).</summary>
        public static bool CapsuleBlockedAt(double s, double lateral)
        {
            PlayerController.Capsule(s, lateral, out Vector3 p0, out Vector3 p1, out float r);
            return Physics.CheckCapsule(p0, p1, r, PlayerTuning.CorridorMask, QueryTriggerInteraction.Ignore);
        }
    }
}
