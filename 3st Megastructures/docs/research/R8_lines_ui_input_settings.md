# R8: Thin lines, text, input and player settings

Scope: Unity 6000.6.0f1, URP 17.6.0 (`com.unity.render-pipelines.universal@8457e85b8184`), SRP Core (`@2d66c71e606e`), Input System 1.20.0 (`com.unity.inputsystem@7a4e1a2a8194`), uGUI 2.6.0 (`com.unity.ugui@23caec89ae27`).
Paths below are relative to `Library/PackageCache/` unless they start with `Editor/` (relative to `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\`) or `ProjectSettings/`.
Researcher: R8 (read-only). Date: 2026-10-29.

## 0. Decisions (TL;DR)

| Topic | Decision |
|---|---|
| Thin lines | Expand the ribbon in clip space. Use `_ScaledScreenParams`, not `_ScreenParams`. Width clamp `w_min = max(1.5, 1.75*H/1080)` px. Energy = `max(true/w_min, 0.35)`. Coverage `saturate(0.5 + halfPx - abs(dPx))`, with `dPx` and `halfPx` passed as **`noperspective`** varyings. Use premultiplied blend for the thread (Transparent queue). Rails use `AlphaToMask On` in the AlphaTest queue (2450), but **only when MSAA > 1**; otherwise fall back to blending. |
| Text | Use **legacy UGUI `Text`**, with `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (verified in the uGUI source). Build one `Text` per glyph, positioned by `CharacterInfo.advance` + tracking (Part 14.11). Canvas: `ScreenSpaceOverlay`, `CanvasScaler.ConstantPixelSize`, sizes in real pixels, rebuilt when the resolution changes. No EventSystem / `StandaloneInputModule`. Avoid UI Toolkit (theme warning, extra .tss asset). Avoid TMP (essentials `.unitypackage` import). |
| Input | Read devices directly: `Keyboard.current`, `Mouse.current`, `Gamepad.current` (null-check all three). `activeInputHandler: 1` is already set (new Input System only). Mouse look = `delta.ReadValue() * degPerCount`, with **no** `Time.deltaTime` and no smoothing. Gamepad look = rate × `unscaledDeltaTime`. |
| Fullscreen | Keep `allowFullscreenSwitch = true`, so the native Alt+Enter works. Handle **only F11** in code (`Screen.SetResolution` with `FullScreenWindow` ↔ `Windowed`), and never handle Alt+Enter in code as well (that would toggle twice). |
| Graphics API | Set `SetUseDefaultGraphicsAPIs(StandaloneWindows64, false)` and `SetGraphicsAPIs(..., { Direct3D11 })`. The Editor uses the first entry in that list (or `-force-d3d11`). Assert `SystemInfo.graphicsDeviceType == Direct3D11` in `-autotest`. |
| Splash | `PlayerSettings.SplashScreen.show = false` is allowed on Personal in Unity 6. Read the value back and log it. |
| Build | `BuildPipeline.BuildPlayer(BuildPlayerOptions)` → check `report.summary.result == BuildResult.Succeeded`, then `EditorApplication.Exit(code)` in batch mode. |

## 1. Thin-line anti-aliasing

### 1.1 Verified facts
- `_ScaledScreenParams` (render-target size including render scale) is declared at `render-pipelines.universal/ShaderLibrary/Input.hlsl:108`. Its accessor `GetScaledScreenParams()` is at `ShaderLibrary/ShaderVariablesFunctions.hlsl:43-45`. URP sets it from the scaled camera target size (`Runtime/ScriptableRenderer.cs:304`). `_ScreenParams` (`ShaderLibrary/UnityInput.hlsl:63`) is the legacy value, so for pixel widths use `GetScaledScreenParams()`.
- `_ProjectionParams`: x = ±1 (flip), y = near, z = far (`UnityInput.hlsl:52-57`). `UNITY_MATRIX_P` = `OptimizeProjectionMatrix(glstate_matrix_projection)` (`Input.hlsl:222`, `UnityInput.hlsl:301`); `_m11` (= cot(FOVy/2)) is kept. `TransformWorldToHClip` / `TransformObjectToWorld` are at `render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl:115/79`.
- URP's own shaders use `AlphaToMask[_AlphaToMask]` as a material-driven render state (`Shaders/Unlit.shader:20,56`, `Shaders/Lit.shader:60,111`).
- URP sets `_AlphaToMaskAvailable = 1` **only** when `msaaSamples > 1 && isOpaque` (`Runtime/Passes/DrawObjectsPass.cs:140-141`), so URP treats A2C as an opaque-pass + MSAA feature. URP's own helper sharpens alpha for A2C as `saturate((a - cutoff - 0.5*fwidth(a)) / max(fwidth(a), 1e-4) + 1)` (`ShaderLibrary/ShaderVariablesFunctions.hlsl:197-201`). That helper isn't needed for analytic pixel coverage.
- The Unity manual says A2C is meant for MSAA, and without MSAA "the results can be unpredictable" (they differ by API and GPU). Hence the C# switch in 1.7.
- Project state: `Assets/Settings/PC_RPAsset.asset:28` has `m_MSAA: 1` (**disabled**), so the builder must set it to 4. `PC_Renderer.asset:56` has `m_RenderingMode: 2` (Forward+, supports MSAA) and `:57` has `m_DepthPrimingMode: 0` (keep it **Disabled**). With priming on, the opaque pass forces `ZTest Equal` (`DrawObjectsPass.cs:219-222`), and custom shaders without a matching DepthOnly pass would vanish.
- `UniversalRenderPipelineAsset.msaaSampleCount` has a getter and setter (`Runtime/Data/UniversalRenderPipelineAsset.cs:1106-1109`).
- HLSL `noperspective` disables perspective-correct interpolation (SM4+; Microsoft HLSL struct docs). A distance measured in **pixels** must be interpolated linearly in screen space, so `noperspective` is required for an exact ramp along long, receding ribbons.
- Phone-wire AA (Persson / Humus): clamp the wire so it is never thinner than about 1 px, and fade it by the ratio. For example, a 0.5 px wire is drawn 1 px wide at 0.5 coverage. Combine this with MSAA.
- Pristine grid (Ben Golus, 2023): `drawWidth = clamp(target, deriv, 0.5)`, AA band `deriv*1.5`, energy `*= saturate(target/drawWidth)`, then fade to the mean `target` once `deriv > 0.5` (`lerp(g, target, saturate(deriv*2-1))`).

### 1.2 Math (ribbon)
For endpoints A, B → clip `ca, cb`, the pixel position is `p = c.xy / c.w * 0.5 * S`, with `S = GetScaledScreenParams().xy`. The normal is `n = perp(normalize(pb - pa))`.
- True width in px: `wTrue = Wworld * f / c.w`, where `f = |P._m11| * 0.5 * S.y` (perpendicular approximation).
- `wDraw = max(wTrue, w_min)`, `energy = max(saturate(wTrue / w_min), 0.35)`, `halfPx = wDraw/2`, and extent `= halfPx + 1` (a 1 px skirt holds the ramp).
- Vertex offset: `c.xy += n * side * extentPx * (2 / S) * c.w`.
- Fragment: `cov = saturate(0.5 + halfPx - |dPx|)` is exactly the 1-px box-filtered coverage of a line of width `2*halfPx` for axis-aligned lines. Total energy across the profile = `wDraw` regardless of sub-pixel phase, so the centroid moves exactly with the line. This is what the T-THREAD centroid-jitter test needs.
- Near plane: if one end has `w < near`, slide it along the segment to `w = near*1.001` **before** the divide. If both ends are behind, emit degenerate vertices.

### 1.3 `ThreadLine.shader` (URP 17.6, compile-ready)
```hlsl
Shader "Megastructure/ThreadLine"
{
    Properties
    {
        _BaseColor ("Color (A = opacity)", Color) = (1,1,1,1)
        _MinWidthPx1080 ("Min width px at 1080p", Float) = 1.75
        _MinWidthFloorPx ("Min width floor px", Float) = 1.5
        _EnergyFloor ("Energy floor", Range(0,1)) = 0.35
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1   // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10  // OneMinusSrcAlpha
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
        [Enum(Off,0,On,1)] _AlphaToMask ("Alpha To Coverage", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ThreadLine"
            Tags { "LightMode"="UniversalForward" }
            Cull Off  ZTest LEqual  ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            AlphaToMask [_AlphaToMask]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _MinWidthPx1080, _MinWidthFloorPx, _EnergyFloor, _AlphaToMask;
            CBUFFER_END
            struct Attributes
            {
                float3 positionOS : POSITION;   // endpoint A (object space)
                float3 otherOS    : TEXCOORD0;  // endpoint B (object space)
                float3 params     : TEXCOORD1;  // x: 0 = at A, 1 = at B; y: side -1/+1; z: true full width (m)
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                noperspective float2 lineData : TEXCOORD0; // x: signed distance to centre (px), y: drawn half width (px)
                noperspective float  energy   : TEXCOORD1;
            };
            // Perspective camera: clip-space w = view depth. Slide 'a' toward 'b' until w >= nearW.
            float4 PullToNearPlane(float4 a, float4 b, float nearW)
            {
                if (a.w < nearW) a = lerp(a, b, saturate((nearW - a.w) / max(b.w - a.w, 1e-6)));
                return a;
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 ca = TransformWorldToHClip(TransformObjectToWorld(v.positionOS));
                float4 cb = TransformWorldToHClip(TransformObjectToWorld(v.otherOS));
                float nearW = _ProjectionParams.y * 1.001;
                if (ca.w < nearW && cb.w < nearW)   // whole segment behind the camera: zero-area, no pixels
                {
                    o.positionCS = float4(0, 0, 0, 1); o.lineData = 0; o.energy = 0;
                    return o;
                }
                ca = PullToNearPlane(ca, cb, nearW);
                cb = PullToNearPlane(cb, ca, nearW);
                float2 screen = GetScaledScreenParams().xy;
                float2 pa = ca.xy / ca.w * (0.5 * screen);
                float2 pb = cb.xy / cb.w * (0.5 * screen);
                float2 dir = pb - pa;
                float len = length(dir);
                dir = (len > 1e-4) ? dir / len : float2(0.0, 1.0);
                float2 nrm = float2(-dir.y, dir.x);
                float4 c = (v.params.x < 0.5) ? ca : cb;
                float focalPx = abs(UNITY_MATRIX_P._m11) * 0.5 * screen.y;   // abs(): projection may be y-flipped
                float trueWidthPx = v.params.z * focalPx / c.w;
                float minPx = max(_MinWidthFloorPx, _MinWidthPx1080 * screen.y / 1080.0);
                float drawPx = max(trueWidthPx, minPx);
                float halfPx = 0.5 * drawPx;
                float extentPx = halfPx + 1.0;                               // 1 px skirt for the AA ramp
                float side = v.params.y;
                c.xy += nrm * (side * extentPx) * (2.0 / screen) * c.w;
                o.positionCS = c;
                o.lineData = float2(side * extentPx, halfPx);
                o.energy = max(saturate(trueWidthPx / minPx), _EnergyFloor);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float cov = saturate(0.5 + i.lineData.y - abs(i.lineData.x));
                float a = cov * i.energy * _BaseColor.a;
                // Blended mode = premultiplied (One, OneMinusSrcAlpha). A2C mode = straight colour, alpha becomes the sample mask.
                half3 rgb = (_AlphaToMask > 0.5) ? _BaseColor.rgb : _BaseColor.rgb * a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
```
Haze, fog and the bank term go into `frag` exactly as for the other materials (multiply `a` by the visibility, or lerp `rgb` before premultiplying).
Optional refinement for diagonal lines: pass `ramp = abs(nrm.x) + abs(nrm.y)` (nointerpolation) and use `saturate(0.5 + (halfPx - abs(d)) / ramp)`. The area stays the same and the edge becomes slightly softer.

### 1.4 Ribbon mesh (C#)
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public static class RibbonMeshBuilder
{
    // One quad per segment; ThreadLine.shader expands it in screen space. widthWorld = true full width (m).
    public static Mesh Build(IReadOnlyList<Vector3> points, float widthWorld, bool closed = false)
    {
        int segs = closed ? points.Count : points.Count - 1;
        var pos = new Vector3[segs * 4]; var other = new Vector3[segs * 4]; var prm = new Vector3[segs * 4];
        var idx = new int[segs * 6];
        for (int s = 0; s < segs; s++)
        {
            Vector3 a = points[s], b = points[(s + 1) % points.Count];
            int v = s * 4;
            for (int k = 0; k < 4; k++) { pos[v + k] = a; other[v + k] = b; }
            prm[v + 0] = new Vector3(0f, -1f, widthWorld);
            prm[v + 1] = new Vector3(0f, +1f, widthWorld);
            prm[v + 2] = new Vector3(1f, -1f, widthWorld);
            prm[v + 3] = new Vector3(1f, +1f, widthWorld);
            int t = s * 6;
            idx[t] = v; idx[t + 1] = v + 1; idx[t + 2] = v + 2;
            idx[t + 3] = v + 2; idx[t + 4] = v + 1; idx[t + 5] = v + 3;
        }
        var m = new Mesh { name = "ThreadLineRibbon" };
        if (pos.Length > 65535) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(pos); m.SetUVs(0, other); m.SetUVs(1, prm);
        m.SetTriangles(idx, 0, false);
        // POSITION only holds endpoint A, so compute bounds from the polyline.
        var bnd = new Bounds(points[0], Vector3.zero);
        for (int i = 1; i < points.Count; i++) bnd.Encapsulate(points[i]);
        bnd.Expand(widthWorld + 1f);
        m.bounds = bnd;
        return m;
    }
}
```
API check: `Mesh.SetVertices(Vector3[])`, `Mesh.SetUVs(int, Vector3[])` and `SetTriangles(int[], int, bool, int)` all appear in `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.xml`.

### 1.5 Rails: 8-gon tube with diameter clamp (vertex fragment, A2C)
For a round tube, a radial push in world space is the same as pushing along the clip-space normal, and it keeps the shading normals valid. The snippet assumes uniform object scale.
```hlsl
// Attributes add: float4 axisOS : TEXCOORD0;  // xyz = tube axis point of this ring (object space), w = radius (m)
float3 axisWS = TransformObjectToWorld(v.axisOS.xyz);
float3 posWS  = TransformObjectToWorld(v.positionOS);
float4 axisCS = TransformWorldToHClip(axisWS);
float2 screen = GetScaledScreenParams().xy;
float focalPx = abs(UNITY_MATRIX_P._m11) * 0.5 * screen.y;
float trueDiamPx = 2.0 * v.axisOS.w * focalPx / max(axisCS.w, 1e-4);
float minPx = max(_MinWidthFloorPx, _MinWidthPx1080 * screen.y / 1080.0);
float inflate = max(1.0, minPx / max(trueDiamPx, 1e-4));
posWS = axisWS + (posWS - axisWS) * inflate;
o.positionCS = TransformWorldToHClip(posWS);
o.energy = max(saturate(trueDiamPx / minPx), _EnergyFloor);   // fragment: return half4(litColour, o.energy) with AlphaToMask On
```

### 1.6 Pattern lines (stair nosings, cracks, panel lines): pristine-line filter
This is the algorithm from Ben Golus, *The Best Darn Grid Shader (Yet)*, re-implemented in HLSL. Credit it in a code comment.
```hlsl
// lines at integer u; lineWidth in cell units (0..1). Returns coverage 0..1, energy-preserving, no moire at distance.
float PristineLine1D(float u, float lineWidth)
{
    float deriv = max(length(float2(ddx(u), ddy(u))), 1e-6);
    bool invertLine = lineWidth > 0.5;
    float targetWidth = invertLine ? 1.0 - lineWidth : lineWidth;
    float drawWidth = clamp(targetWidth, deriv, 0.5);
    float lineAA = deriv * 1.5;
    float g = abs(frac(u) * 2.0 - 1.0);
    g = invertLine ? g : 1.0 - g;
    float c = smoothstep(drawWidth + lineAA, drawWidth - lineAA, g);
    c *= saturate(targetWidth / drawWidth);                  // energy compensation (phone-wire AA)
    c = lerp(c, targetWidth, saturate(deriv * 2.0 - 1.0));   // sub-pixel cells -> mean value
    return invertLine ? 1.0 - c : c;
}
// 2D grid: float2 g2 = float2(PristineLine1D(uv.x, w.x), PristineLine1D(uv.y, w.y)); float grid = lerp(g2.x, 1.0, g2.y);
```
Compute `u` from interpolated world position (it must stay continuous so `ddx`/`ddy` are valid). Do not compute it after a `frac` of a wrapped coordinate.

### 1.7 Material mode switch (C#)
```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class ThreadLineMaterials
{
    public static bool MsaaOn()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        return urp != null && urp.msaaSampleCount > 1;
    }
    // Rails/posts: opaque-range queue so URP draws them in the MSAA opaque pass; A2C only with MSAA.
    public static void ConfigureCoverage(Material m)
    {
        if (!MsaaOn()) { ConfigureBlended(m); return; }
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f); m.SetFloat("_AlphaToMask", 1f);
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;   // 2450 <= 2500 (opaque range)
    }
    // Thread / far cable ribbons: premultiplied blending, 256 coverage levels, no depth write.
    public static void ConfigureBlended(Material m)
    {
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f); m.SetFloat("_AlphaToMask", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }
}
```

### 1.8 Pitfalls
1. 4x MSAA A2C gives only 5 coverage levels, with vendor-specific static dither patterns. The 0.35 energy floor guarantees at least 1 of 4 samples (alpha ≥ 0.25). Use blending for the thread, where smooth levels matter. Optional advanced fix: write `SV_Coverage` yourself to get deterministic masks.
2. Never static-batch ribbon/tube objects (`StaticEditorFlags.BatchingStatic`). Static batching bakes POSITION into world space but not the `TEXCOORD0` endpoint, so the ribbons collapse.
3. A ribbon's POSITION holds only endpoint A, so Unity's auto bounds are wrong. Set `mesh.bounds` explicitly (done in 1.4) or frustum culling will pop.
4. `line`, `point` and `triangle` are reserved HLSL words (geometry-shader primitives). Do not use them as identifiers.
5. MSAA resolves before tonemapping. Very bright HDR line colours (≫1) on dark backgrounds re-alias after the resolve. Keep thin-line radiance ≤ about 1.0 (linear), or below the bloom threshold.
6. URP ignores `QualitySettings.antiAliasing`. Set the MSAA count on the URP asset (`msaaSampleCount = 4`) and keep `Camera.allowMSAA = true`.
7. `Cull Off` is required: flipped projections (`_ProjectionParams.x = -1` when rendering to an RT) reverse the winding.
8. Do not derive pixel width from `_ScreenParams`, or from `fwidth` of a clip position. Use `GetScaledScreenParams()`, which stays correct if the render scale ever changes.

## 2. Text

### 2.1 Verified facts
- The built-in font name in Unity 6 is `"LegacyRuntime.ttf"`. uGUI 2.6.0 itself calls `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` (`com.unity.ugui@23caec89ae27/Runtime/UGUI/UI/Core/Text.cs:591,597`); so does Collab (`com.unity.collab-proxy@4628d2ba6ea4/Editor/UI/Avatar/AvatarGenerator.cs:216`). The object `LegacyRuntime` is present in `Editor/Data/Resources/unity default resources`, with font-name fallback `Arial` and kerning pairs (T, V, Y …). Do not use `"Arial.ttf"`: community reports say it stopped being a valid built-in name in 2022.2 (not re-verified here).
- `Font` API (in `UnityEngine.TextRenderingModule.xml`): `RequestCharactersInTexture(string, int size, FontStyle)` (line 240); `GetCharacterInfo(char, out CharacterInfo, int size, FontStyle)` → `bool` (line 200); `CharacterInfo.advance` (an int, rounded; line 12) plus `minX`/`maxX`/`minY`/`maxY`, `uvBottomLeft` … `uvTopRight` and `glyphWidth`/`glyphHeight`; `Font.dynamic`, `ascent`, `lineHeight`, `material` and `HasCharacter`; `Font.textureRebuilt` (used by `FontUpdateTracker.cs:39`).
- `Text.pixelsPerUnit` for dynamic fonts = `canvas.scaleFactor` (`Text.cs:544-559`). Glyph rounding happens only if `canvas.pixelPerfect` is set (`Text.cs:683-685` → `PixelAdjustPoint`).
- `FontData` clamps `fontSize` to 0..300 (`FontData.cs:194`). The 4K title is about 97 px, which is fine.
- `CanvasScaler.ScaleMode.ConstantPixelSize` and `scaleFactor` are in `Layout/CanvasScaler.cs:36,73`. `Graphic.raycastTarget` is at `Graphic.cs:181`. `Text.alignment`/`horizontalOverflow`/`verticalOverflow`/`supportRichText` are at `Text.cs:347/460/480/226`.
- UI Toolkit at runtime: `UnityEngine.UIElementsModule.dll` contains the strings "No Theme Style Sheet set to PanelSettings " and ", UI will not render properly". It also contains an ICU message saying advanced-text data "will not be present on PanelSettings created at runtime". The default theme file content is `@import url("unity-theme://default");` (string in `UnityEditor.UIElementsModule.dll`). That means a working UI Toolkit setup needs a generated `.tss` asset plus a `PanelSettings` asset. `IStyle.letterSpacing` and `FontDefinition.FromFont(Font)` exist.
- TextMeshPro ships inside uGUI 2.x, but its shaders and settings live in `com.unity.ugui@23caec89ae27/Package Resources/TMP Essential Resources.unitypackage`, which is an import. Avoid it.

### 2.2 Recommendation
Use **UGUI per-glyph `Text`**, which Part 14.11 mandates and which is the safer option. It needs zero assets, gives no player warnings, and the tracking is exact. Skip U+200A hair spaces: whether the glyph exists depends on the OS fallback `Arial`, so it is not deterministic. If ever needed, check it with `font.HasCharacter('\u200A')`.

### 2.3 Canvas + per-glyph builder (compile-ready)
```csharp
using UnityEngine;
using UnityEngine.UI;
public static class ThreadText
{
    static Font s_Font;
    public static Font BuiltinFont =>
        s_Font != null ? s_Font : (s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
    public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder; canvas.pixelPerfect = false;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f; // real pixels; rebuild on resize
        var group = go.AddComponent<CanvasGroup>();                     // fade the whole line via group.alpha
        group.interactable = false; group.blocksRaycasts = false;
        return canvas;                                                  // no GraphicRaycaster / EventSystem needed
    }
    // Builds one Text per glyph, optically centred on 'row' (anchored at its centre). trackingEm: 0.3 title, 0.1 ending.
    public static void BuildRow(RectTransform row, string text, int fontPx, float trackingEm, Color color,
                                FontStyle style = FontStyle.Normal)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Object.Destroy(row.GetChild(i).gameObject);
        if (string.IsNullOrEmpty(text)) return;
        Font font = BuiltinFont;
        font.RequestCharactersInTexture(text, fontPx, style);
        float tracking = trackingEm * fontPx;
        int n = text.Length;
        var adv = new float[n];
        float firstMinX = 0f, lastMaxX = 0f, penAtLast = 0f;
        for (int i = 0; i < n; i++)
        {
            CharacterInfo ci;
            bool ok = font.GetCharacterInfo(text[i], out ci, fontPx, style);
            adv[i] = ok ? ci.advance : fontPx * 0.3f;
            if (i == 0) firstMinX = ok ? ci.minX : 0f;
            if (i == n - 1) lastMaxX = ok ? ci.maxX : adv[i];
            if (i < n - 1) penAtLast += adv[i] + tracking;
        }
        float visualWidth = penAtLast + lastMaxX - firstMinX;
        float pen = Mathf.Round(-0.5f * visualWidth - firstMinX);       // integer pixels: crisp glyphs
        for (int i = 0; i < n; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                var go = new GameObject("glyph_" + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(row, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(adv[i] + 2f, fontPx * 1.5f);
                rt.anchoredPosition = new Vector2(Mathf.Round(pen), 0f);
                var t = go.AddComponent<Text>();
                t.font = font; t.fontSize = fontPx; t.fontStyle = style;
                t.alignment = TextAnchor.MiddleLeft;                        // pen origin = rect left edge
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = false; t.raycastTarget = false;
                t.color = color; t.text = text[i].ToString();
            }
            pen += adv[i] + tracking;
        }
    }
}
// Title:  fontPx = Mathf.RoundToInt(0.045f * Screen.height); row anchoredPosition.y = (0.5f - 0.42f) * Screen.height.
// Ending: 0.035 * H, 0.1 em. Hint: one normal Text (no tracking), 0.016 * H, bottom offset 0.08 * H.
// Rebuild when Screen.width/height change (poll in LateUpdate; SetResolution applies next frame).
```

### 2.4 Pitfalls
1. **No EventSystem with `StandaloneInputModule`.** Its `BaseInput` reads `Input.mousePosition` unguarded (`com.unity.ugui@23caec89ae27/Runtime/UGUI/EventSystem/InputModules/BaseInput.cs:95`). With `activeInputHandler: 1` that throws the native `InvalidOperationException` "You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package…" (string verified in `Editor/Unity.dll`). For the pause menu, drive Resume/Restart/Quit from the keyboard/gamepad plus a `RectTransformUtility.RectangleContainsScreenPoint` mouse test. If you do need an EventSystem, use `UnityEngine.InputSystem.UI.InputSystemUIInputModule` (auto `AssignDefaultActions()`, `InputSystemUIInputModule.cs:1541,1648`).
2. Overlay UI is drawn after URP's post stack, so tonemapping doesn't touch CREAM. It **is** captured by `ScreenCapture.*` but **not** by `Camera.Render()` into an RT, which matters for the capture harness.
3. `GetCharacterInfo` returns false unless `RequestCharactersInTexture` was called with the **same** size and style.
4. Advances are integer pixels at that size, so measure at the exact rendered pixel size (which is why ConstantPixelSize is used).
5. Per-glyph layout drops kerning pairs. At 0.3 em tracking this is invisible.
6. The legacy font has no thin weight. Get the "thin" feel from small size, tracking, and 90 % alpha.

## 3. Input System 1.20 (no InputActionAsset)

### 3.1 Verified facts (`com.unity.inputsystem@7a4e1a2a8194/InputSystem/Runtime/...`)
- Device accessors:
  - `Keyboard.current` (`Devices/Keyboard.cs:2389`), `Mouse.current` (`Devices/Mouse.cs:258`) and `Gamepad.current` (`Devices/Gamepad.cs:627`) are all **null** when the device is absent.
  - `Keyboard[Key]` (`:2368`), `anyKey` (`:1296`), `allKeys` (`:2384`, `ReadOnlyArray` with a struct enumerator), `KeyControl.keyCode` (`Controls/KeyControl.cs:47`), and `ButtonControl.isPressed` / `wasPressedThisFrame` / `wasReleasedThisFrame` (`Controls/ButtonControl.cs:198/282/327`).
  - Gamepad sticks carry a `stickDeadzone` processor (`Devices/Gamepad.cs:138,149`), default min 0.125 (`InputSettings.cs:783`).
- `Pointer.delta` (`Devices/Pointer.cs:86-129`) **resets to (0,0) every update** (in `onBeforeUpdate`), **accumulates** multiple events within one frame ((1,1)+(2,2) = (3,3)), and can report motion while the cursor is locked. So it is "pixels moved since last frame": never multiply it by `Time.deltaTime`, and read it once per frame in `Update`/`LateUpdate` (never `FixedUpdate`).
- `InputSystem.onAnyButtonPress` (`InputSystem.cs:2432`) = `onEvent` + `GetFirstButtonPressOrNull()`. It fires on **buttons only**, not on mouse motion or stick deflection. `.Call(...)`/`.CallOnce(...)` are in `UnityEngine.InputSystem.Utilities.Observable` (`Utilities/Observables/Observable.cs:202,234`) and return an `IDisposable`.
- `activeInputHandler`: 0 = old Input Manager, 1 = new Input System only, 2 = both (`InputSystem/Editor/Settings/EditorPlayerSettingHelpers.cs:73-92`). It has no public `PlayerSettings` API; the package edits it via `SerializedObject` and reflection over `BuildProfile` (`:115-130`). The project already has `ProjectSettings/ProjectSettings.asset:935 activeInputHandler: 1`. Gate the test on that line.
- Defaults: `UpdateMode.ProcessEventsInDynamicUpdate` (`InputSettings.cs:774`) and `BackgroundBehavior.ResetAndDisableNonBackgroundDevices` (`:780`), so keys don't stay stuck after Alt+Tab even with `runInBackground`.
- 1.20.0 changelog (2026-07-21): fixes for `onAnyButtonPress` with touch, and for devices lost after a package upgrade. Nothing breaks direct device polling.

### 3.2 Input reader + mouse look + autowalk cancel (compile-ready)
```csharp
using UnityEngine;
using UnityEngine.InputSystem;
public static class ThreadInput
{
    public static int SkipMouseFrames;   // set to 2 after cursor lock / focus regain to drop the warp spike
    public static Vector2 Move()
    {
        Vector2 v = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) v.y += 1f; if (kb.sKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed) v.x += 1f; if (kb.aKey.isPressed) v.x -= 1f;
        }
        var gp = Gamepad.current;
        if (gp != null) v += gp.leftStick.ReadValue();
        return Vector2.ClampMagnitude(v, 1f);
    }
    public static bool Brisk()
    {
        var kb = Keyboard.current; var gp = Gamepad.current;
        return (kb != null && kb.shiftKey.isPressed) || (gp != null && (gp.leftShoulder.isPressed || gp.rightShoulder.isPressed));
    }
    // Degrees to add this frame: x = yaw, y = pitch-up. Raw mouse counts, no smoothing, Y not inverted.
    public static Vector2 LookDegrees(float degPerCount, float stickDegPerSec)
    {
        Vector2 d = Vector2.zero;
        var m = Mouse.current;
        if (m != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 md = m.delta.ReadValue();                 // counts accumulated since last frame
            if (SkipMouseFrames > 0) SkipMouseFrames--; else d += md * degPerCount;   // NO Time.deltaTime
        }
        var gp = Gamepad.current;
        if (gp != null) { Vector2 s = gp.rightStick.ReadValue(); d += s * s.magnitude * stickDegPerSec * Time.unscaledDeltaTime; }
        return d;
    }
    public static bool CinematicToggle() { var kb = Keyboard.current; return kb != null && kb.cKey.wasPressedThisFrame; }
    public static bool Pause()
    {
        var kb = Keyboard.current; var gp = Gamepad.current;
        return (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
    }
    public static bool FullscreenToggle() { var kb = Keyboard.current; return kb != null && kb.f11Key.wasPressedThisFrame; }
    // "Any player input" for cancelling autowalk; excludes the toggles and OS keys a recorder might press.
    public static bool AnyCancelInput(float mouseThresholdPx = 3f, float stickThreshold = 0.3f)
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.isPressed)
        {
            foreach (var k in kb.allKeys)
            {
                if (k == null || !k.wasPressedThisFrame) continue;
                switch (k.keyCode)
                {
                    case Key.C: case Key.F11: case Key.Escape: case Key.LeftAlt: case Key.RightAlt:
                    case Key.Enter: case Key.NumpadEnter: case Key.PrintScreen: case Key.LeftMeta: case Key.RightMeta:
                        continue;
                    default:
                        return true;
                }
            }
        }
        var m = Mouse.current;
        if (m != null)
        {
            if (m.delta.ReadValue().sqrMagnitude > mouseThresholdPx * mouseThresholdPx) return true;
            if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame) return true;
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            float t2 = stickThreshold * stickThreshold;
            if (gp.leftStick.ReadValue().sqrMagnitude > t2 || gp.rightStick.ReadValue().sqrMagnitude > t2) return true;
            if (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame || gp.buttonWest.wasPressedThisFrame ||
                gp.buttonNorth.wasPressedThisFrame || gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
                gp.leftTrigger.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame) return true;
        }
        return false;
    }
    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;          // set both explicitly
        if (locked) SkipMouseFrames = 2;
    }
}
// Usage in the player controller (Update or LateUpdate, once per frame):
//   Vector2 look = ThreadInput.LookDegrees(0.08f, 120f);
//   yaw += look.x;  pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);   // mouse up (+y) -> look up (negative X rotation)
//   body.localRotation = Quaternion.Euler(0f, yaw, 0f); head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
// Autowalk: check CinematicToggle() first and return; then ignore AnyCancelInput() for 0.25 s after start.
// void OnApplicationFocus(bool f) { if (f && !paused) ThreadInput.SetCursorLocked(true); }
```

### 3.3 Pitfalls
1. Do **not** call `UnityEngine.Input.*` anywhere: it throws with handler = 1. That includes `StandaloneInputModule` and copied snippets using `Input.GetAxis`.
2. Do not smooth or lerp mouse look; the spec forbids smoothing lag. Clamp only pitch. Never apply roll.
3. Raw versus accelerated delta: forum reports say the Input System uses WM_INPUT on Windows, but deltas are integer window pixels. That is unverified in official docs, so treat "no acceleration" as PLAUSIBLE and note it in `DEVELOPMENT.md`. If precision is ever insufficient, the only route is P/Invoke raw input, which is out of scope.
4. The first frame after locking or refocusing can report a large warp delta, so `SkipMouseFrames` drops it (defensive).
5. In the Editor, Esc unlocks the cursor by itself and the Game view needs focus. Test cursor behaviour in the **player**.
6. The project contains `Assets/InputSystem_Actions.inputactions` (template project-wide actions). It is harmless for direct polling. Do not bind gameplay to it.

## 4. Fullscreen toggle
- Facts:
  - `FullScreenMode` has `ExclusiveFullScreen` (Windows only), `FullScreenWindow` (borderless, native res, **letterboxes** a non-native aspect), `MaximizedWindow` and `Windowed` (`UnityEngine.CoreModule.xml:13743-13762`). `Screen.SetResolution(int,int,FullScreenMode)` is at `:55179`. `Screen.mainWindowDisplayInfo` → `DisplayInfo.width/height/refreshRate/workArea`.
  - `PlayerSettings.allowFullscreenSwitch` lets "default OS full-screen key presses" (Alt+Enter on Windows) toggle full-screen ↔ windowed (Windows Player settings manual; `UnityEditor.CoreModule.xml:41087`).
  - Screen mode, resolution and window position persist in PlayerPrefs. Startup precedence is: command line > *changed* Player settings > stored PlayerPrefs > default Player settings (Screen API docs). Player CLI flags: `-screen-fullscreen 0|1`, `-screen-width`, `-screen-height`, `-window-mode exclusive|borderless`, `-monitor N`, `-force-d3d11`, `-logFile <path>`.
- Implementation (F11 only; Alt+Enter is native):
```csharp
using UnityEngine;
public static class FullscreenToggle
{
    static int s_WinW, s_WinH;
    public static void Toggle()
    {
        if (Application.isEditor) return;                      // SetResolution is a no-op in the Game view
        var di = Screen.mainWindowDisplayInfo;
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            s_WinW = Screen.width; s_WinH = Screen.height;
            Screen.SetResolution(di.width, di.height, FullScreenMode.FullScreenWindow);
            return;
        }
        int w = s_WinW > 0 ? s_WinW : Mathf.RoundToInt(di.width * 0.75f);
        int h = s_WinH > 0 ? s_WinH : Mathf.RoundToInt(di.height * 0.75f);
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
    }
}
// In Update: if (ThreadInput.FullscreenToggle()) FullscreenToggle.Toggle();   // never also react to Alt+Enter
```
- Pitfalls:
  1. Handling Alt+Enter in code while `allowFullscreenSwitch = true` toggles twice, which looks like nothing happened. If code must own Alt+Enter, set `allowFullscreenSwitch = false` and check `(leftAlt||rightAlt).isPressed && (enterKey||numpadEnterKey).wasPressedThisFrame`.
  2. `Screen.width`/`height` update one frame later. Rebuild the UI on the next observed change, not right after the call.
  3. Tests must pass `-screen-fullscreen`/`-screen-width`/`-screen-height` (or `-window-mode borderless`), because a previous manual run's PlayerPrefs would otherwise change the start mode.
  4. `FullScreenWindow` at a non-native aspect letterboxes, which conflicts with Part 8.10 ("no black bars"). Always request the display's native size in fullscreen, and keep `defaultIsNativeResolution = true`.

## 5. PlayerSettings, graphics API and build (editor script)

### 5.1 Verified facts
- Every member used below exists in `Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.xml`:
  - PlayerSettings: `companyName`, `productName`, `SplashScreen.show` (:43035) / `showUnityLogo` / `backgroundColor` / `animationMode` (`AnimationMode.Static`) / `unityLogoStyle`, `fullScreenMode`, `resizableWindow`, `runInBackground`, `visibleInBackground`, `allowFullscreenSwitch`, `forceSingleInstance`, `usePlayerLog`, `useFlipModelSwapchain`, `defaultScreenWidth/Height`, `colorSpace` (:41142), `enableFrameTimingStats` (:41232), `advancedLicense`, `SetUseDefaultGraphicsAPIs(BuildTarget,bool)` (:42967), `SetGraphicsAPIs(BuildTarget,GraphicsDeviceType[])` (:42678), `GetGraphicsAPIs`, `SetScriptingBackend(NamedBuildTarget,…)` (:42881), `SetApiCompatibilityLevel(NamedBuildTarget,…)` (:42593). The `BuildTargetGroup` overloads are "marked for deprecation".
  - Build: `EditorBuildSettings.scenes` ("scenes in the active platform profile or build profile", :14635) / `globalScenes`, `BuildPipeline.BuildPlayer(BuildPlayerOptions)`, `BuildReport.summary.{result,totalErrors,totalWarnings,totalSize,totalTime,outputPath}`, `BuildResult.{Succeeded,Failed,Cancelled,Unknown,Pending}`, `EditorApplication.Exit(int)`, `EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget,BuildTarget)`.
- `defaultIsNativeResolution` has no XML entry, but `get_/set_defaultIsNativeResolution` exist in `UnityEditor.CoreModule.dll` (metadata string grep).
- Enums: `ApiCompatibilityLevel.NET_Standard` = ".NET Standard 2.1" (XML :2788), and the project's `apiCompatibilityLevel: 6` already matches. `ScriptingImplementation.Mono2x`. `GraphicsDeviceType.Direct3D11 = 2`, `Direct3D12 = 18`.
- Current `ProjectSettings/ProjectSettings.asset`:
  - `companyName: DefaultCompany` (:15) and `productName: 3rd Megastructures` (:16); both must change to Thread. `m_ShowUnitySplashScreen: 1` (:20), `m_ActiveColorSpace: 1` (Linear, :49), `defaultIsNativeResolution: 1` (:88).
  - `runInBackground: 0` (:90) → 1 and `resizableWindow: 0` (:104) → 1. `allowFullscreenSwitch: 1` (:115), `fullscreenMode: 1` (= FullScreenWindow, :116), `enableFrameTimingStats: 0` (:160).
  - Windows APIs `m_APIs: 0200000012000000` = [D3D11, D3D12] with `m_Automatic: 0` (:542-544). The Standalone backend is not listed, so it is Mono.
- Splash: Unity support says Unity 6 "Added the option on Unity Personal to disable or customize the Made with Unity splash screen".
- Graphics API:
  - The top of the list is the default and the player falls back down the list. The Windows Editor "uses the first API in the list" (Configure graphics APIs, 6000.6). The Editor also accepts `-force-d3d11` / `-force-d3d12`.
  - DX12 has been the default for **new** projects since 6.1. The blog notes extra graphics memory per thread, DX11 as a fallback for older hardware, and PSO pre-cooking against first-use stutter.
  - Recommendation: **D3D11 only**, for both Editor and player. The scene is CPU-light, D3D11 is mature, and there is no PSO-hitch risk in a 9-minute single take. Re-evaluate only on a measured D3D11 problem.
- Build Profiles (Unity 6): the active build profile can carry its own Player settings (the Input System reads `BuildProfile.s_GlobalPlayerSettings` / `GetActiveBuildProfile()`, `EditorPlayerSettingHelpers.cs:115-130`). Keep the plain Windows platform profile active, with no custom profile overriding Player settings.

### 5.2 Editor script (place under `Assets/_Game/Editor/`)
```csharp
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
public static class ThreadProjectSettings
{
    [MenuItem("Thread/Apply Player Settings")]
    public static void Apply()
    {
        PlayerSettings.companyName = "Thread";          // any non-default; changes PlayerPrefs + Player.log folder
        PlayerSettings.productName = "Thread";          // window title + LocalLow\<company>\<product>\Player.log
        PlayerSettings.SplashScreen.show = false;        // allowed on Personal in Unity 6
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = Color.black;   // if the license ever forces it: black, static
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;   // used for Windowed
        PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = false;
        PlayerSettings.allowFullscreenSwitch = true;    // native Alt+Enter; code handles F11 only
        PlayerSettings.forceSingleInstance = false; PlayerSettings.usePlayerLog = true;
        PlayerSettings.useFlipModelSwapchain = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.enableFrameTimingStats = true;   // FrameTimingManager CPU/GPU times in the player
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Settings] splash.show={PlayerSettings.SplashScreen.show} pro={PlayerSettings.advancedLicense} " +
                  $"apis={string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64))}");
    }
}
public static class ThreadBuild
{
    const string Scene = "Assets/_Game/Scenes/Thread.unity";
    const string Exe = "Builds/Windows/Thread.exe";
    // Unity.exe -batchmode -projectPath "<proj>" -buildTarget win64 -executeMethod ThreadBuild.BuildWindows -logFile "<log>"
    // (-quit is not needed: Exit() ends the process with a meaningful code; do not use -nographics if captures follow.)
    public static void BuildWindows()
    {
        ThreadProjectSettings.Apply();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, BuildTarget.StandaloneWindows64);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { Scene }, locationPathName = Exe, options = BuildOptions.None,
            target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary s = report.summary;
        Debug.Log($"[Build] result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                  $"size={s.totalSize} time={s.totalTime} out={s.outputPath}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
```

### 5.3 Pitfalls
1. `SetGraphicsAPIs` doesn't switch the **running** Editor's device; that takes effect on the next Editor start. Launch capture sessions with `-force-d3d11` to be explicit, and log `SystemInfo.graphicsDeviceType` at startup in the Editor and the player. Fail `-autotest` if it isn't `Direct3D11`.
2. Changing `productName`/`companyName` moves `Player.log` to `%USERPROFILE%\AppData\LocalLow\Thread\Thread\Player.log` and resets PlayerPrefs. The QA log scanners must use the new path, or pass `-logFile`.
3. `BuildResult.Succeeded` can still hide shader compile errors, which are logged rather than fatal. Grep the Editor log for `Shader error` / `error CS` after every build.
4. Prefer `-buildTarget win64` on the command line over `SwitchActiveBuildTarget` inside the method, because switching mid-method triggers a domain reload or reimport.
5. Use `NamedBuildTarget` overloads; the `BuildTargetGroup` ones are marked for deprecation and would add warnings.
6. `PlayerSettings.SplashScreen.show` reads back true if the license forbids disabling it. Log the value, and keep the black static fallback.
7. `colorSpace` is already Linear; setting it again is a no-op. Changing it on a populated project triggers a full reimport.

## 6. Mouse look best practice (summary)
- `Mouse.current.delta.ReadValue()` already holds this frame's accumulated counts (`Pointer.cs:103-122`). Rotation = counts × deg/count (about 0.06–0.10 deg/count), with no `deltaTime`, no smoothing and no acceleration curve. Apply yaw to the body and pitch to the head, clamp pitch to ±85°, no roll, in `LateUpdate` after movement.
- Gamepad look is rate-based (deg/s × `Time.unscaledDeltaTime`) with the built-in deadzone and a quadratic response. Skip 1–2 frames of delta after locking the cursor or regaining focus. Ignore mouse look while paused, and while autowalk owns the camera (except for the cancel check).

## Sources (accessed 2026-10-29)
| URL | Used for |
|---|---|
| https://www.humus.name/index.php?page=3D&ID=89 | Phone-wire AA: clamp to 1 px, fade by ratio, combine with MSAA |
| https://godotshaders.com/shader/the-best-darn-grid-shader-yet-for-godot/ | CC0 port of Golus' pristine grid (derivative, `gridUV`, smoothstep lines) |
| https://lovr.org/docs/v0.19.0/Environment/Grid_(Smooth) | Same algorithm: `drawWidth` clamp, energy `*= saturate(w/drawWidth)`, fade-to-mean term |
| https://bgolus.medium.com/the-best-darn-grid-shader-yet-727f9278b9d8 | Original article (HTTP 403 to the fetcher; algorithm cross-checked via the two ports) |
| https://docs.unity3d.com/2022.3/Documentation/Manual/SL-AlphaToMask.html | AlphaToMask syntax; unpredictable without MSAA |
| https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-struct | `noperspective` / `centroid` / `sample` interpolation modifiers |
| https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/windows/playersettings.md | Fullscreen modes, Default Is Native Resolution, Run/Visible In Background, Allow Fullscreen Switch, flip model, splash, scripting backend |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/playersettings/allowfullscreenswitch | allowFullscreenSwitch description |
| https://discussions.unity.com/t/block-or-override-alt-enter-fullscreen/82728 | Alt+Enter (Windows) / Cmd+F (Mac) governed by the flag (community) |
| https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/screen | Screen settings persisted in PlayerPrefs; startup precedence |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player | Player CLI: `-screen-*`, `-window-mode`, `-force-d3d11/12`, `-logFile`, `-monitor` |
| https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor | Editor CLI: `-force-d3d11/12`, `-batchmode`, `-executeMethod`, `-buildTarget`, `-logFile` |
| https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/cross-platform-features/graphics-apis/configure | API list order and fallback; Editor uses the first API in the list |
| https://unity.com/blog/directx-12-improvements-in-unity-6 | DX12 default since 6.1; memory trade-off; DX11 fallback advice; PSO tracing |
| https://support.unity.com/hc/en-us/articles/212165803 | Unity 6 Personal may disable the splash screen |
| https://forum.unity.com/threads/is-frame-delta-accounted-for-in-mouse-current-delta.945542/ | Do not multiply the mouse delta by deltaTime (community) |
| https://discussions.unity.com/t/new-input-system-get-raw-mouse-delta-from-wm_input/857382 | Input System uses WM_INPUT; integer pixel deltas (community, unverified) |
| https://doc.photonengine.com/fusion/current/addons/advanced-kcc/input-smoothing | `Mouse.current.delta` is unprocessed |
| https://discussions.unity.com/t/launching-game-in-windowed-mode-by-default-problem/68708 | Registry/PlayerPrefs screen persistence (community) |
