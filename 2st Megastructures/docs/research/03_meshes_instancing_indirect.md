# 03 - Procedural meshes, GPU instancing, indirect draws (Unity 6000.6.0f1 / URP 17.6.0)

Research notes, read-only research pass, 2026-10-29.
Tags: VERIFIED-SOURCE = read in package source / Unity install files / assembly metadata (path:line);
VERIFIED-DOCS = official docs (local offline docs of THIS editor build, or URL); UNVERIFIED = inference / community / memory.

Ground-truth sources used:
- `UNITY` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data`
- `CGINC` = `UNITY\Resources\CGIncludes` (NOTE: CGIncludes live under `Data\Resources\CGIncludes`, not `Data\CGIncludes`)
- `DOCS` = `UNITY\Documentation\en\` (offline docs for 6000.6, "Built on 2026-08-25") - ScriptReference + Manual (+ `Manual\urp\`)
- `ASM` = `UNITY\Managed\UnityEngine\UnityEngine.CoreModule.dll` (+ `.xml`), inspected by reflection with a throwaway .NET 8 tool in the scratchpad (signatures, enum values, field order, constructor IL). Not decompiled beyond IL opcodes.
- `URP` = `Library\PackageCache\com.unity.render-pipelines.universal@8457e85b8184`
- `CORE` = `Library\PackageCache\com.unity.render-pipelines.core@2d66c71e606e`

---

## 0. TL;DR - decisions for The Ring

1. Build big meshes with `mesh.indexFormat = IndexFormat.UInt32` set FIRST, then `SetVertices/SetNormals/SetTangents/SetUVs/SetColors`, then `SetIndices(..., MeshTopology.Triangles, 0, calculateBounds:false)`, then `RecalculateBounds()` (or set `mesh.bounds`) LAST. For vertex-bent (ring-curvature) shaders override culling bounds with `renderer.localBounds = ...` at runtime.
2. Cook `MeshCollider` BEFORE `UploadMeshData(true)`; better: never mark collider meshes non-readable (Player needs readable meshes for runtime cooking; Editor hides the problem).
3. For scatter (rocks, pylons, swarm panels): prefer `Graphics.RenderMeshPrimitives` (CPU-known count) or `Graphics.RenderMeshIndirect` (GPU args) with a `StructuredBuffer` of per-instance data and a hand-written URP shader that does NOT use `UNITY_MATRIX_M` and declares NO instancing pragmas. Always `new RenderParams(material)` (never `default`), set `worldBounds`, `shadowCastingMode = On`, `receiveShadows = true`.
4. The shader must have ShadowCaster + DepthOnly + DepthNormals passes (project PC renderer = Forward+ with an SSAO feature), all computing the vertex position through ONE shared function.
5. GPU Resident Drawer is currently ENABLED in `Assets/Settings/PC_RPAsset.asset:85` (`m_GPUResidentDrawerMode: 1`). Turn it OFF (`0`) for this project (reasons in section 4).
6. `Object.GetInstanceID()` and `Physics.BakeMesh(int, ...)` are `[Obsolete(error:true)]` in 6000.6 -> compile errors. Use `GetEntityId()` / `Physics.BakeMesh(EntityId, ...)`.

---

## 1. Mesh API (procedural meshes)

### 1.1 Index format / topology
- `Mesh.indexFormat` (`UnityEngine.Rendering.IndexFormat.UInt16 | UInt32`). Default 16-bit. "Changing indexFormat sets subMeshCount to one and clears the index buffer." -> set it BEFORE `SetIndices/SetTriangles`. 32-bit: "up to 4 billion vertices". VERIFIED-DOCS (`DOCS\ScriptReference\Mesh-indexFormat.html`).
- Max index value may be unusable on some APIs (e.g. 0xFFFF for 16-bit = primitive restart). VERIFIED-DOCS (same page). Switch to UInt32 when `vertexCount > 65534`.
- `MeshTopology` values: `Triangles=0, Quads=2, Lines=3, Points=5` (+ LineStrip=4). VERIFIED-SOURCE (ASM reflection; LineStrip from memory = UNVERIFIED).

### 1.2 Simple API signatures (6000.6, VERIFIED-SOURCE via ASM reflection)
```csharp
void SetVertices(Vector3[] / List<Vector3> / NativeArray<T> inVertices [, int start, int length [, MeshUpdateFlags flags]]);
void SetNormals (Vector3[] / List<Vector3> / NativeArray<T> ... same overload pattern);
void SetTangents(Vector4[] / List<Vector4> / NativeArray<T> ...);
void SetUVs(int channel, Vector2[]|Vector3[]|Vector4[]|List<..>|NativeArray<T> uvs [, int start, int length [, MeshUpdateFlags flags]]);
void SetColors(Color[]|Color32[]|List<..>|NativeArray<T> ...);
void SetIndices(int[]|ushort[]|List<int>|List<ushort>|NativeArray<T> indices, MeshTopology topology, int submesh,
                bool calculateBounds = true, int baseVertex = 0);
void SetIndices(<same>, int indicesStart, int indicesLength, MeshTopology topology, int submesh, bool calculateBounds = true, int baseVertex = 0);
// 6000.x adds Mesh LOD overloads with an extra `int meshLod` after `submesh` - don't pass an int where a bool is meant!
void RecalculateBounds(MeshUpdateFlags flags = Default);
void RecalculateNormals(MeshUpdateFlags flags = Default);
void RecalculateTangents(MeshUpdateFlags flags = Default);
void UploadMeshData(bool markNoLongerReadable);
void MarkDynamic();
void Clear(bool keepVertexLayout = true);
void SetSubMesh(int index, SubMeshDescriptor desc, MeshUpdateFlags flags = Default);
void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes);   // also NativeArray overload
void SetIndexBufferParams(int indexCount, IndexFormat format);
void SetVertexBufferData<T>(T[]|List<T>|NativeArray<T> data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = Default);
void SetIndexBufferData<T>(T[]|List<T>|NativeArray<T> data, int dataStart, int meshBufferStart, int count, MeshUpdateFlags flags = Default);
Bounds bounds {get;set}   int subMeshCount {get;set}   bool isReadable {get;set}
GraphicsBuffer.Target vertexBufferTarget / indexBufferTarget {get;set}  // add Raw/Structured to read mesh buffers in compute
GraphicsBuffer GetVertexBuffer(int stream); GraphicsBuffer GetIndexBuffer();
```
- PITFALL: the `SetIndices(int[], MeshTopology, int submesh, int meshLod, bool calculateBounds = true, int baseVertex = 0)` overload exists alongside `(..., int submesh, bool calculateBounds, int baseVertex)`. Writing `SetIndices(idx, MeshTopology.Triangles, 0, 0)` binds to the meshLod overload. Use named args: `SetIndices(idx, MeshTopology.Triangles, 0, calculateBounds: false)`. VERIFIED-SOURCE (ASM reflection).
- `Mesh.uv1` is `[Obsolete(error)]` -> use `uv2`/`SetUVs(1, ...)`. VERIFIED-SOURCE (ASM).
- `SetVertices` "sets the new vertex count"; other per-vertex arrays must then match that count. VERIFIED-DOCS (`DOCS\ScriptReference\Mesh.SetVertices.html`). Order: vertices first, then attributes, then indices (index validation checks against vertex count unless `MeshUpdateFlags.DontValidateIndices`).
- `RecalculateBounds` works only for `VertexAttributeFormat.Float32` positions; otherwise assign `bounds` manually. "Assigning triangles automatically recalculates the bounding volume." VERIFIED-DOCS (`Mesh.RecalculateBounds.html`).
- Bounds override rule: any later call that recalculates bounds (`SetIndices(calculateBounds:true)`, `SetTriangles`, `SetSubMesh` without `DontRecalculateBounds`, `RecalculateBounds`) overwrites a custom `mesh.bounds`. Set `mesh.bounds` as the LAST step. (VERIFIED-DOCS for SetIndices/SetSubMesh flags; ordering conclusion UNVERIFIED but follows.)
- Better for vertex-deformed shaders: `Renderer.localBounds` (local-space override; "mostly useful when the renderer uses a shader that does custom vertex deformations"; not saved in scenes, set at runtime; `ResetLocalBounds()` removes it) or `Renderer.bounds` (world-space override, stops tracking transform). VERIFIED-DOCS (`Renderer-localBounds.html`, `Renderer-bounds.html`); properties exist VERIFIED-SOURCE (ASM).
- `MeshUpdateFlags`: `Default=0, DontValidateIndices=1, DontResetBoneBounds=2, DontNotifyMeshUsers=4, DontRecalculateBounds=8, DontValidateLodRanges=16`. VERIFIED-SOURCE (ASM).
- `UploadMeshData(true)`: "makes Mesh data not be readable from the script anymore, and frees up system memory copy". VERIFIED-DOCS (`Mesh.UploadMeshData.html`). After that: no `vertices`, no `RecalculateBounds`, no runtime collider cooking.
- `MarkDynamic()` before first upload for meshes rewritten often. VERIFIED-DOCS (`Mesh.MarkDynamic.html`).

### 1.3 MeshData (job-friendly) API - NOTE THE NAME
- There is NO `Mesh.AcquireWritableMeshData`. The writable API is **`Mesh.AllocateWritableMeshData`**; `AcquireReadOnlyMeshData` is the read-only one. VERIFIED-SOURCE (ASM reflection).
```csharp
static Mesh.MeshDataArray Mesh.AllocateWritableMeshData(int meshCount);
static Mesh.MeshDataArray Mesh.AllocateWritableMeshData(Mesh mesh);          // copy of existing mesh
static Mesh.MeshDataArray Mesh.AllocateWritableMeshData(Mesh[] meshes);  // + List<Mesh>
static void Mesh.ApplyAndDisposeWritableMeshData(Mesh.MeshDataArray data, Mesh mesh, MeshUpdateFlags flags = MeshUpdateFlags.Default);
static void Mesh.ApplyAndDisposeWritableMeshData(Mesh.MeshDataArray data, Mesh[] meshes, MeshUpdateFlags flags = Default); // + List<Mesh>
static Mesh.MeshDataArray Mesh.AcquireReadOnlyMeshData(Mesh mesh); // + Mesh[] / List<Mesh>; must Dispose()
// Mesh.MeshData members:
void SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] attributes); // + NativeArray overload
void SetIndexBufferParams(int indexCount, IndexFormat format);
NativeArray<T> GetVertexData<T>(int stream = 0);  NativeArray<T> GetIndexData<T>();
int subMeshCount {get;set}  void SetSubMesh(int index, SubMeshDescriptor desc, MeshUpdateFlags flags = Default);
// SubMeshDescriptor(int indexStart, int indexCount, MeshTopology topology = Triangles); props bounds, baseVertex, firstVertex, vertexCount
```
VERIFIED-SOURCE (ASM). Docs: "Unity performs very little data validation" in ApplyAndDispose; sub-mesh ranges must not overlap. The official example calls `mesh.RecalculateNormals(); mesh.RecalculateBounds();` after apply. VERIFIED-DOCS (`Mesh.AllocateWritableMeshData.html`, `Mesh.ApplyAndDisposeWritableMeshData.html`).

Skeleton:
```csharp
var arr = Mesh.AllocateWritableMeshData(1); var md = arr[0];
md.SetVertexBufferParams(vCount,
    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
    new VertexAttributeDescriptor(VertexAttribute.Normal,   VertexAttributeFormat.Float32, 3, 0),
    new VertexAttributeDescriptor(VertexAttribute.Tangent,  VertexAttributeFormat.Float32, 4, 0),
    new VertexAttributeDescriptor(VertexAttribute.Color,    VertexAttributeFormat.UNorm8,  4, 0),
    new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2, 0));
// struct Vtx { float3 pos; float3 nrm; float4 tan; Color32 col; float2 uv; }  -> must match order/size exactly (stream 0 interleaved)
var verts = md.GetVertexData<Vtx>();               // fill (Burst job OK)
md.SetIndexBufferParams(iCount, IndexFormat.UInt32);
var idx = md.GetIndexData<uint>();                 // fill
md.subMeshCount = 1;
md.SetSubMesh(0, new SubMeshDescriptor(0, iCount, MeshTopology.Triangles) { bounds = b, vertexCount = vCount },
              MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
var mesh = new Mesh { name = "RingSegment" };
Mesh.ApplyAndDisposeWritableMeshData(arr, mesh, MeshUpdateFlags.DontRecalculateBounds);
mesh.bounds = b;   // explicit, last
```

### 1.4 MeshCollider from a generated mesh
- `MeshCollider { Mesh sharedMesh; bool convex; MeshColliderCookingOptions cookingOptions; }` VERIFIED-SOURCE (ASM).
- `MeshColliderCookingOptions`: `None=0, InflateConvexMesh=1, CookForFasterSimulation=2, EnableMeshCleaning=4, WeldColocatedVertices=8, UseFastMidphase=16`. Inspector default = "Everything" (all on). VERIFIED-SOURCE (ASM) + VERIFIED-DOCS (`DOCS\Manual\class-MeshCollider.html`).
- "If prior to setting sharedMesh any of the vertices, indices or triangles of the mesh have been changed then the shapes of the MeshCollider will be rebuilt." -> assign `sharedMesh` after the mesh is complete; to re-cook after edits set `sharedMesh = null; sharedMesh = mesh;`. VERIFIED-DOCS (`MeshCollider-sharedMesh.html`).
- Non-default cooking options require `isReadable == true`; "You must enable the Read/Write property of the mesh to use it in the Mesh collider". VERIFIED-DOCS (class-MeshCollider.html).
- `Physics.BakeMesh(EntityId meshEntityId, bool convex [, MeshColliderCookingOptions])` (thread-safe pre-bake, e.g. from a job). The `int meshID` overloads are `[Obsolete(error:true)]`. In the Player BakeMesh needs Read/Write; in the Editor geometry is always available (Editor hides the bug!). MeshCollider reuses the bake only if cookingOptions are identical and the transform has no negative/skewed scale (negative ok if not convex). VERIFIED-SOURCE (ASM Obsolete attrs) + VERIFIED-DOCS (`Physics.BakeMesh.html`).
- `Object.GetInstanceID()` -> `[Obsolete("Use GetEntityId instead.", true)]` (compile ERROR). Use `mesh.GetEntityId()`. VERIFIED-SOURCE (ASM).
- 32-bit-index meshes as MeshCollider: supported by PhysX. UNVERIFIED (not checked in this version).

```csharp
var opts = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning
         | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
Physics.BakeMesh(mesh.GetEntityId(), false, opts);   // optional pre-bake
var mc = go.AddComponent<MeshCollider>(); mc.cookingOptions = opts; mc.sharedMesh = mesh;
// only now, and only for render-only meshes: mesh.UploadMeshData(true);
```

---

## 2. GPU instancing / indirect draws

### 2.1 Signatures (VERIFIED-SOURCE via ASM reflection; the parameter is `in`, offline docs render it as `ref`)
```csharp
static void Graphics.RenderMesh(in RenderParams rparams, Mesh mesh, int submeshIndex, Matrix4x4 objectToWorld, Matrix4x4? prevObjectToWorld = null);
static void Graphics.RenderMeshInstanced<T>(in RenderParams rparams, Mesh mesh, int submeshIndex, T[] instanceData, int instanceCount = -1, int startInstance = 0);
static void Graphics.RenderMeshInstanced<T>(in RenderParams rparams, Mesh mesh, int submeshIndex, List<T> instanceData, int instanceCount = -1, int startInstance = 0);
static void Graphics.RenderMeshInstanced<T>(RenderParams rparams, Mesh mesh, int submeshIndex, NativeArray<T> instanceData, int instanceCount = -1, int startInstance = 0); // by value
static void Graphics.RenderMeshIndirect(in RenderParams rparams, Mesh mesh, GraphicsBuffer argsBuffer, int commandCount = 1, int startCommand = 0);
static void Graphics.RenderMeshPrimitives(in RenderParams rparams, Mesh mesh, int submeshIndex, int instanceCount = 1);
static void Graphics.RenderPrimitives(in RenderParams, MeshTopology, int vertexCount, int instanceCount = 1);
static void Graphics.RenderPrimitivesIndexed(in RenderParams, MeshTopology, GraphicsBuffer indexBuffer, int indexCount, int startIndex = 0, int instanceCount = 1);
static void Graphics.RenderPrimitivesIndirect(in RenderParams, MeshTopology, GraphicsBuffer argsBuffer, int commandCount = 1, int startCommand = 0);
static void Graphics.RenderPrimitivesIndexedIndirect(in RenderParams, MeshTopology, GraphicsBuffer indexBuffer, GraphicsBuffer commandBuffer, int commandCount = 1, int startCommand = 0);
```
(The task text named the 3rd RenderMeshIndirect parameter `commandBuffer`; in 6000.6 it is `argsBuffer`. Positional calls are unaffected.)
- Call as `Graphics.RenderMeshIndirect(rp, mesh, args, 1);` (no `ref`/`in` keyword needed - `in`). VERIFIED-DOCS (examples in `DOCS\ScriptReference\Graphics.RenderMeshIndirect.html`).
- Managed pre-checks (IL): `RenderMeshIndirect` throws `InvalidOperationException` if `!SystemInfo.supportsInstancing` or `!SystemInfo.supportsIndirectArgumentsBuffer`. `RenderMeshPrimitives` checks only `supportsInstancing`. Neither checks `material.enableInstancing`. `RenderMeshInstanced` additionally throws "Material needs to enable instancing for use with RenderMeshInstanced." if `!material.enableInstancing`, and `ArgumentNullException` for null data. VERIFIED-SOURCE (ASM IL of these methods).
- `RenderMeshIndirect` "only works on platforms that support compute shaders" (DX11/DX12/Vulkan on Windows = fine). VERIFIED-DOCS.

### 2.2 RenderParams (VERIFIED-SOURCE: field list + `.ctor(Material)` IL)
Fields/properties: `layer, renderingLayerMask, rendererPriority, entityId, worldBounds, camera, motionVectorMode, reflectionProbeUsage, material, matProps, shadowCastingMode, receiveShadows, lightProbeUsage, lightProbeProxyVolume (obsolete 6000.5), overrideSceneCullingMask, sceneCullingMask, forceMeshLod, meshLodSelectionBias, sortingLayerID, sortingOrder`. `instanceID` is `[Obsolete(error:true)]` -> use `entityId`.

`new RenderParams(Material mat)` sets exactly:
| field | value |
|---|---|
| layer | 0 |
| renderingLayerMask | `RenderingLayerMask.defaultRenderingLayerMask` |
| rendererPriority | 0 |
| worldBounds | `new Bounds(Vector3.zero, Vector3.zero)` (zero-size at origin!) |
| camera | null (= all cameras) |
| motionVectorMode | Camera (0) |
| reflectionProbeUsage | Off (0) |
| matProps | null |
| **shadowCastingMode** | **Off (0)** |
| **receiveShadows** | **false** |
| lightProbeUsage | Off (0) |
| overrideSceneCullingMask / sceneCullingMask | false / 0 |
| entityId | EntityId.None |
| forceMeshLod | -1 |
| meshLodSelectionBias / sortingLayerID / sortingOrder | 0 |

PITFALLS:
- `default(RenderParams)` / `new RenderParams()` leaves `renderingLayerMask = 0` -> nothing draws in URP. Always use the Material ctor. (Community-verified: https://discussions.unity.com/t/graphics-rendermesh-doesnt-work-in-urp/918319 ; consistent with ctor IL.)
- Shadows are OFF by default: set `rp.shadowCastingMode = ShadowCastingMode.On; rp.receiveShadows = true;`.
- For Indirect/Primitives the zero-size default `worldBounds` means "a point at the origin" -> culled whenever the origin is off-screen and wrong shadow-caster culling. Always set `worldBounds` to enclose all (bent) instances. RenderMeshInstanced computes bounds automatically unless overridden. VERIFIED-DOCS (RenderMeshInstanced page) / bounds-as-culling-volume VERIFIED-DOCS (RenderMeshIndirect page: "Use RenderParams.worldBounds to define bounds to cull and sort the geometry ... as a single entity").
- Community reports (DrawMeshInstancedIndirect and RenderMeshIndirect in URP): the bounds CENTER ends up as a translation in the object-to-world used by the draw, i.e. `TransformObjectToWorld()` shifts instances by `worldBounds.center`. UNVERIFIED in 6000.6 (sources: https://discussions.unity.com/threads/bounds-parameter-in-drawmeshinstancedindirect-dictates-world-space-position-of-mesh.495313/ , https://zenn.dev/link/comments/5ce0ac65053476 which subtracts a `_BoundsOffset`). Unity staff (FredMoreau) state for these low-level APIs "there is no gameObject and no transform component, thus Object Space is World Space" (https://discussions.unity.com/t/indirect-procedural-rendering-with-shader-graph/1664601). => Robust rule: in indirect/primitives shaders compute world-space positions yourself and never use `UNITY_MATRIX_M` / `TransformObjectToWorld*`.
- `lightProbeUsage` Off by default: what SH the draw receives (`unity_SHAr..`) is UNVERIFIED (expected: ambient probe). If ambient looks wrong, set `rp.lightProbeUsage = LightProbeUsage.BlendProbes` or sample your own ambient term.

### 2.3 GraphicsBuffer.IndirectDrawIndexedArgs (VERIFIED-SOURCE: ASM + CGINC)
- Managed struct (Sequential): `uint indexCountPerInstance, instanceCount, startIndex, baseVertexIndex, startInstance` (auto-properties with get/set), `const int size = 20`. Non-indexed `IndirectDrawArgs`: `vertexCountPerInstance, instanceCount, startVertex, startInstance`, `size = 16`.
- HLSL mirror: `CGINC\UnityIndirect.cginc:45-61` (`commandId * 20`), `:11-25` (`commandId * 16`).
- Members are properties: `args[i].instanceCount = n` works on arrays (element is a variable) but NOT on `List<T>[i]` (CS1612).
```csharp
GraphicsBuffer args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
var cmd = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
cmd[0].indexCountPerInstance = mesh.GetIndexCount(0);
cmd[0].startIndex            = mesh.GetIndexStart(0);
cmd[0].baseVertexIndex       = mesh.GetBaseVertex(0);
cmd[0].instanceCount         = (uint)count;
cmd[0].startInstance         = 0;
args.SetData(cmd);
// GPU-written args (compute): commonly Target.IndirectArguments | Target.Raw + RWByteAddressBuffer (UNVERIFIED for 6000.6/DX12).
```
- `GraphicsBuffer` ctors: `(Target, int count, int stride)`, `(Target, UsageFlags, int count, int stride)`; `Target` flags: `Vertex=1, Index=2, CopySource=4, CopyDestination=8, Structured=16, Raw=32, Append=64, Counter=128, IndirectArguments=256, Constant=512`. `MaterialPropertyBlock.SetBuffer(string|int, GraphicsBuffer)` exists. VERIFIED-SOURCE (ASM). Release buffers in `OnDisable/OnDestroy`.

### 2.4 Shader side (URP HLSL) for RenderMeshIndirect / RenderMeshPrimitives
Facts:
- `UnityIndirect.cginc` (`CGINC\UnityIndirect.cginc`) is plain HLSL with NO includes and no dependencies on UnityCG -> can be `#include "UnityIndirect.cginc"` from an `HLSLPROGRAM` after the URP includes. VERIFIED-SOURCE (file content, 89 lines); include-path resolution from HLSLPROGRAM: community-verified with URP 14 (zenn.dev link above). No symbol collisions: `GetCommandID`, `IndirectDrawArgs`, `IndirectDrawIndexedArgs`, `GetIndirect*`, `globalIndirectDrawArgs` do not occur in URP/Core ShaderLibrary (grep). VERIFIED-SOURCE.
- Required lines (docs): `#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs` then `#include "UnityIndirect.cginc"`; call `InitIndirectDrawArgs(0);` at the start of the vertex function. VERIFIED-DOCS (`DOCS\ScriptReference\Graphics.RenderMeshIndirect.html`).
- What the helpers do (VERIFIED-SOURCE `UnityIndirect.cginc`):
  - `:5-7` `uint unity_BaseCommandID; uint GetCommandID(uint svDrawID) { return unity_BaseCommandID + svDrawID; }` then `#define unity_BaseCommandID Use_GetCommandID_function_instead_of_unity_BaseCommandID` (don't reference it directly). It is a loose global (lands in `$Globals`).
  - `:76-87` with `UNITY_INDIRECT_DRAW_ARGS` defined: declares `ByteAddressBuffer unity_IndirectDrawArgs;` (Unity binds the args buffer), `static UNITY_INDIRECT_DRAW_ARGS globalIndirectDrawArgs;`, `InitIndirectDrawArgs(svDrawID)` loads the command, plus `GetIndirectInstanceCount()`, `GetIndirectVertexCount()`, `GetIndirectInstanceID(svInstanceID)`, `GetIndirectInstanceID_Base(svInstanceID)`, `GetIndirectVertexID(_Base)`.
  - `:64-70` SV_InstanceID semantics differ per API: on Vulkan/WebGPU `SV_InstanceID` includes `startInstance`; on D3D11/D3D12/Metal it does not. `GetIndirectInstanceID()` normalizes to 0-based-within-command; `GetIndirectInstanceID_Base()` = global index including `startInstance`. Use `_Base` to index a buffer shared by several commands.
- SV_DrawID / multi-command: the docs pass 0. Whether Unity issues one native draw per command (setting `unity_BaseCommandID` each time) on DX11/DX12 is UNVERIFIED -> keep `commandCount = 1` per call, or don't depend on per-command values other than via `_Base` instance IDs.
- `#pragma multi_compile_instancing` is NOT needed: the official RenderMeshIndirect and RenderMeshPrimitives example shaders have no instancing pragmas and just read `uint svInstanceID : SV_InstanceID`. VERIFIED-DOCS (both pages). RenderMeshPrimitives: "Use SV_InstanceID semantic in shaders to access the rendered instance" (0..instanceCount-1). VERIFIED-DOCS.
- If you DO declare instancing: `#pragma instancing_options procedural:FunctionName` "Generates an additional variant for use with Graphics.RenderMeshIndirect" and Unity calls `FunctionName()` at the start of the vertex stage (PROCEDURAL_INSTANCING_ON). VERIFIED-DOCS (`DOCS\Manual\gpu-instancing-birp-shader-modifications.html`). In URP that path keeps `UNITY_MATRIX_M = unity_ObjectToWorld` (UnityPerDraw cbuffer, `URP\ShaderLibrary\UnityInput.hlsl:110-112`) - only `INSTANCING_ON` redefines it to the per-instance array (`CORE\ShaderLibrary\UnityInstancing.hlsl:458-483`), and `UNITY_INSTANCING_PROCEDURAL_FUNC` must exist (`:225-231`). Writing `unity_ObjectToWorld` inside the setup function (BiRP idiom) is UNVERIFIED for URP/DXC. A community report (6000.2.9) says RenderMeshIndirect calls the setup function while RenderPrimitivesIndexedIndirect does not and needed `material.EnableKeyword("PROCEDURAL_INSTANCING_ON")` (forum thread above). => avoid this path.
- If `INSTANCING_ON` were active in an indirect draw, `UNITY_MATRIX_M` reads `unity_Builtins0Array[unity_InstanceID]` that Unity never filled -> garbage. `unity_InstanceID = inputInstanceID + unity_BaseInstanceID` (`UnityInstancing.hlsl:189-222`). Another reason to declare no instancing pragmas in indirect shaders.
- Recommended URP pattern (robust, no matrices): one `.hlsl` include shared by every pass:
```hlsl
// RingInstancing.hlsl  (included AFTER Core.hlsl in every pass)
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
struct RingInstance { float4x4 objectToWorld; float4 params; };   // match C# struct, stride 80
StructuredBuffer<RingInstance> _RingInstances;                     // rp.matProps.SetBuffer("_RingInstances", buf)

void RingInstanceTransform(float3 posOS, float3 nrmOS, uint svInstanceID, out float3 posWS, out float3 nrmWS)
{
    InitIndirectDrawArgs(0);                                  // omit both for RenderMeshPrimitives and use svInstanceID directly
    uint id = GetIndirectInstanceID_Base(svInstanceID);
    float4x4 m = _RingInstances[id].objectToWorld;
    posWS = mul(m, float4(posOS, 1.0)).xyz;
    nrmWS = normalize(mul((float3x3)m, nrmOS));               // uniform scale assumed; else pass inverse-transpose
    // posWS = RingBend(posWS);  nrmWS = RingBendNormal(...)  // the SAME bend function the ground shader uses
}
```
Forward pass: `output.positionCS = TransformWorldToHClip(posWS);` (`CORE\ShaderLibrary\SpaceTransforms.hlsl:115`).
ShadowCaster pass (re-implements `URP\Shaders\ShadowCasterPass.hlsl:33-47` with world-space inputs):
```hlsl
float3 _LightDirection; float3 _LightPosition;      // set by URP ShadowUtils (ShadowCasterPass.hlsl:10-14)
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
...
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 L = normalize(_LightPosition - posWS);
#else
    float3 L = _LightDirection;
#endif
float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, L));   // Shadows.hlsl:542
cs = ApplyShadowClamping(cs);                                          // Shadows.hlsl:553
```
Pass tags to provide (names as in `URP\Shaders\Lit.shader:100-103,195-198,336-339,381-384`): `"LightMode"="UniversalForward"`, `"ShadowCaster"` (ZWrite On, ZTest LEqual, ColorMask 0), `"DepthOnly"` (ColorMask R in Lit), `"DepthNormals"` (fragment writes `half4 normalWS : SV_Target0` and, under `_WRITE_RENDERING_LAYERS`, `uint : SV_Target1 = EncodeMeshRenderingLayer()`; under `_GBUFFER_NORMALS_OCT` octahedral-packed; see `URP\Shaders\DepthNormalsPass.hlsl:48-81`). Forward+ keyword in URP 17.6 is `_CLUSTER_LIGHT_LOOP` (`Lit.shader:158`), not `_FORWARD_PLUS`.
- Shadow/depth passes: RenderMesh* draws go through normal culling + DrawRenderers, so every pass whose LightMode exists is drawn (shadow maps, depth prepass/copy, DepthNormals for SSAO) if `shadowCastingMode != Off` for shadows. The URP pipeline itself doesn't special-case them (no references to RenderMesh* in URP/Core source). UNVERIFIED-as-mechanism, but matches docs ("This Mesh is affected by Lights, can cast and receive shadows", `Graphics.RenderMesh.html`) and community shaders (zenn) that rely on ShadowCaster/DepthOnly passes.
- Every pass must call `InitIndirectDrawArgs(0)` itself (each pass is a separate draw) and use the SAME position code -> otherwise shadow acne/peter-panning or depth/colour mismatch (deadly with Depth Priming / ZTest Equal; currently `PC_Renderer.asset:57 m_DepthPrimingMode: 0`).

### 2.5 SRP Batcher
- SRP Batcher compatibility needs built-ins in `UnityPerDraw` and material props in `UnityPerMaterial`; GameObjects using MaterialPropertyBlocks are not SRP-batched; GPU instancing "isn't compatible with the SRP Batcher". VERIFIED-DOCS (`DOCS\Manual\SRPBatcher-Materials.html`, `SRPBatcher-Incompatible.html`).
- => RenderMeshInstanced/Indirect/Primitives draws (instanced, usually with `matProps`) never take the SRP Batcher path; they render through the standard SRP path. Keeping the shader SRP-Batcher-compatible (`CBUFFER_START(UnityPerMaterial)`) is harmless and lets the same material work on MeshRenderers. The loose `unity_BaseCommandID` / `unity_IndirectDrawArgs` / `StructuredBuffer` globals are not material properties; impact on the Inspector "SRP Batcher compatible" flag UNVERIFIED (irrelevant for indirect draws). Do NOT put StructuredBuffers in `UnityPerMaterial`.
- Manual note: automatic instancing of MeshRenderers in URP works with custom shaders "only if you disable the SRP Batcher or make the shader incompatible" (`DOCS\Manual\GPUInstancing.html`). This concerns GameObjects, not explicit RenderMesh* calls.

### 2.6 RenderMeshInstanced limits (6000.6)
- Docs: "You can only render a maximum of 1023 instances at once ... By default, Unity uses an objectToWorld matrix and a worldToObject matrix for each instance, which means you can render a maximum of 511 instances at once. To remove the worldToObject matrix ... add `#pragma instancing_options assumeuniformscaling`." Throws `InvalidOperationException` if `Material.enableInstancing` is false. VERIFIED-DOCS (`DOCS\ScriptReference\Graphics.RenderMeshInstanced.html`). The managed code does NOT split or clamp to 1023 (IL clamps only to the array length) - chunk yourself. VERIFIED-SOURCE (IL).
- `Graphics.kMaxDrawMeshInstanceCount` is a `static readonly int` filled from native `Internal_GetMaxDrawMeshInstanceCount()` in the static ctor (not a compile-time const). VERIFIED-SOURCE (IL `.cctor`).
- Shader-side array size: `UNITY_INSTANCED_ARRAY_SIZE` = 2 (flexible, sized per batch) on D3D11/D3D12-style APIs, else `maxcount`/500 (250 on mobile Vulkan/Switch/WebGPU); `forcemaxcount:N` overrides. VERIFIED-SOURCE (`CORE\ShaderLibrary\UnityInstancing.hlsl:16-19,256-270`). Unity issue "Only 511 instances instead of 1023 are drawn per call when using Graphics.RenderMeshInstanced" (Won't Fix per search summary, 64 KB cbuffer) - details UNVERIFIED.
- Instance struct for `RenderMeshInstanced<T>`: must contain `Matrix4x4 objectToWorld`; optional `uint renderingLayerMask`, `Matrix4x4 prevObjectToWorld` (exact names/types); other members ignored. VERIFIED-DOCS.
- Practical: chunks of <= 511 (or <= 1023 with assumeuniformscaling); for thousands of instances use RenderMeshPrimitives/Indirect + StructuredBuffer (no per-call limit). Manual tip: don't instance meshes with < 256 vertices (`DOCS\Manual\gpu-instancing-troubleshoot.html`).

### 2.7 RenderMeshPrimitives
- `Graphics.RenderMeshPrimitives(in RenderParams, Mesh, int submeshIndex, int instanceCount = 1)`: like RenderMeshIndirect but args passed directly; shader reads `SV_InstanceID` (0..N-1). Doc example sets `rp.worldBounds`, `rp.matProps.SetMatrix("_ObjectToWorld", ...)`, uses custom `_ObjectToWorld` uniform and `UNITY_MATRIX_VP`. VERIFIED-DOCS (`Graphics.RenderMeshPrimitives.html`). Best choice when the CPU knows the count (static scatter): no args buffer, no UnityIndirect.cginc.

---

## 3. Cameras: who renders RenderMesh* draws

- "RenderMesh renders a Mesh for the current frame ... can be rendered by all Cameras or by a specific Camera ... submits the Mesh for rendering, which means it does not render the Mesh immediately ... The allocation happens immediately and exists until the end of frame if the object is in the render queue for all cameras. Otherwise, the allocation exists until the specified Camera finishes rendering." VERIFIED-DOCS (`DOCS\ScriptReference\Graphics.RenderMesh.html`). `RenderParams.camera`: "If set to null (default) renders for all cameras." VERIFIED-DOCS (`RenderParams-camera.html`).
- Consequences (inference from the doc wording, UNVERIFIED as tested behaviour):
  - `camera = null` + submit in `Update/LateUpdate` -> drawn by every camera that renders later in the same frame: Game camera, Scene view, a disabled capture camera rendered manually via `Camera.Render()` or `RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest{destination=rt})` / `UniversalRenderPipeline.SingleCameraRequest`, also called later in that frame.
  - Draws submitted AFTER a camera already rendered this frame are missed by it (they'd only appear next frame for the main camera if re-submitted). Do captures from `LateUpdate` with a late script execution order, not before the scene's `Update` submitters run.
  - `yield return new WaitForEndOfFrame()` capture + manual `Render()`: whether the per-frame queue is still alive then is UNVERIFIED -> avoid; render capture cameras in LateUpdate.
  - `camera = specificCam`: consumed when that camera finishes rendering -> a second `Render()` of the same camera in one frame won't see it; and if that camera does not render this frame, queued draws persist until it does -> submitting every frame for a rarely-rendered capture camera can accumulate duplicate draws (double-blended transparents, z-fighting, memory). Re-submit right before each manual Render, never "fire and forget" per frame.
  - Robust per-camera alternative: submit from `RenderPipelineManager.beginCameraRendering += (ctx, cam) => {...}` with `rp.camera = cam`. In URP 17.6 `BeginCameraRendering` runs before that camera's culling: `CameraRenderingScope` at `URP\Runtime\UniversalRenderPipeline.cs:1215` precedes `RenderSingleCamera(...)` at `:1275`, which culls at `:1021-1026`; SingleCameraRequest also wraps in `CameraRenderingScope` (`:754`). VERIFIED-SOURCE (order); that the draw is then included is UNVERIFIED but implied.
- `rp.layer` is tested against `camera.cullingMask` (default layer 0). `RenderingLayerMask` must be non-zero (see 2.2).
- Manual camera rendering docs: `Camera.Render()` - "create a camera and disable it. Then call Render on it"; `RenderPipeline.SubmitRenderRequest` is synchronous ("processed sequentially within your script"), URP supports `StandardRequest` and `UniversalRenderPipeline.SingleCameraRequest` (Base cameras only). VERIFIED-DOCS (`Camera.Render.html`, `Rendering.RenderPipeline.SubmitRenderRequest.html`). URP logs an error if the request has no destination texture (`UniversalRenderPipeline.cs:701-705`). VERIFIED-SOURCE.
- Captures: verify in the BUILT player with a pixel test that instanced geometry (and its shadow) is present in the capture RT.

---

## 4. GPU Resident Drawer (GRD) in 6000.6 / URP 17.6 - keep it OFF

Requirements (VERIFIED-SOURCE + VERIFIED-DOCS):
- Mode on the URP asset: `GPUResidentDrawerMode { Disabled, InstancedDrawing }` (`CORE\Runtime\GPUDriven\IGPUResidentRenderPipeline.cs:9-20`); serialized `m_GPUResidentDrawerMode` default Disabled (`URP\Runtime\Data\UniversalRenderPipelineAsset.cs:595-611`); C# `UniversalRenderPipelineAsset.gpuResidentDrawerMode {get;set}` (`:1722-1733`). URP passes `allowInEditMode = true` (`:609`).
- Every renderer in the asset's list must be a `UniversalRendererData` using the cluster light loop (Forward+ or Deferred+) (`UniversalRenderPipelineAsset.cs:1759-1788`).
- Project-level: `BatchRendererGroup.BufferTarget == RawBuffer` (no GLES/constant-buffer platforms), not visionOS, in Editor the GRD kernel must exist and Graphics Settings "BatchRendererGroup Variants" must be **Keep All** (`CORE\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs:30-70`, `:72-111`). `BatchRendererGroupStrippingMode { KeepIfEntitiesGraphics=0, StripAll=1, KeepAll=2 }` (VERIFIED-SOURCE, UnityEditor.CoreModule reflection).
- Per renderer (docs `DOCS\Manual\urp\gpu-resident-drawer.html`, `make-object-compatible-gpu-rendering.html`): MeshRenderer only; shader must support `DOTS_INSTANCING_ON` (URP shaders get it via `#include_with_pragmas ".../ShaderLibrary/DOTS.hlsl"`, which is `#pragma multi_compile _ DOTS_INSTANCING_ON` + `#pragma target 4.5 DOTS_INSTANCING_ON`, `URP\ShaderLibrary\DOTS.hlsl:1-13`); no MaterialPropertyBlock; default sortingLayer/order; no Proxy Volume/anchor override; no OnWillRenderObject/OnBecameVisible/Invisible scripts; <= 128 materials; no Enlighten realtime GI; must not move between one camera's render and the next. Exclusion reasons enum: `CORE\Runtime\GPUDriven\Debug\GPUResidentDrawerDebug.cs:22-46` (`CustomMaterialPropertyBlock`, `MissingDOTSInstancing`, `GPUDrivenDisabled`, ...). Opt out per renderer: `Renderer.allowGPUDrivenRendering = false` (VERIFIED-SOURCE ASM) or component `DisallowGPUDrivenRendering` (`CORE\Runtime\GPUDriven\Components\DisallowGPUDrivenRendering.cs`).
- Unsupported with GRD: animated LOD cross-fade, `Light.shadowMatrixOverride` for caster culling; GPU occlusion culling uses bounding spheres + previous/current depth pyramids (`DOCS\Manual\urp\gpu-culling.html`).

Why OFF for The Ring (custom bent vertex shaders):
1. Culling is done from CPU-side renderer bounds (bounding spheres, small-mesh screen-percentage culling, optional GPU occlusion vs last frame's depth). Geometry displaced in the vertex shader (ring curvature, "land rising into the sky") lies outside those bounds -> popping/missing chunks, especially at the horizon where the ring curves up. (Mechanism VERIFIED-DOCS; impact = inference.)
2. Custom HLSL shaders without DOTS_INSTANCING_ON are excluded anyway (`WorldProcessor.cs:189-222` -> `MissingDOTSInstancing`); supporting it means rewriting every shader to UNITY_DOTS_INSTANCED_PROP metadata + `#pragma target 4.5` variants: more variants, longer builds ("Build times are longer because Unity compiles all the BatchRendererGroup shader variants"), more ways to differ between Editor and Player.
3. Renderers with MaterialPropertyBlocks (per-segment parameters) are excluded anyway; the mix of GRD/non-GRD paths complicates debugging (Frame Debugger shows "Hybrid Batch Group").
4. Capture cameras: GRD requires objects not to move between cameras' renders and enables probe-atlas blending by default with Forward+; extra state that can differ between the main and capture renders.
5. Our heavy repetition already uses explicit RenderMeshPrimitives/Indirect; GRD gives no extra benefit and "slightly increases GPU workload".

Current project state (VERIFIED-SOURCE, read-only):
- `Assets\Settings\PC_RPAsset.asset:85` `m_GPUResidentDrawerMode: 1` (InstancedDrawing = ON) ; `:72 m_UseSRPBatcher: 1` ; `:87 m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0` ; `:86 m_SmallMeshScreenPercentage: 0`.
- `Assets\Settings\Mobile_RPAsset.asset:85` `m_GPUResidentDrawerMode: 0`.
- `Assets\Settings\PC_Renderer.asset:56` `m_RenderingMode: 2` (Forward+; enum `Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3`, `URP\Runtime\UniversalRenderer.cs:11-23`); `:57 m_DepthPrimingMode: 0`; SSAO renderer feature present (`:71`) -> custom shaders need DepthNormals.
- `ProjectSettings\GraphicsSettings.asset:49` `m_BrgStripping: 2` (KeepAll); `:40 m_CustomRenderPipeline: {fileID: 0}`; `ProjectSettings\QualitySettings.asset:105` PC quality level uses PC_RPAsset (guid 4b83569d67af61e458304325a23e5dfd).
- Fix: set `m_GPUResidentDrawerMode: 0` in PC_RPAsset (or `((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;` from an editor setup script). With GRD off, BRG variants may be set to StripAll (1) to shorten builds (only if no BRG/Entities usage).

---

## 5. Other 6000.6 API pitfalls spotted (VERIFIED-SOURCE, ASM Obsolete attributes)
- `Object.GetInstanceID()` -> obsolete ERROR ("Use GetEntityId instead.").
- `Object.FindObjectsOfType*` obsolete (warning); `FindObjectsByType(..., FindObjectsSortMode)` obsolete (warning) -> use `FindObjectsByType<T>()` / `FindObjectsByType<T>(FindObjectsInactive)`.
- `Graphics.DrawMesh*/DrawMeshInstanced*/DrawMeshInstancedIndirect` overloads with `LightProbeProxyVolume` obsolete since 6000.5; DrawMesh docs: "This function is now obsolete. Use Graphics.RenderMesh instead."
- `new Material(string shaderSource)` / `Material.Create(string)` obsolete ERROR -> shaders must be `.shader` assets (find with `Shader.Find`, and make sure they are included in the build: referenced by a material in a scene/Resources or listed in Always Included Shaders).
- `RenderParams.instanceID` obsolete ERROR -> `entityId`. `RenderParams.lightProbeProxyVolume` obsolete (warning).
- `Mesh.uv1` obsolete ERROR -> `uv2`.

---

## 6. C# skeleton - indirect scatter with shadows

```csharp
using UnityEngine; using UnityEngine.Rendering;
public sealed class RingScatter : MonoBehaviour {
    [SerializeField] Material mat; Mesh mesh; GraphicsBuffer inst, args; RenderParams rp; int count;
    struct RingInstance { public Matrix4x4 objectToWorld; public Vector4 prm; } // stride 80
    void OnEnable() {
        // mesh = ProceduralMeshes.Rock(...); instances = ...; count = instances.Length;
        inst = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 80);
        // inst.SetData(instances);
        args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        var a = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        a[0].indexCountPerInstance = mesh.GetIndexCount(0); a[0].startIndex = mesh.GetIndexStart(0);
        a[0].baseVertexIndex = mesh.GetBaseVertex(0); a[0].instanceCount = (uint)count; args.SetData(a);
        rp = new RenderParams(mat) {                       // NEVER default(RenderParams)
            worldBounds = ComputeWorldBoundsIncludingBend(), // must enclose bent geometry; center choice irrelevant if shader ignores UNITY_MATRIX_M
            shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
            layer = gameObject.layer, matProps = new MaterialPropertyBlock() };
        rp.matProps.SetBuffer("_RingInstances", inst);
    }
    void Update() { if (count > 0) Graphics.RenderMeshIndirect(rp, mesh, args, 1); }   // every frame
    // static count alternative: Graphics.RenderMeshPrimitives(rp, mesh, 0, count);  (shader: no InitIndirectDrawArgs, use SV_InstanceID)
    void OnDisable() { inst?.Release(); inst = null; args?.Release(); args = null; }
    Bounds ComputeWorldBoundsIncludingBend() => new Bounds(Vector3.zero, Vector3.one * 1e5f);
}
```

---

## Sources (accessed 2026-10-29)
- `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Resources\CGIncludes\UnityIndirect.cginc` - indirect helper source (struct layouts, per-API instance ID, InitIndirectDrawArgs).
- `...\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll` (+ `.xml`), `UnityEngine.PhysicsModule.dll`, `UnityEditor.CoreModule.dll` - reflection: signatures (`in RenderParams`), RenderParams fields + ctor IL defaults, IndirectDraw*Args layout/size, RenderMesh* managed checks, Mesh/MeshData API, MeshUpdateFlags, MeshColliderCookingOptions, Physics.BakeMesh, Obsolete attributes, GraphicsBuffer.Target, BatchRendererGroupStrippingMode.
- `...\Editor\Data\Documentation\en\ScriptReference\` (offline 6000.6 docs): Graphics.RenderMeshIndirect, Graphics.RenderMeshInstanced, Graphics.RenderMeshPrimitives, Graphics.RenderMesh, Graphics.DrawMesh, RenderParams (+ -ctor/-camera/-worldBounds/...), Mesh-indexFormat, Mesh-bounds, Mesh.RecalculateBounds, Mesh.UploadMeshData, Mesh.MarkDynamic, Mesh.SetIndices, Mesh.SetVertices, Mesh.AllocateWritableMeshData, Mesh.ApplyAndDisposeWritableMeshData, Rendering.MeshUpdateFlags, MeshCollider-cookingOptions, MeshCollider-sharedMesh, MeshColliderCookingOptions.*, Physics.BakeMesh, Renderer-localBounds, Renderer-bounds, Camera.Render, Rendering.RenderPipeline.SubmitRenderRequest - API behaviour.
- `...\Documentation\en\Manual\`: SRPBatcher-Materials, SRPBatcher-Incompatible, GPUInstancing, gpu-instancing-enable, gpu-instancing-troubleshoot, gpu-instancing-birp-shader-modifications (procedural:FunctionName), class-MeshCollider; `Manual\urp\gpu-resident-drawer`, `make-object-compatible-gpu-rendering`, `gpu-culling`, `gpu-resident-drawer-performance` - SRP Batcher / instancing / GRD rules.
- `Library\PackageCache\com.unity.render-pipelines.core@2d66c71e606e\ShaderLibrary\UnityInstancing.hlsl` - instancing keywords, unity_InstanceID, array sizes, matrix macros.
- `...core@2d66c71e606e\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs`, `IGPUResidentRenderPipeline.cs`, `Debug\GPUResidentDrawerDebug.cs`, `SceneProcessors\WorldProcessor.cs`, `Components\DisallowGPUDrivenRendering.cs` - GRD requirements/exclusions.
- `Library\PackageCache\com.unity.render-pipelines.universal@8457e85b8184\Runtime\Data\UniversalRenderPipelineAsset.cs`, `Runtime\UniversalRenderer.cs`, `Runtime\UniversalRenderPipeline.cs` - GRD support check, RenderingMode enum, camera rendering order / render requests.
- `...universal@8457e85b8184\Shaders\Lit.shader`, `Shaders\ShadowCasterPass.hlsl`, `Shaders\DepthNormalsPass.hlsl`, `ShaderLibrary\DOTS.hlsl`, `ShaderLibrary\UnityInput.hlsl`, `ShaderLibrary\Shadows.hlsl`, `core ShaderLibrary\SpaceTransforms.hlsl` - pass tags, shadow bias functions, DOTS pragma, UnityPerDraw.
- Project files (read only): `Assets\Settings\PC_RPAsset.asset`, `Mobile_RPAsset.asset`, `PC_Renderer.asset`, `ProjectSettings\GraphicsSettings.asset`, `ProjectSettings\QualitySettings.asset` - current GRD/renderer settings.
- https://discussions.unity.com/t/graphics-rendermesh-doesnt-work-in-urp/918319 - default RenderParams renderingLayerMask = 0 pitfall.
- https://zenn.dev/link/comments/5ce0ac65053476 - URP RenderMeshIndirect Lit shader with ShadowCaster/DepthOnly using UnityIndirect.cginc in HLSLPROGRAM; bounds-center offset report.
- https://discussions.unity.com/threads/bounds-parameter-in-drawmeshinstancedindirect-dictates-world-space-position-of-mesh.495313/ and https://discussions.unity.com/t/graphics-drawmeshinstancedindirect-bounds-field-causes-instances-to-move/928981 - bounds center added to world position (community).
- https://discussions.unity.com/t/indirect-procedural-rendering-with-shader-graph/1664601 (+ /12) - Unity staff thread: object space = world space for low-level APIs; procedural:setup keyword behaviour; RenderPrimitivesIndexedIndirect keyword report (6000.2.9).
- Web search summary for "Only 511 instances instead of 1023 are drawn per call when using Graphics.RenderMeshInstanced" (issuetracker page itself returned 502/redirect; details unverified).
