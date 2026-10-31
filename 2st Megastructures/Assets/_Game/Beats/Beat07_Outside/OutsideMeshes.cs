using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    /// <summary>
    /// Procedural geometry of beat 7 (Part 14.5.7): deck slab, cantilever beams, rails, bollards, beacon, the outer
    /// face of the rim wall with the tunnel-mouth hole and frame, the hull clipmap and the gantries. All built with
    /// <see cref="MeshBuilder"/>; every generator finishes with <see cref="OrientToNormals"/> so the triangle winding
    /// always agrees with the intended outward normal (MeshValidation.WindingAgreement == 1).
    /// </summary>
    public static class OutsideMeshes
    {
        // ------------------------------------------------------------------ helpers
        /// <summary>Flips every triangle (from index <paramref name="firstIndex"/>) whose winding normal
        /// (cross(b - a, c - a), the MeshValidation convention) disagrees with its stored vertex normals.</summary>
        public static void OrientToNormals(MeshBuilder b, int firstIndex = 0)
        {
            var V = b.V; var N = b.N; var I = b.I;
            for (int t = firstIndex; t + 2 < I.Count; t += 3)
            {
                int a = I[t], c1 = I[t + 1], c2 = I[t + 2];
                Vector3 fn = Vector3.Cross(V[c1] - V[a], V[c2] - V[a]);
                if (Vector3.Dot(fn, N[a] + N[c1] + N[c2]) < 0f) { I[t + 1] = c2; I[t + 2] = c1; }
            }
        }

        /// <summary>Planar quad p0..p3 (in order around its border) with a flat normal pointing to the side of
        /// <paramref name="outward"/>; winding fixed afterwards by OrientToNormals.</summary>
        public static void QuadOut(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            if (n.sqrMagnitude < 1e-14f) n = Vector3.Cross(p2 - p0, p3 - p0);
            n.Normalize();
            if (Vector3.Dot(n, outward) < 0f) n = -n;
            int i0 = b.Vtx(p0, n, new Vector2(p0.x + p0.z, p0.y)), i1 = b.Vtx(p1, n, new Vector2(p1.x + p1.z, p1.y));
            int i2 = b.Vtx(p2, n, new Vector2(p2.x + p2.z, p2.y)), i3 = b.Vtx(p3, n, new Vector2(p3.x + p3.z, p3.y));
            b.Quad(i0, i1, i2, i3);
        }

        /// <summary>Convex planar polygon as a fan, normal toward <paramref name="outward"/>.</summary>
        public static void FanOut(MeshBuilder b, IList<Vector3> poly, Vector3 outward)
        {
            if (poly.Count < 3) return;
            Vector3 n = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]).normalized;
            if (Vector3.Dot(n, outward) < 0f) n = -n;
            int first = b.VertexCount;
            for (int i = 0; i < poly.Count; i++) b.Vtx(poly[i], n, new Vector2(poly[i].x + poly[i].z, poly[i].y));
            for (int i = 1; i + 1 < poly.Count; i++) b.Tri(first, first + i, first + i + 1);
        }

        /// <summary>Builds a mesh with one submesh per builder (shared vertex buffer, 32-bit indices when needed).</summary>
        public static Mesh BuildMulti(string name, params MeshBuilder[] parts)
        {
            var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var UV1 = new List<Vector2>(); var C = new List<Color>();
            var subs = new List<List<int>>();
            foreach (var p in parts)
            {
                int baseV = V.Count;
                V.AddRange(p.V); N.AddRange(p.N); UV.AddRange(p.UV); UV1.AddRange(p.UV1); C.AddRange(p.C);
                var idx = new List<int>(p.I.Count);
                foreach (int i in p.I) idx.Add(i + baseV);
                subs.Add(idx);
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = V.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.SetUVs(1, UV1); mesh.SetColors(C);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s, false);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ deck
        /// <summary>Slab x0..x1, y0..y1, z0..z1 with the top edges at x1, z0, z1 chamfered (ch) and the x0 end open
        /// (it sits on the outer-face plane). Faces that span z are split into nz strips, so the ring bend (a function
        /// of z) is applied smoothly; faces of constant z need no split.</summary>
        public static void DeckSlab(MeshBuilder b, float x0, float x1, float y0, float y1, float z0, float z1, float ch, int nz)
        {
            int i0 = b.I.Count;
            float yc = y1 - ch;
            var plain = b.Col;
            var wear = new Color(plain.r, 1f, plain.b, plain.a);   // chamfers carry edge wear (G = 1) like ChamferedBox
            // top, split in z
            for (int k = 0; k < nz; k++)
            {
                float za = Mathf.Lerp(z0 + ch, z1 - ch, (float)k / nz), zb = Mathf.Lerp(z0 + ch, z1 - ch, (float)(k + 1) / nz);
                QuadOut(b, new Vector3(x0, y1, za), new Vector3(x1 - ch, y1, za), new Vector3(x1 - ch, y1, zb), new Vector3(x0, y1, zb), Vector3.up);
            }
            b.Col = wear;
            // chamfers along x at z0 and z1 (mitred at x1, square at the open end x0)
            var cz0 = new Vector3(0f, 1f, -1f).normalized; var cz1 = new Vector3(0f, 1f, 1f).normalized;
            QuadOut(b, new Vector3(x0, y1, z0 + ch), new Vector3(x1 - ch, y1, z0 + ch), new Vector3(x1, yc, z0), new Vector3(x0, yc, z0), cz0);
            QuadOut(b, new Vector3(x0, y1, z1 - ch), new Vector3(x0, yc, z1), new Vector3(x1, yc, z1), new Vector3(x1 - ch, y1, z1 - ch), cz1);
            // chamfer along z at x1 (split in z; top edge z0+ch..z1-ch, bottom edge z0..z1)
            var cx1 = new Vector3(1f, 1f, 0f).normalized;
            for (int k = 0; k < nz; k++)
            {
                float ta = (float)k / nz, tb = (float)(k + 1) / nz;
                QuadOut(b, new Vector3(x1 - ch, y1, Mathf.Lerp(z0 + ch, z1 - ch, ta)), new Vector3(x1 - ch, y1, Mathf.Lerp(z0 + ch, z1 - ch, tb)),
                    new Vector3(x1, yc, Mathf.Lerp(z0, z1, tb)), new Vector3(x1, yc, Mathf.Lerp(z0, z1, ta)), cx1);
            }
            b.Col = plain;
            // sides of constant z
            QuadOut(b, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, yc, z0), new Vector3(x0, yc, z0), Vector3.back);
            QuadOut(b, new Vector3(x0, y0, z1), new Vector3(x0, yc, z1), new Vector3(x1, yc, z1), new Vector3(x1, y0, z1), Vector3.forward);
            // front (x1) and bottom, split in z
            for (int k = 0; k < nz; k++)
            {
                float za = Mathf.Lerp(z0, z1, (float)k / nz), zb = Mathf.Lerp(z0, z1, (float)(k + 1) / nz);
                QuadOut(b, new Vector3(x1, y0, za), new Vector3(x1, y0, zb), new Vector3(x1, yc, zb), new Vector3(x1, yc, za), Vector3.right);
                QuadOut(b, new Vector3(x0, y0, za), new Vector3(x0, y0, zb), new Vector3(x1, y0, zb), new Vector3(x1, y0, za), Vector3.down);
            }
            OrientToNormals(b, i0);
        }

        /// <summary>Cantilever beam under the deck: from the outer face (xRoot = 234, root end open against the face) to
        /// xTip, width w centred on zc, top at yTop (inside the slab), depth tapering hRoot -> hTip, bottom edges
        /// chamfered. Call OrientToNormals over its indices afterwards (QuadOut / FanOut fix only the normals).</summary>
        public static void TaperBeam(MeshBuilder b, float xRoot, float xTip, float zc, float w, float yTop, float hRoot, float hTip, float ch)
        {
            float zl = zc - 0.5f * w, zr = zc + 0.5f * w;
            float br = yTop - hRoot, bt = yTop - hTip;
            // sides (constant z), ending ch above the bottom
            QuadOut(b, new Vector3(xRoot, yTop, zl), new Vector3(xTip, yTop, zl), new Vector3(xTip, bt + ch, zl), new Vector3(xRoot, br + ch, zl), Vector3.back);
            QuadOut(b, new Vector3(xRoot, yTop, zr), new Vector3(xRoot, br + ch, zr), new Vector3(xTip, bt + ch, zr), new Vector3(xTip, yTop, zr), Vector3.forward);
            Vector3 slopeN = Vector3.Cross(new Vector3(xTip - xRoot, bt - br, 0f), Vector3.forward).normalized;
            if (slopeN.y > 0f) slopeN = -slopeN;
            // bottom chamfers and bottom
            QuadOut(b, new Vector3(xRoot, br + ch, zl), new Vector3(xTip, bt + ch, zl), new Vector3(xTip, bt, zl + ch), new Vector3(xRoot, br, zl + ch), (slopeN + Vector3.back).normalized);
            QuadOut(b, new Vector3(xRoot, br + ch, zr), new Vector3(xRoot, br, zr - ch), new Vector3(xTip, bt, zr - ch), new Vector3(xTip, bt + ch, zr), (slopeN + Vector3.forward).normalized);
            QuadOut(b, new Vector3(xRoot, br, zl + ch), new Vector3(xTip, bt, zl + ch), new Vector3(xTip, bt, zr - ch), new Vector3(xRoot, br, zr - ch), slopeN);
            // tip end (hexagon)
            FanOut(b, new[]
            {
                new Vector3(xTip, yTop, zl), new Vector3(xTip, yTop, zr), new Vector3(xTip, bt + ch, zr),
                new Vector3(xTip, bt, zr - ch), new Vector3(xTip, bt, zl + ch), new Vector3(xTip, bt + ch, zl)
            }, Vector3.right);
        }

        /// <summary>Rail path at height y along the two long sides and the front, inset from the deck edges, with
        /// rounded corners of radius rc (all rail-type paths share the corner centre, so they stay concentric); the
        /// ends are buried 5 cm behind the outer face. The front run is split every metre (bend along z).</summary>
        public static List<Vector3> RailPath(float y, float inset = OutsideLayout.RailInset, float rc = OutsideLayout.RailCornerR)
        {
            float xw = OutsideLayout.FaceX - 0.05f;
            float xf = RingConstants.PlatformX1 - inset;
            float z0 = RingConstants.PlatformZ0 + inset, z1 = RingConstants.PlatformZ1 - inset;
            var p = new List<Vector3> { new Vector3(xw, y, z0), new Vector3(0.5f * (xw + xf - rc), y, z0), new Vector3(xf - rc, y, z0) };
            for (int i = 1; i <= 6; i++)
            {
                float a = 0.5f * Mathf.PI * i / 6f;
                p.Add(new Vector3(xf - rc + rc * Mathf.Sin(a), y, z0 + rc - rc * Mathf.Cos(a)));
            }
            int n = Mathf.CeilToInt(z1 - z0 - 2f * rc);
            for (int i = 1; i < n; i++) p.Add(new Vector3(xf, y, Mathf.Lerp(z0 + rc, z1 - rc, (float)i / n)));
            p.Add(new Vector3(xf, y, z1 - rc));
            for (int i = 1; i <= 6; i++)
            {
                float a = 0.5f * Mathf.PI * i / 6f;
                p.Add(new Vector3(xf - rc + rc * Mathf.Cos(a), y, z1 - rc + rc * Mathf.Sin(a)));
            }
            p.Add(new Vector3(0.5f * (xw + xf - rc), y, z1));
            p.Add(new Vector3(xw, y, z1));
            return p;
        }

        /// <summary>Post positions (x, z): 25 per long side (x 234.5..305.5, pitch 2.96 m) and 5 on the front (pitch
        /// 2.75 m, the two corner posts shared). The corner posts stand on the midpoint of the rounded rail corner, so
        /// they sit exactly under the rail.</summary>
        public static List<Vector2> PostPositions()
        {
            var r = new List<Vector2>();
            float xa = OutsideLayout.FaceX + OutsideLayout.RailInset, xf = RingConstants.PlatformX1 - OutsideLayout.RailInset;
            float z0 = RingConstants.PlatformZ0 + OutsideLayout.RailInset, z1 = RingConstants.PlatformZ1 - OutsideLayout.RailInset;
            float rc = OutsideLayout.RailCornerR, k45 = rc * (1f - Mathf.Sqrt(0.5f));
            int nl = OutsideLayout.PostsLongSide, nf = OutsideLayout.PostsFront;
            for (int k = 0; k < nl - 1; k++) { float x = Mathf.Lerp(xa, xf, (float)k / (nl - 1)); r.Add(new Vector2(x, z0)); r.Add(new Vector2(x, z1)); }
            r.Add(new Vector2(xf - k45, z0 + k45));
            r.Add(new Vector2(xf - k45, z1 - k45));
            for (int k = 1; k < nf - 1; k++) r.Add(new Vector2(xf, Mathf.Lerp(z0, z1, (float)k / (nf - 1))));
            return r;
        }

        /// <summary>One rail post (lathe, 0.08 m, flanged foot 1 cm into the deck) from the deck up to the hand-rail axis.</summary>
        public static void Post(MeshBuilder b, Vector2 xz)
        {
            float r = OutsideLayout.PostRadius, rf = OutsideLayout.PostFlangeR;
            var prof = new List<Vector2>
            {
                new Vector2(rf, -OutsideLayout.PostEmbed), new Vector2(rf, 0.012f), new Vector2(r + 0.004f, 0.026f),
                new Vector2(r, 0.034f), new Vector2(r, OutsideLayout.TopRailY)
            };
            b.Lathe(new Vector3(xz.x, OutsideLayout.DeckTopY, xz.y), prof, 10);
        }

        /// <summary>Kick plates: a 0.02 x 0.13 m plate (5 mm top chamfers, foot 1 cm into the deck) swept along the
        /// rail line 7 cm outside the posts, concentric round the rail corners, both ends buried in the outer face.</summary>
        public static void KickPlates(MeshBuilder b)
        {
            float t = OutsideLayout.KickHalfT, h = OutsideLayout.KickHeight, c = OutsideLayout.KickChamfer;
            var sec = new List<Vector2>
            {
                new Vector2(t, -0.01f), new Vector2(t, h - c), new Vector2(t - c, h),
                new Vector2(-(t - c), h), new Vector2(-t, h - c), new Vector2(-t, -0.01f)
            };
            float rc = OutsideLayout.RailCornerR + (OutsideLayout.RailInset - OutsideLayout.KickInset);
            var path = RailPath(OutsideLayout.DeckTopY, OutsideLayout.KickInset, rc);
            int i0 = b.I.Count;
            b.Loft(sec, path, Vector3.up, false, true);
            OrientToNormals(b, i0);
        }

        /// <summary>Lamp bollard body (lathe, ceramic) with a recessed slot for the emissive band.</summary>
        public static void BollardBody(MeshBuilder b, Vector3 c)
        {
            var prof = new List<Vector2>
            {
                new Vector2(0.115f, -0.01f), new Vector2(0.115f, 0.035f), new Vector2(0.100f, 0.050f), new Vector2(0.082f, 0.070f),
                new Vector2(0.075f, 0.40f), new Vector2(0.072f, 0.69f), new Vector2(0.086f, 0.705f), new Vector2(0.086f, 0.715f),
                new Vector2(0.066f, 0.722f)
            };
            b.Lathe(c, prof, 24);
            var cap = new List<Vector2>
            {
                new Vector2(0.066f, 0.808f), new Vector2(0.090f, 0.815f), new Vector2(0.092f, 0.845f), new Vector2(0.075f, 0.868f),
                new Vector2(0.040f, 0.878f), new Vector2(0.0005f, 0.880f)
            };
            b.Lathe(c, cap, 24);
        }

        /// <summary>The emissive band inside the bollard slot (0.72..0.81 m).</summary>
        public static void BollardLamp(MeshBuilder b, Vector3 c)
        {
            b.Lathe(c, new List<Vector2> { new Vector2(0.068f, 0.716f), new Vector2(0.070f, 0.765f), new Vector2(0.068f, 0.814f) }, 24);
        }

        /// <summary>Beacon: base disc r 1.0, pylon 0.35 -> 0.12 with a gentle concave taper, collar; the lens sits on top
        /// (top of the lens at 1.30 m). Returns the pylon builder; the lens goes into <paramref name="lens"/>.</summary>
        public static MeshBuilder Beacon(Vector3 c, MeshBuilder lens)
        {
            var b = new MeshBuilder();
            var prof = new List<Vector2>
            {
                new Vector2(OutsideLayout.BeaconDiscR, -0.005f), new Vector2(OutsideLayout.BeaconDiscR, 0.028f),
                new Vector2(OutsideLayout.BeaconDiscR - 0.016f, 0.044f), new Vector2(OutsideLayout.BeaconBaseR + 0.03f, 0.046f),
                new Vector2(OutsideLayout.BeaconBaseR + 0.005f, 0.052f)
            };
            float y0 = 0.06f, y1 = OutsideLayout.BeaconLensY - 0.07f;     // 1.16 m
            const int steps = 14;
            for (int k = 0; k <= steps; k++)
            {
                float u = (float)k / steps;
                float r = OutsideLayout.BeaconTopR + (OutsideLayout.BeaconBaseR - OutsideLayout.BeaconTopR) * Mathf.Pow(1f - u, 1.6f);
                prof.Add(new Vector2(r, Mathf.Lerp(y0, y1, u)));
            }
            prof.Add(new Vector2(OutsideLayout.BeaconTopR - 0.006f, y1 + 0.009f));
            prof.Add(new Vector2(0.095f, y1 + 0.014f));
            prof.Add(new Vector2(0.060f, y1 + 0.016f));
            prof.Add(new Vector2(0.040f, y1 + 0.020f));
            prof.Add(new Vector2(0.0005f, y1 + 0.022f));
            b.Lathe(c, prof, 32);
            lens.Sphere(c + Vector3.up * OutsideLayout.BeaconLensY, OutsideLayout.BeaconLensR, 24, 12);
            return b;
        }

        // ------------------------------------------------------------------ outer face
        /// <summary>Distance from the polar centre (MouthCentreY, z 660) to the tunnel-profile boundary along (dz, dy)
        /// (unit): rectangle 2.6 x 1.7 from the floor y 0 plus the semicircular crown r 1.3 centred at y 1.7.</summary>
        static float HoleT(float dz, float dy)
        {
            float halfW = 0.5f * RingConstants.DoorW, straight = RingConstants.DoorStraight, cr = RingConstants.DoorCrownR;
            float yc = OutsideLayout.MouthCentreY;
            float tSide = dz > 1e-6f ? halfW / dz : (dz < -1e-6f ? -halfW / dz : float.MaxValue);
            float tBottom = dy < -1e-6f ? yc / -dy : float.MaxValue;
            float t = Mathf.Min(tSide, tBottom);
            if (dy > 0f && yc + t * dy > straight)
            {
                float oy = yc - straight;
                t = -dy * oy + Mathf.Sqrt(Mathf.Max(dy * dy * oy * oy - oy * oy + cr * cr, 0f));
            }
            return t;
        }

        static float NormDeg(float a) { a %= 360f; return a < 0f ? a + 360f : a; }

        /// <summary>The plane x = 234 facing +X, +-60 km around the mouth, with the tunnel-profile hole: a structured
        /// polar mesh (angles x concentric squares) so there is no T-junction anywhere.</summary>
        public static void OuterFace(MeshBuilder b)
        {
            float x = OutsideLayout.FaceX, zc = 660f, yc = OutsideLayout.MouthCentreY;
            var ang = new List<float>();
            for (int i = 0; i < 90; i++) ang.Add(4f * i);
            float hw = 0.5f * RingConstants.DoorW, st = RingConstants.DoorStraight;
            ang.Add(NormDeg(Mathf.Atan2(-yc, -hw) * Mathf.Rad2Deg));
            ang.Add(NormDeg(Mathf.Atan2(-yc, hw) * Mathf.Rad2Deg));
            ang.Add(NormDeg(Mathf.Atan2(st - yc, -hw) * Mathf.Rad2Deg));
            ang.Add(NormDeg(Mathf.Atan2(st - yc, hw) * Mathf.Rad2Deg));
            ang.Add(45f); ang.Add(135f); ang.Add(225f); ang.Add(315f);
            ang.Sort();
            var A = new List<float>();
            foreach (float a in ang)
            {
                if (A.Count > 0 && a - A[A.Count - 1] < 0.4f)
                {
                    // keep exact corner / diagonal angles over the uniform ones
                    if (a % 4f != 0f) A[A.Count - 1] = a;
                    continue;
                }
                A.Add(a);
            }
            if (A.Count > 1 && 360f - A[A.Count - 1] + A[0] < 0.4f) A.RemoveAt(A.Count - 1);

            var sq = new List<float>();
            for (float s = 4f; s < OutsideLayout.FaceExtent; s *= 2f) sq.Add(s);
            sq.Add(OutsideLayout.FaceExtent);
            int nl = sq.Count + 1, na = A.Count;
            int first = b.VertexCount;
            for (int i = 0; i < na; i++)
            {
                float r = A[i] * Mathf.Deg2Rad;
                float dz = Mathf.Cos(r), dy = Mathf.Sin(r);
                float m = Mathf.Max(Mathf.Abs(dz), Mathf.Abs(dy));
                float th = HoleT(dz, dy);
                b.Vtx(new Vector3(x, yc + th * dy, zc + th * dz), Vector3.right, new Vector2(zc + th * dz, yc + th * dy));
                for (int l = 0; l < sq.Count; l++)
                {
                    float t = sq[l] / m;
                    b.Vtx(new Vector3(x, yc + t * dy, zc + t * dz), Vector3.right, new Vector2(zc + t * dz, yc + t * dy));
                }
            }
            int i0 = b.I.Count;
            for (int i = 0; i < na; i++)
            {
                int i1 = (i + 1) % na;
                for (int l = 0; l < nl - 1; l++)
                    b.Quad(first + i * nl + l, first + i1 * nl + l, first + i1 * nl + l + 1, first + i * nl + l + 1);
            }
            OrientToNormals(b, i0);
        }

        /// <summary>PaleGold mouth frame (vertex colour B = 1 selects the trim in RingOuterFace): a U-profile along the
        /// tunnel profile (legs from y -0.02 inside the sill, crown r 1.3), proud 0.06 m, 0.14 m wide, with a 0.20 m
        /// reveal reaching into the tunnel 1 cm inside its surface (covers any bend offset at the junction).</summary>
        public static void MouthFrame(MeshBuilder b)
        {
            float zc = 660f, hw = 0.5f * RingConstants.DoorW, st = RingConstants.DoorStraight, cr = RingConstants.DoorCrownR;
            float x = OutsideLayout.FaceX;
            var path = new List<Vector3> { new Vector3(x, -0.02f, zc - hw), new Vector3(x, 0.85f, zc - hw), new Vector3(x, st, zc - hw) };
            const int crownSeg = 28;
            for (int i = 1; i < crownSeg; i++)
            {
                float a = Mathf.PI * (1f - (float)i / crownSeg);
                path.Add(new Vector3(x, st + cr * Mathf.Sin(a), zc + cr * Mathf.Cos(a)));
            }
            path.Add(new Vector3(x, st, zc + hw)); path.Add(new Vector3(x, 0.85f, zc + hw)); path.Add(new Vector3(x, -0.02f, zc + hw));
            float w = OutsideLayout.FrameWidth, pr = OutsideLayout.FrameProud, rv = OutsideLayout.FrameReveal, ins = OutsideLayout.FrameInset;
            // section (inward offset, +X offset), counter-clockwise: reveal -> chamfer -> front -> chamfer -> outer side
            var sec = new List<Vector2>
            {
                new Vector2(ins, -rv), new Vector2(ins, pr - 0.015f), new Vector2(ins - 0.015f, pr),
                new Vector2(-(w - 0.015f), pr), new Vector2(-w, pr - 0.015f), new Vector2(-w, -0.03f)
            };
            var old = b.Col;
            b.Col = new Color(1f, 0f, 1f, 0f);
            int i0 = b.I.Count;
            b.Loft(sec, path, Vector3.right, false, false);
            OrientToNormals(b, i0);
            b.Col = old;
        }

        // ------------------------------------------------------------------ hull
        static bool HullVertexUsed(int L, int i, int j, int n)
        {
            if (L == 0) return true;
            int a = n / 4, c = 3 * n / 4;
            return !(i > a && i < c && j > a && j < c);
        }

        /// <summary>Static nested-grid clipmap centred under (300, 660): 7 levels of 128 x 128 quads, spacing 2..128 m,
        /// half-extent 8 192 m (levels >= 1 skip the 64 x 64 quads covered by the finer level). Flat grid (mesh y 0)
        /// displaced by RingHull's vertex hook (y = height(x, z) + mesh y). Odd outer-boundary vertices carry a stitch
        /// vector in uv1 (their height is the mean of their even neighbours, i.e. exactly on the coarse edge: no
        /// T-junction crack), and every level meeting a coarser one gets a 3 m inward-facing skirt (mesh y -3, so the
        /// CPU mesh has no degenerate triangle). Colour: R AO 1, B 1 = displaced vertex.</summary>
        public static Mesh HullClipmap(out int triangles)
        {
            var b = new MeshBuilder();
            float cx = RingConstants.HullCentre.x, cz = RingConstants.HullCentre.y;
            int n = OutsideLayout.HullQuads;
            var idx = new int[n + 1, n + 1];
            var grid = new Color(1f, 0f, 1f, 0f);
            Vector3 drop = Vector3.down * OutsideLayout.HullSkirt;
            for (int L = 0; L < OutsideLayout.HullLevels; L++)
            {
                float sp = OutsideLayout.HullSpacing0 * (1 << L);
                float H = 0.5f * n * sp;
                bool coarser = L < OutsideLayout.HullLevels - 1;
                int a = n / 4, c = 3 * n / 4;
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        idx[i, j] = -1;
                        if (!HullVertexUsed(L, i, j, n)) continue;
                        int v = b.Vtx(new Vector3(cx - H + i * sp, 0f, cz - H + j * sp), Vector3.up, Vector2.zero, grid);
                        b.UV1[v] = coarser ? Stitch(i, j, n, sp) : Vector2.zero;
                        idx[i, j] = v;
                    }
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        if (L > 0 && i >= a && i < c && j >= a && j < c) continue;
                        b.Quad(idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j]);
                    }
                if (!coarser) continue;
                // skirts: 4 sides of the outer boundary, facing the centre
                for (int side = 0; side < 4; side++)
                {
                    Vector3 inward = side == 0 ? Vector3.right : side == 1 ? Vector3.left : side == 2 ? Vector3.forward : Vector3.back;
                    for (int k = 0; k < n; k++)
                    {
                        int ia, ja, ib, jb;
                        if (side == 0) { ia = 0; ja = k; ib = 0; jb = k + 1; }
                        else if (side == 1) { ia = n; ja = k; ib = n; jb = k + 1; }
                        else if (side == 2) { ia = k; ja = 0; ib = k + 1; jb = 0; }
                        else { ia = k; ja = n; ib = k + 1; jb = n; }
                        Vector3 pa = b.V[idx[ia, ja]], pb = b.V[idx[ib, jb]];
                        Vector2 sa = b.UV1[idx[ia, ja]], sb = b.UV1[idx[ib, jb]];
                        int t0 = b.Vtx(pa, inward, Vector2.zero, grid); b.UV1[t0] = sa;
                        int t1 = b.Vtx(pb, inward, Vector2.zero, grid); b.UV1[t1] = sb;
                        int b1 = b.Vtx(pb + drop, inward, Vector2.zero, grid); b.UV1[b1] = sb;
                        int b0 = b.Vtx(pa + drop, inward, Vector2.zero, grid); b.UV1[b0] = sa;
                        b.Quad(t0, t1, b1, b0);
                    }
                }
            }
            // grid quads face up, skirts face the centre
            OrientToNormals(b);
            triangles = b.TriangleCount;
            var mesh = b.Build("Hull.Clipmap", false);
            // The bounds must hold both the CPU mesh (flat grid y 0, skirt bottoms y -3: what Meshes_BoundsContainVertices
            // and any vertex measurement see) and the GPU-displaced cap (what is drawn and culled). The union reaches
            // y +1; that costs nothing for culling since this content is never bent and lies under the platform anyway.
            var bounds = HullCapBounds();
            bounds.Encapsulate(new Vector3(cx, 0f, cz));
            bounds.Expand(2f);                                       // 1 m margin on every side
            mesh.bounds = bounds;
            return mesh;
        }

        /// <summary>World AABB of the displaced cap as RingHull draws it: x, z +-8 192 m about (300, 660); y from the
        /// corner drop of the cap (about 2 237 m) minus the 3 m skirt up to the ridge crests (top -200 + 14 m).
        /// The CPU clipmap mesh is flat (y -3..0), so this is the box to measure the hull by (registry "Hull.Cap").</summary>
        public static Bounds HullCapBounds()
        {
            float cx = RingConstants.HullCentre.x, cz = RingConstants.HullCentre.y;
            float ext = HullHalfExtent;
            float maxDrop = OutsideLayout.CapDrop(cx + ext, cz + ext, out _);
            float yMin = OutsideLayout.HullTopY - maxDrop - OutsideLayout.HullSkirt;
            float yMax = OutsideLayout.HullTopY + OutsideLayout.RidgeHeight;
            var b = new Bounds();
            b.SetMinMax(new Vector3(cx - ext, yMin, cz - ext), new Vector3(cx + ext, yMax, cz + ext));
            return b;
        }

        /// <summary>Half-extent of the coarsest clipmap level: 0.5 x 128 quads x 128 m = 8 192 m.</summary>
        public static float HullHalfExtent => 0.5f * OutsideLayout.HullQuads * OutsideLayout.HullSpacing0 * (1 << (OutsideLayout.HullLevels - 1));

        static Vector2 Stitch(int i, int j, int n, float sp)
        {
            if ((i == 0 || i == n) && (j & 1) == 1) return new Vector2(0f, sp);
            if ((j == 0 || j == n) && (i & 1) == 1) return new Vector2(sp, 0f);
            return Vector2.zero;
        }

        /// <summary>The 24 gantries (40 x 3 x 3 m beam on four 0.6 m legs 6 m tall, end ties), each in its cap-normal
        /// frame at the C# twin of HullGantryFrame. Vertex colour B = 0: drawn rigid by RingHull.</summary>
        public static Mesh Gantries(out int triangles)
        {
            var b = new MeshBuilder();
            float hl = OutsideLayout.GantryHalfLen, hw = OutsideLayout.GantryHalfW;
            float y0 = OutsideLayout.GantryBeamY0, y1 = OutsideLayout.GantryBeamY1;
            float lx = OutsideLayout.GantryLegX, lz = OutsideLayout.GantryLegZ, lh = OutsideLayout.GantryLegHalf;
            for (int k = 0; k < OutsideLayout.GantryCols; k++)
                for (int j = 0; j < OutsideLayout.GantryRows; j++)
                {
                    OutsideLayout.GantryFrame(k, j, out Vector3 G, out Vector3 ex, out Vector3 ey, out Vector3 ez);
                    b.M = new Matrix4x4(new Vector4(ex.x, ex.y, ex.z, 0f), new Vector4(ey.x, ey.y, ey.z, 0f), new Vector4(ez.x, ez.y, ez.z, 0f), new Vector4(G.x, G.y, G.z, 1f));
                    b.ChamferedBox(new Vector3(0f, 0.5f * (y0 + y1), 0f), new Vector3(2f * hw, y1 - y0, 2f * hl), 0.12f);
                    for (int s = 0; s < 4; s++)
                    {
                        float px = (s & 1) != 0 ? lx : -lx, pz = (s & 2) != 0 ? lz : -lz;
                        float yb = -2f, yt = y0 + 0.5f;                       // buried 2 m, 0.5 m into the beam
                        b.ChamferedBox(new Vector3(px, 0.5f * (yb + yt), pz), new Vector3(2f * lh, yt - yb, 2f * lh), 0.05f);
                    }
                    for (int s = 0; s < 2; s++)
                    {
                        float pz = s == 0 ? -lz : lz;
                        b.ChamferedBox(new Vector3(0f, 1.6f, pz), new Vector3(2f * lx, 0.4f, 0.36f), 0.04f);
                        b.ChamferedBox(new Vector3(0f, 4.2f, pz), new Vector3(2f * lx, 0.3f, 0.30f), 0.04f);
                    }
                }
            b.M = Matrix4x4.identity;
            OrientToNormals(b);
            triangles = b.TriangleCount;
            return b.Build("Hull.Gantries", true);
        }
    }
}
