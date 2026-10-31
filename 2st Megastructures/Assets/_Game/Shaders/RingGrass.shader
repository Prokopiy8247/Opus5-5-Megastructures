// M04 RingGrass - grass tufts (_Mode 0) and flowers (_Mode 1) drawn by one RenderMeshIndirect call each, with no
// per-frame CPU work (MASTER_PROMPT 14.2.6 M04, 14.5.2). The vertex stage derives every instance from SV_InstanceID:
// a camera-snapped world grid window (_Grid: cell, cells per side, candidates per cell, acceptance), a PCG hash of the
// WORLD cell (so a tuft never changes when the window moves), exclusions (path, river, pad, capsule, bridge, levee,
// apron, the wall in the plain), the ground height RingHeight, and the shared bend. Fade: every instance owns a hash
// threshold and shrinks smoothly into the ground when the distance fade passes it (a dither in the instance domain on
// a stable world pattern: no per-pixel shimmer, no pop). Before the fade starts (9.9 -> 22 m) the tufts hand their
// shading over to the terrain: terrain normal, occlusion 1, root ground x 0.92 and tip ground x 1.08 (the terrain
// near-detail colour of RingTerrainShared.hlsl + 8 %), whose visible canopy mean is the ground colour, and they get
// shorter (x 1 at 12 m -> x 0.5 at the fade end), so the dither band is a low fringe of ground-coloured blades instead
// of a ragged hedge line; shrinking tufts converge to that ground colour, so the fade is invisible.
// Flowers: green stems (never darker than the meadow) whose colour converges to the ground with their true / drawn
// width and whose lighting hands over to the terrain normal from 3 to 12 m, so beyond a few metres they are invisible
// and only the energy-preserving 2 px heads remain; flowers cast no shadows (the 4 mm stems' self-shadow made them
// dark needles, critique run04 C07).
Shader "Megastructure/RingGrass"
{
    Properties
    {
        _Mode ("Mode (0 tuft, 1 flower)", Float) = 0
        _Grid ("Grid: cell (m), cells per side, candidates per cell, acceptance", Vector) = (0.35, 160, 2, 1)
        _Fade ("Fade: start, end, max radius (m), seed", Vector) = (22, 27.75, 28, 0)
        _Shape ("Shape: height, +- (m), base width, tip width (m)", Vector) = (0.26, 0.10, 0.10, 0.006)
        _Sway ("Sway: tip amplitude (m), gust gain, omega (rad/s), phase per m of x", Vector) = (0.06, 0.8, 0.9, 0.35)
        _Smoothness ("Smoothness", Range(0, 0.5)) = 0.15
    }

    HLSLINCLUDE
    #define RING_INDIRECT
    #define RING_HAS_VERTEX_HOOK
    #define RING_HAS_CLIP_HOOK
    #include "Include/RingLitCore.hlsl"
    // RenderMeshIndirect helpers (Editor/Data/Resources/CGIncludes/UnityIndirect.cginc, plain HLSL): one command,
    // startInstance 0, so GetIndirectInstanceID is the 0-based instance on every API (InstancedField contract)
    #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
    #include "UnityIndirect.cginc"

    CBUFFER_START(UnityPerMaterial)
        float4 _Grid;
        float4 _Fade;
        float4 _Shape;
        float4 _Sway;
        float _Mode;
        float _Smoothness;
    CBUFFER_END

    #include "Assets/_Game/Scripts/Runtime/Terrain/RingTerrainShared.hlsl"

    // ---------------- exclusions (C# twin: GrassLayout.Excluded) ----------------
    bool RingFieldExcluded(float2 p)
    {
        if (RingPlainClipOn() && p.x >= RingWallX() - 0.05) return true;             // the wall (14.5.5)
        if (abs(p.x) < 3.5 && p.y > -3.0 && p.y < 31.0) return true;                 // capsule, gangway, landing pad
        if (abs(p.x) < 3.5 && p.y > 116.0 && p.y < 184.0) return true;               // under the bridge deck
        if (abs(p.x) < 12.0 && abs(p.y - 400.0) < 4.5) return true;                  // footbridge arch footings
        if (p.x > RING_LEVEE.x - 0.5 && p.x < RING_LEVEE.y + 0.5 &&
            p.y > RING_LEVEE.z - 0.5 && p.y < RING_LEVEE.w + 0.5) return true;       // inside the levee footprint
        if (p.x > 97.5 && p.x < 114.5 && p.y > 639.5 && p.y < 680.5) return true;    // plain apron
        if (RingPathDist(p) < 1.7) return true;                                      // path strip 3.0 m + kerbs
        if (RingRiverDist(p) < 19.0) return true;                                    // river and lower bank
        return false;
    }

    // ---------------- instance placement ----------------
    struct RingFieldInst
    {
        float2 p;       // world xz of the instance root
        uint h;         // per-instance hash
        float dist;     // horizontal distance to the camera
        float w;        // fade weight 0..1 (instance-domain dither)
        bool alive;
    };

    RingFieldInst RingFieldInstance(uint id)
    {
        RingFieldInst o;
        float cell = _Grid.x;
        uint n = (uint)(_Grid.y + 0.5);
        uint cand = max((uint)(_Grid.z + 0.5), 1u);
        uint c = id / cand;
        uint k = id - c * cand;
        uint cz = c / n;
        uint cx = c - cz * n;
        int2 snap = (int2)floor(_RingCam.xz / cell + 0.5);
        int2 ic = snap + int2((int)cx, (int)cz) - (int)(n / 2u);
        uint seed = (uint)(_Fade.w + 0.5);
        uint h = RingPcg3(int3(ic.x, ic.y, (int)(k + seed * 7u)));
        float2 j = float2(RingU01(h), RingU01(RingPcg(h ^ 0x9E3779B9u)));
        o.p = (float2(ic) + j) * cell;
        o.h = RingPcg(h + 0x68E31DA4u);
        float2 dv = o.p - _RingCam.xz;
        o.dist = sqrt(dot(dv, dv));
        float accept = RingU01(RingPcg(o.h + 17u));
        float fade = 1.0 - smoothstep(_Fade.x, _Fade.y, o.dist);
        float thr = RingU01(RingPcg(o.h + 101u));
        o.w = saturate((fade * 1.15 - thr) / 0.15);
        o.alive = (cz < n) && (o.dist < _Fade.z) && (accept < _Grid.w) && (o.w > 0.0);
        return o;
    }

    float RingFieldRand(uint h, uint salt) { return RingU01(RingPcg(h + salt * 0x2C1B3C6Du)); }

    // Collapsed instance: every vertex on one point, zero area, never rasterised.
    void RingFieldCollapse(inout RingVertexData v, float2 p)
    {
        v.positionWS = float3(p.x, 0.0, p.y);
        v.normalWS = float3(0.0, 1.0, 0.0);
        v.tangentWS = float4(1.0, 0.0, 0.0, 1.0);
        v.color = float4(0.0, 0.0, 0.0, 1.0);
        v.custom = float4(0.0, 0.0, 0.0, 0.0);
    }

    // Ground colour as the terrain shows it at p from the camera (anisotropic footprint at the grazing angle), so the
    // tips match the terrain pixel under the fading tuft (14.5.2).
    float3 RingFieldGround(float2 p, float y0, float dist)
    {
        float camH = max(_RingCam.y - y0, 0.3);
        float sinGraze = camH / max(sqrt(dist * dist + camH * camH), 1e-3);
        float fp = max(dist, 0.05) * max(_RingCam.w, 1e-6) / max(sinGraze, 0.05);
        return RingTerrainAlbedo(p, fp);
    }

    // ---------------- grass tuft: 3 blades x 2 triangles; positionOS = (across -0.5..0.5, height 0..1, blade 0..2) ----
    void GrassTuftVertex(inout RingVertexData v, uint id)
    {
        RingFieldInst I = RingFieldInstance(id);
        if (!I.alive || RingFieldExcluded(I.p)) { RingFieldCollapse(v, I.p); return; }

        float setv = _RingSet.x;
        float y0 = RingHeight(I.p, setv);
        const float e = 0.5;     // the finest clipmap spacing: the same normal the terrain shows here
        float hx = (RingHeight(I.p + float2(e, 0.0), setv) - RingHeight(I.p - float2(e, 0.0), setv)) * (0.5 / e);
        float hz = (RingHeight(I.p + float2(0.0, e), setv) - RingHeight(I.p - float2(0.0, e), setv)) * (0.5 / e);
        float3 tn = normalize(float3(-hx, 1.0, -hz));

        uint h = I.h;
        float r1 = RingFieldRand(h, 1u), r2 = RingFieldRand(h, 2u), r3 = RingFieldRand(h, 3u);
        float r4 = RingFieldRand(h, 4u), r5 = RingFieldRand(h, 5u);
        float blade = floor(v.positionOS.z + 0.5);
        float uu = v.positionOS.x;
        float vv = saturate(v.positionOS.y);

        float H0 = _Shape.x + _Shape.y * (2.0 * r1 - 1.0);          // 0.26 +- 0.10 m
        // shorter toward the fade (x 1 at 12 m -> x 0.5 at the fade end): the dither band becomes a low fringe
        float shrink = lerp(1.0, 0.5, smoothstep(0.55 * _Fade.x, _Fade.y, I.dist));
        float H = H0 * I.w * shrink;
        float hs = H / max(_Shape.x, 1e-3);
        float ang = r2 * RING_TWO_PI + blade * 2.0943951 + (RingFieldRand(h, 11u + (uint)blade) - 0.5) * 0.6;
        float sa, ca;
        sincos(ang, sa, ca);
        float2 dA = float2(ca, sa);                                 // across the blade face
        float2 dN = float2(-sa, ca);                                // blade face normal = lean direction
        float width = lerp(_Shape.z * 0.45, _Shape.w, vv) * lerp(0.6, 1.0, I.w);   // three blades fan over the base
        // far blades never drop far below a pixel (less MSAA crawl); from 22 m on their colour is centred on the ground
        // colour (root x 0.92, tip x 1.08), so the extra coverage does not change the mean colour
        float fpx = max(I.dist, 0.05) * max(_RingCam.w, 1e-6);
        width = max(width, fpx * lerp(0.8, 0.4, vv) * I.w);
        float lean = (0.035 + 0.05 * r3) * hs;
        float swayAmp = _Sway.x * (1.0 + _Sway.y * saturate(_Wind.x)) * hs;
        float phase = RingWrapPhase(_Sway.z, _RingTime.x) + _Sway.w * I.p.x + (r4 - 0.5) * 0.8 + blade * 0.4;
        float sw = sin(phase) * swayAmp;
        const float2 windDir = float2(0.94, 0.34);
        float v2 = vv * vv;
        float2 off = dN * (0.022 + lean * v2) + windDir * (sw * v2) + dA * (uu * width);
        float bend = saturate(dot(off, off) / max(H * H, 1e-6));
        float y = y0 - 0.03 * (1.0 - vv) + vv * H * (1.0 - 0.5 * bend);   // roots 3 cm under the ground

        v.positionWS = float3(I.p.x + off.x, y, I.p.y + off.y);

        // Shading hand-over to the terrain (14.5.2: the fade must be invisible). Near the camera a tuft leans its normal
        // toward the camera (blade facing), darkens its root and occludes the ambient at the root. Between 0.45 x and
        // 1 x the fade start (9.9 -> 22 m) all three blend away by distance: from there on a tuft is lit exactly like
        // the terrain (terrain normal at the root, occlusion 1) and its colour runs from ground x 0.92 at the root to
        // ground x 1.08 at the tip, whose visible mean over the canopy equals the ground colour (Poisson canopy model of
        // the 16 tufts per m2 at 16-27 m, eye 1.7 m and 4.7 m, 1080p and 4K: 0.99-1.02 x ground). The camera-facing
        // lean alone changed the key NdotL by -40 % (toward the star) to +35 % (away) against the terrain beyond 28 m.
        float farW = smoothstep(0.45 * _Fade.x, _Fade.x, I.dist);
        float2 toCam = _RingCam.xz - I.p;
        float side = dot(dN, toCam) >= 0.0 ? 1.0 : -1.0;
        float3 bn = float3(dN.x, 0.0, dN.y) * side;
        v.normalWS = normalize(tn + (0.3 * (1.0 - farW)) * bn);
        v.tangentWS = float4(dA.x, 0.0, dA.y, 1.0);

        float3 ground = RingFieldGround(I.p, y0, I.dist);
        float3 tip = ground * RING_GRASS_TIP_LIFT;
        float3 body = lerp(lerp(PAL_MeadowA, PAL_MeadowB, r5), ground, 0.35);
        float3 root = lerp(body * 0.72, ground * (2.0 - RING_GRASS_TIP_LIFT), farW);
        float3 col = lerp(root, tip, smoothstep(0.0, 1.0, vv));
        col = lerp(ground, col, I.w);                                // a shrinking tuft converges to the ground colour
        v.color = float4(col, lerp(lerp(0.55, 1.0, vv), 1.0, farW)); // a = root occlusion (ambient only)
        v.custom = float4(vv, I.w, 0.0, 0.0);
    }

    // ---------------- flower: stem quad (z 0) + hexagonal head (z 1 centre, z 2 rim (cos, sin)) ----------------
    void FlowerVertex(inout RingVertexData v, uint id, bool shadowPass)
    {
        RingFieldInst I = RingFieldInstance(id);
        if (shadowPass || !I.alive || RingFieldExcluded(I.p)) { RingFieldCollapse(v, I.p); return; }

        float setv = _RingSet.x;
        float y0 = RingHeight(I.p, setv);
        // terrain normal (as the tufts): from 3 to 12 m the flower lighting hands over to it, so a far stem or head is lit
        // exactly like the ground its colour converges to (a camera-leaning stem normal was up to 36 % darker)
        const float e = 0.5;
        float hx = (RingHeight(I.p + float2(e, 0.0), setv) - RingHeight(I.p - float2(e, 0.0), setv)) * (0.5 / e);
        float hz = (RingHeight(I.p + float2(0.0, e), setv) - RingHeight(I.p - float2(0.0, e), setv)) * (0.5 / e);
        float3 tn = normalize(float3(-hx, 1.0, -hz));
        float far = smoothstep(3.0, 12.0, I.dist);
        uint h = I.h;
        float r1 = RingFieldRand(h, 1u), r2 = RingFieldRand(h, 2u), r3 = RingFieldRand(h, 3u), r4 = RingFieldRand(h, 4u);
        float part = floor(v.positionOS.z + 0.5);

        float Hs = _Shape.x * (0.85 + 0.30 * r1) * I.w;            // stem 0.30 m
        float swayAmp = _Sway.x * (1.0 + _Sway.y * saturate(_Wind.x));
        float phase = RingWrapPhase(_Sway.z, _RingTime.x) + _Sway.w * I.p.x + r2 * 2.0;
        float2 sway = float2(0.94, 0.34) * (sin(phase) * swayAmp * I.w);
        float2 lean = float2(r3 - 0.5, r4 - 0.5) * 0.06 * I.w;
        float3 basePos = float3(I.p.x, y0 - 0.02, I.p.y);
        float3 topPos = float3(I.p.x + lean.x + sway.x, y0 + Hs, I.p.y + lean.y + sway.y);

        float3 toCam = _RingCam.xyz - topPos;
        float dCam = max(sqrt(dot(toCam, toCam)), 1e-3);
        toCam /= dCam;
        float fpx = dCam * max(_RingCam.w, 1e-6);                  // metres per pixel at the head
        float3 ground = RingFieldGround(I.p, y0, I.dist);

        float rc = RingFieldRand(h, 6u);
        float3 fc = rc < 0.30 ? PAL_CeramicWhite : (rc < 0.55 ? PAL_PaleGold : (rc < 0.73 ? PAL_FlowerPink :
                    (rc < 0.90 ? PAL_FlowerViolet : PAL_PureWhite)));

        float3 pos;
        float3 nrm;
        float3 col;
        if (part < 0.5)
        {
            // stem: camera-facing about its axis, at least 1 px wide, colour energy kept (true / drawn width)
            float2 hc = toCam.xz;
            float hl = max(sqrt(dot(hc, hc)), 1e-4);
            float2 across = float2(-hc.y, hc.x) / hl;
            float wS = max(max(_Shape.w, fpx), 1e-5);
            float k = saturate(v.positionOS.y);
            pos = lerp(basePos, topPos, k) + float3(across.x, 0.0, across.y) * (v.positionOS.x * wS);
            float3 nLean = normalize(float3(hc.x / hl, 0.0, hc.y / hl) * 0.4 + float3(0.0, 1.0, 0.0));
            nrm = normalize(lerp(nLean, tn, far));
            float3 stemCol = lerp(PAL_MeadowB, PAL_MeadowA, 0.35);  // a green stem, never darker than the meadow
            col = lerp(ground, stemCol, saturate(_Shape.w / wS) * I.w);
        }
        else
        {
            // head: a hexagon tilted toward the camera, at least 2 px across, colour energy kept ((true / drawn)^2)
            float dTrue = _Shape.z;                                  // 0.04 m
            float dHead = max(max(dTrue, 2.0 * fpx), 1e-4);
            float energy = (dTrue / dHead) * (dTrue / dHead) * I.w;
            float2 tilt = float2(r3 - 0.5, r4 - 0.5) * 0.5;
            float3 nd = normalize(float3(toCam.x * 0.55 + tilt.x, 1.0, toCam.z * 0.55 + tilt.y));
            float3 e1 = normalize(cross(float3(0.0, 0.0, 1.0), nd));
            float3 e2 = cross(nd, e1);
            float rad = 0.5 * dHead * lerp(0.7, 1.0, I.w);
            if (part < 1.5)
            {
                pos = topPos + nd * (0.15 * rad);
                col = lerp(ground, lerp(PAL_PaleGold, fc, 0.35), energy);
            }
            else
            {
                pos = topPos + (e1 * v.positionOS.x + e2 * v.positionOS.y) * rad;
                col = lerp(ground, fc, energy);
            }
            nrm = normalize(lerp(nd, tn, 0.7 * far));
        }
        v.positionWS = pos;
        v.normalWS = nrm;
        v.tangentWS = float4(1.0, 0.0, 0.0, 1.0);
        v.color = float4(col, 1.0);
        v.custom = float4(1.0, I.w, 0.0, 0.0);
    }

    // Each pass is its own indirect draw and reads its own args. The hook is instantiated per pass (the pass defines
    // follow this block, so they cannot be tested here): RING_FIELD_HOOK(true) in the shadow caster drops the flowers.
    #define RING_FIELD_HOOK(SHADOWPASS) \
        void RingVertexHook(inout RingVertexData v) \
        { \
            InitIndirectDrawArgs(0); \
            uint id = GetIndirectInstanceID(v.instanceID); \
            if (_Mode < 0.5) GrassTuftVertex(v, id); \
            else FlowerVertex(v, id, SHADOWPASS); \
        }

    void RingClipHook(RingFragData f)
    {
        RingPlainClip(f.positionFlatWS.x);
    }

    void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)
    {
        s.albedo = max(f.color.rgb, 0.0);
        s.smoothness = _Smoothness;
        s.metallic = 0.0;
        s.occlusion = saturate(f.color.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex RingVert
            #pragma fragment RingFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #define RING_PASS_FORWARD
            RING_FIELD_HOOK(false)
            #include "Include/RingLitPass.hlsl"
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
            RING_FIELD_HOOK(true)
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
            RING_FIELD_HOOK(false)
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
            RING_FIELD_HOOK(false)
            #include "Include/RingLitPass.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
