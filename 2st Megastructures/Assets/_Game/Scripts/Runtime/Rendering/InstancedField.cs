using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Megastructure
{
    /// <summary>
    /// One procedural instanced draw (grass tufts, flowers, swarm bands, bollard rows ...): wraps
    /// Graphics.RenderMeshPrimitives (CPU-known count) or Graphics.RenderMeshIndirect (args in an IndirectArguments
    /// GraphicsBuffer, GraphicsBuffer.IndirectDrawIndexedArgs, one command) with explicit, bend-inflated world bounds.
    /// Created at load (inside IBeatBuilder.Build); nothing is allocated afterwards. The BeatDirector toggles
    /// <see cref="Visible"/> through <see cref="IRingDrawable"/>.
    ///
    /// Submission: every frame from <see cref="InstancedFieldDriver"/> (LateUpdate, last in the frame) for all
    /// cameras (RenderParams.camera = null), EXCEPT in capture mode (<see cref="RingState.Capture"/>): then nothing is
    /// submitted automatically and the CaptureHarness calls <see cref="SubmitAllFor"/>(cam) right before each manual
    /// render (rp.camera = cam), so queued draws never accumulate for a camera that does not render that frame
    /// (docs/research/03 section 3). Today RingCaptureHooks.InvokeBefore finds SubmitAllFor by reflection only while
    /// RingCaptureHooks.BeforeRender has no subscriber; once anything subscribes, BeforeRender must include
    /// InstancedField.SubmitAllFor (lead wiring), or the fields vanish from captures.
    ///
    /// SHADER-SIDE CONTRACT for RING_INDIRECT draws (RingLitCore.hlsl / RingLitPass.hlsl):
    /// - In the shader's HLSLINCLUDE (so every pass sees it): #define RING_INDIRECT and #define RING_HAS_VERTEX_HOOK,
    ///   then void RingVertexHook(inout RingVertexData v). With RING_INDIRECT the attributes carry SV_InstanceID into
    ///   v.instanceID and v.positionWS / v.normalWS / v.tangentWS hold the mesh's OBJECT-space values: the hook must
    ///   write the flat world position (and normal, tangent) itself. unity_ObjectToWorld is not applied and must not
    ///   be used (for procedural draws it is undefined or derived from the bounds). RingBend, shadows and depth then
    ///   run identically in all four passes, so they match bit for bit.
    /// - Per-instance data: declare it OUTSIDE UnityPerMaterial, e.g. StructuredBuffer&lt;float4&gt; _RingInstances;
    ///   bound through <see cref="SetInstanceData{T}"/> or <see cref="SetBuffer"/> on this field's MaterialPropertyBlock.
    /// - Primitives mode: SV_InstanceID is 0..InstanceCount-1 directly; no extra include.
    /// - Indirect mode: also in the HLSLINCLUDE, #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs and
    ///   #include "UnityIndirect.cginc" (Editor/Data/Resources/CGIncludes, resolved from HLSLPROGRAM, plain HLSL), and
    ///   call InitIndirectDrawArgs(0); at the top of the vertex hook in EVERY pass, then index the buffer with
    ///   GetIndirectInstanceID(v.instanceID) (0-based within the command on every API; D3D's SV_InstanceID excludes
    ///   startInstance, Vulkan's includes it).
    /// - No #pragma multi_compile_instancing / instancing_options (INSTANCING_ON would read unfilled per-instance
    ///   matrices). The material does not need enableInstancing.
    /// - Opaque fields with castShadows need the ShadowCaster pass (RingLitPass provides all four passes).
    /// </summary>
    public sealed class InstancedField : IRingDrawable
    {
        public enum DrawMode { Primitives = 0, Indirect = 1 }

        public const string DefaultBufferName = "_RingInstances";

        static readonly List<InstancedField> s_All = new List<InstancedField>();

        /// <summary>Number of live (not released) fields.</summary>
        public static int Count => s_All.Count;
        public static InstancedField At(int index) => s_All[index];

        public readonly string Name;
        public readonly DrawMode Mode;
        public RingSet Set { get; }
        public bool Visible { get; set; } = true;
        public Mesh Mesh { get; private set; }
        public Material Material { get; private set; }
        public int SubMesh { get; private set; }
        /// <summary>CPU instance count (Primitives) or the count written into the args (Indirect; capacity when the
        /// args are GPU-written).</summary>
        public int InstanceCount { get; private set; }
        public Bounds WorldBounds => _rp.worldBounds;
        public bool CastShadows => _rp.shadowCastingMode != ShadowCastingMode.Off;
        public MaterialPropertyBlock Properties => _mpb;
        public GraphicsBuffer ArgsBuffer => _args;
        public GraphicsBuffer InstanceBuffer => _instances;
        public bool IsReleased => _released;
        /// <summary>The GameObject that owns this field's lifetime (null when created without a BuildContext).</summary>
        public GameObject Holder { get; private set; }
        /// <summary>Triangles drawn per submission (QA budgets, Part 7.4).</summary>
        public long TriangleCount => Mesh != null ? (long)(Mesh.GetIndexCount(SubMesh) / 3) * InstanceCount : 0;

        RenderParams _rp;
        readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        GraphicsBuffer _args;
        GraphicsBuffer _instances;
        bool _released;

        /// <param name="flatBounds">bounds of all instances in flat (unbent) world space</param>
        /// <param name="bendMaxDistance">largest camera distance from which the field is visible (RingBend.InflateBounds)</param>
        /// <param name="gpuWritableArgs">Indirect only: the args buffer is also Raw so a compute shader can write it</param>
        public InstancedField(string name, RingSet set, Mesh mesh, Material material, int instanceCount, Bounds flatBounds,
            float bendMaxDistance, bool castShadows, DrawMode mode = DrawMode.Primitives, bool receiveShadows = true,
            int layer = 0, int subMesh = 0, bool gpuWritableArgs = false)
        {
            Name = string.IsNullOrEmpty(name) ? "InstancedField" : name;
            Set = set;
            Mode = mode;
            Mesh = mesh;
            Material = material;
            SubMesh = mesh != null ? Mathf.Clamp(subMesh, 0, Mathf.Max(0, mesh.subMeshCount - 1)) : 0;
            InstanceCount = Mathf.Max(0, instanceCount);
            if (mesh == null) Debug.LogError("[Ring] InstancedField '" + Name + "': mesh is null");
            if (material == null) Debug.LogError("[Ring] InstancedField '" + Name + "': material is null");

            _rp = new RenderParams(material)   // never default(RenderParams): renderingLayerMask would be 0
            {
                worldBounds = RingBend.InflateBounds(flatBounds, bendMaxDistance),
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = receiveShadows,
                layer = layer,
                matProps = _mpb,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };

            if (mode == DrawMode.Indirect)
            {
                var target = GraphicsBuffer.Target.IndirectArguments;
                if (gpuWritableArgs) target |= GraphicsBuffer.Target.Raw;
                _args = new GraphicsBuffer(target, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                WriteArgs();
            }

            s_All.Add(this);
            InstancedFieldDriver.Ensure();
        }

        /// <summary>
        /// Creates a field from a material id, adds it to ctx.Drawables (set toggling) and gives it a holder GameObject
        /// under the set root whose destruction releases the GPU buffers. Register the holder for tests if needed.
        /// </summary>
        public static InstancedField Create(BuildContext ctx, string name, RingSet set, Mesh mesh, string matId, int instanceCount,
            Bounds flatBounds, float bendMaxDistance, bool castShadows, DrawMode mode = DrawMode.Primitives,
            bool receiveShadows = true, int layer = 0, int subMesh = 0, bool gpuWritableArgs = false)
        {
            Material mat = ctx != null && ctx.Assets != null ? ctx.Assets.Get(matId) : null;
            var f = new InstancedField(name, set, mesh, mat, instanceCount, flatBounds, bendMaxDistance, castShadows, mode,
                receiveShadows, layer, subMesh, gpuWritableArgs);
            if (ctx != null)
            {
                ctx.Drawables.Add(f);
                var go = new GameObject(f.Name);
                go.transform.SetParent(ctx.SetRoot(set), false);
                var handle = go.AddComponent<InstancedFieldHandle>();
                handle.Field = f;
                f.Holder = go;
            }
            return f;
        }

        void WriteArgs()
        {
            if (_args == null) return;
            _argsData[0].indexCountPerInstance = Mesh != null ? Mesh.GetIndexCount(SubMesh) : 0u;
            _argsData[0].instanceCount = (uint)InstanceCount;
            _argsData[0].startIndex = Mesh != null ? Mesh.GetIndexStart(SubMesh) : 0u;
            _argsData[0].baseVertexIndex = Mesh != null ? Mesh.GetBaseVertex(SubMesh) : 0u;
            _argsData[0].startInstance = 0u;
            _args.SetData(_argsData);
        }

        /// <summary>Changes the instance count (Primitives: next submission; Indirect: rewrites the args). Load time
        /// or rare events only (Indirect uploads 20 bytes).</summary>
        public void SetInstanceCount(int count)
        {
            count = Mathf.Max(0, count);
            if (count == InstanceCount) return;
            InstanceCount = count;
            if (Mode == DrawMode.Indirect) WriteArgs();
        }

        /// <summary>Moves the culling bounds (e.g. a camera-following grass patch). Allocation-free.</summary>
        public void SetWorldBounds(Bounds flatBounds, float bendMaxDistance)
        {
            _rp.worldBounds = RingBend.InflateBounds(flatBounds, bendMaxDistance);
        }

        /// <summary>Uploads per-instance data into a field-owned StructuredBuffer bound as <paramref name="shaderName"/>.
        /// Load time only. The struct must be blittable with a size that is a multiple of 4 bytes.</summary>
        public void SetInstanceData<T>(T[] data, string shaderName = DefaultBufferName) where T : struct
        {
            if (_released || data == null || data.Length == 0) return;
            int stride = UnsafeUtility.SizeOf<T>();
            if (stride <= 0 || (stride & 3) != 0)
            {
                Debug.LogError("[Ring] InstancedField '" + Name + "': instance stride " + stride + " is not a multiple of 4");
                return;
            }
            if (_instances != null && (_instances.count != data.Length || _instances.stride != stride))
            {
                _instances.Release();
                _instances = null;
            }
            if (_instances == null) _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, data.Length, stride);
            _instances.SetData(data);
            _mpb.SetBuffer(shaderName, _instances);
        }

        /// <summary>Binds an externally owned buffer (e.g. written by a compute shader). The caller releases it.</summary>
        public void SetBuffer(string shaderName, GraphicsBuffer buffer) { if (buffer != null) _mpb.SetBuffer(shaderName, buffer); }
        public void SetFloat(string shaderName, float value) => _mpb.SetFloat(shaderName, value);
        public void SetVector(string shaderName, Vector4 value) => _mpb.SetVector(shaderName, value);
        public void SetInt(string shaderName, int value) => _mpb.SetInteger(shaderName, value);

        /// <summary>Queues this field for <paramref name="cam"/> (null = every camera that renders this frame).
        /// Allocation-free.</summary>
        public void Submit(Camera cam)
        {
            if (!Visible || _released || Mesh == null || Material == null || InstanceCount <= 0) return;
            _rp.camera = cam;
            if (Mode == DrawMode.Indirect)
            {
                if (_args != null) Graphics.RenderMeshIndirect(in _rp, Mesh, _args, 1, 0);
            }
            else
            {
                Graphics.RenderMeshPrimitives(in _rp, Mesh, SubMesh, InstanceCount);
            }
        }

        /// <summary>Every live field for every camera (the driver, outside capture mode).</summary>
        public static void SubmitAll()
        {
            for (int i = 0; i < s_All.Count; i++) s_All[i].Submit(null);
        }

        /// <summary>Every live field for one camera; the CaptureHarness calls this before each manual render.</summary>
        public static void SubmitAllFor(Camera cam)
        {
            for (int i = 0; i < s_All.Count; i++) s_All[i].Submit(cam);
        }

        /// <summary>Releases the GPU buffers and removes the field from the registry (idempotent).</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            if (_args != null) { _args.Release(); _args = null; }
            if (_instances != null) { _instances.Release(); _instances = null; }
            s_All.Remove(this);
        }

        public static void ReleaseAll()
        {
            while (s_All.Count > 0) s_All[s_All.Count - 1].Release();
        }

        // Enter-play-mode without domain reload keeps statics: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ReleaseAll();
    }
}
