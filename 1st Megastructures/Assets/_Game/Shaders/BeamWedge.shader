// =====================================================================================================================
// Megastructure/BeamWedge - light shafts without volumetrics (14.7.4): analytic convex prisms, additive, no jitter.
//
// _Recipe 0 (fan beams): one convex prism per wedge, built in the VERTEX shader from UV1 = (wedge centre angle phi in
//   object space, corner 0..3, cap 0/1, 0) - positionOS is ignored (ShaderMeshes.BeamWedgeMesh builds the topology).
//   Object space = the fan frame: origin at the fan centre, +Z the fan axis, rotation about +Z = the fan rotation, scale 1.
//   Cross-section in the fan plane (z = 0): the annular wedge between the radial lines phi +/- _BeamParams.z and the chord
//   lines at _BeamParams.x / .y (R 70 / 130); extruded along the WORLD light direction _BeamDir (travel direction,
//   pitched 18 deg) for _BeamParams.w (300 m). Fragment: the camera ray is intersected analytically with the 6 planes
//   (t_in = max over entering planes, t_out = min over leaving planes), clamped to [near plane, scene depth] (reversed-Z
//   _CameraDepthTexture, LinearEyeDepth); density D = S(dL) S(dR) w(R) a(s) (smootherstep feathers of max(2 m, 12 px)
//   on the side planes, a radial window on the chords, an axial window), times fog transmittance; integrated with
//   12 Gauss-Legendre samples on the clamped interval (no per-pixel jitter). Winding-independent: Cull Off and only the
//   rasterised layer at the far side of the analytic interval survives, so a camera inside a wedge still sees it.
// _Recipe 1 (wall patches, 14.5.6): any mesh on the far wall; each fragment is projected back along _BeamDir onto the
//   fan plane around _BeamCentre (world axes), wedges at phi_k = _BeamPatch.z + k 2pi/12 + _BeamPatch.w * _AbFanTheta,
//   feather max(_BeamPatch.x (6 m), 12 px of the wall footprint: R2-15), vertical stretch _BeamPatch.y (1.05). Use
//   Cull Back / ZTest LEqual for this variant.
// All modes: every boundary keeps at least 12 px of screen gradient (BEAM_MIN_PX; the side and chord feathers by
//   max(m, 12 px at the sample depth), the axial caps likewise), so no prism or patch edge is a straight step.
// _Recipe 2 (rectangular prism, e.g. the gallery doorway spill): like 0 with a rectangle of half sizes _BeamRect.xy in
//   the object XY plane (UV1.x unused), length _BeamParams.w. Feathers are capped at half the half size of each axis
//   (no 8 m chord feather), and the axial fade-in at a quarter of the length, so the base _BeamFeather works unchanged.
// Output = _EmissionColor x _EmissionStrength (per metre of full density) x integral x 4 pi HG(phase) x _AbBeamFade
//   x |_AbFade|. Additive (Blend One One), ZWrite Off, explicit transparent queue (2950 by MaterialBuilder).
// _BeamShape (modes 0, 2 and 3; all 0 = off, the base material default): .x axial extinction length (density x exp(-s/.x),
//   the key light dimming in the haze it lights), .y/.z a near-camera fade of the density over camera distance [.y, .z]
//   (the samples start at .y), .w a saturation length for the integrated chord (I -> .w (1 - exp(-I/.w)): a chord seen
//   end-on rolls off instead of clipping). All smooth in t and s, so the 12-sample quadrature and the no-jitter rule are
//   unchanged.
// _BeamAxial (modes 0, 2 and 3): .x/.y the axial fade-out window [.x, .y] in metres along the light (density x
//   (1 - smootherstep(.x, .y, s))); both 0 = the default window [0.55 length, length] (the base material and the spill).
//   The fan beams end their window before the gantry eye heights in the annulus (KS6 s 216, KS5b s 240), so a wedge that
//   sweeps through the viewer is dark around them and the spokes keep their light between the gaps and the walls.
// _Recipe 3 (key shadow, 14.5.6 "the blades cast moving shadows"): the same analytic prism as mode 0 (UV1.x = wedge centre,
//   here the BLADE centres, _BeamParams = R range / half width / length of the blade shadow), drawn with BlendOp RevSub
//   (_BeamBlendOp 2). It removes the domain-0 key in-scatter lobe of the fog (AbyssFog: FogSky = ... + SCATTER x
//   HG(v.sunDir, 0.55) x scatterAmt, composited with the fog factor F) from the stretch of the view ray that lies in a
//   blade shadow: out = _EmissionStrength x (FogSky - FogSky without the lobe) x integral D(t) dF/dt dt, dF/dt = 2 k^2 t
//   exp(-(k t)^2) (k = sigma (1 + m_down saturate(-v.y)), so the subtraction never exceeds the lobe the fog put into that
//   pixel: F(tb) - F(ta) <= F(scene depth)). Gated by the active key: full only while _AbSunDir points back up _BeamDir
//   (the fan key of beat 6), zero for the beat-5 sun. The haze between the light wedges goes dark, so the spokes read as
//   light between shadows instead of a glow over a uniformly lit haze, and the frame mean pays for the spokes.
// =====================================================================================================================
Shader "Megastructure/BeamWedge"
{
    Properties
    {
        [Enum(FanPrism,0,WallPatch,1,RectPrism,2,KeyShadow,3)] _Recipe ("Mode", Float) = 0
        [HDR] _EmissionColor ("Beam Color", Color) = (0.788, 0.867, 0.941, 1)
        _EmissionStrength ("Density Scale (per m)", Float) = 0.004
        _BeamDir ("Light Travel Direction (world)", Vector) = (0, -0.309017, 0.951057, 0)
        _BeamParams ("R0, R1, Half Width (rad), Length", Vector) = (70, 130, 0.10472, 300)
        _BeamFeather ("Min Feather m, Feather px, Chord Feather m, Axial Fade-in m", Vector) = (2, 12, 8, 4)
        _BeamPatch ("Patch Feather m, V Stretch, Phase0 rad, Theta Sign", Vector) = (6, 1.05, 0, 1)
        _BeamCentre ("Patch Fan Centre (world)", Vector) = (-45, -317, 50, 0)
        _BeamRect ("Rect Half Size (mode 2)", Vector) = (0.6, 1.0, 0, 0)
        _BeamShape ("Axial Extinction m, Near Fade Start m, Near Fade End m, Saturation m (0 = off)", Vector) = (0, 0, 0, 0)
        _BeamAxial ("Axial Fade-out Start m, End m (0, 0 = 0.55 length .. length)", Vector) = (0, 0, 0, 0)
        _PhaseG ("Phase g", Range(0, 0.9)) = 0.3
        _UseDepth ("Use Scene Depth (0/1)", Float) = 1
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 0
        _AbFade ("Tier Fade (additive: visibility = abs)", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
        [Enum(UnityEngine.Rendering.BlendOp)] _BeamBlendOp ("Blend Op (Add; ReverseSubtract for mode 3)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _EmissionColor;
            float4 _BeamDir;
            float4 _BeamParams;
            float4 _BeamFeather;
            float4 _BeamPatch;
            float4 _BeamCentre;
            float4 _BeamRect;
            float4 _BeamShape;
            float4 _BeamAxial;
            float  _Recipe;
            float  _EmissionStrength;
            float  _PhaseG;
            float  _UseDepth;
            float  _AbDomain;
            float  _AbFade;
            float  _BeamBlendOp;   // render state only (BlendOp), declared for the one-CBUFFER rule
        CBUFFER_END

        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"
        ENDHLSL

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite Off
            ZTest [_ZTest]
            Blend One One
            BlendOp [_BeamBlendOp]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BeamVert
            #pragma fragment BeamFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define BEAM_MARGIN 0.25    // metres: the rasterised prism is slightly larger than the analytic one
            #define BEAM_MIN_PX 12.0    // 14.5.6: at least 12 px of gradient across every beam / patch boundary (R2-15)

            // 12-point Gauss-Legendre on [-1, 1] (docs/research/art-technique.md section 4), symmetric pairs.
            static const float GL_X[6] = { 0.12523340851146891, 0.36783149899818018, 0.58731795428661748,
                                           0.76990267419430469, 0.90411725637047491, 0.98156063424671924 };
            static const float GL_W[6] = { 0.24914704581340277, 0.23349253653835481, 0.20316742672306592,
                                           0.16007832854334622, 0.10693932599531843, 0.04717533638651183 };

            struct BeamAttributes
            {
                float4 positionOS : POSITION;
                float4 uv1        : TEXCOORD1;
            };

            struct BeamVaryings
            {
                float4 positionCS            : SV_POSITION;
                float3 positionWS            : TEXCOORD0;
                nointerpolation float  phi   : TEXCOORD1;
            };

            // Object-space light travel direction (unit), never parallel to the fan plane.
            float3 BeamDirOS()
            {
                float3 L = AbSafeNormalize(mul((float3x3)UNITY_MATRIX_I_M, _BeamDir.xyz), float3(0.0, 0.0, 1.0));
                L.z = max(L.z, 0.05);
                return normalize(L);                                       // |L| >= 0.05 after the clamp
            }

            BeamVaryings BeamVert(BeamAttributes input)
            {
                BeamVaryings output = (BeamVaryings)0;
                int mode = (int)_Recipe;
                float3 positionWS;
                if (mode == 1)
                {
                    positionWS = TransformObjectToWorld(input.positionOS.xyz);
                }
                else
                {
                    int corner = (int)round(input.uv1.y);
                    float cap = saturate(input.uv1.z);
                    float2 p2;
                    if (mode == 2)
                    {
                        float2 hs = _BeamRect.xy + BEAM_MARGIN;
                        p2 = float2((corner == 1 || corner == 2) ? hs.x : -hs.x, (corner >= 2) ? hs.y : -hs.y);
                    }
                    else
                    {
                        float h = _BeamParams.z + BEAM_MARGIN / max(_BeamParams.x, 1.0);
                        float a = input.uv1.x + ((corner == 1 || corner == 2) ? h : -h);
                        float r = ((corner < 2) ? (_BeamParams.x - BEAM_MARGIN) : (_BeamParams.y + BEAM_MARGIN)) / cos(h);
                        p2 = r * float2(cos(a), sin(a));
                    }
                    float len = _BeamParams.w + BEAM_MARGIN;
                    float3 pOS = float3(p2, -BEAM_MARGIN * (1.0 - cap)) + BeamDirOS() * (cap * len);
                    positionWS = TransformObjectToWorld(pOS);
                }
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.phi = input.uv1.x;
                return output;
            }

            // Clip [tIn, tOut] by the half-space a + b t >= 0.
            void ClipHalf(float a, float b, inout float tIn, inout float tOut)
            {
                if (abs(b) < 1e-7)
                {
                    if (a < 0.0) { tIn = 1.0; tOut = -1.0; }
                    return;
                }
                float t = -a / b;
                if (b > 0.0) tIn = max(tIn, t);
                else tOut = min(tOut, t);
            }

            float BeamS(float x) { return AbSmootherstep(0.0, 1.0, x); }

            // Four half-plane lines of the cross-section: dot(n_i, x) + c_i >= 0 inside.
            void BeamLines(int mode, float phi, out float2 n0, out float2 n1, out float2 n2, out float2 n3, out float4 c)
            {
                if (mode == 2)
                {
                    n0 = float2(1.0, 0.0); n1 = float2(-1.0, 0.0); n2 = float2(0.0, 1.0); n3 = float2(0.0, -1.0);
                    c = float4(_BeamRect.x, _BeamRect.x, _BeamRect.y, _BeamRect.y);
                }
                else
                {
                    float h = _BeamParams.z;
                    n0 = float2(sin(phi + h), -cos(phi + h));
                    n1 = float2(-sin(phi - h), cos(phi - h));
                    n2 = float2(cos(phi), sin(phi));
                    n3 = -n2;
                    c = float4(0.0, 0.0, -_BeamParams.x, _BeamParams.y);
                }
            }

            half4 BeamFrag(BeamVaryings input) : SV_Target
            {
                int mode = (int)_Recipe;
                // metres per pixel on the rasterised surface: exact on the planar patch quad (mode 1), unused by the prisms.
                // Screen derivatives are taken here, in uniform control flow, before any branch or discard.
                float wallFootprint = max(length(ddx(input.positionWS)), length(ddy(input.positionWS)));
                // The global fan-beam fade (beams appear over ride seconds 0-10) applies to the fan prisms and wall patches only;
                // the gallery doorway spill (mode 2) is independent of the fan.
                float visibility = (mode == 2 ? 1.0 : _AbBeamFade) * saturate(abs(_AbFade));
                float3 camWS = _WorldSpaceCameraPos;
                float distFrag;
                float3 dirWS = AbViewRay(input.positionWS, distFrag);
                AbFogSet fogSet = AbGetFogSet((int)_AbDomain);
                float3 Lws = AbSafeNormalize(_BeamDir.xyz, float3(0.0, 0.0, 1.0));   // NaN guard: zero _BeamDir
                float phase = 4.0 * PI * HG(dot(dirWS, -Lws), _PhaseG);
                float3 tint = _EmissionColor.rgb * _EmissionStrength;

                // ------------------------------------------------------------------ wall patches (lighthouse sweep)
                if (mode == 1)
                {
                    float3 rel = input.positionWS - _BeamCentre.xyz;
                    float s = rel.z / max(Lws.z, 0.05);
                    float2 x = float2(rel.x - Lws.x * s, (rel.y - Lws.y * s) / max(_BeamPatch.y, 1e-3));
                    float R = length(x);
                    float step12 = 2.0 * PI / 12.0;
                    float delta = atan2(x.y, x.x) - (_BeamPatch.z + _BeamPatch.w * _AbFanTheta);
                    float local = delta - step12 * round(delta / step12);
                    // R2-15: the 6 m feather is only 5-8 px at the 500-700 m of the look-back from the gantry, so every patch
                    // edge read as a straight 1-3 px step. The feather is at least BEAM_MIN_PX pixels of the wall footprint
                    // (wallFootprint, taken before any flow control).
                    float footprint = wallFootprint;
                    float fe = max(max(_BeamPatch.x, max(_BeamFeather.y, BEAM_MIN_PX) * footprint), 1e-3);
                    float dEdge = R * sin(max(_BeamParams.z - abs(local), -0.5));
                    float D = BeamS(dEdge / fe) * BeamS((R - _BeamParams.x) / fe) * BeamS((_BeamParams.y - R) / fe);
                    float3 cPatch = AbyssFogAdditive(tint * (D * visibility), input.positionWS, (int)_AbDomain);
                    return half4(cPatch, 0.0);
                }

                // ------------------------------------------------------------------ analytic prism (modes 0 and 2)
                float3 o = mul(UNITY_MATRIX_I_M, float4(camWS, 1.0)).xyz;
                float3 d = normalize(mul((float3x3)UNITY_MATRIX_I_M, dirWS));
                float3 L = BeamDirOS();
                float kx = L.x / L.z;
                float ky = L.y / L.z;
                float2 x0 = float2(o.x - kx * o.z, o.y - ky * o.z);      // cross-section coordinates along the ray
                float2 xd = float2(d.x - kx * d.z, d.y - ky * d.z);
                float s0 = o.z / L.z;                                     // extrusion parameter along the ray
                float sd = d.z / L.z;
                float len = _BeamParams.w;

                float2 n0, n1, n2, n3;
                float4 c;
                BeamLines(mode, input.phi, n0, n1, n2, n3, c);

                float tIn = -1e7;
                float tOut = 1e7;
                ClipHalf(dot(n0, x0) + c.x, dot(n0, xd), tIn, tOut);
                ClipHalf(dot(n1, x0) + c.y, dot(n1, xd), tIn, tOut);
                ClipHalf(dot(n2, x0) + c.z, dot(n2, xd), tIn, tOut);
                ClipHalf(dot(n3, x0) + c.w, dot(n3, xd), tIn, tOut);
                ClipHalf(s0, sd, tIn, tOut);
                ClipHalf(len - s0, -sd, tIn, tOut);
                if (tOut <= tIn) discard;
                // keep only the rasterised layer on the far side (winding independent, works from inside)
                if (distFrag < 0.5 * (tIn + tOut)) discard;

                // clamp to [near plane, scene depth]
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                float cosFwd = max(dot(dirWS, camFwd), 1e-3);
                float tNear = _ProjectionParams.y / cosFwd;
                float tScene = 1e7;
                if (_UseDepth > 0.5)
                {
                    float raw = LoadSceneDepth(uint2(input.positionCS.xy));
                    if (raw != UNITY_RAW_FAR_CLIP_VALUE) tScene = LinearEyeDepth(raw, _ZBufferParams) / cosFwd;
                }
                // Shape (all off at 0, so the base material and the mode-2 spill are unchanged):
                // .x axial extinction length (the light is attenuated by the haze it lights: exp(-s / .x) from the fan plane),
                // .y/.z near-camera fade over camera distance [.y, .z] (a wedge sweeping through the eye never floods the
                // frame), .w saturation length (in-scatter of a very long chord rolls off as .w (1 - exp(-I / .w))).
                float extL = _BeamShape.x;
                bool nearOn = _BeamShape.z > _BeamShape.y;
                float ta = max(tIn, tNear);
                if (nearOn) ta = max(ta, _BeamShape.y);             // the density is zero closer than the fade start
                float tb = min(tOut, tScene);
                if (tb <= ta) discard;

                // 12 Gauss-Legendre samples of D(t) T(t) on [ta, tb]
                float half_ = 0.5 * (tb - ta);
                float mid = 0.5 * (tb + ta);
                float pixelAngle = AbPixelAngle();
                float fogK = fogSet.sigma * (1.0 + fogSet.mDown * saturate(-dirWS.y));
                // Feather limits. Mode 0: sides max(2 m, 12 px), chords at least 8 m (the R 70-130 wedge is 60 m deep).
                // Mode 2: the rectangle (doorway 0.6 x 1.0 m half sizes) is far smaller than those feathers, which would
                // leave S(0.3)^2 S(0.125)^2 ~ 7e-6 at its centre; every feather is capped at half the half size of its
                // axis, so the middle half of the rectangle keeps full density at any distance. The axial fade-in is
                // capped at a quarter of the length (no change for the 300 m fan beams), so short prisms keep density.
                bool rect = mode == 2;
                bool shadow = mode == 3;
                float fMaxX = rect ? max(0.5 * _BeamRect.x, 1e-3) : 1e7;
                float fMaxY = rect ? max(0.5 * _BeamRect.y, 1e-3) : 1e7;
                float chordF = rect ? 0.0 : _BeamFeather.z;
                float axialIn = max(min(_BeamFeather.w, 0.25 * len), 1e-3);
                bool axialOn = _BeamAxial.y > _BeamAxial.x;
                float axOut0 = axialOn ? _BeamAxial.x : 0.55 * len;
                float axOut1 = axialOn ? _BeamAxial.y : len;
                float sum = 0.0;
                [unroll] for (int i = 0; i < 12; i++)
                {
                    int j = i >> 1;
                    float t = mid + ((i & 1) ? half_ : -half_) * GL_X[j];
                    float2 x = x0 + t * xd;
                    float s = s0 + t * sd;
                    float fBase = max(_BeamFeather.x, _BeamFeather.y * t * pixelAngle);
                    float f = min(fBase, fMaxX);
                    float fc = min(max(fBase, chordF), fMaxY);
                    // the two caps (axial fade-in at the fan plane, fade-out window at the far end) keep at least
                    // BEAM_MIN_PX of screen gradient as well (R2-15: a cap seen edge-on must not end in a straight step);
                    // the fade-in stays capped at a quarter of the length, so short prisms (mode 2) keep their density
                    float pxT = BEAM_MIN_PX * t * pixelAngle;
                    float aIn = max(axialIn, min(pxT, 0.25 * len));
                    float aOut0 = min(axOut0, axOut1 - pxT);
                    float dens = BeamS((dot(n0, x) + c.x) / f) * BeamS((dot(n1, x) + c.y) / f)
                               * BeamS((dot(n2, x) + c.z) / fc) * BeamS((dot(n3, x) + c.w) / fc)
                               * BeamS(s / aIn) * (1.0 - AbSmootherstep(aOut0, axOut1, s));
                    if (extL > 0.0) dens *= exp(-max(s, 0.0) / extL);
                    if (nearOn) dens *= AbSmootherstep(_BeamShape.y, _BeamShape.z, t);
                    float fx = fogK * t;
                    // beams: in-scatter x transmittance; key shadow: the fog's own in-scatter weight dF/dt
                    sum += GL_W[j] * dens * (shadow ? 2.0 * fogK * fx : 1.0) * exp(-fx * fx);
                }
                float integral = sum * half_;
                if (shadow)
                {
                    // the key lobe exactly as FogSky adds it (the same function with and without the scatter amount, so any
                    // weighting AbyssFog applies to the lobe is followed), gated to the key that shines along _BeamDir (beat 6)
                    float gate = AbSmootherstep(0.90, 0.99, dot(fogSet.sunDir, -Lws));
                    AbFogSet noLobe = fogSet;
                    noLobe.scatterAmt = 0.0;
                    float3 lobe = max(AbFogSkySet(dirWS, fogSet) - AbFogSkySet(dirWS, noLobe), 0.0);
                    float3 lost = lobe * (saturate(integral) * gate * visibility * saturate(_EmissionStrength));
                    return half4(lost, 0.0);
                }
                if (_BeamShape.w > 0.0) integral = _BeamShape.w * (1.0 - exp(-integral / _BeamShape.w));
                return half4(tint * (integral * phase * visibility), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
