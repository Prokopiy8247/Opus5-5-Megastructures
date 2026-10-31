using System.Collections;
using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// Builds the two GPU-placed fields of the meadow (MASTER_PROMPT 14.5.2, 14.2.6 M04) on the shared InstancedField:
    /// grass: 51 200 instances (160 x 160 cells of 0.35 m x 2 candidates) of a 6-triangle tuft, one copy in NearValley
    /// and one in NearPlain (identical draws, so the swap at s 576 does not change the grass; see TerrainIndirectField);
    /// flowers: 18 496 candidates (136 x 136 cells of 0.6 m, acceptance 0.86, about 12 000 heads within 40 m) in
    /// NearValley only.
    /// Shadows: the fields cast none. Tufts are 0.16-0.36 m tall, sway every frame and are 1-3 px wide at 20 m, so their
    /// shadow-map footprint (cascade texels of several cm over the first 220 m) would only add flicker for a few hundred
    /// thousand extra vertices per cascade; they receive the terrain's stable shadows and their colour converges to the
    /// terrain, whose own shadow is what the eye reads.
    /// </summary>
    public sealed class GrassFieldBuilder : IBeatBuilder
    {
        public int Order => 12;
        public string Name => "Grass";

        /// <summary>The grass exists in both near sets (14.4: NearValley s 0-576, NearPlain s 576-810).</summary>
        static readonly RingSet[] GrassSets = { RingSet.NearValley, RingSet.NearPlain };
        /// <summary>Flowers: NearValley only (14.5.2).</summary>
        static readonly RingSet[] FlowerSets = { RingSet.NearValley };

        public IEnumerator Build(BuildContext ctx)
        {
            Bounds flat = FieldBounds(out float bendDist);

            // ---- grass tufts ----
            Mesh tuft = TuftMesh();
            int grassDrawn = GrassLayout.ExpectedDrawn(TerrainConfig.GrassCell, TerrainConfig.GrassCandidates, 1f,
                                                       TerrainConfig.GrassFadeStart, TerrainConfig.GrassFadeEnd, TerrainConfig.GrassRadius);
            InstancedField[] grass = TerrainIndirectField.Create(ctx, "Grass", tuft, RingMat.Grass, TerrainConfig.GrassInstances,
                                                                 flat, bendDist, false, GrassSets);
            int tuftTris = Tris(tuft);
            for (int i = 0; i < grass.Length; i++)
                Info(grass[i], "Grass", tuftTris, grassDrawn, TerrainConfig.GrassCell, TerrainConfig.GrassFadeStart, TerrainConfig.GrassFadeEnd);
            if (grass.Length > 0 && grass[0].Holder != null) ctx.Register("Grass", grass[0].Holder);
            if (grass.Length > 1 && grass[1].Holder != null) ctx.Register("Grass.Plain", grass[1].Holder);
            Count(ctx, grassDrawn * tuftTris);   // one copy is visible at a time
            yield return null;

            // ---- flowers ----
            Mesh flower = FlowerMesh();
            int flowerDrawn = GrassLayout.ExpectedDrawn(TerrainConfig.FlowerCell, 1, TerrainConfig.FlowerAccept,
                                                        TerrainConfig.FlowerFadeStart, TerrainConfig.FlowerFadeEnd, TerrainConfig.FlowerRadius);
            InstancedField[] flowers = TerrainIndirectField.Create(ctx, "Flowers", flower, RingMat.Flower, TerrainConfig.FlowerInstances,
                                                                   flat, bendDist, false, FlowerSets);
            int flowerTris = Tris(flower);
            for (int i = 0; i < flowers.Length; i++)
                Info(flowers[i], "Flowers", flowerTris, flowerDrawn, TerrainConfig.FlowerCell, TerrainConfig.FlowerFadeStart, TerrainConfig.FlowerFadeEnd);
            if (flowers.Length > 0 && flowers[0].Holder != null) ctx.Register("Flowers", flowers[0].Holder);
            Count(ctx, flowerDrawn * flowerTris);

            Debug.Log("[Ring] Grass: " + TerrainConfig.GrassInstances + " instances launched, about " + grassDrawn + " drawn (" +
                      tuftTris + " tris each, sets NearValley + NearPlain); flowers: " + TerrainConfig.FlowerInstances +
                      " launched, about " + flowerDrawn + " drawn (" + flowerTris + " tris each); indirect " +
                      SystemInfo.supportsIndirectArgumentsBuffer);
        }

        static int Tris(Mesh m) => m != null ? (int)m.GetIndexCount(0) / 3 : 0;

        static void Count(BuildContext ctx, int tris)
        {
            ctx.Triangles += tris;
            ctx.TrianglesByBuilder.TryGetValue(ctx.CurrentBuilder, out int t0);
            ctx.TrianglesByBuilder[ctx.CurrentBuilder] = t0 + tris;
        }

        static void Info(InstancedField f, string kind, int trisPerInstance, int drawn, float cell, float fade0, float fade1)
        {
            if (f == null || f.Holder == null) return;
            var info = f.Holder.AddComponent<TerrainInfo>();
            info.Kind = kind;
            info.Instances = f.InstanceCount;
            info.ExpectedDrawn = drawn;
            info.Cell = cell;
            info.FadeStart = fade0;
            info.FadeEnd = fade1;
            info.MeshTriangles = f.InstanceCount * trisPerInstance;
            info.VisibleTriangles = drawn * trisPerInstance;
        }

        /// <summary>Fixed flat world bounds of both fields: the walk region grown by the largest field radius (instances
        /// are always within 41 m of the camera, so the bend lifts them by at most 41^2 / 2R = 5 cm: bendDist).</summary>
        static Bounds FieldBounds(out float bendDist)
        {
            float m = TerrainConfig.FlowerRadius + 5f;
            Vector2 a = TerrainConfig.CamMin - new Vector2(m, m), b = TerrainConfig.CamMax + new Vector2(m, m);
            var bb = new Bounds();
            bb.SetMinMax(new Vector3(a.x, -20f, a.y), new Vector3(b.x, 45f, b.y));
            bendDist = m;
            return bb;
        }

        /// <summary>Tuft: 3 blades x 1 quad (6 triangles). positionOS = (across -0.5..0.5, height 0..1, blade 0..2);
        /// the shape comes from RingGrass.shader (Cull Off: one face per blade).</summary>
        static Mesh TuftMesh()
        {
            var mb = new MeshBuilder();
            for (int b = 0; b < 3; b++)
            {
                int i0 = mb.Vtx(new Vector3(-0.5f, 0f, b), Vector3.up, new Vector2(0f, 0f));
                int i1 = mb.Vtx(new Vector3(-0.5f, 1f, b), Vector3.up, new Vector2(0f, 1f));
                int i2 = mb.Vtx(new Vector3(0.5f, 1f, b), Vector3.up, new Vector2(1f, 1f));
                int i3 = mb.Vtx(new Vector3(0.5f, 0f, b), Vector3.up, new Vector2(1f, 0f));
                mb.Quad(i0, i1, i2, i3);
            }
            return mb.Build("GrassTuft", true);
        }

        /// <summary>Flower: stem quad (z 0: across, height) + hexagonal head (z 1 centre, z 2 rim (cos, sin)): 8 triangles.</summary>
        static Mesh FlowerMesh()
        {
            var mb = new MeshBuilder();
            int s0 = mb.Vtx(new Vector3(-0.5f, 0f, 0f), Vector3.up, new Vector2(0f, 0f));
            int s1 = mb.Vtx(new Vector3(-0.5f, 1f, 0f), Vector3.up, new Vector2(0f, 1f));
            int s2 = mb.Vtx(new Vector3(0.5f, 1f, 0f), Vector3.up, new Vector2(1f, 1f));
            int s3 = mb.Vtx(new Vector3(0.5f, 0f, 0f), Vector3.up, new Vector2(1f, 0f));
            mb.Quad(s0, s1, s2, s3);
            int c = mb.Vtx(new Vector3(0f, 0f, 1f), Vector3.up, new Vector2(0.5f, 0.5f));
            int first = mb.VertexCount;
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                mb.Vtx(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 2f), Vector3.up, new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int k = 0; k < 6; k++) mb.Tri(c, first + k, first + (k + 1) % 6);
            return mb.Build("Flower", true);
        }
    }
}
