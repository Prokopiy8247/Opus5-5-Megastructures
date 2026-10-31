// M06 RingWall - the rim wall and its plinth (14.5.5, 14.2.6). Albedo is the pale ceramic WallTone at all times
// (WallNight is the palette's expectation of WallTone under the night light, not an albedo: the night colour comes
// from the lights). The surface must read as one huge built plane while staying smooth:
//  - 100 m joint grid (vertical seams every 100 m along z, horizontal joints every 100 m of height from 100 m up).
//    Arbitration (B56): the spec's 6 mm groove is 0.06 px at 84 m and never reads; the joint is 0.25 m wide with a
//    soft shadow under its lower lip (key and wash come from above). Each axis is box-filtered EXACTLY with the
//    screen footprint of its own coordinate (integral of the profile, energy preserving) and faded out once the
//    spacing drops below _Seam.w px, so the upper wall turns smooth with no moire.
//  - fine 4 m courses (hairline joints) that exist only near the foot: height envelope exp(-y / _Course.w), filtered
//    and faded like the joints.
//  - per-panel tone +-_PanelTone.x per 100 m panel (PCG hash), blended across each seam over one footprint and faded
//    with the panel spacing, so far panels converge to the mean (no tile noise).
//  - an analytic height gradient applied to the lit colour: the foot glow (ground bounce and the lamp-lit plain,
//    gain _FootGlow.x at y 0 with e-folding height _FootGlow.y) and the height haze toward the shared fog colour
//    (_HeightHaze.x (1 - exp(-y / _HeightHaze.y))), so the wall is brightest at the foot and settles into the fog
//    colour with height. Both depend only on the flat height: static, analytic, no banding source, no probe.
// Lit by the shared model (RingLighting: key, RimFill wash, Trilight, RingSkyColor reflections - never a probe, so
// the H10 luma is the same with and without the sky dome), then the soft highlight clamp (wall luma <= 190 at the
// beat-5 exposure) and RingFog last. Vertex R = AO; no local dither (14.7 H5: the final pass dithers).
// Built on the RingLitCore / RingLitPass contract: the same four passes and RingBend vertex path as RingCeramic.
Shader "Megastructure/RingWall"
{
    Properties
    {
        _BaseColor ("Albedo, WallTone (linear)", Color) = (0.716, 0.768, 0.855, 1)
        _Tint ("Albedo tint (linear)", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.35
        _Seam ("Joints: x period (m), y groove width (m), z groove albedo factor, w fade below this spacing (px)", Vector) = (100, 0.25, 0.6, 4)
        _SeamShadow ("Lower-lip shadow: x height (m), y darkening at the lip, z share on vertical seams", Vector) = (0.5, 0.22, 0.4, 0)
        _Course ("Courses: x period (m), y line width (m), z darkening, w envelope e-folding height (m)", Vector) = (4, 0.035, 0.18, 120)
        _PanelTone ("Panel tone: x amplitude (+-), y fade below this panel spacing (px)", Vector) = (0.025, 6, 0, 0)
        _FootGlow ("Foot glow: x gain at the foot, y e-folding height (m)", Vector) = (0.4, 30, 0, 0)
        _HeightHaze ("Height haze to the fog colour: x maximum, y e-folding height (m)", Vector) = (0.7, 600, 0, 0)
        _FootAO ("Contact shade just above the foot", Range(0, 0.5)) = 0.08
        _TopShade ("Albedo factor of up-facing faces (plinth top)", Range(0, 1)) = 1
        _AO ("Vertex AO strength", Range(0, 1)) = 1
        _Highlight ("Soft clamp: x knee lum, y max lum, z max channel (linear, pre-exposure)", Vector) = (0.12, 0.2, 0.6, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _Tint;
        float4 _Seam;
        float4 _SeamShadow;
        float4 _Course;
        float4 _PanelTone;
        float4 _FootGlow;
        float4 _HeightHaze;
        float4 _Highlight;
        float _Smoothness;
        float _FootAO;
        float _TopShade;
        float _AO;
    CBUFFER_END

    // Integral F(d) of the joint darkening profile across one joint centred at d = 0 (d grows upward / along +z):
    // the groove (|d| < w/2, darkening dg) plus, below it, the lower-lip shadow ramp (ds at d = -w/2 falling linearly
    // to 0 at d = -w/2 - sh). Branch-free: two clamped pieces.
    float RingWallJointF(float d, float w, float dg, float sh, float ds)
    {
        float t1 = clamp(d + 0.5 * w + sh, 0.0, sh);
        float t2 = clamp(d + 0.5 * w, 0.0, w);
        return ds * t1 * t1 / (2.0 * max(sh, 1e-4)) + dg * t2;
    }

    // Box-filtered joint darkening of the joint nearest to coord (joints at k * period, k >= minIndex) over the
    // footprint fp (same units): exact, energy preserving (converges to (dg w + ds sh / 2) / fp), faded to 0 once the
    // joint spacing is below _Seam.w px.
    float RingWallJoint(float coord, float fp, float minIndex, float sh, float ds)
    {
        float period = max(_Seam.x, 1.0);
        float k = max(round(coord / period), minIndex);
        float d = coord - k * period;
        float h = 0.5 * max(fp, 1e-5);
        float w = max(_Seam.y, 1e-4), dg = saturate(1.0 - _Seam.z);
        float dark = (RingWallJointF(d + h, w, dg, sh, ds) - RingWallJointF(d - h, w, dg, sh, ds)) / (2.0 * h);
        float spacingPx = period / max(fp, 1e-5);
        float fade = saturate((spacingPx - _Seam.w) / max(_Seam.w, 1e-3));
        return saturate(dark) * fade;
    }

    // Per-panel tone factor (1 +- amplitude) of the 100 m panels at plane coordinates c = (u, v), blended across the
    // nearest seam of each axis over one footprint (fp2 per axis) and faded to 1 when the panels shrink on screen.
    float RingWallPanelTone(float2 c, float2 fp2)
    {
        float period = max(_Seam.x, 1.0);
        float2 g = c / period;
        float2 i = round(g);                                   // nearest seam index per axis
        float2 w = saturate(0.5 + (g - i) * period / max(fp2, 1e-4));   // coverage of the panel on the +side
        int2 i1 = (int2)i, i0 = i1 - int2(1, 1);
        float a = RingHash21(int2(i0.x, i0.y)), b = RingHash21(int2(i1.x, i0.y));
        float cc = RingHash21(int2(i0.x, i1.y)), d = RingHash21(int2(i1.x, i1.y));
        float hsh = lerp(lerp(a, b, w.x), lerp(cc, d, w.x), w.y);
        float spacingPx = period / max(max(fp2.x, fp2.y), 1e-4);
        float fade = saturate((spacingPx - _PanelTone.y) / max(_PanelTone.y, 1e-3));
        return 1.0 + _PanelTone.x * (2.0 * hsh - 1.0) * fade;
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float3 p = f.positionFlatWS;
        float3 n = f.normalFlatWS;
        // per-axis screen footprint of the flat world coordinate (anisotropic, straight-line code: no divergent ddx)
        float3 dpx = ddx(p), dpy = ddy(p);
        float3 fp = max(sqrt(dpx * dpx + dpy * dpy), 1e-5);
        float3 an = abs(n);
        bool face = an.x >= an.y && an.x >= an.z;      // the wall face (and the plinth front / chamfer)

        // joints: vertical seams on z (light lower-lip share), horizontal joints on y from 100 m up (wall face only)
        float sh = max(_SeamShadow.x, 0.0), ds = saturate(_SeamShadow.y);
        float jz = RingWallJoint(p.z, fp.z, -1.0e6, sh, ds * _SeamShadow.z);
        float jy = face ? RingWallJoint(p.y, fp.y, 1.0, sh, ds) : 0.0;
        float joint = saturate(jz + jy - jz * jy);

        // 4 m courses near the foot (hairlines; none below 2 m, where the plinth and contact shade are)
        float env = exp(-max(p.y, 0.0) / max(_Course.w, 1e-3)) * smoothstep(1.5, 3.0, p.y);
        float course = face ? RingFilteredGrid1(p.y, max(_Course.x, 0.1), _Course.y, fp.y, _Seam.w) * env * _Course.z : 0.0;

        // panel tone on the plane coordinates of the face (z, y); up-facing faces use (z, x)
        float2 c2 = face ? p.zy : p.zx;
        float2 f2 = face ? fp.zy : fp.zx;
        float tone = RingWallPanelTone(c2, f2);

        float ao = lerp(1.0, saturate(f.color.r), _AO);
        float contact = 1.0 - _FootAO * (1.0 - smoothstep(0.45, 1.6, p.y));
        float top = lerp(1.0, _TopShade, saturate(n.y));
        float3 alb = _BaseColor.rgb * _Tint.rgb * tone * (1.0 - joint) * (1.0 - course) * contact * top;
        s.albedo = alb * lerp(0.6, 1.0, ao);
        s.smoothness = clamp(_Smoothness * (1.0 - 0.5 * joint), 0.0, 0.65);
        s.occlusion = ao;
        s.emission = half3(0, 0, 0);
    }

    // Analytic height gradient on the lit colour (see the header): foot glow, then the haze toward the fog colour
    // along this view heading (desaturated like RingFog at the same amount). y = flat height (m).
    float3 RingWallHeightGradient(float3 c, float3 positionBentWS, float y)
    {
        float yy = max(y, 0.0);
        c *= 1.0 + max(_FootGlow.x, 0.0) * exp(-yy / max(_FootGlow.y, 1e-3));
        float hz = saturate(_HeightHaze.x) * (1.0 - exp(-yy / max(_HeightHaze.y, 1e-3)));
        float3 v = positionBentWS - _RingCam.xyz;
        float3 fc = RingFogHeadingColor(v / max(length(v), 1e-4));
        fc = lerp(fc, RingLuma(fc).xxx, _FogParams.z * hz);
        return lerp(c, fc, hz);
    }

    // Soft highlight clamp: m = how close the colour is to the limits (luminance _Highlight.y, any channel _Highlight.z);
    // above the knee m is compressed smoothly and monotonically so it never reaches 1.
    float3 RingWallSoftClamp(float3 c)
    {
        float lumMax = max(_Highlight.y, 1e-4);
        float chMax = max(_Highlight.z, 1e-4);
        float m = max(RingLuma(c) / lumMax, max(c.r, max(c.g, c.b)) / chMax);
        float k = saturate(_Highlight.x / lumMax);
        if (m > k && m > 1e-5)
        {
            float span = max(1.0 - k, 1e-4);
            float m2 = k + span * (1.0 - exp(-(m - k) / span));
            c *= m2 / m;
        }
        return c;
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
            #pragma fragment RingWallFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            // the shared forward fragment is compiled without fog under another name; the height gradient and the clamp
            // sit between lighting and RingFog, so the fogged far wall still converges exactly to the shared fog colour
            #define RING_NO_FOG
            #define RingFrag RingWallLitFrag
            #include "Include/RingLitPass.hlsl"
            #undef RingFrag

            half4 RingWallFrag(RingVaryings i) : SV_Target0
            {
                half4 lit = RingWallLitFrag(i);
                float3 c = RingWallHeightGradient(lit.rgb, i.positionWS, i.positionFlatWS.y);
                c = RingWallSoftClamp(c);
                c = RingFog(c, i.positionWS, i.positionFlatWS.y);
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
