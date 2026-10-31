// RingUnderpass - lining and ribs of the beat-4 underpass (14.5.4: TunnelLoft 2.8 x 2.9, legs 18 + 18 m, sealed mitred
// corner, ribs every 3 m, amber strips). Ceramic lining (RingCeramic recipe: triplanar filtered joints, vertex
// R = AO, G = edge wear) plus two baked terms written per vertex by LeveeMeshes:
//   B = amber strip irradiance 0..1 (the EmissiveAmber strips' light, reflected: emission = albedo x AmberLamp x gain x B)
//   A = exterior exposure 1 at the portals -> 0 from 2.2 m inside.
// The forward pass is the shared lit model of RingLighting.hlsl (RingShade, mirrored line by line) with one change for
// a closed tube: every DIRECTIONAL light (the key L, the arch fill and the rim wash, none of which has a shadow inside
// the levee except the key's map) is weighted by A, so the night sky never lights the inside, the portal faces match
// the riser outside, and the world swap in the corner (s 576, 14.6) changes nothing in the tube's own lighting. (It
// does not hide what is seen through the portals: the straight legs show the exit portal from the mitre at camera yaw
// >= 39 deg at 16:9, so the director must keep the view through it unchanged at the swap.) Ambient and
// environment reflection are attenuated by the baked AO (deep inside 0.14). Local lights are untouched (Forward+).
// RingBend in all four passes (RingLitPass vertex path); RingFog last; one UnityPerMaterial CBUFFER (SRP Batcher).
// The same shader dresses the other masonry of beats 3-4 (exposure A = 1 there, so it is the plain shared lit model),
// selected by _Masonry.x (uniform branch, no keywords):
//   0 tiles  : the lining grid above (_PanelSize, _PanelLine).
//   1 courses: stone courses in running bond (_PanelSize = block length x course height, every other course shifted by
//              half a block), triplanar on the dominant axis, joints box-filtered with the pixel footprint (fade below
//              4 px spacing), a per-block tone of +-_Masonry.y (hashed per block, faded out between 4 and 2 px of
//              course height so it never sparkles) and _Masonry.z extra darkening in the joints (their recess).
//   2 turf   : no joints; a two-octave mottle (0.8 m and 3.2 m, amplitude _Masonry.y, each octave faded between 4 and
//              2 px) that also shifts the hue toward dry grass, so a LeveeGreen surface reads as turf, not paint.
// Levee sconces (_SconceParams.w > 0): the eight WarmLamp sconces of the levee's south face (LeveeMeshes.Sconces, set
// per material as _Sconce0.._Sconce7: xyz = the virtual emitting point 0.8 m in front of the riser, w = gain) light
// the surface they wash in the shader, as albedo x _SconceColor x sum(gain N.L (1 - b + b down) window / (d^2 + r0^2))
// x the lamp level _Lamps.x / 3, window (1 - d^2 / R^2)^2 with R = _SconceParams.x (smooth zero at R), r0^2 =
// _SconceParams.y, downlight bias b = _SconceParams.z. Unlike additive pool quads this reveals the stone courses and
// the turf mottle inside the scallops, and it needs no extra geometry on the faces.
Shader "Megastructure/RingUnderpass"
{
    Properties
    {
        _BaseColor ("Base colour (linear)", Color) = (0.42, 0.40, 0.36, 1)
        _JointColor ("Joint colour (linear)", Color) = (0.30, 0.28, 0.25, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.4
        _PanelSize ("Panel size (m)", Vector) = (1.5, 0.75, 1.5, 0)
        _PanelLine ("Panel line width (m)", Float) = 0.008
        _AO ("Vertex AO strength", Range(0, 1)) = 1
        _EdgeWear ("Edge wear strength", Range(0, 1)) = 0.6
        _StripColor ("Strip light colour, AmberLamp (linear)", Color) = (1, 0.451, 0.063, 1)
        _StripLight ("Strip light gain (x vertex B)", Float) = 0.12
        _FillColor ("Bounce fill colour (linear)", Color) = (1, 0.8, 0.62, 1)
        _FillLight ("Bounce fill gain", Float) = 0.004
        _Masonry ("Masonry: x mode (0 tiles, 1 courses, 2 turf), y tone +-, z joint recess darkening, w unused", Vector) = (0, 0, 0, 0)
        _SconceColor ("Sconce light colour (linear)", Color) = (1, 0.518, 0.153, 1)
        _SconceParams ("Sconces: x window radius (m), y r0^2 (m^2), z downlight bias, w gain (0 off)", Vector) = (4.5, 0.3, 0.7, 0)
        _Sconce0 ("Sconce 0 (xyz flat world, w gain)", Vector) = (0, -1000, 0, 0)
        _Sconce1 ("Sconce 1", Vector) = (0, -1000, 0, 0)
        _Sconce2 ("Sconce 2", Vector) = (0, -1000, 0, 0)
        _Sconce3 ("Sconce 3", Vector) = (0, -1000, 0, 0)
        _Sconce4 ("Sconce 4", Vector) = (0, -1000, 0, 0)
        _Sconce5 ("Sconce 5", Vector) = (0, -1000, 0, 0)
        _Sconce6 ("Sconce 6", Vector) = (0, -1000, 0, 0)
        _Sconce7 ("Sconce 7", Vector) = (0, -1000, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Assets/_Game/Shaders/Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _JointColor;
        float4 _PanelSize;
        float4 _StripColor;
        float4 _FillColor;
        float4 _Masonry;
        float4 _SconceColor;
        float4 _SconceParams;
        float4 _Sconce0, _Sconce1, _Sconce2, _Sconce3, _Sconce4, _Sconce5, _Sconce6, _Sconce7;
        float _Smoothness;
        float _PanelLine;
        float _AO;
        float _EdgeWear;
        float _StripLight;
        float _FillLight;
    CBUFFER_END

    // Triplanar filtered joints in flat world space (as RingCeramic; camera-near coordinates)
    float RingUnderpassGrooves(float3 p, float3 n, float fp)
    {
        float3 an = abs(n);
        float2 q; float2 size;
        if (an.y >= an.x && an.y >= an.z) { q = p.xz; size = _PanelSize.xz; }
        else if (an.x >= an.z) { q = p.zy; size = _PanelSize.zy; }
        else { q = p.xy; size = _PanelSize.xy; }
        float gx = RingFilteredGrid1(q.x, max(size.x, 1e-3), _PanelLine, fp, 4.0);
        float gy = RingFilteredGrid1(q.y, max(size.y, 1e-3), _PanelLine, fp, 4.0);
        return saturate(gx + gy - gx * gy);
    }

    // Triplanar plane of the dominant normal axis: q.x along the face, q.y up (walls) or z (tops), with the matching
    // panel size components.
    void RingMasonryPlane(float3 p, float3 n, out float2 q, out float2 size)
    {
        float3 an = abs(n);
        if (an.y >= an.x && an.y >= an.z) { q = p.xz; size = _PanelSize.xz; }
        else if (an.x >= an.z) { q = p.zy; size = _PanelSize.zy; }
        else { q = p.xy; size = _PanelSize.xy; }
        size = max(size, 1e-3);
    }

    // _Masonry.x = 1: running-bond courses; returns the joint coverage, outputs the per-block tone offset.
    float RingMasonryCourses(float3 p, float3 n, float fp, out float tone)
    {
        float2 q, sz;
        RingMasonryPlane(p, n, q, sz);
        float c = floor(q.y / sz.y);
        float qx = q.x + 0.5 * sz.x * (c - 2.0 * floor(0.5 * c));       // every other course shifted by half a block
        float gx = RingFilteredGrid1(qx, sz.x, _PanelLine, fp, 4.0);
        float gy = RingFilteredGrid1(q.y, sz.y, _PanelLine, fp, 4.0);
        float h = RingHash21(int2((int)floor(qx / sz.x), (int)c)) - 0.5;
        float px = min(sz.x, sz.y) / max(fp, 1e-5);                     // course height in pixels
        tone = 2.0 * h * _Masonry.y * saturate((px - 2.0) * 0.5);
        return saturate(gx + gy - gx * gy);
    }

    // _Masonry.x = 2: turf mottle (0.8 m and 3.2 m octaves, each faded between 4 and 2 px); outputs the dry-grass weight.
    float RingMasonryTurf(float3 p, float3 n, float fp, out float dry)
    {
        float2 q, sz;
        RingMasonryPlane(p, n, q, sz);
        float ifp = 1.0 / max(fp, 1e-5);
        float a1 = saturate((0.8 * ifp - 2.0) * 0.5);
        float a2 = saturate((3.2 * ifp - 2.0) * 0.5);
        float n1 = RingValueNoise(q * 1.25 + 11.3) - 0.5;
        float n2 = RingValueNoise(q * 0.3125 - 5.1) - 0.5;
        dry = saturate(0.5 + 1.6 * n2 * a2);
        return 2.0 * _Masonry.y * (0.6 * n1 * a1 + n2 * a2);
    }

    // Irradiance factor of the levee sconces at flat position p with flat normal n (0 when off or in daylight).
    float3 RingSconceLight(float3 p, float3 n)
    {
        float lvl = saturate(_Lamps.x * (1.0 / 3.0)) * max(_SconceParams.w, 0.0);
        float3 r = float3(0.0, 0.0, 0.0);
        [branch] if (lvl > 0.0)
        {
            float4 sc[8] = { _Sconce0, _Sconce1, _Sconce2, _Sconce3, _Sconce4, _Sconce5, _Sconce6, _Sconce7 };
            float R2 = max(_SconceParams.x * _SconceParams.x, 1e-2);
            float r02 = max(_SconceParams.y, 1e-3);
            float b = saturate(_SconceParams.z);
            float e = 0.0;
            [unroll] for (int i = 0; i < 8; i++)
            {
                float3 l = sc[i].xyz - p;
                float d2 = dot(l, l);
                float invD = rsqrt(max(d2, 1e-6));
                float ndl = saturate(dot(n, l) * invD);
                float down = saturate(l.y * invD);
                float win = saturate(1.0 - d2 / R2);
                e += max(sc[i].w, 0.0) * ndl * (1.0 - b + b * down) * win * win / (d2 + r02);
            }
            r = max(_SconceColor.rgb, 0.0) * (e * lvl);
        }
        return r;
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float macro = RingValueNoise(f.positionFlatWS.xz / 7.0 + f.positionFlatWS.y / 5.0);
        float3 base = _BaseColor.rgb * (0.97 + 0.06 * macro);
        float groove = 0.0;
        [branch] if (_Masonry.x > 1.5)
        {
            float dry;
            float m = RingMasonryTurf(f.positionFlatWS, f.normalFlatWS, f.footprint, dry);
            base *= saturate(1.0 + m);
            base = lerp(base, base * float3(1.3, 1.08, 0.55), 0.35 * dry);
        }
        else if (_Masonry.x > 0.5)
        {
            float tone;
            groove = RingMasonryCourses(f.positionFlatWS, f.normalFlatWS, f.footprint, tone);
            base *= saturate(1.0 + tone);
        }
        else groove = RingUnderpassGrooves(f.positionFlatWS, f.normalFlatWS, f.footprint);
        float ao = lerp(1.0, saturate(f.color.r), _AO);
        float wear = saturate(f.color.g) * _EdgeWear;
        float3 alb = lerp(base, _JointColor.rgb, groove * 0.85) * (1.0 - saturate(_Masonry.z) * groove);
        alb = lerp(alb, alb * 1.04 + 0.01, wear);
        alb *= lerp(0.82, 1.0, ao);
        float strip = saturate(f.color.b);
        s.albedo = alb;
        s.smoothness = min(_Smoothness * (1.0 - 0.35 * groove) * (1.0 - 0.25 * wear), 0.65);
        s.occlusion = ao;
        s.emission = alb * (max(_StripColor.rgb, 0.0) * (max(_StripLight, 0.0) * strip) + max(_FillColor.rgb, 0.0) * max(_FillLight, 0.0)
                            + RingSconceLight(f.positionFlatWS, f.normalFlatWS));
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
            #pragma fragment RingUnderpassFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"

            // RingShade (RingLighting.hlsl) with the directional lights weighted by the exterior exposure expo.
            half3 RingUnderpassShade(InputData inputData, SurfaceData surfaceData, float3 positionFlatWS, float theta, half expo)
            {
                BRDFData brdfData;
                InitializeBRDFData(surfaceData, brdfData);
                const BRDFData noClearCoat = (BRDFData)0;
                const half4 shadowMask = half4(1, 1, 1, 1);

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
                mainLight.direction = RingBendRotate(mainLight.direction, theta);
                mainLight.shadowAttenuation *= RingKeyAtten(positionFlatWS.xz) * expo;

                half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
                half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half fresnelTerm = Pow4(1.0h - NoV);
                half3 indirectSpecular = RingReflection(reflectVector, brdfData.perceptualRoughness);
                half3 color = EnvironmentBRDF(brdfData, inputData.bakedGI, indirectSpecular, fresnelTerm) * surfaceData.occlusion;

                color += LightingPhysicallyBased(brdfData, noClearCoat, mainLight, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                // non-main directional lights (arch fill, rim wash) are not in the clusters: weighted by the exposure
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    light.direction = RingBendRotate(light.direction, theta);
                    light.distanceAttenuation *= expo;
                    color += LightingPhysicallyBased(brdfData, noClearCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
                }
                half localWeight = 1.0h;     // clustered loop below: local lights only
                #else
                half localWeight = expo;     // without clusters the loop below also holds the directional fills
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    light.distanceAttenuation *= localWeight;
                    color += LightingPhysicallyBased(brdfData, noClearCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
                LIGHT_LOOP_END
            #endif

                color += surfaceData.emission;
                return min(color, 1000.0);
            }

            half4 RingUnderpassFrag(RingVaryings i) : SV_Target0
            {
                RingFragData f = RingMakeFrag(i);
                SurfaceData s = (SurfaceData)0;
                s.albedo = half3(0.5, 0.5, 0.5);
                s.alpha = 1.0h;
                s.metallic = 0.0h;
                s.specular = half3(0, 0, 0);
                s.smoothness = 0.5h;
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

                half expo = (half)saturate(f.color.a);
                half3 c = RingUnderpassShade(inputData, s, f.positionFlatWS, f.theta, expo);
                c = RingFog(c, f.positionWS, f.positionFlatWS.y);
                return half4(c, 1.0h);
            }
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
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
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
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
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
            #include "Assets/_Game/Shaders/Include/RingLitPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
