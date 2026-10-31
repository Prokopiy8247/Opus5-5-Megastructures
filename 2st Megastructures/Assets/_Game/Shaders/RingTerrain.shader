// M02 RingTerrain - the one persistent terrain clipmap (MASTER_PROMPT 14.5.2, 14.7 H1/H7, DEVELOPMENT.md ADR-002).
// 9 levels x 128 x 128 quads with per-quad vertices; the vertex stage (identical in all four passes) places every level
// around _RingCam (so capture cameras work): level origins snapped to 2 x spacing, quads inside the next finer level
// collapsed (the fine boundary always lies on coarse grid lines), CDLOD geomorph over the outer 15 % (odd vertices
// slide onto their even neighbour, so the fine edge equals the coarse edge exactly at alpha = 1), heights from
// RingHeight at the morphed position (on coarse quads of 2 m and more limited by the continuous path ceiling of
// RingTerrainShared.hlsl, so no coarse triangle buries a path strip), vertex normals from central differences at the
// (morphed) level spacing (shadow bias, DepthNormals), RingBend after; the lit pass re-derives the normal per pixel from
// the same height (ceiling included) at the pixel footprint, so the shading never shows the triangulation or the level
// hand-over and always matches the drawn surface.
// When _RingSet.x >= 1 vertices collapse onto x = 114 and pixels beyond are clipped (14.5.5).
// Albedo = RingGroundAlbedo + near detail (RingTerrainShared.hlsl, shared with the grass tips), wet banks near the
// waterline, and an analytic, box-filtered river where the mesh is too coarse to carve it (no popping, no puddles).
Shader "Megastructure/RingTerrain"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0, 0.6)) = 0.15
        _WetDarken ("Wet bank albedo factor", Range(0.3, 1)) = 0.62
        _WetSmoothness ("Wet bank smoothness", Range(0, 0.6)) = 0.32
        _SkirtDepth ("Skirt depth (m)", Float) = 2
        _ShadowKeep ("Shadow pass keep radius (m)", Float) = 420
        _FarWater ("Far water: spacing ramp start, end (m)", Vector) = (4, 12, 0, 0)
    }

    HLSLINCLUDE
    #define RING_HAS_VERTEX_HOOK
    #define RING_HAS_CLIP_HOOK
    #include "Include/RingLitCore.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _FarWater;
        float _Smoothness;
        float _WetDarken;
        float _WetSmoothness;
        float _SkirtDepth;
        float _ShadowKeep;
    CBUFFER_END

    #include "Assets/_Game/Scripts/Runtime/Terrain/RingTerrainShared.hlsl"

    // Vertex data written by ClipmapBuilder (ClipmapLayout.cs is the C# twin):
    //   positionOS = (gx, skirt ? -1 : 0, gz)  grid corner indices 0..128 of this level
    //   uv0        = quad centre in grid units (qx + 0.5, qz + 0.5)
    //   uv1        = (spacing s = 0.5 x 2^k (exact power of two), level k)
    // Everything below is exact in float32: multiples of powers of two well inside the 24-bit mantissa.
    void TerrainVertex(inout RingVertexData v, bool shadowPass)
    {
        float s = v.uv.z;
        float level = v.uv.w;
        float2 g = v.positionOS.xz;
        float2 q = v.uv.xy;
        bool skirt = v.positionOS.y < -0.5;
        float2 cam = _RingCam.xz;

        float2 o = floor(cam * (0.5 / s) + 0.5) * (2.0 * s);   // origin snapped to 2 x spacing
        float2 c = o + (q - 64.0) * s;                          // quad centre (world xz)
        float2 p = o + (g - 64.0) * s;                          // vertex (world xz)

        bool collapse = false;
        if (level > 0.5)
        {
            // finer level: spacing s / 2, origin snapped to s, half-extent 64 x s / 2 = 32 s
            float2 of = floor(cam * (1.0 / s) + 0.5) * s;
            float2 dq = abs(c - of);
            collapse = max(dq.x, dq.y) < 32.0 * s;
        }
        if (shadowPass)
        {
            // terrain beyond the cascades (220 m) never needs to be in the shadow map
            float2 dc = abs(c - cam);
            collapse = collapse || max(dc.x, dc.y) > _ShadowKeep;
        }
        if (collapse)
        {
            v.positionWS = float3(c.x, 0.0, c.y);   // all four corners equal: zero area, never rasterised
            v.normalWS = float3(0.0, 1.0, 0.0);
            v.tangentWS = float4(1.0, 0.0, 0.0, 1.0);
            v.custom = float4(s, level, 0.0, 0.0);
            return;
        }

        // geomorph over the outer 15 % (54.4 s .. 61.4 s from the true camera, Chebyshev); the boundary vertices
        // (>= 63 s away) are always fully morphed, so the snap of either level never shows. The outermost level
        // has no coarser neighbour and keeps its detail.
        float alpha = 0.0;
        if (level < 7.5)
        {
            float2 dv = abs(p - cam);
            alpha = saturate((max(dv.x, dv.y) - 54.4 * s) / (7.0 * s));
        }
        float2 odd = frac(g * 0.5) * 2.0;           // 1 for odd grid indices (world parity = grid parity)
        p -= odd * (s * alpha);

        p.x = RingPlainClampX(p.x);                 // 14.5.5: collapse onto the wall face in the plain

        float setv = _RingSet.x;
        float e = s * (1.0 + alpha);                // morphed quad size: level spacing, coarse spacing at the edge
        float h = RingHeight(p, setv);
        float hxp = RingHeight(p + float2(e, 0.0), setv), hxm = RingHeight(p - float2(e, 0.0), setv);
        float hzp = RingHeight(p + float2(0.0, e), setv), hzm = RingHeight(p - float2(0.0, e), setv);

        // keep coarse triangles from poking through the path strips: the path ceiling of RingTerrainShared.hlsl,
        // continuous in e (weight 0 up to e = 2 m, 1 from 3 m) and evaluated with the morphed spacing, so the fine and
        // coarse edges still agree exactly; the normal samples get the same ceiling, so the vertex normal (shadow bias,
        // DepthNormals) and the per-pixel normal describe the surface that is drawn
        float cw = RingCeilWeight(e);
        if (cw > 0.0)
        {
            float sd = RingStripDist(p);
            h = RingPathCeil(h, sd, e, cw);
            if (RingCeilNear(sd, e, e))
            {
                hxp = RingPathCeil(hxp, RingStripDist(p + float2(e, 0.0)), e, cw);
                hxm = RingPathCeil(hxm, RingStripDist(p - float2(e, 0.0)), e, cw);
                hzp = RingPathCeil(hzp, RingStripDist(p + float2(0.0, e)), e, cw);
                hzm = RingPathCeil(hzm, RingStripDist(p - float2(0.0, e)), e, cw);
            }
        }
        float inv2e = 0.5 / e;
        float hx = (hxp - hxm) * inv2e;
        float hz = (hzp - hzm) * inv2e;
        if (skirt) h -= _SkirtDepth;

        v.positionWS = float3(p.x, h, p.y);
        v.normalWS = normalize(float3(-hx, 1.0, -hz));
        v.tangentWS = float4(normalize(float3(1.0, hx, 0.0)), 1.0);
        v.custom = float4(e, level, alpha, 0.0);
    }

    void RingClipHook(RingFragData f)
    {
        RingPlainClip(f.positionFlatWS.x);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        float2 p = f.positionFlatWS.xz;
        float fpw = RingSurfaceFootprint(p, f.footprint);

        // per-pixel normal: analytic central differences of RingHeight at the pixel footprint (5 cm .. 256 m). The
        // shading is then a function of the world position only: no Gouraud diagonals across the coarse quads (their
        // parallel diagonals fan out in perspective) and no shading step where one level hands over to the next.
        // The vertex normal (level spacing) still drives the shadow bias and the DepthNormals pass. The coarse-level
        // path ceiling of the vertex stage enters with the interpolated morphed spacing (custom.x), so the shading
        // follows the trimmed geometry instead of the untrimmed trench walls.
        float en = clamp(fpw, 0.05, 256.0);
        float setv = _RingSet.x;
        float eV = f.custom.x;
        float cw = RingCeilWeight(eV);
        bool ceilOn = false;
        if (cw > 0.0) ceilOn = RingCeilNear(RingStripDist(p), en, eV);
        float2 grad = float2(0.0, 0.0);
        [loop] for (int k = 0; k < 4; k++)            // a loop keeps one inlined copy of RingHeight (code size)
        {
            float sgn = (k & 1) == 0 ? 1.0 : -1.0;
            float2 d = (k < 2) ? float2(sgn, 0.0) : float2(0.0, sgn);   // +x, -x, +z, -z
            float2 q = p + d * en;
            float hq = RingHeight(q, setv);
            if (ceilOn) hq = RingPathCeil(hq, RingStripDist(q), eV, cw);
            grad += d * hq;
        }
        grad *= 0.5 / en;                                  // central differences: (h(+e) - h(-e)) / 2e
        float3 nPx = normalize(float3(-grad.x, 1.0, -grad.y));
        f.normalFlatWS = nPx;
        f.normalWS = normalize(RingBendRotate(nPx, f.theta));

        float3 alb = RingTerrainAlbedo(p, fpw);
        float rd = RingRiverDist(p);

        // wet banks just above the waterline (RiverDist about 15..21 m on the 1:3 bank)
        float above = f.positionFlatWS.y - RING_WATER_Y;
        float wet = (1.0 - smoothstep(0.0, 0.9, above)) * (1.0 - smoothstep(19.0, 23.0, rd));
        alb *= lerp(1.0, _WetDarken, wet);
        float smoothV = lerp(_Smoothness, _WetSmoothness, wet);

        // analytic far river where the clipmap is coarser than 4 m (f.custom.x = morphed level spacing)
        float wc = RingFarWaterCover(p, rd, fpw, f.custom.x, _FarWater.xy, _RingSet.x);
        float3 upB = RingBendRotate(float3(0.0, 1.0, 0.0), f.theta);
        f.normalWS = normalize(lerp(f.normalWS, upB, wc));
        float F = RingSchlick02(saturate(dot(f.normalWS, f.viewDirWS)));
        float3 body = RingWaterBody(0.9) * (1.0 - F);   // mean depth of the cross-section

        s.albedo = lerp(alb, body, wc);
        s.smoothness = lerp(smoothV, 0.0, wc);
        s.metallic = 0.0;
        s.occlusion = 1.0;
        f.custom.z = fpw;   // for the far-water specular (derivatives must not be taken inside its branch)
        f.custom.w = wc;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment TerrainFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            void RingVertexHook(inout RingVertexData v) { TerrainVertex(v, false); }
            #include "Include/RingLitPass.hlsl"

            // RingFrag (RingLitPass.hlsl) plus the water specular of the analytic far river, before RingFog.
            half4 TerrainFrag(RingVaryings i) : SV_Target0
            {
                RingFragData f = RingMakeFrag(i);
                RingClipHook(f);
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

                half3 c = RingShade(inputData, s, f.positionFlatWS, f.theta);

                // far terrain beyond the squares' terminator keeps daylight, exactly like the arch above it (ADR-012):
                // the clipmap edge and the arch share one lighting model, so the edge never shows a seam
                float camD = length(f.positionWS - _RingCam.xyz);
                float farLit = smoothstep(800.0, 2000.0, camD) * RingTerminatorLit(f.theta * RING_RAD2DEG);
                if (farLit > 0.0)
                {
                    float3 extra = max(RingGroundLightDay() * RingLitSideScale() - RingGroundLightNow(), 0.0);
                    c += (half3)(s.albedo * extra * (farLit * RingKeyAtten(f.positionFlatWS.xz)));
                }

                float wc = f.custom.w;
                if (wc > 0.0)
                {
                    Light ml = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                    float3 L = RingBendRotate(ml.direction, f.theta);
                    float atten = ml.shadowAttenuation * ml.distanceAttenuation * RingKeyAtten(f.positionFlatWS.xz);
                    float fpw = f.custom.z;
                    float a2 = 0.0064 * 0.0064;                       // roughness floor 0.08 (alpha = 0.08^2)
                    RingWaterRipples(0.0, 0.0, 0.0, fpw, a2);         // filtered-away ripple variance only
                    float3 upB = RingBendRotate(float3(0.0, 1.0, 0.0), f.theta);
                    float3 spec = RingWaterSpecular(inputData.normalWS, f.viewDirWS, upB, L, ml.color * atten, a2,
                                                    RingBridgeReflOcc(f.positionFlatWS.xz));
                    c += (half3)(spec * wc);
                }
                c = RingFog(c, f.positionWS, f.positionFlatWS.y);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define RING_PASS_SHADOW
            void RingVertexHook(inout RingVertexData v) { TerrainVertex(v, true); }
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #define RING_PASS_DEPTH
            void RingVertexHook(inout RingVertexData v) { TerrainVertex(v, false); }
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #define RING_PASS_DEPTHNORMALS
            void RingVertexHook(inout RingVertexData v) { TerrainVertex(v, false); }
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
