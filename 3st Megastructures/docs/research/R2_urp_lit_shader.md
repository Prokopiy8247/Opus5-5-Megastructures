# R2: Handwritten HLSL shaders for URP 17.6 (Unity 6000.6.0f1)

Researcher R2, written 2026-10-29. Every claim below was checked against the installed package source:
- URP: `Library/PackageCache/com.unity.render-pipelines.universal@8457e85b8184` (package.json says `17.6.0`)
- Core: `Library/PackageCache/com.unity.render-pipelines.core@2d66c71e606e`

Paths below are relative to those folders. `URP/` means the URP package and `CORE/` means the Core package.

## 0. Decisions in brief (read this first)

| Topic | Decision |
|---|---|
| Forward+ keyword | Use **`_CLUSTER_LIGHT_LOOP`**. `_FORWARD_PLUS` is deprecated: it still works, but it emits a `#warning`. **Declare it even with only one directional light** (see 1.3, which is a real bug). |
| Main-light shadows | Copy Lit's exact lines: `multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN` and `multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`. URP stripping removes the unused variants by keyword name. |
| Shadow lookup | Compute the shadow coordinate per pixel the way Lit does (`REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR` / `MAIN_LIGHT_CALCULATE_SHADOWS`). Call **`GetMainLight(shadowCoord, positionWS, half4(1,1,1,1))`**: unlike `GetMainLight(shadowCoord)`, it applies the shadow-distance fade, so there is no hard cutoff line. |
| Passes per opaque shader | `ForwardLit` (UniversalForward), `ShadowCaster`, `DepthOnly`, `DepthNormals`. All four are mandatory. Without DepthNormals or DepthOnly, an object disappears from `_CameraDepthTexture` and from SSAO in this project (see 3.2). |
| Material data | Declare one `CBUFFER_START(UnityPerMaterial)` in a SubShader-level `HLSLINCLUDE`, identical for every pass. Keep globals outside it and **out of the `Properties` block**. |
| Instancing | Add `#pragma multi_compile_instancing` and `#include_with_pragmas ".../DOTS.hlsl"` to every pass. The project has GPU Resident Drawer on (`PC_RPAsset.asset:85 m_GPUResidentDrawerMode: 1`) and BRG variants are kept (`GraphicsSettings.asset:49 m_BrgStripping: 2` = `KeepAll`, verified with Cecil on `UnityEditor.CoreModule.dll`). |
| Fog | Do not use Unity fog: no `Fog.hlsl` include and no `multi_compile_fog`. Fog comes from our own global uniforms instead. |
| Skybox | Build the ray from the **object-space position of the sky mesh**, as `mul((float3x3)M, positionOS)`, and normalize it per pixel. Use `Cull Off ZWrite Off`, keep the default ZTest, and draw sun and planets analytically with `fwidth` anti-aliasing. |
| Soft depth fade | Depth texture is ON (`PC_RPAsset.asset:22`) and the renderer's Depth Texture Mode is **After Opaques** (`PC_Renderer.asset:58 m_CopyDepthMode: 0`). Keep both. |
| Variants in build | Keep serialized `Shader` references in the boot scene, and also add the shaders to Always Included. Use **no `shader_feature`** on runtime-created materials. |

Project facts (settings files): PC renderer is **Forward+** (`PC_Renderer.asset:56 m_RenderingMode: 2`; enum `URP/Runtime/UniversalRenderer.cs:11-23`: Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3); depth priming Disabled (`:57`); MSAA off (`PC_RPAsset.asset:28 m_MSAA: 1`); 4 cascades (`:58`), soft shadows on (`:66`); SSAO feature active (`PC_Renderer.asset:71-73`); Linear colour space (`ProjectSettings.asset:49`); Windows APIs D3D11 then D3D12 (`ProjectSettings.asset:543`); Mobile quality level excluded for Standalone (`QualitySettings.asset:61-62`), so a Windows build sees only `PC_RPAsset`.

---

## 1. Verified facts

### 1.1 (a) Lit ForwardLit keyword lines in 17.6 (`URP/Shaders/Lit.shader`)

- 144: `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN`
- 152: `#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH`
- 158: `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`. 157: `#pragma multi_compile _ _LIGHT_LAYERS` and 153: `multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION` (optional, Pitfall 9). We omit 145 `multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS` and 147 `multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS`.
- 164/179: `#include_with_pragmas` of `RenderingLayers.hlsl` / `Fog.hlsl`. 184-186: `#pragma multi_compile_instancing`, `#pragma instancing_options renderinglayer`, `#include_with_pragmas ".../ShaderLibrary/DOTS.hlsl"`. 108-111 render state: `Blend[_SrcBlend][_DstBlend]...`, `ZWrite[_ZWrite]`, `Cull[_Cull]`, `AlphaToMask[_AlphaToMask]`.
- ShadowCaster 203-206: `ZWrite On / ZTest LEqual / ColorMask 0 / Cull[_Cull]`; 234: `#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW`. DepthOnly 344-345: `ZWrite On / ColorMask R`. DepthNormals 417-419: RenderingLayers include, `multi_compile _ _WRITE_SMOOTHNESS`, `multi_compile_fragment _ _GBUFFER_NORMALS_OCT`.
- New in 17.6 (not needed by us): `_REFLECTION_PROBE_ATLAS` (150), `_SCREEN_SPACE_REFLECTION` (151), `_SCREEN_SPACE_IRRADIANCE` (154).

- **Soft-shadow keyword on PC**: only `_SHADOWS_SOFT` is ever set on non-XR-mobile platforms; quality is read at runtime from `_MainLightShadowParams.y` (`URP/Runtime/ShadowUtils.cs:680-713`; `URP/ShaderLibrary/Shadows.hlsl:84,161,326-334`). `_LOW/_MEDIUM/_HIGH` are only used where `SupportsPerLightSoftShadowQuality()` is false (Quest/HoloLens, `ShadowUtils.cs:662-672`). Declare all to match Lit; the stripper removes unused ones (`URP/Editor/ShaderScriptableStripper.cs:553-564`).
- **Main-shadow keyword**: `_MAIN_LIGHT_SHADOWS` for 1 cascade, `_MAIN_LIGHT_SHADOWS_CASCADE` for more (`URP/Runtime/Passes/MainLightShadowCasterPass.cs:322-323`); `_MAIN_LIGHT_SHADOWS_SCREEN` only from the ScreenSpaceShadows feature (`URP/Runtime/RendererFeatures/ScreenSpaceShadows.cs:233-235`). Prefiltering `m_PrefilteringModeMainLightShadows: 3` (`PC_RPAsset.asset:102`) = `SelectMainLightAndCascades`, which **removes the OFF variant** (`URP/Runtime/Data/UniversalRenderPipelineAssetPrefiltering.cs:27-34,62-70`).

### 1.2 Forward+ keyword name and the light loop

- `URP/ShaderLibrary/Core.hlsl:8-22`: includes `ForwardPlusKeyword.deprecated.hlsl`; `#if defined(_CLUSTER_LIGHT_LOOP)` → `USE_CLUSTER_LIGHT_LOOP 1`, then `#define _ADDITIONAL_LIGHTS 1` and `#undef _ADDITIONAL_LIGHTS_VERTEX`.
- `URP/ShaderLibrary/ForwardPlusKeyword.deprecated.hlsl:13`: `#warning _FORWARD_PLUS shader keyword has been deprecated. Please update your shaders to use _CLUSTER_LIGHT_LOOP...`
- `URP/Runtime/ForwardLights.cs:578-579`: the runtime sets `_CLUSTER_LIGHT_LOOP`, and also sets `_FORWARD_PLUS` for backwards compatibility.
- `URP/ShaderLibrary/RealtimeLights.hlsl:34-47`: under the cluster loop, `LIGHT_LOOP_BEGIN(lightCount)` expands to
  `ClusterIterator _urp_internal_clusterIterator = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0); [loop] while (ClusterNext(...)) { lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT; ...`.
  This means **a local variable that is literally named `inputData`** (type `InputData`) must be in scope, with `positionWS` and `normalizedScreenSpaceUV` filled in. The official 6.6 manual says the same (Sources).
- `inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);` comes from `URP/Shaders/LitForwardPass.hlsl:117`. The function is defined at `URP/ShaderLibrary/ShaderVariablesFunctions.hlsl:574-584` and handles the `_ScaleBiasRt` flip.
- `GetAdditionalLightsCount()` returns 0 under the cluster loop (`RealtimeLights.hlsl:292-302`).
- Lit loops the non-main directional lights first with `for (lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS))` and then runs `LIGHT_LOOP_BEGIN` (`URP/ShaderLibrary/Lighting.hlsl:356-391`).
- **With one directional light we need neither loop.** The main light always arrives through `_MainLightPosition`/`_MainLightColor` (`URP/ShaderLibrary/Input.hlsl:117-121`).

### 1.3 Forward+ main-light bug (this is why the keyword is mandatory)

- `RealtimeLights.hlsl:100-119` `GetMainLight()`: with `USE_CLUSTER_LIGHT_LOOP`, `distanceAttenuation = 1.0`; otherwise `distanceAttenuation = unity_LightData.z`.
- `URP/Runtime/UniversalRenderPipeline.cs:2371-2376`: in Forward+, `PerObjectData.LightData` is **not requested**, so `unity_LightData` is not filled.
- Result: a shader **without** `_CLUSTER_LIGHT_LOOP` that multiplies by `distanceAttenuation` renders **black** under Forward+, often only in builds or at startup. Unity's issue tracker confirms this and lists it as "By Design" (Sources).
- Fix: declare `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`. In a build, `StripUnusedFeatures_ClusterLightLoop` (`ShaderScriptableStripper.cs:601-615`) then keeps only the F+ variant, so the cost is zero.

### 1.4 (b) Shadow functions (`URP/ShaderLibrary/Shadows.hlsl`)

- 13-25: `MAIN_LIGHT_CALCULATE_SHADOWS` is defined when any main-shadow keyword is set and `_RECEIVE_SHADOWS_OFF` is not. `REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR` is defined only for `_MAIN_LIGHT_SHADOWS` (no cascades), or for SCREEN on a non-transparent shader. **Cascades are always looked up per pixel.**
- 55: `#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR` (unconditional). 360-374: `TransformWorldToShadowCoord(positionWS)` (cascade: `ComputeCascadeIndex`; screen: NDC). 376-401: `MainLightRealtimeShadow(...)` (1 when shadows off). 443-450: `GetMainLightShadowFade`. 486-504: `MainLightShadow` (mixes in the fade). 533-540: `GetShadowCoord(VertexPositionInputs)`.
- 542-551: `ApplyShadowBias(posWS, nWS, lightDir)` uses `_ShadowBias.x` (depth) and `.y` (normal). 553-569: `ApplyShadowClamping` = pancaking for directional lights (`IsDirectionalLight()` = `_ShadowBias.z == 1`).
- `RealtimeLights.hlsl:121-139`: `GetMainLight(float4 shadowCoord)` = `MainLightRealtimeShadow` only, **no distance fade**; `GetMainLight(float4 shadowCoord, float3 positionWS, half4 shadowMask)` = `MainLightShadow` (with fade) + cookies.
- `URP/Shaders/ShadowCasterPass.hlsl:13-14` declares the globals `float3 _LightDirection; float3 _LightPosition;`. Lines 33-47 are `GetShadowPositionHClip`. URP sets the globals in `URP/Runtime/ShadowUtils.cs:492-503`.

### 1.5 (c) Skybox in URP 17.6

- `URP/Runtime/UniversalRendererRenderGraph.cs:1266-1279`: the skybox is drawn **after the opaques** (and the AfterRenderingOpaques/BeforeRenderingSkybox custom passes) and **before** the transparents (1296); only if `camera.clearFlags == CameraClearFlags.Skybox` (URP Background Type = Skybox) on a non-overlay camera. Material = the camera's `Skybox` component if present, else `RenderSettings.skybox`.
- `URP/Runtime/Passes/DrawSkyboxPass.cs:48,103-104`: `renderGraph.CreateSkyboxRendererList(camera)`. The color target is `activeColorTexture` and the **depth is bound ReadWrite**. So the skybox is depth-tested against the opaque depth. It writes into the camera color target, which is MSAA when MSAA is enabled, so the sky/geometry edges are resolved per sample.
- Unity's built-in skybox shaders use `Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }`, `Cull Off ZWrite Off`, the default ZTest, and `eyeRay = normalize(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz))` (Skybox-Procedural.shader, Sources).
- Under reversed Z, Unity flips `ZTest LEqual` to GEqual automatically (forum, Sources). The depth clear value is the far plane, 0.
- **Ray direction.** The sky mesh is centred on the camera, so the interpolated object-space position × 3x3 of M, normalized **in the fragment**, is the exact (perspective-correct, tessellation-independent) view ray. Do not reconstruct from screen UV + inverse VP: the engine also renders the skybox material into cubemaps (ambient/reflection capture) with its own matrices, where the object-space method stays valid. For sky geometry beyond the far plane, use Aras's trick: `z = 1e-9` (reversed) or `w - 1e-6`.
- `_CameraDepthTexture` is copied After Opaques here, so the sky is never in it. Sky pixels read raw depth 0, and `LinearEyeDepth(0, _ZBufferParams)` = far plane.

### 1.6 (d) Depth texture, depth fade, alpha-to-coverage

- `URP/ShaderLibrary/DeclareDepthTexture.hlsl:6-10,31-45`: `TEXTURE2D_X_FLOAT(_CameraDepthTexture)`, `_CameraDepthTexture_TexelSize`; `sampler_CameraDepthTexture` is now `#define`d to `sampler_PointClamp`; `SampleSceneDepth(uv)` does a **LOAD** (no filtering) at `uv * _ScreenSize.xy`; `LoadSceneDepth(uint2)` also exists.
- `CORE/ShaderLibrary/Common.hlsl:1206-1209`: `LinearEyeDepth(depth, _ZBufferParams)` = `1/(z*depth + w)`. Line 1227 has `LinearEyeDepth(positionWS, viewMatrix)`. The URP soft-particle reference implementation is `URP/ShaderLibrary/Particles.hlsl:84-120`.
- Requirements: URP asset → **Depth Texture** on (`m_RequireDepthTexture`, `URP/Runtime/Data/UniversalRenderPipelineAsset.cs:445,1046`) or per camera; and **Renderer → Depth Texture Mode must be `AfterOpaques`** (or `ForcePrepass`). The new-project default is `AfterTransparents` (`URP/Runtime/UniversalRendererData.cs:16-24,145,430-434`). With that default, `UniversalRenderer.cs:729-743` schedules the copy after the transparents, so transparent shaders read a stale texture.
- Where the depth texture comes from. If any pass needs normals (SSAO in DepthNormals mode), URP runs a **DepthNormals prepass that writes `_CameraDepthTexture` directly** (`UniversalRendererRenderGraph.cs:657-666,1584-1596`; `URP/Runtime/Passes/DepthNormalOnlyPass.cs:19` draws the LightMode tags `DepthNormals`/`DepthNormalsOnly`). Otherwise the depth comes from a copy, or from a `DepthOnly` prepass (`DepthOnlyPass.cs:20`).
- Depth priming (Auto/Forced, non-MSAA, desktop) draws the opaques with **ZTest Equal / ZWrite Off** after a DepthOnly prepass (`URP/Runtime/Passes/DrawObjectsPass.cs:219-223`, `UniversalRendererRenderGraph.cs:1605-1647`). It is Disabled here (`UniversalRendererData.cs:144` default, project `:57`).
- Alpha-to-coverage: `_AlphaToMaskAvailable` = 1 **only when `msaaSamples > 1` and the pass is opaque** (`DrawObjectsPass.cs:140-141`). URP's `AlphaClip`/`SharpenAlphaStrict` (`ShaderVariablesFunctions.hlsl:190-229`) export an `fwidth`-sharpened alpha when A2C is available, else `clip()`; `OutputAlpha` returns 1 for opaques unless A2C is available (250-265). The manual only says "Enables alpha-to-coverage mode"; without MSAA there is one coverage sample, so it is no substitute for `clip()`. **Additive/transparent shaders must not use AlphaToMask.**

### 1.7 (e) Reversed-Z and depth formats

- `CORE/ShaderLibrary/API/D3D11.hlsl:4-7` sets `UNITY_REVERSED_Z 1`, `UNITY_NEAR_CLIP_VALUE 1.0` and `UNITY_RAW_FAR_CLIP_VALUE 0.0`. Vulkan has the same (`Vulkan.hlsl:5-8`).
- `CORE/ShaderLibrary/Common.hlsl:224-225` selects `D3D11.hlsl` for `SHADER_API_D3D11`. There is no separate D3D12 branch, and the fallback at 240-241 is `#error unsupported shader api`, so D3D12 compiles must take the `SHADER_API_D3D11` path. That is an inference from the source, not a documented guarantee.
- The Unity manual table says DirectX/Metal/Vulkan depth runs from 1.0 at the near plane to 0.0 at the far plane.
- Default depth-stencil format: `CORE/Runtime/Utilities/CoreUtils.cs:1944-1951` returns **`GraphicsFormat.D32_SFloat_S8_UInt`** on desktop (D24 only on Android, Switch and embedded). `URP/Runtime/UniversalRenderer.cs:211-212` uses it for both the camera depth attachment and the depth texture (renderer `DepthFormat.Default`, `UniversalRendererData.cs:59-65,146-147`). The depth texture may instead be an R32 color target when copied (`UniversalRendererRenderGraph.cs:665`). Either way, `.r` holds raw device depth.
- `SystemInfo.usesReversedZBuffer` drives `_ZBufferParams` (`URP/Runtime/ScriptableRenderer.cs:281-287`) and the shadow matrices (`ShadowUtils.cs:621-631`). `_ScreenSize` is set at `ScriptableRenderer.cs:308`.
- **Float32 reversed-Z makes a large far plane (tens of km) safe for depth.** It does nothing for vertex precision: URP has **no camera-relative rendering** (`ShaderVariablesFunctions.hlsl:65-78` returns `_WorldSpaceCameraPos` as-is). Keep the player near the origin (floating origin) or the vertices will jitter.

### 1.8 (f) Shader stripping in player builds

- `URP/Editor/ShaderScriptableStripper.cs:1337-1397` (`CanRemoveVariant`): a variant is removed only if **every** URP asset of the build target (Quality levels) considers it unused. Checks match by **keyword name and pass type/name**, so they apply to custom shaders too: ShadowCaster pass dropped when no shadows are supported (1159-1168); Meta passes removed (1137-1156); `_WRITE_RENDERING_LAYERS` stripped by **pass name** `DepthNormals`/`ForwardLit`/`Unlit` (747-783), so **name our passes exactly `ForwardLit`, `DepthOnly`, `DepthNormals`, `ShadowCaster`**; soft shadows keep the OFF variant (547-551); invalid shadow combos removed (1081-1109).
- Settings sources: `URPShaderStrippingSetting.stripUnusedVariants` and others (`URP/Editor/ShaderBuildPreprocessor.cs:368-374`) come from URP Global Settings. Name-based prefiltering via the `[ShaderKeywordFilter]` attributes is in `UniversalRenderPipelineAssetPrefiltering.cs`.
- Built-in Graphics settings (manual): **Always Included Shaders** "includes all possible variants in every build … even if you use the 'shader feature' declaration type" (URP's scriptable stripper still runs on them). **Instancing Variants: Strip Unused** (project `m_InstancingStripping: 0`) keeps `INSTANCING_ON` only if some material in the build has *Enable GPU Instancing* ticked. Fog modes: Automatic (`m_FogStripping: 0`).
- `shader_feature` keywords survive only if a material asset in the build uses them. Materials created at runtime with `new Material()` therefore lose those variants. Use uniforms or `multi_compile_local` instead.
- `Shader.Find` works in a player only if the shader is referenced by a built material or object, listed in Always Included, or placed in `Resources` (Shader.Find manual).
- Fog: `URP/ShaderLibrary/Fog.hlsl:1-9` contains just `#pragma multi_compile_fog`, or `dynamic_branch` when `USE_DYNAMIC_BRANCH_FOG_KEYWORD`, which is 0 here (`universal-config/Runtime/ShaderConfig.cs.hlsl`). If the shader never declares fog keywords, `MixFog`/`ComputeFogIntensity` compile to a no-op, because they are guarded by `FOG_*_KEYWORD_DECLARED` (`ShaderVariablesFunctions.hlsl:444-492`).
- GPU Resident Drawer: renderers whose shaders lack `DOTS_INSTANCING_ON` are silently excluded from GRD and drawn normally (`CORE/Runtime/GPUDriven/SceneProcessors/WorldProcessor.cs:199-218`). GRD needs `BatchRendererGroupStrippingMode.KeepAll` (`GPUResidentDrawer.Validator.cs:61-66`). URP provides the required `BuiltinPropertyMetadata` DOTS block for every shader through Core.hlsl → Input.hlsl:239 → `UniversalDOTSInstancing.hlsl:11-23`. Our UnityPerMaterial values are read per material, so no `UNITY_DOTS_INSTANCING_START` block is needed unless we want per-instance overrides.

### 1.9 (g) Globals and the SRP Batcher

- Manual: the shader must declare all *material properties* in one `UnityPerMaterial` cbuffer, and all built-in engine properties in `UnityPerDraw` (URP does that in `UnityInput.hlsl:110-175`). "A material property that doesn't exist in the UnityPerMaterial constant buffer … makes the shader incompatible." Objects must not use MaterialPropertyBlocks.
- Render-state-only properties are not HLSL variables, so they do not count. Lit leaves `_Cull`/`_ZWrite`/`_SrcBlend` out of its cbuffer (`URP/Shaders/LitInput.hlsl:18-35`) and is still batcher-compatible.
- Plain globals do not break the batcher. URP's own `_MainLightPosition`, `_MainLightColor`, `_AlphaToMaskAvailable` and `_ScaledScreenParams` are plain globals outside any cbuffer (`Input.hlsl:100-121`), and Lit is SRP-Batcher compatible.
- `Shader.SetGlobalVector`: "Global properties are used if a shader needs them but the material does not have them defined (for example, if the shader does not expose them in Properties block)". A same-named Properties entry overrides the global.
- `LitInput.hlsl:37-38`: "Do not ifdef the properties … property constant-buffer offsets change per variant". The cbuffer layout must be identical across all variants and passes.
- GPU instancing vs the SRP Batcher: for MeshRenderers with batcher-compatible shaders, the SRP Batcher takes priority. `Graphics.RenderMeshInstanced`/`RenderMeshIndirect` still use the instancing path.

---

## 2. Compile-ready templates

### 2.1 `Assets/Shaders/ThreadFlatTemplate.shader`

```hlsl
Shader "Megastructure/ThreadFlatTemplate"
{
    Properties
    {
        _BaseColor      ("Base Color", Color) = (0.80, 0.78, 0.74, 1)
        _ShadeTint      ("Ambient Tint", Color) = (1, 1, 1, 1)
        _VertexColorMix ("Vertex Colour Mix", Range(0, 1)) = 1
        _WrapLight      ("Wrap Lighting", Range(0, 1)) = 0.1
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2   // render state only: NOT in the cbuffer
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Identical in every pass -> SRP Batcher compatible. Never #ifdef members.
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; half4 _ShadeTint; half _VertexColorMix; half _WrapLight; half _ShadowStrength;
        CBUFFER_END

        // Globals (set from C#). NOT in Properties, NOT in the cbuffer. Unset globals read as 0.
        float4 _ThreadSkyAmbient, _ThreadGroundAmbient, _ThreadFogColor; // rgb linear
        float4 _ThreadFogParams;      // x density (1/m), y unused, z start distance (m), w max opacity

        half3 ThreadAlbedo(half4 vertexColor) { return _BaseColor.rgb * lerp(half3(1, 1, 1), vertexColor.rgb, _VertexColorMix); }

        float3 ThreadApplyFog(float3 color, float3 positionWS)
        {
            float dist = distance(positionWS, GetCameraPositionWS());
            float f = 1.0 - exp(-_ThreadFogParams.x * max(0.0, dist - _ThreadFogParams.z));
            return lerp(color, _ThreadFogColor.rgb, min(f, _ThreadFogParams.w));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull] ZWrite On ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ThreadForwardVert
            #pragma fragment ThreadForwardFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP          // mandatory under Forward+ (see 1.3)
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : TEXCOORD2;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD3;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ThreadForwardVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz); // WS, VS, CS, NDC
                VertexNormalInputs   vni = GetVertexNormalInputs(input.normalOS);         // normalized normalWS
                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS   = vni.normalWS;
                output.color      = input.color;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vpi);
            #endif
                return output;
            }

            half4 ThreadForwardFrag(Varyings input, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 positionWS = input.positionWS;
                half3  normalWS   = half3(NormalizeNormalPerPixel(input.normalWS));
                normalWS          = IS_FRONT_VFACE(isFront, normalWS, -normalWS);   // correct for Cull Off
                half3  viewDirWS  = GetWorldSpaceNormalizeViewDir(positionWS);       // points to the camera

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);       // per-pixel cascade select
            #else
                float4 shadowCoord = float4(0, 0, 0, 0);
            #endif
                // Overload with positionWS applies the shadow-distance fade (+ light cookie). shadowMask = 1 (no lightmaps).
                Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));

                half3 albedo = ThreadAlbedo(input.color);
                half  ndl    = dot(normalWS, mainLight.direction);
                half  wrap   = saturate((ndl + _WrapLight) / (1.0h + _WrapLight));
                half  shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half3 direct = mainLight.color * (wrap * shadow * (half)mainLight.distanceAttenuation);
                half  hemi    = normalWS.y * 0.5h + 0.5h;
                half3 ambient = half3(lerp(_ThreadGroundAmbient.rgb, _ThreadSkyAmbient.rgb, hemi)) * _ShadeTint.rgb;
                // Example use of viewDirWS: subtle rim (set weight to taste; 0 = pure flat)
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 4.0h) * 0.05h;
                float3 color = albedo * (direct + ambient + rim);
                color = ThreadApplyFog(color, positionWS);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ThreadShadowVert
            #pragma fragment ThreadShadowFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;   // set by URP ShadowUtils.SetupShadowCasterConstantBuffer
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings ThreadShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }
            half4 ThreadShadowFrag(Varyings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ThreadDepthVert
            #pragma fragment ThreadDepthFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings ThreadDepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz); // must match ForwardLit bit-exactly
                return output;
            }
            half ThreadDepthFrag(Varyings input) : SV_Target
            { UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ThreadDepthNormalsVert
            #pragma fragment ThreadDepthNormalsFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings ThreadDepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            void ThreadDepthNormalsFrag(Varyings input, FRONT_FACE_TYPE isFront : FRONT_FACE_SEMANTIC
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                n = IS_FRONT_VFACE(isFront, n, -n);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(oct), 0.0);
            #else
                outNormalWS = half4(n, 0.0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
```

Notes on the template:
- **Vertex displacement.** If you add any, put it in one HLSLINCLUDE function and call it from all four passes. Otherwise the shadows, depth and SSAO will not match the color pass.
- **Getting the vectors.** World position: `GetVertexPositionInputs(...).positionWS`. Normal: `GetVertexNormalInputs(n).normalWS`, renormalized per pixel. View direction: `GetWorldSpaceNormalizeViewDir(positionWS)` (`ShaderVariablesFunctions.hlsl:8-41,137-150`).
- **Vertex colors.** They arrive in linear space only if the procedural mesh supplies linear values: `Mesh.colors` and `colors32` are not converted. Convert in C# with `color.linear` when you write them.

### 2.2 Skybox: `Megastructure/ThreadSky` (assign to `RenderSettings.skybox`)

```hlsl
Shader "Megastructure/ThreadSky"
{
    Properties
    {
        _ZenithColor  ("Zenith", Color)  = (0.18, 0.30, 0.55, 1)
        _HorizonColor ("Horizon", Color) = (0.88, 0.82, 0.74, 1)
        _GroundColor  ("Below Horizon", Color) = (0.62, 0.60, 0.58, 1)
        _GradientPow  ("Gradient Power", Range(0.1, 4)) = 0.5
        _SunRadius    ("Sun Angular Radius (rad)", Range(0.001, 0.2)) = 0.0093
        [HDR] _SunColor ("Sun Colour (HDR)", Color) = (8, 7.6, 7, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off   // default ZTest LEqual; auto-flipped to GEqual under reversed Z
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   SkyVert
            #pragma fragment SkyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor; half4 _HorizonColor; half4 _GroundColor; half4 _SunColor;
                float _GradientPow; float _SunRadius;
            CBUFFER_END
            float4 _ThreadSunDirWS;     // xyz: unit vector TOWARDS the sun (= -sun.transform.forward), set from C#
            float4 _ThreadPlanetDirWS;  // xyz: unit vector to planet centre, w: angular radius (rad); w = 0 -> no planet
            float4 _ThreadPlanetColor;  // rgb linear (lit side)

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 rayWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings SkyVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.rayWS = mul((float3x3)GetObjectToWorldMatrix(), input.positionOS.xyz); // NOT normalized here
                return output;
            }

            // Anti-aliased disc: 1 inside angular radius r around dir (derivative of the angle -> 1-pixel AA edge).
            float AngularDisc(float3 rd, float3 dir, float r)
            {
                float ang = acos(clamp(dot(rd, dir), -1.0, 1.0));
                float aa  = max(fwidth(ang), 1e-6);
                return 1.0 - smoothstep(r - aa, r + aa, ang);
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 rd = normalize(input.rayWS);
                float  h  = rd.y;
                float3 sunDir = SafeNormalize(_ThreadSunDirWS.xyz);   // globals default to 0: never normalize(0) -> NaN
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(h), _GradientPow));
                float  aaH = max(fwidth(h), 1e-5);
                sky = lerp(sky, _GroundColor.rgb, smoothstep(aaH, -aaH, h));        // crisp but AA'd horizon line

                // Planet = sphere at unit distance, radius sin(angRadius): precision-safe at any real scale.
                float3 pd = SafeNormalize(_ThreadPlanetDirWS.xyz);
                float  planetMask = (_ThreadPlanetDirWS.w > 0.0) ? AngularDisc(rd, pd, _ThreadPlanetDirWS.w) : 0.0; // fwidth in uniform flow
                float  pr = sin(_ThreadPlanetDirWS.w), b = dot(rd, pd), disc = b * b - (1.0 - pr * pr);
                if (planetMask > 0.0 && disc > 0.0)
                {
                    float3 n = normalize(rd * (b - sqrt(disc)) - pd);       // hit point (camera at origin) -> normal
                    sky = lerp(sky, _ThreadPlanetColor.rgb * (0.08 + saturate(dot(n, sunDir))), planetMask);
                }
                sky += _SunColor.rgb * AngularDisc(rd, sunDir, _SunRadius) * (1.0 - planetMask);
                sky += (InterleavedGradientNoise(input.positionCS.xy, 0) - 0.5) / 255.0; // anti-banding (CORE Random.hlsl:93)
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
```

Skybox notes:
- Set the URP camera Background Type to **Skybox** (`UniversalRendererRenderGraph.cs:1273`).
- The sun direction comes from our own global, not from `_MainLightPosition`, so it stays valid during the engine's cubemap/ambient captures and SceneView rendering.
- Ambient SH or reflections from the sky are optional. If you want them, call `DynamicGI.UpdateEnvironment()` after changing sky parameters; the template sidesteps this with its own ambient globals.

### 2.3 Additive transparent with soft depth fade

```hlsl
Shader "Megastructure/ThreadAdditiveSoft"
{
    Properties
    {
        [HDR] _TintColor ("Tint (HDR, premultiplied)", Color) = (1, 1, 1, 1)
        _SoftFade   ("Soft Fade Distance (m)", Float) = 2
        _CameraFade ("Camera Fade Distance (m)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardAdditive"
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off ZTest LEqual Cull Off   // no AlphaToMask (A2C only for opaque + MSAA, DrawObjectsPass.cs:140)
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   AddVert
            #pragma fragment AddFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor; float _SoftFade; float _CameraFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionCS : SV_POSITION; half4 color : TEXCOORD0; float eyeDepth : TEXCOORD1;
                                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings AddVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vpi.positionCS;
                output.eyeDepth   = -vpi.positionVS.z;     // view space is right-handed: forward = -z
                output.color      = input.color;
                return output;
            }

            half4 AddFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv       = GetNormalizedScreenSpaceUV(input.positionCS);
                float  rawDepth = SampleSceneDepth(uv);                          // reversed-Z device depth
                float  sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);      // perspective camera only
                float  soft     = saturate((sceneEye - input.eyeDepth) / max(_SoftFade, 1e-3));
                float  camFade  = saturate((input.eyeDepth - _ProjectionParams.y) / max(_CameraFade, 1e-3));
                half3  rgb      = _TintColor.rgb * input.color.rgb * (input.color.a * soft * camFade);
                return half4(rgb, 0.0h);  // additive: alpha unused
            }
            ENDHLSL
        }
    }
    FallBack Off
}
```

### 2.4 C# globals and inclusion helpers

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public sealed class ThreadShaderGlobals : MonoBehaviour
{
    static readonly int SkyAmbientId = Shader.PropertyToID("_ThreadSkyAmbient"), GroundAmbientId = Shader.PropertyToID("_ThreadGroundAmbient"),
        FogColorId = Shader.PropertyToID("_ThreadFogColor"), FogParamsId = Shader.PropertyToID("_ThreadFogParams"), SunDirId = Shader.PropertyToID("_ThreadSunDirWS");

    [SerializeField] Light sun;
    [SerializeField] Color skyAmbient = new Color(0.55f, 0.60f, 0.70f), groundAmbient = new Color(0.35f, 0.33f, 0.30f), fogColor = new Color(0.85f, 0.82f, 0.78f);
    [SerializeField] Vector4 fogParams = new Vector4(0.00015f, 0f, 200f, 0.92f);
    [SerializeField] Shader[] keepInBuild;   // serialized references force the shaders (all multi_compile variants) into the build

    void OnEnable()  { Push(); RenderPipelineManager.beginContextRendering += OnBeginContext; }
    void OnDisable() { RenderPipelineManager.beginContextRendering -= OnBeginContext; }
    void OnBeginContext(ScriptableRenderContext ctx, List<Camera> cams) => Push();

    void Push()   // SetGlobalVector does no colour-space conversion: pass .linear explicitly (project is Linear)
    {
        Shader.SetGlobalVector(SkyAmbientId, skyAmbient.linear);
        Shader.SetGlobalVector(GroundAmbientId, groundAmbient.linear);
        Shader.SetGlobalVector(FogColorId, fogColor.linear);
        Shader.SetGlobalVector(FogParamsId, fogParams);
        if (sun != null) Shader.SetGlobalVector(SunDirId, -sun.transform.forward);
    }
}
```

Editor-only "Always Included" (field name verified in `ProjectSettings/GraphicsSettings.asset:29`):

```csharp
#if UNITY_EDITOR
public static class ThreadAlwaysIncluded   // call from a build/setup menu item
{
    public static void Ensure(params string[] shaderNames)
    {
        var so  = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");
        foreach (var name in shaderNames)
        {
            var sh = UnityEngine.Shader.Find(name);
            if (sh == null) { UnityEngine.Debug.LogError($"Shader not found: {name}"); continue; }
            bool present = false;
            for (int i = 0; i < arr.arraySize && !present; i++) present = arr.GetArrayElementAtIndex(i).objectReferenceValue == sh;
            if (present) continue;
            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
        }
        so.ApplyModifiedProperties();
        UnityEditor.AssetDatabase.SaveAssets();
    }
}
#endif
```

---

## 3. Pitfalls (ranked)

1. **Forward+ black objects.** If `_CLUSTER_LIGHT_LOOP` is missing, `distanceAttenuation` = `unity_LightData.z`, which is unset in F+, so objects render black in builds (1.3).
2. **Missing DepthOnly/DepthNormals passes.** SSAO is active here, so URP renders `_CameraDepthTexture` and `_CameraNormalsTexture` in a **DepthNormals prepass** that uses only the `DepthNormals`/`DepthNormalsOnly` LightModes. A shader without that pass is invisible to the soft-fade transparents and to SSAO, which makes haloes and see-through fog. With depth priming, a missing `DepthOnly` pass makes the object disappear entirely (ZTest Equal).
3. **Depth Texture Mode `AfterTransparents`.** This is the default for new renderers, and it makes the soft fade read stale depth. Keep `AfterOpaques` (`m_CopyDepthMode: 0`). Also keep `m_RequireDepthTexture: 1`.
4. **Hard shadow cutoff line.** `GetMainLight(shadowCoord)` skips `GetMainLightShadowFade`. Use the 3-argument overload. Shadow distance is 50 m here (`PC_RPAsset.asset:57`), so the fade matters for colossal geometry. Consider 4 cascades with a larger distance, and tune `_ShadowBias` through Light depth/normal bias (shadow acne on huge flat planes).
5. **Breaking the SRP Batcher**: a `Properties` entry used in HLSL but not in `UnityPerMaterial`; `#ifdef`'d cbuffer members; different cbuffers per pass; MaterialPropertyBlocks; `UsePass` for the shadow pass. Check the shader inspector ("SRP Batcher: compatible") and the Frame Debugger.
6. **Globals shadowed by Properties / unset globals.** If `_ThreadFogColor` is also in `Properties`, the material value wins and the global is ignored. Unset globals are 0: set them in `OnEnable` and before each frame, and never `normalize()` a global direction unguarded (`SafeNormalize`, `CORE/ShaderLibrary/Common.hlsl:1551`). A NaN sky pixel turns black, and bloom spreads it.
7. **Runtime materials losing variants.** Avoid `shader_feature`. Ensure the shader is referenced: serialized field in the boot scene, plus Always Included. **Instancing Variants = Strip Unused** strips `INSTANCING_ON` unless a material asset in the build enables instancing. For `Graphics.RenderMeshInstanced` with runtime materials, either keep a material asset with instancing enabled in the build, or set Graphics → Instancing Variants to *Keep All*.
8. **The OFF shadow variant is stripped** (`SelectMainLightAndCascades`). Toggling shadows off at runtime does not return to an unshadowed variant. Instead URP binds a 1x1 empty shadowmap with strength 0 (`URP/Runtime/Passes/MainLightShadowCasterPass.cs:29-34,244-250`), so the result is correct but still pays the sampling cost. Verify it in the player.
9. **SSAO is not applied** unless `_SCREEN_SPACE_OCCLUSION` is declared and sampled (`GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV)`), or SSAO runs in "After Opaque" mode, where it multiplies the color target directly. Decide deliberately, because it changes the flat look.
10. **Precision.** URP has no camera-relative rendering. Keep the world near the origin (floating origin) and render the planet and Earth analytically in the sky shader. Never place geometry beyond the far plane.
11. **Gradient banding** in large flat sky areas. Add the IGN dither, as in the sky template, and/or enable camera *Dithering* with post-processing on.
12. **Pass names drive stripping.** Use `ForwardLit` / `ShadowCaster` / `DepthOnly` / `DepthNormals` so the stripper treats our passes the way it treats Lit (`ShaderScriptableStripper.cs:747-783`).
13. **Debugging stripping and hitches**: set the URP asset's *Shader Variant Log Level* (currently `m_ShaderVariantLogLevel: 0`) to log what is kept. The first draw of each variant on D3D12 causes a hitch. Warm up with `ShaderVariantCollection.WarmUp()`, `ShaderWarmup.WarmupShader(...)`, or a traced `GraphicsStateCollection` (all three exist in 6000.6 per `UnityEngine.CoreModule.xml`).
14. **AlphaToMask without MSAA** is effectively a single coverage sample, and the result is hardware-dependent. Use `clip()`, and keep the same threshold in the DepthOnly, DepthNormals and ShadowCaster passes.
15. **Lighting.hlsl includes a lot.** That is harmless, but if a pass only needs shadows, include just `Shadows.hlsl`, as the ShadowCaster above does. Do not include `LitInput.hlsl`: it declares its own `UnityPerMaterial` and would conflict.

---

## 4. Sources (accessed 2026-10-29)

| URL | Used for |
|---|---|
| https://docs.unity.com/en-us/engine/current/manual/lighting-overview/lighting/custom-lighting/use-built-in-shader-methods-additional-lights-fplus | Unity 6.6 manual: `#pragma multi_compile _ _CLUSTER_LIGHT_LOOP`, and that `LIGHT_LOOP_BEGIN` requires an `InputData inputData` with `normalizedScreenSpaceUV` |
| https://issuetracker-mig.prd.it.unity3d.com/issues/getmainlight-returns-incorrect-distanceattenuation-value-for-a-custom-shader-when-urp-forward-plus-rendering-path-is-used | Confirms the Forward+ `distanceAttenuation`=0 black-shader issue ("By Design") |
| https://raw.githubusercontent.com/TwoTailsGames/Unity-Built-in-Shaders/master/DefaultResourcesExtra/Skybox-Procedural.shader | Built-in skybox tags, `Cull Off ZWrite Off`, eye ray from `mul((float3x3)unity_ObjectToWorld, v.vertex.xyz)` (MIT) |
| https://aras-p.info/blog/2019/02/01/Infinite-sky-shader-for-Unity/ | Pushing sky depth to the far plane under reversed Z (`z = 1e-9`) |
| https://discussions.unity.com/t/why-cant-the-unfilled-area-of-the-_cameradepthtexture-be-1/690778 | Reversed Z on D3D, and ZTest LEqual auto-flipped to GEqual (community answer) |
| https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/writing-custom/shader-writing/sl/writing-shader-writing-shader-programs-hlsl/sl-platform-differences | Depth direction table (DirectX/Metal/Vulkan 1→0), `SystemInfo.usesReversedZBuffer`, `UNITY_REVERSED_Z` |
| https://docs.unity3d.com/550/Documentation/Manual/UpgradeGuide55.html | Reversed-Z introduction; `LinearEyeDepth` helpers handle it |
| https://docs.unity.com/en-us/engine/current/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/in-universalrp-srp-batcher | SRP Batcher: `UnityPerMaterial` / `UnityPerDraw` requirements |
| https://docs.unity3d.com/2023.2/Documentation/Manual/SRPBatcher.html | A material property outside `UnityPerMaterial` makes the shader incompatible; no MaterialPropertyBlocks |
| https://docs.unity3d.com/Manual/SRPBatcher-Incompatible.html (search summary) and https://docs.unity3d.com/Manual/gpu-instancing-enable.html (search summary) | The SRP Batcher takes priority over GPU instancing |
| https://docs.unity.com/en-us/engine/current/script-reference/unityengine/shader/setglobalvector | Globals are used only when the material does not define the property (not in Properties) |
| https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-settings-reference/comp-manager-group/class-graphics-settings | Always Included Shaders include all variants (even shader_feature); Instancing Variants strip modes; preloading |
| https://docs.unity3d.com/Manual/shader-variant-stripping.html (search summary) | Unused shader_feature variants are stripped; Always Included as the fix |
| https://docs.unity3d.com/ScriptReference/Shader.Find.html (search summary) | Ways to get a shader into a build (scene reference / Always Included / Resources) |
| https://docs.unity.com/en-us/engine/6000.3/manual/materials-and-shaders/shaders/reference/sl-reference/sl-commands/sl-alpha-to-mask | AlphaToMask On/Off semantics |
| https://docs.unity3d.com/ScriptReference/Rendering.BatchRendererGroupStrippingMode.html (search summary) | BRG stripping modes; numeric values verified locally with Unity.Cecil (KeepAll = 2) |
| https://docs.unity3d.com/Manual/dots-instancing-shaders-declare.html (search summary) | DOTS instancing blocks; URP defines BuiltinPropertyMetadata for every shader |
| https://www.cyanilux.com/tutorials/urp-shader-code/ | Community confirmation: a shared HLSLINCLUDE `UnityPerMaterial` across passes, no `UsePass` for the shadow pass, do not include LitInput.hlsl |

Local ground truth that supersedes the web where they differ:
- URP 17.6 / Core 17.6 package source (file:line references above);
- `UnityEngine.CoreModule.xml` / `UnityEditor.CoreModule.dll` in `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine`;
- project settings assets: `Assets/Settings/PC_RPAsset.asset`, `PC_Renderer.asset`, `ProjectSettings/GraphicsSettings.asset`, `QualitySettings.asset`, `ProjectSettings.asset`.
