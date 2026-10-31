// M07 RingArk - the ark (14.5.3, 14.2.6), one shader for its three materials, selected by the material constant
// _Mode (uniform branch, no keywords; one UnityPerMaterial CBUFFER for every pass, SRP Batcher):
//   0 Hull    (RingMat.ArkHull)   : ArkHull on the upper hull, ArkUnderside on the lower flank and the belly (object-space
//                                   normal, _Shade.xy); 15 m panel lines in object space, box-filtered with the anisotropic
//                                   surface footprint and faded below 6 px spacing; structure (spars, keel: vertex G = 1)
//                                   in a dimmed ArkHull (_Shade.w) so it reads against the belly; downward faces see the
//                                   ground, not the bright horizon, in the shared sky reflection (specular occlusion
//                                   _Shade.z), so the belly stays the darkest part of the ship.
//   1 Windows (RingMat.ArkWindow) : the window band (one cell quad per window, uv0 = cell-local metres, uv1 = (id,
//                                   level)): the hull look of mode 0 plus the 12 x 5 m apertures, footprint-filtered;
//                                   the grid fades into the energy-preserving warm band (its exact mean: aperture area
//                                   fraction x mean level) when the projected pitch drops below 8 px and is a pure band
//                                   below 4 px, separately along the hull and across the decks, so no 3 px grid can ever
//                                   appear. Window level _ArkParams.y (0.4 + 0.6 N, AtmosphereDriver).
//   2 Engine  (Beat34Mat.ArkEngine): nozzle interiors EngineBlue x HDR 6 x _ArkParams.z, brighter toward the throat
//                                   (vertex B).
// Every mode adds a star rim and a weak arch bounce so the hull never reads black (luma >= 25, 14.5.3 pitfalls) while
// the mass stays dark: the rim is the backlit sheen of the silhouette (key colour x (1 - N.V)^p x the star-side facing
// x how far the star stands behind the hull from the viewer), so the upper edge toward the star carries a thin highlight
// and the flank and belly facing the viewer stay in self-shadow. The pixel footprint comes from _RingCam.w x distance (f.footprint),
// projected onto the surface along each pattern axis: fp_a = fp sqrt(1 + (a.v)^2 / cos^2 i) (no derivatives).
// RingLitCore / RingLitPass contract (RingCeramic structure): RingBend in all four passes, shared lighting
// (RingLighting, Forward+), RingFog last (ship fog 59 % at 3.78 km with sigma 2.5e-4). Renderers never cast shadows.
Shader "Megastructure/RingArk"
{
    Properties
    {
        [Enum(Hull, 0, Windows, 1, Engine, 2)] _Mode ("Mode", Float) = 0
        _HullColor ("Hull albedo, ArkHull (linear)", Color) = (0.159, 0.178, 0.216, 1)
        _UnderColor ("Underside albedo, ArkUnderside (linear)", Color) = (0.045, 0.052, 0.072, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.25
        _Panel ("Panels: x pitch (m), y line width (m), z line darkening, w fade spacing (px)", Vector) = (15, 0.35, 0.35, 6)
        _Shade ("x ArkUnderside from object n.y, y full at n.y, z belly specular occlusion, w structure tone (x ArkHull)", Vector) = (0.1, -0.45, 0.7, 0.85)
        _Rim ("x star rim gain, y rim power, z arch bounce gain, w self floor (x albedo)", Vector) = (0.16, 3, 0.12, 0.02)
        _WindowColor ("Window colour, WindowWarm (linear)", Color) = (1, 0.578, 0.188, 1)
        _WinIntensity ("Window radiance at level 1 (HDR)", Float) = 2.5
        _WinCell ("Windows: x pitch, y row pitch, z width, w height (m)", Vector) = (18, 14, 12, 5)
        _WinFade ("Windows: x pure band below (px), y full grid above (px), z mean level, w glass darkening", Vector) = (4, 8, 0.6, 0.5)
        _EngineColor ("Engine colour, EngineBlue (linear)", Color) = (0.274, 0.630, 1, 1)
        _EngineIntensity ("Engine HDR multiplier", Float) = 6
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _HullColor;
        float4 _UnderColor;
        float4 _Panel;
        float4 _Shade;
        float4 _Rim;
        float4 _WindowColor;
        float4 _WinCell;
        float4 _WinFade;
        float4 _EngineColor;
        float _Mode;
        float _Smoothness;
        float _WinIntensity;
        float _EngineIntensity;
    CBUFFER_END

    // object-space position for the panel and window patterns (identical in all passes; only the forward pass reads it)
    #define RING_HAS_VERTEX_HOOK
    void RingVertexHook(inout RingVertexData v)
    {
        v.custom = float4(v.positionOS, 0.0);
    }

    // Footprint (m) along the unit surface direction a (bent space) of the pixel footprint projected onto the surface:
    // the support of the projected pixel ellipse along a, fp sqrt(1 + (a.v)^2 / cos^2 i); cos i clamped at 0.05.
    float RingArkFpDir(RingFragData f, float3 a)
    {
        float ci = abs(dot(f.normalWS, f.viewDirWS));
        float av = dot(a, f.viewDirWS);
        return max(f.footprint * sqrt(1.0 + av * av / max(ci * ci, 0.0025)), 1e-4);
    }

    // Object axis -> bent world direction (unit).
    float3 RingArkAxis(float3 axisOS, float theta)
    {
        return RingBendRotate(TransformObjectToWorldDir(axisOS), theta);
    }

    // 15 m panel lines on the three object-space families; a family only where its axis lies in the surface.
    // Offset by half a pitch so no flat face (stern x -1 500, the y = 0 plane) sits on a line.
    float RingArkPanels(RingFragData f, float3 nO)
    {
        float pitch = max(_Panel.x, 1e-3);
        float3 p = f.custom.xyz + 0.5 * pitch;
        float3 wgt = 1.0 - smoothstep(0.5, 0.8, abs(nO));
        float gx = RingFilteredGrid1(p.x, pitch, _Panel.y, RingArkFpDir(f, RingArkAxis(float3(1.0, 0.0, 0.0), f.theta)), _Panel.w) * wgt.x;
        float gy = RingFilteredGrid1(p.y, pitch, _Panel.y, RingArkFpDir(f, RingArkAxis(float3(0.0, 1.0, 0.0), f.theta)), _Panel.w) * wgt.y;
        float gz = RingFilteredGrid1(p.z, pitch, _Panel.y, RingArkFpDir(f, RingArkAxis(float3(0.0, 0.0, 1.0), f.theta)), _Panel.w) * wgt.z;
        return saturate(1.0 - (1.0 - gx) * (1.0 - gy) * (1.0 - gz));
    }

    // Hull albedo: ArkHull above, ArkUnderside from the lower flank down (object normal), structure (vertex G) in a
    // dimmed ArkHull, macro tone +-4 % at 160 m, panel lines.
    float3 RingArkHullAlbedo(RingFragData f, float3 nO, out float groove)
    {
        float hi = _Shade.x, lo = min(_Shade.y, _Shade.x - 1e-3);
        float under = 1.0 - smoothstep(lo, hi, nO.y);
        float3 base = lerp(_HullColor.rgb, _UnderColor.rgb, under);
        base = lerp(base, _HullColor.rgb * max(_Shade.w, 0.0), saturate(f.color.g));
        float macro = RingValueNoise(f.custom.xz * (1.0 / 160.0) + 3.7);
        base *= 0.96 + 0.08 * macro;
        groove = RingArkPanels(f, nO);
        return base * (1.0 - saturate(_Panel.z) * groove);
    }

    // Unit key direction in bent space (the key vector rotates with the bend, 14.7 H10).
    float3 RingArkKeyDir(float theta)
    {
        float3 L = _KeyLightDir.xyz;
        float ll = dot(L, L);
        L = ll > 1e-8 ? L * rsqrt(ll) : float3(0.0, 1.0, 0.0);
        return RingBendRotate(L, theta);
    }

    // Star rim: the backlit sheen of the silhouette, (1 - N.V)^p on surfaces facing the star (N.L + 0.35), scaled by how
    // far the star stands behind the hull seen from the viewer (0.25 + 0.75 max(0, -V.L)), key colour, not albedo
    // (a specular sheen). Plus a weak arch bounce (sky trilight on the upper hull, ground trilight on the belly) and a
    // small self floor so no face is ever pure black.
    float3 RingArkExtra(RingFragData f, float3 albedo, float3 nO)
    {
        float NoV = saturate(dot(f.normalWS, f.viewDirWS));
        float3 Lb = RingArkKeyDir(f.theta);
        float facing = saturate(dot(f.normalWS, Lb) + 0.35);
        float behind = 0.25 + 0.75 * saturate(-dot(f.viewDirWS, Lb));
        float rimF = pow(saturate(1.0 - NoV), max(_Rim.y, 1.0));
        float3 rim = max(_KeyLightColor.rgb, 0.0) * (max(_Rim.x, 0.0) * rimF * facing * behind);
        float down = saturate(-nO.y), up = saturate(nO.y);
        float3 bounce = albedo * max(_Rim.z, 0.0) * (max(_AmbGround.rgb, 0.0) * down + max(_AmbSky.rgb, 0.0) * (0.25 + 0.5 * up));
        return rim + bounce + albedo * max(_Rim.w, 0.0);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float3 nO = TransformWorldToObjectDir(f.normalFlatWS);
        float groove;
        float3 albedo = RingArkHullAlbedo(f, nO, groove);
        float smoothness = _Smoothness * (1.0 - 0.4 * groove);
        float3 emission = float3(0.0, 0.0, 0.0);
        // downward faces reflect the lit meadow, not the horizon colour the shared RingReflection returns for rays
        // below the horizon: specular (and ambient) occlusion on the belly
        float occlusion = 1.0 - saturate(_Shade.z) * smoothstep(0.0, 0.7, -nO.y);

        [branch] if (_Mode > 1.5)
        {
            // engine nozzle interior
            albedo = _UnderColor.rgb * 0.6;
            smoothness = 0.2;
            float prof = lerp(0.45, 1.0, saturate(f.color.b));
            emission = max(_EngineColor.rgb, 0.0) * (max(_EngineIntensity, 0.0) * max(_ArkParams.z, 0.0) * prof);
        }
        else if (_Mode > 0.5)
        {
            // window band: analytic 12 x 5 m apertures in each 18 x 14 m cell, filtered, fading to the exact mean
            float2 c = f.uv.xy;                          // cell-local metres (x along the hull, flank arc from the row centre)
            float level = saturate(f.uv.w);              // per-window level (uv1.y), deterministic
            float3 aX = RingArkAxis(float3(1.0, 0.0, 0.0), f.theta);
            float3 aV = cross(f.normalWS, aX);
            float lv = length(aV);
            aV = lv > 1e-5 ? aV / lv : float3(0.0, 1.0, 0.0);
            float fpX = RingArkFpDir(f, aX);
            float fpV = RingArkFpDir(f, aV);
            float pxX = _WinCell.x / fpX;                // projected pitch along the hull (px)
            float pxY = _WinCell.y / fpV;                // projected deck pitch (px)
            float lo = _WinFade.x, hi = max(_WinFade.y, _WinFade.x + 1e-3);
            float wX = smoothstep(lo, hi, pxX);
            float wY = smoothstep(lo, hi, pxY);
            float meanX = _WinCell.z / max(_WinCell.x, 1e-3);
            float meanY = _WinCell.w / max(_WinCell.y, 1e-3);
            float covX = lerp(meanX, RingFilteredLine(c.x, _WinCell.z, fpX), wX);
            float covY = lerp(meanY, RingFilteredLine(c.y, _WinCell.w, fpV), wY);
            float cov = covX * covY;
            // the lit/dark pattern resolves only where both directions are resolved; otherwise its exact mean
            float lvl = lerp(saturate(_WinFade.z), level, min(wX, wY));
            emission = max(_WindowColor.rgb, 0.0) * (max(_WinIntensity, 0.0) * max(_ArkParams.y, 0.0) * lvl * cov);
            albedo *= 1.0 - saturate(_WinFade.w) * cov;  // glass is darker than the plating
            smoothness = lerp(smoothness, 0.55, cov);
        }

        s.albedo = albedo;
        s.smoothness = clamp(smoothness, 0.0, 0.65);
        s.occlusion = occlusion;
        s.emission = emission + RingArkExtra(f, albedo, nO);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On ZTest LEqual Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define RING_PASS_SHADOW
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #define RING_PASS_DEPTH
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #define RING_PASS_DEPTHNORMALS
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
