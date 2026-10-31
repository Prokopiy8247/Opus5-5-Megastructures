// Beat03_Stairs: builds the stairs into the haze (MASTER_PROMPT 14.5.3) under the sinking ground group.
// Steps (treads + nosings + risers, Steps material, M03), stringers (Stringer material, M04) and the
// 12 bollards (Bollard material) as static SRP-Batcher meshes. Runs once at load (GameBootstrap).
//
// Shadows: the stairs cast shadow maps (onto the salt) but do not receive them. Step self-shadowing
// at the beat 3/4 sun (elevation 22-24 deg, from the front) would leave only 3.5-7 cm lit strips at
// every nosing; those strips are sub-pixel beyond ~40 m, flicker with the cascades and vanish at the
// 160 m shadow distance, i.e. shimmer/moire (Part 10 items 3, 6). Tread/riser contrast comes from the
// normals (M03) instead, identical at every distance. Bollards cast no shadow for the same reason
// (their shadow would be cut where it leaves a receiving surface onto the non-receiving treads).
using UnityEngine;

namespace Megastructure.Beats
{
    public sealed class Beat03_Stairs : IBeatBuilder
    {
        public string Name => "Beat03_Stairs";

        public void Build(WorldContext ctx)
        {
            Transform parent = ctx.GroundGroup;
            if (parent == null) Debug.LogError("[THREAD-FAIL] Beat03_Stairs: ctx.GroundGroup is null; building at the scene root");
            var root = new GameObject("Beat03_Stairs").transform;
            root.SetParent(parent, false);

            MaterialLibrary mats = ctx.Materials;
            Material steps = Pick(mats != null ? mats.Steps : null, "Steps");
            Material stringer = Pick(mats != null ? mats.Stringer : null, "Stringer");
            Material bollard = Pick(mats != null ? mats.Bollard : null, "Bollard");

            Mesh mSteps = StairBuilder.BuildSteps().ToMesh("Stairs_Steps");
            Mesh mStringers = StairBuilder.BuildStringers().ToMesh("Stairs_Stringers");
            Mesh mBollards = StairBuilder.BuildBollards().ToMesh("Stairs_Bollards");

            ctx.AddMesh(root, "Stairs_Steps", mSteps, steps, true, false);
            ctx.AddMesh(root, "Stairs_Stringers", mStringers, stringer, true, false);
            ctx.AddMesh(root, "Stairs_Bollards", mBollards, bollard, false, false);

            ctx.Log($"Beat03_Stairs: steps {MeshKit.TriangleCount(mSteps)} tris, stringers {MeshKit.TriangleCount(mStringers)} tris, " +
                    $"bollards {StairBuilder.BollardsPerEdge * 2} + {StairBuilder.WalkLinePerSide * 2} walk-line ({MeshKit.TriangleCount(mBollards)} tris)");
        }

        static Material Pick(Material m, string name)
        {
            if (m == null) Debug.LogError($"[THREAD-FAIL] Beat03_Stairs: MaterialLibrary.{name} is missing");
            return m;
        }
    }
}
