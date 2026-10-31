// RingTunnelGlow - additive light of beats 5-6 (owner BJ): the tunnel mouth spill (14.5.6: analytic falloff over the
// last 12 m, intensity _Lamps.w, capped so the clipped fraction stays < 12 %) and the door / plain bollard light pools
// (14.2.3: additive pools, radius 2.5-3 m, level _Lamps.x / 3). Quads sit 0.02 m in front of their surface.
// Vertex data: uv0 = falloff coordinate (pools: offset / radius, radial; spill: (234 - x) / 12 along the tube),
// colour rgb = tint x peak (pools) or face factor (spill). RingBend in the vertex stage, fog as (1 - f) on the
// added light (RingFogAmount, the shared fog). ZWrite off, Blend One One, no local dither.
// _FadeS (pools): a progress fade-out inside the tunnel (s 780 -> 806, before NearPlain is switched off at s 810), so
// the door pool seen through the door from inside the tunnel dims out instead of vanishing (14.6 5->6: "at s 810 the
// plain is dark"); a pure function of s, so walking back brings it back.
Shader "Megastructure/RingTunnelGlow"
{
    Properties
    {
        [HDR] _Color ("Colour (linear HDR)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Mode ("Falloff (0 radial pool, 1 mouth spill)", Float) = 0
        _LampW ("Weight of the lamp level _Lamps.x / 3", Range(0, 1)) = 0
        _SpillW ("Weight of the spill level _Lamps.w", Range(0, 1)) = 0
        _Cap ("Radiance cap (linear, pre-exposure)", Float) = 1
        _SpillFalloff ("Spill e-folding (per 12 m)", Float) = 3.2
        _FadeS ("Progress fade-out: x start s, y end s, z on (0/1)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/Include/RingBend.hlsl"
            #include "../../Shaders/Include/RingAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FadeS;
                float _Intensity;
                float _Mode;
                float _LampW;
                float _SpillW;
                float _Cap;
                float _SpillFalloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;   // bent
                float flatY : TEXCOORD1;
                float4 color : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 pb = RingBendPosition(pw);
                o.positionCS = TransformWorldToHClip(pb);
                o.positionWS = pb;
                o.flatY = pw.y;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float prof;
                if (_Mode < 0.5)
                {
                    float t = saturate(1.0 - dot(i.uv, i.uv));      // radial pool: (1 - r^2)^2, 0 at the rim
                    prof = t * t;
                }
                else
                {
                    float u = saturate(i.uv.x);                      // 0 at the mouth, 1 at 12 m inside
                    prof = exp(-_SpillFalloff * u) * (1.0 - smoothstep(0.7, 1.0, u));
                }
                float level = lerp(1.0, _Lamps.x * (1.0 / 3.0), saturate(_LampW)) * lerp(1.0, _Lamps.w, saturate(_SpillW));
                level *= lerp(1.0, 1.0 - smoothstep(_FadeS.x, max(_FadeS.y, _FadeS.x + 1e-3), _RingTime.w), saturate(_FadeS.z));
                float3 c = _Color.rgb * (_Intensity * max(level, 0.0) * prof) * i.color.rgb;
                c = min(c, _Cap);
                float f = RingFogAmount(length(i.positionWS - _RingCam.xyz), i.flatY);
                c *= 1.0 - f;
                return half4(max(c, 0.0), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
