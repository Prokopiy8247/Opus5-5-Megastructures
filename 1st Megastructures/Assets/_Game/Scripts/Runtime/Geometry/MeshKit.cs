using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// A 2D cross-section (polyline or polygon) for <see cref="MeshKit.Loft"/>, <see cref="MeshKit.Extrude"/> and friends.
    ///
    /// Conventions:
    /// - Points live in the loft frame's local XY plane (x = frame right, y = frame up); the loft runs along frame +Z.
    /// - Closed sections: the outside is determined from the signed area, so either winding gives outward normals
    ///   (counter-clockwise in x-right / y-up is the canonical order). Open sections: the front side is to the RIGHT of the
    ///   point order (for travel direction t the 2D normal is (t.y, -t.x)).
    /// - Per-point <see cref="Hard"/> flag: a hard point splits the normal (two vertices, each flat to its own edge).
    /// - Optional explicit outward <see cref="Normals"/> (zero = derive from the neighbouring edges) give exact normals on
    ///   sampled curves (rounded corners, circles, airfoils).
    /// Reuse instances: <see cref="Clear"/> keeps the list capacity, so building sections never allocates after warm-up.
    /// </summary>
    public sealed class CrossSection
    {
        public readonly List<Vector2> Points = new List<Vector2>(32);
        public readonly List<bool> Hard = new List<bool>(32);
        public readonly List<Vector2> Normals = new List<Vector2>(32);

        /// <summary>True for a closed polygon (the last point connects back to the first).</summary>
        public bool Closed = true;

        public int Count => Points.Count;

        public CrossSection Clear(bool closed = true)
        {
            Points.Clear(); Hard.Clear(); Normals.Clear();
            Closed = closed;
            return this;
        }

        /// <summary>Append a point. Consecutive duplicates (closer than 1e-6 m) are ignored.</summary>
        public CrossSection Add(Vector2 p, bool hard = false) => Add(p, hard, Vector2.zero);

        public CrossSection Add(float x, float y, bool hard = false) => Add(new Vector2(x, y), hard, Vector2.zero);

        /// <summary>Append a point with an explicit outward normal (used for smooth points only).</summary>
        public CrossSection Add(Vector2 p, bool hard, Vector2 outwardNormal)
        {
            int n = Points.Count;
            if (n > 0 && (Points[n - 1] - p).sqrMagnitude < 1e-12f)
            {
                if (hard) Hard[n - 1] = true;
                return this;
            }
            Points.Add(p); Hard.Add(hard); Normals.Add(outwardNormal);
            return this;
        }

        /// <summary>Mark every point hard (flat-shaded facets, e.g. chamfered profiles).</summary>
        public void SetAllHard(bool hard)
        {
            for (int i = 0; i < Hard.Count; i++) Hard[i] = hard;
        }

        /// <summary>Shoelace signed area (positive = counter-clockwise in x-right / y-up). Zero for open sections.</summary>
        public float SignedArea()
        {
            if (!Closed) return 0f;
            int n = Points.Count;
            double a = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = Points[i], q = Points[(i + 1) % n];
                a += (double)p.x * q.y - (double)q.x * p.y;
            }
            return (float)(a * 0.5);
        }

        /// <summary>Perimeter length (closing edge included when <see cref="Closed"/>).</summary>
        public float Perimeter()
        {
            int n = Points.Count;
            float l = 0f;
            int edges = Closed ? n : n - 1;
            for (int i = 0; i < edges; i++) l += (Points[(i + 1) % n] - Points[i]).magnitude;
            return l;
        }

        public void CopyFrom(CrossSection other)
        {
            Clear(other.Closed);
            for (int i = 0; i < other.Points.Count; i++)
            {
                Points.Add(other.Points[i]); Hard.Add(other.Hard[i]); Normals.Add(other.Normals[i]);
            }
        }

        /// <summary>Drop a closing duplicate (last point equal to the first) of a closed section.</summary>
        internal void RemoveClosingDuplicate()
        {
            int n = Points.Count;
            if (Closed && n > 1 && (Points[n - 1] - Points[0]).sqrMagnitude < 1e-12f)
            {
                if (Hard[n - 1]) Hard[0] = true;
                Points.RemoveAt(n - 1); Hard.RemoveAt(n - 1); Normals.RemoveAt(n - 1);
            }
        }
    }

    /// <summary>
    /// One frame of a loft: the cross-section's local XY plane is placed at <see cref="Position"/> with <see cref="Rotation"/>
    /// (section x = rotation * right, y = rotation * up) and scaled per axis by <see cref="Scale"/> ((0,0) counts as (1,1)).
    /// <see cref="Hard"/> splits the vertex ring at this frame (sharp crease across the path, e.g. a stair slope change).
    /// </summary>
    public struct LoftFrame
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Scale;
        public bool Hard;

        public LoftFrame(Vector3 position, Quaternion rotation) : this(position, rotation, Vector2.one, false) { }

        public LoftFrame(Vector3 position, Quaternion rotation, Vector2 scale, bool hard = false)
        {
            Position = position; Rotation = rotation; Scale = scale; Hard = hard;
        }

        public Vector2 EffectiveScale => new Vector2(Scale.x == 0f ? 1f : Scale.x, Scale.y == 0f ? 1f : Scale.y);

        public Vector3 Forward => Rotation * Vector3.forward;

        /// <summary>World (builder-local) position of a section point in this frame.</summary>
        public Vector3 Transform(Vector2 p)
        {
            Vector2 s = EffectiveScale;
            return Position + Rotation * new Vector3(p.x * s.x, p.y * s.y, 0f);
        }
    }

    /// <summary>Member cross-section for <see cref="MeshKit.WarrenTruss"/>.</summary>
    public enum TrussMemberShape { IBeam = 0, ChamferBox = 1 }

    /// <summary>
    /// Procedural mesh generators (Part 7.3, 8.7, 14.7.5). Every generator appends to a <see cref="MeshBuilder"/> and
    /// respects its conventions (see the MeshBuilder header): positions and normals pass through <see cref="MeshBuilder.Matrix"/>
    /// (keep it rigid + uniform scale), vertex colour comes from <see cref="MeshBuilder.Color"/> (R = AO, G = edge wear; the
    /// generators raise G to 255 on chamfers and bevels and restore the colour afterwards), UV0 is in metres.
    /// ThinLine members (thinLine = true: ChamferMember, IBeam, Cylinder / TaperedCylinder, WarrenTruss members and gussets)
    /// write UV1 = (member axis direction, radius) and UV2 = (a point on the member axis, 1), both in mesh space (after
    /// <see cref="MeshBuilder.Matrix"/>), which the ThinLine vertex pass needs for a crack-free push of split-normal sections
    /// (Shaders/Include/AbyssCommon.hlsl AbThinLineExpand). Other vertices of the same mesh get zero UV1 / UV2 (w = 0).
    ///
    /// Guarantees (verified by MeshKitTests + <see cref="MeshValidation"/>): outward normals on closed shapes, triangle winding
    /// always agrees with the vertex normals (each triangle is oriented against its own vertex normals at emission, which also
    /// keeps mirrored builder matrices correct), no degenerate triangles (area below 1e-10 is never emitted), hard normals split
    /// at the documented angle, long members authored at final length (no non-uniform scaling anywhere).
    ///
    /// Angles: "phi" angles run from +X toward +Z (the shaft convention of AbyssConst.SHAFT_FACE_NORMAL_DEG); degrees where the
    /// parameter name ends in Deg, radians otherwise.
    ///
    /// Performance: generation runs at load (1.5 M triangles must build well within 15 s). No per-vertex allocation: shared
    /// static scratch lists (main thread only, not re-entrant), capacity reserved up front.
    /// </summary>
    public static partial class MeshKit
    {
        /// <summary>Default normal split angle (14.7.5).</summary>
        public const float HardEdgeAngleDeg = 40f;

        /// <summary>Squared cross-product length below which a triangle is not emitted (area 1.58e-10 m^2, validator limit 1e-10).</summary>
        const float DegenerateCross2 = 1e-19f;

        const float Cos30 = 0.8660254037844386f;
        const float Sqrt3 = 1.7320508075688772f;
        const byte WearFull = 255;

        // ------------------------------------------------------------------------------------------ emission helpers

        /// <summary>Grow builder list capacities for an upcoming generator (geometric growth, never shrinks).</summary>
        public static void Reserve(MeshBuilder mb, int addVertices, int addIndices)
        {
            Grow(mb.Positions, addVertices);
            Grow(mb.Normals, addVertices);
            Grow(mb.Colors, addVertices);
            Grow(mb.UV0, addVertices);
            if (mb.WriteUV1) Grow(mb.UV1, addVertices);
            if (mb.WriteUV2) Grow(mb.UV2, addVertices);
            Grow(mb.Indices, addIndices);
        }

        static void Grow<T>(List<T> list, int add)
        {
            int need = list.Count + add;
            if (need > list.Capacity) list.Capacity = Math.Max(need, list.Capacity * 2);
        }

        /// <summary>
        /// Emit triangle (a, b, c) wound so that its geometric normal agrees with the sum of its vertex normals.
        /// Triangles with area below ~1.6e-10 m^2 are skipped (degenerate poles, zero-length bevels).
        /// </summary>
        internal static void Tri(MeshBuilder mb, int a, int b, int c)
        {
            List<Vector3> p = mb.Positions;
            Vector3 pa = p[a];
            Vector3 cr = Vector3.Cross(p[b] - pa, p[c] - pa);
            if (cr.sqrMagnitude < DegenerateCross2) return;
            List<Vector3> n = mb.Normals;
            Vector3 ns = n[a] + n[b] + n[c];
            if (Vector3.Dot(cr, ns) >= 0f) mb.Tri(a, b, c);
            else mb.Tri(a, c, b);
        }

        /// <summary>Quad a-b-c-d (cyclic order, either orientation) as two oriented triangles split along a-c.</summary>
        internal static void Quad(MeshBuilder mb, int a, int b, int c, int d)
        {
            Tri(mb, a, b, c);
            Tri(mb, a, c, d);
        }

        /// <summary>Orthonormal tangent pair for a unit normal (t horizontal whenever n is not vertical).</summary>
        internal static void Basis(Vector3 n, out Vector3 t, out Vector3 b)
        {
            Vector3 refAxis = Mathf.Abs(n.y) < 0.999f ? Vector3.up : Vector3.right;
            t = Vector3.Cross(refAxis, n).normalized;
            b = Vector3.Cross(n, t);
        }

        static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            float m = v.magnitude;
            return m > 1e-12f ? v / m : fallback;
        }

        static Color32 WithWear(Color32 c, byte g) { c.g = g; return c; }

        // ------------------------------------------------------------------------------------------ ThinLine UV1 / UV2

        struct ThinLineState { public bool Active, Write1, Write2; public Vector4 Value1, Value2; }

        /// <summary>
        /// Start writing the ThinLine vertex data (14.7.9) for one member: UV1 = (member axis direction in mesh space, radius),
        /// UV2 = (<paramref name="axisPoint"/> in mesh space, 1). <paramref name="axis"/> and axisPoint are in the builder's
        /// current local frame; both are converted with <see cref="MeshBuilder.Matrix"/> here because MeshBuilder stores UV1 / UV2
        /// as given. UV2 makes the shader push every vertex radially away from the axis line, so the split normals of boxes and
        /// I-sections (and the flat caps of cylinders) no longer open cracks when a member is widened.
        /// </summary>
        static ThinLineState BeginThinLine(MeshBuilder mb, bool enable, Vector3 axis, float radius, Vector3 axisPoint)
        {
            var s = new ThinLineState
            {
                Active = enable, Write1 = mb.WriteUV1, Value1 = mb.CurrentUV1, Write2 = mb.WriteUV2, Value2 = mb.CurrentUV2
            };
            if (!enable) return s;
            // Earlier vertices written without UV1 / UV2 must be padded so indices stay aligned.
            while (mb.UV1.Count < mb.Positions.Count) mb.UV1.Add(Vector4.zero);
            while (mb.UV2.Count < mb.Positions.Count) mb.UV2.Add(Vector4.zero);
            float al = axis.magnitude;
            Vector3 ax = mb.Matrix.MultiplyVector(al > 1e-12f ? axis / al : Vector3.up);
            float scale = ax.magnitude;
            ax = scale > 1e-12f ? ax / scale : Vector3.up;
            mb.WriteUV1 = true;
            mb.CurrentUV1 = new Vector4(ax.x, ax.y, ax.z, radius * scale);
            Vector3 ap = mb.Matrix.MultiplyPoint3x4(axisPoint);
            mb.WriteUV2 = true;
            mb.CurrentUV2 = new Vector4(ap.x, ap.y, ap.z, 1f);
            return s;
        }

        static void EndThinLine(MeshBuilder mb, in ThinLineState s)
        {
            if (!s.Active) return;
            mb.WriteUV1 = s.Write1;
            mb.CurrentUV1 = s.Value1;
            mb.WriteUV2 = s.Write2;
            mb.CurrentUV2 = s.Value2;
        }

        // ------------------------------------------------------------------------------------------ ChamferBox

        static readonly Vector3[] s_poly = new Vector3[4];

        /// <summary>Axis-aligned chamfered box (see the rotated overload).</summary>
        public static void ChamferBox(MeshBuilder mb, Vector3 centre, Vector3 size, float bevel)
            => ChamferBox(mb, centre, size, bevel, Quaternion.identity);

        /// <summary>
        /// Chamfered box: exactly 44 triangles = 6 faces (2 each) + 12 edge chamfers (2 each) + 8 corner triangles, 96 vertices,
        /// every facet flat-shaded (normals split: face/chamfer 45 degrees, chamfer/chamfer 60 degrees; the 35 degree corner
        /// triangles are kept flat as well, which is visually identical at 1-3 cm). Chamfer and corner vertices get edge wear
        /// G = 255, face vertices keep <see cref="MeshBuilder.Color"/>. UV0 in metres (planar per facet along its dominant axis).
        /// The bevel is clamped to [min(1e-4, 0.5 h), 0.9 h] where h is the smallest half extent; a box with a zero extent is skipped.
        /// </summary>
        /// <param name="centre">Box centre in the builder's local frame.</param>
        /// <param name="size">Full extents along the rotated axes (authored at final size, never scaled afterwards).</param>
        /// <param name="bevel">Chamfer width in metres measured along each face (1-3 cm near the path).</param>
        /// <param name="rotation">Box orientation about its centre.</param>
        public static void ChamferBox(MeshBuilder mb, Vector3 centre, Vector3 size, float bevel, Quaternion rotation)
        {
            Vector3 h = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;
            float minH = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
            if (minH <= 1e-6f) return;
            float b = Mathf.Clamp(bevel, Mathf.Min(1e-4f, 0.5f * minH), 0.9f * minH);
            Vector3 e = h - new Vector3(b, b, b);

            Reserve(mb, 96, 132);
            Color32 saved = mb.Color;
            byte faceWear = saved.g;
            mb.Push(Matrix4x4.TRS(centre, rotation, Vector3.one));

            // 6 faces
            for (int a = 0; a < 3; a++)
            {
                int u = (a + 1) % 3, w = (a + 2) % 3;
                for (int si = 0; si < 2; si++)
                {
                    float s = si == 0 ? -1f : 1f;
                    for (int q = 0; q < 4; q++)
                    {
                        float su = (q == 1 || q == 2) ? 1f : -1f;
                        float sw = q >= 2 ? 1f : -1f;
                        Vector3 p = default;
                        p[a] = s * h[a]; p[u] = su * e[u]; p[w] = sw * e[w];
                        s_poly[q] = p;
                    }
                    Vector3 n = default; n[a] = s;
                    Facet(mb, 4, n, faceWear);
                }
            }

            // 12 edge chamfers
            for (int pair = 0; pair < 3; pair++)
            {
                int a = pair == 2 ? 1 : 0;
                int c = pair == 0 ? 1 : 2;
                int t = 3 - a - c;
                for (int sa = 0; sa < 2; sa++)
                for (int sc = 0; sc < 2; sc++)
                {
                    float ga = sa == 0 ? -1f : 1f, gc = sc == 0 ? -1f : 1f;
                    Vector3 p0 = default, p1 = default, p2 = default, p3 = default;
                    p0[a] = ga * h[a]; p0[c] = gc * e[c]; p0[t] = e[t];
                    p1[a] = ga * h[a]; p1[c] = gc * e[c]; p1[t] = -e[t];
                    p2[a] = ga * e[a]; p2[c] = gc * h[c]; p2[t] = -e[t];
                    p3[a] = ga * e[a]; p3[c] = gc * h[c]; p3[t] = e[t];
                    s_poly[0] = p0; s_poly[1] = p1; s_poly[2] = p2; s_poly[3] = p3;
                    Vector3 n = default; n[a] = ga; n[c] = gc;
                    Facet(mb, 4, n.normalized, WearFull);
                }
            }

            // 8 corner triangles
            for (int corner = 0; corner < 8; corner++)
            {
                float gx = (corner & 1) != 0 ? 1f : -1f, gy = (corner & 2) != 0 ? 1f : -1f, gz = (corner & 4) != 0 ? 1f : -1f;
                s_poly[0] = new Vector3(gx * h.x, gy * e.y, gz * e.z);
                s_poly[1] = new Vector3(gx * e.x, gy * h.y, gz * e.z);
                s_poly[2] = new Vector3(gx * e.x, gy * e.y, gz * h.z);
                Facet(mb, 3, new Vector3(gx, gy, gz) / Sqrt3, WearFull);
            }

            mb.Pop();
            mb.Color = saved;
        }

        /// <summary>Flat convex polygon from s_poly (cyclic order) with one normal; UV0 planar along the dominant normal axis.</summary>
        static void Facet(MeshBuilder mb, int count, Vector3 n, byte wear)
        {
            mb.Color = WithWear(mb.Color, wear);
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            int dom = (ax >= ay && ax >= az) ? 0 : (ay >= az ? 1 : 2);
            int first = mb.VertexCount;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = s_poly[i];
                Vector2 uv = dom == 0 ? new Vector2(p.z, p.y) : dom == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
                mb.Vertex(p, n, uv);
            }
            for (int i = 1; i < count - 1; i++) Tri(mb, first, first + i, first + i + 1);
        }

        /// <summary>
        /// Chamfered rectangular member between two points, authored at final length (box length = |b - a|).
        /// width runs along the member's right axis, height along <paramref name="up"/> (projected perpendicular to the member).
        /// With <paramref name="thinLine"/> UV1 = (axis, max half extent) and UV2 = (midpoint on the axis, 1), mesh space.
        /// </summary>
        public static void ChamferMember(MeshBuilder mb, Vector3 a, Vector3 b, float width, float height, float bevel, Vector3 up,
            bool thinLine = false)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            Vector3 dir = d / len;
            Quaternion rot = Quaternion.LookRotation(dir, PerpendicularUp(dir, up));
            var tl = BeginThinLine(mb, thinLine, dir, 0.5f * Mathf.Max(width, height), (a + b) * 0.5f);
            ChamferBox(mb, (a + b) * 0.5f, new Vector3(width, height, len), bevel, rot);
            EndThinLine(mb, tl);
        }

        /// <summary>A unit vector perpendicular to dir, as close to the hint as possible (stable fallbacks when parallel).</summary>
        internal static Vector3 PerpendicularUp(Vector3 dir, Vector3 upHint)
        {
            Vector3 u = upHint - dir * Vector3.Dot(upHint, dir);
            if (u.sqrMagnitude < 1e-8f)
            {
                Vector3 alt = Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.forward;
                u = alt - dir * Vector3.Dot(alt, dir);
            }
            return u.normalized;
        }

        // ------------------------------------------------------------------------------------------ HexBolt

        static readonly Vector3[] s_hexRing = new Vector3[6];

        /// <summary>
        /// Hex bolt head in exactly 12 triangles, watertight: a flat hexagonal top (6-triangle fan, normal = <paramref name="normal"/>)
        /// at <paramref name="headHeight"/> above <paramref name="position"/>, and 6 flat side facets converging to an apex
        /// <paramref name="embedDepth"/> below the surface (a hexagonal bipyramid, so the visible sides taper slightly inward like a
        /// shadowed bolt flank). The host surface must be at least embedDepth thick at the bolt. Ring and side vertices carry
        /// edge wear G = 255.
        /// </summary>
        /// <param name="position">Point on the host surface under the bolt centre.</param>
        /// <param name="normal">Host surface outward normal (bolt axis).</param>
        /// <param name="acrossFlats">Head size across flats (e.g. 0.08 m flange bolts).</param>
        /// <param name="rotationDeg">Rotation of the hexagon about the axis.</param>
        /// <param name="headHeight">Head height above the surface (default 0.38 x acrossFlats).</param>
        /// <param name="embedDepth">Apex depth below the surface (default 1.0 x acrossFlats).</param>
        public static void HexBolt(MeshBuilder mb, Vector3 position, Vector3 normal, float acrossFlats, float rotationDeg = 0f,
            float headHeight = -1f, float embedDepth = -1f)
        {
            if (acrossFlats <= 1e-5f) return;
            Vector3 n = SafeNormalize(normal, Vector3.up);
            Basis(n, out Vector3 t, out Vector3 bt);
            if (rotationDeg != 0f)
            {
                Quaternion r = Quaternion.AngleAxis(rotationDeg, n);
                t = r * t; bt = r * bt;
            }
            float H = headHeight > 0f ? headHeight : 0.38f * acrossFlats;
            float D = embedDepth > 0f ? embedDepth : 1.0f * acrossFlats;
            float R = acrossFlats / Sqrt3; // corner radius
            Vector3 top = position + n * H;
            Vector3 apex = position - n * D;

            Reserve(mb, 25, 36);
            Color32 saved = mb.Color;
            // top fan
            int c0 = mb.Vertex(top, n, Vector2.zero);
            mb.Color = WithWear(saved, WearFull);
            int ring0 = mb.VertexCount;
            for (int k = 0; k < 6; k++)
            {
                float ang = k * (Mathf.PI / 3f);
                Vector3 off = (t * Mathf.Cos(ang) + bt * Mathf.Sin(ang)) * R;
                s_hexRing[k] = top + off;
                mb.Vertex(top + off, n, new Vector2(Vector3.Dot(off, t), Vector3.Dot(off, bt)));
            }
            for (int k = 0; k < 6; k++) Tri(mb, c0, ring0 + k, ring0 + (k + 1) % 6);

            // flanks
            for (int k = 0; k < 6; k++)
            {
                Vector3 r0 = s_hexRing[k];
                Vector3 r1 = s_hexRing[(k + 1) % 6];
                Vector3 fn = Vector3.Cross(r1 - r0, apex - r0);
                Vector3 radial = (r0 + r1) * 0.5f - top;
                if (Vector3.Dot(fn, radial) < 0f) fn = -fn;
                fn = SafeNormalize(fn, radial.normalized);
                float edge = (r1 - r0).magnitude;
                int i0 = mb.Vertex(r0, fn, new Vector2(0f, 0f));
                int i1 = mb.Vertex(r1, fn, new Vector2(edge, 0f));
                int i2 = mb.Vertex(apex, fn, new Vector2(edge * 0.5f, H + D));
                Tri(mb, i0, i1, i2);
            }
            mb.Color = saved;
        }

        // ------------------------------------------------------------------------------------------ flat primitives

        /// <summary>
        /// Subdivided flat parallelogram for large walls (keeps vertex density reasonable for per-vertex AO and long-distance
        /// interpolation). axisU points right and axisV up as seen from the front; the front normal is cross(axisV, axisU)
        /// (Unity's clockwise rule). UV0 = (u, v) in metres from the origin.
        /// </summary>
        public static void GridQuad(MeshBuilder mb, Vector3 origin, Vector3 axisU, Vector3 axisV, float sizeU, float sizeV,
            int divisionsU, int divisionsV)
        {
            Vector3 u = SafeNormalize(axisU, Vector3.right), v = SafeNormalize(axisV, Vector3.up);
            Vector3 n = SafeNormalize(Vector3.Cross(v, u), Vector3.back);
            int du = Mathf.Max(1, divisionsU), dv = Mathf.Max(1, divisionsV);
            Reserve(mb, (du + 1) * (dv + 1), du * dv * 6);
            int first = mb.VertexCount;
            for (int j = 0; j <= dv; j++)
            {
                float fv = sizeV * j / dv;
                for (int i = 0; i <= du; i++)
                {
                    float fu = sizeU * i / du;
                    mb.Vertex(origin + u * fu + v * fv, n, new Vector2(fu, fv));
                }
            }
            int row = du + 1;
            for (int j = 0; j < dv; j++)
            for (int i = 0; i < du; i++)
            {
                int a = first + j * row + i;
                Quad(mb, a, a + row, a + row + 1, a + 1);
            }
        }

        /// <summary>Flat disc (triangle fan) facing <paramref name="normal"/>. UV0 planar in metres around the centre.</summary>
        public static void Disc(MeshBuilder mb, Vector3 centre, Vector3 normal, float radius, int segments = 32, float startDeg = 0f)
        {
            if (radius <= 1e-6f) return;
            int seg = Mathf.Max(3, segments);
            Vector3 n = SafeNormalize(normal, Vector3.up);
            Basis(n, out Vector3 t, out Vector3 b);
            Reserve(mb, seg + 1, seg * 3);
            int c = mb.Vertex(centre, n, Vector2.zero);
            float a0 = startDeg * Mathf.Deg2Rad;
            for (int k = 0; k < seg; k++)
            {
                float a = a0 + k * (2f * Mathf.PI / seg);
                float x = Mathf.Cos(a) * radius, y = Mathf.Sin(a) * radius;
                mb.Vertex(centre + t * x + b * y, n, new Vector2(x, y));
            }
            for (int k = 0; k < seg; k++) Tri(mb, c, c + 1 + k, c + 1 + (k + 1) % seg);
        }

        /// <summary>
        /// Flat horizontal ring (or ring sector) at centre.y between rIn and rOut, facing up (or down). Angles from +X toward +Z.
        /// UV0 planar (x, z) in metres (local positions, so adjacent pieces line up).
        /// </summary>
        public static void Annulus(MeshBuilder mb, Vector3 centre, float rIn, float rOut, int segments, bool faceUp = true,
            float arcStartDeg = 0f, float arcEndDeg = 360f)
        {
            int seg = Mathf.Max(3, segments);
            float r0 = Mathf.Max(0f, Mathf.Min(rIn, rOut)), r1 = Mathf.Max(rIn, rOut);
            if (r1 - r0 <= 1e-6f) return;
            Vector3 n = faceUp ? Vector3.up : Vector3.down;
            float a0 = arcStartDeg * Mathf.Deg2Rad, a1 = arcEndDeg * Mathf.Deg2Rad;
            Reserve(mb, (seg + 1) * 2, seg * 6);
            int first = mb.VertexCount;
            for (int k = 0; k <= seg; k++)
            {
                float a = Mathf.Lerp(a0, a1, (float)k / seg);
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vector3 pi = centre + new Vector3(ca * r0, 0f, sa * r0);
                Vector3 po = centre + new Vector3(ca * r1, 0f, sa * r1);
                mb.Vertex(pi, n, new Vector2(pi.x, pi.z));
                mb.Vertex(po, n, new Vector2(po.x, po.z));
            }
            for (int k = 0; k < seg; k++)
            {
                int a = first + 2 * k;
                Quad(mb, a, a + 1, a + 3, a + 2);
            }
        }

        // ------------------------------------------------------------------------------------------ Sphere, Torus

        /// <summary>UV sphere, smooth. UV0 = (arc length around at the equator radius, arc length from the south pole), metres.</summary>
        public static void Sphere(MeshBuilder mb, Vector3 centre, float radius, int lonSegments = 24, int latSegments = 12)
        {
            if (radius <= 1e-6f) return;
            int lon = Mathf.Max(3, lonSegments), lat = Mathf.Max(2, latSegments);
            Reserve(mb, (lon + 1) * (lat + 1), lon * lat * 6);
            int first = mb.VertexCount;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float st = Mathf.Sin(th), ct = Mathf.Cos(th);
                for (int k = 0; k <= lon; k++)
                {
                    float ph = 2f * Mathf.PI * k / lon;
                    Vector3 n = new Vector3(st * Mathf.Cos(ph), -ct, st * Mathf.Sin(ph));
                    mb.Vertex(centre + n * radius, n, new Vector2(radius * ph, radius * th));
                }
            }
            int row = lon + 1;
            for (int i = 0; i < lat; i++)
            for (int k = 0; k < lon; k++)
            {
                int a = first + i * row + k;
                Quad(mb, a, a + 1, a + row + 1, a + row);
            }
        }

        /// <summary>
        /// Torus around <paramref name="axis"/> through centre (handwheels, rings). Smooth. UV0 = (major arc at the centre circle,
        /// minor arc), metres.
        /// </summary>
        public static void Torus(MeshBuilder mb, Vector3 centre, Vector3 axis, float majorRadius, float minorRadius,
            int majorSegments = 32, int minorSegments = 12)
        {
            if (majorRadius <= 1e-6f || minorRadius <= 1e-6f) return;
            int ma = Mathf.Max(3, majorSegments), mi = Mathf.Max(3, minorSegments);
            Quaternion q = Quaternion.FromToRotation(Vector3.up, SafeNormalize(axis, Vector3.up));
            Reserve(mb, (ma + 1) * (mi + 1), ma * mi * 6);
            int first = mb.VertexCount;
            for (int i = 0; i <= ma; i++)
            {
                float ph = 2f * Mathf.PI * i / ma;
                Vector3 radial = new Vector3(Mathf.Cos(ph), 0f, Mathf.Sin(ph));
                for (int j = 0; j <= mi; j++)
                {
                    float th = 2f * Mathf.PI * j / mi;
                    float ct = Mathf.Cos(th), st = Mathf.Sin(th);
                    Vector3 n = radial * ct + Vector3.up * st;
                    Vector3 p = radial * (majorRadius + minorRadius * ct) + Vector3.up * (minorRadius * st);
                    mb.Vertex(centre + q * p, q * n, new Vector2(majorRadius * ph, minorRadius * th));
                }
            }
            int row = mi + 1;
            for (int i = 0; i < ma; i++)
            for (int j = 0; j < mi; j++)
            {
                int a = first + i * row + j;
                Quad(mb, a, a + row, a + row + 1, a + 1);
            }
        }

        // ------------------------------------------------------------------------------------------ vertex bake forwarding

        /// <summary>Hemisphere-sampled AO against boxes, multiplied into vertex colour R. See <see cref="VertexBake.BakeAO"/>.</summary>
        public static void BakeAO(MeshBuilder mb, IList<Bounds> occluders, float maxDistance, int samples, uint seed)
            => VertexBake.BakeAO(mb, occluders, maxDistance, samples, seed);

        /// <summary>Edge wear G from the angle between split normals at shared positions. See <see cref="VertexBake.EdgeWearFromNormals"/>.</summary>
        public static void EdgeWearFromNormals(MeshBuilder mb, float angleStartDeg = 20f, float angleFullDeg = 50f)
            => VertexBake.EdgeWearFromNormals(mb, angleStartDeg, angleFullDeg);
    }
}
