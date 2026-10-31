// M08 RingTunnel - the wall tunnel (14.5.6, 14.2.6): TunnelGrey, smoothness 0.2, vertex R = AO, lit by the amber
// strips (AmberLamp x 0.5 strips in TunnelAmber): an analytic line-light (two finite strips along the floor, exact
// finite-length integral, emitter and receiver cosines, distance falloff), a floor bleed beside each strip, soft
// halos round the 40 markers and a faint warm bounce fill so the darkest corners never crush (luma >= 6 at EV +2).
// Real lights: the shared lit model (RingLighting, Forward+) with one change for a tube open at both ends: every
// directional light (key, arch fill, rim wash) is occluded analytically - a fragment inside the tube is lit only if
// its ray toward the light leaves through the door (x 114) or the mouth (x 234) inside the clear profile (soft
// penumbra of the 1.2 degree star) - and the sky ambient / reflection fade with the solid angle of the openings.
// So the outside wash never lights the whole floor seen through the door, and the key L7 enters only at the mouth.
// The shell also casts shadows on both sides (ShadowCaster Cull Off) for the rib shadows near the mouth.
// The beat-5 door recess (DoorRecess) uses this shader too, in wall ceramic with the strip / marker / fill gains at 0,
// so the same occlusion keeps the beat-7 key L7 (unshadowed beyond the 90 m shadow distance) out of it.
// Variation along the 120 m (B56, critic S3 "start, KS10 and mid look identical"): every 5th rib (k = 4, 9, 14, 19,
// one per 30 m; vertex colour B = 1) is a section rib with a lighter, warmer trim albedo (_TrimColor), and an amber
// floor rung lies between the strips at every section rib and at every rib of the last 30 m (k >= 15, the "ladder"
// into the light); the rungs add a floor bleed (_Rung) like the strips. The numbered markers are geometry (builder).
// RingBend in all four passes (RingLitPass vertex path); RingFog last (tunnel fog sigma 4e-3, TunnelFog).
Shader "Megastructure/RingTunnel"
{
    Properties
    {
        _BaseColor ("Albedo, TunnelGrey (linear)", Color) = (0.023, 0.026, 0.037, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.2
        _AO ("Vertex AO strength", Range(0, 1)) = 1
        [HDR] _StripColor ("Strip light colour, AmberLamp (linear)", Color) = (1, 0.451, 0.063, 1)
        _StripLight ("Strip light gain", Float) = 16
        _Strip ("Strips: x |z - axis|, y height, z width, w near-field softening (m)", Vector) = (0.97, 0.006, 0.06, 0.03)
        _StripFalloff ("Strip light distance falloff (m)", Float) = 1
        _Bleed ("Floor bleed: x gain, y width (m), z height window (m)", Vector) = (0.065, 0.12, 0.08, 0)
        _Axis ("x axis z, y strip start x, z strip end x", Vector) = (660, 114.85, 233.95, 0)
        _MarkerLight ("Marker halo gain", Float) = 1.3
        _Marker ("Markers: x first rib x, y pitch, z last rib index, w |z - axis| of the marker face", Vector) = (117, 6, 19, 1.038)
        _MarkerShape ("Markers: x centre height, y halo sigma (m)", Vector) = (0.5, 0.18, 0, 0)
        [HDR] _FillColor ("Bounce fill colour (linear)", Color) = (1, 0.8, 0.62, 1)
        _FillLight ("Bounce fill gain", Float) = 0.032
        _TrimColor ("Section-rib trim albedo, vertex B (linear)", Color) = (0.023, 0.026, 0.037, 1)
        _Rung ("Floor rungs: x bleed gain, y bleed falloff (m), z half length |z - axis| (m)", Vector) = (0, 0.1, 0.94, 0)
        _RungSel ("Rung ribs: x section period (ribs), y phase, z ladder from rib index", Vector) = (5, 4, 15, 0)
        _Openings ("x door plane, y mouth plane, z openness radius (m)", Vector) = (114, 234, 1.3, 0)
        _Profile ("Clear profile: x half width, y straight wall height (crown r = half width)", Vector) = (1.3, 1.7, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _ShadowCull ("Shadow caster cull", Float) = 0
    }

    HLSLINCLUDE
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _StripColor;
        float4 _Strip;
        float4 _Bleed;
        float4 _Axis;
        float4 _Marker;
        float4 _MarkerShape;
        float4 _FillColor;
        float4 _Openings;
        float4 _Profile;
        float4 _TrimColor;
        float4 _Rung;
        float4 _RungSel;
        float _Smoothness;
        float _AO;
        float _StripLight;
        float _StripFalloff;
        float _MarkerLight;
        float _FillLight;
    CBUFFER_END

    // Signed distance (m) to the clear profile in section coordinates (q.x across, q.y up); negative inside.
    // Union of the wall box (|x| <= hw, 0 <= y <= straight) and the crown disc (r = hw at (0, straight)).
    // C# twin: TunnelProfile.Sdf.
    float RingTunnelProfileSdf(float2 q)
    {
        float hw = _Profile.x, st = _Profile.y;
        float2 d = abs(q - float2(0.0, 0.5 * st)) - float2(hw, 0.5 * st);
        float box = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
        float disc = length(q - float2(0.0, st)) - hw;
        return min(box, disc);
    }

    // Analytic irradiance factor of the two amber strips (finite line lights along +X, emitting upward).
    float RingTunnelStrips(float3 p, float3 n)
    {
        float e = 0.0;
        [unroll] for (int k = 0; k < 2; k++)
        {
            float side = k == 0 ? -1.0 : 1.0;
            float2 sp = float2(_Axis.x + side * _Strip.x, _Strip.y);        // strip centre line (z, y)
            float2 v = sp - float2(p.z, p.y);                                // receiver -> strip, across the tube
            float d2 = dot(v, v) + _Strip.w * _Strip.w;                      // near-field softening (no 1/0)
            float d = sqrt(d2);
            float2 vh = v / d;
            float cr = saturate(dot(float2(n.z, n.y), vh)) + 0.45 * abs(n.x);   // receiver cosine (+ faces along x)
            float ce = 0.3 + 0.7 * saturate(-vh.y);                              // emitter faces +Y (+ its 6 mm sides)
            // exact finite-length factor: integral of d^2 / (d^2 + t^2)^2 over the strip, normalised by the infinite line
            float a = _Axis.y - p.x, b = _Axis.z - p.x;
            float Fa = atan(a / d) + a * d / (d2 + a * a);
            float Fb = atan(b / d) + b * d / (d2 + b * b);
            float len = saturate((Fb - Fa) * (1.0 / RING_PI));
            e += cr * ce * len * (_Strip.z / (2.0 * d)) * exp(-d / max(_StripFalloff, 1e-3));
            // floor bleed beside the strip (up-facing surfaces at floor level)
            float lateral = abs(p.z - sp.x) - 0.5 * _Strip.z;
            float atFloor = 1.0 - smoothstep(0.0, max(_Bleed.z, 1e-3), p.y - sp.y);
            float inStrip = smoothstep(_Axis.y - 0.3, _Axis.y + 0.3, p.x) * (1.0 - smoothstep(_Axis.z - 0.3, _Axis.z + 0.3, p.x));
            e += saturate(n.y) * _Bleed.x * exp(-max(lateral, 0.0) / max(_Bleed.y, 1e-3)) * atFloor * inStrip;
        }
        return e;
    }

    // Soft halos of the two markers on the nearest rib (0.2 m squares at 0.5 m on the rib inner faces).
    float RingTunnelMarkers(float3 p, float3 n)
    {
        float k = clamp(round((p.x - _Marker.x) / max(_Marker.y, 1e-3)), 0.0, _Marker.z);
        float xr = _Marker.x + _Marker.y * k;
        float sigma = max(_MarkerShape.y, 1e-3);
        float e = 0.0;
        [unroll] for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1.0 : 1.0;
            float3 m = float3(xr, _MarkerShape.x, _Axis.x + side * _Marker.w);
            float3 v = m - p;
            float dm = max(length(v), 1e-4);
            float g = exp(-dm * dm / (2.0 * sigma * sigma));
            e += g * (0.4 + 0.6 * saturate(dot(n, v / dm)));
        }
        return e;
    }

    // Floor bleed of the amber rung on the nearest rib, if that rib carries one (section ribs k = phase mod period,
    // and every rib from the ladder index on): up-facing surfaces at floor level between the strips.
    float RingTunnelRungs(float3 p, float3 n)
    {
        float k = clamp(round((p.x - _Marker.x) / max(_Marker.y, 1e-3)), 0.0, _Marker.z);
        float per = max(_RungSel.x, 1.0);
        float on = (abs(k - per * floor(k / per) - _RungSel.y) < 0.5 || k > _RungSel.z - 0.5) ? 1.0 : 0.0;
        float xr = _Marker.x + _Marker.y * k;
        float lateral = abs(p.z - _Axis.x) - _Rung.z;
        float atFloor = 1.0 - smoothstep(0.0, max(_Bleed.z, 1e-3), p.y - _Strip.y);
        return on * saturate(n.y) * _Rung.x * exp(-abs(p.x - xr) / max(_Rung.y, 1e-3))
            * exp(-max(lateral, 0.0) / max(_Bleed.y, 1e-3)) * atFloor;
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float3 p = f.positionFlatWS;
        float3 n = f.normalFlatWS;
        float ao = lerp(1.0, saturate(f.color.r), _AO);
        float3 alb = lerp(_BaseColor.rgb, _TrimColor.rgb, saturate(f.color.b)) * lerp(0.75, 1.0, ao);
        float3 e = _StripColor.rgb * (_StripLight * RingTunnelStrips(p, n) + _MarkerLight * RingTunnelMarkers(p, n)
            + RingTunnelRungs(p, n));
        e += _FillColor.rgb * _FillLight;
        s.albedo = alb;
        s.smoothness = clamp(_Smoothness, 0.0, 0.65);
        s.occlusion = ao;
        s.emission = alb * e * ao;     // the strips' light, reflected by the grey surface (emissive only in beat 6)
    }

    // Fraction of a directional light (flat-space direction L toward the light) that reaches p through an opening.
    float RingTunnelVisibility(float3 p, float3 L)
    {
        if (p.x <= _Openings.x || p.x >= _Openings.y) return 1.0;
        float lx = L.x;
        if (abs(lx) < 1e-4) lx = lx < 0.0 ? -1e-4 : 1e-4;
        float xe = lx < 0.0 ? _Openings.x : _Openings.y;
        float t = (xe - p.x) / lx;                                       // >= 0: distance factor to the exit plane
        float2 q = float2(_Axis.x - (p.z + t * L.z), p.y + t * L.y);     // section coordinates at the exit plane
        float sdf = RingTunnelProfileSdf(q);
        float soft = max(0.01, 0.042 * t * length(L));                   // penumbra of a 1.2 degree star: 2 t tan 1.2 deg
        return saturate(0.5 - sdf / soft);
    }

    // Openness to the sky: solid-angle falloff away from the nearer opening (1 at the opening plane).
    float RingTunnelOpenness(float3 p)
    {
        float dd = max(p.x - _Openings.x, 0.0), dm = max(_Openings.y - p.x, 0.0);
        float r2 = max(_Openings.z * _Openings.z, 1e-4);
        return saturate(max(r2 / (r2 + dd * dd), r2 / (r2 + dm * dm)));
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
            #pragma fragment RingTunnelFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            #include "Include/RingLitPass.hlsl"

            // RingShade (RingLighting.hlsl) with the opening visibility on every directional light and the
            // openness on the environment term; everything else is identical (bent key vector, Trilight, sky reflections).
            half3 RingTunnelShade(InputData inputData, SurfaceData surfaceData, float3 positionFlatWS, float theta)
            {
                BRDFData brdfData;
                InitializeBRDFData(surfaceData, brdfData);
                const BRDFData noClearCoat = (BRDFData)0;
                const half4 shadowMask = half4(1, 1, 1, 1);
                float open = RingTunnelOpenness(positionFlatWS);

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
                mainLight.shadowAttenuation *= RingKeyAtten(positionFlatWS.xz) * RingTunnelVisibility(positionFlatWS, mainLight.direction);
                mainLight.direction = RingBendRotate(mainLight.direction, theta);

                half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
                half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                half fresnelTerm = Pow4(1.0h - NoV);
                half3 indirectSpecular = RingReflection(reflectVector, brdfData.perceptualRoughness);
                half3 color = EnvironmentBRDF(brdfData, inputData.bakedGI, indirectSpecular, fresnelTerm) * (surfaceData.occlusion * open);

                color += LightingPhysicallyBased(brdfData, noClearCoat, mainLight, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    light.shadowAttenuation *= RingTunnelVisibility(positionFlatWS, light.direction);
                    light.direction = RingBendRotate(light.direction, theta);
                    color += LightingPhysicallyBased(brdfData, noClearCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    color += LightingPhysicallyBased(brdfData, noClearCoat, light, inputData.normalWS, inputData.viewDirectionWS, 0.0h, false);
                LIGHT_LOOP_END
            #endif

                color += surfaceData.emission;
                return min(color, 1000.0);
            }

            // The RingLitPass forward fragment with RingTunnelShade instead of RingShade.
            half4 RingTunnelFrag(RingVaryings i) : SV_Target0
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

                half3 c = RingTunnelShade(inputData, s, f.positionFlatWS, f.theta);
                c = RingFog(c, f.positionWS, f.positionFlatWS.y);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_ShadowCull]
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
