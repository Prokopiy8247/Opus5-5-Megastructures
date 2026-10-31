// RingOrbital - the WHOLE ring seen from space, for the capture-only orbital stills (QA/OrbitalView.cs, -shots orbit).
// Not used by the walk. Lit by the outside star (_StarDirOut, key 3.4 x light calibration, StarOutside) plus a little
// SpaceBounce; the sunlit interior (floor and inner wall faces, seen through the 8 km opening of the rim walls) uses the
// shared day lighting and ground colour (RingGroundAlbedo, RingCloudCover) under a blue veil of its own air.
// Every procedural line is filtered with the pixel footprint (RingFilteredLine / RingFilteredGrid1), so the 100 m - 2 km
// structure converges to its mean instead of shimmering at 30-50 km.
// Parts (uv.x): 0 hull, 1 right outer face (faces the star), 2 wall tops, 3 inner wall faces, 4 floor, 5 left outer face.
Shader "Megastructure/RingOrbital"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Include/RingSkyArch.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _OrbitalUnused;
        CBUFFER_END

        struct OrbAttr { float3 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
        struct OrbVary { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

        OrbVary OrbVert(OrbAttr a)
        {
            OrbVary o;
            o.positionWS = TransformObjectToWorld(a.positionOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(a.normalOS);
            o.uv = a.uv;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OrbVert
            #pragma fragment OrbFrag

            #define ORB_AXIS_Y 16000.0
            #define ORB_AXIS_Z 660.0

            // gaussian point light of radius sig0 (m) at distance dist (m), drawn at least 0.7 footprint wide with its
            // energy kept: far points stay faint instead of sparkling
            float OrbPoint(float dist, float sig0, float fp)
            {
                float sig = max(0.7 * fp, sig0);
                return exp(-(dist * dist) / (sig * sig)) * (sig0 * sig0) / (sig * sig);
            }

            float4 OrbFrag(OrbVary i) : SV_Target
            {
                float part = round(i.uv.x);
                float3 P = i.positionWS;
                float3 N = normalize(i.normalWS);
                float d = length(_WorldSpaceCameraPos - P);
                float fp = d * max(_RingCam.w, 1e-6);                       // metres per pixel
                float2 q = float2(P.y - ORB_AXIS_Y, P.z - ORB_AXIS_Z);
                float r = max(length(q), 1.0);
                float phi = atan2(q.y, -q.x);                               // 0 at the bottom (the walk), +z positive
                float arcR = r * phi;                                       // metres along the circle of radius r

                float3 L = _StarDirOut.xyz;
                float3 key = PAL_StarOutside * (3.4 * RING_LIGHT_CAL);
                float3 amb = PAL_SpaceBounce * (1.6 * RING_LIGHT_CAL);
                float3 col;
                float3 emit = 0.0;

                if (part < 0.5)
                {
                    // hull: dark plating, ribs every 400 m along the axis whose +X faces catch the star (L is nearly +X),
                    // circumferential seams, panel tones
                    float xr = frac((P.x + 40000.0) / 800.0) * 800.0;
                    float litFace = RingFilteredLine(abs(xr - 70.0), 140.0, fp);
                    float darkFace = RingFilteredLine(abs(xr - 210.0), 140.0, fp);
                    float seam = RingFilteredGrid1(arcR, 400.0, 8.0, fp, 3.0);
                    float tone = 0.8 + 0.4 * RingHash21(int2((int)floor((P.x + 40000.0) / 800.0), (int)floor(arcR / 600.0 + 1000.0)));
                    float3 alb = PAL_HullPale * (0.5 * tone) * (1.0 - 0.35 * seam);
                    float ndl = saturate(dot(N, L));
                    float ndlRib = saturate(dot(normalize(N + float3(1.3, 0.0, 0.0)), L));
                    float lit = lerp(ndl, ndlRib, litFace) * (1.0 - 0.7 * darkFace);
                    col = alb * (key * lit + amb);
                    // service lights along the dark hull (one candidate per 300 m cell, 4 % lit)
                    float2 g = float2(P.x + 40000.0, arcR) / 300.0;
                    float2 gi = floor(g);
                    [unroll] for (int hj = -1; hj <= 1; hj++)
                    [unroll] for (int hi = -1; hi <= 1; hi++)
                    {
                        float2 cc = gi + float2(hi, hj);
                        uint h = RingPcg3(int3((int)cc.x, (int)cc.y, 41));
                        if (RingU01(h) > 0.04) continue;
                        float2 pp = cc + float2(RingU01(RingPcg(h + 1u)), RingU01(RingPcg(h + 2u)));
                        float3 tint = RingU01(RingPcg(h + 3u)) < 0.75 ? PAL_WarmLamp : PAL_CityCool;
                        emit += tint * (8.0 * OrbPoint(length((g - pp) * 300.0), 12.0, fp));
                    }
                }
                else if (part < 1.5 || part > 4.5)
                {
                    // rim-wall outer faces: concentric ledges (250 m) and grooves (1 km), radial seams (1 deg), 24 raised
                    // buttresses (15 deg), per-band tones, scattered service lights, and the walk's pier and door glowing
                    // at the bottom (r 16 000, phi 0) of the face the walk exits through
                    float band = floor(r / 250.0);
                    float sector = floor(phi * (180.0 / RING_PI));
                    float tone = 0.72 + 0.56 * RingHash21(int2((int)band, (int)sector + 400));
                    float groove = RingFilteredGrid1(r, 1000.0, 40.0, fp, 3.0);
                    float ledge = RingFilteredGrid1(r, 250.0, 8.0, fp, 3.0);
                    float radial = RingFilteredGrid1(arcR, r * 0.0174533, 10.0, fp, 3.0);
                    float terrace = RingFilteredGrid1(r + 1500.0, 3000.0, 320.0, fp, 1.0);
                    float buttress = RingFilteredGrid1(arcR + 300.0, r * 0.2617994, 600.0, fp, 1.0);
                    float bShadow = RingFilteredGrid1(arcR + 300.0 - 450.0, r * 0.2617994, 300.0, fp, 1.0);
                    float3 alb = PAL_HullPale * (0.24 * tone) * (1.0 - 0.45 * groove - 0.25 * ledge - 0.25 * radial - 0.55 * terrace - 0.45 * bShadow)
                               * (1.0 + 0.45 * buttress);
                    float ndl = saturate(dot(N, L));
                    col = alb * (key * ndl + amb);
                    // service lights: one candidate per 250 m x 250 m cell, 5 % lit, warm or cool
                    float2 g = float2(r, arcR) / 250.0;
                    float2 gi = floor(g);
                    [unroll] for (int lj = -1; lj <= 1; lj++)
                    [unroll] for (int li = -1; li <= 1; li++)
                    {
                        float2 cc = gi + float2(li, lj);
                        uint h = RingPcg3(int3((int)cc.x, (int)cc.y, part > 4.5 ? 77 : 11));
                        if (RingU01(h) > 0.05) continue;
                        float2 pp = cc + float2(RingU01(RingPcg(h + 1u)), RingU01(RingPcg(h + 2u)));
                        float3 tint = RingU01(RingPcg(h + 3u)) < 0.7 ? PAL_WarmLamp : PAL_CityCool;
                        emit += tint * (6.0 * OrbPoint(length((g - pp) * 250.0), 10.0, fp));
                    }
                    if (part < 1.5)
                    {
                        float dp = length(float2(r, arcR) - float2(16000.0, 0.0));
                        emit += PAL_BeaconAmber * (40.0 * OrbPoint(dp, 45.0, fp)) + PAL_WarmLamp * (1.5 * OrbPoint(dp, 260.0, fp));
                    }
                }
                else if (part < 2.5)
                {
                    // wall tops (120 m bands at r 4 km): pale ceramic between the star light and the interior's glow
                    float3 alb = PAL_CeramicShade * 0.5;
                    col = alb * (key * saturate(dot(N, L)) + amb + RingGroundLightDay() * 0.25);
                }
                else
                {
                    // the sunlit interior: floor (fields, forests, lakes, towns, clouds) and inner wall faces in daylight,
                    // seen through the ring's own air (a blue veil that grows with the depth of the view into it)
                    float3 day = RingGroundLightDay();
                    float3 c;
                    if (part < 3.5)
                    {
                        float hgt = saturate((16000.0 - r) / 12000.0);
                        c = PAL_WallTone * day * (0.75 - 0.25 * hgt);
                    }
                    else
                    {
                        float3 alb = RingGroundAlbedo(16000.0 * phi, P.x, fp);
                        float cloud = RingCloudCover(16000.0 * phi, P.x, fp);
                        c = alb * day;
                        c = lerp(c, PAL_CloudWhite * day * 1.1, cloud * 0.85);
                    }
                    float veil = 0.25 + 0.35 * saturate((d - 20000.0) / 40000.0);
                    col = lerp(c, PAL_SkyDayMid * (RingLuma(day) * 1.2), veil);
                }
                col += emit;
                return float4(min(col, 60.0), 1.0);
            }

            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex OrbVert
            #pragma fragment OrbDepth
            half OrbDepth(OrbVary i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
