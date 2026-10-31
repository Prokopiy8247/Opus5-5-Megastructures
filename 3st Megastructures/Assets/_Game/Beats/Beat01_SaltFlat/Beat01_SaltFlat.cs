// Beat01_SaltFlat: the salt flat (MASTER_PROMPT 14.5.1). The only geometry of beat 1 is the PolarGrid ground
// (SaltGround); the far thread, the sky and the mist bank are analytic in the shaders and the first colossus pair
// belongs to Beat02_Avenue. The ground lives in the sinking ground group and follows the camera per ring group.
using UnityEngine;

namespace Megastructure.Beats
{
    public sealed class Beat01_SaltFlat : IBeatBuilder
    {
        public string Name => "Beat01_SaltFlat";

        public void Build(WorldContext ctx)
        {
            Transform parent = ctx.GroundGroup != null ? ctx.GroundGroup : ctx.NearRoot;
            var root = new GameObject("SaltFlat").transform;
            root.SetParent(parent, false);
            Material salt = ctx.Materials != null ? ctx.Materials.Salt : null;
            if (salt == null) Debug.LogError("[THREAD-FAIL] Beat01_SaltFlat: no Salt material");

            var groups = new Transform[SaltGround.GroupCount];
            long tris = 0;
            for (int g = 0; g < groups.Length; g++)
            {
                MeshBuilder mb = SaltGround.BuildGroup(g);
                Mesh mesh = mb.ToMesh("SaltGround_" + g);
                if (!MeshKit.Validate(mesh, out string report)) Debug.LogError("[THREAD-FAIL] salt ground: " + report);
                // the plane receives shadows (ledges, bollards, stairs) but casts none (no self-shadow acne)
                MeshRenderer mr = ctx.AddMesh(root, "SaltGround_" + g, mesh, salt, false, true);
                mr.transform.localPosition = new Vector3(0f, SaltGround.GroupY(g), 0f);
                groups[g] = mr.transform;
                tris += mb.TriangleCount;
            }
            var follower = new SaltGroundFollower(root, groups);
            ctx.Register(follower);
            // place the groups for the spawn pose before the first frame
            var f = new FrameState { cameraPos = ctx.Camera != null ? ctx.Camera.transform.position : new Vector3(0f, ThreadConstants.EyeHeight, 0f), groundActive = true };
            follower.OnFrame(in f);
            ctx.Log($"Beat01_SaltFlat: polar ground {groups.Length} ring groups, {tris} triangles, outer radius {SaltGround.GroupOuterRadius(groups.Length - 1):F0} m");
        }
    }
}
