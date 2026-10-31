// RingWallClimber - the beat-5 climber (MASTER_PROMPT 14.5.5, cut order 3b; owner BJ). One warm emissive point on the
// rim-wall face, no mesh detail, no cast light, computed entirely here from the globals _RingCam (camera, pixel angle)
// and _RingTime.w (progress s), so captures and free-look agree with the walk:
//   elevation from the eye  eps(s) = 8 + 72 (s - 594) / 180 deg   (clamped to s 594..774: 8 -> 80 deg)
//   point                   (113.98, 1.7 + D tan(eps), 700), D = horizontal distance camera -> (114, 700)
// Drawn as a camera-facing sprite: a Gaussian whose FWHM is the constant angular footprint _AngularSize (1.4 mrad =
// 2.3 px at 4K, vFOV 66), never smaller than _MinPx pixels (energy kept: radiance x (true / drawn)^2), plus a faint
// tail 0.6 deg long that trails down the wall toward the point's earlier position (eps - 0.6 deg), fading as (1 - t)^2.
// WarmLamp x 4 (HDR). The sprite plane is pulled toward the camera by _DepthPull of the distance (angular size kept)
// so grazing views along the wall never clip it against the wall it sits on; nothing lies between the eye and the
// point in that range (it is always >= 24 m up the wall). Shared fog as attenuation (RingFogAmount), RingBend on the
// point itself, so it stays on the bent wall face. Blend One One, ZWrite Off, no local dither, output clamped at 64.
// Mesh: 4 vertices with uv0 = corner (0..1, 0..1); their positions only provide the (inflated) culling bounds.
Shader "Megastructure/RingWallClimber"
{
    Properties
    {
        _Color ("Colour, WarmLamp (linear)", Color) = (1, 0.693, 0.352, 1)
        _Intensity ("HDR intensity", Float) = 4
        _AngularSize ("Footprint FWHM (rad)", Float) = 0.0014
        _MinPx ("Minimum drawn FWHM (px)", Float) = 2
        _TailRad ("Tail length, elevation (rad)", Float) = 0.0104720
        _TailGain ("Tail level (x peak)", Float) = 0.05
        _Climb ("x s start, y s end, z elevation start (deg), w elevation end (deg)", Vector) = (594, 774, 8, 80)
        _Anchor ("x point x, y column z, z eye height, w distance reference x", Vector) = (113.98, 700, 1.7, 114)
        _DepthPull ("Sprite pull toward the camera (fraction of the distance)", Range(0, 0.9)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Climber"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../Shaders/Include/RingBend.hlsl"
            #include "../../Shaders/Include/RingAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Climb;
                float4 _Anchor;
                float _Intensity;
                float _AngularSize;
                float _MinPx;
                float _TailRad;
                float _TailGain;
                float _DepthPull;
            CBUFFER_END

            #define CLIMBER_PAD 3.2                 // sprite half-extent in sigmas (profile reaches 0 at 3 sigma)
            #define CLIMBER_EDGE 0.011108997        // exp(-4.5): the Gaussian at 3 sigma (renormalised to 0 there)
            #define CLIMBER_FWHM_TO_SIGMA 0.4246609 // 1 / (2 sqrt(2 ln 2))
            #define CLIMBER_MAX_ELEVATION 85.0      // deg; tan stays finite (the schedule tops out at 80)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 radiance   : TEXCOORD0;   // linear radiance at the profile peak (level, energy, fog applied)
                float3 q          : TEXCOORD1;   // x along (head -> tail), y across, in sigmas; z tail length in sigmas
            };

            // Vertical angle of one pixel (rad). _RingCam.w is written per camera by AtmosphereDriver; the fallback
            // (Scene view, before the first driver frame) derives it from the projection.
            float ClimberPixelAngle()
            {
                float w = _RingCam.w;
                if (w > 1e-7) return w;
                float p11 = abs(UNITY_MATRIX_P[1][1]);
                return 2.0 / max(p11 * _ScreenParams.y, 1.0);
            }

            // Unit vector of v, or the fallback when v is (near) zero.
            float3 ClimberSafeNormalize(float3 v, float3 fallback)
            {
                float l2 = dot(v, v);
                return l2 > 1e-14 ? v * rsqrt(l2) : fallback;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                float2 corner = saturate(v.uv0);

                // elevation schedule (deg -> rad), driven by the progress s
                float span = max(_Climb.y - _Climb.x, 1e-3);
                float f = saturate((_RingTime.w - _Climb.x) / span);
                float epsDeg = clamp(lerp(_Climb.z, _Climb.w, f), 0.0, CLIMBER_MAX_ELEVATION);
                float eps = epsDeg * RING_DEG2RAD;
                float epsTail = max(eps - max(_TailRad, 0.0), 0.0);

                // head and tail on the wall face (flat world), then bent like the wall itself
                float D = length(float2(_Anchor.w, _Anchor.y) - _RingCam.xz);
                float yHead = _Anchor.z + D * tan(eps);
                float yTail = _Anchor.z + D * tan(epsTail);
                float3 head = RingBendPosition(float3(_Anchor.x, yHead, _Anchor.y));
                float3 tail = RingBendPosition(float3(_Anchor.x, yTail, _Anchor.y));

                float3 cam = _WorldSpaceCameraPos.xyz;
                float3 vh = head - cam;
                float dH = max(length(vh), 1e-3);
                float3 w = vh / dH;                                   // camera -> head
                float3 wt = ClimberSafeNormalize(tail - cam, w);      // camera -> tail
                float3 dw = wt - w * dot(wt, w);                      // angular offset head -> tail (small angle)
                float tailAng = length(dw);

                // sprite axes: a along the tail on screen (down the wall), b across; screen-down when there is no tail
                float3 down = -UNITY_MATRIX_V[1].xyz;
                float3 aFallback = ClimberSafeNormalize(down - w * dot(down, w), float3(0.0, -1.0, 0.0));
                float3 a = tailAng > 1e-7 ? dw / max(tailAng, 1e-7) : aFallback;
                float3 b = ClimberSafeNormalize(cross(w, a), UNITY_MATRIX_V[0].xyz);

                // constant angular footprint, at least _MinPx pixels; energy preserved when drawn larger
                float pixAng = ClimberPixelAngle();
                float fwhmTrue = max(_AngularSize, 1e-6);
                float fwhm = max(fwhmTrue, max(_MinPx, 0.0) * pixAng);
                float k = fwhmTrue / fwhm;
                float sigma = fwhm * CLIMBER_FWHM_TO_SIGMA;
                float tailS = tailAng / sigma;

                // corner -> (along, across) in sigmas; the sprite plane sits at the pulled distance dP, extents scaled
                // with it so the angular size is unchanged
                float u = lerp(-CLIMBER_PAD, tailS + CLIMBER_PAD, corner.x);
                float vv = lerp(-CLIMBER_PAD, CLIMBER_PAD, corner.y);
                float dP = dH * (1.0 - clamp(_DepthPull, 0.0, 0.9));
                float3 pos = cam + w * dP + (a * u + b * vv) * (sigma * dP);

                // shared fog as attenuation (the wall behind is already fogged), plus the beat-7 space darkening
                float fa = saturate(RingFogAmount(dH, yHead));
                float dark = lerp(1.0, exp(-dH / 25000.0), saturate(_FogParams.w));
                float3 rad = max(_Color.rgb, 0.0) * (max(_Intensity, 0.0) * k * k * (1.0 - fa) * dark);

                o.positionCS = TransformWorldToHClip(pos);
                o.radiance = rad;
                o.q = float3(u, vv, tailS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float u = i.q.x, v = i.q.y, L = max(i.q.z, 0.0);
                const float inv = 1.0 / (1.0 - CLIMBER_EDGE);
                // core: Gaussian of the drawn FWHM, renormalised to 0 at 3 sigma (no square edge on the sprite)
                float core = saturate((exp(-0.5 * (u * u + v * v)) - CLIMBER_EDGE) * inv);
                // tail: the same Gaussian swept from the head (t = 0) to the tail end (t = 1), fading as (1 - t)^2
                float ua = clamp(u, 0.0, L);
                float t = L > 1e-3 ? ua / L : 1.0;
                float du = u - ua;
                float sweep = saturate((exp(-0.5 * (du * du + v * v)) - CLIMBER_EDGE) * inv);
                float fade = (1.0 - t) * (1.0 - t);
                float wgt = core + max(_TailGain, 0.0) * sweep * fade;
                float3 c = min(max(i.radiance, 0.0) * wgt, 64.0);
                return half4(c, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
