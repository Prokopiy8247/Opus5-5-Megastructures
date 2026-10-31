// Beat02_Avenue: the avenue of colossi (MASTER_PROMPT 14.5.2). Eight faceless watchers in four pairs at
// z = ThreadConstants.ColossusPairZ, cool row (TEAL) on the left, warm row (CORAL) on the right, each generated
// separately from its own seed (never mirrored), plus their flat ground shadows. Everything is static and lives
// under the sinking ground group; the beat registers no updater.
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure.Beats
{
    public sealed class Beat02_Avenue : IBeatBuilder
    {
        public string Name => "Beat02_Avenue";

        public void Build(WorldContext ctx)
        {
            Transform parent = ctx.GroundGroup != null ? ctx.GroundGroup : ctx.NearRoot;
            var root = new GameObject("Avenue").transform;
            root.SetParent(parent, false);
            var lib = ctx.Materials;
            if (lib == null) Debug.LogError("[THREAD-FAIL] Beat02_Avenue: no MaterialLibrary");

            int pairs = ThreadConstants.ColossusPairZ.Length;
            long colossusTris = 0, shadowTris = 0;
            for (int pair = 0; pair < pairs; pair++)
            {
                for (int side = 0; side < 2; side++)
                {
                    ColossusGeometry g = ColossusBuilder.Generate(pair, side);
                    var croot = new GameObject(g.Name).transform;
                    croot.SetParent(root, false);
                    croot.localPosition = g.RootPosition;

                    Material robe = lib != null ? (side == 0 ? lib.RobeTeal : lib.RobeCoral) : null;
                    Material ink = lib != null ? lib.HoodInk : null;

                    // robe + hood + sleeves: a giant, casts no shadow maps (Part 8.5)
                    Mesh body = g.CreateBodyMesh();
                    AvenueMesh.ValidateOrLog(body, ctx);
                    var mr = ctx.AddMesh(croot, "Body", body, robe, false, true);
                    mr.sharedMaterials = new[] { robe, ink, lib != null ? lib.FacePlate : null };

                    // hem band with the doorway (no shadow maps), plus its hidden shadow-only proxy
                    Mesh hem = g.CreateHemMesh();
                    AvenueMesh.ValidateOrLog(hem, ctx);
                    ctx.AddMesh(croot, "Hem", hem, lib != null ? lib.RobeHem : null, false, true);
                    Mesh proxy = g.CreateHemProxyMesh();
                    AvenueMesh.ValidateOrLog(proxy, ctx);
                    var pr = ctx.AddMesh(croot, "HemShadowProxy", proxy, lib != null ? lib.RobeHem : null, true, false);
                    pr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    ctx.Triangles -= MeshKit.TriangleCount(proxy);   // shadow pass only: not a visible triangle

                    // ledge, bollards, alcove and door frame: human scale, cast shadows. The frame is CREAM
                    // (DoorFrame) so the 1.1 x 2 m opening reads as a lit doorway from the avenue (critique run07 A15)
                    Mesh foot = g.CreateFootMesh();
                    AvenueMesh.ValidateOrLog(foot, ctx);
                    var fr = ctx.AddMesh(croot, "Foot", foot, lib != null ? lib.Stone : null, true, true);
                    fr.sharedMaterials = new[] { lib != null ? lib.Stone : null, lib != null ? lib.Bollard : null, ink, lib != null ? lib.DoorFrame : null };

                    // flat ground shadow (shade mode of the salt shader), part of the ground group
                    Mesh shadow = g.CreateShadowMesh();
                    AvenueMesh.ValidateOrLog(shadow, ctx);
                    ctx.AddMesh(croot, "GroundShadow", shadow, lib != null ? lib.SaltShadow : null, false, false);

                    colossusTris += g.ColossusTriangles;
                    shadowTris += g.Shadow.TriangleCount;
                    var tip = g.ShadowShape.Tip;
                    ctx.Log($"Beat02_Avenue: {g.Shape.Describe()}, top {g.TopY:F2} m, {g.ColossusTriangles} tris, shadow {g.Shadow.TriangleCount} tris, tip ({tip.x:F1}, {tip.z:F1})");
                }
            }
            ctx.Log($"Beat02_Avenue: {pairs * 2} colossi, {colossusTris} triangles, ground shadows {shadowTris} triangles");
        }
    }
}
