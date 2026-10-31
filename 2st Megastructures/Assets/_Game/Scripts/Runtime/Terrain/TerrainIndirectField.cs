using UnityEngine;

namespace Megastructure
{
    /// <summary>
    /// GPU-placed terrain fields (grass tufts, flowers) on top of the shared <see cref="InstancedField"/> of the
    /// rendering foundations: one Graphics.RenderMeshIndirect command per field and frame, all placement in the vertex
    /// shader (RingGrass.shader), fixed world bounds inflated for the bend, no per-frame CPU work or allocation
    /// (MASTER_PROMPT 14.5.2; research 03 sections 2 and 6). Using InstancedField keeps the capture rule in one place:
    /// no automatic submit while <see cref="RingState.Capture"/>, and the capture harness re-submits every live field for
    /// its camera through InstancedField.SubmitAllFor right before each capture render.
    ///
    /// A field that belongs to several world sets (the meadow grass lives in NearValley AND NearPlain, 14.4) is created
    /// once per set with the same mesh, material and instance count, so the BeatDirector toggles each copy with its set.
    /// The copies are bit-identical draws (the placement depends only on _RingCam and the world cell), and NearValley /
    /// NearPlain switch in the same director tick at s 576 (back below s 570), so the sealed swap never changes the grass.
    /// </summary>
    public static class TerrainIndirectField
    {
        /// <summary>Creates one indirect field per set (index 0 is the primary copy, named <paramref name="name"/>; the
        /// others are named name.Set). Build time only.</summary>
        public static InstancedField[] Create(BuildContext ctx, string name, Mesh mesh, string matId, int instanceCount,
                                              Bounds flatBounds, float bendMaxDistance, bool castShadows, RingSet[] sets)
        {
            int n = sets != null ? sets.Length : 0;
            var fields = new InstancedField[n];
            // RenderMeshPrimitives where indirect arguments are unsupported: the shader's GetIndirectInstanceID returns
            // SV_InstanceID unchanged on D3D, so the same shader serves both modes (startInstance is always 0)
            var mode = SystemInfo.supportsIndirectArgumentsBuffer ? InstancedField.DrawMode.Indirect : InstancedField.DrawMode.Primitives;
            if (mode != InstancedField.DrawMode.Indirect)
                Debug.LogWarning("[Ring] " + name + ": indirect argument buffers unsupported, drawing with RenderMeshPrimitives");
            for (int i = 0; i < n; i++)
            {
                string fieldName = i == 0 ? name : name + "." + sets[i];
                fields[i] = InstancedField.Create(ctx, fieldName, sets[i], mesh, matId, instanceCount, flatBounds, bendMaxDistance,
                                                  castShadows, mode);
            }
            return fields;
        }
    }
}
