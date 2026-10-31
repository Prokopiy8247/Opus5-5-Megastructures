using System.Globalization;
using System.Text;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Analytic world functions shared by C# and HLSL (twin: Shaders/Include/RingGround.hlsl, data generated into
    /// Shaders/Include/RingWorldData.hlsl by <see cref="GenerateHlsl"/>): the sharp path polyline and progress s,
    /// PathDist over 14 segments, the river centre line, and RingHeight(x, z, set).
    /// </summary>
    public static class RingWorld
    {
        // ---------------- path polyline ----------------
        static float[] _cum;

        /// <summary>Cumulative s at each waypoint (0, 24, 276, 411, 558, 576, 594, 676, 760, 774, 894, 960).</summary>
        public static float[] Cumulative
        {
            get
            {
                if (_cum == null)
                {
                    var w = RingConstants.Waypoints;
                    _cum = new float[w.Length];
                    for (int i = 1; i < w.Length; i++) _cum[i] = _cum[i - 1] + Vector2.Distance(w[i - 1], w[i]);
                }
                return _cum;
            }
        }

        public static int LegCount => RingConstants.Waypoints.Length - 1;

        /// <summary>Point (x, z) on the sharp polyline at progress s (clamped).</summary>
        public static Vector2 PointAt(float s)
        {
            var w = RingConstants.Waypoints; var c = Cumulative;
            if (s <= 0f) return w[0];
            for (int i = 1; i < w.Length; i++)
                if (s <= c[i]) return Vector2.Lerp(w[i - 1], w[i], (s - c[i - 1]) / Mathf.Max(1e-6f, c[i] - c[i - 1]));
            return w[w.Length - 1];
        }

        /// <summary>Unit direction (x, z) of the leg that contains s.</summary>
        public static Vector2 DirAt(float s)
        {
            int i = LegAt(s);
            var w = RingConstants.Waypoints;
            return (w[i + 1] - w[i]).normalized;
        }

        public static int LegAt(float s)
        {
            var c = Cumulative;
            for (int i = 1; i < c.Length; i++) if (s < c[i]) return i - 1;
            return c.Length - 2;
        }

        /// <summary>Projection of (x, z) onto the polyline: s of the nearest point, searched only over legs whose
        /// s range lies within [hintS - window, hintS + window] (monotone progress; pass window &lt; 0 for global).</summary>
        public static float Project(Vector2 p, float hintS = 0f, float window = -1f)
        {
            var w = RingConstants.Waypoints; var c = Cumulative;
            float best = float.MaxValue, bestS = 0f;
            for (int i = 0; i < w.Length - 1; i++)
            {
                if (window >= 0f && (c[i + 1] < hintS - window || c[i] > hintS + window)) continue;
                Vector2 a = w[i], b = w[i + 1], ab = b - a;
                float L = ab.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, L * L));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best - 1e-6f) { best = d; bestS = c[i] + t * L; }
            }
            return bestS;
        }

        // ---------------- PathDist segments (14) ----------------
        /// <summary>11 path legs + footbridge cross path + belvedere spur + pavilion spur.</summary>
        public static readonly Vector4[] PathSegments = BuildSegments();

        static Vector4[] BuildSegments()
        {
            var w = RingConstants.Waypoints;
            var segs = new Vector4[14];
            for (int i = 0; i < 11; i++) segs[i] = new Vector4(w[i].x, w[i].y, w[i + 1].x, w[i + 1].y);
            segs[11] = new Vector4(-90f, 400f, 90f, 400f);     // cross footpath over the footbridge arch (s 396-404)
            segs[12] = new Vector4(0f, 196f, -28f, 199f);      // spur to the foot of the belvedere hill (the hill stays whole)
            segs[13] = new Vector4(0f, 470f, -110f, 500f);     // spur to pavilion P2 (its footprint is flat)
            return segs;
        }

        public static float SegDist(Vector2 p, Vector4 s)
        {
            Vector2 a = new Vector2(s.x, s.y), b = new Vector2(s.z, s.w), ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }

        public static float PathDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            for (int i = 0; i < PathSegments.Length; i++) d = Mathf.Min(d, SegDist(p, PathSegments[i]));
            return d;
        }

        // ---------------- river ----------------
        /// <summary>River centre polyline extended to +-8 200 m in x along the end tangents (so it reaches the terrain edge).</summary>
        public static readonly Vector2[] River = BuildRiver();

        static Vector2[] BuildRiver()
        {
            var r = RingConstants.RiverCentre;
            var o = new Vector2[r.Length + 2];
            Vector2 d0 = (r[0] - r[1]).normalized, d1 = (r[r.Length - 1] - r[r.Length - 2]).normalized;
            o[0] = r[0] + d0 * ((8200f + r[0].x) / Mathf.Max(1e-3f, -d0.x));
            for (int i = 0; i < r.Length; i++) o[i + 1] = r[i];
            o[o.Length - 1] = r[r.Length - 1] + d1 * ((8200f - r[r.Length - 1].x) / Mathf.Max(1e-3f, d1.x));
            return o;
        }

        public static float RiverDist(float x, float z)
        {
            var p = new Vector2(x, z);
            float d = float.MaxValue;
            for (int i = 0; i < River.Length - 1; i++)
                d = Mathf.Min(d, SegDist(p, new Vector4(River[i].x, River[i].y, River[i + 1].x, River[i + 1].y)));
            return d;
        }

        /// <summary>Carve depth below 0 for river distance d: bed -3.2 within 15 m, bank 1:3 (waterline -2.2 at 18 m), 0 at 24.6 m.</summary>
        public static float RiverCarve(float d) => Mathf.Clamp(RingConstants.RiverCarve - (d - 15f) / 3f, 0f, RingConstants.RiverCarve);

        // ---------------- height ----------------
        /// <summary>RingHeight(x, z) of Part 14.5.2. set: 0 valley (flank on), &gt;= 1 plain (flank off).</summary>
        public static float Height(float x, float z, float set)
        {
            float pd = PathDist(x, z);
            float rd = RiverDist(x, z);
            float n = 6f * (RingMath.ValueNoise(x / 420f, z / 420f) * 2f - 1f)
                    + 1.5f * (RingMath.ValueNoise(x / 90f + 17.3f, z / 90f + 5.1f) * 2f - 1f);
            float zNear = RingMath.Smoothstep(4500f, 1800f, Mathf.Abs(z - 300f));   // the walk's stretch of the valley (x-only terms)
            n *= 1f - 0.85f * RingMath.Smoothstep(40f, 100f, x) * zNear;      // the plain toward the wall is calm
            n *= RingMath.Smoothstep(24.6f, 60f, rd);                          // clean banks
            n *= RingMath.Smoothstep(8f, 60f, pd);                             // no trench: the land settles within 60 m of a path
            float hd = Vector2.Distance(new Vector2(x, z), RingConstants.HillCentre);
            float hill = hd < RingConstants.HillRadius ? RingConstants.HillHeight * (0.5f + 0.5f * Mathf.Cos(Mathf.PI * hd / RingConstants.HillRadius)) : 0f;
            float flank = 22f * RingMath.Smoothstep(60f, 260f, x) * (1f - Mathf.Clamp01(set)) * zNear;
            float levee = LeveeMask(x, z);
            float h = (n + hill + flank) * RingMath.Smoothstep(6f, 16f, pd) * (1f - levee);
            // the river valley (bed -3.2 within 15 m, banks 1:3) cuts through any land term (hill, flank):
            // identical to h - RiverCarve(rd) wherever the land term is 0
            // beyond the 24.6 m bank the valley side steepens (river bluff), so the hill keeps its height
            return Mathf.Min(h, -RingConstants.RiverCarve + Mathf.Max(rd - 15f, 0f) / 3f + Mathf.Max(rd - 24.6f, 0f) * 0.9f);
        }

        /// <summary>1 inside the levee footprint grown by 4 m, falling to 0 at 14 m outside (terrain stays flat under the mound).</summary>
        public static float LeveeMask(float x, float z)
        {
            float dx = Mathf.Max(RingConstants.LeveeX0 - x, x - RingConstants.LeveeX1, 0f);
            float dz = Mathf.Max(RingConstants.LeveeZ0 - z, z - RingConstants.LeveeZ1, 0f);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return 1f - RingMath.Smoothstep(4f, 14f, d);
        }

        // ---------------- HLSL data generation ----------------
        public static string GenerateHlsl()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("// GENERATED by RingWorld.GenerateHlsl() (Tools/Megastructure/Rebuild All). Do not edit by hand.");
            sb.AppendLine("#ifndef RING_WORLD_DATA_INCLUDED");
            sb.AppendLine("#define RING_WORLD_DATA_INCLUDED");
            sb.AppendLine("#define RING_PATH_SEGS " + PathSegments.Length);
            sb.Append("static const float4 RING_PATH[RING_PATH_SEGS] = {");
            for (int i = 0; i < PathSegments.Length; i++)
                sb.AppendFormat(ci, "{0}float4({1:0.###}, {2:0.###}, {3:0.###}, {4:0.###})", i == 0 ? "" : ", ", PathSegments[i].x, PathSegments[i].y, PathSegments[i].z, PathSegments[i].w);
            sb.AppendLine("};");
            sb.AppendLine("#define RING_RIVER_PTS " + River.Length);
            sb.Append("static const float2 RING_RIVER[RING_RIVER_PTS] = {");
            for (int i = 0; i < River.Length; i++)
                sb.AppendFormat(ci, "{0}float2({1:0.###}, {2:0.###})", i == 0 ? "" : ", ", River[i].x, River[i].y);
            sb.AppendLine("};");
            sb.AppendFormat(ci, "#define RING_HILL float4({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})\n", RingConstants.HillCentre.x, RingConstants.HillCentre.y, RingConstants.HillRadius, RingConstants.HillHeight);
            sb.AppendFormat(ci, "#define RING_LEVEE float4({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})\n", RingConstants.LeveeX0, RingConstants.LeveeX1, RingConstants.LeveeZ0, RingConstants.LeveeZ1);
            sb.AppendLine("#endif");
            return sb.ToString();
        }
    }
}
