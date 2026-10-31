// Beat07_Top: the pier, the pad and the beacon at the top (MASTER_PROMPT 14.5.7, M06, M11), built at load.
//  * Top_PierPad: ONE welded slab on the outline of PierPadOutline (3.0 m pier from P5 united with the pad
//    circle r = 3.0 at (0, 424), 0.8 m fillets). Single top surface at y = 146.0, the deck section of the
//    helix (0.02 m top chamfer, vertical side, 0.25 m lower bevel, 0.4 m deep) swept around the outline, so its
//    first ring is bit-identical to the last ring of the helix deck (no seam, no overlap). Under the pad a
//    chamfered boss of r = 2.4 brings the pad to 0.6 m thickness (its top is buried 5 cm in the slab, no
//    co-planar faces). The top and bottom faces are symmetric trapezoid strips over the outline samples (no
//    T-junctions). UV1.x = 1.5 - distance from the edge (>= 0): the edge stripe follows the whole outline.
//  * Top_PierArms: support arms 41-43 under the pier at z = 400, 411, 422 (arm rule: they end in the cable).
//  * Beacon: pillar lathe 1.6 m (D-051; spec 1.2) (r 0.25 -> 0.12, 24 segments, BeaconPillar) at (0, 146, 424), a chamfered
//    octahedron crystal 0.5 m tall above it (BeaconCrystal), and two halo quads r = 3 m and 12 m (BeaconHalo;
//    the shader turns them to the camera; the mesh bounds hold the full radius; _HaloRadius per renderer via
//    MaterialPropertyBlock and also stored in uv1.x).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Megastructure.Beats
{
    public sealed class Beat07_Top : IBeatBuilder
    {
        public string Name => "Beat07_Top";

        public const int PillarSegments = 24;              // 14.5.7: pillar lathe, 24 segments
        public const double PillarEmbed = 0.03;             // the pillar foot is buried 3 cm in the pad
        public const double PillarTopChamfer = 0.01;
        public const double CrystalHalfWidth = 0.17;
        public const double CrystalTipCorrection = 0.010048;   // lead: the tip chamfer cut 0.0201 m in total (measured 0.4799 m)        // elongated octahedron (0.5 m tall, 0.34 m wide)
        public const double CrystalChamfer = 0.012;
        public const double CrystalEmbed = 0.03;            // the crystal tip sits 3 cm inside the pillar top
        public const double CrystalYawDeg = 22.5;
        public const int BossSegments = 64;

        static readonly int s_haloRadius = Shader.PropertyToID("_HaloRadius");
        static readonly int s_haloEmission = Shader.PropertyToID("_Emission");

        /// <summary>The beacon position of 14.5.7 (pillar foot centre on the pad top).</summary>
        public static D3 BeaconBase => new D3(0.0, ThreadConstants.TopY, ThreadConstants.PadCentreZ);
        public static double CrystalBottomY => ThreadConstants.TopY + ThreadConstants.BeaconPillarHeight - CrystalEmbed;
        public static D3 CrystalCentre => new D3(0.0, CrystalBottomY + 0.5 * ThreadConstants.BeaconCrystalHeight, ThreadConstants.PadCentreZ);

        public void Build(WorldContext ctx)
        {
            var root = new GameObject(Name).transform;
            root.SetParent(ctx.NearRoot, false);
            MaterialLibrary lib = ctx.Materials;

            var mb = new MeshBuilder(4096);
            BuildPierPad(mb);
            Mesh pier = mb.ToMesh("Top_PierPad");
            ctx.AddMesh(root, "Top_PierPad", pier, lib != null ? lib.Deck : null, true, true);

            var ab = new MeshBuilder(256);
            WalkwayLayout.BuildArms(ab, ThreadConstants.ArmCountHelix, WalkwayLayout.ArmCount);
            Mesh arms = ab.ToMesh("Top_PierArms");
            ctx.AddMesh(root, "Top_PierArms", arms, lib != null ? lib.Arm : null, true, true);

            var pb = new MeshBuilder(256);
            BuildPillar(pb);
            Mesh pillar = pb.ToMesh("Beacon_Pillar");
            ctx.AddMesh(root, "Beacon_Pillar", pillar, lib != null ? lib.BeaconPillar : null, true, true);

            var cb = new MeshBuilder(256);
            BuildCrystal(cb);
            Mesh crystal = cb.ToMesh("Beacon_Crystal");
            ctx.AddMesh(root, "Beacon_Crystal", crystal, lib != null ? lib.BeaconCrystal : null, false, false);

            double[] haloR = { ThreadConstants.BeaconHaloR0, ThreadConstants.BeaconHaloR1 };
            for (int h = 0; h < haloR.Length; h++)
            {
                Mesh halo = BuildHaloMesh(haloR[h], $"Beacon_Halo_{h}");
                var mr = ctx.AddMesh(root, $"Beacon_Halo_{h}", halo, lib != null ? lib.BeaconHalo : null, false, false);
                mr.transform.localPosition = BeatMeshUtil.V(CrystalCentre);
                var mpb = new MaterialPropertyBlock();
                mpb.SetFloat(s_haloRadius, (float)haloR[h]);
                // equal energy: the larger halo is dimmer by (R0 / R)^2, so the 12 m glow never washes the night sky (D-029)
                double haloGain = (ThreadConstants.BeaconHaloR0 / haloR[h]) * (ThreadConstants.BeaconHaloR0 / haloR[h]);
                if (lib != null && lib.BeaconHalo != null) mpb.SetFloat(s_haloEmission, (float)(lib.BeaconHalo.GetFloat(s_haloEmission) * haloGain));
                mr.SetPropertyBlock(mpb);
            }

            ctx.Log($"{Name}: pier/pad {pier.vertexCount} v / {MeshKit.TriangleCount(pier)} t, {ThreadConstants.ArmCountPier} arms, beacon " +
                    $"{MeshKit.TriangleCount(pillar) + MeshKit.TriangleCount(crystal)} t + 2 halos");
        }

        // ---------------------------------------------------------------- pier and pad
        struct EdgeDef
        {
            public double d0, h0, d1, h1;   // profile edge from (d0, h0) to (d1, h1): d = inward inset, h = height to the top
            public double nIn, nUp;         // 3D normal = -inward * nIn + up * nUp ... (nIn: outward horizontal weight)
            public double ao;
        }

        /// <summary>The welded pier + pad slab and the pad boss (see the header).</summary>
        public static void BuildPierPad(MeshBuilder mb)
        {
            double T = ThreadConstants.DeckThickness, C = ThreadConstants.DeckChamfer, B = WalkwayLayout.DeckBevel, S = WalkwayLayout.TopBandInset;
            double W = PierPadOutline.W, top = ThreadConstants.TopY;
            double r2 = Math.Sqrt(0.5);
            var edges = new[]
            {
                new EdgeDef { d0 = S, h0 = 0, d1 = C, h1 = 0, nIn = 0, nUp = 1, ao = 1.0 },              // top edge band
                new EdgeDef { d0 = C, h0 = 0, d1 = 0, h1 = -C, nIn = r2, nUp = r2, ao = 1.0 },           // top chamfer
                new EdgeDef { d0 = 0, h0 = -C, d1 = 0, h1 = -T + B, nIn = 1, nUp = 0, ao = 0.95 },        // side
                new EdgeDef { d0 = 0, h0 = -T + B, d1 = B, h1 = -T, nIn = r2, nUp = -r2, ao = 0.88 },    // lower bevel
            };
            var smp = PierPadOutline.Samples();
            int n = smp.Count;
            double sAlong0 = ThreadConstants.HelixEndS - ThreadConstants.PierStartZ;   // uv0.x continues the deck's s
            for (int e = 0; e < edges.Length; e++)
            {
                var ed = edges[e];
                double len = Math.Sqrt((ed.d1 - ed.d0) * (ed.d1 - ed.d0) + (ed.h1 - ed.h0) * (ed.h1 - ed.h0));
                Color col = BeatMeshUtil.Ao(ed.ao);
                int start = mb.VertexCount;
                for (int i = 0; i < n; i++)
                {
                    var q = smp[i];
                    var nrm = new Vector3((float)(-q.nx * ed.nIn), (float)ed.nUp, (float)(-q.nz * ed.nIn));
                    for (int k = 0; k < 2; k++)
                    {
                        double d = k == 0 ? ed.d0 : ed.d1, h = k == 0 ? ed.h0 : ed.h1;
                        var p = new D3(q.x + q.nx * d, top + h, q.z + q.nz * d);
                        mb.Add(BeatMeshUtil.V(p), nrm, new Vector2((float)(sAlong0 + q.z), (float)(k == 0 ? 0.0 : len)), col,
                               new Vector4((float)-q.nz, 0f, (float)q.nx, 1f), new Vector4((float)(W - d), (float)h, 0f, 0f));
                    }
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a0 = start + 2 * i, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    BeatMeshUtil.QuadFacing(mb, a0, b0, b1, a1, mb.Normals[a0] + mb.Normals[a1]);
                }
            }
            // top and bottom faces: symmetric trapezoid strips between the right chain i and the left chain n-1-i
            Face(mb, smp, S, top, true, W - S, 0.0, 1.0, sAlong0);
            Face(mb, smp, B, top - T, false, W - B, -T, 0.80, sAlong0);

            // pad boss: pad thickness 0.6 under the beacon
            double bt = ThreadConstants.PadThickness, ch = PierPadOutline.BossChamfer, R = PierPadOutline.BossRadius;
            var prof = new List<Vector2>
            {
                new Vector2((float)-bt, (float)(R - ch)),
                new Vector2((float)(-bt + ch), (float)R),
                new Vector2((float)(-bt + ch), (float)R),
                new Vector2((float)(-T + PierPadOutline.BossEmbed), (float)R),
            };
            Color bossCol = BeatMeshUtil.Ao(0.80);
            MeshKit.LatheColor bossColour = (y, a, ring) => bossCol;
            MeshKit.Lathe(mb, prof, BossSegments, new Vector3(0f, (float)top, (float)ThreadConstants.PadCentreZ), Vector2.one,
                          null, bossColour, true, true);
        }

        static void Face(MeshBuilder mb, List<OutlineSample> smp, double d, double y, bool up, double uvx, double uvy, double ao, double sAlong0)
        {
            int n = smp.Count, m = PierPadOutline.FarIndex;
            var nrm = up ? Vector3.up : Vector3.down;
            Color col = BeatMeshUtil.Ao(ao);
            var uv1 = new Vector4((float)uvx, (float)uvy, 0f, 0f);
            var tan = new Vector4(0f, 0f, 1f, 1f);
            int start = mb.VertexCount;
            // vertices: chain index i = 0..m (right, far point at m) then the left chain for i = 0..m-1 (sample n-1-i)
            for (int i = 0; i <= m; i++)
            {
                var q = smp[i];
                double x = q.x + q.nx * d, z = q.z + q.nz * d;
                mb.Add(new Vector3((float)x, (float)y, (float)z), nrm, new Vector2((float)(sAlong0 + q.z), (float)x), col, tan, uv1);
            }
            for (int i = 0; i < m; i++)
            {
                var q = smp[n - 1 - i];
                double x = q.x + q.nx * d, z = q.z + q.nz * d;
                mb.Add(new Vector3((float)x, (float)y, (float)z), nrm, new Vector2((float)(sAlong0 + q.z), (float)x), col, tan, uv1);
            }
            int R(int i) => start + i;
            int L(int i) => start + m + 1 + i;
            for (int i = 0; i < m - 1; i++) BeatMeshUtil.QuadFacing(mb, R(i), R(i + 1), L(i + 1), L(i), nrm);
            BeatMeshUtil.TriFacing(mb, R(m - 1), R(m), L(m - 1), nrm);
        }

        // ---------------------------------------------------------------- beacon
        public static void BuildPillar(MeshBuilder mb)
        {
            double H = ThreadConstants.BeaconPillarHeight, r0 = ThreadConstants.BeaconPillarR0, r1 = ThreadConstants.BeaconPillarR1;
            Func<double, double> rad = y => r0 + (r1 - r0) * (y / H);
            double yc = H - PillarTopChamfer;
            var prof = new List<Vector2>
            {
                new Vector2((float)-PillarEmbed, (float)rad(-PillarEmbed)),
                new Vector2((float)yc, (float)rad(yc)),
                new Vector2((float)yc, (float)rad(yc)),
                new Vector2((float)H, (float)(rad(yc) - PillarTopChamfer)),
            };
            Color col = BeatMeshUtil.Ao(1.0);
            MeshKit.LatheColor colour = (y, a, ring) => col;
            MeshKit.Lathe(mb, prof, PillarSegments, BeatMeshUtil.V(BeaconBase), Vector2.one, null, colour, false, true);
        }

        public static void BuildCrystal(MeshBuilder mb)
        {
            var normals = new List<D3>(32);
            var offsets = new List<double>(32);
            double hh = 0.5 * ThreadConstants.BeaconCrystalHeight + CrystalChamfer + CrystalTipCorrection;   // tips chamfered back to 0.5 m overall
            BeatMeshUtil.ChamferedOctahedronPlanes(CrystalCentre, hh, CrystalHalfWidth, CrystalChamfer, CrystalYawDeg * ThreadMath.Deg, normals, offsets);
            BeatMeshUtil.ConvexFromPlanes(mb, normals, offsets, BeatMeshUtil.Ao(1.0), 2.0, CrystalCentre);
        }

        /// <summary>Halo quad in object space (the GameObject sits at the halo centre): corners at uv0 * R, uv0 in [-1, 1], uv1.x = R; bounds = full radius cube.</summary>
        public static Mesh BuildHaloMesh(double radius, string name)
        {
            var mb = new MeshBuilder(4);
            float r = (float)radius;
            var nrm = new Vector3(0f, 0f, -1f);
            var uv1 = new Vector4(r, 0f, 0f, 0f);
            var tan = new Vector4(1f, 0f, 0f, 1f);
            Color col = BeatMeshUtil.Ao(1.0);
            int a = mb.Add(new Vector3(-r, -r, 0f), nrm, new Vector2(-1f, -1f), col, tan, uv1);
            int b = mb.Add(new Vector3(-r, r, 0f), nrm, new Vector2(-1f, 1f), col, tan, uv1);
            int c = mb.Add(new Vector3(r, r, 0f), nrm, new Vector2(1f, 1f), col, tan, uv1);
            int d = mb.Add(new Vector3(r, -r, 0f), nrm, new Vector2(1f, -1f), col, tan, uv1);
            BeatMeshUtil.QuadFacing(mb, a, b, c, d, nrm);
            Mesh m = mb.ToMesh(name);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * r));
            return m;
        }
    }
}
