using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat-4 geometry (MASTER_PROMPT 14.5.4): the terraced levee (x -70..18, z 558..640, 3 terraces of 14/3 m with
    /// 3 m setbacks on all four sides; coursed limestone risers (Beat34Mat.LeveeStone), ceramic copings; each setback is a
    /// LeveeGreen turf berm rising 1 m from the coping to a 1.2 m level ledge at the foot of the next riser, so the turf
    /// reads from the path below (a level terrace top above eye height never would); the top terrace is level turf; the
    /// south face carries a zigzag stair (three flights against the risers, ceramic cheek walls with PaleGold caps) and
    /// eight WarmLamp sconces with wall washes and ledge pools: the human-scale cues of KS06), the sealed L-shaped underpass (rounded
    /// rectangle 2.8 x 2.9, legs 18 + 18 m, mitred corner at (0, 576), ribs every 3 m, amber strips), the portal
    /// frames (PaleGold lintels, the lit lamp on its bracket in front of the mouth lintel) and the 12 beat-4 bollards
    /// with light pools.
    /// The underpass lining bakes per vertex: R = ambient occlusion, B = the amber strip irradiance, A = exterior
    /// exposure (1 at the portals, 0 from 2.2 m inside): the RingUnderpass shader scales the directional lights by A,
    /// so no sky light leaks inside and the world swap at the corner (s 576 = the mitre diagonal x + z = 576) changes
    /// nothing in the lighting of the tube itself. It does not hide the portals: both legs are straight (14.5.4), so
    /// from the mitre the exit portal (x 18) is in a 16:9 / vFOV 60 frame whenever the camera yaw is >= 39 deg (16:10
    /// 42, 21:9 31; the AutoWalker crosses the mitre at yaw ~67 deg, the portal then covers ~1.4 % of the frame) and the
    /// mouth when looking back. What is seen through them must not change at the swap (BeatDirector / AtmosphereDriver).
    /// The lining is one closed tube from the mouth plane (z 558) to the exit plane (x 18); its end rows are exactly the
    /// hole polygons of the terrace-0 risers, so the levee shell and the tube form one sealed surface.
    /// </summary>
    public static class LeveeMeshes
    {
        // levee
        const float X0 = RingConstants.LeveeX0, X1 = RingConstants.LeveeX1, Z0 = RingConstants.LeveeZ0, Z1 = RingConstants.LeveeZ1;
        public const int Terraces = 3;
        public const float Tier = RingConstants.LeveeH / Terraces;   // 14/3 m ("3 terraces of 4.7 m (14 m)", 14.5.4)
        public const float Setback = 3f;                             // 14.5.4
        const float Bury = 0.3f, StepBury = 0.1f;
        const float CopingW = 0.35f, CopingH = 0.12f, CopingProud = 0.05f, CopingDrop = 0.07f, CopingSeg = 8f;
        const float Cell = 6f;                                       // max face cell (bend interpolation error < 0.3 mm)
        public const float Berm = 1.0f;                              // turf berm rise from the coping to the ledge
        public const float Ledge = 1.2f;                             // level ledge along the foot of each riser (stairs, sconces)
        const float BermEdge = 0.02f;                                // turf meets the coping 2 cm below the terrace line (hidden)
        // stair (south face, x -3.6..-11.4): going 0.3 m, rise <= 0.18 m, 1.15 m wide; cheek wall 0.18 m, 0.9 m above the nosings
        const float StairGoing = 0.3f, StairRiseMax = 0.18f, StairW = 1.15f, CheekT = 0.18f, CheekH = 0.9f, CapH = 0.07f;
        // sconces: lens (Lamp34) under a PaleGold housing on a wall plate, 0.16 m out from the riser. Their wash on the
        // stone, the copings, the cheek walls and the turf ledges is lit in the RingUnderpass shader from a virtual
        // emitting point SconceVirtual in front of the riser (gain SconceGain); only the ground in front of riser 0 (the
        // terrain shader) gets an additive pool (SconceFloor x WarmLampDisplay).
        const float SconceOut = 0.16f, SconceFloor = 0.16f;
        public const float SconceVirtual = 0.8f, SconceGain = 0.75f;
        // stair rail lights: WarmLampDisplay points on the cheek caps every third step (RingMat.Glow, lamp channel)
        const float StepLightR = 0.06f, StepLightI = 2.0f;
        static readonly Vector2[] Sconces =
        {
            // (x, riser index): two framing the stair on every riser, a third further west on the upper two
            new Vector2(-13.5f, 0f), new Vector2(9f, 0f),
            new Vector2(-13.5f, 1f), new Vector2(9f, 1f), new Vector2(-36f, 1f),
            new Vector2(-13.5f, 2f), new Vector2(9f, 2f), new Vector2(-36f, 2f),
        };

        // underpass
        public const float PathTop = 0.012f;                         // path strip top (14.5.2: +0.012 m)
        public const float FloorY = PathTop + 0.006f;                // underpass floor 6 mm above it: never co-planar with a strip
        const float SillTop = 0.006f, SillDepth = 0.3f, SillHalfW = 1.45f;   // threshold stones over the floor edges
        public const float UW = RingConstants.UnderpassW, UH = RingConstants.UnderpassH;
        const float TopR = 0.45f, CoveR = 0.12f;
        public static float MouthZ => RingConstants.Waypoints[4].y;  // 558
        public static Vector2 Corner => RingConstants.Waypoints[5];  // (0, 576)
        public static float ExitX => RingConstants.Waypoints[6].x;   // 18
        const float FrameHalfW = 1.9f, FrameTop = 3.4f;              // portal frame ring rectangle (face-local)
        const float RibW = 0.2f, RibD = 0.09f, RibCh = 0.015f;
        const int RibFirst = 7, RibLast = 33;                        // section indices of the rib ends (walls at y 0.32)
        const float StripY0 = 0.22f, StripY1 = 0.28f, StripDepth = 0.025f, StripEmbed = 0.005f;

        // bollards (beat 4): s 411..552, 12 bollards (14.5.4); right of the path so KS05 (yaw -58) never cuts one
        public const int BollardCount = 12;
        public const float BollardS0 = 411f, BollardS1 = 552f, BollardX = 2.6f, BollardH = 1.0f, BollardTop = 0.19f;
        public const float PoolR = Beat34Additive.PoolRadius;        // 3 m (14.2.3)
        const float StripHalf = 1.5f, KerbOuter = 1.64f;             // 3.0 m path strip + 0.14 m kerbs (14.5.2)
        const float KerbTop = PathTop + 0.05f;                       // kerb top (the beat-2 path kerbs rise 0.05 m above the strip)
        const float PoolBollard = 0.2f, PoolMouth = 0.26f;           // pool gain x WarmLampDisplay at lamp level 3 (art values)
        const float BollardLampY = 0.9035f;                          // bollard lens centre above the ground

        // mouth lintel (PaleGold): centre FloorY + LintelCy, MouthZ - LintelCz; size LintelW x LintelH x LintelD
        const float LintelCy = 3.17f, LintelCz = 0.165f, LintelW = 3.9f, LintelH = 0.38f, LintelD = 0.35f;
        // mouth lamp: lens (emissive, LensW x LensH x LensD) under a PaleGold housing, carried by an arm from a wall plate
        // above the lintel; the lens sits 0.10 m in front of the lintel face, so its light reaches the strip, the
        // threshold and (past the lintel's front-bottom edge, with a baked soft shadow) the first metres of the floor
        const float MouthLampY = 3.5575f;                            // lens centre (world y; its bottom face at 3.53)
        const float MouthLampDz = 0.55f;                             // lens centre in front of the mouth plane (lintel face 0.34)
        const float LensW = 0.38f, LensH = 0.055f, LensD = 0.22f;
        static float MouthLampZ => MouthZ - MouthLampDz;             // 557.45

        struct SecPt { public Vector2 p, n; public float ao; }

        // ================================================================ section
        static List<SecPt> Section()
        {
            var s = new List<SecPt>(40);
            float hw = 0.5f * UW;
            void P(float x, float y, Vector2 n, float ao) => s.Add(new SecPt { p = new Vector2(x, y), n = n, ao = ao });
            void Arc(Vector2 c, float r, float a0, float a1, int steps, float ao)
            {
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    s.Add(new SecPt { p = c + d * r, n = -d, ao = ao });
                }
            }
            P(0f, 0f, Vector2.up, 1f); P(0.45f, 0f, Vector2.up, 1f); P(0.9f, 0f, Vector2.up, 1f);
            Arc(new Vector2(hw - CoveR, CoveR), CoveR, -90f, 0f, 3, 0.8f);
            foreach (float y in new[] { 0.32f, 0.7f, 1.2f, 1.7f, 2.2f }) P(hw, y, Vector2.left, 1f);
            Arc(new Vector2(hw - TopR, UH - TopR), TopR, 0f, 90f, 6, 0.9f);
            P(0.5f, UH, Vector2.down, 0.95f); P(0f, UH, Vector2.down, 0.95f); P(-0.5f, UH, Vector2.down, 0.95f);
            Arc(new Vector2(-hw + TopR, UH - TopR), TopR, 90f, 180f, 6, 0.9f);
            foreach (float y in new[] { 2.2f, 1.7f, 1.2f, 0.7f, 0.32f }) P(-hw, y, Vector2.right, 1f);
            Arc(new Vector2(-hw + CoveR, CoveR), CoveR, 180f, 270f, 3, 0.8f);
            P(-0.9f, 0f, Vector2.up, 1f); P(-0.45f, 0f, Vector2.up, 1f);
            return s;
        }

        /// <summary>Baked amber strip irradiance (0..1) for a section point with inward normal n.</summary>
        static float StripLight(Vector2 p, Vector2 n)
        {
            float hw = 0.5f * UW - 0.5f * StripDepth;
            float ys = 0.5f * (StripY0 + StripY1);
            float e = 0f;
            for (int k = -1; k <= 1; k += 2)
            {
                var sp = new Vector2(k * hw, ys);
                var emit = new Vector2(-k * 0.6f, -0.8f);   // guide strips light the floor more than the ceiling
                Vector2 v = sp - p;
                float d = v.magnitude;
                if (d < 1e-3f) { e += 1f; continue; }
                Vector2 l = v / d;
                float cr = Mathf.Max(0f, Vector2.Dot(n, l));
                float ce = Mathf.Max(0f, Vector2.Dot(emit, -l));
                e += cr * (0.3f + 0.7f * ce) / (d + 0.25f);
            }
            return Mathf.Clamp01(0.12f + e / 0.9f);
        }

        static float Ao(float d) => 0.14f + 0.86f * Mathf.Exp(-d / 1.6f);
        static float Expo(float d) => 1f - RingMath.Smoothstep(0f, 2.2f, d);

        // world mapping of the two legs; section x maps to the walker's right (+X on leg 1, -Z on leg 2)
        static Vector3 MapLeg1(Vector2 sp, float z) => new Vector3(Corner.x + sp.x, FloorY + sp.y, z);
        static Vector3 MapMitre(Vector2 sp) => new Vector3(Corner.x + sp.x, FloorY + sp.y, Corner.y - sp.x);
        static Vector3 MapLeg2(Vector2 sp, float x) => new Vector3(x, FloorY + sp.y, Corner.y - sp.x);
        static Vector3 N1(Vector2 n) => new Vector3(n.x, n.y, 0f);
        static Vector3 N2(Vector2 n) => new Vector3(0f, n.y, -n.x);

        static float DistLeg1(float z) => Mathf.Min(z - MouthZ, (Corner.y - z) + (ExitX - Corner.x));
        static float DistLeg2(float x) => Mathf.Min(ExitX - x, (x - Corner.x) + (Corner.y - MouthZ));

        // ================================================================ underpass lining
        public static MeshBuilder Lining(MeshBuilder b)
        {
            var sec = Section();
            int ns = sec.Count;
            float m0 = MouthZ, c = Corner.y;
            var z1 = new List<float> { m0, m0 + 0.35f, m0 + 0.8f, m0 + 1.5f, m0 + 2.5f, m0 + 3.5f };
            for (float z = m0 + 5f; z < c - 2.3f; z += 1.5f) z1.Add(z);
            z1.Add(c - 1.7f);
            var x2 = new List<float> { Corner.x + 1.7f, Corner.x + 2.5f };
            for (float x = Corner.x + 4f; x < ExitX - 2.2f; x += 1.5f) x2.Add(x);
            x2.Add(ExitX - 1.5f); x2.Add(ExitX - 0.8f); x2.Add(ExitX - 0.35f); x2.Add(ExitX);

            // leg 1 rows + mitre (leg-1 normals)
            var rows1 = new List<int>();
            foreach (float z in z1) rows1.Add(Row(b, sec, i => MapLeg1(sec[i].p, z), i => N1(sec[i].n), i => DistLeg1(z)));
            rows1.Add(Row(b, sec, i => MapMitre(sec[i].p), i => N1(sec[i].n), i => DistLeg1(c - sec[i].p.x)));
            // mitre (leg-2 normals) + leg 2 rows
            var rows2 = new List<int>();
            rows2.Add(Row(b, sec, i => MapMitre(sec[i].p), i => N2(sec[i].n), i => DistLeg2(Corner.x + sec[i].p.x)));
            foreach (float x in x2) rows2.Add(Row(b, sec, i => MapLeg2(sec[i].p, x), i => N2(sec[i].n), i => DistLeg2(x)));
            Stitch(b, rows1, ns);
            Stitch(b, rows2, ns);
            return b;
        }

        static int Row(MeshBuilder b, List<SecPt> sec, System.Func<int, Vector3> pos, System.Func<int, Vector3> nrm, System.Func<int, float> dist)
        {
            int first = b.V.Count;
            for (int i = 0; i < sec.Count; i++)
            {
                float d = Mathf.Max(0f, dist(i));
                var col = Beat34Geo.Col(Ao(d) * sec[i].ao, 0f, StripLight(sec[i].p, sec[i].n), Expo(d));
                b.Vtx(pos(i), nrm(i), new Vector2(sec[i].p.x, sec[i].p.y), col);
            }
            return first;
        }

        static void Stitch(MeshBuilder b, List<int> rows, int ns)
        {
            for (int r = 0; r < rows.Count - 1; r++)
                for (int i = 0; i < ns; i++)
                {
                    int i1 = (i + 1) % ns;
                    int a0 = rows[r] + i, a1 = rows[r] + i1, b0 = rows[r + 1] + i, b1 = rows[r + 1] + i1;
                    Vector3 d = b.N[a0] + b.N[a1] + b.N[b0] + b.N[b1];
                    Beat34Geo.QuadO(b, a0, a1, b1, b0, d);
                }
        }

        // ================================================================ ribs
        public static MeshBuilder Ribs(MeshBuilder b)
        {
            var sec = Section();
            float m0 = MouthZ;
            for (int k = 1; k <= 5; k++) Rib(b, sec, 1, m0 + 3f * k);                 // leg 1: z 561 .. 573
            for (int k = 1; k <= 5; k++) Rib(b, sec, 2, Corner.x + 3f * k);           // leg 2: x 3 .. 15
            return b;
        }

        static void Rib(MeshBuilder b, List<SecPt> sec, int leg, float along)
        {
            Vector3 T = leg == 1 ? Vector3.forward : Vector3.right;
            float dist = leg == 1 ? DistLeg1(along) : DistLeg2(along);
            // (along offset, depth) of the five strips' edges: side A, chamfer A, inner face, chamfer B, side B
            float a0 = along - 0.5f * RibW, a1 = along + 0.5f * RibW;
            var ea = new[] { a0, a0, a0 + RibCh, a1 - RibCh, a1, a1 };
            var ed = new[] { -0.01f, RibD - RibCh, RibD, RibD, RibD - RibCh, -0.01f };
            Vector3 W(int j, int e)
            {
                Vector2 p = sec[j].p + sec[j].n * ed[e];
                return leg == 1 ? MapLeg1(p, ea[e]) : MapLeg2(p, ea[e]);
            }
            Vector3 NS(int j) => leg == 1 ? N1(sec[j].n) : N2(sec[j].n);
            for (int strip = 0; strip < 5; strip++)
            {
                int first = b.V.Count;
                for (int j = RibFirst; j <= RibLast; j++)
                {
                    Vector3 n;
                    switch (strip)
                    {
                        case 0: n = -T; break;
                        case 1: n = (NS(j) - T).normalized; break;
                        case 2: n = NS(j); break;
                        case 3: n = (NS(j) + T).normalized; break;
                        default: n = T; break;
                    }
                    Vector2 pIn = sec[j].p + sec[j].n * RibD;
                    var col = Beat34Geo.Col(Ao(dist) * 0.95f, strip == 1 || strip == 3 ? 1f : 0f, StripLight(pIn, sec[j].n), Expo(dist));
                    b.Vtx(W(j, strip), n, new Vector2(ea[strip], j), col);
                    b.Vtx(W(j, strip + 1), n, new Vector2(ea[strip + 1], j), col);
                }
                int cnt = RibLast - RibFirst + 1;
                for (int j = 0; j < cnt - 1; j++)
                {
                    int a = first + 2 * j, c = first + 2 * (j + 1);
                    Beat34Geo.QuadO(b, a, c, c + 1, a + 1, b.N[a] + b.N[c]);
                }
            }
            // end caps (the rib ends 0.32 m above the floor face downward along the wall)
            for (int e = 0; e < 2; e++)
            {
                int j = e == 0 ? RibFirst : RibLast;
                int jn = e == 0 ? RibFirst + 1 : RibLast - 1;
                Vector2 t2 = sec[j].p - sec[jn].p;
                Vector3 nc = leg == 1 ? new Vector3(t2.x, t2.y, 0f).normalized : new Vector3(0f, t2.y, -t2.x).normalized;
                var ids = new int[6];
                var col = Beat34Geo.Col(Ao(dist) * 0.8f, 0f, StripLight(sec[j].p, sec[j].n), Expo(dist));
                for (int k = 0; k < 6; k++) ids[k] = b.Vtx(W(j, k), nc, new Vector2(ea[k], ed[k]), col);
                for (int k = 1; k < 5; k++) Beat34Geo.TriO(b, ids[0], ids[k], ids[k + 1], nc);
            }
        }

        // ================================================================ amber strips
        public static MeshBuilder Strips(MeshBuilder b)
        {
            float hw = 0.5f * UW, ys = FloorY + 0.5f * (StripY0 + StripY1), hy = StripY1 - StripY0;
            float xc = Corner.x, zc = Corner.y;
            float zA = MouthZ + 0.35f, xB = ExitX - 0.35f;
            float inX = hw + StripEmbed - 0.5f * (StripDepth + StripEmbed);   // centre offset from the axis
            float dd = StripDepth + StripEmbed;
            var col = Beat34Geo.Col(1f);
            const float ch = 0.004f;
            // leg 1 right (x +1.4) ends at the inner corner, leg 1 left (x -1.4) runs to the outer wall
            BoxSpan(b, new Vector3(xc + inX, ys, zA), new Vector3(xc + inX, ys, zc - hw - 0.02f), dd, hy, ch, col, true);
            BoxSpan(b, new Vector3(xc - inX, ys, zA), new Vector3(xc - inX, ys, zc + hw - 0.02f), dd, hy, ch, col, true);
            // leg 2 right (z 574.6) from the inner corner, leg 2 left (z 577.4) from the outer corner
            BoxSpan(b, new Vector3(xc + hw + 0.02f, ys, zc - inX), new Vector3(xB, ys, zc - inX), dd, hy, ch, col, false);
            BoxSpan(b, new Vector3(xc - hw + 0.04f, ys, zc + inX), new Vector3(xB, ys, zc + inX), dd, hy, ch, col, false);
            return b;
        }

        static void BoxSpan(MeshBuilder b, Vector3 a, Vector3 c, float depth, float height, float ch, Color col, bool alongZ)
        {
            Vector3 centre = 0.5f * (a + c);
            float len = (c - a).magnitude;
            Vector3 size = alongZ ? new Vector3(depth, height, len) : new Vector3(len, height, depth);
            Beat34Geo.Box(b, centre, size, ch, col);
        }

        // ================================================================ portal frames and the mouth lamp
        /// <summary>Jambs (ceramic) and PaleGold lintels at both portals; the mouth lamp (PaleGold wall plate, arm and
        /// housing, RingMat.Emissive lens) in front of the mouth lintel. Builders: ceramic, gold, lens.</summary>
        public static void Portals(MeshBuilder ceramic, MeshBuilder gold, MeshBuilder lens)
        {
            var col = Beat34Geo.Col(1f);
            float m = MouthZ;
            // mouth (face z = 558, outward -Z)
            for (int s = -1; s <= 1; s += 2)
                Beat34Geo.Box(ceramic, new Vector3(Corner.x + s * 1.6f, 0.5f * (-Bury + 3.005f), m - 0.095f), new Vector3(0.36f, 3.005f + Bury, 0.21f), 0.02f, col);
            Beat34Geo.Box(gold, new Vector3(Corner.x, FloorY + LintelCy, m - LintelCz), new Vector3(LintelW, LintelH, LintelD), 0.02f, col);
            // the lamp: wall plate on the riser above the lintel (1 cm into the face), an arm over the lintel, the
            // housing and its lens 0.10 m in front of the lintel face (lens z 557.34..557.56, lintel face 557.66)
            float zl = MouthLampZ;
            float hz0 = zl - 0.155f, hz1 = zl + 0.155f;                 // housing 0.46 x 0.2 x 0.31, y 3.58..3.78
            Beat34Geo.Box(gold, new Vector3(Corner.x, 3.70f, m - 0.015f), new Vector3(0.24f, 0.30f, 0.05f), 0.012f, col);
            float a0 = hz1 - 0.025f, a1 = m - 0.01f;                      // arm ends inside the housing and the plate
            Beat34Geo.Box(gold, new Vector3(Corner.x, 3.70f, 0.5f * (a0 + a1)), new Vector3(0.08f, 0.08f, a1 - a0), 0.01f, col);
            Beat34Geo.Box(gold, new Vector3(Corner.x, 3.68f, 0.5f * (hz0 + hz1)), new Vector3(0.46f, 0.2f, hz1 - hz0), 0.015f, col);
            Beat34Geo.Box(lens, new Vector3(Corner.x, MouthLampY, zl), new Vector3(LensW, LensH, LensD), 0.008f, col);
            // exit (face x = 18, outward +X)
            float e = ExitX;
            for (int s = -1; s <= 1; s += 2)
                Beat34Geo.Box(ceramic, new Vector3(e + 0.095f, 0.5f * (-Bury + 3.005f), Corner.y + s * 1.6f), new Vector3(0.21f, 3.005f + Bury, 0.36f), 0.02f, col);
            Beat34Geo.Box(gold, new Vector3(e + LintelCz, FloorY + LintelCy, Corner.y), new Vector3(LintelD, LintelH, LintelW), 0.02f, col);
            // threshold stones: cover the floor edge in the portal planes (the strip-to-floor step), ends inside the jambs
            float sy0 = -0.04f, sy1 = FloorY + SillTop;
            Beat34Geo.Box(ceramic, new Vector3(Corner.x, 0.5f * (sy0 + sy1), m), new Vector3(2f * SillHalfW, sy1 - sy0, SillDepth), 0.008f, col);
            Beat34Geo.Box(ceramic, new Vector3(e, 0.5f * (sy0 + sy1), Corner.y), new Vector3(SillDepth, sy1 - sy0, 2f * SillHalfW), 0.008f, col);
        }

        // ================================================================ levee
        /// <summary>Risers (CeramicShade, two with portal holes), copings (ceramic) and turf tops.</summary>
        public static void Levee(MeshBuilder risers, MeshBuilder copings, MeshBuilder turf)
        {
            var hole = new List<Vector2>();
            foreach (var p in Section()) hole.Add(new Vector2(p.p.x, FloorY + p.p.y));
            for (int k = 0; k < Terraces; k++)
            {
                // 3 m setbacks on every side (14.5.4); the mouth (terrace 0) stays in the plane z 558 (KS06: the top
                // terrace front at z 564 tops out at +11.8 deg instead of +13, 27 % of the frame instead of 29 %)
                float x0 = X0 + Setback * k, x1 = X1 - Setback * k, z0 = Z0 + Setback * k, z1 = Z1 - Setback * k;
                float vb = k == 0 ? -Bury : k * Tier - StepBury, vt = (k + 1) * Tier - 0.02f;
                // south (outward -Z, u = x); the mouth hole on terrace 0
                if (k == 0) FaceWithHole(risers, new Vector3(0f, 0f, z0), Vector3.right, Vector3.back, x0, x1, vb, vt, hole, Corner.x, 0, 1);
                else PlainFace(risers, new Vector3(0f, 0f, z0), Vector3.right, Vector3.back, x0, x1, vb, vt, 0, 1);
                // north (outward +Z)
                PlainFace(risers, new Vector3(0f, 0f, z1), Vector3.right, Vector3.forward, x0, x1, vb, vt, 0, 1);
                // west (outward -X, u = z)
                PlainFace(risers, new Vector3(x0, 0f, 0f), Vector3.forward, Vector3.left, z0, z1, vb, vt, 2, 1);
                // east (outward +X, u = corner z - z so the hole maps like the leg-2 section); the exit hole on terrace 0
                var o = new Vector3(x1, 0f, Corner.y);
                if (k == 0) FaceWithHole(risers, o, Vector3.back, Vector3.right, Corner.y - z1, Corner.y - z0, vb, vt, hole, 0f, 2, 1);
                else PlainFace(risers, o, Vector3.back, Vector3.right, Corner.y - z1, Corner.y - z0, vb, vt, 2, 1);
                Copings(copings, k, x0, x1, z0, z1);
                Turf(turf, k, x0, x1, z0, z1);
            }
        }

        static Color FaceCol(float u, float v) => Beat34Geo.Col(v < 0.2f ? 0.82f : 1f);

        static void PlainFace(MeshBuilder b, Vector3 o, Vector3 U, Vector3 n, float u0, float u1, float vb, float vt, int uvU, int uvV)
        {
            Beat34Geo.Grid(b, o, U, Vector3.up, Beat34Geo.Steps(u0, u1, Cell), new List<float> { vb, vt }, n, FaceCol, uvU, uvV);
        }

        /// <summary>A riser face with a portal hole (the underpass section, face-local u = section x, v = height): a ring
        /// of quads between the hole and the frame rectangle (rays from the hole centre, rectangle corners inserted),
        /// then three rectangles around it sharing every boundary vertex (no T-junctions).</summary>
        static void FaceWithHole(MeshBuilder b, Vector3 o, Vector3 U, Vector3 n, float u0, float u1, float vb, float vt,
                                 List<Vector2> hole, float uc, int uvU, int uvV)
        {
            Vector3 V = Vector3.up;
            Vector2 C = new Vector2(uc, FloorY + 0.5f * UH);
            float rl = uc - FrameHalfW, rr = uc + FrameHalfW, rb = vb, rt = FloorY + FrameTop;
            var cornerPts = new[] { new Vector2(rr, rb), new Vector2(rr, rt), new Vector2(rl, rt), new Vector2(rl, rb) };
            // unwrapped increasing angles (the section is counter-clockwise, starting at the floor centre)
            int nh = hole.Count;
            var ang = new float[nh + 1];
            float prev = Mathf.Atan2(hole[0].y - C.y, hole[0].x - C.x);
            ang[0] = prev;
            for (int i = 1; i <= nh; i++)
            {
                Vector2 h = hole[i % nh];
                float a = Mathf.Atan2(h.y - C.y, h.x - C.x);
                while (a < prev) a += 2f * Mathf.PI;
                ang[i] = a; prev = a;
            }
            var cornerAng = new float[4];
            for (int c = 0; c < 4; c++)
            {
                float a = Mathf.Atan2(cornerPts[c].y - C.y, cornerPts[c].x - C.x);
                while (a < ang[0]) a += 2f * Mathf.PI;
                cornerAng[c] = a;
            }
            var H = new List<Vector2>(); var R = new List<Vector2>();
            for (int i = 0; i < nh; i++)
            {
                Vector2 p = hole[i], q = hole[(i + 1) % nh];
                H.Add(p); R.Add(RayRect(C, ang[i], rl, rr, rb, rt));
                for (int c = 0; c < 4; c++)
                {
                    float a = cornerAng[c];
                    if (a <= ang[i] + 1e-5f || a >= ang[i + 1] - 1e-5f) continue;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    H.Add(RaySeg(C, dir, p, q));
                    R.Add(cornerPts[c]);
                }
            }
            int m = H.Count;
            int baseIdx = b.V.Count;
            for (int k = 0; k < m; k++)
            {
                Vector3 ph = o + U * H[k].x + V * H[k].y, pr = o + U * R[k].x + V * R[k].y;
                b.Vtx(ph, n, new Vector2(ph[uvU], ph[uvV]), Beat34Geo.Col(0.9f));
                b.Vtx(pr, n, new Vector2(pr[uvU], pr[uvV]), FaceCol(R[k].x, R[k].y));
            }
            for (int k = 0; k < m; k++)
            {
                int a = baseIdx + 2 * k, c = baseIdx + 2 * ((k + 1) % m);
                Beat34Geo.QuadO(b, a, c, c + 1, a + 1, n);
            }
            // rows/columns shared with the ring's frame rectangle
            var leftV = new List<float> { rb, rt, vt }; var rightV = new List<float> { rb, rt, vt }; var topU = new List<float> { rl, rr };
            for (int k = 0; k < m; k++)
            {
                if (Mathf.Abs(R[k].x - rl) < 1e-4f) AddUnique(leftV, R[k].y);
                if (Mathf.Abs(R[k].x - rr) < 1e-4f) AddUnique(rightV, R[k].y);
                if (Mathf.Abs(R[k].y - rt) < 1e-4f) AddUnique(topU, R[k].x);
            }
            leftV.Sort(); rightV.Sort(); topU.Sort();
            Beat34Geo.Grid(b, o, U, V, Beat34Geo.Steps(u0, rl, Cell), leftV, n, FaceCol, uvU, uvV);
            Beat34Geo.Grid(b, o, U, V, Beat34Geo.Steps(rr, u1, Cell), rightV, n, FaceCol, uvU, uvV);
            Beat34Geo.Grid(b, o, U, V, topU, new List<float> { rt, vt }, n, FaceCol, uvU, uvV);
        }

        static void AddUnique(List<float> l, float v)
        {
            for (int i = 0; i < l.Count; i++) if (Mathf.Abs(l[i] - v) < 1e-4f) return;
            l.Add(v);
        }

        static Vector2 RayRect(Vector2 c, float a, float rl, float rr, float rb, float rt)
        {
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float t = float.MaxValue;
            if (d.x > 1e-6f) t = Mathf.Min(t, (rr - c.x) / d.x);
            if (d.x < -1e-6f) t = Mathf.Min(t, (rl - c.x) / d.x);
            if (d.y > 1e-6f) t = Mathf.Min(t, (rt - c.y) / d.y);
            if (d.y < -1e-6f) t = Mathf.Min(t, (rb - c.y) / d.y);
            var p = c + d * t;
            p.x = Mathf.Clamp(p.x, rl, rr); p.y = Mathf.Clamp(p.y, rb, rt);
            return p;
        }

        static Vector2 RaySeg(Vector2 c, Vector2 d, Vector2 p, Vector2 q)
        {
            Vector2 e = q - p;
            float den = d.x * e.y - d.y * e.x;
            if (Mathf.Abs(den) < 1e-9f) return 0.5f * (p + q);
            Vector2 w = p - c;
            float s = (d.y * w.x - d.x * w.y) / den;   // parameter along p -> q
            return p + e * Mathf.Clamp01(s);
        }

        static void Copings(MeshBuilder b, int k, float x0, float x1, float z0, float z1)
        {
            float yc = (k + 1) * Tier - CopingDrop + 0.5f * CopingH;
            var col = Beat34Geo.Col(1f, 0f);
            const float ch = 0.015f;
            // south and north run over the corners; east and west butt against them
            SegmentedBox(b, new Vector3(x0 - CopingProud, yc, z0 - CopingProud + 0.5f * CopingW), new Vector3(x1 + CopingProud, yc, z0 - CopingProud + 0.5f * CopingW), CopingH, CopingW, ch, col, true);
            SegmentedBox(b, new Vector3(x0 - CopingProud, yc, z1 + CopingProud - 0.5f * CopingW), new Vector3(x1 + CopingProud, yc, z1 + CopingProud - 0.5f * CopingW), CopingH, CopingW, ch, col, true);
            float za = z0 - CopingProud + CopingW, zb = z1 + CopingProud - CopingW;
            SegmentedBox(b, new Vector3(x0 - CopingProud + 0.5f * CopingW, yc, za), new Vector3(x0 - CopingProud + 0.5f * CopingW, yc, zb), CopingH, CopingW, ch, col, false);
            SegmentedBox(b, new Vector3(x1 + CopingProud - 0.5f * CopingW, yc, za), new Vector3(x1 + CopingProud - 0.5f * CopingW, yc, zb), CopingH, CopingW, ch, col, false);
        }

        /// <summary>A coping run split into stones of at most CopingSeg metres (each its own chamfered box, so the bend
        /// never lifts a long box off the riser).</summary>
        static void SegmentedBox(MeshBuilder b, Vector3 a, Vector3 c, float h, float w, float ch, Color col, bool alongX)
        {
            float len = (c - a).magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / CopingSeg));
            Vector3 dir = (c - a) / len;
            float sl = len / n;
            for (int i = 0; i < n; i++)
            {
                Vector3 centre = a + dir * (sl * (i + 0.5f));
                Vector3 size = alongX ? new Vector3(sl - 0.004f, h, w) : new Vector3(w, h, sl - 0.004f);
                Beat34Geo.Box(b, centre, size, ch, col);
            }
        }

        /// <summary>Turf of terrace k: the level top on the last terrace, otherwise the setback frame as a berm: the turf
        /// leaves the coping line at the terrace height and rises <see cref="Berm"/> over 1.8 m to a level
        /// <see cref="Ledge"/> along the foot of the next riser (whose face runs on below it: no gap). Chebyshev distance to
        /// the next riser, so each corner is a level square plus two planar trapezoids meeting on the diagonal. AO darkens
        /// the ledge toward the riser foot.</summary>
        static void Turf(MeshBuilder b, int k, float x0, float x1, float z0, float z1)
        {
            float y = (k + 1) * Tier;
            if (k == Terraces - 1)
            {
                Beat34Geo.Grid(b, new Vector3(0f, y, 0f), Vector3.right, Vector3.forward, Beat34Geo.Steps(x0, x1, Cell), Beat34Geo.Steps(z0, z1, Cell), Vector3.up,
                               (x, z) => Beat34Geo.Col(0.93f + 0.07f * Mathf.Clamp01(Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(z - z0, z1 - z)) / 2f)), 0, 2);
                return;
            }
            float s = Setback, l = Ledge;
            float yE = y - BermEdge, yL = y + Berm;
            // inner rectangle = the next riser's footprint
            float ix0 = x0 + s, ix1 = x1 - s, iz0 = z0 + s, iz1 = z1 - s;
            // side strips between the corners (outer line, ledge line, riser line), subdivided every Cell
            var xs = Beat34Geo.Steps(ix0, ix1, Cell);
            var zs = Beat34Geo.Steps(iz0, iz1, Cell);
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float xa = xs[i], xb = xs[i + 1];
                // south (rises toward +Z) and north (rises toward -Z)
                BermStrip(b, new Vector3(xa, yE, z0), new Vector3(xb, yE, z0), new Vector3(xb, yL, iz0 - l), new Vector3(xa, yL, iz0 - l),
                          new Vector3(xa, yL, iz0), new Vector3(xb, yL, iz0));
                BermStrip(b, new Vector3(xb, yE, z1), new Vector3(xa, yE, z1), new Vector3(xa, yL, iz1 + l), new Vector3(xb, yL, iz1 + l),
                          new Vector3(xb, yL, iz1), new Vector3(xa, yL, iz1));
            }
            for (int i = 0; i < zs.Count - 1; i++)
            {
                float za = zs[i], zb = zs[i + 1];
                // west (rises toward +X) and east (rises toward -X)
                BermStrip(b, new Vector3(x0, yE, zb), new Vector3(x0, yE, za), new Vector3(ix0 - l, yL, za), new Vector3(ix0 - l, yL, zb),
                          new Vector3(ix0, yL, zb), new Vector3(ix0, yL, za));
                BermStrip(b, new Vector3(x1, yE, za), new Vector3(x1, yE, zb), new Vector3(ix1 + l, yL, zb), new Vector3(ix1 + l, yL, za),
                          new Vector3(ix1, yL, za), new Vector3(ix1, yL, zb));
            }
            // corners: level ledge square + two planar trapezoids meeting on the diagonal
            for (int cx = 0; cx < 2; cx++)
                for (int cz = 0; cz < 2; cz++)
                {
                    float sx = cx == 0 ? -1f : 1f, sz = cz == 0 ? -1f : 1f;
                    float ox = cx == 0 ? x0 : x1, oz = cz == 0 ? z0 : z1;          // outer corner
                    float rx = cx == 0 ? ix0 : ix1, rz = cz == 0 ? iz0 : iz1;      // riser corner
                    float lx = rx + sx * l, lz = rz + sz * l;                       // ledge corner
                    var O = new Vector3(ox, yE, oz);
                    var R = new Vector3(rx, yL, rz);
                    var Lc = new Vector3(lx, yL, lz);
                    var Lx = new Vector3(rx, yL, lz);                               // end of the x strip's ledge line
                    var Lz = new Vector3(lx, yL, rz);                               // end of the z strip's ledge line
                    var Ex = new Vector3(rx, yE, oz);                               // end of the x strip's outer line
                    var Ez = new Vector3(ox, yE, rz);                               // end of the z strip's outer line
                    TurfQuad(b, Lc, Lx, R, Lz, 0.86f, 0.8f, 0.74f, 0.8f);           // level ledge square
                    TurfQuad(b, O, Ex, Lx, Lc, 1f, 1f, 0.8f, 0.86f);                 // slope continuing the x strip
                    TurfQuad(b, O, Lc, Lz, Ez, 1f, 0.86f, 0.8f, 1f);                 // slope continuing the z strip
                }
        }

        /// <summary>One subdivision of a berm side: slope quad (outer line e0 e1 -> ledge line l1 l0) and level ledge quad
        /// (ledge line -> riser line r0 r1); e0 / l0 / r0 and e1 / l1 / r1 share their along-coordinate.</summary>
        static void BermStrip(MeshBuilder b, Vector3 e0, Vector3 e1, Vector3 l1, Vector3 l0, Vector3 r0, Vector3 r1)
        {
            TurfQuad(b, e0, e1, l1, l0, 1f, 1f, 0.86f, 0.86f);
            TurfQuad(b, l0, l1, r1, r0, 0.86f, 0.86f, 0.74f, 0.74f);
        }

        /// <summary>Planar turf quad with its own vertices, flat normal facing up, per-vertex AO, uvs from x and z.</summary>
        static void TurfQuad(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, float aoA, float aoB, float aoC, float aoD)
        {
            Vector3 n = Vector3.Cross(c1 - a, c2 - a) + Vector3.Cross(c2 - a, d - a);
            if (n.y < 0f) n = -n;
            n = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
            int i0 = b.Vtx(a, n, new Vector2(a.x, a.z), Beat34Geo.Col(aoA));
            int i1 = b.Vtx(c1, n, new Vector2(c1.x, c1.z), Beat34Geo.Col(aoB));
            int i2 = b.Vtx(c2, n, new Vector2(c2.x, c2.z), Beat34Geo.Col(aoC));
            int i3 = b.Vtx(d, n, new Vector2(d.x, d.z), Beat34Geo.Col(aoD));
            Beat34Geo.QuadO(b, i0, i1, i2, i3, n);
        }

        // ================================================================ stair, cheek walls, sconces
        struct FlightDef { public float Zf, YBase, Rise, X0, Dir; }

        /// <summary>The three flights of the south-face stair: ground -> terrace 0 against riser 0, then terrace k's ledge
        /// -> terrace k + 1 against riser k + 1, zigzagging between x -3.6 and x -11.9 (left of the mouth in KS06).</summary>
        static FlightDef[] Flights()
        {
            float s = Setback;
            return new[]
            {
                new FlightDef { Zf = Z0, YBase = 0f, Rise = Tier, X0 = -3.6f, Dir = -1f },
                new FlightDef { Zf = Z0 + s, YBase = Tier + Berm, Rise = Tier - Berm, X0 = -11.9f, Dir = 1f },
                new FlightDef { Zf = Z0 + 2f * s, YBase = 2f * Tier + Berm, Rise = Tier - Berm, X0 = -5.1f, Dir = -1f },
            };
        }

        /// <summary>Treads and risers (stone), the cheek walls on the open side (ceramic) and their PaleGold rail caps. The
        /// treads run 1 cm into the riser face (no co-planar seam); the cheek wall hides the stepped side.</summary>
        public static void Stairs(MeshBuilder stone, MeshBuilder ceramic, MeshBuilder gold)
        {
            var col = Beat34Geo.Col(1f);
            foreach (var f in Flights())
            {
                int n = Mathf.CeilToInt(f.Rise / StairRiseMax - 1e-4f);
                float r = f.Rise / n;
                float zIn = f.Zf + 0.01f, zOut = f.Zf - StairW;
                for (int i = 0; i < n; i++)
                {
                    float xa = f.X0 + f.Dir * StairGoing * i, xb = f.X0 + f.Dir * StairGoing * (i + 1);
                    float y0 = f.YBase + r * i, y1 = f.YBase + r * (i + 1);
                    // tread (up; the nosing edge catches the light: wear) and riser (facing down the flight)
                    Beat34Geo.FlatQuadP(stone, new Vector3(xa, y1, zOut), new Vector3(xb, y1, zOut), new Vector3(xb, y1, zIn), new Vector3(xa, y1, zIn),
                                        Vector3.up, col, 0, 2);
                    Beat34Geo.FlatQuadP(stone, new Vector3(xa, y0, zOut), new Vector3(xa, y1, zOut), new Vector3(xa, y1, zIn), new Vector3(xa, y0, zIn),
                                        new Vector3(-f.Dir, 0f, 0f), Beat34Geo.Col(0.88f), 2, 1);
                }
                // closing face under the top tread end (facing +Dir), down into the ground / ledge
                float xe = f.X0 + f.Dir * StairGoing * n;
                Beat34Geo.FlatQuadP(stone, new Vector3(xe, f.YBase - 0.2f, zOut), new Vector3(xe, f.YBase + f.Rise, zOut), new Vector3(xe, f.YBase + f.Rise, zIn),
                                    new Vector3(xe, f.YBase - 0.2f, zIn), new Vector3(f.Dir, 0f, 0f), Beat34Geo.Col(0.85f), 2, 1);
                // cheek wall on the open side: top CheekH above the nosing line (extended 0.15 m past both ends), bottom buried
                float c0 = zOut - CheekT, c1 = zOut;
                float slope = r / StairGoing;
                float xs = f.X0 - f.Dir * 0.15f, xt = xe + f.Dir * 0.15f;
                float ys = f.YBase + r + CheekH - 0.15f * slope;
                float yt = f.YBase + f.Rise + CheekH + 0.15f * slope;
                float yb = f.YBase - 0.2f;
                Slab(ceramic, xs, xt, c0, c1, yb, yb, ys - CapH - 0.01f, yt - CapH - 0.01f, col, false);
                Slab(gold, xs, xt, c0 - 0.02f, c1 + 0.02f, ys - CapH, yt - CapH, ys, yt, col, true);
            }
        }

        /// <summary>A slab between x xs and xt (either order), z c0..c1, bottom bs -> bt and top ys -> yt (linear in x):
        /// outer (-Z), inner (+Z), top, both ends, and the bottom face when requested (the cap's overhang).</summary>
        static void Slab(MeshBuilder b, float xs, float xt, float c0, float c1, float bs, float bt, float ys, float yt, Color col, bool bottom)
        {
            var p = new[]
            {
                new Vector3(xs, bs, c0), new Vector3(xt, bt, c0), new Vector3(xt, yt, c0), new Vector3(xs, ys, c0),
                new Vector3(xs, bs, c1), new Vector3(xt, bt, c1), new Vector3(xt, yt, c1), new Vector3(xs, ys, c1),
            };
            float dx = Mathf.Sign(xt - xs);
            Beat34Geo.FlatQuadP(b, p[0], p[1], p[2], p[3], Vector3.back, col, 0, 1);
            Beat34Geo.FlatQuadP(b, p[4], p[5], p[6], p[7], Vector3.forward, col, 0, 1);
            Beat34Geo.FlatQuadP(b, p[3], p[2], p[6], p[7], Vector3.up, Beat34Geo.Col(1f, 1f), 0, 2);
            Beat34Geo.FlatQuadP(b, p[0], p[3], p[7], p[4], new Vector3(-dx, 0f, 0f), col, 2, 1);
            Beat34Geo.FlatQuadP(b, p[1], p[2], p[6], p[5], new Vector3(dx, 0f, 0f), col, 2, 1);
            if (bottom) Beat34Geo.FlatQuadP(b, p[0], p[1], p[5], p[4], Vector3.down, col, 0, 2);
        }

        static Vector3 SconcePos(Vector2 sc, out float zf, out float floorY)
        {
            int k = Mathf.RoundToInt(sc.y);
            zf = Z0 + Setback * k;
            floorY = k == 0 ? 0f : k * Tier + Berm;
            float y = k == 0 ? 3.2f : floorY + 2.0f;
            return new Vector3(sc.x, y, zf - SconceOut);
        }

        /// <summary>The eight terrace sconces: lens (Lamp34) under a PaleGold housing on a wall plate.</summary>
        public static void SconceLamps(MeshBuilder gold, MeshBuilder lens)
        {
            var col = Beat34Geo.Col(1f);
            foreach (var sc in Sconces)
            {
                Vector3 c = SconcePos(sc, out float zf, out _);
                Beat34Geo.Box(gold, new Vector3(c.x, c.y + 0.12f, zf - 0.015f), new Vector3(0.2f, 0.34f, 0.05f), 0.01f, col);   // wall plate, 1 cm into the face
                Beat34Geo.Box(gold, new Vector3(c.x, c.y + 0.075f, c.z), new Vector3(0.3f, 0.09f, 0.22f), 0.012f, col);          // housing (to the plate)
                Beat34Geo.Box(lens, new Vector3(c.x, c.y, c.z), new Vector3(0.24f, 0.06f, 0.16f), 0.006f, col);                    // lens below the housing
            }
        }

        /// <summary>Number of terrace sconces (the RingUnderpass material vectors _Sconce0.._Sconce7).</summary>
        public static int SconceCount => Sconces.Length;

        /// <summary>Virtual emitting point of sconce i for the in-shader wash (flat world): the lens centre moved to
        /// SconceVirtual in front of its riser, so a downlight 0.16 m off the wall throws a ~2 m scallop.</summary>
        public static Vector3 SconceLight(int i)
        {
            Vector3 c = SconcePos(Sconces[i], out float zf, out _);
            return new Vector3(c.x, c.y, zf - SconceVirtual);
        }

        /// <summary>Additive pools of the sconces on riser 0 (the ground in front of the levee is terrain, which the
        /// in-shader wash cannot reach): RingMat.LightPool, lamp channel _Lamps.x / 3.</summary>
        public static void SconcePools(MeshBuilder b)
        {
            Color warm = Beat34Mat.WarmLampDisplay;
            float ch = Beat34Additive.ChannelLamp;
            foreach (var sc in Sconces)
            {
                if (sc.y > 0.5f) continue;
                Vector3 c = SconcePos(sc, out _, out float floorY);
                Beat34Additive.PoolRect(b, new Vector3(c.x, floorY, c.z), Vector3.right, -PoolR, PoolR, -(SconceOut - 0.02f), PoolR - SconceOut,
                                        warm, SconceFloor, ch, c.y - floorY, 2);
            }
        }

        /// <summary>Stair rail lights: a WarmLampDisplay point (RingMat.Glow, lamp channel) 5 cm above the PaleGold cap of
        /// every cheek wall at every third step, so the zigzag stair reads as a line of small lights at night.</summary>
        public static void StepLights(MeshBuilder b)
        {
            Color warm = Beat34Mat.WarmLampDisplay;
            foreach (var f in Flights())
            {
                int n = Mathf.CeilToInt(f.Rise / StairRiseMax - 1e-4f);
                float r = f.Rise / n;
                float z = f.Zf - StairW - 0.5f * CheekT;
                for (int i = 1; i < n; i += 3)
                {
                    float x = f.X0 + f.Dir * StairGoing * (i + 0.5f);
                    float y = f.YBase + r * (i + 1) + CheekH + 0.05f + StepLightR;
                    Beat34Additive.Glow(b, new Vector3(x, y, z), StepLightR, warm, StepLightI, Beat34Additive.ChannelLamp);
                }
            }
        }

        // ================================================================ bollards and pools
        public static float BollardZ(int i) => BollardS0 + (BollardS1 - BollardS0) * i / (BollardCount - 1);   // s = z on this leg

        /// <summary>Shafts (Ceramic), lens bands (Emissive, _Lamps.x), caps (PaleGold) of the 12 beat-4 bollards.</summary>
        public static void Bollards(MeshBuilder shaft, MeshBuilder lens, MeshBuilder cap)
        {
            var col = Beat34Geo.Col(1f);
            for (int i = 0; i < BollardCount; i++)
            {
                float z = BollardZ(i);
                Beat34Geo.Box(shaft, new Vector3(BollardX, 0.38f, z), new Vector3(0.16f, 0.96f, 0.16f), 0.015f, col);
                Beat34Geo.Box(lens, new Vector3(BollardX, BollardLampY, z), new Vector3(0.15f, 0.097f, 0.15f), 0.006f, Beat34Geo.Col(1f, 0f, 0f, 1f));
                Beat34Geo.Box(cap, new Vector3(BollardX, 0.974f, z), new Vector3(0.19f, 0.052f, 0.19f), 0.012f, col);
            }
        }

        /// <summary>Light pools of the 12 bollards (RingMat.LightPool, lamp channel _Lamps.x / 3: 0 -> 3.0 over u
        /// 0.30-0.60): one quad on the path strip between the kerbs (0.02 m above its top, polygon offset in the
        /// material), one on the right kerb top and one on the meadow beyond the right kerb (0.02 m above the flat
        /// ground), all with the analytic falloff centred under the lamp, so the pool never cuts through a kerb and the
        /// kerb is not a dark band through it.</summary>
        public static void BollardPools(MeshBuilder b)
        {
            Color warm = Beat34Mat.WarmLampDisplay;
            float ch = Beat34Additive.ChannelLamp;
            for (int i = 0; i < BollardCount; i++)
            {
                float z = BollardZ(i);
                Beat34Additive.PoolRect(b, new Vector3(BollardX, PathTop, z), Vector3.forward, -PoolR, PoolR,
                    -StripHalf + 0.02f - BollardX, StripHalf - 0.02f - BollardX, warm, PoolBollard, ch, BollardLampY - PathTop, 2);
                Beat34Additive.PoolRect(b, new Vector3(BollardX, KerbTop, z), Vector3.forward, -PoolR, PoolR,
                    StripHalf - BollardX, KerbOuter - BollardX, warm, PoolBollard, ch, BollardLampY - KerbTop, 2);
                Beat34Additive.PoolRect(b, new Vector3(BollardX, 0f, z), Vector3.forward, -PoolR, PoolR,
                    KerbOuter + 0.02f - BollardX, PoolR, warm, PoolBollard, ch, BollardLampY, 2);
            }
        }

        /// <summary>
        /// Light pool of the mouth lamp, foot under the lens (z 557.45). The path strip between the kerbs (up to the
        /// threshold stone), the threshold stone and the tube floor share one pool plane 0.02 m above the strip (8 mm
        /// above the threshold top, 14 mm above the floor) and meet edge to edge at the threshold lines z 557.85 and
        /// 558.15, so the brightest part of the pool has no unlit band and no sliver opens between pools of different
        /// heights. The plane is cut into a grid of rectangles (columns at the strip, threshold and cove widths, rows at
        /// the threshold lines and the shadow line) that share whole edges, so no T-junction can crack or double the
        /// additive light. The kerb tops and the ground beyond the kerbs get their own pools (none cuts through a kerb;
        /// the parts that run under the jambs are hidden inside them). On the tube floor the lintel's front-bottom edge
        /// casts the lens's soft shadow: full light to <see cref="LintelPenumbra"/> zFull (558.22), linear to zero at
        /// zDark (559.45), baked into the vertex intensity with vertex rows at both lines (the radiance is interpolated
        /// linearly, the falloff profile is per pixel, so single quads are exact).
        /// </summary>
        public static void MouthPools(MeshBuilder b)
        {
            Color warm = Beat34Mat.WarmLampDisplay;
            float ch = Beat34Additive.ChannelLamp;
            float zl = MouthLampZ;
            float sill0 = MouthZ - 0.5f * SillDepth - zl, sill1 = MouthZ + 0.5f * SillDepth - zl;   // threshold edges (u)
            float wall = MouthZ - 0.02f - zl;                                                      // riser foot (u)
            float hPlane = MouthLampY - PathTop;                     // one profile on the shared plane: continuous at the edges
            var plane = new Vector3(Corner.x, PathTop, zl);
            LintelPenumbra(out float zFull, out float zDark);
            float uFull = Mathf.Clamp(zFull - zl, sill1, PoolR), uDark = Mathf.Clamp(zDark - zl, uFull, PoolR);
            float cove = 0.5f * UW - CoveR - 0.02f, stone = SillHalfW - 0.02f, strip = StripHalf - 0.02f;
            // columns (v = x): strip edge | threshold end | cove | cove | threshold end | strip edge
            var vs = new[] { -strip, -stone, -cove, cove, stone, strip };
            // path strip in front of the threshold: all five columns
            for (int c = 0; c < 5; c++) MouthRect(b, plane, -PoolR, sill0, vs[c], vs[c + 1], warm, ch, hPlane);
            // threshold stone (z 557.85..558.15, top FloorY + SillTop): the inner three columns
            for (int c = 1; c < 4; c++) MouthRect(b, plane, sill0, sill1, vs[c], vs[c + 1], warm, ch, hPlane);
            // tube floor between the coves: lit row, then the lintel penumbra (intensity baked per vertex)
            int first = b.VertexCount;
            MouthRect(b, plane, sill1, uFull, -cove, cove, warm, ch, hPlane);
            MouthRect(b, plane, uFull, uDark, -cove, cove, warm, ch, hPlane);
            Beat34Additive.ScaleIntensity(b, first, b.VertexCount, p => LintelVisibility(p.z, zFull, zDark));
            // kerb tops and the ground beyond the kerbs (flat y 0: RingWorld.Height is 0 within 6 m of the path)
            for (int s = -1; s <= 1; s += 2)
            {
                float k0 = s > 0 ? StripHalf : -KerbOuter, k1 = s > 0 ? KerbOuter : -StripHalf;
                MouthRect(b, new Vector3(Corner.x, KerbTop, zl), -PoolR, wall, k0, k1, warm, ch, MouthLampY - KerbTop);
                float g0 = s > 0 ? KerbOuter + 0.02f : -PoolR, g1 = s > 0 ? PoolR : -(KerbOuter + 0.02f);
                MouthRect(b, new Vector3(Corner.x, 0f, zl), -PoolR, wall, g0, g1, warm, ch, MouthLampY);
            }
        }

        static void MouthRect(MeshBuilder b, Vector3 foot, float u0, float u1, float v0, float v1, Color warm, float ch, float lampHeight) =>
            Beat34Additive.PoolRect(b, foot, Vector3.forward, u0, u1, v0, v1, warm, PoolMouth, ch, lampHeight, 1);

        /// <summary>
        /// Soft shadow of the mouth lintel on the tube floor (similar triangles in the z-y plane): a floor point sees a
        /// lens point z_e past the lintel's front-bottom edge (zEdge, yEdge) while z &lt;= z_e + (zEdge - z_e) g with
        /// g = (yLens - yFloor) / (yLens - yEdge). zFull: the whole lens is visible up to here (its edge nearer the levee
        /// is hidden first); zDark: none of it beyond. In between the visible fraction is linear in z.
        /// </summary>
        static void LintelPenumbra(out float zFull, out float zDark)
        {
            float yLens = MouthLampY - 0.5f * LensH;                        // emitting bottom face, 3.53
            float yEdge = FloorY + LintelCy - 0.5f * LintelH;               // lintel bottom, 2.998
            float zEdge = MouthZ - LintelCz - 0.5f * LintelD;               // lintel front, 557.66
            float g = (yLens - FloorY) / Mathf.Max(yLens - yEdge, 1e-3f);   // 6.6
            float zNear = MouthLampZ + 0.5f * LensD, zFar = MouthLampZ - 0.5f * LensD;   // lens edges toward / away from the levee
            zFull = zNear + (zEdge - zNear) * g;                            // 558.22
            zDark = zFar + (zEdge - zFar) * g;                              // 559.45
        }

        static float LintelVisibility(float z, float zFull, float zDark) =>
            Mathf.Clamp01((zDark - z) / Mathf.Max(zDark - zFull, 1e-3f));
    }
}
