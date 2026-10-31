using System;
// Beat05_Spiral: the walkway of the spiral ascent (MASTER_PROMPT 14.5.5), built at load behind the black fade.
//  * Walkway_Deck: one continuous sweep of the 3.0 x 0.4 m deck (0.02 m top chamfers, 0.25 m lower bevels)
//    from s = 397 on the terrace to P5 (s = 840), Deck material, casts and receives shadows.
//  * Walkway_Rail: the ONE continuous 0.07 m 8-gon handrail at 1.0 m (up the cable-side helix edge, along the
//    pier, around the far arc of the pad, back down the outer edge), Handrail material (ThreadLine
//    _LINE_TUBE), no shadow casting. It is built here because a tube split at s = 840 would need two rings
//    at the 14 degree grade kink and open a wedge.
//  * Rail posts: 440 on the helix plus the pier and pad posts (14.5.7), instanced (RailPost, dithered fade
//    52.5-67.5 m, no shadow casting), drawn by an InstancedSet updater.
//  * Walkway_Arms: the 40 helix support arms (s = 400 + 11 i); the 3 pier arms are built by Beat07_Top.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    public sealed class Beat05_Spiral : IBeatBuilder
    {
        public string Name => "Beat05_Spiral";

        public void Build(WorldContext ctx)
        {
            var root = new GameObject(Name).transform;
            root.SetParent(ctx.NearRoot, false);
            MaterialLibrary lib = ctx.Materials;
            int layer = ctx.NearRoot != null ? ctx.NearRoot.gameObject.layer : 0;

            // deck
            var mb = new MeshBuilder(8192);
            WalkwayLayout.BuildDeck(mb);
            Mesh deck = mb.ToMesh("Walkway_Deck");
            ctx.AddMesh(root, "Walkway_Deck", deck, lib != null ? lib.Deck : null, true, true);

            // handrail (one tube)
            var rb = new MeshBuilder(8192);
            WalkwayLayout.BuildRail(rb);
            Mesh rail = rb.ToMesh("Walkway_Rail");
            ctx.AddMesh(root, "Walkway_Rail", rail, lib != null ? lib.Handrail : null, false, true);

            // helix support arms
            var ab = new MeshBuilder(4096);
            WalkwayLayout.BuildArms(ab, 0, ThreadConstants.ArmCountHelix);
            Mesh arms = ab.ToMesh("Walkway_Arms");
            ctx.AddMesh(root, "Walkway_Arms", arms, lib != null ? lib.Arm : null, true, true);

            // rail posts (instanced)
            var pb = new MeshBuilder(64);
            WalkwayLayout.BuildPost(pb);
            Mesh post = pb.ToMesh("RailPost");
            var placements = WalkwayLayout.Posts();
            int helixPosts = 2 * WalkwayLayout.HelixPostStations;   // sorted along s, both sides interleaved
            var helixM = new List<Matrix4x4>(helixPosts);
            var pierM = new List<Matrix4x4>(placements.Count - helixPosts);
            int stray = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                if (!PostOnDeckEdge(placements[i].basePos))
                {
                    if (stray++ < 8) ctx.Log($"{Name}: stray post {i} at {placements[i].basePos} dropped");
                    continue;
                }
                (i < helixPosts ? helixM : pierM).Add(WalkwayLayout.PostMatrix(placements[i]));
            }
            if (stray > 0) ctx.Log($"{Name}: {stray} stray posts dropped");
            Material postMat = lib != null ? lib.RailPost : null;
            float fadeFar = postMat != null && postMat.HasProperty(ShaderIds.FadeFar) ? postMat.GetFloat(ShaderIds.FadeFar) : 0f;
            var posts = new InstancedSet("RailPosts", fadeFar > 0f ? fadeFar + 2f : 0f);
            bool ok = posts.Add(post, pb, postMat, null, helixM, WalkwayLayout.PostsPerChunk, layer, ShadowCastingMode.Off, true);
            ok &= posts.Add(post, pb, postMat, null, pierM, InstancedSet.MaxPerCall, layer, ShadowCastingMode.Off, true);
            posts.Finish();
            if (!ok) ctx.Log($"{Name}: RailPost material missing, posts not drawn");
            if (SystemInfo.supportsInstancing) ctx.Register(posts);
            else { posts.MakeStatic(ctx, root); ctx.Log($"{Name}: instancing unsupported, posts baked static"); }
            ctx.Triangles += posts.Triangles;

            ctx.Log($"{Name}: deck {deck.vertexCount} v / {MeshKit.TriangleCount(deck)} t, rail {MeshKit.TriangleCount(rail)} t, " +
                    $"{ThreadConstants.ArmCountHelix} arms {MeshKit.TriangleCount(arms)} t, {placements.Count} posts in {posts.ChunkCount} instanced chunks");
        }
    
        /// <summary>A post must stand on a deck edge: the helix rail circle, the pier sides or the pad rim (lead check).</summary>
        static bool PostOnDeckEdge(D3 p)
        {
            double dx = p.x - ThreadConstants.CableAxisX, dz = p.z - ThreadConstants.CableAxisZ;
            double r = Math.Sqrt(dx * dx + dz * dz);
            double half = 0.5 * ThreadConstants.DeckWidth;
            bool helix = Math.Abs(r - ThreadConstants.HelixRadius) <= half + 0.05 && p.y < ThreadConstants.TopY + 0.05;
            bool pier = Math.Abs(p.y - ThreadConstants.TopY) < 0.05 && Math.Abs(p.x) <= half + 0.05
                        && p.z >= ThreadConstants.PierStartZ - 0.05 && p.z <= ThreadConstants.PadCentreZ + ThreadConstants.PadRadius + 0.05;
            double px = p.x, pz = p.z - ThreadConstants.PadCentreZ;
            bool pad = Math.Abs(p.y - ThreadConstants.TopY) < 0.05 && Math.Sqrt(px * px + pz * pz) <= ThreadConstants.PadRadius + 0.05;
            return helix || pier || pad;
        }
    }
}
