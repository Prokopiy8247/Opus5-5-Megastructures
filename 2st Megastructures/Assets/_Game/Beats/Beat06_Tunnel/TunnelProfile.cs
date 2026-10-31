using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// The door / tunnel profile shared by the wall hole, the door frame and the recess (beat 5) and the tunnel loft
    /// (beat 6), Part 14.5.5-14.5.6: straight walls 1.7 m plus a semicircular crown r 1.3 m (2.6 wide, 3.0 high).
    /// Every consumer builds its vertices from these chains, so rings shared between meshes are bit-identical and the
    /// junctions are watertight without T-junctions.
    ///
    /// Section coordinates (sx, sy): sy up from the floor, sx across. <see cref="MeshBuilder.Loft"/> along +X with
    /// up = +Y maps section x to world -Z exactly (x axis = Cross(up, +X) = (0, 0, -1)), so world z = AxisZ - sx.
    /// <see cref="World"/> reproduces the Loft arithmetic bit for bit. Chains run counter-clockwise in (sx, sy) (the
    /// Loft convention): the floor from -sx to +sx, the arch from the +sx foot up the wall, over the crown and down to
    /// the -sx foot.
    /// </summary>
    public static class TunnelProfile
    {
        /// <summary>Axis z of the door and the tunnel (14.4 waypoints P8-P10 lie on z = 660).</summary>
        public const float AxisZ = 660f;
        public const float HalfWidth = 0.5f * RingConstants.DoorW;      // 1.3
        public const float Straight = RingConstants.DoorStraight;       // 1.7 (crown springing height)
        public const float CrownR = RingConstants.DoorCrownR;           // 1.3
        /// <summary>Wall inner face (door plane), loft start after the 0.8 m recess, mouth (wall outer face).</summary>
        public const float DoorX = RingConstants.WallX;                                // 114
        public const float LoftX0 = RingConstants.WallX + RingConstants.DoorRecess;   // 114.8
        public const float MouthX = RingConstants.WallOuterX;                          // 234

        /// <summary>Crown segments of the full-size profile (5.6 deg, chord sagitta 1.6 mm at r 1.3).</summary>
        public const int CrownSegments = 32;
        /// <summary>Wall breakpoints between the foot and the springing (vertex AO gradients near the floor).</summary>
        public static readonly float[] WallYs = { 0.25f, 0.7f };
        /// <summary>No intermediate wall points (the wall hole: straight posts).</summary>
        public static readonly float[] NoWallYs = new float[0];
        /// <summary>Interior floor breakpoints (|sx| &lt; half width); the ends are the arch feet.</summary>
        public static readonly float[] FloorInnerXs = { -1.05f, -0.6f, 0f, 0.6f, 1.05f };

        /// <summary>
        /// Arch chain (walls + crown) offset outward by <paramref name="offset"/> (negative = inset), feet at
        /// <paramref name="footY"/>. Counter-clockwise: (+hw, footY) up to (+hw, 1.7), crown to (-hw, 1.7), down to (-hw, footY).
        /// </summary>
        public static List<Vector2> Arch(float offset, float footY, float[] wallYs, int crownSegs)
        {
            var c = new List<Vector2>(crownSegs + 2 * wallYs.Length + 3);
            float hw = HalfWidth + offset, r = CrownR + offset;
            c.Add(new Vector2(hw, footY));
            for (int i = 0; i < wallYs.Length; i++)
                if (wallYs[i] > footY + 1e-4f && wallYs[i] < Straight - 1e-4f) c.Add(new Vector2(hw, wallYs[i]));
            for (int k = 0; k <= crownSegs; k++)
            {
                Vector2 p;
                if (k == 0) p = new Vector2(r, Straight);
                else if (k == crownSegs) p = new Vector2(-r, Straight);
                else if (2 * k == crownSegs) p = new Vector2(0f, Straight + r);
                else
                {
                    float a = Mathf.PI * k / crownSegs;
                    p = new Vector2(r * Mathf.Cos(a), Straight + r * Mathf.Sin(a));
                }
                c.Add(p);
            }
            for (int i = wallYs.Length - 1; i >= 0; i--)
                if (wallYs[i] > footY + 1e-4f && wallYs[i] < Straight - 1e-4f) c.Add(new Vector2(-hw, wallYs[i]));
            c.Add(new Vector2(-hw, footY));
            return c;
        }

        /// <summary>Floor chain at y = 0 from (-hw, 0) to (+hw, 0) (counter-clockwise order of the closed profile).</summary>
        public static List<Vector2> Floor(float offset)
        {
            float hw = HalfWidth + offset;
            var c = new List<Vector2>(FloorInnerXs.Length + 2) { new Vector2(-hw, 0f) };
            for (int i = 0; i < FloorInnerXs.Length; i++)
                if (FloorInnerXs[i] > -hw + 1e-3f && FloorInnerXs[i] < hw - 1e-3f) c.Add(new Vector2(FloorInnerXs[i], 0f));
            c.Add(new Vector2(hw, 0f));
            return c;
        }

        /// <summary>Closed counter-clockwise profile (floor + arch, shared corner points once).</summary>
        public static List<Vector2> Closed(float offset, float[] wallYs, int crownSegs)
        {
            var f = Floor(offset);
            var a = Arch(offset, 0f, wallYs, crownSegs);
            var c = new List<Vector2>(f.Count + a.Count);
            for (int i = 0; i < f.Count - 1; i++) c.Add(f[i]);
            for (int i = 0; i < a.Count - 1; i++) c.Add(a[i]);
            return c;
        }

        /// <summary>World position of section point s at axial x (identical to the MeshBuilder.Loft arithmetic).</summary>
        public static Vector3 World(Vector2 s, float x) => new Vector3(x, s.y, AxisZ - s.x);

        /// <summary>World direction of a section-space direction.</summary>
        public static Vector3 WorldDir(Vector2 d) => new Vector3(0f, d.y, -d.x);

        /// <summary>Section coordinates of a world position.</summary>
        public static Vector2 Section(Vector3 w) => new Vector2(AxisZ - w.z, w.y);

        /// <summary>Outward unit normal (section space) of chain point i (central differences; one-sided at open ends).
        /// Same rule as MeshBuilder.Loft.</summary>
        public static Vector2 Outward(IList<Vector2> c, int i, bool closed)
        {
            int n = c.Count;
            Vector2 a = c[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
            Vector2 b = c[closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1)];
            Vector2 t = (b - a).normalized;
            return new Vector2(t.y, -t.x);
        }

        /// <summary>Signed distance (m) to the profile offset by <paramref name="offset"/>; negative inside.
        /// C# twin of RingTunnelProfileSdf in RingTunnel.shader (used by vertex AO and tests).</summary>
        public static float Sdf(Vector2 q, float offset = 0f)
        {
            float hw = HalfWidth + offset;
            float cy = 0.5f * Straight;
            float dx = Mathf.Abs(q.x) - hw, dy = Mathf.Abs(q.y - cy) - cy;
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            float box = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
            float disc = new Vector2(q.x, q.y - Straight).magnitude - (CrownR + offset);
            return Mathf.Min(box, disc);
        }
    }
}
