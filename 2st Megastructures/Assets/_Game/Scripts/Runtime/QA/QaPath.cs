using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Deterministic path geometry for QA cameras and bots: the analytic floor height under the path (14.4, 14.5.1,
    /// 14.5.2), eye poses with rounded corners (quadratic fillets of 1.2 m in the underpass and 4 m elsewhere, 14.4),
    /// headings, the fallback lane table of 14.4 and the inset offset paths of the wall-hugging bot (Part 11.8).
    /// </summary>
    public static class QaPath
    {
        // Fillet radii of the walker (14.4: 1.2 m in the underpass corner, 4 m elsewhere).
        const float FilletUnderpass = 1.2f;
        const float FilletDefault = 4f;

        public struct Corner
        {
            public float S, R;
            public Vector2 P, DIn, DOut;
            /// <summary>+1 for a right turn (heading increases), -1 for a left turn.</summary>
            public int Turn;
        }

        static Corner[] _corners;

        /// <summary>The direction changes of the sharp polyline (s 576, 676, 760).</summary>
        public static Corner[] Corners
        {
            get
            {
                if (_corners != null) return _corners;
                var w = RingConstants.Waypoints; var c = RingWorld.Cumulative;
                var list = new List<Corner>();
                for (int i = 1; i < w.Length - 1; i++)
                {
                    Vector2 dIn = (w[i] - w[i - 1]).normalized, dOut = (w[i + 1] - w[i]).normalized;
                    if (Vector2.Dot(dIn, dOut) > 0.9999f) continue;
                    float cross = dIn.x * dOut.y - dIn.y * dOut.x;
                    list.Add(new Corner
                    {
                        S = c[i], P = w[i], DIn = dIn, DOut = dOut, Turn = cross < 0f ? 1 : -1,
                        R = Mathf.Abs(c[i] - RingConstants.SwapS) < 0.5f ? FilletUnderpass : FilletDefault
                    });
                }
                return _corners = list.ToArray();
            }
        }

        /// <summary>Floor top (m) under the path at progress s: capsule floor 0.9 (z &lt; 10), gangway ramp 0.90 -> 0.03
        /// over z 10-24, landing pad top 0.03 (z 24-30), bridge deck y = 1.5 (1 + cos(pi (z - 150) / 30)) over z 120-180,
        /// otherwise 0 (key-shot eyes are exactly 1.7 above it). s equals z on the first legs (s &lt;= 558).</summary>
        public static float FloorY(float s)
        {
            if (s < RingConstants.RampZ0) return RingConstants.CapsuleFloorY;
            if (s < RingConstants.RampZ1)
                return Mathf.Lerp(RingConstants.RampY0, RingConstants.RampY1, (s - RingConstants.RampZ0) / (RingConstants.RampZ1 - RingConstants.RampZ0));
            if (s < RingConstants.PadZ1) return RingConstants.PadTopY;
            if (s >= RingConstants.BridgeZ0 && s <= RingConstants.BridgeZ1)
            {
                float half = 0.5f * (RingConstants.BridgeZ1 - RingConstants.BridgeZ0);
                return 0.5f * RingConstants.BridgeCrest * (1f + Mathf.Cos(Mathf.PI * (s - RingConstants.BridgeCrestZ) / half));
            }
            return 0f;
        }

        /// <summary>Point (x, z) at progress s with the corners rounded (quadratic Bezier between s_c - r and s_c + r,
        /// control point at the corner), and the unit travel direction there.</summary>
        public static Vector2 PointRounded(float s, out Vector2 dir)
        {
            var cs = Corners;
            for (int i = 0; i < cs.Length; i++)
            {
                var c = cs[i];
                if (s > c.S - c.R && s < c.S + c.R)
                {
                    float t = (s - (c.S - c.R)) / (2f * c.R);
                    Vector2 a = c.P - c.DIn * c.R, b = c.P + c.DOut * c.R;
                    float u = 1f - t;
                    Vector2 p = u * u * a + 2f * u * t * c.P + t * t * b;
                    Vector2 d = 2f * u * (c.P - a) + 2f * t * (b - c.P);
                    dir = d.sqrMagnitude > 1e-12f ? d.normalized : c.DOut;
                    return p;
                }
            }
            dir = RingWorld.DirAt(Mathf.Min(s, RingConstants.PathLength - 1e-3f));
            return RingWorld.PointAt(s);
        }

        /// <summary>Yaw in degrees of a ground direction (0 = +Z, +90 = +X).</summary>
        public static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        /// <summary>Eye position 1.7 m above the floor on the rounded path, and the heading yaw.</summary>
        public static Vector3 EyeAt(float s, out float yaw)
        {
            var p = PointRounded(s, out var d);
            yaw = YawOf(d);
            return new Vector3(p.x, FloorY(s) + RingConstants.Eye, p.y);
        }

        /// <summary>Gaze for QA cameras: the cinematic GazeSchedule when available, else the heading at pitch +4.</summary>
        public static void GazeAt(float s, out float yaw, out float pitch)
        {
            if (RingQaHooks.TryGaze(s, out yaw, out pitch)) return;
            EyeAt(s, out yaw);
            pitch = 4f;
        }

        /// <summary>Lane half widths of the 14.4 table (collider gap minus 0.70 m), used only when PathGuide does not
        /// expose LaneHalfWidth. Narrowings are abrupt here; callers take the minimum over a look-ahead window.</summary>
        public static float FallbackLaneHalfWidth(float s)
        {
            if (s < 9.5f) return 0.95f;            // capsule clear interior 2.6 -> lane 1.9
            if (s < 10f) return 0.65f;             // hatch opening 2.0 -> 1.3
            if (s < 24f) return 0.85f;             // gangway 2.4 -> 1.7
            if (s < 120f) return 4.65f;            // meadow, colliders at x = +-5.0 -> 9.3
            if (s < 180f) return 2.15f;            // bridge rails 5.0 apart -> 4.3
            if (s < 396f) return 4.65f;
            if (s < 404f) return 1.75f;            // footbridge arch 4.2 -> 3.5
            if (s < 538f) return 4.65f;
            if (s < 558f) return Mathf.Lerp(4.65f, 1.05f, (s - 538f) / 20f); // last 20 m funnel to the 2.8 m mouth -> 2.1
            if (s < 594f) return 1.05f;            // underpass 2.1
            if (s < 760f + (RingConstants.LaneCapX - 100f)) return 7f; // plain: polyline buffered 7 m, cap x <= 111.5
            if (s < 894f) return 0.95f;            // door funnel and tunnel 2.6 -> 1.9
            return 0.5f * RingConstants.PlatformLane; // platform 29.0 between rails -> 28.3
        }

        /// <summary>Lane half width from PathGuide when available, else the fallback table.</summary>
        public static float LaneHalfWidth(float s) => RingQaHooks.TryLaneHalfWidth(s, out float hw) ? hw : FallbackLaneHalfWidth(s);

        /// <summary>Minimum lane half width over [s, s + ahead] (0.25 m steps), so a bot is inside before a narrowing.</summary>
        public static float MinHalfWidthAhead(float s, float ahead)
        {
            float m = LaneHalfWidth(s);
            for (float x = s + 0.25f; x <= s + ahead + 1e-4f; x += 0.25f) m = Mathf.Min(m, LaneHalfWidth(Mathf.Min(x, RingConstants.PathLength)));
            return m;
        }

        /// <summary>Unit right-hand normal of a travel direction (heading +Z -> +X, heading +X -> -Z).</summary>
        public static Vector2 RightOf(Vector2 dir) => new Vector2(dir.y, -dir.x);

        /// <summary>
        /// Dense path of a wall-hugging bot: the polyline offset to the given side (+1 right, -1 left) by
        /// (lane half width - inset), evaluated with a 3 m look-ahead minimum. Inner corners use the mitre point,
        /// outer corners an arc around the corner (inside both square corner blocks and round buffers).
        /// </summary>
        public static List<Vector2> OffsetPath(int side, float inset, float s0, float s1, float step)
        {
            var pts = new List<Vector2>(4096);
            var w = RingConstants.Waypoints;
            var cs = Corners;
            float prev = s0 - step;
            for (float s = s0; s <= s1 + 1e-4f; s += step)
            {
                // outer-corner arcs inserted when crossing a corner
                for (int i = 0; i < cs.Length; i++)
                {
                    var c = cs[i];
                    if (prev < c.S && s >= c.S && side != c.Turn)
                    {
                        float off = Mathf.Max(0f, MinHalfWidthAhead(c.S, 3f) - inset);
                        Vector2 r0 = RightOf(c.DIn) * side, r1 = RightOf(c.DOut) * side;
                        float a0 = Mathf.Atan2(r0.y, r0.x), a1 = Mathf.Atan2(r1.y, r1.x);
                        float da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                        for (int k = 1; k < 8; k++)
                        {
                            float a = a0 + da * k / 8f;
                            pts.Add(c.P + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * off);
                        }
                    }
                }
                prev = s;

                Vector2 p = Vector2.zero;
                bool done = false;
                for (int i = 0; i < cs.Length; i++)
                {
                    var c = cs[i];
                    if (side != c.Turn) continue;   // inner side only
                    float off = Mathf.Max(0f, MinHalfWidthAhead(c.S, 3f) - inset);
                    if (s >= c.S - off && s <= c.S + off)
                    {
                        Vector2 rIn = RightOf(c.DIn), rOut = RightOf(c.DOut);
                        float den = 1f + Vector2.Dot(rIn, rOut);
                        p = c.P + (rIn + rOut) * (side * off / Mathf.Max(1e-3f, den));
                        done = true;
                        break;
                    }
                }
                if (!done)
                {
                    float sc = Mathf.Min(s, RingConstants.PathLength - 1e-3f);
                    int leg = RingWorld.LegAt(sc);
                    Vector2 dir = (w[leg + 1] - w[leg]).normalized;
                    float off = Mathf.Max(0f, MinHalfWidthAhead(s, 3f) - inset);
                    p = RingWorld.PointAt(s) + RightOf(dir) * (side * off);
                }
                pts.Add(p);
            }
            return pts;
        }

        /// <summary>Distance from (x, z) to the sharp polyline and the s of the nearest point (searched near hintS).</summary>
        public static float DistanceToPath(Vector2 p, float hintS, float window, out float s)
        {
            s = RingWorld.Project(p, hintS, window);
            var q = RingWorld.PointAt(s);
            float d = (q - p).magnitude;
            // the projection is restricted to a window; also check the corner vertices nearby
            var cs = Corners;
            for (int i = 0; i < cs.Length; i++)
                if (Mathf.Abs(cs[i].S - hintS) <= window) d = Mathf.Min(d, (cs[i].P - p).magnitude);
            return d;
        }
    }
}
