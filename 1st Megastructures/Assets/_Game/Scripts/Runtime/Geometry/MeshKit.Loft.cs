using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>Sweeps: Loft, LoftVarying, Extrude, IBeam, Helix; cross-section builders; path frames; polygon triangulation.</summary>
    public static partial class MeshKit
    {
        struct LoftCol
        {
            public int Point;   // section point index
            public byte Mode;   // 0 smooth (averaged or explicit normal), 1 end of the previous edge, 2 start of the next edge
            public bool UEnd;   // closing column of a closed section (u = full perimeter)
        }

        static readonly List<LoftCol> s_cols = new List<LoftCol>(64);
        static readonly List<Vector3> s_lpos = new List<Vector3>(2048);
        static readonly List<Vector2> s_ltan = new List<Vector2>(2048);
        static readonly List<float> s_lu = new List<float>(2048);
        static readonly List<float> s_lfv = new List<float>(256);
        static readonly List<float> s_cum = new List<float>(64);
        static readonly List<Vector2> s_capA = new List<Vector2>(64);
        static readonly List<Vector2> s_capB = new List<Vector2>(64);
        static readonly List<int> s_ear = new List<int>(64);
        static readonly List<LoftFrame> s_twoFrames = new List<LoftFrame>(2);
        static readonly List<LoftFrame> s_helixFrames = new List<LoftFrame>(256);
        static readonly CrossSection s_ibeamSection = new CrossSection();
        static readonly CrossSection s_helixSection = new CrossSection();

        // ------------------------------------------------------------------------------------------ Loft

        /// <summary>
        /// Sweep one cross-section through a list of frames (gallery shell, tunnel, rods, rails, beams).
        ///
        /// Normals are computed from the swept surface itself (cross product of the section tangent and the path tangent), so
        /// tapering, shearing (vertical sections on a sloped path) and bends shade correctly. Closed sections get outward normals
        /// (from the section's signed area); open sections face to the right of their point order. <paramref name="inside"/> flips
        /// everything (corridors and tunnels seen from within). Hard section points split normals along the path; hard frames
        /// split them across the path.
        ///
        /// UV0 = (perimeter distance around the scaled section, cumulative distance between frame origins), metres.
        /// Caps (closed sections only) are flat polygons triangulated by ear clipping (concave sections such as an I-beam work),
        /// facing away from the solid, UV0 = scaled section coordinates.
        /// </summary>
        public static void Loft(MeshBuilder mb, CrossSection section, IList<LoftFrame> frames, bool capEnds = false, bool inside = false)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            LoftCore(mb, section, null, frames, capEnds, inside);
        }

        /// <summary>
        /// Loft with a different cross-section per frame (fan blades with chord and pitch per radius, tapered connecting rods).
        /// <paramref name="sectionAt"/> is called exactly once per frame, in frame order, so it may refill one shared CrossSection.
        /// Every section must have the same point count and Closed flag; hard flags are taken from frame 0's section.
        /// </summary>
        public static void LoftVarying(MeshBuilder mb, Func<int, CrossSection> sectionAt, IList<LoftFrame> frames, bool capEnds = false,
            bool inside = false)
        {
            if (sectionAt == null) throw new ArgumentNullException(nameof(sectionAt));
            LoftCore(mb, null, sectionAt, frames, capEnds, inside);
        }

        static void LoftCore(MeshBuilder mb, CrossSection fixedSection, Func<int, CrossSection> varying, IList<LoftFrame> frames,
            bool capEnds, bool inside)
        {
            int F = frames == null ? 0 : frames.Count;
            if (F < 2) throw new ArgumentException("Loft needs at least two frames");
            CrossSection s0 = varying != null ? varying(0) : fixedSection;
            if (s0 == null) throw new ArgumentException("Loft section 0 is null");
            int N = s0.Count;
            bool closed = s0.Closed;
            if (N < 2 || (closed && N < 3)) throw new ArgumentException("Loft section needs at least 2 points (3 when closed)");

            float orient = closed && s0.SignedArea() < 0f ? -1f : 1f;
            float side = inside ? -1f : 1f;
            BuildColumns(s0);
            int C = s_cols.Count;

            // pass 1: positions, section tangents and u per frame (each section is read exactly once)
            s_lpos.Clear(); s_ltan.Clear(); s_lu.Clear(); s_lfv.Clear(); s_capA.Clear(); s_capB.Clear();
            float v = 0f;
            for (int k = 0; k < F; k++)
            {
                CrossSection s = k == 0 ? s0 : (varying != null ? varying(k) : fixedSection);
                if (s == null || s.Count != N || s.Closed != closed)
                    throw new ArgumentException("Loft section " + k + " differs in topology from section 0");
                LoftFrame fr = frames[k];
                Vector2 sc = fr.EffectiveScale;
                if (k > 0) v += (fr.Position - frames[k - 1].Position).magnitude;
                s_lfv.Add(v);
                for (int i = 0; i < N; i++) s_lpos.Add(fr.Transform(s.Points[i]));

                s_cum.Clear();
                float acc = 0f;
                s_cum.Add(0f);
                for (int i = 1; i < N; i++)
                {
                    Vector2 d = s.Points[i] - s.Points[i - 1];
                    acc += new Vector2(d.x * sc.x, d.y * sc.y).magnitude;
                    s_cum.Add(acc);
                }
                float total = acc;
                if (closed)
                {
                    Vector2 d = s.Points[0] - s.Points[N - 1];
                    total += new Vector2(d.x * sc.x, d.y * sc.y).magnitude;
                }
                for (int c = 0; c < C; c++)
                {
                    LoftCol col = s_cols[c];
                    s_ltan.Add(ColumnTangent(s, col, orient));
                    s_lu.Add(col.UEnd ? total : s_cum[col.Point]);
                }
                if (capEnds && closed && (k == 0 || k == F - 1))
                {
                    List<Vector2> dst = k == 0 ? s_capA : s_capB;
                    dst.Clear();
                    for (int i = 0; i < N; i++) dst.Add(new Vector2(s.Points[i].x * sc.x, s.Points[i].y * sc.y));
                }
            }

            // pass 2: vertex rings and strips
            Reserve(mb, C * F * 2 + (capEnds ? 2 * N : 0), C * (F - 1) * 6 + (capEnds ? 6 * N : 0));
            int prevOut = -1;
            for (int k = 0; k < F; k++)
            {
                bool hard = frames[k].Hard && k > 0 && k < F - 1;
                int ringIn = EmitLoftRing(mb, frames, k, N, C, hard ? -1 : 0, orient * side);
                int ringOut = hard ? EmitLoftRing(mb, frames, k, N, C, 1, orient * side) : ringIn;
                if (k > 0)
                {
                    for (int c = 0; c < C - 1; c++)
                    {
                        if (s_cols[c].Point == s_cols[c + 1].Point) continue;
                        Quad(mb, prevOut + c, prevOut + c + 1, ringIn + c + 1, ringIn + c);
                    }
                }
                prevOut = ringOut;
            }

            // caps
            if (capEnds && closed)
            {
                for (int end = 0; end < 2; end++)
                {
                    int k = end == 0 ? 0 : F - 1;
                    List<Vector2> poly = end == 0 ? s_capA : s_capB;
                    Triangulate(poly, s_capTris);
                    Vector3 fwd = frames[k].Forward;
                    Vector3 along = frames[Mathf.Min(k + 1, F - 1)].Position - frames[Mathf.Max(k - 1, 0)].Position;
                    float sgn = Vector3.Dot(fwd, along) >= 0f ? 1f : -1f;
                    Vector3 n = fwd * (sgn * (end == 0 ? -1f : 1f) * side);
                    int b0 = mb.VertexCount;
                    for (int i = 0; i < N; i++) mb.Vertex(s_lpos[k * N + i], n, poly[i]);
                    for (int t = 0; t + 2 < s_capTris.Count; t += 3)
                        Tri(mb, b0 + s_capTris[t], b0 + s_capTris[t + 1], b0 + s_capTris[t + 2]);
                }
            }
        }

        static int EmitLoftRing(MeshBuilder mb, IList<LoftFrame> frames, int k, int N, int C, int mode, float sign)
        {
            int F = frames.Count;
            LoftFrame fr = frames[k];
            Vector2 sc = fr.EffectiveScale;
            Quaternion rot = fr.Rotation;
            float v = s_lfv[k];
            int first = mb.VertexCount;
            for (int c = 0; c < C; c++)
            {
                int i = s_cols[c].Point;
                Vector3 pos = s_lpos[k * N + i];
                Vector2 t2 = s_ltan[k * C + c];
                Vector3 secTan = rot * new Vector3(t2.x * sc.x, t2.y * sc.y, 0f);
                Vector3 prev = k > 0 ? s_lpos[(k - 1) * N + i] : pos;
                Vector3 next = k < F - 1 ? s_lpos[(k + 1) * N + i] : pos;
                Vector3 pathTan = mode < 0 ? pos - prev : mode > 0 ? next - pos : next - prev;
                Vector3 refN = rot * new Vector3(t2.y / sc.x, -t2.x / sc.y, 0f);
                Vector3 n = Vector3.Cross(secTan, pathTan);
                if (n.sqrMagnitude <= 1e-12f * secTan.sqrMagnitude * pathTan.sqrMagnitude || n.sqrMagnitude < 1e-30f) n = refN;
                else if (Vector3.Dot(n, refN) < 0f) n = -n;
                n = SafeNormalize(n, Vector3.up) * sign;
                mb.Vertex(pos, n, new Vector2(s_lu[k * C + c], v));
            }
            return first;
        }

        static void BuildColumns(CrossSection s)
        {
            s_cols.Clear();
            int n = s.Count;
            if (s.Closed)
            {
                s_cols.Add(new LoftCol { Point = 0, Mode = (byte)(s.Hard[0] ? 2 : 0) });
                for (int i = 1; i < n; i++)
                {
                    if (s.Hard[i])
                    {
                        s_cols.Add(new LoftCol { Point = i, Mode = 1 });
                        s_cols.Add(new LoftCol { Point = i, Mode = 2 });
                    }
                    else s_cols.Add(new LoftCol { Point = i, Mode = 0 });
                }
                s_cols.Add(new LoftCol { Point = 0, Mode = (byte)(s.Hard[0] ? 1 : 0), UEnd = true });
            }
            else
            {
                s_cols.Add(new LoftCol { Point = 0, Mode = 2 });
                for (int i = 1; i < n - 1; i++)
                {
                    if (s.Hard[i])
                    {
                        s_cols.Add(new LoftCol { Point = i, Mode = 1 });
                        s_cols.Add(new LoftCol { Point = i, Mode = 2 });
                    }
                    else s_cols.Add(new LoftCol { Point = i, Mode = 0 });
                }
                s_cols.Add(new LoftCol { Point = n - 1, Mode = 1 });
            }
        }

        static Vector2 ColumnTangent(CrossSection s, LoftCol col, float orient)
        {
            int n = s.Count, i = col.Point;
            Vector2 p = s.Points[i];
            bool hasPrev = s.Closed || i > 0, hasNext = s.Closed || i < n - 1;
            Vector2 tp = hasPrev ? Norm2(p - s.Points[(i - 1 + n) % n]) : Vector2.zero;
            Vector2 tn = hasNext ? Norm2(s.Points[(i + 1) % n] - p) : Vector2.zero;
            if (col.Mode == 1) return hasPrev ? tp : tn;
            if (col.Mode == 2) return hasNext ? tn : tp;
            Vector2 en = s.Normals[i];
            if (en.sqrMagnitude > 1e-12f)
            {
                en.Normalize();
                return orient > 0f ? new Vector2(-en.y, en.x) : new Vector2(en.y, -en.x);
            }
            Vector2 t = tp + tn;
            if (t.sqrMagnitude < 1e-12f) return hasNext ? tn : tp;
            return t.normalized;
        }

        static Vector2 Norm2(Vector2 v)
        {
            float m = v.magnitude;
            return m > 1e-12f ? v / m : Vector2.zero;
        }

        // ------------------------------------------------------------------------------------------ frames

        /// <summary>
        /// Frames along a polyline for <see cref="Loft"/>. Interior frames sit on the bisector of the two adjacent segments and are
        /// widened by the mitre factor so the section keeps its width through bends; frames whose path turns by more than
        /// <paramref name="hardAngleDeg"/> are marked hard.
        /// keepVertical: the section plane stays vertical (frame up = world up, forward = horizontal path direction), so a sloped
        /// segment shears the section instead of tilting it: the gallery stays exactly 2.5 m tall measured vertically over the
        /// stairs (14.5.1). Otherwise frames use LookRotation(direction, up).
        /// </summary>
        public static void PathFrames(IList<Vector3> points, Vector3 up, List<LoftFrame> result, bool keepVertical = false,
            float hardAngleDeg = 5f)
        {
            result.Clear();
            int n = points == null ? 0 : points.Count;
            if (n < 2) return;
            Vector3 upN = SafeNormalize(up, Vector3.up);
            for (int i = 0; i < n; i++)
            {
                Vector3 dIn = i > 0 ? points[i] - points[i - 1] : Vector3.zero;
                Vector3 dOut = i < n - 1 ? points[i + 1] - points[i] : Vector3.zero;
                bool hard = i > 0 && i < n - 1 && Vector3.Angle(dIn, dOut) > hardAngleDeg;
                Vector2 scale = Vector2.one;
                Quaternion rot;
                if (keepVertical)
                {
                    Vector3 hIn = SafeNormalize(dIn - upN * Vector3.Dot(dIn, upN), Vector3.zero);
                    Vector3 hOut = SafeNormalize(dOut - upN * Vector3.Dot(dOut, upN), Vector3.zero);
                    Vector3 fwd = SafeNormalize(hIn + hOut, hOut.sqrMagnitude > 0f ? hOut : hIn);
                    if (fwd.sqrMagnitude < 0.5f) fwd = Mathf.Abs(upN.z) < 0.9f ? Vector3.forward : Vector3.right;
                    if (hIn.sqrMagnitude > 0f && hOut.sqrMagnitude > 0f)
                        scale.x = 1f / Mathf.Max(Vector3.Dot(fwd, hOut), 0.2f);
                    rot = Quaternion.LookRotation(fwd, upN);
                }
                else
                {
                    Vector3 nIn = SafeNormalize(dIn, Vector3.zero), nOut = SafeNormalize(dOut, Vector3.zero);
                    Vector3 fwd = SafeNormalize(nIn + nOut, nOut.sqrMagnitude > 0f ? nOut : nIn);
                    rot = Quaternion.LookRotation(fwd, PerpendicularUp(fwd, upN));
                    if (nIn.sqrMagnitude > 0f && nOut.sqrMagnitude > 0f)
                    {
                        float c = Mathf.Max(Vector3.Dot(fwd, nOut), 0.2f);
                        Vector3 axisLocal = Quaternion.Inverse(rot) * Vector3.Cross(nIn, nOut);
                        if (axisLocal.sqrMagnitude > 1e-10f)
                        {
                            if (Mathf.Abs(axisLocal.y) >= Mathf.Abs(axisLocal.x)) scale.x = 1f / c;
                            else scale.y = 1f / c;
                        }
                    }
                }
                result.Add(new LoftFrame(points[i], rot, scale, hard));
            }
        }

        /// <summary>Straight sweep of a section from a to b (section up = <paramref name="up"/> projected perpendicular to a->b).</summary>
        public static void Extrude(MeshBuilder mb, CrossSection section, Vector3 a, Vector3 b, Vector3 up, bool capEnds = true,
            bool inside = false)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-6f) return;
            Vector3 dir = d / len;
            Quaternion rot = Quaternion.LookRotation(dir, PerpendicularUp(dir, up));
            s_twoFrames.Clear();
            s_twoFrames.Add(new LoftFrame(a, rot));
            s_twoFrames.Add(new LoftFrame(b, rot));
            Loft(mb, section, s_twoFrames, capEnds, inside);
        }

        /// <summary>
        /// I-section member from a to b at final length (truss members, gantry stringers): web along <paramref name="up"/>,
        /// flanges perpendicular. 44 triangles (24 sides + 2 caps of 10), all edges hard. UV0 = (perimeter, length) metres.
        /// With <paramref name="thinLine"/> UV1 = (axis, max half extent) and UV2 = (midpoint on the axis, 1), mesh space.
        /// </summary>
        public static void IBeam(MeshBuilder mb, Vector3 a, Vector3 b, float height, float flangeWidth, float webThickness,
            float flangeThickness, Vector3 up, bool thinLine = false)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-12f) return;
            IBeamSection(height, flangeWidth, webThickness, flangeThickness, s_ibeamSection);
            var tl = BeginThinLine(mb, thinLine, d, 0.5f * Mathf.Max(height, flangeWidth), (a + b) * 0.5f);
            Extrude(mb, s_ibeamSection, a, b, up, true);
            EndThinLine(mb, tl);
        }

        // ------------------------------------------------------------------------------------------ helix

        /// <summary>
        /// Sweep any section along a helix around the vertical axis through <paramref name="centre"/> (tube: a circle section;
        /// ribbon: an open line section). The helix starts at angle <paramref name="startDeg"/> (from +X toward +Z) and climbs
        /// <paramref name="risePerTurn"/> per revolution; negative turns wind the other way. Section x is horizontal (radial),
        /// section y is the frame up.
        /// </summary>
        public static void Helix(MeshBuilder mb, Vector3 centre, float radius, float risePerTurn, float startDeg, float turns,
            int segmentsPerTurn, CrossSection section, bool capEnds = false, bool inside = false)
        {
            if (section == null || Mathf.Abs(turns) < 1e-6f) return;
            int segs = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(turns) * Mathf.Max(3, segmentsPerTurn)));
            float a0 = startDeg * Mathf.Deg2Rad;
            float sweep = 2f * Mathf.PI * turns;
            float rise = risePerTurn * turns;
            s_helixFrames.Clear();
            for (int i = 0; i <= segs; i++)
            {
                float f = (float)i / segs;
                float a = a0 + sweep * f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector3 pos = centre + new Vector3(radius * ca, rise * f, radius * sa);
                Vector3 tan = new Vector3(-radius * sa * sweep, rise, radius * ca * sweep);
                tan = SafeNormalize(tan, Vector3.forward);
                Quaternion rot = Quaternion.LookRotation(tan, PerpendicularUp(tan, Vector3.up));
                s_helixFrames.Add(new LoftFrame(pos, rot));
            }
            Loft(mb, section, s_helixFrames, capEnds, inside);
        }

        /// <summary>Round tube along a helix (cables, spiral rails). See <see cref="Helix"/>.</summary>
        public static void HelixTube(MeshBuilder mb, Vector3 centre, float radius, float risePerTurn, float startDeg, float turns,
            float tubeRadius, int segmentsPerTurn = 48, int tubeSegments = 8, bool capEnds = true)
        {
            Circle(tubeRadius, tubeSegments, s_helixSection);
            Helix(mb, centre, radius, risePerTurn, startDeg, turns, segmentsPerTurn, s_helixSection, capEnds);
        }

        /// <summary>
        /// Flat ribbon of <paramref name="width"/> (radially centred on the helix) facing up along a helix (spiral treads, cable
        /// trays); doubleSided adds the downward face as a separate surface. See <see cref="Helix"/>.
        /// </summary>
        public static void HelixRibbon(MeshBuilder mb, Vector3 centre, float radius, float risePerTurn, float startDeg, float turns,
            float width, int segmentsPerTurn = 48, bool doubleSided = false)
        {
            // open section: front side is to the right of the point order, so +x -> -x faces frame up
            s_helixSection.Clear(false);
            s_helixSection.Add(new Vector2(0.5f * width, 0f));
            s_helixSection.Add(new Vector2(-0.5f * width, 0f));
            Helix(mb, centre, radius, risePerTurn, startDeg, turns, segmentsPerTurn, s_helixSection);
            if (doubleSided) Helix(mb, centre, radius, risePerTurn, startDeg, turns, segmentsPerTurn, s_helixSection, false, true);
        }

        // ------------------------------------------------------------------------------------------ sections

        /// <summary>
        /// Rounded rectangle w x h centred on <paramref name="centre"/>, counter-clockwise from the bottom-right corner arc.
        /// segmentsPerCorner = 1 gives a 45 degree chamfer (use hardCorners for crisp chamfer facets, e.g. the 0.08 m gallery
        /// chamfer); larger counts give smooth arcs with exact radial normals. radius 0 gives a sharp rectangle (all points hard).
        /// </summary>
        public static CrossSection RoundedRectangle(float w, float h, float radius, int segmentsPerCorner, CrossSection result = null,
            Vector2 centre = default, bool hardCorners = false)
        {
            CrossSection s = (result ?? new CrossSection()).Clear(true);
            float hw = Mathf.Abs(w) * 0.5f, hh = Mathf.Abs(h) * 0.5f;
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(hw, hh));
            int seg = Mathf.Max(1, segmentsPerCorner);
            if (r <= 1e-6f)
            {
                s.Add(centre + new Vector2(hw, -hh), true);
                s.Add(centre + new Vector2(hw, hh), true);
                s.Add(centre + new Vector2(-hw, hh), true);
                s.Add(centre + new Vector2(-hw, -hh), true);
                return s;
            }
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = (corner == 0 || corner == 1) ? hw - r : -hw + r;
                float cy = (corner == 1 || corner == 2) ? hh - r : -hh + r;
                float startAng = -0.5f * Mathf.PI + corner * 0.5f * Mathf.PI;
                for (int i = 0; i <= seg; i++)
                {
                    float a = startAng + 0.5f * Mathf.PI * i / seg;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    s.Add(centre + new Vector2(cx, cy) + dir * r, hardCorners, hardCorners ? Vector2.zero : dir);
                }
            }
            s.RemoveClosingDuplicate();
            return s;
        }

        /// <summary>Axis-aligned rectangle (4 hard points), counter-clockwise.</summary>
        public static CrossSection Rectangle(float w, float h, CrossSection result = null, Vector2 centre = default)
            => RoundedRectangle(w, h, 0f, 1, result, centre);

        /// <summary>Circle with exact radial normals (smooth), counter-clockwise from +x.</summary>
        public static CrossSection Circle(float radius, int segments, CrossSection result = null, Vector2 centre = default)
        {
            CrossSection s = (result ?? new CrossSection()).Clear(true);
            int seg = Mathf.Max(3, segments);
            for (int i = 0; i < seg; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                s.Add(centre + dir * radius, false, dir);
            }
            return s;
        }

        /// <summary>I-section (12 hard points, counter-clockwise): height along y, flange width along x.</summary>
        public static CrossSection IBeamSection(float height, float flangeWidth, float webThickness, float flangeThickness,
            CrossSection result = null)
        {
            CrossSection s = (result ?? new CrossSection()).Clear(true);
            float H = Mathf.Abs(height) * 0.5f, W = Mathf.Abs(flangeWidth) * 0.5f;
            float wt = Mathf.Clamp(Mathf.Abs(webThickness) * 0.5f, 1e-4f, W * 0.95f);
            float T = Mathf.Clamp(Mathf.Abs(flangeThickness), 1e-4f, H * 0.95f);
            s.Add(-W, -H, true); s.Add(W, -H, true); s.Add(W, -H + T, true); s.Add(wt, -H + T, true);
            s.Add(wt, H - T, true); s.Add(W, H - T, true); s.Add(W, H, true); s.Add(-W, H, true);
            s.Add(-W, H - T, true); s.Add(-wt, H - T, true); s.Add(-wt, -H + T, true); s.Add(-W, -H + T, true);
            return s;
        }

        /// <summary>
        /// Symmetric airfoil (NACA 00xx thickness law, closed trailing edge) with chord along x centred on 0 and thickness along y,
        /// counter-clockwise from the trailing edge (hard) over the upper surface; smooth elsewhere. Constant point count
        /// (2 * pointsPerSide) so it can vary per frame in <see cref="LoftVarying"/> (fan blades).
        /// </summary>
        public static CrossSection Airfoil(float chord, float thickness, int pointsPerSide, CrossSection result = null)
        {
            CrossSection s = (result ?? new CrossSection()).Clear(true);
            int m = Mathf.Max(3, pointsPerSide);
            float c = Mathf.Abs(chord);
            float tr = c > 1e-6f ? Mathf.Abs(thickness) / c : 0.1f;
            // trailing edge, upper surface toward the leading edge (cosine spacing), leading edge, lower surface back
            s.Add(new Vector2(0.5f * c, 0f), true);
            for (int i = 1; i <= m - 1; i++)
            {
                float x = 0.5f * (1f + Mathf.Cos(Mathf.PI * i / m)); // 1 -> 0
                s.Add(new Vector2((x - 0.5f) * c, NacaHalfThickness(x, tr) * c), false);
            }
            s.Add(new Vector2(-0.5f * c, 0f), false, new Vector2(-1f, 0f));
            for (int i = m - 1; i >= 1; i--)
            {
                float x = 0.5f * (1f + Mathf.Cos(Mathf.PI * i / m));
                s.Add(new Vector2((x - 0.5f) * c, -NacaHalfThickness(x, tr) * c), false);
            }
            return s;
        }

        static float NacaHalfThickness(float x, float t)
        {
            x = Mathf.Clamp01(x);
            return 5f * t * (0.2969f * Mathf.Sqrt(x) - 0.1260f * x - 0.3516f * x * x + 0.2843f * x * x * x - 0.1036f * x * x * x * x);
        }

        // ------------------------------------------------------------------------------------------ triangulation

        /// <summary>
        /// Ear-clipping triangulation of a simple polygon (either winding, collinear points allowed). Writes index triples into
        /// <paramref name="tris"/> (cleared), each counter-clockwise in x-right / y-up. Load-time helper, O(n^2) to O(n^3).
        /// </summary>
        public static void Triangulate(List<Vector2> poly, List<int> tris)
        {
            tris.Clear();
            int n = poly.Count;
            if (n < 3) return;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                area += (double)p.x * q.y - (double)q.x * p.y;
            }
            s_ear.Clear();
            if (area >= 0) for (int i = 0; i < n; i++) s_ear.Add(i);
            else for (int i = n - 1; i >= 0; i--) s_ear.Add(i);

            int guard = 0, guardMax = 4 * n * n + 16;
            while (s_ear.Count > 3 && guard++ < guardMax)
            {
                int m = s_ear.Count;
                bool clipped = false;
                for (int i = 0; i < m; i++)
                {
                    int ia = s_ear[(i + m - 1) % m], ib = s_ear[i], ic = s_ear[(i + 1) % m];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    float cr = Cross2(b - a, c - b);
                    float scale = (b - a).sqrMagnitude + (c - b).sqrMagnitude;
                    if (Mathf.Abs(cr) <= 1e-9f * scale)
                    {
                        s_ear.RemoveAt(i); // collinear vertex: drop without a triangle
                        clipped = true;
                        break;
                    }
                    if (cr < 0f) continue; // reflex
                    bool blocked = false;
                    for (int j = 0; j < m; j++)
                    {
                        int ip = s_ear[j];
                        if (ip == ia || ip == ib || ip == ic) continue;
                        Vector2 p = poly[ip];
                        if (p == a || p == b || p == c) continue;
                        if (PointInTriangle(p, a, b, c)) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    s_ear.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (s_ear.Count == 3)
            {
                Vector2 a = poly[s_ear[0]], b = poly[s_ear[1]], c = poly[s_ear[2]];
                if (Mathf.Abs(Cross2(b - a, c - b)) > 1e-12f) { tris.Add(s_ear[0]); tris.Add(s_ear[1]); tris.Add(s_ear[2]); }
            }
            else if (s_ear.Count > 3)
            {
                // numerical fallback (self-touching input): fan the remainder
                for (int i = 1; i + 1 < s_ear.Count; i++) { tris.Add(s_ear[0]); tris.Add(s_ear[i]); tris.Add(s_ear[i + 1]); }
            }
        }

        static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2(b - a, p - a), d2 = Cross2(c - b, p - b), d3 = Cross2(a - c, p - c);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }
    }
}
