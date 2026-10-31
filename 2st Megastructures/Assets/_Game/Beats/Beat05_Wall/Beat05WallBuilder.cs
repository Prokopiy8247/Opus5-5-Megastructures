using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    /// <summary>
    /// Beat 5 - the rim wall (MASTER_PROMPT 14.5.5, 14.6 boundaries 4->5 and 5->6). Builds, behind the load fade:
    /// the RimWall plane x 114 (50 m columns, 24 cubic rows to 12 km, skirt to -6 m, the door hole cut exactly), the
    /// terrain-following plinth along the foot, the door (PaleGold frame proud 0.02 m around the tunnel-profile
    /// opening, lintel, WarmLamp lamp, height ticks to 6 m, the 0.8 m recess), the apron (4 m slabs) in front of the
    /// door, 14 lamp bollards every 12 m along the plain legs with light pools, and the climber point.
    /// The door lamp's light is an additive WarmLamp glow on the wall around the opening (a collar from the opening edge
    /// outward, so nothing glows in the air over the hole) and on the threshold inside the recess: from 84-120 m the
    /// door reads as a tiny warm speck in the enormous wall (14.5.5 purpose), not as a dark slot.
    /// Everything belongs to NearPlain (s 576-810) except the recess, which belongs to the always-built tunnel set so
    /// the door outline seen from inside the tunnel never changes when NearPlain is switched off at s 810.
    /// Nothing casts shadows (the wall never does; beat 5 has no key shadows, 14.2.3).
    /// </summary>
    public sealed class Beat05WallBuilder : IBeatBuilder
    {
        public int Order => 500;
        public string Name => "Beat05Wall";

        // ---- 14.5.5 numbers that RingConstants does not carry ----
        const float WallZ0 = -9000f, WallZ1 = 9000f;      // 14.5.5 "z -9 000..9 000"
        const float ColumnW = 50f;                         // "in 50 m columns"
        const int Rows = 24;                               // "24 cubic-spaced rows h_k = 12 000 (k/24)^3"
        const float SkirtY = -6f;                          // "a skirt down to y -6 m"
        const float WallBendDistance = 12000f;             // bounds inflated for 12 km (the wall top, 14.7 H1)
        const float PlinthBendDistance = 4500f;            // plinth extent from the plain
        const float NearBendDistance = 400f;               // door, apron, bollards, climber: seen from <= 400 m

        const float PlinthDepth = 0.35f, PlinthHeight = 0.40f;   // "plinth 0.35 x 0.4 m" (depth from the wall x height)
        const float PlinthZ0 = -3400f, PlinthZ1 = 4700f;          // beyond ~4 km the foot is fully fogged (sigma 5e-4)
        const float PlinthBury = 0.6f;                            // below the analytic ground (coarse clipmap levels)
        const float Chamfer = 0.02f;                              // 1-3 cm chamfers on everything seen within 3 m (8.7)

        public const float FrameProud = 0.02f;             // "trim proud 0.02 m" (14.5.5 pitfalls); also the DoorRecess door plane
        const float FrameChamfer = 0.01f;
        const float FrameEmbed = 1.0f;                     // frame collar reaches 1 m behind the wall face (no leaks)
        const float LintelY0 = 3.30f, LintelW = 3.6f, LintelProud = 0.12f, LintelEmbed = 0.06f;   // 0.5 m lintel above
        const float TickZ = 657.70f;                       // gauge beside the door (-Z side, 0.9 m from the frame)
        const float TickStep = 0.5f, TickTop = 6.0f;       // "height ticks every 0.5 m to 6 m"

        const float ApronX0 = 98f, ApronX1 = 113.99f, ApronZ0 = 640f, ApronZ1 = 680f;   // task: x 98..114, z 640..680
        const float ApronLift = 0.03f;                     // slab top above the ground (path strip is +0.012)
        const float ApronSkirt = 0.12f;                    // slab sides reach below the ground

        const int BollardCount = 14;                       // "14 instanced bollards every 12 m"
        const float BollardS0 = 600f, BollardPitch = 12f;  // s 600..756 along the plain legs (s 594-774)
        const float BollardOffset = 2.2f;                  // left of the path, outside the 3.0 m strip and kerbs
        const int BollardSegments = 16;
        const float PoolRadiusBollard = 2.5f, PoolRadiusDoor = 3.0f;
        const float PoolLiftTerrain = 0.035f, PoolLiftApron = 0.02f;   // 0.02 above the strip / apron (14.10 T4)
        const float PoolBollard = 0.12f, PoolDoor = 0.18f;             // additive peak radiance (x WarmLamp)
        // door glow (PlainPool material): the lamp's light on the wall face around the opening, an ellipse centred
        // 1.1 m below the lamp (the lamp is a downlight; the lintel above stays dark), 4 cm in front of the wall
        // (2 cm in front of the frame front: never co-planar), a strip on the recess floor (the threshold) and the two
        // straight jambs inside the recess (2 cm in front of them), so the opening itself reads warm, not black
        const float GlowX = RingConstants.WallX - 0.04f;
        const float GlowCy = 2.2f, GlowRy = 2.2f, GlowRz = 2.6f, GlowPeak = 0.16f;
        const float ThresholdX0 = 113.6f, ThresholdRx = 1.4f, ThresholdRz = 1.6f, ThresholdPeak = 0.20f, ThresholdLift = 0.02f;
        const float JambX0 = 113.7f, JambRx = 1.1f, JambCy = 1.4f, JambRy = 2.0f, JambPeak = 0.12f, JambInset = 0.02f;

        // ---- climber (14.5.5; twin of RingWallClimber.shader, the material constants come from here) ----
        public const float ClimberX = RingConstants.WallX - 0.02f;    // "x = 113.98" (2 cm in front of the face)
        public const float ClimberEps0 = 8f, ClimberEps1 = 80f;       // "epsilon(s) = 8 + 72 (s - 594)/180 degrees"
        public const float ClimberFootprint = 0.0014f;               // "constant footprint 2.3 px at 4K = 1.4 mrad" (FWHM)
        public const float ClimberTailDeg = 0.6f;                     // "a faint tail 0.6 degrees long"
        // "WarmLamp, HDR 4" arbitrated to 1.2 (B56): at the beat-5 exposure (EV +1.6) HDR 4 clips all three channels, so
        // the 2.3 px point rendered as a 6-8 px white-cyan disc (run04/b56-01), failing acceptance (9) (2.3 +- 0.6 px)
        // and losing the warm colour; 1.2 keeps the peak above luma 200 with the footprint and the WarmLamp hue.
        public const float ClimberHdr = 1.2f;

        static float Ground(float x, float z) => RingWorld.Height(x, z, 1f);   // the plain: flank off (_Set = 1)
        static float ApronTop(float x, float z) => Ground(x, z) + ApronLift;
        static bool InApron(float x, float z) => x >= ApronX0 && x <= ApronX1 && z >= ApronZ0 && z <= ApronZ1;

        static float DoorZL => TunnelProfile.AxisZ - (TunnelProfile.HalfWidth + RingConstants.DoorFrame);   // 658.58
        static float DoorZR => TunnelProfile.AxisZ + (TunnelProfile.HalfWidth + RingConstants.DoorFrame);   // 661.42

        public IEnumerator Build(BuildContext ctx)
        {
            const RingSet set = RingSet.NearPlain;
            int tris0 = ctx.Triangles;

            // 1 RimWall (no shadows, bounds for 12 km)
            var wallMesh = BuildWall(out bool fallback);
            if (fallback) Debug.LogWarning("[Ring] Beat05Wall: door patch triangulation needed a degenerate fallback");
            Beat56Kit.Check(wallMesh);
            var wall = ctx.AddMesh("RimWall", wallMesh, RingMat.Wall, set, false, WallBendDistance);
            ctx.Register("Wall", wall);
            Beat56Kit.Marker(ctx, set, "Subject.Wall", new Vector3(RingConstants.WallX, 60f, TunnelProfile.AxisZ), new Vector3(0.01f, 120f, 300f));
            yield return null;

            // 2 plinth
            var plinth = BuildPlinth();
            Beat56Kit.Check(plinth);
            ctx.Register("Wall.Plinth", ctx.AddMesh("WallPlinth", plinth, Beat56Ids.WallPlinth, set, false, PlinthBendDistance));
            yield return null;

            // 3 door: frame (PaleGold), lintel (Ceramic), lamp (Emissive), ticks (PaleGold), recess (DoorRecess on M08, tunnel set)
            var frame = BuildFrame();
            Beat56Kit.Check(frame);
            var frameGo = ctx.AddMesh("DoorFrame", frame, RingMat.PaleGold, set, false, NearBendDistance);
            ctx.Register("Door.FrameMesh", frameGo);

            var lintel = BuildLintel();
            Beat56Kit.Check(lintel);
            ctx.Register("Door.Lintel", ctx.AddMesh("DoorLintel", lintel, RingMat.Ceramic, set, false, NearBendDistance));

            var lamp = BuildDoorLamp();
            Beat56Kit.Check(lamp);
            ctx.Register("Door.Lamp", ctx.AddMesh("DoorLamp", lamp, RingMat.Emissive, set, false, NearBendDistance));

            var ticks = BuildTicks();
            Beat56Kit.Check(ticks);
            ctx.Register("Door.Ticks", ctx.AddMesh("DoorTicks", ticks, RingMat.PaleGold, set, false, NearBendDistance));

            var glow = BuildDoorGlow();
            Beat56Kit.Check(glow);
            ctx.Register("Door.Glow", ctx.AddMesh("DoorGlow", glow, Beat56Ids.PlainPool, set, false, NearBendDistance));

            // The recess stays on (Tunnel set) through beat 7, where the key L7 points along +X and the 90 m shadow distance
            // leaves it unshadowed 108-126 m from the mouth: DoorRecess (wall ceramic on RingTunnel) occludes every
            // directional light that would have to cross the tube, while the beat-5 lights (L.x < 0) still enter through the door.
            var recess = BuildRecess();
            Beat56Kit.Check(recess);
            ctx.Register("Door.Recess", ctx.AddMesh("DoorRecess", recess, Beat56Ids.DoorRecess, RingSet.Tunnel, false, NearBendDistance));

            float zc = TunnelProfile.AxisZ;
            Beat56Kit.Marker(ctx, set, "Door.Opening",
                new Vector3(RingConstants.WallX + 0.5f * RingConstants.DoorRecess, 0.5f * RingConstants.DoorH, zc),
                new Vector3(RingConstants.DoorRecess, RingConstants.DoorH, RingConstants.DoorW));
            // the PaleGold frame: 0.12 m around the opening, proud 0.02 m (x 113.98..114)
            float frameW = RingConstants.DoorW + 2f * RingConstants.DoorFrame;
            float frameH = RingConstants.DoorH + RingConstants.DoorFrame;
            Beat56Kit.Marker(ctx, set, "Door.Frame",
                new Vector3(RingConstants.WallX - 0.5f * FrameProud, 0.5f * frameH, zc),
                new Vector3(FrameProud, frameH, frameW));
            // the whole door assembly: lintel front (x 113.88) to the end of the recess (x 114.8), floor to lintel top
            float subX0 = RingConstants.WallX - LintelProud, subX1 = TunnelProfile.LoftX0, subY1 = LintelY0 + RingConstants.DoorLintel;
            Beat56Kit.Marker(ctx, set, "Subject.Door",
                new Vector3(0.5f * (subX0 + subX1), 0.5f * subY1, zc),
                new Vector3(subX1 - subX0, subY1, Mathf.Max(frameW, LintelW)));
            yield return null;

            // 4 apron
            var apron = BuildApron();
            Beat56Kit.Check(apron);
            ctx.Register("Apron", ctx.AddMesh("Apron", apron, Beat56Ids.Apron, set, false, NearBendDistance));
            yield return null;

            // 5 bollards (ceramic body, PaleGold ring, emissive lamp band), colliders, light pools
            var feet = BollardFeet();
            BuildBollards(feet, out Mesh body, out Mesh ring, out Mesh lampBand);
            Beat56Kit.Check(body); Beat56Kit.Check(ring); Beat56Kit.Check(lampBand);
            ctx.Register("Bollards", ctx.AddMesh("PlainBollards", body, RingMat.Ceramic, set, false, NearBendDistance));
            ctx.AddMesh("PlainBollardRings", ring, RingMat.PaleGold, set, false, NearBendDistance);
            ctx.AddMesh("PlainBollardLamps", lampBand, RingMat.Emissive, set, false, NearBendDistance);
            for (int i = 0; i < feet.Count; i++)
                ctx.AddBoxCollider("PlainBollardCollider" + i, feet[i] + new Vector3(0f, 0.55f, 0f), new Vector3(0.26f, 1.1f, 0.26f), Quaternion.identity);

            var pools = BuildPools(feet);
            Beat56Kit.Check(pools);
            ctx.Register("Plain.Pools", ctx.AddMesh("PlainPools", pools, Beat56Ids.PlainPool, set, false, NearBendDistance));
            yield return null;

            // 6 climber (cut order 3b): one camera-facing point computed in its shader from _RingCam and _RingTime.w = s
            var climber = BuildClimberMesh();
            var climberGo = ctx.AddMesh("WallClimber", climber, Beat56Ids.Climber, set, false, NearBendDistance);
            ctx.Register("Climber", climberGo);

            Debug.Log("[Ring] Beat05Wall built: " + (ctx.Triangles - tris0) + " triangles (wall " + Beat56Kit.Triangles(wallMesh)
                + ", plinth " + Beat56Kit.Triangles(plinth) + ", " + feet.Count + " bollards)");
        }

        // =====================================================================================================
        // RimWall: plane x 114 facing -X. Grid columns z = -9000 + 50 j, rows y = -6, 0, h_1 .. h_24. The column that
        // contains the door (z 650..700) is replaced below h_2 by an ear-clipped patch around the frame's outer profile
        // (the hole), sharing every boundary vertex with the neighbouring cells (no T-junctions).
        // =====================================================================================================
        static Mesh BuildWall(out bool fallback)
        {
            var mb = new MeshBuilder();
            float x = RingConstants.WallX;
            int nCols = Mathf.RoundToInt((WallZ1 - WallZ0) / ColumnW);
            var ys = new float[Rows + 2];
            ys[0] = SkirtY;
            ys[1] = 0f;
            for (int k = 1; k <= Rows; k++)
            {
                float f = k / (float)Rows;
                ys[k + 1] = RingConstants.WallTop * f * f * f;
            }
            int nRows = ys.Length;
            Vector3 nrm = Vector3.left;
            var grid = new int[nCols + 1, nRows];
            for (int j = 0; j <= nCols; j++)
            {
                float z = WallZ0 + ColumnW * j;
                for (int r = 0; r < nRows; r++) grid[j, r] = mb.Vtx(new Vector3(x, ys[r], z), nrm, new Vector2(z, ys[r]));
            }
            int dc = Mathf.FloorToInt((TunnelProfile.AxisZ - WallZ0) / ColumnW);   // the door column (z 650..700)
            for (int j = 0; j < nCols; j++)
                for (int r = 0; r < nRows - 1; r++)
                {
                    if (j == dc && r < 3) continue;
                    mb.Quad(grid[j, r], grid[j + 1, r], grid[j + 1, r + 1], grid[j, r + 1]);   // clockwise seen from -X
                }

            // the hole: the frame's outer profile (opening + 0.12) with straight posts down to y 0
            var hole = TunnelProfile.Arch(RingConstants.DoorFrame, 0f, TunnelProfile.NoWallYs, TunnelProfile.CrownSegments);
            var hv = new int[hole.Count];
            for (int i = 0; i < hole.Count; i++)
            {
                Vector3 hp = TunnelProfile.World(hole[i], x);
                hv[i] = mb.Vtx(hp, nrm, new Vector2(hp.z, hp.y));   // same (z, y) mapping as the grid: valid tangents
            }

            // patch polygon (local u = z - axis, v = y), counter-clockwise: bottom-left, up around the hole, bottom-right,
            // up the right edge, along the top, down the left edge
            var poly = new List<Vector2>(hole.Count + 6);
            var vid = new List<int>(hole.Count + 6);
            void Add(int id) { Vector3 p = mb.V[id]; poly.Add(new Vector2(p.z - TunnelProfile.AxisZ, p.y)); vid.Add(id); }
            Add(grid[dc, 1]);
            for (int i = 0; i < hv.Length; i++) Add(hv[i]);
            Add(grid[dc + 1, 1]);
            Add(grid[dc + 1, 2]);
            Add(grid[dc + 1, 3]);
            Add(grid[dc, 3]);
            Add(grid[dc, 2]);
            var tris = Beat56Kit.EarClip(poly, out fallback);
            for (int t = 0; t + 2 < tris.Count; t += 3) Beat56Kit.Tri(mb, vid[tris[t]], vid[tris[t + 1]], vid[tris[t + 2]], nrm);

            // skirt below the door column, split at the hole feet
            int h0 = hv[0], hn = hv[hv.Length - 1];
            int bl = mb.Vtx(new Vector3(x, SkirtY, mb.V[h0].z), nrm, new Vector2(mb.V[h0].z, SkirtY));
            int br = mb.Vtx(new Vector3(x, SkirtY, mb.V[hn].z), nrm, new Vector2(mb.V[hn].z, SkirtY));
            Beat56Kit.Quad(mb, grid[dc, 0], bl, h0, grid[dc, 1], nrm);
            Beat56Kit.Quad(mb, bl, br, hn, h0, nrm);
            Beat56Kit.Quad(mb, br, grid[dc + 1, 0], grid[dc + 1, 1], hn, nrm);
            return mb.Build("Beat05.RimWall");
        }

        // =====================================================================================================
        // Plinth: 0.35 deep (x 113.65..114), 0.40 above the ground (0.43 above the bare terrain so it reads 0.40 above
        // the apron), following max(ground, river surface) along the foot so it always covers the terrain / wall
        // junction (14.5.5 (6)); hard-edged front, 2 cm chamfer, top; end caps at the door frame and at both far ends.
        // =====================================================================================================
        static float PlinthStep(float z)
        {
            if (z > 500f && z < 840f) return 1.5f;   // the plain: close views (T5 at z 700)
            if (z > 95f && z < 210f) return 1.5f;    // river banks at the wall foot (river centre z ~151)
            if (z > -700f && z < 2000f) return 6f;
            return 40f;
        }

        static void PlinthSamples(float z0, float z1, List<float> zs)
        {
            zs.Clear();
            float z = z0;
            zs.Add(z);
            while (z < z1 - 1e-3f)
            {
                z = Mathf.Min(z + PlinthStep(z), z1);
                if (z1 - z < 0.3f) z = z1;
                zs.Add(z);
            }
        }

        static float PlinthBase(float z)
        {
            float x0 = RingConstants.WallX - PlinthDepth, x1 = RingConstants.WallX - 0.01f;
            float g = Mathf.Max(Ground(x0, z), Mathf.Max(Ground(0.5f * (x0 + x1), z), Ground(x1, z)));
            return Mathf.Max(g, RingConstants.RiverSurfaceY);
        }

        static float PlinthBottom(float z)
        {
            float x0 = RingConstants.WallX - PlinthDepth, x1 = RingConstants.WallX - 0.01f;
            return Mathf.Min(Ground(x0, z), Ground(x1, z)) - PlinthBury;
        }

        static Mesh BuildPlinth()
        {
            var mb = new MeshBuilder();
            var zs = new List<float>(1200);
            PlinthSamples(PlinthZ0, DoorZL, zs);
            PlinthPiece(mb, zs, true, true);
            PlinthSamples(DoorZR, PlinthZ1, zs);
            PlinthPiece(mb, zs, true, true);
            return mb.Build("Beat05.Plinth");
        }

        static void PlinthPiece(MeshBuilder mb, List<float> zs, bool capStart, bool capEnd)
        {
            float xf = RingConstants.WallX - PlinthDepth, xw = RingConstants.WallX;
            int n = zs.Count;
            var top = new float[n];
            var bot = new float[n];
            for (int i = 0; i < n; i++) { top[i] = PlinthBase(zs[i]) + ApronLift + PlinthHeight; bot[i] = PlinthBottom(zs[i]); }
            int f0 = mb.VertexCount;
            var white = new Color(1f, 0f, 0f, 0f);
            var wear = new Color(1f, 1f, 0f, 0f);
            // vertices: 0 front bottom, 1 front top, 2 chamfer low, 3 chamfer high, 4 top front, 5 top back
            for (int i = 0; i < n; i++)
            {
                float z = zs[i];
                float slope = (top[Mathf.Min(i + 1, n - 1)] - top[Mathf.Max(i - 1, 0)]) / Mathf.Max(zs[Mathf.Min(i + 1, n - 1)] - zs[Mathf.Max(i - 1, 0)], 1e-3f);
                Vector3 nTop = new Vector3(0f, 1f, -slope).normalized;
                Vector3 nCh = new Vector3(-1f, 1f, -slope).normalized;
                float g = PlinthBase(z) + ApronLift;
                float aoBottom = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01((bot[i] - g + 0.25f) / 0.25f));
                mb.Vtx(new Vector3(xf, bot[i], z), Vector3.left, new Vector2(z, bot[i]), new Color(aoBottom, 0f, 0f, 0f));
                mb.Vtx(new Vector3(xf, top[i] - Chamfer, z), Vector3.left, new Vector2(z, top[i]), white);
                mb.Vtx(new Vector3(xf, top[i] - Chamfer, z), nCh, new Vector2(z, 0f), wear);
                mb.Vtx(new Vector3(xf + Chamfer, top[i], z), nCh, new Vector2(z, 1f), wear);
                mb.Vtx(new Vector3(xf + Chamfer, top[i], z), nTop, new Vector2(z, 0f), white);
                mb.Vtx(new Vector3(xw, top[i], z), nTop, new Vector2(z, 1f), new Color(0.9f, 0f, 0f, 0f));
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = f0 + 6 * i, b = a + 6;
                Beat56Kit.Quad(mb, a + 0, b + 0, b + 1, a + 1, Vector3.left);
                Beat56Kit.Quad(mb, a + 2, b + 2, b + 3, a + 3, new Vector3(-1f, 1f, 0f));
                Beat56Kit.Quad(mb, a + 4, b + 4, b + 5, a + 5, Vector3.up);
            }
            if (capStart) PlinthCap(mb, zs[0], top[0], bot[0], Vector3.back);
            if (capEnd) PlinthCap(mb, zs[n - 1], top[n - 1], bot[n - 1], Vector3.forward);
        }

        static void PlinthCap(MeshBuilder mb, float z, float top, float bot, Vector3 nrm)
        {
            float xf = RingConstants.WallX - PlinthDepth, xw = RingConstants.WallX;
            var c = new Color(0.92f, 0f, 0f, 0f);
            int a = mb.Vtx(new Vector3(xw, bot, z), nrm, Vector2.zero, c);
            int b = mb.Vtx(new Vector3(xf, bot, z), nrm, Vector2.zero, c);
            int d = mb.Vtx(new Vector3(xf, top - Chamfer, z), nrm, Vector2.zero, c);
            int e = mb.Vtx(new Vector3(xf + Chamfer, top, z), nrm, Vector2.zero, c);
            int f = mb.Vtx(new Vector3(xw, top, z), nrm, Vector2.zero, c);
            Beat56Kit.Tri(mb, a, b, d, nrm);
            Beat56Kit.Tri(mb, a, d, e, nrm);
            Beat56Kit.Tri(mb, a, e, f, nrm);
        }

        // =====================================================================================================
        // Door frame (PaleGold): a collar around the opening. Inner chamfer (opening -> +1 cm, x 113.99 -> 113.98),
        // front ring (x 113.98, proud 0.02), outer chamfer, and the outer side at the frame's outer profile from
        // x 113.99 to 115.0: the wall's hole edge (x 114, the same profile) lies on that surface, so a rasterisation
        // gap at the hole can only ever show the frame itself (the "1 m overlap into the wall").
        // =====================================================================================================
        static Mesh BuildFrame()
        {
            var mb = new MeshBuilder();
            int cs = TunnelProfile.CrownSegments;
            float[] wy = TunnelProfile.WallYs;
            float xF = RingConstants.WallX - FrameProud, xC = xF + FrameChamfer, xBack = RingConstants.WallX + FrameEmbed;
            float o = RingConstants.DoorFrame;
            var a0 = TunnelProfile.Arch(0f, 0f, wy, cs);
            var a1 = TunnelProfile.Arch(FrameChamfer, 0f, wy, cs);
            var a2 = TunnelProfile.Arch(o - FrameChamfer, 0f, wy, cs);
            var a3 = TunnelProfile.Arch(o, 0f, wy, cs);
            var plain = new Color(1f, 0f, 0f, 0f);
            var wear = new Color(1f, 1f, 0f, 0f);
            Beat56Kit.Band(mb, a0, xC, a1, xF,
                i => (Vector3.left - TunnelProfile.WorldDir(TunnelProfile.Outward(a0, i, false))).normalized, wear);
            Beat56Kit.Band(mb, a1, xF, a2, xF, i => Vector3.left, plain);
            Beat56Kit.Band(mb, a2, xF, a3, xC,
                i => (Vector3.left + TunnelProfile.WorldDir(TunnelProfile.Outward(a3, i, false))).normalized, wear);
            mb.Col = plain;
            mb.Loft(a3, new[] { new Vector3(xC, 0f, TunnelProfile.AxisZ), new Vector3(xBack, 0f, TunnelProfile.AxisZ) }, Vector3.up, false, false);
            return mb.Build("Beat05.DoorFrame");
        }

        // Recess (x 113.99..114.8, the opening profile, faces inward): its last ring is bit-identical to the first ring
        // of the tunnel loft (same chains, same x), so the junction is watertight. Vertex AO deepens toward the tunnel.
        static Mesh BuildRecess()
        {
            var mb = new MeshBuilder();
            int cs = TunnelProfile.CrownSegments;
            float xC = RingConstants.WallX - FrameProud + FrameChamfer;
            var path = new[]
            {
                new Vector3(xC, 0f, TunnelProfile.AxisZ),
                new Vector3(0.5f * (xC + TunnelProfile.LoftX0), 0f, TunnelProfile.AxisZ),
                new Vector3(TunnelProfile.LoftX0, 0f, TunnelProfile.AxisZ)
            };
            int v0 = mb.VertexCount;
            mb.Loft(TunnelProfile.Floor(0f), path, Vector3.up, true, false);
            mb.Loft(TunnelProfile.Arch(0f, 0f, TunnelProfile.WallYs, cs), path, Vector3.up, true, false);
            Beat56Kit.SetAO(mb, v0, mb.VertexCount, p =>
            {
                float depth = Mathf.Clamp01((p.x - xC) / (TunnelProfile.LoftX0 - xC));
                Vector2 q = TunnelProfile.Section(p);
                float dc = Mathf.Sqrt(Sq(Mathf.Max(TunnelProfile.HalfWidth - Mathf.Abs(q.x), 0f)) + q.y * q.y);
                return (1f - 0.28f * depth) * (1f - 0.25f * Mathf.Exp(-dc / 0.2f));
            });
            return mb.Build("Beat05.DoorRecess");
        }

        static float Sq(float v) => v * v;

        // Lintel (Ceramic) above the frame: 3.6 x 0.5 m, proud 0.12 m, embedded 0.06 m into the wall.
        static Mesh BuildLintel()
        {
            var mb = new MeshBuilder();
            float x0 = RingConstants.WallX - LintelProud, x1 = RingConstants.WallX + LintelEmbed;
            mb.ChamferedBox(new Vector3(0.5f * (x0 + x1), LintelY0 + 0.5f * RingConstants.DoorLintel, TunnelProfile.AxisZ),
                new Vector3(x1 - x0, RingConstants.DoorLintel, LintelW), Chamfer);
            return mb.Build("Beat05.DoorLintel");
        }

        // WarmLamp lamp bar on the lintel's lower front edge (proud 4 cm, 5 mm below the lintel's underside: no co-planar face).
        static Mesh BuildDoorLamp()
        {
            var mb = new MeshBuilder();
            float xFront = RingConstants.WallX - LintelProud - 0.04f, xBack = RingConstants.WallX - LintelProud + 0.01f;
            float y0 = LintelY0 - 0.005f, y1 = LintelY0 + 0.075f;
            mb.ChamferedBox(new Vector3(0.5f * (xFront + xBack), 0.5f * (y0 + y1), TunnelProfile.AxisZ),
                new Vector3(xBack - xFront, y1 - y0, 0.9f), 0.008f);
            return mb.Build("Beat05.DoorLamp");
        }

        // Door glow (additive, PlainPool: radial (1 - r^2)^2 of uv0, tint x peak in the vertex colour, level _Lamps.x / 3,
        // faded out with the plain pools over s 780-806). Collar: a band between the opening edge and the profile offset
        // beyond the ellipse (uv0 = ((z - 660) / Rz, (y - Cy) / Ry), exact per pixel on the plane). Threshold: the recess
        // floor x 113.99..114.8 (uv0 = ((z - 660) / Rz, (x - 113.6) / Rx)), 2 cm above it, 2 cm short of the jambs; the
        // jambs likewise, falling off with the depth into the recess.
        static Mesh BuildDoorGlow()
        {
            var mb = new MeshBuilder();
            Color warm = RingPalette.Lin("WarmLamp");
            Color cw = warm * GlowPeak; cw.a = 1f;
            var inner = TunnelProfile.Arch(0f, 0f, TunnelProfile.NoWallYs, TunnelProfile.CrownSegments);
            var outer = TunnelProfile.Arch(GlowRz + 0.2f, 0f, TunnelProfile.NoWallYs, TunnelProfile.CrownSegments);
            int v0 = mb.VertexCount;
            Beat56Kit.Band(mb, inner, GlowX, outer, GlowX, i => Vector3.left, cw);
            for (int i = v0; i < mb.VertexCount; i++)
            {
                Vector3 p = mb.V[i];
                mb.UV[i] = new Vector2((p.z - TunnelProfile.AxisZ) / GlowRz, (p.y - GlowCy) / GlowRy);
            }
            Color ct = warm * ThresholdPeak; ct.a = 1f;
            float x0 = RingConstants.WallX - FrameProud + FrameChamfer, x1 = TunnelProfile.LoftX0;
            float hw = TunnelProfile.HalfWidth - 0.02f, y = ThresholdLift;
            int a = mb.Vtx(new Vector3(x0, y, TunnelProfile.AxisZ - hw), Vector3.up, Vector2.zero, ct);
            int b = mb.Vtx(new Vector3(x0, y, TunnelProfile.AxisZ + hw), Vector3.up, Vector2.zero, ct);
            int c = mb.Vtx(new Vector3(x1, y, TunnelProfile.AxisZ + hw), Vector3.up, Vector2.zero, ct);
            int d = mb.Vtx(new Vector3(x1, y, TunnelProfile.AxisZ - hw), Vector3.up, Vector2.zero, ct);
            Beat56Kit.Quad(mb, a, b, c, d, Vector3.up);
            for (int i = a; i <= d; i++)
            {
                Vector3 p = mb.V[i];
                mb.UV[i] = new Vector2((p.z - TunnelProfile.AxisZ) / ThresholdRz, (p.x - ThresholdX0) / ThresholdRx);
            }
            // jambs: the straight recess walls (y 0.02..1.7), facing the axis; uv0 = ((x - 113.7) / Rx, (y - Cy) / Ry)
            Color cj = warm * JambPeak; cj.a = 1f;
            float zj = TunnelProfile.HalfWidth - JambInset, yj0 = ThresholdLift, yj1 = TunnelProfile.Straight;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = TunnelProfile.AxisZ + side * zj;
                Vector3 n = new Vector3(0f, 0f, -side);
                int j0 = mb.Vtx(new Vector3(x0, yj0, z), n, Vector2.zero, cj);
                int j1 = mb.Vtx(new Vector3(x1, yj0, z), n, Vector2.zero, cj);
                int j2 = mb.Vtx(new Vector3(x1, yj1, z), n, Vector2.zero, cj);
                int j3 = mb.Vtx(new Vector3(x0, yj1, z), n, Vector2.zero, cj);
                Beat56Kit.Quad(mb, j0, j1, j2, j3, n);
                for (int i = j0; i <= j3; i++)
                {
                    Vector3 p = mb.V[i];
                    mb.UV[i] = new Vector2((p.x - JambX0) / JambRx, (p.y - JambCy) / JambRy);
                }
            }
            return mb.Build("Beat05.DoorGlow", false);
        }

        // Height gauge (PaleGold): a 14 mm staff from 0.45 to 6.03 m and 12 ticks every 0.5 m (0.22 m at whole metres,
        // 0.10 m at half metres). The ticks stand 2 mm less proud than the staff: no co-planar faces where they meet.
        static Mesh BuildTicks()
        {
            var mb = new MeshBuilder();
            float xw = RingConstants.WallX;
            float staffX0 = xw - 0.012f, staffX1 = xw + 0.01f;
            mb.ChamferedBox(new Vector3(0.5f * (staffX0 + staffX1), 0.5f * (0.45f + TickTop + 0.03f), TickZ),
                new Vector3(staffX1 - staffX0, TickTop + 0.03f - 0.45f, 0.014f), 0.003f);
            int n = Mathf.RoundToInt(TickTop / TickStep);
            for (int k = 1; k <= n; k++)
            {
                float y = k * TickStep;
                float len = (k % 2 == 0) ? 0.22f : 0.10f;
                float z0 = TickZ - 0.007f - len, z1 = TickZ + 0.004f;
                float tx0 = xw - 0.010f, tx1 = xw + 0.01f;
                mb.ChamferedBox(new Vector3(0.5f * (tx0 + tx1), y, 0.5f * (z0 + z1)), new Vector3(tx1 - tx0, 0.012f, z1 - z0), 0.003f);
            }
            return mb.Build("Beat05.DoorTicks");
        }

        // =====================================================================================================
        // Apron: draped slab (top = ground + 0.03, 1 m vertex grid, normals from the analytic ground), 2 cm chamfered
        // edges and sides that reach 0.12 m below the ground on the three open sides; the +X end (x 113.99) is a plain
        // side face (the 3 cm sill into the recess). Joints come from the Apron material (4 m panels, 8 mm lines).
        // =====================================================================================================
        static List<float> Lines(float a, float b, float step)
        {
            var l = new List<float> { a };
            float g = Mathf.Floor(a / step) * step + step;
            while (g < b - 0.05f) { if (g > a + 0.05f) l.Add(g); g += step; }
            l.Add(b);
            return l;
        }

        static Vector3 ApronNormal(float x, float z)
        {
            const float h = 0.25f;
            float dx = (ApronTop(x + h, z) - ApronTop(x - h, z)) / (2f * h);
            float dz = (ApronTop(x, z + h) - ApronTop(x, z - h)) / (2f * h);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        static float ApronAO(float x, float z)
        {
            // contact shade where the plinth stands on the slab (not in the door gap)
            float gap = RingMath.Smoothstep(TunnelProfile.HalfWidth + RingConstants.DoorFrame, TunnelProfile.HalfWidth + RingConstants.DoorFrame + 0.3f,
                Mathf.Abs(z - TunnelProfile.AxisZ));
            float px = RingMath.Smoothstep(RingConstants.WallX - PlinthDepth - 0.5f, RingConstants.WallX - PlinthDepth, x);
            return 1f - 0.22f * gap * px;
        }

        static Mesh BuildApron()
        {
            var mb = new MeshBuilder();
            var xs = Lines(ApronX0 + Chamfer, ApronX1, 1f);
            var zs = Lines(ApronZ0 + Chamfer, ApronZ1 - Chamfer, 1f);
            int nx = xs.Count, nz = zs.Count;
            var top = new int[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = xs[i], z = zs[j];
                    top[i, j] = mb.Vtx(new Vector3(x, ApronTop(x, z), z), ApronNormal(x, z), new Vector2(x, z), new Color(ApronAO(x, z), 0f, 0f, 0f));
                }
            for (int i = 0; i < nx - 1; i++)
                for (int j = 0; j < nz - 1; j++)
                    Beat56Kit.Quad(mb, top[i, j], top[i, j + 1], top[i + 1, j + 1], top[i + 1, j], Vector3.up);

            // edge x = 98 (-X): mitred chamfer + side
            var inner = new List<Vector3>(); var outer = new List<Vector3>();
            for (int j = 0; j < nz; j++)
            {
                float zo = j == 0 ? ApronZ0 : (j == nz - 1 ? ApronZ1 : zs[j]);
                inner.Add(new Vector3(xs[0], ApronTop(xs[0], zs[j]), zs[j]));
                outer.Add(new Vector3(ApronX0, ApronTop(ApronX0, zo) - Chamfer, zo));
            }
            ApronEdge(mb, inner, outer, Vector3.left);
            // edge z = 640 (-Z)
            inner.Clear(); outer.Clear();
            for (int i = 0; i < nx; i++)
            {
                float xo = i == 0 ? ApronX0 : xs[i];
                inner.Add(new Vector3(xs[i], ApronTop(xs[i], zs[0]), zs[0]));
                outer.Add(new Vector3(xo, ApronTop(xo, ApronZ0) - Chamfer, ApronZ0));
            }
            ApronEdge(mb, inner, outer, Vector3.back);
            // edge z = 680 (+Z)
            inner.Clear(); outer.Clear();
            for (int i = 0; i < nx; i++)
            {
                float xo = i == 0 ? ApronX0 : xs[i];
                inner.Add(new Vector3(xs[i], ApronTop(xs[i], zs[nz - 1]), zs[nz - 1]));
                outer.Add(new Vector3(xo, ApronTop(xo, ApronZ1) - Chamfer, ApronZ1));
            }
            ApronEdge(mb, inner, outer, Vector3.forward);
            // +X end (x 113.99): side face from the chamfer corners and the last grid column down below the ground
            var rim = new List<Vector3> { new Vector3(ApronX1, ApronTop(ApronX1, ApronZ0) - Chamfer, ApronZ0) };
            for (int j = 0; j < nz; j++) rim.Add(new Vector3(ApronX1, ApronTop(ApronX1, zs[j]), zs[j]));
            rim.Add(new Vector3(ApronX1, ApronTop(ApronX1, ApronZ1) - Chamfer, ApronZ1));
            ApronSide(mb, rim, Vector3.right);
            return mb.Build("Beat05.Apron");
        }

        static void ApronEdge(MeshBuilder mb, List<Vector3> inner, List<Vector3> outer, Vector3 side)
        {
            int n = inner.Count;
            var wear = new Color(1f, 1f, 0f, 0f);
            int b0 = mb.VertexCount;
            for (int k = 0; k < n; k++)
            {
                Vector3 nc = (side + Vector3.up).normalized;
                mb.Vtx(inner[k], nc, Vector2.zero, wear);
                mb.Vtx(outer[k], nc, Vector2.zero, wear);
            }
            for (int k = 0; k < n - 1; k++)
            {
                int a = b0 + 2 * k;
                Beat56Kit.Quad(mb, a, a + 2, a + 3, a + 1, side + Vector3.up);
            }
            ApronSide(mb, outer, side);
        }

        static void ApronSide(MeshBuilder mb, List<Vector3> rim, Vector3 side)
        {
            int n = rim.Count;
            int b0 = mb.VertexCount;
            for (int k = 0; k < n; k++)
            {
                Vector3 p = rim[k];
                float g = Ground(p.x, p.z) - ApronSkirt;
                mb.Vtx(p, side, Vector2.zero, new Color(1f, 0f, 0f, 0f));
                mb.Vtx(new Vector3(p.x, Mathf.Min(g, p.y - 0.05f), p.z), side, Vector2.zero, new Color(0.85f, 0f, 0f, 0f));
            }
            for (int k = 0; k < n - 1; k++)
            {
                int a = b0 + 2 * k;
                Beat56Kit.Quad(mb, a, a + 2, a + 3, a + 1, side);
            }
        }

        // =====================================================================================================
        // Bollards: 14 along the plain legs at s 600 + 12 k, 2.2 m to the left of the walk (the inside of the corner),
        // 1.0 m tall lathe: chamfered base, shaft, PaleGold ring, WarmLamp band (Emissive, _Lamps.x), chamfered cap.
        // =====================================================================================================
        static List<Vector3> BollardFeet()
        {
            var feet = new List<Vector3>(BollardCount);
            for (int k = 0; k < BollardCount; k++)
            {
                float s = BollardS0 + BollardPitch * k;
                Vector2 p = RingWorld.PointAt(s), d = RingWorld.DirAt(s);
                Vector2 q = p + new Vector2(-d.y, d.x) * BollardOffset;   // left of the walking direction
                feet.Add(new Vector3(q.x, Ground(q.x, q.y), q.y));
            }
            return feet;
        }

        static void Section(MeshBuilder mb, Vector3 c, float r0, float y0, float r1, float y1)
        {
            mb.Lathe(c, new[] { new Vector2(r0, y0), new Vector2(r1, y1) }, BollardSegments);
        }

        static void BuildBollards(List<Vector3> feet, out Mesh body, out Mesh ring, out Mesh lamp)
        {
            var mbB = new MeshBuilder(); var mbR = new MeshBuilder(); var mbL = new MeshBuilder();
            var wear = new Color(1f, 1f, 0f, 0f);
            var plain = new Color(1f, 0f, 0f, 0f);
            for (int i = 0; i < feet.Count; i++)
            {
                Vector3 c = feet[i];
                mbB.Col = new Color(0.86f, 0f, 0f, 0f);
                Section(mbB, c, 0.12f, -0.10f, 0.12f, 0.035f);          // base drum (foot buried 0.1 m)
                mbB.Col = wear;
                Section(mbB, c, 0.12f, 0.035f, 0.10f, 0.055f);          // base chamfer
                mbB.Col = plain;
                Section(mbB, c, 0.10f, 0.055f, 0.085f, 0.055f);         // ledge
                Section(mbB, c, 0.085f, 0.055f, 0.085f, 0.80f);         // shaft
                mbR.Col = plain;
                Section(mbR, c, 0.085f, 0.80f, 0.095f, 0.80f);          // ring underside
                Section(mbR, c, 0.095f, 0.80f, 0.095f, 0.84f);          // ring
                Section(mbR, c, 0.095f, 0.84f, 0.082f, 0.84f);          // ring top
                mbL.Col = plain;
                Section(mbL, c, 0.082f, 0.84f, 0.082f, 0.92f);          // lamp band (WarmLamp x3 at night)
                mbB.Col = plain;
                Section(mbB, c, 0.082f, 0.92f, 0.095f, 0.92f);          // cap underside
                Section(mbB, c, 0.095f, 0.92f, 0.095f, 0.98f);          // cap
                mbB.Col = wear;
                Section(mbB, c, 0.095f, 0.98f, 0.077f, 1.0f);           // cap chamfer
                mbB.Col = plain;
                mbB.Disc(c + new Vector3(0f, 1.0f, 0f), 0.077f, BollardSegments, true);
            }
            body = mbB.Build("Beat05.Bollards");
            ring = mbR.Build("Beat05.BollardRings");
            lamp = mbL.Build("Beat05.BollardLamps");
        }

        // =====================================================================================================
        // Light pools (additive, PlainPool material: radial falloff (1 - r^2)^2 from uv0 = offset / radius, tint and
        // peak in the vertex colour, level _Lamps.x / 3). Cells never straddle the apron boundary, so every cell lies
        // 0.02 m above its own surface (apron top or ground + path strip).
        // =====================================================================================================
        static Mesh BuildPools(List<Vector3> feet)
        {
            var mb = new MeshBuilder();
            Color warm = RingPalette.Lin("WarmLamp");
            for (int i = 0; i < feet.Count; i++)
                AddPool(mb, new Vector2(feet[i].x, feet[i].z), PoolRadiusBollard, warm * PoolBollard, float.PositiveInfinity);
            // door lamp pool on the apron, clipped 2 cm short of the frame front
            AddPool(mb, new Vector2(RingConstants.WallX - 1.0f, TunnelProfile.AxisZ), PoolRadiusDoor, warm * PoolDoor,
                RingConstants.WallX - FrameProud - 0.01f);
            return mb.Build("Beat05.Pools");
        }

        static void AddPool(MeshBuilder mb, Vector2 c, float radius, Color tint, float xMax)
        {
            const int cells = 8;
            var xs = PoolLines(c.x - radius, Mathf.Min(c.x + radius, xMax), radius / cells, ApronX0, ApronX1);
            var zs = PoolLines(c.y - radius, c.y + radius, radius / cells, ApronZ0, ApronZ1);
            tint.a = 1f;
            for (int i = 0; i < xs.Count - 1; i++)
                for (int j = 0; j < zs.Count - 1; j++)
                {
                    float x0 = xs[i], x1 = xs[i + 1], z0 = zs[j], z1 = zs[j + 1];
                    // skip cells entirely outside the pool radius
                    float nxp = Mathf.Clamp(c.x, x0, x1) - c.x, nzp = Mathf.Clamp(c.y, z0, z1) - c.y;
                    if (nxp * nxp + nzp * nzp >= radius * radius) continue;
                    bool apron = InApron(0.5f * (x0 + x1), 0.5f * (z0 + z1));
                    int a = PoolVtx(mb, x0, z0, c, radius, tint, apron);
                    int b = PoolVtx(mb, x0, z1, c, radius, tint, apron);
                    int d = PoolVtx(mb, x1, z1, c, radius, tint, apron);
                    int e = PoolVtx(mb, x1, z0, c, radius, tint, apron);
                    Beat56Kit.Quad(mb, a, b, d, e, Vector3.up);
                }
        }

        static int PoolVtx(MeshBuilder mb, float x, float z, Vector2 c, float radius, Color tint, bool apron)
        {
            float y = apron ? ApronTop(x, z) + PoolLiftApron : Ground(x, z) + PoolLiftTerrain;
            return mb.Vtx(new Vector3(x, y, z), Vector3.up, new Vector2((x - c.x) / radius, (z - c.y) / radius), tint);
        }

        static List<float> PoolLines(float a, float b, float step, float cut0, float cut1)
        {
            var l = new List<float>();
            for (float v = a; v < b - 1e-3f; v += step) l.Add(v);
            l.Add(b);
            if (cut0 > a + 1e-3f && cut0 < b - 1e-3f) l.Add(cut0);
            if (cut1 > a + 1e-3f && cut1 < b - 1e-3f) l.Add(cut1);
            l.Sort();
            for (int i = l.Count - 1; i > 0; i--) if (l[i] - l[i - 1] < 0.02f) l.RemoveAt(l[i] == cut0 || l[i] == cut1 ? i - 1 : i);
            return l;
        }

        // =====================================================================================================
        // Climber: a 4-vertex quad whose corners (uv0) are expanded in the vertex shader (RingWallClimber.shader) around
        // the point (113.98, 1.7 + D tan(eps(s)), 700). The mesh positions only provide generous bounds (y 0..1 200 m;
        // the highest point on the walk is ~230 m, 1.7 + 160 tan 80 = 909 m bounds any free-look pose in the plain).
        // =====================================================================================================

        /// <summary>Climber elevation (deg) seen from the eye at progress s (clamped to beat 5, s 594..774).</summary>
        public static float ClimberElevation(float s)
        {
            float s0 = RingConstants.BeatS[4], s1 = RingConstants.BeatS[5];
            return Mathf.Lerp(ClimberEps0, ClimberEps1, Mathf.Clamp01((s - s0) / Mathf.Max(s1 - s0, 1e-3f)));
        }

        /// <summary>Flat-world climber point for progress s and a camera at camXZ (x, z): the C# twin of the vertex
        /// stage of RingWallClimber.shader (tests and composition checks).</summary>
        public static Vector3 ClimberPoint(float s, Vector2 camXZ)
        {
            float d = Vector2.Distance(new Vector2(RingConstants.WallX, RingConstants.ClimberZ), camXZ);
            float eps = Mathf.Min(ClimberElevation(s), 85f) * Mathf.Deg2Rad;
            return new Vector3(ClimberX, RingConstants.Eye + d * Mathf.Tan(eps), RingConstants.ClimberZ);
        }

        static Mesh BuildClimberMesh()
        {
            var mb = new MeshBuilder();
            float z = RingConstants.ClimberZ;
            int a = mb.Vtx(new Vector3(ClimberX, 0f, z - 1f), Vector3.left, new Vector2(0f, 0f));
            int b = mb.Vtx(new Vector3(ClimberX, 0f, z + 1f), Vector3.left, new Vector2(1f, 0f));
            int c = mb.Vtx(new Vector3(ClimberX, 1200f, z + 1f), Vector3.left, new Vector2(1f, 1f));
            int d = mb.Vtx(new Vector3(ClimberX, 1200f, z - 1f), Vector3.left, new Vector2(0f, 1f));
            mb.Quad(a, b, c, d);
            return mb.Build("Beat05.Climber", false);
        }
    }
}
