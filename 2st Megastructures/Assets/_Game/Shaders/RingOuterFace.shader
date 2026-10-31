// RingOuterFace - the outer face of the rim wall seen from the outside platform (Part 14.5.7): the plane x = 234
// facing +X (toward the star, NdotL = L7.x = 0.99), a +-60 km grid around the tunnel mouth. Lit head-on, a plain
// glaze reads as an overexposed house wall (critics run04, KS15 mean luma 166), so the face carries the scale cues
// itself: a darkened HullPale (the material tint keeps KS15 at face luma ~110 and the beat-7 mean in 35-105), a
// staggered course of 6.25 x 3.125 m plates with an inset line 0.3 m inside each edge (the doorway is one course
// high), 25 m sub-panels, the 100 m seam grid and a per-100 m panel tone (+-10 %), all box-filtered per axis and
// faded below 3-4 footprints per period; large horizontal ledges every
// 100 m (2.5 m high, 4 m deep) drawn analytically on the plane: for each fragment the eye ray is intersected with the
// ledge prism (infinite along z), so from below the dark underside, the lit front and, from above, the dim top
// (NdotL 0.12) appear with the right parallax, and the 0.5 m key shadow each ledge casts on the face below
// (depth x L.y / L.x under the 7 deg light) is applied through the key-visibility hook (exact, like the hull
// self-shadow); the undersides get a small uplight (emission) standing for the light the sunlit hull throws back up;
// then RingFog (space darkening) and a final darkening to SpaceBlack by 55 km so the 60 km far plane
// never shows a cut. Vertex colour B = 1 marks the PaleGold mouth frame (the same material, so frame and face share
// one lighting and no bend mismatch). Receives the deck's shadows, casts none (Part 8.5).
//
// RING_NO_BEND (justification): the bend rolls (y, z) about the ring axis but never changes x, so it maps the plane
// x = 234 onto itself; bending it would only re-map the seam pattern and, worse, RingBend clamps y to >= -50 m,
// which would collapse the face below the deck (it reaches the hull 200 m down and -60 km). The platform region
// x > 234 is outside the ring, so the outside content (face, hull, swarm) is flat and consistent with the sky star
// (a direction). The bent deck, rails and tunnel touch the face only near the camera z: the mouth (|dz| <= 1.3 m,
// offset < 0.1 mm, covered by the 0.14 m frame and its 0.20 m reveal) and the rail / kick-plate ends (|dz| <= 6 m,
// offset <= 1.1 mm, buried 5 cm in the face), so no gap or step can open at the junction.
Shader "Megastructure/RingOuterFace"
{
    Properties
    {
        _BaseColor ("Base colour (linear)", Color) = (0.174, 0.165, 0.145, 1)
        _TrimColor ("Trim colour (linear)", Color) = (0.694, 0.468, 0.144, 1)
        _Smoothness ("Smoothness", Range(0, 0.65)) = 0.22
        _TrimSmoothness ("Trim smoothness", Range(0, 0.65)) = 0.55
        _Seam ("Seam period, width, offset z, offset y", Vector) = (100, 0.25, 10, 50)
        _SubPanel ("Sub-panel period, width, darkening", Vector) = (25, 0.08, 0.80, 0)
        _Plate ("Plate width (z), height (y), joint width, joint darkening", Vector) = (6.25, 3.125, 0.04, 0.62)
        _PlateOrigin ("Plate origin z, y, plate tone amplitude, panel tone amplitude", Vector) = (656.875, 0, 0.05, 0.10)
        _Ledge ("Ledge period, top y offset, front height, depth (m)", Vector) = (100, 50, 2.5, 4)
        _LedgeShade ("Ledge front albedo, top albedo, underside albedo, face x", Vector) = (1.12, 0.90, 0.70, 234)
        _PlateInset ("Plate inset line distance, width, darkening, ledge underside uplight", Vector) = (0.3, 0.02, 0.78, 0.15)
        _Portal ("Door surround half-width, top y, bottom y, groove width", Vector) = (2.6, 4.4, -0.5, 0.025)
        _PortalCentre ("Door surround centre z", Float) = 660
        _SeamDarken ("Seam darkening", Range(0, 1)) = 0.45
        _Dark ("Darkening start, end distance (m)", Vector) = (30000, 55000, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #define RING_NO_BEND
    #define RING_HAS_POST_HOOK
    #define RING_HAS_KEY_HOOK
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float4 _TrimColor;
        float4 _Seam;
        float4 _SubPanel;
        float4 _Plate;
        float4 _PlateOrigin;
        float4 _Ledge;
        float4 _LedgeShade;
        float4 _PlateInset;
        float4 _Portal;
        float4 _Dark;
        float _Smoothness;
        float _TrimSmoothness;
        float _PortalCentre;
        float _SeamDarken;
    CBUFFER_END

    // Box-filtered coverage of the outline of an axis-aligned rectangle (open at the bottom edge) in the face plane.
    float FacePortalGroove(float2 q, float fp)
    {
        float hx = _Portal.x, top = _Portal.y, bottom = _Portal.z, w = _Portal.w;
        float dz = abs(q.x - _PortalCentre);
        float inY = step(bottom, q.y) * step(q.y, top + 0.5 * w);
        float inZ = step(dz, hx + 0.5 * w);
        float side = RingFilteredLine(dz - hx, w, fp) * inY;
        float lintel = RingFilteredLine(q.y - top, w, fp) * inZ;
        float fade = saturate((hx / max(fp, 1e-4) - 4.0) / 4.0);
        return saturate(max(side, lintel)) * fade;
    }

    // Box-filtered coverage of the interval [a, b] (wall y) by a footprint of width 2 h centred at y.
    float FaceIntervalCov(float y, float h, float a, float b)
    {
        return saturate((min(y + h, b) - max(y - h, a)) / (2.0 * h));
    }

    // Analytic ledges (see the header). For the wall point at height yP seen from the camera, returns the filtered
    // coverages of the ledge underside (x), front (y), top (z) and of the key shadow on the visible wall (w).
    // Geometry of ledge j: y in [top_j - height, top_j], x in [face, face + depth], top_j = offset + j period.
    // In the x-y plane the eye ray passes x = face + depth at ya = yc + (yP - yc) k, k = (D - depth) / D (D = camera
    // distance to the face); a wall height yP shows ledge j when [ya, yP] meets [y0, y1]. Its projected front edges
    // are F(y) = yc + (y - yc) / k; k -> 0 (camera within the depth) sends them to infinity (underside everywhere).
    float4 FaceLedgeCov1(float yP, float h, float yc, float k, float j, float sh)
    {
        float y1 = _Ledge.y + j * _Ledge.x;
        float y0 = y1 - _Ledge.z;
        float invK = 1.0 / max(k, 1e-4);
        float F0 = yc + (y0 - yc) * invK;
        float F1 = yc + (y1 - yc) * invK;
        float4 c = 0.0;
        if (yc < y0)
        {
            c.x = FaceIntervalCov(yP, h, y0, F0);           // underside, seen from below
            c.y = FaceIntervalCov(yP, h, F0, F1);           // front
            c.w = FaceIntervalCov(yP, h, y0 - sh, y0);      // key shadow on the wall just below the ledge
        }
        else if (yc > y1)
        {
            c.y = FaceIntervalCov(yP, h, F0, F1);           // front
            c.z = FaceIntervalCov(yP, h, F1, y1);           // top, seen from above
            c.w = FaceIntervalCov(yP, h, y0 - sh, min(y0, F0));
        }
        return c;
    }

    float4 FaceLedges(float yP, float fpy, float3 L)
    {
        float D = _RingCam.x - _LedgeShade.w;                // camera distance to the face plane (x 234)
        if (D <= 0.01 || _Ledge.x <= 0.0) return 0.0;
        float yc = _RingCam.y;
        float k = saturate((D - _Ledge.w) / D);
        float ya = yc + (yP - yc) * k;
        float h = max(0.5 * fpy, 1e-3);
        float sh = _Ledge.w * saturate(L.y) / max(L.x, 0.05);
        // the first ledge the ray meets: upward the lowest with top >= ya, downward the highest with bottom <= ya;
        // the neighbour toward the eye is evaluated too, so every boundary is filtered from both sides
        float j, jn;
        if (yP >= yc) { j = ceil((ya - _Ledge.y) / _Ledge.x); jn = j - 1.0; }
        else { j = floor((ya - (_Ledge.y - _Ledge.z)) / _Ledge.x); jn = j + 1.0; }
        float4 a = FaceLedgeCov1(yP, h, yc, k, j, sh);
        float4 b = FaceLedgeCov1(yP, h, yc, k, jn, sh);
        float4 c = max(a, b);
        float solid = saturate(c.x + c.y + c.z);
        c.xyz = c.xyz / max(1.0, c.x + c.y + c.z);
        c.w *= 1.0 - solid;
        return c;
    }

    float3 FaceKeyDir()
    {
        float3 L = _MainLightPosition.xyz;
        float l2 = dot(L, L);
        return l2 > 0.25 ? L * rsqrt(l2) : float3(1.0, 0.0, 0.0);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float3 dpx = ddx(f.positionFlatWS);
        float3 dpy = ddy(f.positionFlatWS);
        float fp = max(max(length(dpx), length(dpy)), f.footprint);
        float fpz = max(abs(dpx.z) + abs(dpy.z), 0.25 * f.footprint);   // per-axis filter widths in the face plane
        float fpy = max(abs(dpx.y) + abs(dpy.y), 0.25 * f.footprint);
        float2 q = f.positionFlatWS.zy;     // face plane coordinates (z, y)
        float trim = saturate(f.color.b);

        // macro tone: 420 m noise, per-100 m panel tone, faint vertical weathering
        float macro = RingValueNoise(q / 420.0 + 9.2);
        float2 cell100 = floor((q - _Seam.zw) / _Seam.x);
        float panelTone = (RingHash21(int2(cell100) + int2(57, 211)) - 0.5) * saturate(2.0 - fp / 12.0);
        float streak = (RingValueNoise(float2(q.x / 6.0, q.y / 90.0) + 4.4) - 0.5) * saturate(2.0 - fp / 1.5);
        float3 base = _BaseColor.rgb * (0.95 + 0.10 * macro) * (1.0 + 2.0 * _PlateOrigin.w * panelTone) * (1.0 + 0.08 * streak);

        // staggered plate courses (6.25 x 3.125 m; odd courses shifted by half a plate), joints and per-plate tone
        float course = floor((q.y - _PlateOrigin.y) / _Plate.y);
        float shift = (frac(course * 0.5) > 0.25) ? 0.5 * _Plate.x : 0.0;
        float pz = q.x - _PlateOrigin.x + shift;
        float jh = RingFilteredGrid1(q.y - _PlateOrigin.y, _Plate.y, _Plate.z, fpy, 4.0);
        float jv = RingFilteredGrid1(pz, _Plate.x, _Plate.z, fpz, 4.0);
        float joint = saturate(jh + jv - jh * jv);
        float plateTone = (RingHash21(int2((int)floor(pz / _Plate.x), (int)course) + int2(911, 37)) - 0.5) * saturate(2.0 - fp / 1.0);
        base *= 1.0 + 2.0 * _PlateOrigin.z * plateTone;
        // inset line 0.3 m inside every plate edge (a bevelled inner panel: one more step of the scale ladder)
        float pu = pz - _Plate.x * floor(pz / _Plate.x);
        float pv = (q.y - _PlateOrigin.y) - _Plate.y * course;
        float insetFade = saturate(2.0 - fp / (4.0 * _PlateInset.y + 0.02));
        float inset = max(max(RingFilteredLine(pu - _PlateInset.x, _PlateInset.y, fpz), RingFilteredLine(_Plate.x - pu - _PlateInset.x, _PlateInset.y, fpz)),
                          max(RingFilteredLine(pv - _PlateInset.x, _PlateInset.y, fpy), RingFilteredLine(_Plate.y - pv - _PlateInset.x, _PlateInset.y, fpy)));
        inset *= insetFade;

        // 25 m sub-panels and the 100 m seam grid
        float g1 = RingFilteredGrid1(q.x - _Seam.z, _Seam.x, _Seam.y, fpz, 3.0);
        float g2 = RingFilteredGrid1(q.y - _Seam.w, _Seam.x, _Seam.y, fpy, 3.0);
        float seam = saturate(g1 + g2 - g1 * g2);
        float h1 = RingFilteredGrid1(q.x - _Seam.z, _SubPanel.x, _SubPanel.y, fpz, 4.0);
        float h2 = RingFilteredGrid1(q.y - _Seam.w, _SubPanel.x, _SubPanel.y, fpy, 4.0);
        float sub = saturate(h1 + h2 - h1 * h2);
        float portal = FacePortalGroove(q, fp);

        float3 face = lerp(base, base * _Plate.w, joint);
        face = lerp(face, face * _PlateInset.z, inset * (1.0 - joint));
        face = lerp(face, face * _SubPanel.z, sub * (1.0 - seam));
        face = lerp(face, face * _SeamDarken, max(seam, portal * 0.8));

        // analytic ledges: the visible ledge surfaces replace the wall pattern (flat ledge albedo)
        float4 lc = FaceLedges(q.y, fpy, FaceKeyDir());
        float ledge = saturate(lc.x + lc.y + lc.z);
        float3 ledgeAlb = base * (lc.x * _LedgeShade.z + lc.y * _LedgeShade.x + lc.z * _LedgeShade.y) / max(ledge, 1e-4);
        face = lerp(face, ledgeAlb, ledge * (1.0 - trim));

        float3 trimCol = _TrimColor.rgb * (1.0 + 0.04 * f.color.g);
        s.albedo = lerp(face, trimCol, trim);
        s.smoothness = lerp(_Smoothness * (1.0 - 0.5 * max(max(seam, portal), joint)), _TrimSmoothness, trim);
        s.occlusion = lerp(1.0, f.color.r, 0.8);
        // the undersides get the light the sunlit hull 200 m below throws back up (they face it; NdotL of the key < 0)
        s.emission = ledgeAlb * (lc.x * _PlateInset.w * (1.0 - trim));
    }

    // Key visibility on the face: 0 on the ledge undersides (they face down, NdotL < 0), L.y / L.x on the ledge tops
    // (NdotL 0.12 instead of the face's 0.99), 0 in the 0.5 m ledge shadows, 1 elsewhere (the trim is untouched).
    float RingKeyVisibilityHook(RingFragData f)
    {
        float3 dpx = ddx(f.positionFlatWS);
        float3 dpy = ddy(f.positionFlatWS);
        float fpy = max(abs(dpx.y) + abs(dpy.y), 0.25 * f.footprint);
        if (f.color.b > 0.5) return 1.0;
        float3 L = FaceKeyDir();
        float4 lc = FaceLedges(f.positionFlatWS.y, fpy, L);
        float topK = saturate(L.y) / max(L.x, 0.05);
        return saturate(1.0 - lc.x - lc.z * (1.0 - topK) - lc.w);
    }

    float3 RingPostHook(RingFragData f, float3 c)
    {
        float k = smoothstep(_Dark.x, _Dark.y, f.viewDist);
        return lerp(c, PAL_SpaceBlack, k);
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
