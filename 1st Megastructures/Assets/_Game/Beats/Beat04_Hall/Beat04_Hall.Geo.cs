using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Geometry helpers of beat 4. Large faces are grids on world-anchored lattices (x every 2 m from -142, y every 2 m from
    /// -45, z every 4 m from 199, plus the opening and chamfer edges as extra cuts), so every two faces that share an edge
    /// (exterior box edges, the opening reveals, the inner corners of the hall) share their edge vertices exactly: no
    /// T-junctions, no pinholes along creases. All triangles are oriented by their normals (BeatKit.Tri).
    /// </summary>
    public sealed partial class Beat04_Hall
    {
        const float LAT_X0 = AbyssConst.HALL_X1, LAT_Y0 = AbyssConst.HALL_HULL_BOTTOM_Y, LAT_Z0 = AbyssConst.HALL_Z0;
        const float LAT_XY = 2f, LAT_Z = 4f;

        static readonly List<float> s_us = new List<float>(128);
        static readonly List<float> s_vs = new List<float>(128);
        static readonly List<Vector2> s_prof = new List<Vector2>(64);
        static readonly List<LoftFrame> s_frames = new List<LoftFrame>(8);
        static readonly CrossSection s_sec = new CrossSection();

        /// <summary>Breakpoints of [a, b]: a, every anchor + k * step strictly inside, b, plus optional extra cuts inside.</summary>
        static void Breaks(List<float> dst, float a, float b, float anchor, float step, float cut0 = float.NaN, float cut1 = float.NaN)
        {
            dst.Clear();
            dst.Add(a);
            int k0 = Mathf.FloorToInt((a - anchor) / step) + 1;
            for (int k = k0; k < k0 + 100000; k++)
            {
                float p = anchor + k * step;
                if (p >= b - 1e-3f) break;
                if (p > a + 1e-3f) dst.Add(p);
            }
            dst.Add(b);
            InsertCut(dst, cut0);
            InsertCut(dst, cut1);
        }

        static void InsertCut(List<float> l, float c)
        {
            if (float.IsNaN(c) || c <= l[0] + 1e-3f || c >= l[l.Count - 1] - 1e-3f) return;
            for (int i = 0; i < l.Count; i++)
            {
                if (Mathf.Abs(l[i] - c) < 1e-3f) { l[i] = c; return; }
                if (l[i] > c) { l.Insert(i, c); return; }
            }
        }

        /// <summary>
        /// Grid at origin + axisU * u + axisV * v for every u in us and v in vs (shared vertices inside the grid), front side =
        /// normal. UV0 = (u, v) in metres. Positions pass through the builder matrix (push -chunkOrigin for chunk-local data).
        /// </summary>
        static void GridRect(MeshBuilder mb, Vector3 origin, Vector3 axisU, Vector3 axisV, List<float> us, List<float> vs, Vector3 normal)
        {
            int nu = us.Count, nv = vs.Count;
            if (nu < 2 || nv < 2) return;
            MeshKit.Reserve(mb, nu * nv, (nu - 1) * (nv - 1) * 6);
            int first = mb.VertexCount;
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                    mb.Vertex(origin + axisU * us[i] + axisV * vs[j], normal, new Vector2(us[i], vs[j]));
            for (int j = 0; j + 1 < nv; j++)
                for (int i = 0; i + 1 < nu; i++)
                {
                    int a = first + j * nu + i;
                    BeatKit.Tri(mb, a, a + 1, a + nu + 1);
                    BeatKit.Tri(mb, a, a + nu + 1, a + nu);
                }
        }

        /// <summary>
        /// Face rectangle [u0, u1] x [v0, v1] on the plane origin + axisU u + axisV v, minus an optional hole [hu0, hu1] x
        /// [hv0, hv1] strictly inside it, split into below / above / left / right grids on the given lattices.
        /// </summary>
        static void Face(MeshBuilder mb, Vector3 origin, Vector3 axisU, Vector3 axisV, Vector3 normal,
            float u0, float u1, float v0, float v1, float uAnchor, float uStep, float vAnchor, float vStep,
            bool hole = false, float hu0 = 0f, float hu1 = 0f, float hv0 = 0f, float hv1 = 0f)
        {
            if (!hole)
            {
                Breaks(s_us, u0, u1, uAnchor, uStep);
                Breaks(s_vs, v0, v1, vAnchor, vStep);
                GridRect(mb, origin, axisU, axisV, s_us, s_vs, normal);
                return;
            }
            Breaks(s_us, u0, u1, uAnchor, uStep, hu0, hu1);
            Breaks(s_vs, v0, hv0, vAnchor, vStep);
            GridRect(mb, origin, axisU, axisV, s_us, s_vs, normal);
            Breaks(s_vs, hv1, v1, vAnchor, vStep);
            GridRect(mb, origin, axisU, axisV, s_us, s_vs, normal);
            Breaks(s_vs, hv0, hv1, vAnchor, vStep);
            Breaks(s_us, u0, hu0, uAnchor, uStep);
            GridRect(mb, origin, axisU, axisV, s_us, s_vs, normal);
            Breaks(s_us, hu1, u1, uAnchor, uStep);
            GridRect(mb, origin, axisU, axisV, s_us, s_vs, normal);
        }

        /// <summary>Flat quad from four corners (any cyclic order) with UV0 = (0..1, 0..1): u along a->d, v along a->b (GradientCard: v up).</summary>
        static void CardQuad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i0 = mb.Vertex(a, n, new Vector2(0f, 0f));
            int i1 = mb.Vertex(b, n, new Vector2(0f, 1f));
            int i2 = mb.Vertex(c, n, new Vector2(1f, 1f));
            int i3 = mb.Vertex(d, n, new Vector2(1f, 0f));
            BeatKit.Tri(mb, i0, i1, i2);
            BeatKit.Tri(mb, i0, i2, i3);
        }

        // ------------------------------------------------------------------------------------------ lathe helpers

        static void P(float r, float y) => s_prof.Add(new Vector2(r, y));

        /// <summary>Lathe of s_prof (radius, height) around the local +Y axis of a frame at 'centre' rotated so +Y = axis.</summary>
        static void LatheAt(MeshBuilder mb, Vector3 centre, Vector3 axis, int segments, bool caps)
        {
            mb.Push(centre, Quaternion.FromToRotation(Vector3.up, axis));
            MeshKit.Lathe(mb, s_prof, segments, MeshKit.HardEdgeAngleDeg, 0f, 360f, caps);
            mb.Pop();
        }

        /// <summary>Flanged disc around an axis: radius r, thickness t, centred at 'centre', with 45 degree chamfers of c.</summary>
        static void FlangeDisc(MeshBuilder mb, Vector3 centre, Vector3 axis, float r, float t, float c, int segments)
        {
            s_prof.Clear();
            P(r - c, -0.5f * t); P(r, -0.5f * t + c); P(r, 0.5f * t - c); P(r - c, 0.5f * t);
            LatheAt(mb, centre, axis, segments, true);
        }

        /// <summary>Ring of bolts on a face: 'count' hex bolts at radius r around 'centre' in the plane normal to 'normal'.</summary>
        static void BoltRing(List<BoltSpec> dst, Vector3 centre, Vector3 normal, float r, int count, float acrossFlats, float phaseDeg)
        {
            Vector3 n = normal.normalized;
            Vector3 t = Mathf.Abs(n.y) < 0.9f ? Vector3.Cross(Vector3.up, n).normalized : Vector3.Cross(Vector3.right, n).normalized;
            Vector3 b = Vector3.Cross(n, t);
            for (int i = 0; i < count; i++)
            {
                float a = (phaseDeg + 360f * i / count) * Mathf.Deg2Rad;
                dst.Add(new BoltSpec(centre + (t * Mathf.Cos(a) + b * Mathf.Sin(a)) * r, n, acrossFlats, 30f * (i % 2)));
            }
        }

        /// <summary>A bolt: head position on the host surface, outward normal, size across flats, hexagon rotation.</summary>
        struct BoltSpec
        {
            public Vector3 Position, Normal;
            public float AcrossFlats, RotationDeg;
            public BoltSpec(Vector3 p, Vector3 n, float af, float rot) { Position = p; Normal = n; AcrossFlats = af; RotationDeg = rot; }
        }

        static void EmitBolts(MeshBuilder mb, List<BoltSpec> bolts)
        {
            for (int i = 0; i < bolts.Count; i++)
            {
                BoltSpec b = bolts[i];
                MeshKit.HexBolt(mb, b.Position, b.Normal, b.AcrossFlats, b.RotationDeg);
            }
        }

        /// <summary>Instance matrix of a unit hex bolt (built at the origin, head along +Y) placed as 'b' under the bank matrix.</summary>
        static Matrix4x4 BoltMatrix(Matrix4x4 bank, BoltSpec b)
        {
            Quaternion q = Quaternion.FromToRotation(Vector3.up, b.Normal) * Quaternion.AngleAxis(b.RotationDeg, Vector3.up);
            return bank * Matrix4x4.TRS(b.Position, q, new Vector3(b.AcrossFlats, b.AcrossFlats, b.AcrossFlats));
        }

        /// <summary>Axis-aligned bounds of a box given by centre and size (AO occluders).</summary>
        static Bounds Bx(float cx, float cy, float cz, float sx, float sy, float sz) => new Bounds(new Vector3(cx, cy, cz), new Vector3(sx, sy, sz));

        static Bounds Shift(Bounds b, Vector3 d) => new Bounds(b.center + d, b.size);
    }
}
