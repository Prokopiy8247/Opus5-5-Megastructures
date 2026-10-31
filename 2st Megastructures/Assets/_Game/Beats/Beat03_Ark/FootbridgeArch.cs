using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Footbridge arch (MASTER_PROMPT 14.5.3, cut order 3b, set FarValley): a low stone barrel vault over the path at
    /// s 396-404. Clear opening 4.2 m wide, 1.1 m straight walls + a semicircular crown r 2.1 (3.2 m), 8.0 m long;
    /// stone courses 0.6 x 0.3 in running bond (Beat34Mat.BridgeStone, triplanar, filtered) on the intrados, the headwalls
    /// and the wing walls; 13 voussoirs per portal with real 8 mm joints (pale ashlar), PaleGold keystone; a 14 m wide
    /// LeveeGreen turf mound (x -7..7) carrying the cross footpath (PathDist segment 11, z 400). The stone headwall is
    /// only the portal (|x| <= 3.5); beyond it the turf runs on past the face planes in 35-degree end slopes held by
    /// wing walls with sloped copings, so from the path (KS04, 32 m) the mound reads as green turf around a dark stone
    /// portal, not as a stone slab. Colliders are PathGuide's (lane 3.5 m, soffit at y 3.2).
    /// </summary>
    public static class FootbridgeArch
    {
        const float Z0 = RingConstants.FootbridgeS0;                     // 396 (s = z on this leg)
        const float Z1 = RingConstants.FootbridgeS1;                     // 404
        const float ZC = 0.5f * (Z0 + Z1);                               // 400 = the cross path line
        const float HalfW = 0.5f * RingConstants.FootbridgeW;            // 2.1
        const float CrownR = HalfW;                                      // semicircular crown r 2.1
        const float Spring = RingConstants.FootbridgeH - CrownR;         // 1.1 m straight walls
        const float MoundHalf = 7f;                                      // mound x -7..7 (14.12)
        const float HumpTop = 3.95f;                                     // turf top over the crown (0.3 m over the ring)
        const float HumpSink = 0.08f;                                    // the hump meets the ground at a clean angle (x ~6.4)
        const float Bury = 0.2f;                                         // walls run below the ground (no gaps)
        const int ArchSeg = 26;                                          // 13 voussoirs x 2 segments
        const int Voussoirs = 13;
        const float RingOuter = 2.6f, KeyOuter = 2.82f;
        const float StoneProud = 0.04f, KeyProud = 0.07f, Joint = 0.008f, Chamfer = 0.012f;
        const float Parapet = 0.15f, CopingH = 0.16f, CopingProud = 0.06f, CopingBack = 0.5f;
        const float PathHalf = 1.2f, PathLift = 0.025f, PathEnd = 90f, PathTaper = 5f;
        const float FloorY = 0.006f;                                     // stone floor 6 mm above ground, 6 mm below the path strip
        const float HeadHalf = 3.5f;                                     // stone headwall (portal) half-width; turf beyond
        const float WingSlope = 0.7f;                                    // turf end slopes beyond the portal (35 deg)
        const float WingBack = 0.3f, WingProud = 0.04f;                  // wing copings: narrower and flatter than the headwall's

        public static float Hump(float x)
        {
            float ax = Mathf.Min(Mathf.Abs(x) / MoundHalf, 1f);
            return (HumpTop + HumpSink) * 0.5f * (1f + Mathf.Cos(Mathf.PI * ax)) - HumpSink;
        }

        static float HumpSlope(float x)
        {
            if (Mathf.Abs(x) >= MoundHalf) return 0f;
            return -(HumpTop + HumpSink) * 0.5f * Mathf.PI / MoundHalf * Mathf.Sin(Mathf.PI * x / MoundHalf);
        }

        static Vector3 HumpNormal(float x) => new Vector3(-HumpSlope(x), 1f, 0f).normalized;

        // the headwall face runs 2 cm up behind the coping's proud front, so the two polylines can never open a slit
        static float FaceTop(float x) => Hump(x) + Parapet - CopingH + 0.02f;
        static float CopingTop(float x) => Hump(x) + Parapet;

        /// <summary>Builds every mesh of the footbridge into the FarValley set and registers Footbridge.Opening.</summary>
        public static void Build(BuildContext ctx)
        {
            const float bend = 800f;   // visible from s 24 (376 m) and from the plain
            var stone = new MeshBuilder();
            var ashlar = new MeshBuilder();
            var gold = new MeshBuilder();
            var turf = new MeshBuilder();
            var path = new MeshBuilder();
            Intrados(stone);
            Floor(stone);
            for (int side = -1; side <= 1; side += 2)
            {
                Headwall(stone, side);
                Coping(ashlar, side);
                ArchRing(ashlar, gold, side);
                for (int sgn = -1; sgn <= 1; sgn += 2) Wing(stone, side, sgn);
            }
            Turf(turf);
            CrossPath(path);
            var root = new GameObject("Footbridge").transform;
            root.SetParent(ctx.SetRoot(RingSet.FarValley), false);
            Beat34Geo.Add(ctx, "Footbridge.Stone", stone, Beat34Mat.BridgeStone, RingSet.FarValley, true, bend, root);
            Beat34Geo.Add(ctx, "Footbridge.Ashlar", ashlar, Beat34Mat.Ashlar, RingSet.FarValley, true, bend, root);
            Beat34Geo.Add(ctx, "Footbridge.Keystones", gold, RingMat.PaleGold, RingSet.FarValley, true, bend, root);
            Beat34Geo.Add(ctx, "Footbridge.Turf", turf, Beat34Mat.Turf, RingSet.FarValley, true, bend, root);
            Beat34Geo.Add(ctx, "Footbridge.CrossPath", path, RingMat.PathStrip, RingSet.FarValley, true, bend, root);
            // registry: the clear opening 4.2 x 3.2 x 8.0 and the whole structure
            Beat34Geo.Marker(ctx, "Footbridge.Opening", root, new Vector3(0f, 0.5f * RingConstants.FootbridgeH, ZC),
                             new Vector3(RingConstants.FootbridgeW, RingConstants.FootbridgeH, RingConstants.FootbridgeL));
            Beat34Geo.Marker(ctx, "Footbridge", root, new Vector3(0f, 0.5f * CopingTop(0f), ZC),
                             new Vector3(2f * MoundHalf, CopingTop(0f), Z1 - Z0 + 2f * CopingProud));
        }

        // ---------------------------------------------------------------- vault interior
        static void OpeningProfile(List<Vector2> p, List<Vector2> n)
        {
            // right wall bottom -> up -> crown -> left wall bottom; inward normals (toward the opening axis)
            // (the walls carry only their end points: the headwall hole edge has exactly these vertices, no T-junctions)
            p.Add(new Vector2(HalfW, -Bury)); n.Add(Vector2.left);
            for (int k = 0; k <= ArchSeg; k++)
            {
                float a = Mathf.PI * k / ArchSeg;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.Add(new Vector2(CrownR * d.x, Spring + CrownR * d.y)); n.Add(-d);
            }
            p.Add(new Vector2(-HalfW, -Bury)); n.Add(Vector2.right);
        }

        static void Intrados(MeshBuilder b)
        {
            var p = new List<Vector2>(); var n = new List<Vector2>();
            OpeningProfile(p, n);
            var zs = Beat34Geo.Steps(Z0, Z1, 1f, new[] { Z0 + 0.5f, Z1 - 0.5f });
            int np = p.Count, nz = zs.Count;
            var id = new int[nz, np];
            for (int r = 0; r < nz; r++)
            {
                float d = Mathf.Min(zs[r] - Z0, Z1 - zs[r]);
                float aoZ = 0.95f - 0.40f * RingMath.Smoothstep(0f, 3f, d);
                for (int i = 0; i < np; i++)
                {
                    float aoY = 0.80f + 0.20f * Mathf.Clamp01((p[i].y + Bury) / 1.2f);
                    var pos = new Vector3(p[i].x, p[i].y, zs[r]);
                    id[r, i] = b.Vtx(pos, new Vector3(n[i].x, n[i].y, 0f), new Vector2(zs[r], p[i].y), Beat34Geo.Col(aoZ * aoY));
                }
            }
            for (int r = 0; r < nz - 1; r++)
                for (int i = 0; i < np - 1; i++)
                {
                    Vector3 d = new Vector3(n[i].x + n[i + 1].x, n[i].y + n[i + 1].y, 0f);
                    Beat34Geo.QuadO(b, id[r, i], id[r, i + 1], id[r + 1, i + 1], id[r + 1, i], d);
                }
        }

        static void Floor(MeshBuilder b)
        {
            var xs = new List<float> { -RingOuter, -HalfW, -1.05f, 0f, 1.05f, HalfW, RingOuter };
            var zs = Beat34Geo.Steps(Z0 - 0.6f, Z1 + 0.6f, 1f, new[] { Z0, Z1 });
            Beat34Geo.Grid(b, new Vector3(0f, FloorY, 0f), Vector3.right, Vector3.forward, xs, zs, Vector3.up, (x, z) =>
            {
                float d = Mathf.Min(z - Z0, Z1 - z);
                float ao = d <= 0f ? 1f : 0.95f - 0.35f * RingMath.Smoothstep(0f, 3f, d);
                if (Mathf.Abs(x) >= HalfW - 1e-3f) ao *= 0.85f;
                return Beat34Geo.Col(ao);
            }, 0, 2);
        }

        // ---------------------------------------------------------------- headwalls
        static Vector3 FaceP(float x, float y, float w, int side) => new Vector3(x, y, (side < 0 ? Z0 : Z1) + side * w);

        static void Headwall(MeshBuilder b, int side)
        {
            Vector3 nrm = new Vector3(0f, 0f, side);
            // piers (both sides): three vertices per column (bottom, springing row, top) so the spandrel joins without T-junctions
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var xs = Beat34Geo.Steps(HalfW, HeadHalf, 0.35f, new[] { RingOuter });
                int baseIdx = b.V.Count;
                for (int i = 0; i < xs.Count; i++)
                {
                    float x = sgn * xs[i];
                    float top = FaceTop(xs[i]);
                    float mid = Mathf.Min(Spring, -Bury + 0.5f * (top + Bury));
                    b.Vtx(FaceP(x, -Bury, 0f, side), nrm, new Vector2(x, -Bury), Beat34Geo.Col(0.8f));
                    b.Vtx(FaceP(x, mid, 0f, side), nrm, new Vector2(x, mid), Beat34Geo.Col(0.95f));
                    b.Vtx(FaceP(x, top, 0f, side), nrm, new Vector2(x, top), Beat34Geo.Col(1f));
                }
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    int a = baseIdx + 3 * i, c = baseIdx + 3 * (i + 1);
                    Beat34Geo.QuadO(b, a, c, c + 1, a + 1, nrm);
                    Beat34Geo.QuadO(b, a + 1, c + 1, c + 2, a + 2, nrm);
                }
            }
            // spandrel over the crown, on the intrados angles
            {
                int baseIdx = b.V.Count;
                for (int k = 0; k <= ArchSeg; k++)
                {
                    float a = Mathf.PI * k / ArchSeg;
                    float x = CrownR * Mathf.Cos(a), y = Spring + CrownR * Mathf.Sin(a);
                    float top = FaceTop(x);
                    b.Vtx(FaceP(x, y, 0f, side), nrm, new Vector2(x, y), Beat34Geo.Col(0.95f));
                    b.Vtx(FaceP(x, top, 0f, side), nrm, new Vector2(x, top), Beat34Geo.Col(1f));
                }
                for (int k = 0; k < ArchSeg; k++)
                {
                    int a = baseIdx + 2 * k, c = baseIdx + 2 * (k + 1);
                    Beat34Geo.QuadO(b, a, c, c + 1, a + 1, nrm);
                }
            }
        }

        static void Coping(MeshBuilder b, int side)
        {
            var xs = Beat34Geo.Steps(-HeadHalf, HeadHalf, 0.3f, null);
            Vector3 outw = new Vector3(0f, 0f, side);
            int n = xs.Count;
            // section points (w outward from the face plane, y) for each x
            System.Func<float, int, Vector2> S = (x, j) =>
            {
                float hc = CopingTop(x), ht = Hump(x);
                switch (j)
                {
                    case 0: return new Vector2(0f, hc - CopingH);                     // underside meets the face
                    case 1: return new Vector2(CopingProud, hc - CopingH);            // front bottom
                    case 2: return new Vector2(CopingProud, hc - Chamfer);            // front top (chamfer start)
                    case 3: return new Vector2(CopingProud - Chamfer, hc);            // top front
                    case 4: return new Vector2(-CopingBack + Chamfer, hc);            // top back
                    case 5: return new Vector2(-CopingBack, hc - Chamfer);            // back top
                    default: return new Vector2(-CopingBack, ht - 0.1f);              // back bottom, below the turf
                }
            };
            // faces: (from j, to j, normal kind) 0 underside, 1 front, 2 front chamfer, 3 top, 4 back chamfer, 5 back
            for (int f = 0; f < 6; f++)
            {
                int baseIdx = b.V.Count;
                for (int i = 0; i < n; i++)
                {
                    float x = xs[i];
                    Vector3 top = HumpNormal(x);
                    Vector3 nf;
                    switch (f)
                    {
                        case 0: nf = Vector3.down; break;
                        case 1: nf = outw; break;
                        case 2: nf = (outw + top).normalized; break;
                        case 3: nf = top; break;
                        case 4: nf = (top - outw).normalized; break;
                        default: nf = -outw; break;
                    }
                    Vector2 p0 = S(x, f), p1 = S(x, f + 1);
                    b.Vtx(FaceP(x, p0.y, p0.x, side), nf, new Vector2(x, p0.y), Beat34Geo.Col(1f, f == 2 || f == 4 ? 1f : 0f));
                    b.Vtx(FaceP(x, p1.y, p1.x, side), nf, new Vector2(x, p1.y), Beat34Geo.Col(1f, f == 2 || f == 4 ? 1f : 0f));
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = baseIdx + 2 * i, c = baseIdx + 2 * (i + 1);
                    Vector3 d = b.N[a] + b.N[c];
                    Beat34Geo.QuadO(b, a, c, c + 1, a + 1, d);
                }
            }
            // end caps at x = +-HeadHalf (they stand 0.15 m above the turf of the end slopes)
            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? xs[0] : xs[n - 1];
                Vector3 nc = e == 0 ? Vector3.left : Vector3.right;
                var ids = new int[7];
                for (int j = 0; j < 7; j++)
                {
                    Vector2 p = S(x, j);
                    ids[j] = b.Vtx(FaceP(x, p.y, p.x, side), nc, new Vector2(p.x, p.y), Beat34Geo.Col(1f));
                }
                for (int j = 1; j < 6; j++) Beat34Geo.TriO(b, ids[0], ids[j], ids[j + 1], nc);
            }
        }

        // ---------------------------------------------------------------- voussoirs, keystone, jambs
        static void ArchRing(MeshBuilder ashlar, MeshBuilder gold, int side)
        {
            float g = 0.5f * Joint / 2.35f;   // half joint as an angle at the ring's mid radius
            const float reveal = 0.005f;      // blocks sit 5 mm back from the intrados edge: no co-planar or T-junction seam
            for (int j = 0; j < Voussoirs; j++)
            {
                bool key = j == Voussoirs / 2;
                float a0 = Mathf.PI * j / Voussoirs + g, a2 = Mathf.PI * (j + 1) / Voussoirs - g, a1 = 0.5f * (a0 + a2);
                float ro = key ? KeyOuter : RingOuter;
                float ri = CrownR + reveal;
                var inner = new List<Vector2> { Polar(ri, a0), Polar(ri, a1), Polar(ri, a2) };
                var outer = new List<Vector2> { Polar(ro, a0), Polar(ro, a1), Polar(ro, a2) };
                Block(key ? gold : ashlar, inner, outer, side, key ? KeyProud : StoneProud);
            }
            // jamb stones below the springing: three courses each side
            float[] cy = { -Bury, 0.3f, 0.7f, Spring };
            for (int sgn = -1; sgn <= 1; sgn += 2)
                for (int c = 0; c < 3; c++)
                {
                    float y0 = cy[c] + (c == 0 ? 0f : 0.5f * Joint), y1 = cy[c + 1] - 0.5f * Joint;
                    var inner = new List<Vector2> { new Vector2(sgn * (HalfW + reveal), y0), new Vector2(sgn * (HalfW + reveal), y1) };
                    var outer = new List<Vector2> { new Vector2(sgn * RingOuter, y0), new Vector2(sgn * RingOuter, y1) };
                    Block(ashlar, inner, outer, side, StoneProud);
                }
        }

        static Vector2 Polar(float r, float a) => new Vector2(r * Mathf.Cos(a), Spring + r * Mathf.Sin(a));

        /// <summary>A stone block proud of the face plane: side faces from the face (w 0) to w = depth - chamfer,
        /// a 45-degree chamfer, and a front face between the inset rails. Rails have the same point count.</summary>
        static void Block(MeshBuilder b, List<Vector2> inner, List<Vector2> outer, int side, float depth)
        {
            int m = inner.Count;
            var poly = new List<Vector2>(inner);
            for (int i = m - 1; i >= 0; i--) poly.Add(outer[i]);
            int np = poly.Count;
            float area = 0f;
            for (int i = 0; i < np; i++) { Vector2 p = poly[i], q = poly[(i + 1) % np]; area += p.x * q.y - q.x * p.y; }
            float orient = area >= 0f ? 1f : -1f;   // +1 counter-clockwise (x right, y up)
            var en = new Vector2[np];               // outward in-plane normal of edge i -> i+1
            for (int i = 0; i < np; i++)
            {
                Vector2 d = (poly[(i + 1) % np] - poly[i]).normalized;
                en[i] = new Vector2(d.y, -d.x) * orient;
            }
            var inset = new Vector2[np];
            for (int i = 0; i < np; i++)
            {
                Vector2 n0 = en[(i - 1 + np) % np], n1 = en[i];
                float den = 1f + Vector2.Dot(n0, n1);
                Vector2 off = den > 1e-3f ? (n0 + n1) / den : n1;
                inset[i] = poly[i] - off * Chamfer;
            }
            Vector3 fwd = new Vector3(0f, 0f, side);
            float wc = depth - Chamfer;
            for (int i = 0; i < np; i++)
            {
                int i1 = (i + 1) % np;
                Vector3 e3 = new Vector3(en[i].x, en[i].y, 0f);
                Vector2 p0 = poly[i], p1 = poly[i1], q0 = inset[i], q1 = inset[i1];
                Beat34Geo.FlatQuadP(b, FaceP(p0.x, p0.y, 0f, side), FaceP(p1.x, p1.y, 0f, side), FaceP(p1.x, p1.y, wc, side), FaceP(p0.x, p0.y, wc, side),
                                    e3, Beat34Geo.Col(0.9f), 0, 1);
                Beat34Geo.FlatQuadP(b, FaceP(p0.x, p0.y, wc, side), FaceP(p1.x, p1.y, wc, side), FaceP(q1.x, q1.y, depth, side), FaceP(q0.x, q0.y, depth, side),
                                    (e3 + fwd).normalized, Beat34Geo.Col(1f, 1f), 0, 1);
            }
            int baseIdx = b.V.Count;
            for (int i = 0; i < m; i++)
            {
                Vector2 qi = inset[i], qo = inset[np - 1 - i];
                b.Vtx(FaceP(qi.x, qi.y, depth, side), fwd, qi, Beat34Geo.Col(1f));
                b.Vtx(FaceP(qo.x, qo.y, depth, side), fwd, qo, Beat34Geo.Col(1f));
            }
            for (int i = 0; i < m - 1; i++)
            {
                int a = baseIdx + 2 * i, c = baseIdx + 2 * (i + 1);
                Beat34Geo.QuadO(b, a, c, c + 1, a + 1, fwd);
            }
        }

        // ---------------------------------------------------------------- mound and cross path
        /// <summary>Turf height at (x, z): the hump over the vault length, falling at <see cref="WingSlope"/> past the face
        /// planes, never below -HumpSink (it meets the ground at a clean angle). Only built where |x| >= HeadHalf outside
        /// the vault length: in front of the portal the forecourt between the wing walls is open.</summary>
        static float TurfH(float x, float z)
        {
            float dz = Mathf.Max(Mathf.Max(Z0 - z, z - Z1), 0f);
            return Mathf.Max(Hump(x) - WingSlope * dz, -HumpSink);
        }

        /// <summary>Length of the end slope at the portal edge (where it meets the ground beside the wing wall).</summary>
        static float WingLen => (Hump(HeadHalf) + HumpSink) / WingSlope;

        static Vector3 TurfNormal(float x, float z)
        {
            const float e = 0.05f;
            float hx = (TurfH(x + e, z) - TurfH(x - e, z)) / (2f * e);
            float hz = (TurfH(x, z + e) - TurfH(x, z - e)) / (2f * e);
            return new Vector3(-hx, 1f, -hz).normalized;
        }

        static void Turf(MeshBuilder b)
        {
            float za = Z0 + CopingBack - 0.02f, zb = Z1 - CopingBack + 0.02f;
            var zMid = new List<float> { za, za + 0.9f, ZC - PathHalf - 0.2f, ZC, ZC + PathHalf + 0.2f, zb - 0.9f, zb };
            // centre piece over the vault, behind the portal copings
            TurfGrid(b, Beat34Geo.Steps(-HeadHalf, HeadHalf, 0.35f, null), zMid);
            // side pieces past the portal: the hump with both end slopes (rows inside [za, zb] identical to the centre
            // piece's, so the seam at |x| = HeadHalf has no T-junction)
            float L = WingLen;
            var zs = Beat34Geo.Steps(Z0 - L, Z0, 0.35f, null);
            zs.AddRange(zMid);
            zs.AddRange(Beat34Geo.Steps(Z1, Z1 + L, 0.35f, null));
            TurfGrid(b, Beat34Geo.Steps(HeadHalf, MoundHalf, 0.35f, null), zs);
            TurfGrid(b, Beat34Geo.Steps(-MoundHalf, -HeadHalf, 0.35f, null), zs);
        }

        static void TurfGrid(MeshBuilder b, List<float> xs, List<float> zs)
        {
            int nx = xs.Count, nz = zs.Count;
            var id = new int[nx, nz];
            float za = Z0 + CopingBack, zb = Z1 - CopingBack;
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < nz; k++)
                {
                    float x = xs[i], z = zs[k];
                    // AO: a little darker in the lee of the copings and toward the slope foot
                    float nearCoping = Mathf.Abs(x) <= HeadHalf + 0.01f ? Mathf.Clamp01(Mathf.Min(z - za, zb - z) / 0.9f) : 1f;
                    float foot = Mathf.Clamp01((TurfH(x, z) + HumpSink) / 0.6f);
                    float ao = (0.82f + 0.18f * nearCoping) * (0.85f + 0.15f * foot);
                    id[i, k] = b.Vtx(new Vector3(x, TurfH(x, z), z), TurfNormal(x, z), new Vector2(x, z), Beat34Geo.Col(ao));
                }
            for (int i = 0; i < nx - 1; i++)
                for (int k = 0; k < nz - 1; k++)
                    Beat34Geo.QuadO(b, id[i, k], id[i + 1, k], id[i + 1, k + 1], id[i, k + 1], Vector3.up);
        }

        /// <summary>Wing wall of portal <paramref name="side"/> on the <paramref name="sgn"/> flank: a vertical coursed face in
        /// the plane x = sgn HeadHalf facing the forecourt, from below ground to the end-slope turf line, under a sloped
        /// ashlar coping (the headwall coping's section, proud toward the forecourt, its back sunk in the turf). It runs
        /// from the headwall face (concave corner, shared edge heights) to where the slope meets the ground.</summary>
        static void Wing(MeshBuilder stone, int side, int sgn)
        {
            float L = WingLen;
            float zFace = side < 0 ? Z0 : Z1;
            float xw = sgn * HeadHalf;
            var F = new Vector3(-sgn, 0f, 0f);                                     // toward the forecourt
            System.Func<float, float> Zt = t => zFace + side * t;                 // t = distance out from the headwall face
            // face
            var ts = Beat34Geo.Steps(0f, L + 0.1f, 0.35f, null);
            int baseIdx = stone.V.Count;
            for (int i = 0; i < ts.Count; i++)
            {
                float z = Zt(ts[i]);
                float top = TurfH(xw, z) + Parapet - CopingH + 0.02f;
                stone.Vtx(new Vector3(xw, -Bury, z), F, new Vector2(z, -Bury), Beat34Geo.Col(0.8f));
                stone.Vtx(new Vector3(xw, top, z), F, new Vector2(z, top), Beat34Geo.Col(1f));
            }
            for (int i = 0; i < ts.Count - 1; i++)
            {
                int a = baseIdx + 2 * i, c = baseIdx + 2 * (i + 1);
                Beat34Geo.QuadO(stone, a, c, c + 1, a + 1, F);
            }
            // coping, from just past the headwall coping's proud front (2 mm, no co-planar faces) to the slope foot
            var tc = Beat34Geo.Steps(CopingProud + 0.002f, L + 0.1f, 0.3f, null);
            int n = tc.Count;
            System.Func<float, int, Vector2> S = (z, j) =>
            {
                float hc = TurfH(xw, z) + Parapet, ht = TurfH(xw + sgn * WingBack, z);
                switch (j)
                {
                    case 0: return new Vector2(0f, hc - CopingH);
                    case 1: return new Vector2(WingProud, hc - CopingH);
                    case 2: return new Vector2(WingProud, hc - Chamfer);
                    case 3: return new Vector2(WingProud - Chamfer, hc);
                    case 4: return new Vector2(-WingBack + Chamfer, hc);
                    case 5: return new Vector2(-WingBack, hc - Chamfer);
                    default: return new Vector2(-WingBack, Mathf.Min(ht, hc - CopingH) - 0.1f);
                }
            };
            System.Func<float, Vector2, Vector3> P = (z, wy) => new Vector3(xw - sgn * wy.x, wy.y, z);
            for (int f = 0; f < 6; f++)
            {
                int b0 = stone.V.Count;
                for (int i = 0; i < n; i++)
                {
                    float z = Zt(tc[i]);
                    float slope = TurfH(xw, z) > -HumpSink + 1e-4f ? WingSlope : 0f;
                    Vector3 top = new Vector3(0f, 1f, side * slope).normalized;
                    Vector3 nf;
                    switch (f)
                    {
                        case 0: nf = Vector3.down; break;
                        case 1: nf = F; break;
                        case 2: nf = (F + top).normalized; break;
                        case 3: nf = top; break;
                        case 4: nf = (top - F).normalized; break;
                        default: nf = -F; break;
                    }
                    Color c = Beat34Geo.Col(1f, f == 2 || f == 4 ? 1f : 0f);
                    Vector3 p0 = P(z, S(z, f)), p1 = P(z, S(z, f + 1));
                    stone.Vtx(p0, nf, new Vector2(z, p0.y), c);
                    stone.Vtx(p1, nf, new Vector2(z, p1.y), c);
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = b0 + 2 * i, c = b0 + 2 * (i + 1);
                    Beat34Geo.QuadO(stone, a, c, c + 1, a + 1, stone.N[a] + stone.N[c]);
                }
            }
            // end caps: toward the headwall and at the slope foot
            for (int e = 0; e < 2; e++)
            {
                float z = Zt(e == 0 ? tc[0] : tc[n - 1]);
                Vector3 nc = new Vector3(0f, 0f, e == 0 ? -side : side);
                var ids = new int[7];
                for (int j = 0; j < 7; j++)
                {
                    Vector2 wy = S(z, j);
                    ids[j] = stone.Vtx(P(z, wy), nc, wy, Beat34Geo.Col(1f));
                }
                for (int j = 1; j < 6; j++) Beat34Geo.TriO(stone, ids[0], ids[j], ids[j + 1], nc);
            }
        }

        static float PathY(float x) => Mathf.Max(Hump(x), 0f) + PathLift;

        static float PathSlope(float x) => Hump(x) > 0f ? HumpSlope(x) : 0f;

        static float PathHalfAt(float x)
        {
            float t = Mathf.Clamp01((Mathf.Abs(x) - (PathEnd - PathTaper)) / PathTaper);
            return PathHalf * (1f - 0.5f * RingMath.Smootherstep01(t));
        }

        static void CrossPath(MeshBuilder b)
        {
            var xs = Beat34Geo.Steps(-PathEnd, PathEnd, 6f, null);
            var dense = Beat34Geo.Steps(-MoundHalf - 0.3f, MoundHalf + 0.3f, 0.35f, null);
            for (int i = xs.Count - 1; i >= 0; i--) if (Mathf.Abs(xs[i]) < MoundHalf + 0.3f + 1e-3f) xs.RemoveAt(i);
            xs.AddRange(dense);
            xs.Add(-(PathEnd - PathTaper)); xs.Add(PathEnd - PathTaper);
            xs.Sort();
            for (int i = xs.Count - 1; i > 0; i--) if (xs[i] - xs[i - 1] < 0.05f) xs.RemoveAt(i == xs.Count - 1 ? i - 1 : i);
            int n = xs.Count;
            // top surface
            int top = b.V.Count;
            for (int i = 0; i < n; i++)
            {
                float x = xs[i], y = PathY(x), h = PathHalfAt(x);
                Vector3 nt = new Vector3(-PathSlope(x), 1f, 0f).normalized;
                b.Vtx(new Vector3(x, y, ZC - h), nt, new Vector2(x, ZC - h), Beat34Geo.Col(1f));
                b.Vtx(new Vector3(x, y, ZC + h), nt, new Vector2(x, ZC + h), Beat34Geo.Col(1f));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = top + 2 * i, c = top + 2 * (i + 1);
                Beat34Geo.QuadO(b, a, c, c + 1, a + 1, Vector3.up);
            }
            // edges (2.5 cm visible, buried 3.5 cm)
            for (int e = -1; e <= 1; e += 2)
            {
                int s0 = b.V.Count;
                Vector3 ne = new Vector3(0f, 0f, e);
                for (int i = 0; i < n; i++)
                {
                    float x = xs[i], y = PathY(x), z = ZC + e * PathHalfAt(x);
                    b.Vtx(new Vector3(x, y, z), ne, new Vector2(x, y), Beat34Geo.Col(1f, 1f));
                    b.Vtx(new Vector3(x, y - 0.06f, z), ne, new Vector2(x, y - 0.06f), Beat34Geo.Col(0.9f));
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = s0 + 2 * i, c = s0 + 2 * (i + 1);
                    Beat34Geo.QuadO(b, a, c, c + 1, a + 1, ne);
                }
            }
            // end faces
            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? xs[0] : xs[n - 1];
                float y = PathY(x), h = PathHalfAt(x);
                Vector3 nx = e == 0 ? Vector3.left : Vector3.right;
                Beat34Geo.FlatQuadP(b, new Vector3(x, y, ZC - h), new Vector3(x, y, ZC + h), new Vector3(x, y - 0.06f, ZC + h), new Vector3(x, y - 0.06f, ZC - h),
                                    nx, Beat34Geo.Col(0.9f), 2, 1);
            }
        }
    }
}
