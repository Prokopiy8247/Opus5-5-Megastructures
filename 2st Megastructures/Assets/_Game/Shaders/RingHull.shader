// M09 RingHull - the underside of the world below the outside platform (Part 14.5.7, 14.7 H7, ADR-011).
// HullPale; a static nested-grid clipmap (7 levels, 2..128 m, half-extent 8 192 m) displaced in the vertex hook
// into a convex spherical cap (R 30 000 m, top y -200 under (300, 660); stable form rho^2 / (R + sqrt(R^2 - rho^2)))
// with 14 m ridges every 300 m (geometric near the platform, shading-only beyond 480 m) and 2.5 m minor ribs every
// 75 m (shading and shadow only); 100 m seam grid with a +-12 % plate patchwork and 25 m sub-panel joints (filtered
// per axis, so the seams running toward the eye survive to the 3.5 km horizon while the ones across the view fade
// before they alias): the patchwork and ribs shrinking toward the horizon are the texture gradient that reads 0.7 to
// 3.5 km far below; weathering streaks #C9C2B3 at 6 % coverage; low-frequency analytic normals; 12-step analytic
// horizon self-shadow and analytic gantry shadows on the key only (raking light, L7 NdotL = 0.12 on the cap top,
// ~0.5 on the sunward ridge faces); distance darkening (post hook, plus the space term of RingFog).
// The same material draws the 24 gantries (vertex colour B = 0: rigid geometry, own normals).
// Seen from the 1.7 m eye: horizon 3 485 m at a dip of 6.6 deg, i.e. far inside the 8 192 m clipmap (its edge lies
// behind the horizon in every direction, so it can never show).
//
// RING_NO_BEND: the hull is outside the ring (x > 234), seen only from the platform and through the tunnel mouth,
// and it has its own convex curvature in x AND z. Rolling it onto the ring cylinder would add the concave rise of
// the ring along z (+1.1 km at 6 km) and turn the cap into a saddle; RingBend also clamps y to >= -50 m. So the hull
// renders in flat world space (camera z within +-6 m of 660 on the pier: no seam with bent near content).
// Vertex convention of the hull mesh (OutsideMeshes.HullClipmap): flat grid at mesh y 0, skirt bottoms at mesh y -3;
// the hook sets y = HullGeomHeight(x, z) + mesh y. Colour R = AO, G = edge wear, B = 1 displaced grid vertex (B = 0:
// rigid gantry geometry with its own normals); uv1 = stitch vector to the two even neighbours of an odd outer-boundary
// vertex (its height becomes their mean, i.e. exactly on the coarse edge: no T-junction crack).
// _RidgeGeom: x, y = geometric-ridge fade radii, z = width of the contact darkening next to the outer face, w = face x.
// No shadow casting (Part 8.5); it receives none either (beyond the 90 m cascades) - its shadows are analytic.
Shader "Megastructure/RingHull"
{
    Properties
    {
        _BaseColor ("Base colour (linear)", Color) = (0.694, 0.658, 0.578, 1)
        _StreakColor ("Streak colour (linear)", Color) = (0.584, 0.539, 0.451, 1)
        _GantryColor ("Gantry colour (linear)", Color) = (0.52, 0.50, 0.46, 1)
        _HullCentre ("Cap centre x, top y, centre z, radius", Vector) = (300, -200, 660, 30000)
        _Ridge ("Ridge period, height, half-width, crest x", Vector) = (300, 14, 50, 560)
        _Rib ("Minor rib period, height, half-width, crest x", Vector) = (75, 2.5, 12, 597.5)
        _RidgeGeom ("Geometric ridge fade r0, r1, face AO width, face x", Vector) = (380, 480, 40, 234)
        _Seam ("Seam period, width, offset x, offset z", Vector) = (100, 3, 50, 10)
        _Tone ("Plate tone amplitude, sub-panel period, sub-panel width, sub-panel darkening", Vector) = (0.12, 25, 0.8, 0.82)
        _DistDark ("Distance darkening floor, e-folding distance (m)", Vector) = (0.55, 4000, 0, 0)
        _Gantry ("Gantry x0, pitch x, jitter x, near row lateral", Vector) = (1000, 420, 60, 800)
        _GantryB ("Gantry jitter z, beam y0, beam y1, half-length", Vector) = (120, 6, 9, 20)
        _GantryC ("Gantry leg x, leg z, leg half-size, beam half-width", Vector) = (1.2, 18.5, 0.3, 1.5)
        _GantryD ("Gantry far row lateral, -, -, -", Vector) = (1400, 0, 0, 0)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.32
        _StreakAmount ("Streak blend", Range(0, 1)) = 1
        _SeamDarken ("Seam darkening", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #define RING_NO_BEND
    #define RING_HAS_VERTEX_HOOK
    #define RING_HAS_KEY_HOOK
    #define RING_HAS_POST_HOOK
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _StreakColor;
        float4 _GantryColor;
        float4 _HullCentre;
        float4 _Ridge;
        float4 _Rib;
        float4 _Tone;
        float4 _DistDark;
        float4 _RidgeGeom;
        float4 _Seam;
        float4 _Gantry;
        float4 _GantryB;
        float4 _GantryC;
        float4 _GantryD;
        float _Smoothness;
        float _StreakAmount;
        float _SeamDarken;
    CBUFFER_END

    #include "Assets/_Game/Beats/Beat07_Outside/Shaders/RingHullCore.hlsl"

    void RingVertexHook(inout RingVertexData v)
    {
        v.custom = float4(0.0, 0.0, 0.0, 0.0);
        if (v.color.b > 0.5)
        {
            float2 xz = v.positionWS.xz;
            float2 e = v.uv.zw;
            float h;
            if (abs(e.x) + abs(e.y) > 0.0)
                h = 0.5 * (HullGeomHeight(xz + e) + HullGeomHeight(xz - e));   // odd boundary vertex: on the coarse edge
            else
                h = HullGeomHeight(xz);
            v.positionWS.y = h + v.positionWS.y;                                  // mesh y: 0 grid, -3 skirt bottom
            float sc;
            HullCapDrop(xz, sc);
            v.normalWS = normalize(float3(xz.x - _HullCentre.x, sc, xz.y - _HullCentre.z));
            v.tangentWS = float4(1.0, 0.0, 0.0, 1.0);
            v.custom.x = 1.0;
        }
    }

    float HullFootprint(RingFragData f)
    {
        float3 dpx = ddx(f.positionFlatWS);
        float3 dpy = ddy(f.positionFlatWS);
        return max(max(length(dpx), length(dpy)), f.footprint);
    }

    // Per-axis box-filter widths (m) of the pixel footprint along world x and z: a line of constant x is filtered with
    // the x width only, so a seam that runs toward the eye (narrow across the view) stays crisp while one across the
    // view (stretched by the grazing angle) converges to its mean.
    float2 HullAxisFootprint(RingFragData f)
    {
        float3 dpx = ddx(f.positionFlatWS);
        float3 dpy = ddy(f.positionFlatWS);
        float floorFp = 0.25 * f.footprint;
        return max(float2(abs(dpx.x) + abs(dpy.x), abs(dpx.z) + abs(dpy.z)), floorFp.xx);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float fp = HullFootprint(f);
        float2 fpa = HullAxisFootprint(f);
        float3 p = f.positionFlatWS;
        if (f.custom.x > 0.5)
        {
            // low-frequency analytic normal: exact cap normal + the ridge slope (a function of x only), faded once the
            // 100 m bump is only a few x-footprints wide (hull shimmer, 14.5.7 pitfalls)
            float sc, slope, ribSlope;
            HullCapDrop(p.xz, sc);
            HullRidge(p.x, slope);
            HullRib(p.x, ribSlope);
            float wR = saturate(2.0 - fpa.x / (0.25 * _Ridge.z));
            float wB = saturate(2.0 - fpa.x / (0.25 * _Rib.z));
            float2 d = p.xz - _HullCentre.xz;
            float gx = -d.x / sc + slope * wR + ribSlope * wB;
            float gz = -d.y / sc;
            f.normalWS = normalize(float3(-gx, 1.0, -gz));

            float3 base = _BaseColor.rgb;
            float macro = RingValueNoise(p.xz / 1500.0 + 3.1);
            base *= 0.93 + 0.14 * macro;
            // 100 m plates: per-plate tone +-12 % (a patchwork whose cells shrink toward the horizon: the texture
            // gradient of distance), converging to the mean before it can alias
            float2 cell = floor((p.xz - _Seam.zw) / _Seam.x);
            float tone = RingHash21(int2(cell) + int2(311, 97)) - 0.5;
            base *= 1.0 + 2.0 * _Tone.x * tone * saturate(2.0 - fp / 20.0);
            // weathering streaks along z (6 % coverage; mean 0.062 beyond the footprint fade)
            float sn = RingValueNoise(float2(p.x / 45.0, p.z / 600.0) + float2(17.3, 5.9));
            float streak = smoothstep(0.805, 0.905, sn);
            streak = lerp(0.062, streak, saturate(2.0 - fpa.x / 10.0));
            base = lerp(base, _StreakColor.rgb, streak * _StreakAmount);
            // 100 m seam grid, box-filtered per axis and faded below 3 footprints per period
            float g1 = RingFilteredGrid1(p.x - _Seam.z, _Seam.x, _Seam.y, fpa.x, 3.0);
            float g2 = RingFilteredGrid1(p.z - _Seam.w, _Seam.x, _Seam.y, fpa.y, 3.0);
            float seam = saturate(g1 + g2 - g1 * g2);
            // 25 m sub-panel joints inside the plates (fade below 4 footprints per period)
            float k1 = RingFilteredGrid1(p.x - _Seam.z, _Tone.y, _Tone.z, fpa.x, 4.0);
            float k2 = RingFilteredGrid1(p.z - _Seam.w, _Tone.y, _Tone.z, fpa.y, 4.0);
            float sub = saturate(k1 + k2 - k1 * k2) * (1.0 - seam);
            base = lerp(base, base * _Tone.w, sub);
            base = lerp(base, base * _SeamDarken, seam);

            // contact darkening where the cap runs into the outer face (x 234): the crease reads as a structural joint.
            // On the albedo, because the key (not the tiny SpaceBounce ambient that occlusion scales) lights the hull.
            float faceAO = lerp(0.6, 1.0, RingSmootherstep01((p.x - _RidgeGeom.w) / max(_RidgeGeom.z, 1.0)));

            s.albedo = base * faceAO;
            s.smoothness = _Smoothness * (1.0 - 0.4 * seam) * (1.0 - 0.3 * streak);
            s.occlusion = faceAO;
        }
        else
        {
            float ao = f.color.r;
            float wear = f.color.g;
            s.albedo = _GantryColor.rgb * lerp(0.8, 1.0, ao) * (1.0 + 0.06 * wear);
            s.smoothness = 0.36;
            s.occlusion = ao;
        }
    }

    // Distance darkening (14.5.7 hull look): no air outside, but the cap falls away into the dark, so its radiance is
    // eased toward a floor with range (0.93 at 0.7 km, 0.74 at the 3.5 km horizon); RingFog adds the space term.
    float3 RingPostHook(RingFragData f, float3 c)
    {
        return c * lerp(_DistDark.x, 1.0, exp(-f.viewDist / max(_DistDark.y, 1.0)));
    }

    float RingKeyVisibilityHook(RingFragData f)
    {
        float fp = HullFootprint(f);
        if (f.custom.x < 0.5) return 1.0;
        float3 L = _MainLightPosition.xyz;
        float l2 = dot(L, L);
        if (l2 < 0.25) return 1.0;
        L *= rsqrt(l2);
        return HullKeyVisibility(f.positionFlatWS.xz, L, fp);
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
            #pragma fragment RingOutsideFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            #include "Include/RingLitPass.hlsl"
            #include "Assets/_Game/Beats/Beat07_Outside/Shaders/RingOutsideForward.hlsl"
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
