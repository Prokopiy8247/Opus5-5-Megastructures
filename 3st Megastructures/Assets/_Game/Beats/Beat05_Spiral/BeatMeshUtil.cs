// BeatMeshUtil: geometry helpers shared by the beat 5-7 builders (MASTER_PROMPT 8.7, research R3 3.5):
// double-precision conversions, winding-checked quads, round tubes swept with rotation-minimising frames
// (double reflection, Wang et al. 2008) and convex polyhedra built from planes (the chamfered octahedron of
// the beacon). Build-time only (allocations are fine here; nothing in this file runs per frame).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    public static class BeatMeshUtil
    {
        public static Vector3 V(D3 d) => new Vector3((float)d.x, (float)d.y, (float)d.z);

        /// <summary>Vertex colour of the MeshKit convention: R = baked AO, G = 1 (no height shade), B = 0 (no sway), A = 1.</summary>
        public static Color Ao(double ao) => new Color((float)ao, 1f, 0f, 1f);

        public static double Wrap2Pi(double a)
        {
            double t = 2.0 * Math.PI;
            a -= Math.Floor(a / t) * t;
            return a >= t ? 0.0 : a;
        }

        /// <summary>Signed angle difference a - b wrapped to (-pi, pi].</summary>
        public static double AngDiff(double a, double b)
        {
            double d = Wrap2Pi(a - b);
            return d > Math.PI ? d - 2.0 * Math.PI : d;
        }

        /// <summary>Azimuth of a horizontal direction, measured from +Z toward +X (the lathe convention of MeshKit).</summary>
        public static double Azimuth(double x, double z) => Math.Atan2(x, z);

        /// <summary>Adds the quad (a, b, c, d) with the winding whose front face points along refNormal.</summary>
        public static void QuadFacing(MeshBuilder mb, int a, int b, int c, int d, Vector3 refNormal)
        {
            Vector3 pa = mb.Positions[a];
            Vector3 fn = Vector3.Cross(mb.Positions[b] - pa, mb.Positions[c] - pa) + Vector3.Cross(mb.Positions[c] - pa, mb.Positions[d] - pa);
            if (Vector3.Dot(fn, refNormal) >= 0f) mb.Quad(a, b, c, d); else mb.Quad(a, d, c, b);
        }

        /// <summary>Adds the triangle (a, b, c) with the winding whose front face points along refNormal.</summary>
        public static void TriFacing(MeshBuilder mb, int a, int b, int c, Vector3 refNormal)
        {
            Vector3 pa = mb.Positions[a];
            Vector3 fn = Vector3.Cross(mb.Positions[b] - pa, mb.Positions[c] - pa);
            if (Vector3.Dot(fn, refNormal) >= 0f) mb.Tri(a, b, c); else mb.Tri(a, c, b);
        }

        // ------------------------------------------------------------------ round tube (handrail)
        /// <summary>
        /// Sweeps a regular n-gon tube of the given radius along a polyline (double precision). Frames are
        /// rotation-minimising (double reflection); the tangent at interior points is the bisector of the two
        /// segment directions, so every ring lies in the miter plane and the tube has no wedge gaps at bends.
        /// Each ring has sides + 1 vertices (the seam column duplicated with an identical position and normal).
        /// Normals are exactly radial, so a shader that pushes vertices along the normal (ThreadLine _LINE_TUBE)
        /// scales the tube about its centre line without cracks. The optional domes are hemispheres whose normals
        /// are radial from the end point, for the same reason. tangent.xyz = tube axis, uv1.x = radius (m).
        /// </summary>
        public static void Tube(MeshBuilder mb, IList<D3> c, double radius, int sides, Color colour, bool domeStart, bool domeEnd)
        {
            int n = c.Count;
            if (n < 2 || sides < 3) return;
            var t = new D3[n];
            var seg = new D3[n - 1];
            for (int i = 0; i < n - 1; i++) seg[i] = (c[i + 1] - c[i]).Normalized;
            for (int i = 0; i < n; i++)
            {
                if (i == 0) t[i] = seg[0];
                else if (i == n - 1) t[i] = seg[n - 2];
                else
                {
                    D3 b = seg[i - 1] + seg[i];
                    t[i] = b.Length > 1e-9 ? b.Normalized : seg[i];
                }
            }
            // rotation-minimising frames (Wang et al. 2008, double reflection), initial reference horizontal
            var r = new D3[n];
            D3 up = new D3(0, 1, 0);
            D3 r0 = D3.Cross(up, t[0]);
            if (r0.Length < 1e-6) r0 = D3.Cross(new D3(0, 0, 1), t[0]);
            r[0] = r0.Normalized;
            for (int i = 0; i < n - 1; i++)
            {
                D3 v1 = c[i + 1] - c[i];
                double c1 = D3.Dot(v1, v1);
                D3 rL = r[i], tL = t[i];
                if (c1 > 1e-18)
                {
                    rL = r[i] - v1 * (2.0 / c1 * D3.Dot(v1, r[i]));
                    tL = t[i] - v1 * (2.0 / c1 * D3.Dot(v1, t[i]));
                }
                D3 v2 = t[i + 1] - tL;
                double c2 = D3.Dot(v2, v2);
                D3 ri = c2 > 1e-18 ? rL - v2 * (2.0 / c2 * D3.Dot(v2, rL)) : rL;
                ri = ri - t[i + 1] * D3.Dot(ri, t[i + 1]);
                r[i + 1] = ri.Normalized;
            }

            float circ = (float)(2.0 * Math.PI * radius);
            var uv1 = new Vector4((float)radius, 0f, 0f, 0f);
            double u = 0;
            int ringStride = sides + 1;
            int firstRing = mb.VertexCount;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) u += (c[i] - c[i - 1]).Length;
                D3 b = D3.Cross(t[i], r[i]).Normalized;
                var tan = new Vector4((float)t[i].x, (float)t[i].y, (float)t[i].z, 1f);
                for (int j = 0; j <= sides; j++)
                {
                    int jj = j % sides;
                    double phi = 2.0 * Math.PI * jj / sides;
                    D3 dir = r[i] * Math.Cos(phi) + b * Math.Sin(phi);
                    mb.Add(V(c[i] + dir * radius), V(dir), new Vector2((float)u, circ * j / sides), colour, tan, uv1);
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = firstRing + i * ringStride + j, a1 = a0 + 1, b0 = a0 + ringStride, b1 = b0 + 1;
                    Vector3 refN = mb.Normals[a0] + mb.Normals[a1] + mb.Normals[b0] + mb.Normals[b1];
                    QuadFacing(mb, a0, a1, b1, b0, refN);
                }
            if (domeStart) Dome(mb, c[0], t[0] * -1.0, r[0], D3.Cross(t[0], r[0]).Normalized, radius, sides, colour, firstRing, uv1);
            if (domeEnd) Dome(mb, c[n - 1], t[n - 1], r[n - 1], D3.Cross(t[n - 1], r[n - 1]).Normalized, radius, sides, colour, firstRing + (n - 1) * ringStride, uv1);
        }

        /// <summary>Hemispherical end cap on an existing ring (ringStart .. ringStart + sides), pointing along axisOut.</summary>
        static void Dome(MeshBuilder mb, D3 centre, D3 axisOut, D3 r, D3 b, double radius, int sides, Color colour, int ringStart, Vector4 uv1)
        {
            const int Steps = 2;   // rings at 45 degrees, then the tip
            int prev = ringStart;
            var tan = new Vector4((float)axisOut.x, (float)axisOut.y, (float)axisOut.z, 1f);
            for (int k = 1; k < Steps; k++)
            {
                double beta = 0.5 * Math.PI * k / Steps;
                int cur = mb.VertexCount;
                for (int j = 0; j <= sides; j++)
                {
                    double phi = 2.0 * Math.PI * (j % sides) / sides;
                    D3 dir = (r * Math.Cos(phi) + b * Math.Sin(phi)) * Math.Cos(beta) + axisOut * Math.Sin(beta);
                    mb.Add(V(centre + dir * radius), V(dir), new Vector2((float)beta, j), colour, tan, uv1);
                }
                for (int j = 0; j < sides; j++)
                {
                    Vector3 refN = mb.Normals[prev + j] + mb.Normals[prev + j + 1] + mb.Normals[cur + j] + mb.Normals[cur + j + 1];
                    QuadFacing(mb, prev + j, prev + j + 1, cur + j + 1, cur + j, refN);
                }
                prev = cur;
            }
            int tip = mb.Add(V(centre + axisOut * radius), V(axisOut), new Vector2((float)(0.5 * Math.PI), 0f), colour, tan, uv1);
            for (int j = 0; j < sides; j++)
            {
                Vector3 refN = mb.Normals[prev + j] + mb.Normals[prev + j + 1] + mb.Normals[tip];
                TriFacing(mb, prev + j, prev + j + 1, tip, refN);
            }
        }

        // ------------------------------------------------------------------ convex polyhedra
        /// <summary>
        /// Adds the convex polyhedron { p : dot(n_i, p) &lt;= w_i } (planes as xyz = unit outward normal, w = offset),
        /// one flat polygon per plane (Sutherland-Hodgman clipping of a large square by all other planes).
        /// </summary>
        public static void ConvexFromPlanes(MeshBuilder mb, IList<D3> normals, IList<double> offsets, Color colour, double extent)
            => ConvexFromPlanes(mb, normals, offsets, colour, extent, new D3(0, 0, 0));

        /// <summary>Convex polyhedron from planes n.x = offset; each face's clip square is seeded at the projection of
        /// `centre` onto its plane (seeding at the world-origin foot point lost every face of objects far from the origin).</summary>
        public static void ConvexFromPlanes(MeshBuilder mb, IList<D3> normals, IList<double> offsets, Color colour, double extent, D3 centre)
        {
            int np = normals.Count;
            var poly = new List<D3>(32);
            var next = new List<D3>(32);
            var pts = new List<Vector3>(32);
            for (int i = 0; i < np; i++)
            {
                D3 n = normals[i];
                D3 a = Math.Abs(n.y) < 0.9 ? new D3(0, 1, 0) : new D3(1, 0, 0);
                D3 u = D3.Cross(a, n).Normalized, v = D3.Cross(n, u).Normalized;
                D3 o = centre + n * (offsets[i] - D3.Dot(n, centre));
                poly.Clear();
                poly.Add(o + u * extent + v * extent); poly.Add(o - u * extent + v * extent);
                poly.Add(o - u * extent - v * extent); poly.Add(o + u * extent - v * extent);
                for (int j = 0; j < np && poly.Count >= 3; j++)
                {
                    if (j == i) continue;
                    D3 nj = normals[j];
                    double wj = offsets[j];
                    next.Clear();
                    for (int k = 0; k < poly.Count; k++)
                    {
                        D3 p = poly[k], q = poly[(k + 1) % poly.Count];
                        double dp = D3.Dot(nj, p) - wj, dq = D3.Dot(nj, q) - wj;
                        if (dp <= 0) next.Add(p);
                        if ((dp < 0 && dq > 0) || (dp > 0 && dq < 0)) next.Add(p + (q - p) * (dp / (dp - dq)));
                    }
                    poly.Clear();
                    for (int k = 0; k < next.Count; k++)
                        if (poly.Count == 0 || (next[k] - poly[poly.Count - 1]).Length > 1e-7) poly.Add(next[k]);
                    if (poly.Count > 1 && (poly[0] - poly[poly.Count - 1]).Length <= 1e-7) poly.RemoveAt(poly.Count - 1);
                }
                if (poly.Count < 3) continue;
                double area = 0;
                for (int k = 1; k + 1 < poly.Count; k++) area += D3.Cross(poly[k] - poly[0], poly[k + 1] - poly[0]).Length * 0.5;
                if (area < 1e-8) continue;
                pts.Clear();
                for (int k = 0; k < poly.Count; k++) pts.Add(V(poly[k]));
                MeshKit.ConvexPolygon(mb, pts, V(n), colour);
            }
        }

        /// <summary>
        /// Planes of an octahedron (tips at +-halfHeight on Y, equator vertices at +-halfWidth on X and Z) with its 6
        /// tips and 12 edges chamfered by `chamfer`, rotated by yaw (rad) about Y and translated to centre.
        /// </summary>
        public static void ChamferedOctahedronPlanes(D3 centre, double halfHeight, double halfWidth, double chamfer, double yaw,
                                                     List<D3> normals, List<double> offsets)
        {
            normals.Clear(); offsets.Clear();
            double[] L = { halfWidth, halfHeight, halfWidth };
            // 8 faces: |x|/w + |y|/h + |z|/w <= 1
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        D3 f = new D3(sx / L[0], sy / L[1], sz / L[2]);
                        double len = f.Length;
                        normals.Add(f * (1.0 / len)); offsets.Add(1.0 / len);
                    }
            // 6 tips
            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    D3 n = Axis(axis) * s;
                    normals.Add(n); offsets.Add(L[axis] - chamfer);
                }
            // 12 edges: between tips on two different axes
            for (int a1 = 0; a1 < 3; a1++)
                for (int a2 = a1 + 1; a2 < 3; a2++)
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            D3 n = (Axis(a1) * (s1 / L[a1]) + Axis(a2) * (s2 / L[a2])).Normalized;
                            D3 tipA = Axis(a1) * (s1 * L[a1]);
                            normals.Add(n); offsets.Add(D3.Dot(n, tipA) - chamfer);
                        }
            // rotate and translate
            double cy = Math.Cos(yaw), sy2 = Math.Sin(yaw);
            for (int i = 0; i < normals.Count; i++)
            {
                D3 n = normals[i];
                D3 rn = new D3(n.x * cy + n.z * sy2, n.y, -n.x * sy2 + n.z * cy);
                normals[i] = rn;
                offsets[i] = offsets[i] + D3.Dot(rn, centre);
            }
        }

        static D3 Axis(int a) => a == 0 ? new D3(1, 0, 0) : (a == 1 ? new D3(0, 1, 0) : new D3(0, 0, 1));
    }
}
