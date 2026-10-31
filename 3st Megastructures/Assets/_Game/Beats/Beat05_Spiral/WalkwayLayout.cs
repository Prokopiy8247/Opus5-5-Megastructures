// WalkwayLayout: the single geometric description of the beat 5-7 walkway (MASTER_PROMPT 14.4.2, 14.5.5,
// 14.5.6 arm rule, 14.5.7, 14.7.2): deck cross-section and stations, the helix deck sweep, the continuous
// handrail centre line, the rail post placements and the 43 support arms. Pure functions of ThreadConstants
// and ThreadMath, evaluated in double and cast to float only when a vertex is written. The builders of
// beats 5, 6 and 7 and the EditMode tests all call this code, so geometry and tests cannot drift apart.
//
// Decisions recorded here (see the B06 report):
//  * The deck sweep starts at s = 397.0, not 396.0. ThreadMath.PathY eases the grade in from s = 396, so at
//    s = 396.0-396.3 the deck top would lie less than 0.1 mm above the terrace top (y = 36.0): co-planar
//    faces z-fight (Part 8.7, 10 item 1). At s = 397 the deck top is 3.7 mm above the terrace: the deck
//    emerges from the terrace with an invisible 3.7 mm lip, its body below is inside the terrace solid.
//  * The deck cross-section is vertical (frame up = world up) and flat in the radial direction (14.4.3);
//    vertex normals are the exact normals of the swept surface (they include the 0.25 grade), not the
//    frame axes, so the deck shades like the sloped surface it is.
//  * The last station (s = 840) uses the exact P5 frame (0, 146, 400) with right = +X: the helix formula
//    with R_h = 70.028 ends 1.5 mm beyond z = 400 (440 / 70.028 = 2 pi + 2.1e-5); snapping the last frame makes
//    the deck end ring bit-identical to the pier start ring of the welded pier/pad outline (no seam).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>A point of the deck cross-section: side (+1 = right = toward the cable, -1 = left), inset d from that edge, height h relative to the deck top.</summary>
    public struct DeckProfilePoint
    {
        public int side;
        public double d, h;
        public DeckProfilePoint(int side, double d, double h) { this.side = side; this.d = d; this.h = h; }
        /// <summary>Lateral coordinate (m, + right). Written so that the pier outline (x = +-W, inward normal -+1) yields bit-identical values.</summary>
        public double X => side > 0 ? WalkwayLayout.HalfWidth - d : -(WalkwayLayout.HalfWidth - d);
    }

    public struct PostPlacement
    {
        public D3 basePos;   // deck top under the post centre
        public double yaw;   // rad, from +Z toward +X
    }

    /// <summary>One support arm (horizontal tapered beam from the cable or a bead lens to the deck underside).</summary>
    public struct ArmSpec
    {
        public int index;
        public double s;                  // arm station on the path (400 + 11 i; 840.. on the pier)
        public double dirX, dirZ;         // unit horizontal direction from the cable axis toward the deck
        public double rRoot, rTip;        // distances from the cable axis of the inner (root) and outer (tip) end faces
        public double yTop;               // top surface (horizontal, 0.3 m up inside the deck body)
        public double deckTopY;
        public double depthRoot, depthTip, widthRoot, widthTip;
        public int bead;                  // bead (0..8) whose lens holds the root, -1 = the cable (every arm under D-035)

        public double TanX => -dirZ;
        public double TanZ => dirX;

        public D3 Point(double r, double lateral, double y)
        {
            return new D3(ThreadConstants.CableAxisX + dirX * r + TanX * lateral, y, ThreadConstants.CableAxisZ + dirZ * r + TanZ * lateral);
        }

        public double DepthAt(double r) => depthRoot + (depthTip - depthRoot) * ThreadMath.Clamp01((r - rRoot) / Math.Max(1e-6, rTip - rRoot));
        public double WidthAt(double r) => widthRoot + (widthTip - widthRoot) * ThreadMath.Clamp01((r - rRoot) / Math.Max(1e-6, rTip - rRoot));
    }

    public static class WalkwayLayout
    {
        // ---------------------------------------------------------------- dimensions
        public const double HalfWidth = ThreadConstants.DeckWidth * 0.5;                // 1.5 (14.4.2: deck 3.0 m)
        public const double DeckStartS = 397.0;                                         // see header (co-planarity with the terrace)
        public const double DeckSegment = 2.0;                                          // 14.5.5 line 710: 2.0 m plan segments
        public const double BlendSegment = 0.5;                                         // finer through the grade blend s = 396-404
        public const double DeckBevel = 0.25;                                           // M06 line 465: 0.25 m deep chamfers (lower side edges)
        public const double TopBandInset = 0.30;                                        // top split 0.3 m from each edge (the pier/pad edge band)
        public const double RailLateral = HalfWidth - ThreadConstants.HandrailDiameter * 0.5;          // tube flush with the deck edge (KF7c, 14.5.7)
        public const double RailCentreHeight = ThreadConstants.RailHeight - ThreadConstants.HandrailDiameter * 0.5; // tube top at 1.0 m
        public const int RailSides = 8;                                                 // 14.5.5: 8-gon handrail
        public const double RailStartBack = 0.04;                                       // the tube starts 4 cm behind the first post
        public const double PostEmbed = 0.04;                                           // posts reach 4 cm into the deck (no gap on the chamfer)
        public const double PostChamfer = 0.006;
        public const int PostsPerChunk = 40;                                            // instanced chunk (about 20 m of helix, culling)
        public const double ArmEmbed = 0.3;                                             // 14.7.4 line 834: arms 0.3 m into deck and cable
        public const double ArmTopBelowDeckTop = ThreadConstants.DeckThickness - ArmEmbed;            // 0.1
        public const double ArmTipDepth = 0.55, ArmRootWidth = 0.50, ArmTipWidth = 0.34, ArmChamfer = 0.03;
        public const double ArmTipBeyondCentre = 1.17;                                  // tip under the flat deck underside (|x| < 1.25)
        public const double PierArmTipInset = 0.40;                                     // pier arm tips that would meet the pad boss stop 0.4 m inside the edge
        public const double ArmBossClearance = 0.10;

        // ---------------------------------------------------------------- deck cross-section (CCW, x right, y up)
        public static readonly DeckProfilePoint[] DeckProfile =
        {
            new DeckProfilePoint(-1, DeckBevel, -ThreadConstants.DeckThickness),
            new DeckProfilePoint(+1, DeckBevel, -ThreadConstants.DeckThickness),
            new DeckProfilePoint(+1, 0.0, -ThreadConstants.DeckThickness + DeckBevel),
            new DeckProfilePoint(+1, 0.0, -ThreadConstants.DeckChamfer),
            new DeckProfilePoint(+1, ThreadConstants.DeckChamfer, 0.0),
            new DeckProfilePoint(+1, TopBandInset, 0.0),
            new DeckProfilePoint(-1, TopBandInset, 0.0),
            new DeckProfilePoint(-1, ThreadConstants.DeckChamfer, 0.0),
            new DeckProfilePoint(-1, 0.0, -ThreadConstants.DeckChamfer),
            new DeckProfilePoint(-1, 0.0, -ThreadConstants.DeckThickness + DeckBevel),
        };

        /// <summary>Plan stations of the deck sweep: 0.5 m from 397 through the grade blend to 404, then 2.0 m to 840.</summary>
        public static List<double> DeckStations()
        {
            var st = new List<double>(240);
            int nBlend = (int)Math.Round((ThreadConstants.GradeBlendEnd - DeckStartS) / BlendSegment);
            for (int i = 0; i < nBlend; i++) st.Add(DeckStartS + BlendSegment * i);
            int nHelix = (int)Math.Round((ThreadConstants.HelixEndS - ThreadConstants.GradeBlendEnd) / DeckSegment);
            for (int j = 0; j <= nHelix; j++) st.Add(ThreadConstants.GradeBlendEnd + DeckSegment * j);
            return st;
        }

        /// <summary>Deck-top centre point and plan right vector at s; the last station is the exact P5 frame.</summary>
        public static void DeckFrame(double s, out D3 p, out double rx, out double rz)
        {
            if (s >= ThreadConstants.HelixEndS - 1e-9)
            {
                p = new D3(ThreadConstants.HelixCentreX - ThreadConstants.HelixRadius, ThreadConstants.TopY, ThreadConstants.PierStartZ);
                rx = 1.0; rz = 0.0;
                return;
            }
            p = ThreadMath.PathPoint(s);
            ThreadMath.Right(s, out rx, out rz);
        }

        public static D3 DeckPoint(double s, double x, double h)
        {
            DeckFrame(s, out D3 p, out double rx, out double rz);
            return new D3(p.x + rx * x, p.y + h, p.z + rz * x);
        }

        /// <summary>d/ds of the exact swept surface point (one-sided at the sweep ends, true helix at s = 840).</summary>
        static D3 DeckDerivative(double s, double x, double h, double sMin, double sMax)
        {
            const double e = 1e-3;
            double a = Math.Max(sMin, s - e), b = Math.Min(sMax, s + e);
            D3 pa = ThreadMath.CorridorToWorld(a, x, h), pb = ThreadMath.CorridorToWorld(b, x, h);
            return (pb - pa) * (1.0 / (b - a));
        }

        static double EdgeAo(double ny)
        {
            if (ny > 0.9) return 1.0;      // top
            if (ny > 0.1) return 1.0;      // top chamfer
            if (ny > -0.1) return 0.95;    // side
            if (ny > -0.9) return 0.88;    // lower bevel
            return 0.80;                   // underside
        }

        /// <summary>
        /// The helix walkway deck: one continuous sweep from s = 397 to 840 (shared vertices at every joint,
        /// hard creases only at the profile corners), the start cap facing back to the terrace, no end cap
        /// (the pier/pad slab continues with an identical ring). UV0 = (s, profile metres), UV1 = (lateral x, h).
        /// </summary>
        public static void BuildDeck(MeshBuilder mb)
        {
            var st = DeckStations();
            int nf = st.Count;
            var prof = DeckProfile;
            int np = prof.Length;
            double sMin = st[0], sMax = st[nf - 1];
            double v = 0;
            for (int e = 0; e < np; e++)
            {
                var A = prof[e];
                var B = prof[(e + 1) % np];
                double ax = A.X, bx = B.X;
                double ex = bx - ax, eh = B.h - A.h;
                double len = Math.Sqrt(ex * ex + eh * eh);
                double nx = eh / len, ny = -ex / len;   // outward normal of a CCW profile
                Color col = BeatMeshUtil.Ao(EdgeAo(ny));
                int start = mb.VertexCount;
                for (int k = 0; k < nf; k++)
                {
                    double s = st[k];
                    DeckFrame(s, out D3 p, out double rx, out double rz);
                    D3 e3 = new D3(rx * ex, eh, rz * ex);
                    D3 n3 = new D3(rx * nx, ny, rz * nx);
                    ThreadMath.Tangent(s, out double tx, out double tz);
                    var tan = new Vector4((float)tx, 0f, (float)tz, 1f);
                    for (int q = 0; q < 2; q++)
                    {
                        double x = q == 0 ? ax : bx, h = q == 0 ? A.h : B.h;
                        D3 pos = new D3(p.x + rx * x, p.y + h, p.z + rz * x);
                        D3 nrm = D3.Cross(DeckDerivative(s, x, h, sMin, sMax), e3).Normalized;
                        if (D3.Dot(nrm, n3) < 0) nrm = nrm * -1.0;
                        mb.Add(BeatMeshUtil.V(pos), BeatMeshUtil.V(nrm), new Vector2((float)s, (float)(q == 0 ? v : v + len)), col, tan,
                               new Vector4((float)x, (float)h, 0f, 0f));
                    }
                }
                for (int k = 0; k < nf - 1; k++)
                {
                    int A0 = start + 2 * k, B0 = A0 + 1, A1 = start + 2 * (k + 1), B1 = A1 + 1;
                    mb.Quad(A0, B0, B1, A1);   // MeshKit.Sweep winding (CCW profile, right = Cross(up, forward))
                }
                v += len;
            }
            // start cap (vertical, facing back along the path)
            var cap = new List<Vector3>(np);
            for (int i = 0; i < np; i++) cap.Add(BeatMeshUtil.V(DeckPoint(st[0], prof[i].X, prof[i].h)));
            ThreadMath.Tangent(st[0], out double t0x, out double t0z);
            MeshKit.ConvexPolygon(mb, cap, new Vector3(-(float)t0x, 0f, -(float)t0z), BeatMeshUtil.Ao(0.9));
        }

        // ---------------------------------------------------------------- handrail
        /// <summary>Rail stations on the helix: the tube start just behind the first post, then every deck station from s = 400.</summary>
        public static List<double> RailStations()
        {
            var st = new List<double>(240) { ThreadConstants.HelixStartS - RailStartBack };
            var deck = DeckStations();
            for (int i = 0; i < deck.Count; i++) if (deck[i] >= ThreadConstants.HelixStartS - 1e-9) st.Add(deck[i]);
            return st;
        }

        public static D3 RailPoint(double s, int side)
        {
            DeckFrame(s, out D3 p, out double rx, out double rz);
            double x = side * RailLateral;
            return new D3(p.x + rx * x, p.y + RailCentreHeight, p.z + rz * x);
        }

        /// <summary>
        /// The one continuous handrail centre line: up the right (cable-side) edge of the helix, along the right
        /// pier edge, around the far arc of the pad, back along the left pier edge and down the left helix edge.
        /// </summary>
        public static List<D3> RailCentreLine()
        {
            var st = RailStations();
            var pts = new List<D3>(2 * st.Count + 160);
            for (int i = 0; i < st.Count; i++) pts.Add(RailPoint(st[i], +1));
            PierPadOutline.AppendInset(pts, ThreadConstants.HandrailDiameter * 0.5, ThreadConstants.TopY + RailCentreHeight, true);
            for (int i = st.Count - 1; i >= 0; i--) pts.Add(RailPoint(st[i], -1));
            return pts;
        }

        public static void BuildRail(MeshBuilder mb)
        {
            BeatMeshUtil.Tube(mb, RailCentreLine(), ThreadConstants.HandrailDiameter * 0.5, RailSides, BeatMeshUtil.Ao(1.0), true, true);
        }

        // ---------------------------------------------------------------- posts
        /// <summary>All rail posts: 2 x 220 on the helix (s = 400, 402 .. 838), then the pier edges and the far arc of the pad.</summary>
        public static List<PostPlacement> Posts()
        {
            var list = new List<PostPlacement>(480);
            int nHelix = HelixPostStations;
            for (int k = 0; k < nHelix; k++)
            {
                double s = ThreadConstants.HelixStartS + ThreadConstants.PostSpacing * k;
                double yaw = ThreadMath.Heading(s);
                list.Add(new PostPlacement { basePos = DeckPoint(s, +RailLateral, 0.0), yaw = yaw });
                list.Add(new PostPlacement { basePos = DeckPoint(s, -RailLateral, 0.0), yaw = yaw });
            }
            PierPadOutline.AppendPosts(list, ThreadConstants.HandrailDiameter * 0.5);
            return list;
        }

        public static int HelixPostStations => (int)Math.Round((ThreadConstants.HelixEndS - ThreadConstants.HelixStartS) / ThreadConstants.PostSpacing);

        /// <summary>Chamfered post box in post space: base on the deck top at y = 0, top at the handrail centre.</summary>
        public static void BuildPost(MeshBuilder mb)
        {
            double y0 = -PostEmbed, y1 = RailCentreHeight;
            MeshKit.ChamferedBox(mb, new Vector3(0f, (float)(0.5 * (y0 + y1)), 0f),
                                 new Vector3((float)ThreadConstants.PostSize, (float)(y1 - y0), (float)ThreadConstants.PostSize),
                                 (float)PostChamfer, Quaternion.identity, BeatMeshUtil.Ao(1.0));
        }

        public static Matrix4x4 PostMatrix(PostPlacement p)
        {
            return Matrix4x4.TRS(BeatMeshUtil.V(p.basePos), Quaternion.Euler(0f, (float)(p.yaw / ThreadMath.Deg), 0f), Vector3.one);
        }

        // ---------------------------------------------------------------- support arms (14.5.5, 14.5.6 arm rule, 14.5.7)
        public static int ArmCount => ThreadConstants.ArmCountHelix + ThreadConstants.ArmCountPier;
        public static double ArmStation(int i) => ThreadConstants.HelixStartS + ThreadConstants.ArmSpacing * i;

        static ArmSpec[] s_arms;

        /// <summary>The 43 support arms (40 on the helix at s = 400 + 11 i, 3 under the pier at z = 400, 411, 422).</summary>
        public static ArmSpec[] Arms()
        {
            if (s_arms != null) return s_arms;
            var arms = new ArmSpec[ArmCount];
            for (int i = 0; i < arms.Length; i++) arms[i] = ComputeArm(i);
            s_arms = arms;
            return arms;
        }

        static ArmSpec ComputeArm(int i)
        {
            double s = ArmStation(i);
            double refX, refZ, deckTop;
            bool pier = i >= ThreadConstants.ArmCountHelix;
            if (!pier) { D3 p = ThreadMath.PathPoint(s); refX = p.x; refZ = p.z; deckTop = p.y; }
            else { refX = 0.0; refZ = ThreadConstants.PierStartZ + (s - ThreadConstants.HelixEndS); deckTop = ThreadConstants.TopY; }
            double dx = refX - ThreadConstants.CableAxisX, dz = refZ - ThreadConstants.CableAxisZ;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            var a = new ArmSpec
            {
                index = i, s = s, dirX = dx / dist, dirZ = dz / dist,
                deckTopY = deckTop, yTop = deckTop - ArmTopBelowDeckTop,
                depthRoot = ThreadConstants.ArmDepth, depthTip = ArmTipDepth,
                widthRoot = ArmRootWidth, widthTip = ArmTipWidth, bead = -1,
            };
            a.rTip = dist + ArmTipBeyondCentre;
            if (pier) a.rTip = PierArmTip(a, dist);
            RootRadius(ref a);
            return a;
        }

        /// <summary>Pier arms run under the pier like the helix arms; one that would meet the pad boss stops 0.4 m inside the slab edge.</summary>
        static double PierArmTip(ArmSpec a, double dist)
        {
            double candidate = dist + ArmTipBeyondCentre;
            double bossLimit = PierPadOutline.BossRadius + 0.5 * a.widthTip + ArmBossClearance;
            if (DistanceToPadCentre(a, candidate) >= bossLimit) return candidate;
            // first radius (from the cable outward) where the centre line enters the slab outline
            double rEntry = dist - 6.0;
            for (double r = dist - 6.0; r < dist + 6.0; r += 0.005)
            {
                D3 q = a.Point(r, 0, 0);
                if (PierPadOutline.Contains(q.x, q.z)) { rEntry = r; break; }
            }
            return rEntry + PierArmTipInset;
        }

        static double DistanceToPadCentre(ArmSpec a, double r)
        {
            D3 q = a.Point(r, 0, 0);
            double ex = q.x, ez = q.z - ThreadConstants.PadCentreZ;
            return Math.Sqrt(ex * ex + ez * ez);
        }

        /// <summary>
        /// Arm rule (14.5.6): the root ends inside the cable or inside a bead lens, ArmEmbed (0.3 m) below the
        /// surface at every point of the root face. The surface radius is max(cable core 50.0, faceted lens
        /// radius); using the core radius (not the 51.2 crest) keeps the root buried even in a strand valley
        /// and under the relief fade, and the visible end is where the beam meets the real surface.
        /// Under D-035 no bead lens reaches deck height (the lowest lens point is above the pier), so every arm
        /// root ends in the cable core: the root face corners at r = 50 - 0.3 = 49.7, bead = -1.
        /// </summary>
        static void RootRadius(ref ArmSpec a)
        {
            double rr = ThreadConstants.CableRidgeCrest;
            int bead = -1;
            for (int iter = 0; iter < 3; iter++)
            {
                double best = double.MaxValue;
                bead = -1;
                for (int yi = 0; yi <= 4; yi++)
                {
                    double y = a.yTop - a.depthRoot * yi / 4.0;
                    for (int li = -1; li <= 1; li++)
                    {
                        double lat = li * 0.5 * a.widthRoot;
                        D3 q = a.Point(rr, lat, y);
                        double az = BeatMeshUtil.Azimuth(q.x - ThreadConstants.CableAxisX, q.z - ThreadConstants.CableAxisZ);
                        double lens = LensBead.SurfaceRadius(y, az, out int k);
                        double surf = Math.Max(ThreadConstants.CableRadius, lens);
                        double lim = surf - ArmEmbed;
                        double r = Math.Sqrt(Math.Max(0.0, lim * lim - lat * lat));
                        if (r < best) { best = r; bead = lens > ThreadConstants.CableRadius ? k : -1; }
                    }
                }
                rr = best;
            }
            a.rRoot = rr;
            a.bead = bead;
        }

        /// <summary>Adds arms [from, to) as lofted, chamfered, tapered beams with flat faces (edges catch the light).</summary>
        public static void BuildArms(MeshBuilder mb, int from, int to)
        {
            var arms = Arms();
            var corners = new D3[2, 8];
            var pts2 = new double[8, 2];
            var cap = new List<Vector3>(8);
            for (int i = Math.Max(0, from); i < Math.Min(arms.Length, to); i++)
            {
                var a = arms[i];
                for (int sec = 0; sec < 2; sec++)
                {
                    double r = sec == 0 ? a.rRoot : a.rTip;
                    double hw = 0.5 * a.WidthAt(r), yt = a.yTop, yb = a.yTop - a.DepthAt(r), ch = ArmChamfer;
                    // CCW in (lateral, y)
                    pts2[0, 0] = -hw + ch; pts2[0, 1] = yb;
                    pts2[1, 0] = hw - ch;  pts2[1, 1] = yb;
                    pts2[2, 0] = hw;       pts2[2, 1] = yb + ch;
                    pts2[3, 0] = hw;       pts2[3, 1] = yt - ch;
                    pts2[4, 0] = hw - ch;  pts2[4, 1] = yt;
                    pts2[5, 0] = -hw + ch; pts2[5, 1] = yt;
                    pts2[6, 0] = -hw;      pts2[6, 1] = yt - ch;
                    pts2[7, 0] = -hw;      pts2[7, 1] = yb + ch;
                    for (int c = 0; c < 8; c++) corners[sec, c] = a.Point(r, pts2[c, 0], pts2[c, 1]);
                }
                Color cRoot = BeatMeshUtil.Ao(0.78), cTip = BeatMeshUtil.Ao(1.0);
                for (int e = 0; e < 8; e++)
                {
                    int e1 = (e + 1) % 8;
                    D3 r0 = corners[0, e], r1 = corners[0, e1], t0 = corners[1, e], t1 = corners[1, e1];
                    // outward profile normal (2D CCW: (dy, -dlat)) mapped to 3D
                    D3 d2 = corners[0, e1] - corners[0, e];
                    double dl = D3.Dot(d2, new D3(a.TanX, 0, a.TanZ)), dy = d2.y;
                    D3 refN = new D3(a.TanX * dy, -dl, a.TanZ * dy);
                    D3 fn = D3.Cross(r1 - r0, t0 - r0);
                    if (fn.Length < 1e-12) fn = D3.Cross(t1 - r1, t0 - r1);
                    fn = fn.Normalized;
                    if (D3.Dot(fn, refN) < 0) fn = fn * -1.0;
                    Vector3 n = BeatMeshUtil.V(fn);
                    float la = (float)a.rRoot, lb = (float)a.rTip;
                    int i0 = mb.Add(BeatMeshUtil.V(r0), n, new Vector2(la, e), cRoot);
                    int i1 = mb.Add(BeatMeshUtil.V(r1), n, new Vector2(la, e + 1), cRoot);
                    int i2 = mb.Add(BeatMeshUtil.V(t1), n, new Vector2(lb, e + 1), cTip);
                    int i3 = mb.Add(BeatMeshUtil.V(t0), n, new Vector2(lb, e), cTip);
                    BeatMeshUtil.QuadFacing(mb, i0, i1, i2, i3, n);
                }
                for (int sec = 0; sec < 2; sec++)
                {
                    cap.Clear();
                    for (int c = 0; c < 8; c++) cap.Add(BeatMeshUtil.V(corners[sec, c]));
                    var dn = new Vector3((float)a.dirX, 0f, (float)a.dirZ);
                    MeshKit.ConvexPolygon(mb, cap, sec == 0 ? -dn : dn, sec == 0 ? cRoot : cTip);
                }
            }
        }
    }
}
