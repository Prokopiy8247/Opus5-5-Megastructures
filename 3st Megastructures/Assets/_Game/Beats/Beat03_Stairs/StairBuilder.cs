// StairBuilder: procedural geometry of beat 3 (MASTER_PROMPT 14.5.3, 14.12 "Stairs", 8.7).
// Pure generators (no scene access) so EditMode tests can measure them (T-SCALE-1, T-NORMALS).
//
// Layout (ground-group local coordinates; the salt is at y = 0, the group sinks by -A_virt):
//   200 treads of 0.40 m x 0.18 m, x in [-20, 20], riser i at z = 240 + 0.4 (i - 1), tread i top at y = 0.18 i.
//   Every nosing (tread front edge) carries a 0.02 m 45-degree chamfer so it catches light (8.7).
//   Riser 1 continues down to the skirt y = -4 (the stair body enters the salt, no co-planar faces with it).
//   The side faces x = +-20 are the solid stringers with a stepped (chamfered) silhouette.
//   Stair / terrace joint: the terrace slab has a 0.05 m chamfer on its front top edge; the top tread
//   (tread 200) runs 0.05 m further, over that chamfer, and ends exactly on the terrace top's front
//   edge (z = TerraceBuilder.JointZ). The two co-planar y = 36 faces therefore only ABUT along one shared
//   edge whose end vertices (x = +-20) exist in both meshes (bit-identical floats, same transform), so
//   the joint is watertight and nothing overlaps co-planarly (Part 8.7, Part 10 item 1).
// Normals are flat per face (tread up, riser -Z, nosing (0, 1, -1)/sqrt 2, stringers +-X): the M03 shader
// tells tread from riser by the normal. Vertex colour = (AO 1, height 1, sway 0, alpha 1): no per-step AO
// on purpose (an unfiltered 0.4 m periodic AO term would moire at distance; the M03 filter handles colour).
// UV0 = (x, z) local metres; UV1 = (tread index, face type 0 tread / 0.5 nosing / 1 riser, local y, 0);
// tangent = (normal, 1) (no normal maps; a defined value instead of zero).
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    public static class StairBuilder
    {
        /// <summary>Nosing chamfer (14.5.3 line 677: "0.02 m chamfered nosings").</summary>
        public const double NosingChamfer = 0.02;
        /// <summary>Bollard spacing along z from the stair foot (14.5.3 line 677: "every 16 m from z = 240").</summary>
        public const double BollardSpacing = 16.0;
        /// <summary>Bollards per edge: z = 240, 256, ..., 320 (6 per edge, 12 instances, 14.5.3 / 14.7.9).</summary>
        public static int BollardsPerEdge => (int)System.Math.Round((ThreadConstants.StairEndZ - ThreadConstants.StairStartZ) / BollardSpacing) + 1;
        /// <summary>Lateral inset of the bollard axis from the stair edge (outside the 10 m corridor).</summary>
        public const double BollardInset = 0.6;
        /// <summary>Bollard base sinks this far into its tread (no co-planar base disc, no gap).</summary>
        public const double BollardSink = 0.05;
        /// <summary>Height of the domed cap of the bollard.</summary>
        public const double BollardCapHeight = 0.16;
        /// <summary>Radius of the small flat disc at the top of the dome (avoids a degenerate lathe pole).</summary>
        public const double BollardTopRadius = 0.04;
        public const int BollardSegments = 20;
        /// <summary>Walk-line bollards (lead decisions D-050 / D-053): two rows lining the 4 m stair walk (ThreadMath.
        /// StairWalkHalfWidth) every 4 m (10 treads) from z = 256 to the terrace, so the white-out keeps a human-scale cue
        /// and a row of posts recedes into the mist ahead (the edge bollards at x = +-19.4 are outside the KF3 frame or
        /// veiled by tau > 3; critics A8, E14, PAC-07). Inner face 4.575 m: clear of the player capsule (4.0 + 0.35).</summary>
        public const double WalkLineX = 4.75;
        public const double WalkLineSpacing = 4.0;
        public const double WalkLineZ0 = 16.0;       // first pair 16 m above the stair foot (the corridor is 4 m from s = 255)
        public const int WalkLinePerSide = 17;       // z = 256.2, 260.2, ..., 320.2 (the last on the terrace)

        static readonly Color VertexColour = new Color(1f, 1f, 0f, 1f);   // AO 1, height shade none, sway 0, alpha 1

        public static float X0 => (float)(-ThreadConstants.StairWidth * 0.5);
        public static float X1 => (float)(ThreadConstants.StairWidth * 0.5);

        /// <summary>z of riser i (1-based) = front edge of tread i.</summary>
        public static double RiserZ(int i) => ThreadConstants.StairStartZ + ThreadConstants.TreadDepth * (i - 1);
        /// <summary>Top of tread i (1-based).</summary>
        public static double TreadY(int i) => ThreadConstants.RiserHeight * i;
        /// <summary>Back edge z of tread i; the top tread ends on the terrace joint (see header).</summary>
        public static float TreadBackZ(int i)
        {
            if (i >= ThreadConstants.StairTreads) return TerraceBuilder.JointZ;
            return (float)(ThreadConstants.StairStartZ + ThreadConstants.TreadDepth * i);
        }

        // ------------------------------------------------------------------ steps (treads, nosings, risers)
        public static MeshBuilder BuildSteps()
        {
            int n = ThreadConstants.StairTreads;
            var mb = new MeshBuilder(n * 12 + 16);
            float x0 = X0, x1 = X1;
            float c = (float)NosingChamfer;
            Vector3 up = Vector3.up, back = Vector3.back;
            Vector3 nose = new Vector3(0f, 1f, -1f).normalized;
            for (int i = 1; i <= n; i++)
            {
                float zf = (float)RiserZ(i);
                float zb = TreadBackZ(i);
                float yt = (float)TreadY(i);
                float yr = i == 1 ? (float)ThreadConstants.StairSkirtY : (float)TreadY(i - 1);
                // riser i (riser 1 is also the front skirt down to y = -4)
                Quad(mb, new Vector3(x0, yr, zf), new Vector3(x0, yt - c, zf), new Vector3(x1, yt - c, zf), new Vector3(x1, yr, zf),
                     back, new Vector4(i, 1f, 0f, 0f));
                // nosing chamfer
                Quad(mb, new Vector3(x0, yt - c, zf), new Vector3(x0, yt, zf + c), new Vector3(x1, yt, zf + c), new Vector3(x1, yt - c, zf),
                     nose, new Vector4(i, 0.5f, 0f, 0f));
                // tread top
                Quad(mb, new Vector3(x0, yt, zf + c), new Vector3(x0, yt, zb), new Vector3(x1, yt, zb), new Vector3(x1, yt, zf + c),
                     up, new Vector4(i, 0f, 0f, 0f));
            }
            return mb;
        }

        // ------------------------------------------------------------------ stringers (side walls x = +-20)
        /// <summary>
        /// Solid side walls with the stepped, chamfered silhouette, from the skirt y = -4 to the treads.
        /// One convex column per tread; neighbouring columns share their full common edge (vertex at the
        /// tread height on both sides), so there are no T-junctions. The back edge (inside the terrace) is open.
        /// </summary>
        public static MeshBuilder BuildStringers()
        {
            int n = ThreadConstants.StairTreads;
            var mb = new MeshBuilder(n * 12 + 16);
            float c = (float)NosingChamfer;
            float yb = (float)ThreadConstants.StairSkirtY;
            var pts = new List<Vector3>(6);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? X0 : X1;
                Vector3 nrm = side == 0 ? Vector3.left : Vector3.right;
                for (int i = 1; i <= n; i++)
                {
                    float zf = (float)RiserZ(i), zb = TreadBackZ(i), yt = (float)TreadY(i);
                    pts.Clear();
                    pts.Add(new Vector3(x, yb, zb));                                  // f: fan origin (bottom back)
                    pts.Add(new Vector3(x, yb, zf));                                  // a: bottom front
                    if (i > 1) pts.Add(new Vector3(x, (float)TreadY(i - 1), zf));     // b: shared corner with column i-1
                    pts.Add(new Vector3(x, yt - c, zf));                              // c: riser top
                    pts.Add(new Vector3(x, yt, zf + c));                              // d: nosing top
                    pts.Add(new Vector3(x, yt, zb));                                  // e: tread back
                    Fan(mb, pts, nrm, new Vector4(i, 2f, 0f, 0f));
                }
            }
            return mb;
        }

        // ------------------------------------------------------------------ bollards
        /// <summary>Base points (centre of the base on the walking surface) of the 12 stair bollards.</summary>
        public static void BollardBases(List<Vector3> result)
        {
            result.Clear();
            double halfW = ThreadConstants.StairWidth * 0.5 - BollardInset;
            for (int side = 0; side < 2; side++)
            {
                double x = side == 0 ? -halfW : halfW;
                for (int k = 0; k < BollardsPerEdge; k++)
                {
                    // centred on the tread that starts at the 16 m mark (k = 5 stands on the terrace, y = 36)
                    double z = ThreadConstants.StairStartZ + BollardSpacing * k + ThreadConstants.TreadDepth * 0.5;
                    int tread = (int)System.Math.Round(BollardSpacing * k / ThreadConstants.TreadDepth) + 1;
                    double y = tread > ThreadConstants.StairTreads ? ThreadConstants.TerraceY : TreadY(tread);
                    result.Add(new Vector3((float)x, (float)y, (float)z));
                }
            }
        }

        /// <summary>Base points of the 34 walk-line bollards (D-050 / D-053), each centred on its tread.</summary>
        public static void WalkLineBollardBases(List<Vector3> result)
        {
            result.Clear();
            for (int side = 0; side < 2; side++)
            {
                double x = side == 0 ? -WalkLineX : WalkLineX;
                for (int k = 0; k < WalkLinePerSide; k++)
                {
                    double d = WalkLineZ0 + WalkLineSpacing * k;
                    double z = ThreadConstants.StairStartZ + d + ThreadConstants.TreadDepth * 0.5;
                    int tread = (int)System.Math.Round(d / ThreadConstants.TreadDepth) + 1;
                    double y = tread > ThreadConstants.StairTreads ? ThreadConstants.TerraceY : TreadY(tread);
                    result.Add(new Vector3((float)x, (float)y, (float)z));
                }
            }
        }

        /// <summary>Bollard lathe profile (y, r): straight shaft, quarter-ellipse dome, small flat top.</summary>
        public static List<Vector2> BollardProfile()
        {
            double h = ThreadConstants.BollardHeight, r = ThreadConstants.BollardDiameter * 0.5;
            double y1 = h - BollardCapHeight;
            var p = new List<Vector2>(10);
            p.Add(new Vector2((float)-BollardSink, (float)r));
            for (int k = 0; k <= 6; k++)
            {
                double t = k * (System.Math.PI * 0.5 / 6.0);
                p.Add(new Vector2((float)(y1 + BollardCapHeight * System.Math.Sin(t)),
                                  (float)(BollardTopRadius + (r - BollardTopRadius) * System.Math.Cos(t))));
            }
            return p;
        }

        /// <summary>All 12 edge bollards and the 34 walk-line bollards (D-050 / D-053) baked into one static mesh (one
        /// SRP-Batcher draw, 14.7.9 "bollards 28").</summary>
        public static MeshBuilder BuildBollards()
        {
            var bases = new List<Vector3>(32);
            BollardBases(bases);
            var walk = new List<Vector3>(2 * WalkLinePerSide);
            WalkLineBollardBases(walk);
            bases.AddRange(walk);
            var profile = BollardProfile();
            var mb = new MeshBuilder(bases.Count * (profile.Count + 2) * (BollardSegments + 1));
            MeshKit.LatheColor col = BollardColour;
            for (int i = 0; i < bases.Count; i++)
            {
                int start = mb.VertexCount;
                MeshKit.Lathe(mb, profile, BollardSegments, bases[i], Vector2.one, null, col, false, true);
                FinishVertices(mb, start);
            }
            return mb;
        }

        static Color BollardColour(float y, float angle, int ring) => VertexColour;

        // ------------------------------------------------------------------ helpers
        /// <summary>Planar quad with a flat normal; triangles are oriented to face along nrm.</summary>
        static void Quad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, Vector4 uv1)
        {
            int ia = Vert(mb, a, nrm, uv1), ib = Vert(mb, b, nrm, uv1), ic = Vert(mb, c, nrm, uv1), id = Vert(mb, d, nrm, uv1);
            TriN(mb, ia, ib, ic, nrm);
            TriN(mb, ia, ic, id, nrm);
        }

        /// <summary>Convex planar polygon given in boundary order, fanned from pts[0] (never collinear with a fan pair here).</summary>
        static void Fan(MeshBuilder mb, List<Vector3> pts, Vector3 nrm, Vector4 uv1)
        {
            int start = mb.VertexCount;
            for (int i = 0; i < pts.Count; i++) Vert(mb, pts[i], nrm, uv1);
            for (int i = 1; i < pts.Count - 1; i++) TriN(mb, start, start + i, start + i + 1, nrm);
        }

        static int Vert(MeshBuilder mb, Vector3 p, Vector3 nrm, Vector4 uv1)
        {
            uv1.z = p.y;
            return mb.Add(p, nrm, new Vector2(p.x, p.z), VertexColour, new Vector4(nrm.x, nrm.y, nrm.z, 1f), uv1);
        }

        static void TriN(MeshBuilder mb, int a, int b, int c, Vector3 nrm)
        {
            Vector3 fn = Vector3.Cross(mb.Positions[b] - mb.Positions[a], mb.Positions[c] - mb.Positions[a]);
            if (Vector3.Dot(fn, nrm) >= 0f) mb.Tri(a, b, c); else mb.Tri(a, c, b);
        }

        /// <summary>Lathe output gets tangent = (normal, 1) and UV1 = (0, 3 (bollard), local y, 0).</summary>
        static void FinishVertices(MeshBuilder mb, int start)
        {
            for (int i = start; i < mb.VertexCount; i++)
            {
                Vector3 n = mb.Normals[i];
                mb.Tangents[i] = new Vector4(n.x, n.y, n.z, 1f);
                mb.UV1[i] = new Vector4(0f, 3f, mb.Positions[i].y, 0f);
            }
        }
    }
}
