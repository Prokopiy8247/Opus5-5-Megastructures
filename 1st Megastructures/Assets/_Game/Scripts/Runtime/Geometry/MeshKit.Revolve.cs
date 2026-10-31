using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Surfaces of revolution: Lathe, Cylinder, TaperedCylinder, banded column profiles.</summary>
    public static partial class MeshKit
    {
        struct LatheRing
        {
            public int Point;     // profile point index
            public Vector2 N;     // outward normal in (radius, y)
            public bool Planar;   // belongs only to near-horizontal segments: planar (x, z) UVs
        }

        static readonly List<Vector2> s_lp = new List<Vector2>(64);
        static readonly List<bool> s_lpForced = new List<bool>(64);
        static readonly List<float> s_lv = new List<float>(64);
        static readonly List<LatheRing> s_lr = new List<LatheRing>(128);
        static readonly List<float> s_cos = new List<float>(130);
        static readonly List<float> s_sin = new List<float>(130);
        static readonly List<float> s_phi = new List<float>(130);
        static readonly List<Vector2> s_cylProfile = new List<Vector2>(4);
        static readonly List<Vector2> s_capPoly = new List<Vector2>(64);
        static readonly List<int> s_capTris = new List<int>(192);

        /// <summary>
        /// Surface of revolution around the local +Y axis of the builder frame (use <see cref="MeshBuilder.Push(Vector3, Quaternion)"/>
        /// to place it). The profile is a polyline of (radius, y) points; the solid it bounds is closed through the axis, and its
        /// outside is found from the signed area, so normals point away from the solid whichever way the profile runs (bottom to
        /// top along the outside is the canonical order). For a ring-shaped solid (profile not touching the axis) pass a closed
        /// loop by repeating the first point at the end; that junction is always hard.
        ///
        /// Normals: smooth around the axis; split along the profile wherever adjacent profile segments turn by more than
        /// <paramref name="hardEdgeAngleDeg"/> (and always where a cap meets the profile). Poles (radius 0) collapse cleanly:
        /// their degenerate triangles are not emitted.
        ///
        /// UV0 (M_ConcreteCyl, 14.2): x = arc length at the vertex radius = r * phi (phi in radians from +X toward +Z, absolute, so
        /// a full revolution ends at 2 pi r on the seam), y = height y (or the cumulative profile length when
        /// <paramref name="vFromProfileLength"/>). Rings that belong only to near-horizontal segments (caps, band tops) use planar
        /// (x, z) instead.
        /// </summary>
        /// <param name="profile">(radius, y) points; radii below 0 are clamped to 0.</param>
        /// <param name="segments">Segments around (24-128 typical; at least 3).</param>
        /// <param name="arcStartDeg">Start angle from +X toward +Z (degrees).</param>
        /// <param name="arcEndDeg">End angle (degrees). A partial arc is open unless <paramref name="arcEndCaps"/>.</param>
        /// <param name="caps">Close the first/last profile points to the axis with flat discs.</param>
        /// <param name="inside">Flip every normal (surface seen from inside, e.g. a bore).</param>
        /// <param name="arcEndCaps">For partial arcs: close both arc ends with the flat profile polygon.</param>
        public static void Lathe(MeshBuilder mb, IList<Vector2> profile, int segments, float hardEdgeAngleDeg = HardEdgeAngleDeg,
            float arcStartDeg = 0f, float arcEndDeg = 360f, bool caps = false, bool inside = false, bool vFromProfileLength = false,
            bool arcEndCaps = false)
        {
            if (profile == null || profile.Count < 2) return;
            int seg = Mathf.Max(3, segments);

            // 1. clean profile (+ cap points on the axis)
            s_lp.Clear(); s_lpForced.Clear();
            for (int i = 0; i < profile.Count; i++)
            {
                Vector2 p = profile[i];
                if (p.x < 0f) p.x = 0f;
                if (s_lp.Count > 0 && (s_lp[s_lp.Count - 1] - p).sqrMagnitude < 1e-12f) continue;
                s_lp.Add(p); s_lpForced.Add(false);
            }
            if (s_lp.Count < 2) return;
            if (caps)
            {
                if (s_lp[0].x > 1e-6f)
                {
                    s_lpForced[0] = true;
                    s_lp.Insert(0, new Vector2(0f, s_lp[0].y)); s_lpForced.Insert(0, false);
                }
                int last = s_lp.Count - 1;
                if (s_lp[last].x > 1e-6f)
                {
                    s_lpForced[last] = true;
                    s_lp.Add(new Vector2(0f, s_lp[last].y)); s_lpForced.Add(false);
                }
            }
            int np = s_lp.Count;

            // 2. orientation of the solid (closed through the axis) and v coordinates
            double area = 0;
            for (int i = 0; i < np; i++)
            {
                Vector2 p = s_lp[i];
                Vector2 q = i + 1 < np ? s_lp[i + 1] : new Vector2(0f, s_lp[np - 1].y);
                area += (double)p.x * q.y - (double)q.x * p.y;
            }
            {
                Vector2 p = new Vector2(0f, s_lp[np - 1].y), q = new Vector2(0f, s_lp[0].y), r0 = s_lp[0];
                area += (double)p.x * q.y - (double)q.x * p.y;
                area += (double)q.x * r0.y - (double)r0.x * q.y;
            }
            float sign = (area < 0 ? -1f : 1f) * (inside ? -1f : 1f);

            s_lv.Clear();
            float acc = 0f;
            for (int i = 0; i < np; i++)
            {
                if (i > 0) acc += (s_lp[i] - s_lp[i - 1]).magnitude;
                s_lv.Add(vFromProfileLength ? acc : s_lp[i].y);
            }

            // 3. rings (one per smooth point, two per hard point)
            s_lr.Clear();
            for (int j = 0; j < np; j++)
            {
                bool hasPrev = j > 0, hasNext = j < np - 1;
                Vector2 nPrev = hasPrev ? LatheSegNormal(j - 1, sign) : Vector2.zero;
                Vector2 nNext = hasNext ? LatheSegNormal(j, sign) : Vector2.zero;
                bool flatPrev = hasPrev && LatheSegFlat(j - 1), flatNext = hasNext && LatheSegFlat(j);
                if (hasPrev && hasNext)
                {
                    Vector2 dPrev = s_lp[j] - s_lp[j - 1], dNext = s_lp[j + 1] - s_lp[j];
                    bool hard = s_lpForced[j] || Vector2.Angle(dPrev, dNext) > hardEdgeAngleDeg;
                    if (hard)
                    {
                        s_lr.Add(new LatheRing { Point = j, N = nPrev, Planar = flatPrev });
                        s_lr.Add(new LatheRing { Point = j, N = nNext, Planar = flatNext });
                    }
                    else
                    {
                        Vector2 n = nPrev + nNext;
                        n = n.sqrMagnitude > 1e-12f ? n.normalized : nNext;
                        s_lr.Add(new LatheRing { Point = j, N = n, Planar = flatPrev && flatNext });
                    }
                }
                else if (hasNext) s_lr.Add(new LatheRing { Point = j, N = nNext, Planar = flatNext });
                else s_lr.Add(new LatheRing { Point = j, N = nPrev, Planar = flatPrev });
            }

            // 4. angle tables
            float a0 = arcStartDeg * Mathf.Deg2Rad, a1 = arcEndDeg * Mathf.Deg2Rad;
            s_cos.Clear(); s_sin.Clear(); s_phi.Clear();
            for (int k = 0; k <= seg; k++)
            {
                float ph = a0 + (a1 - a0) * k / seg;
                s_phi.Add(ph); s_cos.Add(Mathf.Cos(ph)); s_sin.Add(Mathf.Sin(ph));
            }
            bool fullTurn = Mathf.Abs(a1 - a0) >= 2f * Mathf.PI - 1e-4f;
            if (fullTurn)
            {
                // exact seam: last column at the same position as the first
                s_cos[seg] = s_cos[0]; s_sin[seg] = s_sin[0];
            }

            int rings = s_lr.Count;
            Reserve(mb, rings * (seg + 1) + (arcEndCaps ? 2 * np : 0), (rings - 1) * seg * 6 + (arcEndCaps ? 6 * np : 0));
            int first = mb.VertexCount;
            for (int q = 0; q < rings; q++)
            {
                LatheRing ring = s_lr[q];
                Vector2 p = s_lp[ring.Point];
                float v = s_lv[ring.Point];
                for (int k = 0; k <= seg; k++)
                {
                    float c = s_cos[k], s = s_sin[k];
                    Vector3 pos = new Vector3(p.x * c, p.y, p.x * s);
                    Vector3 n = new Vector3(ring.N.x * c, ring.N.y, ring.N.x * s);
                    Vector2 uv = ring.Planar ? new Vector2(pos.x, pos.z) : new Vector2(p.x * s_phi[k], v);
                    mb.Vertex(pos, n, uv);
                }
            }
            int stride = seg + 1;
            for (int q = 0; q < rings - 1; q++)
            {
                if (s_lr[q].Point == s_lr[q + 1].Point) continue; // split pair at a hard point
                int ra = first + q * stride, rb = first + (q + 1) * stride;
                for (int k = 0; k < seg; k++) Quad(mb, ra + k, rb + k, rb + k + 1, ra + k + 1);
            }

            // 5. flat arc-end caps
            if (arcEndCaps && !fullTurn)
            {
                s_capPoly.Clear();
                for (int i = 0; i < np; i++) s_capPoly.Add(s_lp[i]);
                if (s_capPoly.Count > 1 && (s_capPoly[s_capPoly.Count - 1] - s_capPoly[0]).sqrMagnitude < 1e-12f)
                    s_capPoly.RemoveAt(s_capPoly.Count - 1);
                Triangulate(s_capPoly, s_capTris);
                float dirSign = (a1 >= a0 ? 1f : -1f) * (inside ? -1f : 1f);
                for (int end = 0; end < 2; end++)
                {
                    int k = end == 0 ? 0 : seg;
                    float c = s_cos[k], s = s_sin[k];
                    Vector3 n = new Vector3(-s, 0f, c) * (end == 0 ? -dirSign : dirSign);
                    int b0 = mb.VertexCount;
                    for (int i = 0; i < s_capPoly.Count; i++)
                    {
                        Vector2 p = s_capPoly[i];
                        mb.Vertex(new Vector3(p.x * c, p.y, p.x * s), n, p);
                    }
                    for (int t = 0; t + 2 < s_capTris.Count; t += 3)
                        Tri(mb, b0 + s_capTris[t], b0 + s_capTris[t + 1], b0 + s_capTris[t + 2]);
                }
            }
        }

        static Vector2 LatheSegNormal(int s, float sign)
        {
            Vector2 d = s_lp[s + 1] - s_lp[s];
            Vector2 n = new Vector2(d.y, -d.x);
            float m = n.magnitude;
            return m > 1e-12f ? n * (sign / m) : Vector2.up;
        }

        static bool LatheSegFlat(int s)
        {
            Vector2 d = s_lp[s + 1] - s_lp[s];
            return Mathf.Abs(d.y) < 0.2f * Mathf.Abs(d.x);
        }

        /// <summary>Cylinder between two points (axis a to b), smooth sides, optional flat caps. See <see cref="TaperedCylinder"/>.</summary>
        public static void Cylinder(MeshBuilder mb, Vector3 a, Vector3 b, float radius, int segments = 24, bool caps = true,
            bool thinLine = false)
            => TaperedCylinder(mb, a, b, radius, radius, segments, caps, thinLine);

        /// <summary>
        /// Cone frustum between two points, radius r0 at a and r1 at b, authored at final length (pipes, posts, cables, rods).
        /// UV0 = (arc length r * phi, distance along the axis from a). With <paramref name="thinLine"/> UV1 carries the member axis
        /// and max radius and UV2 the axis midpoint (w = 1) for the ThinLine pass (14.7.9), so the split cap rims widen with the
        /// sides instead of cracking open.
        /// </summary>
        public static void TaperedCylinder(MeshBuilder mb, Vector3 a, Vector3 b, float r0, float r1, int segments = 24,
            bool caps = true, bool thinLine = false)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-6f || (r0 <= 0f && r1 <= 0f)) return;
            Vector3 dir = d / len;
            s_cylProfile.Clear();
            s_cylProfile.Add(new Vector2(Mathf.Max(0f, r0), 0f));
            s_cylProfile.Add(new Vector2(Mathf.Max(0f, r1), len));
            var tl = BeginThinLine(mb, thinLine, dir, Mathf.Max(r0, r1), (a + b) * 0.5f);
            mb.Push(a, Quaternion.FromToRotation(Vector3.up, dir));
            Lathe(mb, s_cylProfile, segments, HardEdgeAngleDeg, 0f, 360f, caps);
            mb.Pop();
            EndThinLine(mb, tl);
        }

        /// <summary>
        /// Profile (radius, y) for a column with protruding bands (14.5.2 b: 2 m bands every 120 m protruding 0.4 m, centres at
        /// y = 1200 - 120 k), bottom to top, for <see cref="Lathe"/>. Band centres sit at bandPhaseY + k * bandSpacing (any integer
        /// k); each band has 45 degree chamfers of <paramref name="bandBevel"/> on its outer edges. Clears and fills
        /// <paramref name="result"/>.
        ///
        /// Chunking (CONTRACTS 6: 200 m column chunks): the profile is the full banded column clipped to [y0, y1], so a band that
        /// straddles y0 or y1 is cut there instead of dropped. Adjacent chunks [y0, Y] and [Y, y1] built with the same band
        /// parameters therefore end and start with the bit-identical point at Y (lathe them without caps and the rings stitch
        /// seamlessly). Rule at a boundary Y: the boundary point is the first point of the full profile that reaches Y; a
        /// horizontal band face lying exactly on Y belongs to the chunk above.
        /// </summary>
        public static void BandedColumnProfile(List<Vector2> result, float radius, float y0, float y1, float bandSpacing,
            float bandHeight, float bandProtrusion, float bandBevel, float bandPhaseY)
        {
            result.Clear();
            bool bands = bandSpacing > 0f && bandHeight > 0f && bandProtrusion > 0f && y1 > y0
                && (y1 - y0) / bandSpacing < 100000f;
            if (!bands)
            {
                result.Add(new Vector2(radius, y0));
                result.Add(new Vector2(radius, y1));
                return;
            }
            float hb = 0.5f * Mathf.Min(bandHeight, 0.9f * bandSpacing); // bands never touch each other: monotone profile
            float bev = Mathf.Clamp(bandBevel, 0f, Mathf.Min(bandProtrusion, hb) * 0.9f);
            float ro = radius + bandProtrusion;
            // every band that can reach [y0, y1], plus one on each side (points outside the range are clipped away)
            int kMin = Mathf.FloorToInt((y0 - hb - bandPhaseY) / bandSpacing) - 1;
            int kMax = Mathf.CeilToInt((y1 + hb - bandPhaseY) / bandSpacing) + 1;

            var clip = new ProfileClip { Y0 = y0, Y1 = y1 };
            // virtual start below everything: the segment to the first band point is vertical at the base radius
            clip.Prev = new Vector2(radius, Mathf.Min(y0, bandPhaseY + kMin * bandSpacing - hb) - 1f);
            for (int k = kMin; k <= kMax && !clip.Done; k++)
            {
                float yc = bandPhaseY + k * bandSpacing;
                float yb = yc - hb, yt = yc + hb;
                clip.Feed(result, new Vector2(radius, yb));
                if (bev > 0f)
                {
                    clip.Feed(result, new Vector2(ro - bev, yb));
                    clip.Feed(result, new Vector2(ro, yb + bev));
                    clip.Feed(result, new Vector2(ro, yt - bev));
                    clip.Feed(result, new Vector2(ro - bev, yt));
                }
                else
                {
                    clip.Feed(result, new Vector2(ro, yb));
                    clip.Feed(result, new Vector2(ro, yt));
                }
                clip.Feed(result, new Vector2(radius, yt));
            }
            if (!clip.Done) clip.Feed(result, new Vector2(radius, Mathf.Max(y1, clip.Prev.y) + 1f));
        }

        /// <summary>
        /// Clips a polyline whose y never decreases to [Y0, Y1]: output starts at the first point of the polyline reaching Y0 and
        /// ends at the first point reaching Y1 (interpolated on the crossing segment, exact when a vertex lies on the boundary).
        /// </summary>
        struct ProfileClip
        {
            public float Y0, Y1;
            public Vector2 Prev;
            public bool Started, Done;

            public void Feed(List<Vector2> result, Vector2 q)
            {
                if (Done) return;
                if (!Started)
                {
                    if (q.y < Y0) { Prev = q; return; }
                    result.Add(AtY(Prev, q, Y0));
                    Started = true;
                }
                if (q.y >= Y1)
                {
                    AddDistinct(result, AtY(Prev, q, Y1));
                    Done = true;
                }
                else AddDistinct(result, q);
                Prev = q;
            }

            /// <summary>Point at height y on the segment p -> q, for p.y &lt; y &lt;= q.y (q itself when q.y == y).</summary>
            static Vector2 AtY(Vector2 p, Vector2 q, float y)
            {
                if (q.y <= y) return q;
                if (q.x == p.x) return new Vector2(q.x, y);
                float t = (y - p.y) / (q.y - p.y);
                return new Vector2(p.x + (q.x - p.x) * t, y);
            }

            static void AddDistinct(List<Vector2> result, Vector2 p)
            {
                int n = result.Count;
                if (n > 0 && result[n - 1].x == p.x && result[n - 1].y == p.y) return; // exact (Vector2 == is approximate)
                result.Add(p);
            }
        }
    }
}
