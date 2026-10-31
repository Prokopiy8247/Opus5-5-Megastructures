// M03 RingWater - the river ribbon (MASTER_PROMPT 14.2.6 M03, 14.5.2, DEVELOPMENT.md ADR-006). Opaque, no depth-buffer
// reads: body = lerp(TealRiver, WaterDeep, depth / 3.2) with an analytic depth from the bank SDF (the 1:3 bank of ADR-006
// continued under the waterline: TealRiver shallows along both banks, a WaterDeep core from 9.6 m out); reflection
// RingSkyColor(reflect(view, n)) x Schlick (F0 0.02), never shadowed, so the river darkens with the sky state by itself;
// two ripple layers (3 waves each, crests spread over +-15..55 degrees so they never read as a corrugated sheet)
// advected at 0.6 m/s along the river, flattened by the footprint with the lost slope variance moved into the roughness
// (floor 0.08); the key shadow darkens only the body and by at most _ShadowBody (light scatters under the surface: rail
// shadows are soft dips, not lines drawn on a floor); a key glint with the shadow map; highlight clamp 6 (no sparkle);
// foam along the analytic bank SDF (the waterline at 18 m); RingBend and RingFog like every lit material. The ribbon
// ends at the wall face in the plain (14.5.5).
// Vertex data (RiverBuilder): uv0 = (u = arc length along the river centre line, v = across offset), uv1 = flow
// direction (x, z), normal up.
Shader "Megastructure/RingWater"
{
    Properties
    {
        _Roughness ("Perceptual roughness floor", Range(0.02, 0.5)) = 0.08
        _FoamColor ("Foam colour (linear)", Color) = (0.69, 0.67, 0.62, 1)
        _FoamStrength ("Foam strength", Range(0, 1)) = 0.55
        _BankSlope ("Analytic depth per metre from the waterline", Range(0.05, 1)) = 0.3333
        _ShadowBody ("Key shadow depth on the body (0 none .. 1 full)", Range(0, 1)) = 0.3
    }

    HLSLINCLUDE
    #define RING_HAS_VERTEX_HOOK
    #define RING_HAS_CLIP_HOOK
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _FoamColor;
        float _Roughness;
        float _FoamStrength;
        float _BankSlope;
        float _ShadowBody;
    CBUFFER_END

    #include "Assets/_Game/Scripts/Runtime/Terrain/RingTerrainShared.hlsl"

    void RingVertexHook(inout RingVertexData v)
    {
        v.positionWS.x = RingPlainClampX(v.positionWS.x);
        v.positionWS.y = RING_WATER_Y;
        v.normalWS = float3(0.0, 1.0, 0.0);
        v.tangentWS = float4(1.0, 0.0, 0.0, 1.0);
    }

    void RingClipHook(RingFragData f)
    {
        RingPlainClip(f.positionFlatWS.x);
    }

    // Ripple waves: (m, kv, slope amplitude A k). Along-flow wavenumber ku = 2 pi m / 43.2 m; 0.6 m/s x 7200 s = 4320 m
    // = 100 x 43.2 m, so the advected pattern is exactly periodic with the clock wrap (Part 10 #31). Crest angles
    // atan(kv / ku) = +55, -34, +15 (layer 1, 3.6 - 2.3 m) and -37, +35, -14 degrees (layer 2, 1.05 - 0.61 m).
    #define B12_RIPPLE_N 6
    static const float3 B12_RIPPLES[B12_RIPPLE_N] =
    {
        float3(7.0, 1.45, 0.032), float3(12.0, -1.20, 0.032), float3(18.0, 0.70, 0.028),
        float3(33.0, -3.60, 0.030), float3(49.0, 4.90, 0.026), float3(68.0, -2.40, 0.022)
    };

    // Slope (dh/du, dh/dv) in river coordinates, flattened by the footprint fp; the filtered slope variance is added to
    // a2 (GGX alpha^2), so distant water turns into a stable broad highlight instead of sparkling (research 07 section 8).
    float2 B12WaterRipples(float u, float v, float t, float fp, inout float a2)
    {
        float uf = u - RING_FLOW * t;
        float2 slope = float2(0.0, 0.0);
        float lost = 0.0;
        [unroll] for (int i = 0; i < B12_RIPPLE_N; i++)
        {
            float3 w = B12_RIPPLES[i];
            float ku = RING_TWO_PI * w.x / RING_RIPPLE_P;
            float kv = w.y;
            float k = sqrt(ku * ku + kv * kv);
            float ph = RING_TWO_PI * frac(w.x * uf / RING_RIPPLE_P) + kv * v + 1.7 * i;
            float att = 1.0 - smoothstep(0.6, 2.2, k * fp);
            slope += (w.z * cos(ph) * att / k) * float2(ku, kv);
            lost += (1.0 - att * att) * w.z * w.z * 0.5;
        }
        a2 += 2.0 * lost;
        return slope;
    }

    // Fills the diffuse part (body and foam) and leaves the specular parameters in f.custom for WaterFrag:
    // x = GGX alpha^2 after ripple filtering, y = sky-reflection occlusion, z = foam, w = 1 (water).
    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float2 p = f.positionFlatWS.xz;
        float fpw = RingSurfaceFootprint(p, f.footprint);
        // distance from the waterline (m, > 0 on the water) and the analytic depth of the 1:3 bank continued below it
        float bank = RING_WATERLINE_D - RingRiverDist(p);
        float depth = max(bank, 0.0) * _BankSlope;

        float r = max(_Roughness, 0.02);
        float alpha = r * r;
        float a2 = alpha * alpha;
        float t = _RingTime.x;
        float2 sl = B12WaterRipples(f.uv.x, f.uv.y, t, fpw, a2);
        float2 flow = f.uv.zw;
        flow = flow / max(length(flow), 1e-4);
        float2 across = float2(-flow.y, flow.x);
        float2 grad = sl.x * flow + sl.y * across;
        float3 nFlat = normalize(float3(-grad.x, 1.0, -grad.y));
        f.normalWS = normalize(RingBendRotate(nFlat, f.theta));

        float F = RingSchlick02(saturate(dot(f.normalWS, f.viewDirWS)));
        float3 body = lerp(PAL_TealRiver, PAL_WaterDeep, saturate(depth / 3.2)) * (1.0 - F);

        // bank foam along the waterline: 0 .. 0.9 m out (0.3 m deep on the 1:3 bank), broken by advected noise, gone once
        // the band is sub-footprint (no shimmer); periodic with the clock wrap (2400 cells of 1.8 m = 4320 m)
        float band = smoothstep(-0.10, 0.10, bank) * (1.0 - smoothstep(0.20, 0.90, bank));
        float n = RingValueNoisePX(float2((f.uv.x - RING_FLOW * t) * (1.0 / 1.8), f.uv.y * (1.0 / 1.8)), 2400);
        float foam = band * saturate(0.35 + 0.9 * n) * saturate(1.5 - fpw / 1.2) * _FoamStrength;

        s.albedo = lerp(body, _FoamColor.rgb, foam);
        s.smoothness = 0.0;
        s.metallic = 0.0;
        s.occlusion = 1.0;
        f.custom = float4(a2, RingBridgeReflOcc(p), foam, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment WaterFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            #include "Include/RingLitPass.hlsl"

            // RingFrag (RingLitPass.hlsl) with the softened body shadow and the water specular added before RingFog.
            half4 WaterFrag(RingVaryings i) : SV_Target0
            {
                RingFragData f = RingMakeFrag(i);
                RingClipHook(f);
                SurfaceData s = (SurfaceData)0;
                s.albedo = half3(0.5, 0.5, 0.5);
                s.alpha = 1.0h;
                s.metallic = 0.0h;
                s.specular = half3(0, 0, 0);
                s.smoothness = 0.0h;
                s.occlusion = 1.0h;
                s.emission = half3(0, 0, 0);
                s.normalTS = half3(0, 0, 1);
                RingSurfaceHook(f, s);

                InputData inputData = (InputData)0;
                inputData.positionWS = f.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(f.normalWS);
                inputData.viewDirectionWS = f.viewDirWS;
                inputData.normalizedScreenSpaceUV = f.screenUV;
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(f.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.bakedGI = RingAmbient(inputData.normalWS, f.theta);

                half3 c = RingShade(inputData, s, f.positionFlatWS, f.theta);

                Light ml = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                float3 L = RingBendRotate(ml.direction, f.theta);
                float3 keyCol = ml.color;
                if (_RingSet.x > 1.5) keyCol *= saturate(f.positionFlatWS.x - 233.0);   // as RingShade: L7 never reaches inside the rim
                float keyA = ml.distanceAttenuation * RingKeyAtten(f.positionFlatWS.xz);
                float sh = ml.shadowAttenuation;
                // RingShade lit the body with the full key shadow; give back (1 - _ShadowBody) of the diffuse the shadow
                // removed (URP dielectric diffuse = albedo x 0.96), so a shadow dims the body by at most _ShadowBody
                float NoL = saturate(dot(inputData.normalWS, L));
                c += (half3)(s.albedo * 0.96 * keyCol * (keyA * NoL * (1.0 - sh) * (1.0 - _ShadowBody)));

                float3 upB = RingBendRotate(float3(0.0, 1.0, 0.0), f.theta);
                float3 spec = RingWaterSpecular(inputData.normalWS, f.viewDirWS, upB, L, keyCol * (sh * keyA), f.custom.x, f.custom.y);
                c += (half3)(spec * (1.0 - f.custom.z));
                c = min(c, 6.0h);
                c = RingFog(c, f.positionWS, f.positionFlatWS.y);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
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
            ZWrite On ColorMask R Cull Back
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
            ZWrite On Cull Back
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
