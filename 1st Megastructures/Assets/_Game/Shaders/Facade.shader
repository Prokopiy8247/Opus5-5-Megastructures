// =====================================================================================================================
// Megastructure/Facade - the vertical city windows (14.5.5, 14.7.3).
//   _Recipe 0 (M_Facade, shader tier): one quad per wall chunk. Window grid (cell 3.0 x 3.5 m, window 1.2 x 1.6 m) as a
//      box-filtered mask with fake depth: reveal darkening 0.35 at the window top fading to 0 at the bottom, +/-12 %
//      room tint by hash. When a cell is under 7 px the pattern blends linearly to its mean, complete at 4 px.
//   _Recipe 1 (M_FacadeModule, near tier): the module meshes with real chamfered reveals; vertex colour B = 255 marks
//      glass faces, everything else is the concrete frame. Same window function, so lit windows match across tiers.
// Window identity is a WORLD-SPACE cell index, never a per-mesh id: face k = round((atan2(z - cz, x - cx) - 30 deg) /
// 60 deg) mod 6 around the shaft centre _GridOrigin.xy, u = signed distance along face k from its centre (tangent at
// face angle + 90 deg), v = y - _GridOrigin.z. Cell (col, row) = (floor(u / 3), floor(v / 3.5)); the window occupies
// u in [3 col + 0.9, 3 col + 2.1], v in [3.5 row + 0.95, 3.5 row + 2.55]. Lit = PCG(face, col, row) < 6 %, of which
// 85 % WIN_COLD and 15 % AMBER, emissive 2.5-5.0; static in time. See docs/SHADERS.md for the module-mesh contract.
// Passes: ForwardLit, ShadowCaster, DepthOnly, DepthNormals (dithered _AbFade tier cross-fade in all of them).
// _DownViewAtten (default 1 = opted in) x the global weight _AbDownView.x (unset = 0 = off): downward-view attenuation of
// the window emission and darkening of the concrete below the deck (R1-01, AbDownViewEmission in AbyssCommon.hlsl).
// =====================================================================================================================
Shader "Megastructure/Facade"
{
    Properties
    {
        [Enum(ShaderTier,0,Module,1)] _Recipe ("Mode", Float) = 0
        [MainColor] _BaseColor ("Concrete", Color) = (0.365, 0.384, 0.404, 1)
        _GlassColor ("Glass (unlit)", Color) = (0.043, 0.067, 0.090, 1)
        _WinCold ("Window Cold", Color) = (0.624, 0.769, 0.910, 1)
        _WinAmber ("Window Amber", Color) = (1.0, 0.706, 0.353, 1)
        _LitFraction ("Lit Fraction", Range(0, 1)) = 0.06
        _AmberFraction ("Amber Fraction of Lit", Range(0, 1)) = 0.15
        _EmissiveRange ("Emissive Min/Max", Vector) = (2.5, 5.0, 0, 0)
        _EmissionStrength ("Emission Multiplier", Float) = 1
        _GridOrigin ("Grid Origin (centre x, centre z, top y)", Vector) = (0, 410, -60, 0)
        _Roughness ("Concrete Roughness", Range(0, 1)) = 0.9
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 0
        _AbFade ("Tier Fade (dithered)", Float) = 1
        _ThinLine ("ThinLine min width (px, 0 = off)", Float) = 0
        _DistFade ("Distance Fade (start, end, mode)", Vector) = (0, 0, 0, 0)
        _KeyLightMask ("Key Light Mask (main light only)", Range(0, 1)) = 1
        _DownViewAtten ("Down-View Attenuation opt-in (x global _AbDownView.x)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _GlassColor;
            float4 _WinCold;
            float4 _WinAmber;
            float4 _EmissiveRange;
            float4 _GridOrigin;
            float4 _DistFade;
            float  _Recipe;
            float  _LitFraction;
            float  _AmberFraction;
            float  _EmissionStrength;
            float  _Roughness;
            float  _Metallic;
            float  _AbDomain;
            float  _AbFade;
            float  _ThinLine;
            float  _KeyLightMask;
            float  _DownViewAtten;
        CBUFFER_END

        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"

        #define FACADE_CELL_W   3.0
        #define FACADE_CELL_H   3.5
        #define FACADE_WIN_W    1.2
        #define FACADE_WIN_H    1.6
        #define FACADE_AREA     0.18286      // 1.92 / 10.5
        #define FACADE_REVEAL   0.35
        // R2-12: depth staging of the city. Window emission x exp(-(d - 170) / 210) beyond 170 m (both tiers: the 14.10 b
        // crossover at 150 m holds), so a window at 400 m keeps about a third (lift_r32 KS5a: the face-150 far windows still
        // sat 0.24 above their facade at L 350, target <= 0.15) and each farther layer has less contrast; a city-only
        // aerial blend toward the shaft fog of up to 13 % by 400 m; a darker 0.5 m floor-slab band in every 3.5 m row and
        // vertical grime runs per 3 m column, so the facade is not a flat pegboard
        #define FACADE_FALL_D0  170.0
        #define FACADE_FALL_L   210.0
        #define FACADE_AERIAL   0.13
        #define FACADE_AER_D0   150.0
        #define FACADE_AER_D1   400.0
        #define FACADE_SLAB_H   0.5
        #define FACADE_SLAB_DARK 0.18

        // World-space window frame of a fragment: face index, coordinates along the face and up, and the face tangent.
        void FacadeFrame(float3 positionWS, out int face, out float2 uv, out float3 tangent)
        {
            float2 rel = positionWS.xz - _GridOrigin.xy;
            float angle = atan2(rel.y, rel.x);                                        // from +X toward +Z
            float k = round((angle - PI / 6.0) / (PI / 3.0));
            float faceAngle = PI / 6.0 + k * (PI / 3.0);
            face = AbWrap((int)k, 6);
            float2 t = float2(-sin(faceAngle), cos(faceAngle));                       // face normal angle + 90 deg
            tangent = float3(t.x, 0.0, t.y);
            uv = float2(dot(rel, t), positionWS.y - _GridOrigin.z);
        }

        // Static window state: x = lit 0/1, y = amber 0/1, z = emissive strength, w = room tint (-1..1).
        float4 FacadeWindowState(int face, int col, int row)
        {
            uint3 h = AbPcg3d(uint3((uint)face, (uint)(col + 32768), (uint)(row + 32768)));
            float lit = AbU01(h.x) < _LitFraction ? 1.0 : 0.0;
            float amber = AbU01(h.y) < _AmberFraction ? 1.0 : 0.0;
            float strength = lerp(_EmissiveRange.x, _EmissiveRange.y, AbU01(h.z));
            float tint = AbU01(h.x ^ h.z) * 2.0 - 1.0;
            return float4(lit, amber, strength, tint);
        }

        // Mean emission of a window area (for the far blend): 6 % lit x mean strength x mean colour.
        float3 FacadeMeanEmission()
        {
            float3 c = lerp(_WinCold.rgb, _WinAmber.rgb, _AmberFraction);
            return c * (_LitFraction * 0.5 * (_EmissiveRange.x + _EmissiveRange.y));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment FacadeForwardFrag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Assets/_Game/Shaders/Include/AbyssLighting.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"

            half4 FacadeForwardFrag(AbVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                AbFadeClip(_AbFade, input.positionCS.xy);
                AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy);

                float3 N = AbSafeNormalize(input.normalWS, float3(0.0, 0.0, 1.0));
                N = IS_FRONT_VFACE(face, N, -N);
                float specKernel = AbSpecularAAKernel(N);
                float3 dNdx = ddx(N);
                float3 dNdy = ddy(N);
                float3 dQdx = ddx(input.patternLocal);
                float3 dQdy = ddy(input.patternLocal);
                float3 P = input.patternLocal + input.patternOrigin;

                int faceId;
                float2 uv;
                float3 T;
                FacadeFrame(input.positionWS, faceId, uv, T);
                float fpU = AbFootprint(dQdx, dQdy, T);
                float fpV = AbFootprint(dQdx, dQdy, float3(0.0, 1.0, 0.0));

                int col = (int)floor(uv.x / FACADE_CELL_W);
                int row = (int)floor(uv.y / FACADE_CELL_H);
                float4 st = FacadeWindowState(faceId, col, row);
                float fu = frac(uv.x / FACADE_CELL_W);
                float fv = frac(uv.y / FACADE_CELL_H);

                // window mask: module glass faces (vertex B) or the box-filtered analytic window (14.7.3 formula)
                float mask;
                if (_Recipe > 0.5)
                {
                    mask = step(0.5, input.color.b);
                }
                else
                {
                    float mx = saturate((0.5 * FACADE_WIN_W - abs(fu - 0.5) * FACADE_CELL_W) / max(fpU, 1e-6) + 0.5);
                    float my = saturate((0.5 * FACADE_WIN_H - abs(fv - 0.5) * FACADE_CELL_H) / max(fpV, 1e-6) + 0.5);
                    mask = mx * my;
                }

                // fake depth: lintel shadow 0.35 at the window top fading to 0 at the bottom, room tint +/-12 %
                float wy = saturate((fv * FACADE_CELL_H - (0.5 * (FACADE_CELL_H - FACADE_WIN_H))) / FACADE_WIN_H);
                float reveal = 1.0 - FACADE_REVEAL * wy;
                float3 litColor = lerp(_WinCold.rgb, _WinAmber.rgb, st.y) * (st.z * (1.0 + 0.12 * st.w));
                float3 emission = st.x * litColor * reveal * mask;
                float3 glass = _GlassColor.rgb * reveal;

                // concrete frame: macro variation (9 m / 37 m value noise), a darker 0.5 m floor-slab band at the bottom of
                // every 3.5 m row (R2-12; held at 1.5 px, faded with the cell) and vertical grime runs per 3 m column
                // (StreakTex, 12 x 36 m, darker runs only)
                float macro = 1.0 + 0.24 * (0.6 * AbValueNoise3(P, 9.0, 148, 101u) + 0.4 * AbValueNoise3(P, 37.0, 36, 211u) - 0.5);
                float kSlab;
                float slab = AbLineMinWidth(uv.y - 0.5 * FACADE_SLAB_H, FACADE_CELL_H, FACADE_SLAB_H, fpV, 1.5, kSlab) * kSlab;
                float2 gUV = float2(uv.x / 12.0, uv.y / 36.0);
                float gr = SAMPLE_TEXTURE2D_GRAD(_AbStreakTex, sampler_AbStreakTex, gUV, float2(fpU / 12.0, 0.0), float2(0.0, fpV / 36.0)).r;
                float grimeRun = saturate(2.0 * (gr - 0.5));
                float3 concrete = _BaseColor.rgb * macro * (1.0 - FACADE_SLAB_DARK * slab) * (1.0 - 0.12 * grimeRun);

                float3 albedo = lerp(concrete, glass, mask);
                float rough = lerp(_Roughness, 0.15, mask);

                // far blend: cells under 7 px blend linearly to the mean, complete at 4 px
                float cellPx = min(FACADE_CELL_W / max(fpU, 1e-6), FACADE_CELL_H / max(fpV, 1e-6));
                float k = saturate((cellPx - 4.0) / 3.0);
                float3 meanAlbedo = lerp(concrete, _GlassColor.rgb * (1.0 - 0.5 * FACADE_REVEAL), FACADE_AREA);
                float3 meanEmission = FacadeMeanEmission() * FACADE_AREA * (1.0 - 0.5 * FACADE_REVEAL);
                albedo = lerp(meanAlbedo, albedo, k);
                emission = lerp(meanEmission, emission, k);
                rough = lerp(lerp(_Roughness, 0.15, FACADE_AREA), rough, k);

                // R1-01: seen from the bridge, windows within 50 degrees of the nadir go dark and the concrete darkens
                // with depth below the deck (global weight _AbDownView.x, 0 = off: the lift ride keeps the full city)
                float downAlbedo;
                float downEmit = AbDownViewEmission(input.positionWS, _DownViewAtten, downAlbedo);

                // R2-12: window emission falls off with distance beyond 170 m; R2-25: a window narrower than 1.5 px on
                // screen (grazing faces at block silhouettes) fades out instead of flickering as a 1-3 px sliver;
                // R2-21: amber windows stay amber through the tonemapper (hue-safe, lead D4)
                float camDist;
                float3 viewRay = AbViewRay(input.positionWS, camDist);
                float fall = exp(-max(camDist - FACADE_FALL_D0, 0.0) / FACADE_FALL_L);
                float winPx = FACADE_WIN_W * abs(dot(N, viewRay)) / max(camDist * AbPixelAngle(), 1e-5);
                emission *= fall * lerp(1.0, smoothstep(0.75, 1.5, winPx), k);
                emission = AbWarmEmission(emission * (_EmissionStrength * downEmit));

                AbSurface s = AbDefaultSurface(N);
                s.albedo = albedo * downAlbedo;
                s.metallic = _Metallic;
                s.roughness = AbGeometricSpecularAA(AbApplySpecularAA(rough, specKernel), dNdx, dNdy);
                s.ao = saturate(input.color.r);
                s.emission = emission;

                InputData inputData = AbMakeInputData(input.positionWS, N, input.positionCS);
                float3 color = AbyssShadeLit(s, inputData, (int)_AbDomain, _KeyLightMask);
                // R2-12: city-only aerial blend toward the shaft fog (up to 13 % by 400 m) on top of the domain fog
                color = lerp(color, FogSky(viewRay, (int)_AbDomain), FACADE_AERIAL * smoothstep(FACADE_AER_D0, FACADE_AER_D1, camDist));
                color = AbyssApplyFog(color, input.positionWS, (int)_AbDomain);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #define AB_SHADOW_CASTER_PASS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbDepthOnlyFrag
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
