// =====================================================================================================================
// Megastructure/Lattice - honeycomb tier-2 shader field (14.7.7), drawn on a camera-following disc (radius 28 km,
// y = -1087.6, moved on a 100 m grid, rotation locked). The pattern uses ABSOLUTE world xz, so it is world-fixed.
//   * World xz relative to the S0 centre (_LatticeOrigin) -> flat-top axial coordinates with size = 760 / sqrt(3):
//     q = (2/3 x) / size, r = (-x/3 + sqrt(3)/3 z) / size, cube rounding (Red Blob Games) -> cell (q, r) and centre.
//   * Hex distance to the cell centre (apothem metric over the normals at 30, 90, 150 deg): hole where < 350
//     (circumradius 404.15), web 350..380, a lit rim band 350..356 on the web.
//   * Holes: the view ray is followed down into the hole to the far wall (analytic ray-hexagon exit), the wall at that
//     depth is lit by the main light and the trilight ambient with albedo x exp(-depth / 140) (as the tier 0/1 tubes),
//     blended into FOG_DOWN of the Exterior domain: a vertical gradient that goes FOG_DOWN-dark with depth.
//   * Webs: _BaseColor (CONC_MID) lit from above, rim x 1.35.
//   * One amber lamp per cell at (cx, cz - 269) (the neighbour-bridge midpoint), drawn as an analytic soft dot of at
//     least _MinPixels (2.5 px) in true screen pixels (inverse screen Jacobian), with the per-lamp ignite of
//     _AbLampIgnite. The lamps of the cells +/- b1 are evaluated too, because a dot stretched along z at grazing angles
//     can reach into the neighbouring cell.
//   * Footprint rules (fwidth of the hex distance): the 6 m rim fades to its band mean under 1.5 px; the 60 m webs are
//     held at 1.5 px with contrast scaled by true/held width (mean preserved, no aliasing, cells stay readable with
//     falling contrast); the whole cell (holes, webs, lamps) fades to the cell-average tone when a cell drops under
//     3-6 px, so the field becomes smooth haze by ~12-15 km.
//   * Exterior fog domain; dithered cross-fade against tier 1 over _DistFade (5.4-6.6 km, mode +1 = far tier).
// Passes: ForwardLit, DepthOnly, DepthNormals (no shadows: far beyond the cascades).
// =====================================================================================================================
Shader "Megastructure/Lattice"
{
    Properties
    {
        [MainColor] _BaseColor ("Web Concrete", Color) = (0.365, 0.384, 0.404, 1)
        [HDR] _EmissionColor ("Lamp Color", Color) = (1.0, 0.706, 0.353, 1)
        _EmissionStrength ("Lamp Strength", Float) = 8
        _MinPixels ("Lamp Minimum Diameter px", Float) = 2.5
        _LampRadius ("Lamp True Radius m", Float) = 0.3
        _LatticeOrigin ("Lattice Origin (cell 0 centre x, z)", Vector) = (0, 410, 0, 0)
        _LatticeParams ("Size, Hole Apothem, Rim Width, Lamp Offset", Vector) = (438.7975, 350, 6, 269)
        _TopY ("Terrace Y", Float) = -1087
        _DepthFalloff ("Depth Falloff m", Float) = 140
        _TubeDepth ("Tube Depth m", Float) = 600
        [Enum(Shaft,0,Tunnel,1,Exterior,2)] _AbDomain ("Fog Domain", Float) = 2
        _AbFade ("Tier Fade (dithered)", Float) = 1
        _ThinLine ("ThinLine (unused)", Float) = 0
        _DistFade ("Distance Fade (start, end, mode)", Vector) = (5400, 6600, 1, 0)
        _KeyLightMask ("Key Light Mask (main light only)", Range(0, 1)) = 1
        _HoneyAir ("Honeycomb Air (aerial amount, aerial length m, tube-air depth m, tube-air amount)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" "IgnoreProjector" = "True" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _LatticeOrigin;
            float4 _LatticeParams;
            float4 _DistFade;
            float4 _HoneyAir;
            float  _EmissionStrength;
            float  _MinPixels;
            float  _LampRadius;
            float  _TopY;
            float  _DepthFalloff;
            float  _TubeDepth;
            float  _AbDomain;
            float  _AbFade;
            float  _ThinLine;
            float  _KeyLightMask;
        CBUFFER_END

        #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"
        #include "Assets/_Game/Shaders/Include/AbyssFog.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment LatticeFrag
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssLighting.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"

            #include "Assets/_Game/Shaders/Include/AbyssHoneycomb.hlsl"

            // Lamp dots (R2-21 / lead D4): an additive dot over the 0.2-0.5 haze of the far field reads cream or salmon (lead_b01
            // KS7b cores (184, 148, 141), hue 10, S 0.24), so a resolved dot is a small amber disc over the fogged cell, with
            // coverage saturate(gain x Gaussian) (about 1 on the core pixel) and the hue-safe lamp colour
            #define AB_LAMP_DISC_GAIN 2.5
            #define AB_LAMP_MEAN_OCCL 0.6       // unresolved / far dots: partial occlusion as the R2 checkpoint (horizon tests)

            // Unit normals of the hexagon flats (30, 90, 150 degrees from +X toward +Z).
            static const float2 HEX_N0 = float2(0.8660254, 0.5);
            static const float2 HEX_N1 = float2(0.0, 1.0);
            static const float2 HEX_N2 = float2(-0.8660254, 0.5);

            float HexDist(float2 d)
            {
                return max(max(abs(dot(d, HEX_N0)), abs(d.y)), abs(dot(d, HEX_N2)));
            }

            // Horizontal distance from local point d (inside the hole) along unit direction u to the hexagon (apothem a);
            // also returns the inward wall normal of the exit flat.
            float HexExit(float2 d, float2 u, float a, out float2 wallN)
            {
                float best = 1e7;
                wallN = float2(0.0, 1.0);
                float2 ns[3] = { HEX_N0, HEX_N1, HEX_N2 };
                [unroll] for (int i = 0; i < 3; i++)
                {
                    float2 n = ns[i];
                    float un = dot(u, n);
                    float dn = dot(d, n);
                    if (un > 1e-5)  { float t = (a - dn) / un;  if (t < best) { best = t; wallN = -n; } }
                    if (un < -1e-5) { float t = (-a - dn) / un; if (t < best) { best = t; wallN = n; } }
                }
                return max(best, 0.0);
            }

            // Shading of a hole seen along the view ray v from the plane point with local offset d: the far wall at the
            // visible depth, lit and darkened with depth as the tier-0/1 tubes, then the tube air at that depth (R2-04).
            float3 HoleColor(float2 d, float3 v, Light sun, float3 albedo)
            {
                float2 vh = v.xz;
                float vhl = max(length(vh), 1e-4);
                float2 wallN2;
                float l = HexExit(d, vh / vhl, _LatticeParams.y, wallN2);
                float depth = l * saturate(-v.y) / vhl;
                float3 wallN = float3(wallN2.x, 0.0, wallN2.y);
                float3 lit = sun.color * saturate(dot(wallN, sun.direction)) + AbTrilight(wallN, (int)_AbDomain);
                float k = exp(-depth / max(_DepthFalloff, 1.0)) * (depth < _TubeDepth ? 1.0 : 0.0);
                float3 c = lerp(AB_EXT_FOG_DOWN, albedo * lit, k);
                return AbHoneyAir(c, depth, 0.0, float4(0.0, 1.0, _HoneyAir.zw));     // tube air only (aerial below)
            }

            // Gaussian weight (x ignite) of one lamp dot at lampXZ for the plane point p (true screen pixels via the inverse
            // screen Jacobian).
            float LampG(float2 lampXZ, float2 p, float2 dpx, float2 dpy, float det, float rTruePx)
            {
                float2 o = lampXZ - p;
                float a = (o.x * dpy.y - o.y * dpy.x) / det;
                float b = (dpx.x * o.y - dpx.y * o.x) / det;
                float rPx = length(float2(a, b));
                float sigma = max(0.5 * max(_MinPixels, 2.5), rTruePx) * 0.6;
                return AbLampIgnite(lampXZ) * exp(-0.5 * (rPx * rPx) / (sigma * sigma));
            }

            half4 LatticeFrag(AbVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                AbFadeClip(_AbFade, input.positionCS.xy);
                AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy);

                float size = _LatticeParams.x;
                float2 p = input.positionWS.xz;
                float2 dpx = ddx(p);
                float2 dpy = ddy(p);
                float2 rel = p - _LatticeOrigin.xy;
                int2 qr;
                float2 centre = AbHexCellCentre(rel, size, qr) + _LatticeOrigin.xy;
                float2 d = p - centre;
                float hd = HexDist(d);
                float fwHd = max(fwidth(hd), 1e-3);
                float det = dpx.x * dpy.y - dpx.y * dpy.x;
                det = abs(det) < 1e-8 ? 1e-8 : det;
                float pixelArea = abs(det);

                float dist;
                float3 v = AbViewRay(input.positionWS, dist);
                Light sun = GetMainLight();
                sun.color *= saturate(_KeyLightMask);                                  // main light (Key) only
                float3 ambUp = AbTrilight(float3(0.0, 1.0, 0.0), (int)_AbDomain);
                // per-cell +/-6 % tone, the same hash as the tier-0/1 tubes (R2-19, 14.10 c)
                float3 albedo = _BaseColor.rgb * AbHoneyCellTone(p, _LatticeOrigin.xy, size);

                // Webs: the half-band of a cell is 30 m (hd 350..380). Below 1.5 px total width the band is kept at
                // 1.5 px and its contrast scaled by 30 / width, so the mean tone is preserved, nothing aliases, and the
                // cell outline stays readable (with falling contrast, like fog) out to ~10 km (14.7.7 readability).
                float apo = _LatticeParams.y;
                float rimW = _LatticeParams.z;
                float pitchHalf = 380.0;
                float wTrue = pitchHalf - apo;
                float wEff = max(wTrue, 0.75 * fwHd);
                float contrast = wTrue / wEff;
                float edge = pitchHalf - wEff;
                float band = smoothstep(edge - 0.5 * fwHd, edge + 0.5 * fwHd, hd) * contrast;
                // lit rim on the inner edge of the web (6 m), its band fraction under 1.5 px
                float kRim = AbFeatureFade(rimW / fwHd);
                float rimIn = 1.0 - smoothstep(apo + rimW - 0.5 * fwHd, apo + rimW + 0.5 * fwHd, hd);
                float rimF = lerp(rimW / wTrue, rimIn, kRim);
                float3 webLit = albedo * (sun.color * saturate(sun.direction.y) + ambUp) * (1.0 + 0.35 * rimF);
                float3 webMeanLit = albedo * (sun.color * saturate(sun.direction.y) + ambUp) * (1.0 + 0.35 * rimW / wTrue);

                // holes; the whole-cell structure fades to the cell average when a cell is under 3-6 px
                float3 hole = HoleColor(d, v, sun, albedo);
                float3 holeMean = HoleColor(float2(0.0, 0.0), v, sun, albedo);
                float cellPx = 760.0 / fwHd;
                float kCell = AbFeatureFade(cellPx / 2.0);
                float webArea = 1.0 - (apo * apo) / (pitchHalf * pitchHalf);           // 0.152
                float3 cellMean = lerp(holeMean, webMeanLit, webArea);
                float3 color = lerp(cellMean, lerp(hole, webLit, band), kCell);
                // aerial veil of the 600 m of air under the balcony (R2-04), as on the tier-0/1 tubes
                color = AbHoneyAir(color, 0.0, dist, float4(_HoneyAir.xy, 1.0, 0.0));

                // amber lamp dots: this cell and the cells +/- b1 (z neighbours); hue-safe amber (R2-21, lead D4) with a
                // partially occluding core, fading to the cell mean with the cell
                float rTruePx = _LampRadius / sqrt(pixelArea);
                float2 lampOff = float2(0.0, -_LatticeParams.w);
                float2 b1 = float2(0.0, 760.0);
                float gl = LampG(centre + lampOff, p, dpx, dpy, det, rTruePx)
                         + LampG(centre + b1 + lampOff, p, dpx, dpy, det, rTruePx)
                         + LampG(centre - b1 + lampOff, p, dpx, dpy, det, rTruePx);
                float sigmaPx = max(0.5 * max(_MinPixels, 2.5), rTruePx) * 0.6;
                float cellAreaPx = 500214.0 / pixelArea;                               // (sqrt(3)/2) 760^2 m^2
                float glMean = AbLampIgnite(centre + lampOff) * saturate(2.0 * PI * sigmaPx * sigmaPx / max(cellAreaPx, 1e-3));
                float kDots = AbFeatureFade(cellPx / 2.0);
                float3 E = _EmissionColor.rgb * _EmissionStrength;
                // unresolved cells and far dots: the partly occluding additive dot, fogged with the cell (as the checkpoint:
                // the far field and the horizon backdrop keep their tone, 14.10 horizon agreement)
                float g = lerp(glMean, gl, kDots);
                float3 cOld = AbyssApplyFog(color * (1.0 - AB_LAMP_MEAN_OCCL * saturate(g)) + AbWarmEmission(E * g), input.positionWS, (int)_AbDomain);
                // resolved dots: amber discs over the fogged cell, the hue-safe lamp dimmed by half of the fog (the in-scatter
                // stays around the disc); they hand over to the fogged dot as the transmittance falls from 0.4 to 0.1 (8-12 km),
                // so the horizon backdrop stays haze; cold or dim lattice colours stay additive
                float T = AbyssFogAdditive(float3(1.0, 1.0, 1.0), input.positionWS, (int)_AbDomain).g;
                float discW = AbWarmth(_EmissionColor.rgb) * saturate(_EmissionStrength - 1.0) * smoothstep(0.1, 0.4, T);
                float a = saturate(AB_LAMP_DISC_GAIN * gl);
                float3 cDisc = AbyssApplyFog(color, input.positionWS, (int)_AbDomain) * (1.0 - a) + AbWarmEmission(E * lerp(1.0, T, 0.5)) * a;
                color = lerp(cOld, cDisc, discW * kDots);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbDepthOnlyFrag
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex AbVert
            #pragma fragment AbDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "Assets/_Game/Shaders/Include/AbyssPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
