# R3 - Procedural meshes and instancing (Unity 6000.6.0f1, URP 17.6.0)

Researcher: read-only R3. Ground truth: managed assemblies, inspected as IL with the Editor's own `Unity.Cecil.dll`, plus the XML docs; the installed URP/SRP Core package source; project settings files. Web sources are listed at the end.
Abbreviations: `XML` = `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.xml`, `CORE` = `Library\PackageCache\com.unity.render-pipelines.core@2d66c71e606e`, `URP` = `Library\PackageCache\com.unity.render-pipelines.universal@8457e85b8184`, IL = disassembled `UnityEngine.CoreModule.dll`.

## 0. Decisions (TL;DR)

1. **The API is `Mesh.AllocateWritableMeshData`, not `AcquireWritableMeshData`.** No method named `AcquireWritableMeshData` exists in 6000.6 (the IL lists only `AllocateWritableMeshData(int | Mesh | Mesh[] | List<Mesh>)`). `AcquireReadOnlyMeshData` is the read-only counterpart.
2. **Unique geometry uses static `MeshRenderer` GameObjects with the SRP Batcher.** This covers the cable LOD0/1/2 chunks, walkway, colossi, stairs, terrace and beads. Use no `MaterialPropertyBlock`s; per-colour materials or vertex colour instead.
3. **Repeated props use `Graphics.RenderMeshInstanced`, chunked at ≤ 500 instances per call with explicit per-chunk `worldBounds`.** This covers rail posts (~470), window dots (≤ 1296) and bollards (28). Chunks are spatially sorted. Fallback ladder step: bake posts/bollards into the walkway/stair chunk meshes, which costs ~50 k triangles.
4. **Generate meshes at runtime during the opening fade.** The generators are deterministic pure functions, and the cable LOD0 costs ~25 ms of plain C#. Do not ship baked `.asset` meshes; keep an optional Editor "bake for inspection" menu only.
5. **Turn the GPU Resident Drawer OFF.** It is currently **ON** in `Assets\Settings\PC_RPAsset.asset:85` (`m_GPUResidentDrawerMode: 1`). The spec says it is not needed (MASTER_PROMPT.md line 880), and handwritten ThreadFlat without `DOTS_INSTANCING_ON` is excluded from it anyway.
6. **Burst, Mathematics and Jobs are usable without touching `manifest.json`.** In 6000.6 they are engine modules, and Collections is a resolved builtin dependency. They are not required: use them only if a generator measures > 100 ms.
7. **Do not use rotation-minimising frames for the helix walkway deck.** On this helix the RMF rolls ~87° relative to "radially level" over one turn. Use the analytic cylindrical frame. Use RMF (double reflection) only for round tubes and free-form sweeps.

## 1. Verified facts

### 1.1 Mesh API (6000.6 signatures from IL, defaults shown)
- `Mesh.MeshDataArray Mesh.AllocateWritableMeshData(int meshCount)` (+ `Mesh`, `Mesh[]`, `List<Mesh>` overloads that copy existing meshes). XML:26559.
- `Mesh.ApplyAndDisposeWritableMeshData(MeshDataArray data, Mesh mesh, MeshUpdateFlags flags = Default)` (+ `Mesh[]`, `List<Mesh>`). It disposes the array; the single-mesh form throws `InvalidOperationException` unless `data.Length == 1` (IL string "{0} length must be 1 to apply to one mesh"). XML:26568-26593.
- The `Mesh.MeshData` methods are:
  - `SetVertexBufferParams(int vertexCount, params VertexAttributeDescriptor[] | NativeArray<VertexAttributeDescriptor>)`
  - `SetIndexBufferParams(int indexCount, IndexFormat format)`
  - `GetVertexData<T>(int stream = 0)` and `GetIndexData<T>()` (both return `NativeArray<T>`)
  - `subMeshCount { get; set; }`
  - `SetSubMesh(int index, SubMeshDescriptor desc, MeshUpdateFlags flags = Default)`
  - `vertexCount` and `indexFormat` (read-only)
  - also `SetLod`/`lodCount` (Mesh LOD, not needed here)
  
  XML:27236-27466.
- On `Mesh` (advanced path), the methods are `SetVertexBufferParams`, `SetVertexBufferData<T>(T[] | List<T> | NativeArray<T> data, int dataStart, int meshBufferStart, int count, int stream = 0, MeshUpdateFlags flags = 0)`, `SetIndexBufferParams(int, IndexFormat)`, `SetIndexBufferData<T>(..., MeshUpdateFlags flags = 0)`, `SetSubMesh(int, SubMeshDescriptor, MeshUpdateFlags = 0)`, `GetVertexBufferStride(int stream)`, `GetIndexCount(int)`, `GetTopology(int)`, `MarkModified()` and `UploadMeshData(bool markNoLongerReadable)` (XML:28919).
- On `Mesh` (simple path), every one of `SetVertices`, `SetNormals`, `SetTangents`, `SetColors` (Color and Color32), `SetUVs(channel, Vector2/3/4)` and `SetIndices`/`SetTriangles` has `T[]`, `List<T>` and `NativeArray<T>` overloads (plus `(start, length[, flags])`). `SetIndices(..., MeshTopology, int submesh, bool calculateBounds = true, int baseVertex = 0)`.
- `new SubMeshDescriptor(int indexStart, int indexCount, MeshTopology topology = Triangles)`; its properties are `bounds`, `firstVertex`, `vertexCount` and `baseVertex`.
- `new VertexAttributeDescriptor(VertexAttribute attribute = Position, VertexAttributeFormat format = Float32, int dimension = 3, int stream = 0)`.
- Enum values (IL):
  - `MeshUpdateFlags`: Default=0, DontValidateIndices=1, DontResetBoneBounds=2, DontNotifyMeshUsers=4, DontRecalculateBounds=8, DontValidateLodRanges=16.
  - `IndexFormat`: UInt16=0, UInt32=1.
  - `MeshTopology`: Triangles=0, Quads=2, Lines=3, LineStrip=4, Points=5. Strips are rejected by `SetSubMeshes` (IL string "triangles strip topology, which is no longer supported").
  - `VertexAttribute` order: Position, Normal, Tangent, Color, TexCoord0..7, BlendWeight, BlendIndices.
- Bounds:
  - `MeshUpdateFlags.DontRecalculateBounds` means `SetSubMesh` does **not** recalculate bounds (XML:45708). With Default flags, `SetSubMesh` computes them from the referenced vertices.
  - The official `AllocateWritableMeshData` sample calls `mesh.RecalculateNormals(); mesh.RecalculateBounds();` after apply (docs page, Sources).
  - `RecalculateBounds(MeshUpdateFlags)` exists (XML:27507).
- Index format (docs, Sources):
  - Default is 16-bit (≤ 65 535 vertices).
  - **Changing `indexFormat` sets `subMeshCount` to 1 and clears the index buffer**, so set it before writing indices.
  - Index 0xFFFF may be skipped by some GPUs, so keep UInt16 meshes at ≤ 65 534 vertices.
  - `SetVertexBufferParams` throws if the buffer exceeds `SystemInfo.maxGraphicsBufferSize` (an `Int64`, IL).
- Vertex layout rules (VertexAttributeDescriptor docs):
  - Attributes in a stream follow the enum order.
  - Each attribute must be a multiple of 4 bytes (Float16x3 is invalid).
  - At most 4 streams.
  - Validate exotic formats with `SystemInfo.SupportsVertexAttributeFormat`.
- `Color32` is explicit-layout, 4 bytes (IL: `rgba@0, r@0, g@1, b@2, a@3`), so a `Vector3, Vector3, Color32, Vector2` struct is exactly 36 bytes with no padding.

### 1.2 Graphics.RenderMeshInstanced / RenderMeshIndirect / RenderParams (IL-verified)
- Signatures:
  - `RenderMeshInstanced<T>(in RenderParams rparams, Mesh mesh, int submeshIndex, T[] instanceData, int instanceCount = -1, int startInstance = 0) where T : unmanaged`
  - the same with a `List<T>` overload
  - the `NativeArray<T>` overload takes `RenderParams` by value
  - `RenderMeshIndirect(in RenderParams, Mesh, GraphicsBuffer argsBuffer, int commandCount = 1, int startCommand = 0)`
  
  XML:16137-16178.
- Managed checks in 6000.6 `RenderMeshInstanced`, in order:
  1. `!SystemInfo.supportsInstancing` throws "Instancing is not supported."
  2. **`!rparams.material.enableInstancing` throws `InvalidOperationException` "Material needs to enable instancing for use with RenderMeshInstanced."**
  3. `instanceData == null` throws `ArgumentNullException`.
  
  The count is then `Math.Min((uint)instanceCount, max(0, Length - startInstance))`. **There is no managed upper-limit check.** The native cap applies: docs say "maximum of 1023 instances at once... depends on how much data you use for each instance", which is 511 with objectToWorld + worldToObject.
- `RenderMeshIndirect` checks `supportsInstancing` and `supportsIndirectArgumentsBuffer`. It does **not** check `enableInstancing`.
- `Graphics.kMaxDrawMeshInstanceCount` is a static field filled from native `Internal_GetMaxDrawMeshInstanceCount()` in `Graphics..cctor`. It is enforced only by the legacy `DrawMeshInstanced` ("Count must be in the range of 0 to {0}").
- The instance struct layout (`RenderInstancedDataLayout`, IL) reads `Marshal.SizeOf(T)` and the field offsets of `objectToWorld` (mandatory unless `T == Matrix4x4`), `prevObjectToWorld` and `renderingLayerMask`. In 6000.6 there is **no `rendererShaderUserValue`** field (the GitHub master has it). Other fields are ignored, so custom data can ride along.
- `RenderParams` properties (IL):
  - `layer`, `renderingLayerMask`, `rendererPriority`, `instanceID`, `entityId`
  - `worldBounds`, `camera`, `motionVectorMode`, `reflectionProbeUsage`, `material`, `matProps`
  - `shadowCastingMode`, `receiveShadows`, `lightProbeUsage`, `lightProbeProxyVolume`
  - `overrideSceneCullingMask`, `sceneCullingMask`
  - `forceMeshLod`, `meshLodSelectionBias`, `sortingLayerID`, `sortingOrder`
- `new RenderParams(mat)` defaults (IL `.ctor`):
  - layer 0; renderingLayerMask = default mask; worldBounds = `Bounds(zero, zero)`, which means auto-computed from the instances (docs); camera null (all cameras)
  - motionVectorMode Camera; reflectionProbeUsage Off
  - **shadowCastingMode Off, receiveShadows false**; lightProbeUsage Off
  - forceMeshLod -1
- URP instancing plumbing:
  - `URP\ShaderLibrary\Input.hlsl:218-239` defines `UNITY_MATRIX_M` and then includes `UnityInstancing.hlsl`.
  - In the `INSTANCING_ON` variant, `UNITY_MATRIX_M`/`UNITY_MATRIX_I_M` are redefined to `unity_ObjectToWorldArray`/`unity_WorldToObjectArray` (`CORE\ShaderLibrary\UnityInstancing.hlsl:458-483`).
  - D3D11/D3D12/Vulkan/Metal use flexible cbuffer array sizes (`:16-19`, `:260-261`); otherwise the size is 500 (`:268`).
  - `assumeuniformscaling` (`UNITY_ASSUME_UNIFORM_SCALING`) moves worldToObject to a separate cbuffer (`:317-322`), and `TransformObjectToWorldNormal` then uses M (`CORE\ShaderLibrary\SpaceTransforms.hlsl:201-202`).
  - URP Lit uses `#pragma multi_compile_instancing` + `#include_with_pragmas ".../DOTS.hlsl"` (`URP\Shaders\Lit.shader:184-186`). `DOTS.hlsl:5` adds `multi_compile _ DOTS_INSTANCING_ON`, so it is only needed for GRD/BRG.
- SRP Batcher vs instancing: the URP manual says GPU instancing for *GameObjects* with custom shaders works only if the SRP Batcher is off or the shader is incompatible, so the SRP Batcher takes priority for MeshRenderers. Scripted `RenderMeshInstanced` draws are instanced draws of the `INSTANCING_ON` variant. URP Lit, which is SRP-Batcher-compatible, is the docs' own example material for it. **So one SRP-Batcher-compatible ThreadFlat shader with `multi_compile_instancing` serves both paths.** This is an inference from docs plus source; verify it once with the Frame Debugger ("Draw Mesh (instanced)").
- `GraphicsBuffer.IndirectDrawIndexedArgs.size = 20` and `IndirectDrawArgs.size = 16` (IL). The shader side is `Editor\Data\Resources\CGIncludes\UnityIndirect.cginc:45-86` (`UNITY_INDIRECT_DRAW_ARGS`, `InitIndirectDrawArgs`, `GetIndirectInstanceID`).

### 1.3 GPU Resident Drawer (GRD)
- URP requires every renderer to use the cluster light loop, meaning Forward+ or Deferred+ (`URP\Runtime\Data\UniversalRenderPipelineAsset.cs:1759-1788`).
- SRP Core requires all of the following (`CORE\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs:30-70`):
  - `BatchRendererGroup.BufferTarget == RawBuffer`, so no GLES/constant-buffer platforms
  - the `OccluderDepthDownscale` kernel present
  - **Editor `batchRendererGroupShaderStrippingMode == KeepAll`**
  - mode != Disabled (`:78`)
- URP passes `allowInEditMode = true` (`UniversalRenderPipelineAsset.cs:609`), so GRD also runs in Edit mode.
- Renderers whose shader lacks `DOTS_INSTANCING_ON`, and renderers with a custom `MaterialPropertyBlock`, are excluded and drawn the normal way (`CORE\Runtime\GPUDriven\Debug\GRDProfilerCounters.cs:71,77`; `SceneProcessors\WorldProcessor.cs:199-219`).
- The manual adds that it works only for MeshRenderers, needs compute support, helps most with many objects sharing a mesh, and makes **build times longer** because all BRG variants are compiled.
- Current project state:
  - `PC_RPAsset.asset:85 m_GPUResidentDrawerMode: 1` (InstancedDrawing; enum in `CORE\Runtime\GPUDriven\IGPUResidentRenderPipeline.cs:9-20`), `:72 m_UseSRPBatcher: 1`
  - `PC_Renderer.asset:56 m_RenderingMode: 2` (ForwardPlus; `URP\Runtime\UniversalRenderer.cs:17`)
  - `ProjectSettings\GraphicsSettings.asset:49 m_BrgStripping: 2` (KeepAll, from the IL enum KeepIfEntitiesGraphics=0, StripAll=1, KeepAll=2)
  
  **So GRD is live right now.**
- The active RP comes from the quality level: `QualitySettings.asset:105` (PC) has `customRenderPipeline`, while `GraphicsSettings.asset:40 m_CustomRenderPipeline: {fileID: 0}`. Use `QualitySettings.GetRenderPipelineAssetAt(i)` (XML:33658) or `GraphicsSettings.currentRenderPipeline`, not `defaultRenderPipeline`.

### 1.4 Burst / Mathematics / Collections / Jobs
- `Library\PackageCache\com.unity.burst@626e66ab421b\package.json` declares `"type": "shim", "version": "2.0.0"`. Its only code is `Runtime\TypeForwarders.cs`, which forwards `BurstCompileAttribute`, `BurstCompiler`, `FunctionPointer<>`, `SharedStatic<>` and the intrinsics to the engine. The engine ships `UnityEngine.BurstModule.dll` (`T:Unity.Burst.BurstCompileAttribute` at `UnityEngine.BurstModule.xml:7`), `UnityEngine.MathematicsModule.dll` (`T:Unity.Mathematics.float3` at `UnityEngine.MathematicsModule.xml:17792`) and the AOT compiler `Editor\Data\Tools\Burst\Client\bcl\bcl.exe`.
- `Packages\packages-lock.json` has `com.unity.burst` 2.0.0 and `com.unity.collections` 6.6.0, both builtin at depth 2 via render-pipelines.core. `Library\ScriptAssemblies\Unity.Burst.dll` and `Unity.Collections.dll` are already compiled. `Unity.Collections.asmdef` has `autoReferenced: true`.
- **Conclusion:** `using Unity.Burst; using Unity.Mathematics; using Unity.Collections; using Unity.Jobs;` compile in Assembly-CSharp with no manifest edit. Jobs live in CoreModule. Collections' availability depends on URP staying installed, which it will.

### 1.5 Project settings that matter for meshes
- `EditorSettings.asset:7 m_SerializationMode: 2` is Force Text, so a `.asset` mesh is YAML with hex vertex data (~2x binary size, slow import, noisy diffs).
- `EditorSettings.asset:29-30` enables Enter Play Mode Options = 3 (no domain reload, no scene reload). **Static mesh caches survive between Play sessions**, so reset them with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.

## 2. Architecture recommendation

| Content | Path | Notes |
|---|---|---|
| Cable LOD0 (y -300..600, 264 rings x 432 = 114 048 v, 227 232 tris) | static MeshRenderers, 8 chunks of ~33 bands (34 rings x 432 = 14 688 v, so **UInt16 per chunk**) | Boundary ring duplicated in both chunks, computed from the same global ring index, so it is bit-identical with no T-junctions. |
| Cable LOD1 (108 v/ring), LOD2 (64-gon) | static MeshRenderers + zipper stitch bands (3.4) | 432/108 = 4:1 and 108->64 are both handled by the generic zipper. |
| Walkway, rails, pier, terrace, stairs, colossi, beads | static MeshRenderers, chunked ~10-20 k verts for culling/shadows | Analytic cylindrical frame for helix sweeps (3.5). |
| Rail posts 470, window dots ≤1296, bollards 28 | `RenderMeshInstanced`, chunks ≤ 500 (posts ~64/chunk for culling), explicit `worldBounds`, `shadowCastingMode = On`, `receiveShadows = true` | Sort instances by path s / bead before chunking. |
| >10 k instances (not in spec) | `RenderMeshIndirect` | Requires explicit `worldBounds` and a StructuredBuffer shader. |

- **Ring spacing must divide each band exactly.** [-300, 30] is 330 m, which is not a multiple of 4. Use `n = round(len / spacing)` and `spacing = len / n` (83 x 3.976 m), then 75 x 2 m and 105 x 4 m, for 263 bands.
- **Runtime vs `.asset`.** The cable LOD0 is ~4.1 MB of vertices (36 B/vertex) plus 2.7 MB of UInt32 indices (or 1.4 MB with UInt16 chunks). As Force-Text YAML it would be ~14 MB, re-imported on every change; in the player it would be a binary ~6.8 MB. Runtime generation costs ~25 ms (114 k vertices x ~0.2 µs) plus upload; all meshes together should come to < 0.3 s. Log the time with `System.Diagnostics.Stopwatch` and spread the builders over frames during the black fade if needed. One source of truth (the generator) also prevents tier/crossover drift.
- **No `StaticBatchingUtility`, no static batching flags, no LODGroup.** The cable LODs are spatial bands. Mesh LOD (`lodCount`, `RenderParams.forceMeshLod`) exists but is unnecessary.
- After validation, call `mesh.UploadMeshData(true)` to free the CPU copy. Skip this for any mesh used by a `MeshCollider` or read later (validators, captures).

## 3. Snippets (compile-ready for 6000.6 / C# 9)

### 3.1 Vertex format + main-thread writer (advanced API, zero per-vertex GC)
```csharp
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct ThreadVertex
{
    public Vector3 position;   // 12 B  Position Float32x3
    public Vector3 normal;     // 12 B  Normal   Float32x3
    public Color32 color;      //  4 B  Color    UNorm8x4 (R = AO 0.55..1, G = height gradient)
    public Vector2 uv;         //  8 B  TexCoord0 Float32x2
    public const int Stride = 36;
}

// Kept OUT of the vertex struct: a managed static array would give ThreadVertex a static ctor,
// which Burst refuses to touch inside jobs.
public static class ThreadVertexLayout
{
    public static readonly VertexAttributeDescriptor[] Attributes =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position,  VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Normal,    VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Color,     VertexAttributeFormat.UNorm8,  4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
    };
}

public sealed class MeshWriter
{
    public readonly List<ThreadVertex> V;
    public readonly List<int> I;
    public MeshWriter(int vCap = 1024, int iCap = 4096) { V = new List<ThreadVertex>(vCap); I = new List<int>(iCap); }

    public int Add(Vector3 p, Vector3 n, Color32 c, Vector2 uv)
    { V.Add(new ThreadVertex { position = p, normal = n, color = c, uv = uv }); return V.Count - 1; }

    public void Tri(int a, int b, int c) { I.Add(a); I.Add(b); I.Add(c); }

    /// boundsMargin: per-side growth for shaders that displace vertices (else 0).
    public Mesh ToMesh(string name, float boundsMargin = 0f)
    {
        const MeshUpdateFlags fast = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;
        int vc = V.Count, ic = I.Count;
        var mesh = new Mesh { name = name };
        mesh.SetVertexBufferParams(vc, ThreadVertexLayout.Attributes);
        mesh.SetVertexBufferData(V, 0, 0, vc, 0, fast);
        if (vc <= 65534)
        {
            var idx16 = new ushort[ic];
            for (int k = 0; k < ic; k++) idx16[k] = (ushort)I[k];
            mesh.SetIndexBufferParams(ic, IndexFormat.UInt16);
            mesh.SetIndexBufferData(idx16, 0, 0, ic, fast);
        }
        else
        {
            mesh.SetIndexBufferParams(ic, IndexFormat.UInt32);
            mesh.SetIndexBufferData(I, 0, 0, ic, fast);
        }
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, ic, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices);
        mesh.RecalculateBounds();                      // exact AABB from vertex data
        if (boundsMargin > 0f) { var b = mesh.bounds; b.Expand(2f * boundsMargin); mesh.bounds = b; }
        Debug.Assert(mesh.GetVertexBufferStride(0) == ThreadVertex.Stride);
        return mesh;
    }
}
```
Set `mesh.bounds` **after** the last `SetSubMesh`/`RecalculateBounds`, because either call overwrites it. `DontValidateIndices` is safe only because section 3.6 validates in dev builds.

### 3.2 MeshData path with an optional Burst job (only if profiling demands it)
```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
struct CableRingsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> ringY;                 // one entry per ring
    [NativeDisableParallelForRestriction] public NativeArray<ThreadVertex> verts;
    public void Execute(int k)
    {
        for (int j = 0; j < CableSurface.Cols; j++)
            verts[k * CableSurface.Cols + j] = CableSurface.Vertex(j, ringY[k]);
    }
}

public static class CableMeshData
{
    public static Mesh Build(float[] ringYs, string name)
    {
        int rings = ringYs.Length, cols = CableSurface.Cols, vc = rings * cols, ic = (rings - 1) * cols * 6;
        var arr = Mesh.AllocateWritableMeshData(1);
        var md = arr[0];
        md.SetVertexBufferParams(vc, ThreadVertexLayout.Attributes);
        md.SetIndexBufferParams(ic, vc <= 65534 ? IndexFormat.UInt16 : IndexFormat.UInt32);
        using (var ys = new NativeArray<float>(ringYs, Allocator.TempJob))
        {
            var job = new CableRingsJob { ringY = ys, verts = md.GetVertexData<ThreadVertex>() };
            job.Schedule(rings, 4).Complete();
        }
        if (md.indexFormat == IndexFormat.UInt16) LatheIndices.Write(md.GetIndexData<ushort>(), rings, cols);
        else LatheIndices.Write(md.GetIndexData<uint>(), rings, cols);
        md.subMeshCount = 1;
        md.SetSubMesh(0, new SubMeshDescriptor(0, ic));           // Default flags -> bounds computed
        var mesh = new Mesh { name = name };
        Mesh.ApplyAndDisposeWritableMeshData(arr, mesh);           // disposes arr
        mesh.RecalculateBounds();
        return mesh;
    }
}
```
**Floating-point determinism:** Burst's float math may differ from Mono in the last ulp. If chunk A is built with Burst and chunk B without it, the shared boundary rings may not be bit-identical. Use **one** path for every chunk of a surface.

### 3.3 Seam-correct twisted-strand lathe (cable LOD0), analytic normals
The cable is built in **local space**, with the GameObject at the axis (70.028, 0, 400), so |x|,|z| ≤ 51 m. Columns follow the strands, so ridge crests and valleys are mesh edges (no zig-zag aliasing). The ring closes by **index wrap** (432 columns, 432 quads per ring, no duplicate seam vertices). If a `u` coordinate is ever needed, use 433 columns and **copy** column 0's position and normal into column 432. Never recompute it at θ+2π, because `cos(2π)` in float is not exactly 1 and produces a crack and a shading seam.
```csharp
using Unity.Collections;
using UnityEngine;

public static class CableSurface
{
    public const float Radius = 50f, Relief = 1.2f, Pitch = 600f;
    public const int Strands = 36, PerStrand = 12, Cols = Strands * PerStrand;   // 432
    // Left-hand (S) lay in Unity's frame: strand angle decreases with height -> "\" seen from outside.
    // Lean = atan(2*pi*R/Pitch) = 27.6 deg (matches spec).
    public static double Phase(double y) => -2.0 * System.Math.PI * y / Pitch;
    // Strand profile h(u), u in [0,1): rounded crest at 0.5, V valley at 0/1 with finite slope.
    public static double H(double u) { double d = 2.0 * u - 1.0; return Relief * (1.0 - d * d); }
    static double Frac(double x) => x - System.Math.Floor(x);
    static double Hp(double u) { const double e = 1e-4; return (H(Frac(u + e)) - H(Frac(u - e))) / (2 * e); } // dh/du, 0 at valleys
    public static ThreadVertex Vertex(int col, float yF)
    {
        double y = yF;
        double u = (double)(col % PerStrand) / PerStrand;              // constant per column
        double theta = Phase(y) + 2.0 * System.Math.PI * col / Cols;
        double rho = Radius - Relief + H(u);
        double k = Strands / (2.0 * System.Math.PI);                    // du/dtheta
        double rhoT = Hp(u) * k;                                        // d rho / d theta
        double rhoY = Hp(u) * Strands / Pitch;                          // d rho / d y  (= -rhoT * Phase')
        double c = System.Math.Cos(theta), s = System.Math.Sin(theta);
        var St = new Vector3((float)(rhoT * c - rho * s), 0f, (float)(rhoT * s + rho * c));
        var Sy = new Vector3((float)(rhoY * c), 1f, (float)(rhoY * s));
        Vector3 n = Vector3.Cross(Sy, St).normalized;                  // outward (checked: rhoT=rhoY=0 -> (c,0,s))
        byte ao = (byte)(255 * (0.55 + 0.45 * H(u) / Relief));          // valleys darker
        return new ThreadVertex { position = new Vector3((float)(rho * c), yF, (float)(rho * s)),
                                  normal = n, color = new Color32(ao, 0, 0, 255), uv = new Vector2((float)u, yF) };
    }
    /// Exact ring heights: bands [(-300,30,4),(30,180,2),(180,600,4)] -> 264 rings.
    public static float[] RingYs()
    {
        var list = new System.Collections.Generic.List<float>();
        (double a, double b, double sp)[] bands = { (-300, 30, 4), (30, 180, 2), (180, 600, 4) };
        foreach (var (a, b, sp) in bands)
        {
            int n = (int)System.Math.Round((b - a) / sp);
            for (int i = (list.Count == 0 ? 0 : 1); i <= n; i++) list.Add((float)(a + (b - a) * i / n));
        }
        return list.ToArray();
    }
}

public static class LatheIndices
{
    // Rings bottom->top, theta increasing from +X toward +Z: (i00,i10,i01),(i10,i11,i01) face outward
    // (cross(b-a,c-a) points along +radial; Unity front face = clockwise = that normal).
    public static void Write(NativeArray<uint> ib, int rings, int cols)
    { int t = 0; for (int k = 0; k < rings - 1; k++) for (int j = 0; j < cols; j++) {
        uint i00 = (uint)(k * cols + j), i01 = (uint)(k * cols + (j + 1) % cols);
        uint i10 = i00 + (uint)cols, i11 = i01 + (uint)cols;
        ib[t++] = i00; ib[t++] = i10; ib[t++] = i01; ib[t++] = i10; ib[t++] = i11; ib[t++] = i01; } }
    public static void Write(NativeArray<ushort> ib, int rings, int cols)
    { int t = 0; for (int k = 0; k < rings - 1; k++) for (int j = 0; j < cols; j++) {
        int i00 = k * cols + j, i01 = k * cols + (j + 1) % cols, i10 = i00 + cols, i11 = i01 + cols;
        ib[t++] = (ushort)i00; ib[t++] = (ushort)i10; ib[t++] = (ushort)i01;
        ib[t++] = (ushort)i10; ib[t++] = (ushort)i11; ib[t++] = (ushort)i01; } }
}
```
For chunking, pass `RingYs()[k0..k1]` inclusive to each chunk. The shared ring is computed from the same float y, so it is identical. Normals come from the analytic partial derivatives, never `RecalculateNormals`, which would split at chunk borders and seams. The valley normal is the symmetric average, so shading is smooth. For a crisp crease, duplicate the valley column with the two one-sided normals.

### 3.4 Zipper stitch between rings of different counts (LOD0 432 -> LOD1 108, LOD1 108 -> LOD2 64)
Ring A is lower and ring B is upper (they are at different y). Each ring's vertex indices come with **unwrapped increasing angles** that start at aligned vertices: rotate B's start to the vertex nearest A[0]. The stitch emits `na + nb` triangles, is watertight because every edge is shared, and keeps the same outward winding as 3.3. A's vertices must be bit-identical copies of the LOD0 last ring.
```csharp
public static class RingStitch
{
    public static void Zipper(MeshWriter w, int[] a, float[] angA, int[] b, float[] angB)
    {
        int na = a.Length, nb = b.Length, i = 0, j = 0;
        const float TwoPi = 2f * Mathf.PI;
        float A(int k) => k < na ? angA[k] : angA[0] + TwoPi;      // sentinel closes the loop
        float B(int k) => k < nb ? angB[k] : angB[0] + TwoPi;
        while (i < na || j < nb)
        {
            bool advanceA = j >= nb || (i < na && A(i + 1) <= B(j + 1));
            if (advanceA) { w.Tri(a[i % na], b[j % nb], a[(i + 1) % na]); i++; }
            else          { w.Tri(a[i % na], b[j % nb], b[(j + 1) % nb]); j++; }
        }
    }
}
```

### 3.5 Frames for sweeps
**Helix walkway: analytic cylindrical frame (exact, no drift).** On a constant-radius helix the tangent is perpendicular to the horizontal radial direction, so `{t, radial, up}` is orthonormal. The deck stays level across its width and tilts only with the grade along `t` (spec: "flat in the radial direction").
```csharp
public struct SweepFrame { public Vector3 p, t, lateral, up; }

public static class Frames
{
    public static SweepFrame Cylindrical(Vector3 p, Vector3 tangent, Vector3 axisPoint)
    {
        Vector3 radial = new Vector3(p.x - axisPoint.x, 0f, p.z - axisPoint.z).normalized;
        Vector3 t = tangent - Vector3.Dot(tangent, radial) * radial;   // exact 0 radial part on a helix
        t.Normalize();
        Vector3 up = Vector3.Cross(t, radial).normalized;
        if (up.y < 0f) up = -up;                                       // travel-direction independent
        return new SweepFrame { p = p, t = t, lateral = radial, up = up };
    }

    /// Wang et al. 2008 double reflection RMF. x: samples, t: unit tangents, r0: initial reference.
    /// Output r[i] (unit, perpendicular to t[i]); binormal s = Cross(t, r). For closed curves
    /// (x[n-1] == x[0]) the holonomy angle is spread linearly so the frame closes.
    public static void DoubleReflection(Vector3[] x, Vector3[] t, Vector3 r0, Vector3[] r, bool closed)
    {
        int n = x.Length;
        r[0] = (r0 - Vector3.Dot(r0, t[0]) * t[0]).normalized;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 v1 = x[i + 1] - x[i];
            float c1 = Vector3.Dot(v1, v1);
            Vector3 rL = r[i], tL = t[i];
            if (c1 > 1e-12f)
            {
                rL = r[i] - (2f / c1) * Vector3.Dot(v1, r[i]) * v1;   // reflect by plane perpendicular to v1
                tL = t[i] - (2f / c1) * Vector3.Dot(v1, t[i]) * v1;
            }
            Vector3 v2 = t[i + 1] - tL;
            float c2 = Vector3.Dot(v2, v2);
            Vector3 ri = c2 > 1e-12f ? rL - (2f / c2) * Vector3.Dot(v2, rL) * v2 : rL; // second reflection
            r[i + 1] = (ri - Vector3.Dot(ri, t[i + 1]) * t[i + 1]).normalized;          // kill float drift
        }
        if (closed && n > 2)
        {
            float total = Vector3.SignedAngle(r[n - 1], r[0], t[n - 1]);
            for (int i = 1; i < n; i++) r[i] = Quaternion.AngleAxis(total * i / (n - 1), t[i]) * r[i];
        }
    }
}
```
Why RMF is wrong for the deck: an RMF rotates relative to the Frenet frame at rate -τ. For R = 70.028 m and rise ≈ 110 m per turn, c = 17.5 m/rad, τ = c/(R²+c²) = 0.00336 /m and s = 2π·√(R²+c²) = 453 m. The total roll is τ·s = **1.52 rad ≈ 87°**. Use RMF only for round handrail tubes (it avoids UV/facet twist) or for free curves. Sample ≥ 1 point per 0.5-1 m, and use exact analytic tangents when available.

### 3.6 Mesh validator (run in Editor/dev builds before `UploadMeshData(true)`)
```csharp
using System.Collections.Generic;
using UnityEngine;

public static class MeshValidator
{
    public struct Report
    {
        public int badIndex, degenerate, nonFinite, badNormals, windingFlips, notOutward, openEdges, nonManifold;
        public bool boundsOk;
        public bool Ok(int expectedOpenEdges) => badIndex == 0 && degenerate == 0 && nonFinite == 0 && badNormals == 0
            && windingFlips == 0 && notOutward == 0 && nonManifold == 0 && openEdges == expectedOpenEdges && boundsOk;
        public override string ToString() =>
            $"idx {badIndex} degen {degenerate} nan {nonFinite} nrm {badNormals} wind {windingFlips} out {notOutward} open {openEdges} nonManifold {nonManifold} bounds {boundsOk}";
    }

    /// isOutward(p, n): e.g. lathe -> Vector2.Dot(new Vector2(p.x, p.z), new Vector2(n.x, n.z)) > 0.
    /// Open edges are counted on a position-welded graph (1e-4 m), so UV seams don't count and real cracks do.
    public static Report Validate(Mesh m, System.Func<Vector3, Vector3, bool> isOutward = null, float minArea = 1e-6f)
    {
        var r = new Report { boundsOk = true };
        Vector3[] v = m.vertices, n = m.normals;           // mesh must still be readable
        bool hasN = n != null && n.Length == v.Length;
        Bounds b = m.bounds; b.Expand(2e-3f);
        var weld = new Dictionary<Vector3Int, int>(v.Length);
        var canon = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = v[i];
            if (!(Fin(p.x) && Fin(p.y) && Fin(p.z))) r.nonFinite++;
            if (!b.Contains(p)) r.boundsOk = false;
            var key = new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));
            if (!weld.TryGetValue(key, out canon[i])) { canon[i] = i; weld.Add(key, i); }
            if (hasN)
            {
                float len = n[i].magnitude;
                if (!(Mathf.Abs(len - 1f) < 1e-3f)) r.badNormals++;
                else if (isOutward != null && !isOutward(p, n[i])) r.notOutward++;
            }
        }
        var edges = new Dictionary<long, int>();
        for (int s = 0; s < m.subMeshCount; s++)
        {
            if (m.GetTopology(s) != MeshTopology.Triangles) continue;
            int[] idx = m.GetIndices(s);
            for (int k = 0; k + 2 < idx.Length; k += 3)
            {
                int a = idx[k], bb = idx[k + 1], c = idx[k + 2];
                if ((uint)a >= v.Length || (uint)bb >= v.Length || (uint)c >= v.Length) { r.badIndex++; continue; }
                Vector3 fn = Vector3.Cross(v[bb] - v[a], v[c] - v[a]);      // Unity front-face normal
                if (fn.sqrMagnitude < 4f * minArea * minArea) { r.degenerate++; continue; }
                if (hasN && Vector3.Dot(fn, n[a] + n[bb] + n[c]) <= 0f) r.windingFlips++;
                AddEdge(edges, canon[a], canon[bb]); AddEdge(edges, canon[bb], canon[c]); AddEdge(edges, canon[c], canon[a]);
            }
        }
        foreach (var kv in edges) { if (kv.Value == 1) r.openEdges++; else if (kv.Value > 2) r.nonManifold++; }
        return r;
    }

    static bool Fin(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    static void AddEdge(Dictionary<long, int> e, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        e.TryGetValue(key, out int c); e[key] = c + 1;
    }
}
```
Expected open edges: an open lathe chunk has 2·432 = 864. A closed slab has 0. In a test, also assert the vertex and triangle counts against the spec numbers (LOD0: 114 048 v / 227 232 tris across the chunks, minus duplicated boundary rings).

### 3.7 Instanced props with chunking (`RenderMeshInstanced`)
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class InstancedSet : MonoBehaviour
{
    public const int MaxPerCall = 500;   // < 511 (2 matrices/instance); docs cap 1023 only with assumeuniformscaling
    Mesh _mesh; Matrix4x4[] _m = System.Array.Empty<Matrix4x4>();
    RenderParams[] _rp = System.Array.Empty<RenderParams>(); int[] _start, _count;

    /// matrices must be spatially sorted (e.g. by path s) so chunk bounds are tight.
    public void Init(Mesh mesh, Material mat, IReadOnlyList<Matrix4x4> matrices, int layer, int chunkSize = MaxPerCall)
    {
        _mesh = mesh;
        if (!mat.enableInstancing) mat.enableInstancing = true;       // RenderMeshInstanced throws otherwise
        _m = new Matrix4x4[matrices.Count];
        for (int i = 0; i < _m.Length; i++) _m[i] = matrices[i];
        chunkSize = Mathf.Clamp(chunkSize, 1, MaxPerCall);
        int chunks = (_m.Length + chunkSize - 1) / chunkSize;
        _rp = new RenderParams[chunks]; _start = new int[chunks]; _count = new int[chunks];
        for (int c = 0; c < chunks; c++)
        {
            _start[c] = c * chunkSize; _count[c] = Mathf.Min(chunkSize, _m.Length - _start[c]);
            _rp[c] = new RenderParams(mat)
            {
                layer = layer, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
                worldBounds = ChunkBounds(mesh.bounds, _m, _start[c], _count[c]),
            };
        }
    }

    void Update()   // queues for this frame; all cameras (camera = null). Use [ExecuteAlways] for edit-mode captures.
    {
        for (int c = 0; c < _rp.Length; c++)
            Graphics.RenderMeshInstanced(in _rp[c], _mesh, 0, _m, _count[c], _start[c]);
    }

    static Bounds ChunkBounds(Bounds local, Matrix4x4[] m, int start, int count)
    {
        Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
        for (int i = start; i < start + count; i++)
            for (int k = 0; k < 8; k++)
            {
                var corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                Vector3 w = m[i].MultiplyPoint3x4(corner); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w);
            }
        var b = new Bounds(); b.SetMinMax(mn, mx); return b;
    }
}
```

### 3.8 ThreadFlat HLSL: instancing-compatible and SRP-Batcher-compatible (per pass)
```hlsl
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing                 // in EVERY pass: UniversalForward, ShadowCaster, DepthOnly, DepthNormals
// no DOTS.hlsl / DOTS_INSTANCING_ON (GRD off) -> half the variants
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)                   // identical layout in all passes (SRP Batcher rule)
    half4 _BaseColor; half4 _ShadeColor; float _Emission;
CBUFFER_END
struct Attributes { float3 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
Varyings vert(Attributes v)
{
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v);                    // selects unity_ObjectToWorldArray[instanceID]
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    o.positionWS = TransformObjectToWorld(v.positionOS);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
    o.color      = v.color;
    return o;
}
```
Without `multi_compile_instancing`, no `INSTANCING_ON` variant exists, and every instance would be drawn with the single `unity_ObjectToWorld`, all at one place.

### 3.9 RenderMeshIndirect (for reference; not needed at spec counts)
```csharp
var args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
var cmd = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
cmd[0].indexCountPerInstance = mesh.GetIndexCount(0); cmd[0].instanceCount = (uint)count;
cmd[0].startIndex = mesh.GetIndexStart(0); cmd[0].baseVertexIndex = mesh.GetBaseVertex(0);
args.SetData(cmd);
var rp = new RenderParams(mat) { worldBounds = explicitWorldBounds, matProps = new MaterialPropertyBlock() };
rp.matProps.SetBuffer("_InstanceMatrices", matrixBuffer);      // StructuredBuffer<float4x4>
Graphics.RenderMeshIndirect(rp, mesh, args);                    // each frame; Release() buffers OnDestroy
```
On the shader side: `#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs`, `#include "UnityIndirect.cginc"`, then in the vertex shader `InitIndirectDrawArgs(0); uint id = GetIndirectInstanceID(svInstanceID);` and build the world position from `_InstanceMatrices[id]`. Bounds are never auto-computed here.

### 3.10 Editor utilities: GRD off, optional bake for inspection
```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ThreadMeshEditorTools
{
    [MenuItem("Thread/Setup/Disable GPU Resident Drawer")]
    public static void DisableGrd()
    {
        for (int i = 0; i < QualitySettings.count; i++)
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset a
                && a.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled)
            { a.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled; EditorUtility.SetDirty(a); }
        AssetDatabase.SaveAssets();
    }

    /// Inspection only (Force Text makes these large). Keeps the GUID stable when the asset exists.
    public static void SaveMeshAsset(Mesh generated, string path /* "Assets/Generated/Meshes/X.asset" */)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(generated, existing); EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(generated, path);          // parent folders must exist (AssetDatabase.CreateFolder)
        AssetDatabase.SaveAssets();
    }
}
#endif
```
`CreateAsset` overwrites an existing asset and requires a native extension (`.asset`). Never call it during import (CreateAsset docs). Put the editor code in an `Editor` folder or an editor-only asmdef so the player build compiles.

## 4. Pitfalls (each one has caused real bugs)
1. **Wrong API name.** `Mesh.AcquireWritableMeshData` does not exist; use `AllocateWritableMeshData`.
2. **Instancing not enabled.** `RenderMeshInstanced` throws unless `material.enableInstancing` is true, and the shader also needs `multi_compile_instancing`. Set and assert both for runtime-created materials.
3. **Silent instance cap.** There is no managed count check: >511 instances per call (or >1023 with `assumeuniformscaling`) is a native limit. Always chunk (≤ 500).
4. **RenderParams defaults.** No shadows, no shadow receiving, layer 0. Set `shadowCastingMode`, `receiveShadows` and `layer` explicitly, otherwise posts and bollards will lack shadows, unlike their MeshRenderer neighbours.
5. **Draw timing.** `RenderMeshInstanced` draws only in frames where it is called (Update), and not in Edit mode without `[ExecuteAlways]`. It draws in every camera unless `camera` is set; `layer` obeys camera culling masks.
6. **Bounds overwrites.** Set `mesh.bounds` after the last `SetSubMesh`/`RecalculateBounds`. With `DontRecalculateBounds` you must set `SubMeshDescriptor.bounds` and `mesh.bounds` yourself, or the mesh is culled. Shaders that displace vertices need a margin.
7. **Index format resets.** Changing `indexFormat` clears the indices and sets subMeshCount to 1, so set it first. UInt16 meshes should stay ≤ 65 534 vertices (0xFFFF may be skipped).
8. **Vertex layout.** Attribute order must follow the `VertexAttribute` enum. Each attribute must be a multiple of 4 bytes. The struct layout must match exactly: check `GetVertexBufferStride(0) == 36`.
9. **Seams from normals.** `RecalculateNormals` splits normals at duplicated seam and chunk-boundary vertices, so use analytic normals. Seam duplicates must be **copies**, not recomputed at θ+2π.
10. **RMF on the helix deck.** It rolls ~87° per turn on this helix; use the cylindrical frame.
11. **Ring spacing.** It must divide the band length exactly (the [-300, 30] band at 4 m does not), or the last band is a sliver or overlaps.
12. **LOD cracks.** LOD transitions with different ring counts crack (T-junctions) unless stitched with a zipper band whose ring-A vertices are bit-identical to the LOD0 boundary ring.
13. **Mixed generation paths.** Mixing Burst and Mono for neighbouring chunks of one surface can break bit-identity at shared rings.
14. **Leaks and stale statics.** Runtime `Mesh` objects must be destroyed (`Destroy(mesh)` in `OnDestroy`). With Enter Play Mode Options (no domain reload) static caches persist, so reset them in `SubsystemRegistration`. Use `MeshFilter.sharedMesh`, not `.mesh`, which clones.
15. **Unreadable meshes.** `UploadMeshData(true)` makes the mesh unreadable: run the validators first, and never do this for MeshCollider meshes.
16. **MaterialPropertyBlock.** A MaterialPropertyBlock on a MeshRenderer drops it out of SRP Batcher batches and excludes it from GRD. Use per-colour materials or vertex colour.
17. **GRD is on in the template.** It costs build time (BRG KeepAll variants) and also runs in Edit mode, with no benefit for ThreadFlat. Disable it on every quality level's URP asset.
18. **Wrong RP accessor.** `GraphicsSettings.defaultRenderPipeline` is null in this project; the RP lives on the quality level.
19. **Force Text bloat.** It turns `.asset` meshes into huge YAML. Bake only for inspection, keep the GUID via `CopySerialized`, and never put generated meshes under version-sensitive paths without need.
20. **Spatial chunking.** Instanced chunks must be spatially coherent; unsorted chunks have world-spanning bounds and are never culled. The same applies to shadow casting.
21. **Precision at distance.** Far LOD2 rings at |y| ≤ 20 km are fine in float (~2 mm), but compute in double and convert at the end, and generate in the cable's local space.
22. **Mesh LOD.** `lodCount`/`forceMeshLod` are spatially irrelevant here; leave `forceMeshLod = -1`.

## 5. Sources (accessed 2026-10-29)
- Unity Scripting API, Graphics.RenderMeshInstanced (6000.3): https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/graphics/rendermeshinstanced. Used for the 1023/511 limit, the enableInstancing exception, worldBounds auto-computation and the instance-struct fields.
- Unity Scripting API, Mesh.AllocateWritableMeshData: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/allocatewritablemeshdata. Used for disposal semantics and the official sample (RecalculateNormals/Bounds after apply).
- Unity Scripting API, Mesh.SetVertexBufferParams: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/setvertexbufferparams. Used for minimal validation and the maxGraphicsBufferSize exception.
- Unity Scripting API, VertexAttributeDescriptor: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendering/vertexattributedescriptor. Used for attribute order, the 4-byte rule, 4 streams and the recommended streams.
- Unity Scripting API, Mesh.indexFormat: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/mesh/indexformat. Used for the 16/32-bit rule, the clear-on-change behaviour and the 0xFFFF caveat.
- Unity Scripting API, AssetDatabase.CreateAsset: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/assetdatabase/createasset. Used for overwrite behaviour, the extension requirement and the no-import-time rule.
- Unity Manual, GPU instancing (6000.3): https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/gpuinstancing/gpuinstancing. Used for SRP Batcher vs instancing for GameObjects and RenderMeshInstanced support.
- Unity Manual, GPU Resident Drawer (URP, 6000.0): https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer. Used for requirements, Keep All and longer build times.
- UnityCsReference, Graphics.cs (GitHub master): https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Runtime/Export/Graphics/Graphics.cs. Cross-check of the RenderMeshInstanced checks and RenderParams defaults; differences vs 6000.6 IL are noted (rendererShaderUserValue).
- Unity-Technologies/MeshApiExamples: https://github.com/Unity-Technologies/MeshApiExamples. Used for C# vs Burst vs compute generation timings (300 k-tri noise ball: 2.7 s C#, 22 ms multithreaded Burst).
- W. Wang, B. Jüttler, D. Zheng, Y. Liu, "Computation of Rotation Minimizing Frames", ACM TOG 27(1), 2008, doi:10.1145/1330511.1330513; listing https://www.microsoft.com/en-us/research/?p=344486 and https://hub.hku.hk/handle/10722/152386. Used for the double reflection method (section 3.5).
- Unity Discussions, "GPU instancing count limit": https://forum.unity.com/threads/gpu-instancing-count-limit.591397/. Used as corroboration that callers must chunk; no automatic splitting is documented.
