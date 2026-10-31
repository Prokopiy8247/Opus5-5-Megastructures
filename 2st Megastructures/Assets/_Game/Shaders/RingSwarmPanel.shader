// M10 RingSwarmPanel - the Dyson swarm (Part 14.5.7, 14.7 H6, DEVELOPMENT.md ADR-008 as amended for ADR-011).
// 24 000 opaque 70 m plates in three bands round the star, drawn by three RenderMeshIndirect calls (one per band,
// Beats/Beat07_Outside/SwarmField.cs on the shared InstancedField) of a unit quad; every panel's orbit, orientation
// and size come from a per-instance StructuredBuffer and _Swarm.y (swarm time, wrapped at 13 200 s = LCM of the
// periods) in the vertex stage; nothing per frame on the CPU.
//  * The star is a sky element (14.7 H3): it sits 40 km along L7 from the CAMERA (_BandC.xyz), so the bands stay
//    centred on the sky disc for every camera. True panel position in metres from the camera (24-56 km), then the
//    ADR-008 radial remap (amended for ADR-011): drawn at 9 000-14 000 m along the SAME direction (screen position
//    and angular size exact, depth order preserved, monotone in true range). The hull rises to 6.6 deg below the eye
//    line with its horizon 3.5 km away, so the old 150-250 m shell drew panels in front of it; 9-14 km lies behind
//    every hull and deck hit (the hull hides the panels below its horizon by the depth test) and inside the 15 km
//    far plane used before s 880.
//  * Drawn as a camera-facing SOFT ROUND POINT (no hard square pixels): a disc of radius max(true, 1.1 px) (2.2 px
//    minimum diameter) with a 1 px linear edge, peak coverage (true / drawn)^2: the integrated coverage equals the
//    true plate area, so a sub-pixel panel neither blocks more of the corona nor glints brighter than its true size
//    (flicker control, energy preservation). The coverage is blended (SrcAlpha / OneMinusSrcAlpha) in the opaque
//    queue, depth-TESTED but not depth-writing: the hull, deck and face still hide the panels (drawn before them,
//    they are overwritten; drawn after, they fail the depth test), and the smooth coverage never steps between frames
//    the way 4-sample alpha-to-coverage did (b7-01: isolated one-pixel spikes at 1080p). No sorting is needed: the
//    points are tiny, dark or glinting, and the 9-14 km shell lies behind everything else.
//  * Behind-the-star occlusion is analytic: a panel farther than the star whose drawn point would touch the disc
//    (1.2 deg) collapses, smoothly over its own size beyond the limb; panels in front of the disc stay and are drawn
//    as black, fully opaque silhouettes (hard disc, no glint) so they read against the HDR 90 disc.
//  * Orientation: the panels are statite mirrors aimed at the ring: base normal = bisector of panel->star and
//    panel->viewer, tilted by a 2D Gaussian (sigma 18 deg) plus a slow libration whose peak angular speed is the
//    panel's spin (0.02-0.08 rad/s; angular frequencies quantised to the 13 200 s wrap). With a Gaussian glint lobe of
//    width w = 0.09 rad the expected glinting fraction is w^2 / (w^2 + 2 sigma^2) ~ 4 %, close to the 6 % of 14.5.7.
//  * Shading (per panel, in the vertex stage: every value is constant over a point): SwarmBase diffuse from the key
//    on the face the camera sees (the far sides of the orbits show their sunlit faces, the near sides are back-lit),
//    a faint SpaceBounce lift (ring-shine on the dark faces) and a forward-scattered warm rim on back-lit plates
//    (StarOutside x _Rim x cos^8 of the angle between view ray and panel->star), so the bands stay faintly visible
//    against black space and read as dark silhouettes against the corona; plus the Gaussian glint
//    exp(-(a / 0.09)^2) x 6 x _Swarm.x (wake-up g(s)), a = angle between the visible face normal and the half vector
//    of (panel->star, panel->camera); radiance clamp 8. No fog (outside sky), no shadows (Part 8.5).
// RING_NO_BEND: outside content (see RingOuterFace.shader). RING_INDIRECT: no object-to-world matrix is used.
Shader "Megastructure/RingSwarmPanel"
{
    Properties
    {
        _BaseColor ("Base colour (linear)", Color) = (0.010, 0.016, 0.030, 1)
        _GlintAmp ("Glint amplitude", Float) = 6
        _GlintWidth ("Glint width (rad)", Float) = 0.09
        _SpecClamp ("Radiance clamp", Float) = 8
        _PlateSize ("Plate size (m)", Float) = 70
        _MinPx ("Minimum drawn size (px)", Float) = 2.2
        _Remap ("Remap true d0, d1 (m) -> drawn r0, r1 (m)", Vector) = (25000, 55000, 9000, 14000)
        _LibAmp ("Libration amplitude (rad)", Float) = 0.15
        _Ambient ("Ambient multiplier", Float) = 1
        _Bounce ("SpaceBounce lift on the dark faces", Float) = 0.35
        _Rim ("Forward-scatter rim of back-lit plates (x StarOutside)", Float) = 0.06
        _EdgePx ("Soft edge width (px)", Float) = 1
        _StarRadius ("Star disc radius (rad), fallback for _StarDirOut.w", Float) = 0.020944
    }

    HLSLINCLUDE
    #define RING_NO_BEND
    #define RING_NO_FOG
    #define RING_INDIRECT
    #define RING_HAS_VERTEX_HOOK
    #define RING_HAS_CLIP_HOOK
    #include "Include/RingLitCore.hlsl"
    #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
    #include "UnityIndirect.cginc"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _Remap;
        float _GlintAmp;
        float _GlintWidth;
        float _SpecClamp;
        float _PlateSize;
        float _MinPx;
        float _LibAmp;
        float _Ambient;
        float _Bounce;
        float _Rim;
        float _EdgePx;
        float _StarRadius;
    CBUFFER_END

    // Swarm time wrap (s): LCM of the periods 600 / 1 200 / 2 200 s; AtmosphereDriver wraps _Swarm.y with it and
    // SwarmField (OutsideLayout.SwarmTimeWrap) quantises the libration to whole cycles per wrap.
    #define SWARM_TIME_WRAP 13200.0

    // Per-panel data (C# SwarmField.PanelData, stride 48):
    //  a: x orbital phase (turns), y radial offset (fraction of r), z normal offset (fraction of r), w size scale
    //  b: xy base tilt (rad, in the mirror frame), z libration direction (rad), w libration cycles per wrap (integer)
    //  c: x libration phase (rad), y albedo variation 0..1, z band index, w unused
    struct SwarmPanelData { float4 a; float4 b; float4 c; };
    StructuredBuffer<SwarmPanelData> _SwarmPanels;
    // Per-band uniforms (the InstancedField's MaterialPropertyBlock; never in UnityPerMaterial)
    float4 _BandC;   // xyz star centre relative to the camera (m) = L7 x 40 km, w band radius (m)
    float4 _BandE1;  // xyz in-plane axis 1 (node line), w orbital rate (turns / s, signed by the orbit sense)
    float4 _BandE2;  // xyz in-plane axis 2
    float4 _BandN;   // xyz band normal

    float3 SwarmSafeNormalize(float3 v, float3 fallback)
    {
        float l2 = dot(v, v);
        return l2 > 1e-12 ? v * rsqrt(l2) : fallback;
    }

    // Vertex outputs: color.rgb = panel radiance (linear HDR, clamped), color.a = peak coverage (true / drawn)^2
    // (1 for a silhouette on the disc); custom.x = drawn radius (px), custom.y = quad half-size (px), custom.z = 1 for
    // a hard silhouette in front of the disc; uv.xy = quad corner uv (0..1).
    void RingVertexHook(inout RingVertexData v)
    {
        InitIndirectDrawArgs(0);
        uint id = GetIndirectInstanceID(v.instanceID);
        SwarmPanelData pd = _SwarmPanels[id];
        float t = _Swarm.y;

        // orbit in true space (metres from the star); the phase stays in [0, 1) turns for precision
        float ph = frac(pd.a.x + t * _BandE1.w) * RING_TWO_PI;
        float sp, cp;
        sincos(ph, sp, cp);
        float r = _BandC.w * (1.0 + pd.a.y);
        float3 rel = r * (cp * _BandE1.xyz + sp * _BandE2.xyz) + (_BandC.w * pd.a.z) * _BandN.xyz;
        float3 cam = _WorldSpaceCameraPos.xyz;
        float3 starRel = _BandC.xyz;
        float starRange = max(length(starRel), 1.0);
        float3 starDir = starRel / starRange;
        float3 toPanel = starRel + rel;
        float D = max(length(toPanel), 1.0);
        float3 dir = toPanel / D;                                   // camera -> panel
        float3 sdir = SwarmSafeNormalize(-rel, -starDir);           // panel -> star
        float3 vdir = -dir;                                         // panel -> camera

        // mirror orientation: bisector of panel->star and panel->viewer, tilted (2D Gaussian) and librating
        float3 hb = sdir + vdir;
        float3 side0 = SwarmSafeNormalize(cross(sdir, _BandN.xyz), float3(1.0, 0.0, 0.0));
        float3 n0 = SwarmSafeNormalize(hb, side0);
        float3 upRef = abs(n0.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
        float3 t1 = SwarmSafeNormalize(cross(upRef, n0), side0);
        float3 t2 = cross(n0, t1);
        float sl, cl;
        sincos(pd.b.z, sl, cl);
        // libration phase from whole cycles per wrap: the sin argument stays in [0, 2 pi + phase] (GPU sin accuracy)
        float libPhase = RING_TWO_PI * frac(pd.b.w * (t * (1.0 / SWARM_TIME_WRAP))) + pd.c.x;
        float2 tau = pd.b.xy + float2(cl, sl) * (_LibAmp * sin(libPhase));
        float ta = length(tau);
        float sinc = ta > 1e-4 ? sin(ta) / ta : 1.0;
        float3 n = SwarmSafeNormalize(cos(ta) * n0 + sinc * (tau.x * t1 + tau.y * t2), n0);

        // drawn size (px): radius max(true, MinPx / 2), coverage x (true / drawn)^2; quad = disc + soft edge
        float pix = max(_RingCam.w, 1e-6);                          // rad per pixel (vertical)
        float trueHalfPx = 0.5 * _PlateSize * pd.a.w / D / pix;
        float drawnPx = max(trueHalfPx, 0.5 * _MinPx);
        float energy = saturate((trueHalfPx * trueHalfPx) / max(drawnPx * drawnPx, 1e-8));
        float edge = max(_EdgePx, 0.25);

        // analytic star occlusion (H6): behind the star (range > star range) and touching the disc -> collapse;
        // in front of the disc -> black, fully opaque silhouette (hard disc of at least 1.25 px radius)
        float rDisc = _StarDirOut.w > 1e-4 ? _StarDirOut.w : _StarRadius;
        float ang = RingAngle(dir, starDir);
        float behind = step(starRange, D);
        float onDisc = (1.0 - behind) * step(ang, rDisc + 2.0 * drawnPx * pix);
        drawnPx = lerp(drawnPx, max(drawnPx, 1.25), onDisc);
        float quadPx = drawnPx + 0.5 * edge + 0.25;
        float quadHalf = quadPx * pix;                               // rad
        float vis = lerp(1.0, smoothstep(rDisc + quadHalf, rDisc + 3.0 * quadHalf, ang), behind);

        // per-panel shading (constant over the point)
        float3 V = vdir;
        float3 face = dot(n, V) >= 0.0 ? n : -n;                     // the face the camera sees
        float ndl = dot(face, sdir);
        float3 albedo = _BaseColor.rgb * (0.85 + 0.3 * pd.c.y);
        float3 c = albedo * (saturate(ndl) * _MainLightColor.rgb + _Ambient * (_AmbSky.rgb + _AmbEquator.rgb) * 0.5);
        c += PAL_SpaceBounce * _Bounce;                              // ring-shine: faintly visible against black space
        float fwd = saturate(dot(dir, sdir));                        // 1 when the star stands right behind the plate
        float fwd2 = fwd * fwd;
        float fwd4 = fwd2 * fwd2;
        c += PAL_StarOutside * (_Rim * fwd4 * fwd4);                 // forward-scattered warm rim of back-lit plates
        float3 h = SwarmSafeNormalize(sdir + V, face);
        float ga = RingAngle(face, h) / max(_GlintWidth, 1e-3);
        float glint = ndl > 0.0 ? exp(-ga * ga) : 0.0;
        c += PAL_StarOutside * (glint * _GlintAmp * saturate(_Swarm.x));
        c = min(c, _SpecClamp) * (1.0 - onDisc);

        // ADR-008 radial remap (amended): true range 25-55 km -> 9-14 km, monotone, same direction
        float Dr = lerp(_Remap.z, _Remap.w, saturate((D - _Remap.x) / max(_Remap.y - _Remap.x, 1.0)));
        float3 right = UNITY_MATRIX_V[0].xyz;
        float3 up = UNITY_MATRIX_V[1].xyz;
        float size = 2.0 * Dr * tan(min(quadHalf, 0.2)) * vis;
        v.positionWS = cam + dir * Dr + (right * v.positionOS.x + up * v.positionOS.y) * size;
        v.normalWS = n;
        v.tangentWS = float4(sdir, 1.0);
        v.color = float4(c, lerp(energy, 1.0, onDisc));
        v.custom = float4(drawnPx, quadPx, onDisc, edge);
    }

    // Coverage of the soft round point at this fragment: peak x a disc of radius custom.x (px) with a linear edge
    // of custom.w px, from the quad uv (the quad spans +-custom.y px).
    float SwarmCoverage(float2 uv, float4 custom, float peak)
    {
        float rpx = length(uv - 0.5) * 2.0 * custom.y;
        return peak * saturate((custom.x - rpx) / max(custom.w, 1e-3) + 0.5);
    }

    // depth-only / depth-normals / shadow passes: keep the drawn disc only (never a square of depth)
    void RingClipHook(RingFragData f)
    {
        clip(SwarmCoverage(f.uv.xy, f.custom, f.color.a) - 0.5);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        s.albedo = _BaseColor.rgb;
        s.smoothness = 0.5;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off ZTest LEqual Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment SwarmFrag
            #define RING_PASS_FORWARD
            #include "Include/RingLitPass.hlsl"

            // the alpha is the coverage of the soft round point (energy preserving), blended over the sky
            half4 SwarmFrag(RingVaryings i) : SV_Target0
            {
                float a = SwarmCoverage(i.uv.xy, i.custom, i.color.a);
                clip(a - 0.01);
                return half4(max(i.color.rgb, 0.0), a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
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
            ZWrite On ColorMask R Cull Off
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
            ZWrite On Cull Off
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
