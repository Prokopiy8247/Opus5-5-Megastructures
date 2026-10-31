// RingAdditive - additive light of The Ring in one shader, selected by the material constant _Mode (uniform branch,
// no keywords):
//   0 Mote   (M13): camera-facing dust motes, 4 mm, lit by the key light, optional sunbeam mask (capsule porthole),
//                   closed-loop drift + turbulence that is exactly periodic in the 7200 s wrap of _RingTime.x.
//   1 Glow        : camera-facing Gaussian billboards for lamps, the ark engines and point cues (climber); world or
//                   angular radius; HDR; pulled toward the camera so the quad clears the lamp housing.
//   2 LightPool   : flat quads 0.02 m above a surface (polygon offset -1 -1 from the material), analytic windowed
//                   point-lamp irradiance of radius 3 m (14.2.3), driven by a lamp channel (_Lamps.x/3 for bollards).
// Common: Blend One One, ZWrite Off, ZTest LEqual, Cull Off, no shadow/depth passes, RingBend in the vertex stage,
// the shared fog applied as attenuation (lerp toward 0 by the RingFogAmount of the shared model, plus the beat-7
// space darkening), output clamped at 64 and alpha 0. No depth-texture reads unless _SoftDepth > 0.
// Billboards honour a minimum on-screen core diameter _MinPx (px) using _RingCam.w (rad / px): the drawn radius is
// max(true, 0.5 _MinPx _RingCam.w d) and the radiance is scaled by (true / drawn)^2, so the energy is preserved and
// sub-pixel sources never sparkle (Part 10 #3, #15).
//
// Vertex layout (written by Megastructure.LightPoolKit; keep both sides in sync):
//   billboards: POSITION = centre + (uv0.x, uv0.y, 0) * e (object-space XY quad, e = uv1.x, or 0.01 m when angular)
//               uv0 = corner (-1..1); COLOR.rgb = tint (0..1)
//               mote : uv1 = (core radius m, seed 0..1),  COLOR.a = sunbeam weight 0..1
//               glow : uv1 = (core radius m, or -angular radius rad, HDR intensity), COLOR.a = lamp channel / 4
//   light pool: POSITION = surface + 0.02 m (flat world via the object transform); uv0 = offset from the lamp foot /
//               radius R; uv1 = (R / lamp height h, HDR intensity); COLOR.rgb = tint, COLOR.a = lamp channel / 4.
//               The windowed profile depends only on r / h and r / R, so the per-vertex R / h carries each lamp's own
//               height (bollard lens 0.9 m, lamp posts 3.2 m) without a third texcoord or a per-material height.
// Lamp channels: 0 constant 1, 1 _Lamps.x / 3, 2 _Lamps.y, 3 _Lamps.z, 4 _Lamps.w.
// Bounds (C# side, LightPoolKit.ExpandForBillboards): billboard meshes are grown by QuadPad x the largest drawn radius
// and, for motes, by the drift margin (|_Drift| + _Turbulence.x per axis), so nothing drawn leaves the renderer bounds.
Shader "Megastructure/RingAdditive"
{
    Properties
    {
        [Enum(Mote, 0, Glow, 1, LightPool, 2)] _Mode ("Mode", Float) = 1
        _Color ("Colour multiplier (linear)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _MinPx ("Billboards: minimum core diameter (px)", Float) = 2
        _NearFade ("Billboards: near fade start, end (m)", Vector) = (0.05, 0.3, 0, 0)
        _SoftDepth ("Soft depth fade (m), 0 = off (needs the camera depth texture)", Float) = 0
        _UseKeyLight ("Mote: lit by the key light (0..1)", Range(0, 1)) = 1
        _ForwardScatter ("Mote: forward-scatter boost toward the light", Float) = 2
        _Drift ("Mote: drift amplitude xyz (m)", Vector) = (0.10, 0.07, 0.10, 0)
        _Turbulence ("Mote: turbulence amplitude (m) x, frequency (Hz) y", Vector) = (0.005, 0.3, 0, 0)
        _BeamOrigin ("Mote: beam origin xyz (flat world m), w = radius (m), 0 = no beam", Vector) = (0, 0, 0, 0)
        _BeamOutside ("Mote: level outside the beam", Float) = 0.06
        _Core ("Glow: core weight", Range(0, 1)) = 0.35
        _Pull ("Glow: pull toward the camera (x quad radius)", Float) = 1
        _OffsetFactor ("Depth offset factor", Float) = 0
        _OffsetUnits ("Depth offset units", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Additive"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Include/RingBend.hlsl"
            #include "Include/RingAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _NearFade;
                float4 _Drift;
                float4 _Turbulence;
                float4 _BeamOrigin;
                float _Mode;
                float _Intensity;
                float _MinPx;
                float _SoftDepth;
                float _UseKeyLight;
                float _ForwardScatter;
                float _BeamOutside;
                float _Core;
                float _Pull;
            CBUFFER_END

            #define RING_ANGULAR_MESH_EXTENT 0.01   // twin: LightPoolKit.AngularMeshExtent
            #define RING_QUAD_PAD 1.5               // billboard quad half-extent in core radii
            #define RING_GAUSS_EDGE 0.011108997     // exp(-4.5): profile value at the quad edge
            #define RING_TIME_WRAP 7200.0           // _RingTime.x wraps here (DEVELOPMENT.md uniform table)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 radiance   : TEXCOORD0;   // linear radiance at the profile peak (level, energy, fog applied)
                float4 prof       : TEXCOORD1;   // xy profile coords, z core weight (glow) or R / h (pool), w eye depth
            };

            // Vertical angle of one pixel (rad). _RingCam.w is written per camera by AtmosphereDriver; the fallback
            // (Scene view, before the first driver frame) derives it from the projection.
            float RingAdditivePixelAngle()
            {
                float w = _RingCam.w;
                if (w > 1e-7) return w;
                float p11 = abs(UNITY_MATRIX_P[1][1]);
                return 2.0 / max(p11 * _ScreenParams.y, 1.0);
            }

            float RingLampLevel(float a)
            {
                int ch = (int)round(saturate(a) * 4.0);
                float l = 1.0;
                if (ch == 1) l = _Lamps.x * (1.0 / 3.0);
                else if (ch == 2) l = _Lamps.y;
                else if (ch == 3) l = _Lamps.z;
                else if (ch == 4) l = _Lamps.w;
                return max(l, 0.0);
            }

            // Closed Lissajous drift plus turbulence. Every component runs an integer number of cycles per
            // RING_TIME_WRAP, so the wrap of _RingTime.x is seamless (Part 10 #31, #33).
            float3 RingMoteOffset(float seed, float t)
            {
                uint h0 = RingPcg((uint)(saturate(seed) * 16777215.0) + 1013u);
                uint h1 = RingPcg(h0), h2 = RingPcg(h1), h3 = RingPcg(h2), h4 = RingPcg(h3), h5 = RingPcg(h4);
                float3 r = float3(RingU01(h0), RingU01(h1), RingU01(h2));
                float3 q = float3(RingU01(h3), RingU01(h4), RingU01(h5));
                float tw = t * (1.0 / RING_TIME_WRAP);
                float3 kd = floor(130.0 + 45.0 * r);                                         // 0.113..0.152 rad/s
                float3 kt = floor(max(_Turbulence.y, 0.0) * RING_TIME_WRAP * (0.85 + 0.3 * q) + 0.5);
                float3 drift = _Drift.xyz * sin(RING_TWO_PI * frac(tw * kd + r));
                float3 turb = _Turbulence.x * sin(RING_TWO_PI * frac(tw * kt + q));
                return drift + turb;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                float2 corner = v.uv0;
                float3 rad = saturate(v.color.rgb) * max(_Color.rgb, 0.0) * max(_Intensity, 0.0);
                float3 posB = float3(0.0, 0.0, 0.0);
                float fogD = 0.0;
                float fogY = 0.0;
                float4 prof = float4(corner, 0.0, 0.0);

                [branch] if (_Mode > 1.5)
                {
                    // ---------------- light pool ----------------
                    float3 flatPos = TransformObjectToWorld(v.positionOS.xyz);
                    posB = RingBendPosition(flatPos);
                    fogD = length(posB - _RingCam.xyz);       // same distance as RingFog (bent space, from _RingCam)
                    fogY = flatPos.y;
                    rad *= max(v.uv1.y, 0.0) * RingLampLevel(v.color.a);
                    prof.z = max(v.uv1.x, 1e-3);              // R / h (pool radius over this pool's lamp height)
                }
                else
                {
                    // ---------------- billboards (mote, glow) ----------------
                    bool mote = _Mode < 0.5;
                    float r0 = v.uv1.x;
                    float meshHalf = r0 > 0.0 ? r0 : RING_ANGULAR_MESH_EXTENT;
                    float3 centreOS = v.positionOS.xyz - float3(corner * meshHalf, 0.0);
                    float3 flatPos = TransformObjectToWorld(centreOS);
                    if (mote) flatPos += RingMoteOffset(v.uv1.y, _RingTime.x);
                    float theta = RingBendTheta(flatPos);
                    posB = RingBendPosition(flatPos);
                    float3 toCam = _WorldSpaceCameraPos.xyz - posB;
                    float d = max(length(toCam), 1e-3);
                    float3 viewDir = toCam / d;                       // centre -> camera (the camera that renders)
                    fogD = length(posB - _RingCam.xyz);               // same distance as RingFog
                    fogY = flatPos.y;

                    float pixAng = RingAdditivePixelAngle();
                    float rhoTrue = r0 > 0.0 ? r0 : (-r0) * d;
                    float rhoMin = 0.5 * max(_MinPx, 0.0) * pixAng * d;
                    float rho = max(max(rhoTrue, rhoMin), 1e-6);
                    float k = rhoTrue / rho;
                    rad *= k * k;                                     // energy preserved: (true / drawn)^2
                    rad *= smoothstep(_NearFade.x, max(_NearFade.y, _NearFade.x + 1e-3), d);

                    [branch] if (mote)
                    {
                        float3 L = _KeyLightDir.xyz;
                        float ll = dot(L, L);
                        L = ll > 1e-8 ? L * rsqrt(ll) : float3(0.0, 1.0, 0.0);
                        float3 lightCol = lerp(float3(1.0, 1.0, 1.0), max(_KeyLightColor.rgb, 0.0), saturate(_UseKeyLight));
                        float beam = 1.0;
                        if (_BeamOrigin.w > 0.0)
                        {
                            float3 rel = flatPos - _BeamOrigin.xyz;
                            float along = -dot(rel, L);               // depth into the room along the light
                            float3 perp = rel + along * L;            // component across the beam axis
                            float m = exp(-2.0 * dot(perp, perp) / (_BeamOrigin.w * _BeamOrigin.w));
                            m *= smoothstep(-0.05, 0.10, along);
                            beam = lerp(1.0, lerp(saturate(_BeamOutside), 1.0, m), saturate(v.color.a));
                        }
                        float3 Lb = RingBendRotate(L, theta);
                        float c = saturate(dot(-viewDir, Lb));        // looking toward the light: forward scattering
                        float c2 = c * c, c4 = c2 * c2;
                        float fs = 1.0 + max(_ForwardScatter, 0.0) * (c4 * c4);
                        rad *= lightCol * (beam * fs);
                    }
                    else
                    {
                        rad *= max(v.uv1.y, 0.0) * RingLampLevel(v.color.a);
                        // the sharp core only where it is resolved (core radius >= 3 px), never as a sub-pixel sparkle
                        float rhoPx = rho / max(pixAng * d, 1e-9);
                        prof.z = saturate(_Core) * saturate((rhoPx - 3.0) / 3.0);
                        // pull toward the camera so the quad clears the lamp housing; keep the angular size
                        float pull = min(max(_Pull, 0.0) * rho * RING_QUAD_PAD, 0.5 * d);
                        posB += viewDir * pull;
                        rho *= (d - pull) / d;
                    }
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    posB += (corner.x * right + corner.y * up) * (rho * RING_QUAD_PAD);
                }

                // shared fog as attenuation (the background behind is already fogged), plus beat-7 space darkening
                float fa = RingFogAmount(fogD, fogY);
                rad *= (1.0 - saturate(fa)) * lerp(1.0, exp(-fogD / 25000.0), saturate(_FogParams.w));

                o.positionCS = TransformWorldToHClip(posB);
                prof.w = -TransformWorldToView(posB).z;
                o.radiance = rad;
                o.prof = prof;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 q = i.prof.xy;
                float q2 = dot(q, q);
                float w;
                [branch] if (_Mode > 1.5)
                {
                    // windowed irradiance of a point lamp at height h over a plane: (h^2 / (r^2 + h^2))^1.5
                    // = (1 / (1 + (r / h)^2))^1.5 with (r / h)^2 = q2 (R / h)^2, window (1 - (r / R)^4)^2 so it
                    // reaches exactly 0 at the radius R (q2 = (r / R)^2)
                    float Rh = i.prof.z;
                    float k = 1.0 / (1.0 + q2 * (Rh * Rh));               // denominator >= 1
                    float win = saturate(1.0 - q2 * q2);
                    w = k * sqrt(k) * win * win;
                }
                else
                {
                    // Gaussian of 1/e^2 radius rho (quad edge at 1.5 rho), renormalised to 0 at the quad edge,
                    // plus an optional core of radius rho / 3 (glows, faded in only when resolved)
                    float g = saturate((exp(-4.5 * q2) - RING_GAUSS_EDGE) * (1.0 / (1.0 - RING_GAUSS_EDGE)));
                    w = g + i.prof.z * exp(-40.5 * q2);
                }
                [branch] if (_SoftDepth > 0.0)
                {
                    float sceneEye = LinearEyeDepth(LoadSceneDepth(uint2(i.positionCS.xy)), _ZBufferParams);
                    w *= saturate((sceneEye - i.prof.w) / _SoftDepth);
                }
                float3 c = min(max(i.radiance, 0.0) * w, 64.0);
                return half4(c, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
