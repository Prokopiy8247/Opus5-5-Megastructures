## Procedural meshes and instancing (Unity 6000.6.0f1 / URP 17.6)

Researched 2026-10-29, read-only. In the citations, `XML` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml`, `PXML` = `...\UnityEngine.PhysicsModule.xml`, `URP` = `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184` and `CORE` = `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`. Where it says "DLL IL" or "DLL string", I read the 6000.6 `UnityEngine.CoreModule.dll` / `UnityEngine.PhysicsModule.dll` through a reflection-only load or a UTF-16 string scan, in memory only.

Note: `docs.unity3d.com/6000.x/...` URLs now return 301 redirects to `docs.unity.com/en-us/engine/<ver>/...`. Pages for 6000.6 exist there.

### 1. Mesh API
- **VERIFIED-SOURCE**: the job-friendly API is `Mesh.AllocateWritableMeshData(int | Mesh | Mesh[] | List<Mesh>)` → `Mesh.MeshDataArray`, then `Mesh.ApplyAndDisposeWritableMeshData(MeshDataArray, Mesh | Mesh[] | List<Mesh>, MeshUpdateFlags flags = Default)` (XML 26559-26594). **There is no `AcquireWritableMeshData`.** `AcquireReadOnlyMeshData` exists, but it is read-only.
- **VERIFIED-DOCS** ([AllocateWritableMeshData 6000.6]): the workflow is `data.SetVertexBufferParams` → `GetVertexData<T>()` → `SetIndexBufferParams(count, IndexFormat)` → `GetIndexData<T>()` → `subMeshCount` → `SetSubMesh` → Apply. The official example then calls `RecalculateNormals()` and `RecalculateBounds()`.
- **VERIFIED-SOURCE**: `MeshUpdateFlags` values are `Default=0, DontValidateIndices=1, DontResetBoneBounds=2, DontNotifyMeshUsers=4, DontRecalculateBounds=8, DontValidateLodRanges=16` (DLL reflection; XML 45693-45727).
- **VERIFIED-SOURCE**: `SetIndices(int[] / List<int> / ushort[] / NativeArray<T>, MeshTopology, int submesh, bool calculateBounds = true, int baseVertex = 0[, int meshLod])` recalculates submesh bounds by default (XML 27750-27760). Mesh LOD (`lodCount`, `RenderParams.forceMeshLod`) exists in this version.
- **VERIFIED-DOCS** ([indexFormat]): the default is 16-bit (max 65535 vertices), and the max index may not render. **Changing `indexFormat` sets `subMeshCount` to 1 and clears the index buffer**, so set `mesh.indexFormat = IndexFormat.UInt32` *before* `SetIndices`/`SetTriangles`.
- **VERIFIED-DOCS** ([SetVertexBufferParams]): "The total byte size of each vertex attribute (format x dimension) must be a multiple of 4 bytes." For example, Float16×3 is invalid. `VertexAttributeFormat` has Float32/16, UNorm/SNorm8/16, U/SInt8/16/32 (XML 52079-52138).
- **VERIFIED-DOCS** ([UploadMeshData]): modified data uploads on the next render. `UploadMeshData(true)` frees the CPU copy, and after that the script can't read the mesh.
- **UNVERIFIED (known behaviour)**:
  - A runtime `MeshCollider` and `StaticBatchingUtility` need a readable mesh, so assign colliders before calling `UploadMeshData(true)`, or never mark collider meshes unreadable.
  - `RecalculateNormals` averages across shared vertices. Brutalist hard edges need split vertices per face (24 per box) with explicit normals.
  - `RecalculateTangents` needs normals and UV0.
- **Pitfall**: if a vertex shader displaces geometry (fan blades, pistons animated in the shader), inflate `mesh.bounds` manually and set it **last**, because `SetIndices(calculateBounds:true)` overwrites it. Otherwise the geometry and its shadows get frustum-culled.

### 2. Graphics.RenderMeshInstanced / Indirect / BRG
- **VERIFIED-SOURCE** (XML 16147-16179, DLL reflection): there are three overloads:
  - `RenderMeshInstanced<T>(in RenderParams, Mesh, int submeshIndex, T[] instanceData, int instanceCount=-1, int startInstance=0) where T: unmanaged`
  - the same with `List<T>`
  - the same with `NativeArray<T>`, which takes `RenderParams` **by value**.
- **VERIFIED-DOCS** ([RenderMeshInstanced 6000.6]): T is `Matrix4x4` or a struct with fields named exactly `objectToWorld` (Matrix4x4, mandatory), `renderingLayerMask` (uint, optional) and `prevObjectToWorld` (optional), in any order. The page says "You can only render a maximum of 1023 instances at once… By default… objectToWorld and worldToObject… maximum of 511… add `#pragma instancing_options assumeuniformscaling`." Bounds are auto-computed unless `worldBounds` is set, and "Unity uses the bounds to cull and sort all the instances … as a single entity."
- **VERIFIED-SOURCE**: the managed code has no instance-count check and no splitting loop. Each overload makes one `Internal_RenderMeshInstanced` call (6000.6 DLL: no 1023/511 constants in the IL; [UnityCsReference Graphics.cs] shows `fixed` pinning). The DLL strings "Instancing is not supported." and "Material needs to enable instancing for use with RenderMeshInstanced." match the reference source.
- **UNVERIFIED**: whether native code splits calls above the limit. Treat ≤511 per call (≤1023 with assumeuniformscaling) as the hard rule. The numbers line up with a 64 KB cbuffer ÷ 128 B or 64 B per instance (CORE `ShaderLibrary/UnityInstancing.hlsl` 17-18 and 258-269: the flexible array size on D3D/Vulkan). Extra per-instance props live in their own cbuffer.
- **VERIFIED-SOURCE (6000.6 DLL IL of `RenderParams(Material)`)**:
  - `layer=0`
  - `renderingLayerMask=RenderingLayerMask.defaultRenderingLayerMask`
  - `worldBounds=new Bounds(0,0)` (auto)
  - `camera=null`
  - `motionVectorMode=Camera`
  - `reflectionProbeUsage=Off`
  - `matProps=null`
  - **`shadowCastingMode=Off`, `receiveShadows=false`, `lightProbeUsage=Off`**
  - `entityId=None`
  - `forceMeshLod=-1`

  You must set shadows explicitly.
- **VERIFIED-DOCS** ([RenderParams]): `camera == null` renders for all cameras, including the Scene view. Fields: camera, entityId, forceMeshLod, layer, lightProbeProxyVolume, lightProbeUsage, material, matProps, meshLodSelectionBias, motionVectorMode, overrideSceneCullingMask, receiveShadows, reflectionProbeUsage, rendererPriority, renderingLayerMask, sceneCullingMask, shadowCastingMode, sortingLayerID, sortingOrder, worldBounds.
- **VERIFIED-SOURCE**: `RenderParams.instanceID` is obsolete ("Please use entityId instead.") and `lightProbeProxyVolume` is obsolete `#from(6000.5)` (DLL attributes).
- **VERIFIED-SOURCE (reference source)**:
  - GC: `RenderParams` is a struct, and the T[]/NativeArray paths pin memory, so they make no per-call managed allocation. The only allocation is a one-time layout cache entry per T.
  - You must keep the arrays and `MaterialPropertyBlock` preallocated yourself.
  - The draw is for the current frame, so call it every frame from `Update`/`LateUpdate`.
- **VERIFIED-DOCS** ([RenderMeshIndirect]):
  - The args buffer is `new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, n, GraphicsBuffer.IndirectDrawIndexedArgs.size)`.
  - You must set `worldBounds` yourself.
  - Shader side: `#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs`, `#include "UnityIndirect.cginc"` and `InitIndirectDrawArgs(0)`. That file is `Editor/Data/Resources/CGIncludes/UnityIndirect.cginc` lines 45-80, and its 20-byte stride is VERIFIED-SOURCE.
  - The shader must fetch transforms from your own StructuredBuffer.
- **VERIFIED-SOURCE**: BRG/GRD needs `BatchRendererGroup.BufferTarget == RawBuffer` (CORE `Runtime/GPUDriven/GPUResidentDrawer.Validator.cs:44`) and DOTS-instancing shaders. That is overkill for this project.

### 3. Shader side (URP)
- **VERIFIED-SOURCE**: URP Lit uses `#pragma multi_compile_instancing`, `#pragma instancing_options renderinglayer` and `#include_with_pragmas ".../DOTS.hlsl"` in **every** pass (URP `Shaders/Lit.shader` 184-186, 223-224, 322-324, 368-369, 423-424). `DOTS.hlsl` adds `multi_compile _ DOTS_INSTANCING_ON` at target 4.5 (URP `ShaderLibrary/DOTS.hlsl:5-9`).
- **VERIFIED-SOURCE** (CORE `UnityInstancing.hlsl` 297-300):
  - `UNITY_INSTANCING_BUFFER_START(buf)` expands to `cbuffer UnityInstancing_buf { struct {...} arrArray[N]; }`.
  - Read props with `UNITY_ACCESS_INSTANCED_PROP(arr, var)`.
  - `UNITY_MATRIX_M` becomes `unity_Builtins0.unity_ObjectToWorldArray[unity_InstanceID]` (lines 458-482).
- **VERIFIED-SOURCE**: in **non-instanced** variants the macro becomes `CBUFFER_START(buf)` (lines 495-497). That is a second material cbuffer.
- **VERIFIED-DOCS + inference**: [SRP Batcher materials] requires all material properties in a single `UnityPerMaterial` cbuffer, built-ins in `UnityPerDraw`, and no MaterialPropertyBlocks on the renderer. So instanced props make the shader's MeshRenderer path SRP-Batcher-incompatible. Use a **dedicated instanced shader** for RenderMeshInstanced.
- **VERIFIED-SOURCE**: `UnityPerDraw` (including `unity_LODFade`) is declared by URP `ShaderLibrary/UnityInput.hlsl:108-114` ("Block Layout should be respected due to SRP Batcher"). Include URP `Core.hlsl` and never redeclare the built-ins.
- **VERIFIED-SOURCE**: `assumeuniformscaling` defines `UNITY_ASSUME_UNIFORM_SCALING`. It moves `unity_WorldToObjectArray` to a separate CB (`UnityInstancing.hlsl:317-322`), and `TransformObjectToWorldNormal` then uses M instead of the inverse-transpose (CORE `SpaceTransforms.hlsl:199-209`). **Non-uniformly scaled instances get wrong normals.**
- **VERIFIED-DOCS** ([MPB.SetVectorArray]): an array's length is fixed at the first set; longer arrays are capped. Use one MPB per chunk with a fixed size.
- **UNVERIFIED**: MPB arrays are indexed by the per-draw instance ID, not offset by `startInstance`.
- **VERIFIED-DOCS** ([GPU instancing]): instancing of *MeshRenderers* with custom URP shaders only works if the SRP Batcher is off or the shader is incompatible. RenderMeshInstanced is a direct instanced draw, so the SRP Batcher doesn't apply to it.
- **Pitfall (UNVERIFIED, classic)**: without `UNITY_VERTEX_INPUT_INSTANCE_ID` + `UNITY_SETUP_INSTANCE_ID(input)` (+ `UNITY_TRANSFER_INSTANCE_ID` if the fragment reads props) in the ShadowCaster/DepthOnly/DepthNormals passes, all instances' shadows and depth collapse onto instance 0.

### 4. GPU Resident Drawer
- **VERIFIED-SOURCE (project)**: `Assets/Settings/PC_RPAsset.asset:85 m_GPUResidentDrawerMode: 1` (= `InstancedDrawing`; enum in CORE `Runtime/GPUDriven/IGPUResidentRenderPipeline.cs:9-20`). Also `ProjectSettings/GraphicsSettings.asset:49 m_BrgStripping: 2` (= KeepAll; reflected enum `KeepIfEntitiesGraphics=0, StripAll=1, KeepAll=2`) and `PC_Renderer.asset:56 m_RenderingMode: 2` (= Forward+). **GRD is currently ON.**
- **VERIFIED-SOURCE**: URP allows GRD only with the cluster light loop, i.e. Forward+/Deferred+ (URP `Runtime/Data/UniversalRenderPipelineAsset.cs:1759-1788`). In the Editor it also requires BRG variants = Keep All (`Validator.cs:61`).
- **VERIFIED-SOURCE** — exclusion reasons (CORE `Runtime/GPUDriven/Debug/GPUResidentDrawerDebug.cs:22-45`): `LODAnimateCrossFading`, `CustomMaterialPropertyBlock`, `RenderCallback`, `ProxyVolumeProbe`, `MissingDOTSInstancing`, `TooManySubmeshes`, `GPUDrivenDisabled`, and others.
- **VERIFIED-DOCS** ([GRD], [GRD compat]):
  - It handles MeshRenderers only, with DOTS-instancing shaders, static GI only, no MPB and no `OnRenderObject`.
  - An object must not "move position after one camera finishes rendering and before another camera starts".
  - Per-frame changes are tracked.
  - Exclude an object with the *Disallow GPU Driven Rendering* component.
- **Recommendation**: set GRD to **Disabled** for ~300 renderers. Handwritten shaders without `DOTS_INSTANCING_ON` are excluded anyway; leaving it on mixes two render paths, makes builds longer, and adds no benefit at this scale.

### 5. Batching and LOD
- **VERIFIED-SOURCE**: `m_UseSRPBatcher: 1` and `m_SupportsDynamicBatching: 0` (PC_RPAsset 72-73).
- **VERIFIED-DOCS** ([StaticBatchingUtility.Combine]): it copies mesh data into one internal mesh (extra memory), and child transforms are locked afterwards.
- **UNVERIFIED estimate**: 300-500 SRP-batched MeshRenderers are cheap on a desktop CPU (well under 1 ms). Don't static-batch runtime-generated geometry; merge per module+material at generation time instead (UInt32 indices).
- **VERIFIED-SOURCE**: `LODFadeMode {None=0, CrossFade=1, SpeedTree=2}`. `LODGroup.fadeMode`, `animateCrossFading` and `static float crossFadeAnimationDuration` exist; `LOD.fadeTransitionWidth` is "only used if it's not animated" (XML 22988-23104; reflection).
- **VERIFIED-SOURCE**:
  - URP LOD crossfade is on: `m_EnableLODCrossFade: 1` and BlueNoise dither (PC_RPAsset 33-34).
  - If you turn it off, `LOD_FADE_CROSSFADE` is stripped from builds (URP `UniversalRenderPipelineAsset.cs:471-474`).
  - Shader: add `#pragma multi_compile _ LOD_FADE_CROSSFADE` to every pass, and under `#if defined(LOD_FADE_CROSSFADE)` include `Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl` and call `LODFadeCrossFade(input.positionCS)` in the fragment (URP `ShaderLibrary/LODCrossFade.hlsl:15-24`; `Shaders/ShadowCasterPass.hlsl:6-8,71-73`).
- **Pitfall**: dithered fades are visible in captures as noise, and `animateCrossFading` excludes the object from GRD.

### 6. Physics
- **VERIFIED-SOURCE**: in 6000.6, `Physics.autoSyncTransforms` **is deprecated** (DLL string: "has been deprecated please use Physics.SyncTransforms instead…").
- **VERIFIED-DOCS** ([SyncTransforms 6000.6]): transforms sync automatically before each physics step. Call `SyncTransforms()` after Transform changes in Update/LateUpdate before queries, and don't overuse it.
- **VERIFIED-SOURCE**: `CollisionFlags Move(Vector3 motion)`, `SimpleMove`, `stepOffset`, `slopeLimit`, `skinWidth`, `minMoveDistance`, `enableOverlapRecovery` (PXML 1077-1153).
- **VERIFIED-DOCS** ([CC manual], [CC.Move]):
  - Move does not apply gravity.
  - Use skinWidth ≈ 10% of radius (>0.01).
  - Use stepOffset 0.1-0.4 for a 2 m character; it must not exceed the height.
  - Keep minMoveDistance at 0.
  - Changing properties recreates the controller.
- **VERIFIED-DOCS (pre-6 manual, [MeshCollider 2023.1])**: convex colliders max 255 triangles; non-default cooking needs a readable mesh.
- **VERIFIED-SOURCE**: `Physics.BakeMesh(int, …)` is obsolete; use `BakeMesh(EntityId, bool[, MeshColliderCookingOptions])` (DLL string, PXML 3303-3332). `Object.GetInstanceID()` is obsolete; use `GetEntityId()` (DLL string).
- **UNVERIFIED (community, [CC ignores transform.position])**: the CC keeps an internal position. Direct `transform.position` writes get overwritten unless you disable/enable the controller or call `Physics.SyncTransforms()`.
- **Stairs (design)**:
  - Give visual steps no collider and use one invisible rotated BoxCollider ramp.
  - Keep the ramp at ≤35° with `slopeLimit` 45-50°, plus `stepOffset` 0.3 for the lips.
  - When grounded and descending, add a downward snap of at least `speed*tan(angle)*dt` (or a constant −stepOffset) so `isGrounded` doesn't flicker or bounce.

### 7. Moving platforms and precision
- **VERIFIED-SOURCE**: `[DefaultExecutionOrder(int order)]` exists (XML 8553-8568).
- **Why parenting jitters (UNVERIFIED, community [platform thread])**: the CC's internal PhysX position isn't moved by the parent. The next `Move` writes the stale position back and the player snaps or falls, and FixedUpdate/Update rate mismatch adds aliasing.
- **Recommended order**:
  1. Platform script at `[DefaultExecutionOrder(-100)]`: in Update, move the kinematic platforms from an analytic time curve, then call `Physics.SyncTransforms()` once.
  2. Player at order 0: one `Move(carry + input + gravity)` per frame.
  3. Camera in LateUpdate.
- **For the lift ride (most robust)**: use local coordinates.
  - Store the player's position in lift space, set `cc.enabled=false`, write `transform.position = lift.TransformPoint(local)` every frame, and clamp input to the lift's floor rectangle.
  - On arrival, re-enable the CC and call `Physics.SyncTransforms()`.
- **VERIFIED (computed)**: the float32 ULP at 487 m is **2^-15 = 3.05e-5 m** (the whole 256-512 range; 512-1024 is 2^-14). That is not a jitter source.
- **Pitfall**: high-frequency shader noise or hashes on world position degrade. At 50 000 the ULP is 2^-8 (0.004), so wrap coordinates (`frac(posWS/period)`) or use object space.

### Recommendations
1. Large meshes: `AllocateWritableMeshData` (one call for many meshes), explicit layout of Float32×3 position, Float32×3 or SNorm16×4 normal, and UInt32 indices. Set indexFormat before indices. Set bounds last. Use `RecalculateBounds()`, or explicit inflated bounds for shader-animated parts.
2. Honeycomb and repeated parts: RenderMeshInstanced in spatial chunks of ≤511 instances. Pass precomputed `worldBounds` per chunk and set `shadowCastingMode=On, receiveShadows=true` explicitly. Use preallocated `Matrix4x4[]` and a `RenderParams` built once. Set `material.enableInstancing = true` in code.
3. Use a dedicated instanced shader with `multi_compile_instancing` and instance-ID setup in **all** passes. Derive per-instance variation from a hash of `UNITY_MATRIX_M._m03_m13_m23` instead of MPB arrays. Never use assumeuniformscaling with non-uniform scale.
4. Keep every other handwritten shader SRP-Batcher compatible: one identical `CBUFFER_START(UnityPerMaterial)` in every pass, URP Core.hlsl, and no MPB on MeshRenderers. Check with the Inspector's "SRP Batcher: compatible".
5. Set `m_GPUResidentDrawerMode` to 0 (Disabled) in PC_RPAsset (and Mobile_RPAsset).
6. Avoid LODGroups unless profiling demands them. If you use them, add the crossfade keyword and call to all passes.
7. Use BoxColliders and ramp colliders over MeshColliders. Only bake MeshColliders from readable meshes, with `BakeMesh(EntityId, …)` at load.
8. Run platforms in Update with execution order, then call `Physics.SyncTransforms()`, then one CC.Move. Use local-space kinematic riding for the lift. Don't parent the CC.
9. Avoid obsolete APIs so builds stay warning-free: GetInstanceID, BakeMesh(int), RenderParams.instanceID/lightProbeProxyVolume, `Physics.autoSyncTransforms`.

### Web sources (all accessed 2026-10-29)
- [RenderMeshInstanced 6000.6] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/rendermeshinstanced — instance limits, struct fields, culling, exceptions.
- [RenderParams] https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderparams — field list; camera null means all cameras.
- [UnityCsReference Graphics.cs] https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/Graphics/Graphics.cs — managed implementation (no splitting, pinning). Cross-checked against the 6000.6 DLL IL.
- [RenderMeshIndirect] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/graphics/rendermeshindirect — args buffer, shader macros, bounds.
- [GRD] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer — requirements.
- [GRD compat] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/make-object-compatible-gpu-rendering — compatibility conditions.
- [SRP Batcher materials] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials — compatibility rules.
- [GPU instancing] https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing — SRP Batcher vs instancing for MeshRenderers.
- [MPB.SetVectorArray] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/materialpropertyblock/setvectorarray — fixed array length.
- [AllocateWritableMeshData] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/allocatewritablemeshdata — MeshData workflow.
- [indexFormat] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/indexformat — 16/32-bit rules.
- [UploadMeshData] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/uploadmeshdata — upload and readability.
- [SetVertexBufferParams] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/mesh/setvertexbufferparams — 4-byte attribute rule.
- [StaticBatchingUtility.Combine] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/staticbatchingutility/combine — copy and lock semantics.
- [CC manual] https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/character-control-section/class-character-controller — property guidance.
- [CC.Move] https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/charactercontroller/move — Move semantics.
- [SyncTransforms 6000.6] https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/physics/synctransforms — when to sync.
- [MeshCollider 2023.1] https://docs.unity3d.com/2023.1/Documentation/Manual/class-MeshCollider.html — convex 255 triangles, readable requirement. Older version, so UNVERIFIED for 6000.6.
- [CC ignores transform.position] https://discussions.unity.com/t/character-controller-ignores-transform-position/729515 — community teleport behaviour.
- [platform thread] https://discussions.unity.com/t/moving-platform-and-charactercontroller-move-not-working-the-player-falls/223755 — community platform-delta approach.
