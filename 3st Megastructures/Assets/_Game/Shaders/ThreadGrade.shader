// ThreadGrade.shader - Thread's own bloom, grade and final dither (MASTER_PROMPT 8.2, 14.2.5, 14.7.1).
// Drawn by Megastructure.Rendering.ThreadGradeFeature (Render Graph, AfterRenderingPostProcessing, URP
// post-processing OFF on the camera, research R1 option B / B1). Full-screen triangles via Core Blit.hlsl.
//
// Passes (the C# side finds them by name):
//   ThreadBloomPrefilter  camera colour (full res, RGBA16F, MSAA resolved) -> half res: 13-tap downsample with
//                         a partial Karis average (firefly-safe), exposure, HDR clamp, URP soft-knee threshold
//   ThreadBloomDown       13-tap downsample (Jimenez 2014, "Next Generation Post Processing in Call of Duty: AW")
//   ThreadBloomUp         3x3 tent of the coarser level lerped with this level by scatter (URP-style scatter)
//   ThreadGrade           exposure + bloom, saturation, Neutral highlight curve, display-space contrast,
//                         vignette, fade, sRGB encode + TPDF blue-noise dither, written to an R8G8B8A8_SRGB target
// Every intermediate stays in floating point (14.7.1 #5); the only quantisation is the final 8-bit write,
// and the dither is the last operation before it (8.2: exactly one display-referred dither).
// The CPU mirror of every curve is ThreadGradeMath.cs (EditMode tests: GradeTests.cs).
// Unity fog is not used here (full-screen pass). Numeric safety (Part 8.6): NaN -> 0 and an HDR clamp on every
// scene fetch, guarded divisions, saturated sqrt / pow inputs.
Shader "Megastructure/ThreadGrade"
{
    HLSLINCLUDE
    // Include order as URP's own Bloom.shader.
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Generated/ThreadConstantsGen.hlsl"
    // Blit.hlsl provides: TEXTURE2D_X(_BlitTexture), float4 _BlitScaleBias, Attributes, Varyings
    // { positionCS : SV_POSITION, texcoord : TEXCOORD0 }, Varyings Vert(Attributes) (full-screen triangle,
    // texcoord = uv * _BlitScaleBias.xy + _BlitScaleBias.zw), sampler_LinearClamp / sampler_PointClamp.

    TEXTURE2D_X(_ThGradeLow);     // up: the coarser bloom level | grade: the bloom result
    TEXTURE2D(_ThGradeNoise);     // 64 x 64 blue-noise rank map, (rank + 0.5) / 4096, read with Load

    CBUFFER_START(UnityPerMaterial)
        float4 _ThGradeSrcSize;   // _BlitTexture: w, h, 1/w, 1/h
        float4 _ThGradeLowSize;   // _ThGradeLow:  w, h, 1/w, 1/h
        float4 _ThGradeP0;        // prefilter: threshold, knee, clamp, exposure gain | up: scatter | grade: exposure gain, bloom intensity, saturation factor, contrast k
        float4 _ThGradeP1;        // grade: vignette intensity, aspect (w / h), fade multiplier (1 - FadeBlack), dither amplitude (LSB)
        float4 _ThGradeP2;        // grade: dither tile offset x, y (integer texels, R2 sequence), Neutral start, Neutral desaturation
    CBUFFER_END

    static const float3 kThGradeLuma = float3(0.2126729, 0.7151522, 0.0721750); // pal:ok (Rec.709 luminance weights, linear)
    static const float kThGradeHdrMax = 65000.0;          // below the half-float maximum 65504
    static const float kThGradeVigRef = 1.0 + (16.0 / 9.0) * (16.0 / 9.0);   // squared half-diagonal of a 16:9 frame (half-height units)
    static const float kThGradeVigShape = 1.5;            // ThreadGradeMath.VignetteShape
    static const uint kThGradeTileMask = (uint)TC_DitherTile - 1u;            // 64 x 64 tile (14.12)

    // ------------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------------
    float3 ThGradeSanitize(float3 c)
    {
        c = AnyIsNaN(c) ? (0.0).xxx : c;
        return clamp(c, 0.0, kThGradeHdrMax);   // +Inf -> max, -Inf and negatives -> 0
    }

    float3 ThGradeFetch(float2 uv)
    {
        return ThGradeSanitize(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb);
    }

    float ThGradeLuma(float3 c) { return dot(c, kThGradeLuma); }
    float ThGradeKarisW(float3 c) { return 1.0 / (1.0 + ThGradeLuma(c)); }

    // Exact piecewise sRGB (IEC 61966-2-1), as the GPU's sRGB encode; ThreadGradeMath mirrors it.
    float3 ThGradeLinearToSrgb(float3 c)
    {
        c = saturate(c);
        float3 lo = c * 12.92;
        float3 hi = 1.055 * pow(max(c, 1e-10), 1.0 / 2.4) - 0.055;
        return (c <= 0.0031308) ? lo : hi;
    }
    float3 ThGradeSrgbToLinear(float3 s)
    {
        s = saturate(s);
        float3 lo = s / 12.92;
        float3 hi = pow((s + 0.055) / 1.055, 2.4);
        return (s <= 0.04045) ? lo : hi;
    }

    // 13-tap 2x downsample (Jimenez 2014). t = SOURCE texel size; the taps cover 6 x 6 source texels.
    // karis: partial Karis average of the five 2x2 groups (first downsample only; suppresses fireflies from
    // the hair-thin bright cable, the sun disc and window dots, so the bloom never flickers).
    float3 ThGradeDown13(float2 uv, float2 t, bool karis)
    {
        float3 a = ThGradeFetch(uv + t * float2(-2.0,  2.0));
        float3 b = ThGradeFetch(uv + t * float2( 0.0,  2.0));
        float3 c = ThGradeFetch(uv + t * float2( 2.0,  2.0));
        float3 d = ThGradeFetch(uv + t * float2(-2.0,  0.0));
        float3 e = ThGradeFetch(uv);
        float3 f = ThGradeFetch(uv + t * float2( 2.0,  0.0));
        float3 g = ThGradeFetch(uv + t * float2(-2.0, -2.0));
        float3 h = ThGradeFetch(uv + t * float2( 0.0, -2.0));
        float3 i = ThGradeFetch(uv + t * float2( 2.0, -2.0));
        float3 j = ThGradeFetch(uv + t * float2(-1.0,  1.0));
        float3 k = ThGradeFetch(uv + t * float2( 1.0,  1.0));
        float3 l = ThGradeFetch(uv + t * float2(-1.0, -1.0));
        float3 m = ThGradeFetch(uv + t * float2( 1.0, -1.0));
        if (!karis)
            return e * 0.125 + (a + c + g + i) * 0.03125 + (b + d + f + h) * 0.0625 + (j + k + l + m) * 0.125;
        float3 b0 = (j + k + l + m) * 0.25;
        float3 b1 = (a + b + d + e) * 0.25;
        float3 b2 = (b + c + e + f) * 0.25;
        float3 b3 = (d + e + g + h) * 0.25;
        float3 b4 = (e + f + h + i) * 0.25;
        float w0 = 0.5 * ThGradeKarisW(b0);
        float w1 = 0.125 * ThGradeKarisW(b1);
        float w2 = 0.125 * ThGradeKarisW(b2);
        float w3 = 0.125 * ThGradeKarisW(b3);
        float w4 = 0.125 * ThGradeKarisW(b4);
        return (b0 * w0 + b1 * w1 + b2 * w2 + b3 * w3 + b4 * w4) / max(w0 + w1 + w2 + w3 + w4, 1e-6);
    }

    float3 ThGradeLowTap(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X_LOD(_ThGradeLow, sampler_LinearClamp, uv, 0).rgb;
    }

    // Khronos PBR Neutral highlight compression WITHOUT the 0.04 toe offset (ThreadGradeMath decision B03-1):
    // identity below `start` (0.76, ThreadGradeMath.NeutralStart, passed in _ThGradeP2.z so C# is the single
    // source), then one common scale (hue and saturation kept) and a mix toward white of speed `desat`.
    float3 ThGradeNeutral(float3 c, float start, float desat)
    {
        c = max(c, 0.0);
        start = clamp(start, 0.01, 0.99);
        float peak = max(c.r, max(c.g, c.b));
        if (peak < start) return c;
        float d = 1.0 - start;
        float newPeak = 1.0 - d * d / (peak + d - start);   // denominator >= d > 0
        c *= newPeak / peak;                                 // peak >= start > 0
        float g = 1.0 - 1.0 / (max(desat, 0.0) * (peak - newPeak) + 1.0);
        return lerp(c, newPeak.xxx, g);
    }

    // Display-space contrast (ThreadGradeMath decision B03-2): odd S-curve around 0.5, ends fixed, monotonic.
    float3 ThGradeContrast(float3 s, float k)
    {
        float3 q = s * (1.0 - s);
        return s + k * (s - 0.5) * 16.0 * q * q;
    }

    // Subtle radial vignette, circular in pixels, radius in half-height units (Hor+); 1 - intensity at a 16:9 corner.
    float ThGradeVignette(float2 uv, float aspect, float intensity)
    {
        float2 p = (uv - 0.5) * 2.0;
        p.x *= aspect;
        float r2 = dot(p, p) / kThGradeVigRef;
        float shape = (1.0 - exp(-kThGradeVigShape * r2)) / (1.0 - exp(-kThGradeVigShape));
        return 1.0 - intensity * shape;
    }

    // Blue-noise TPDF on [-1, 1]: tile value (uniform) -> monotonic triangular remap (URP ApplyDithering form),
    // tile offset each frame by the integer R2 offset (static with -freeze-dither). One value for R, G and B
    // (monochrome: luma is what survives 4:2:0 video, research R6 0.4).
    float ThGradeTpdf(float2 positionSS)
    {
        uint2 p = (uint2(positionSS) + uint2(_ThGradeP2.xy)) & kThGradeTileMask;
        float u = LOAD_TEXTURE2D(_ThGradeNoise, int2(p)).r;
        float t = u * 2.0 - 1.0;
        return FastSign(t) * (1.0 - sqrt(saturate(1.0 - abs(t))));
    }

    // ------------------------------------------------------------------------------------------------
    // Fragment programs
    // ------------------------------------------------------------------------------------------------
    float4 FragPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float3 c = ThGradeDown13(input.texcoord, _ThGradeSrcSize.zw, true);
        c = min(c * _ThGradeP0.w, _ThGradeP0.z);              // exposure (threshold acts on exposed values), clamp
        // URP soft-knee threshold (URP Bloom.shader FragPrefilter)
        float br = max(c.r, max(c.g, c.b));
        float knee = max(_ThGradeP0.y, 1e-4);
        float soft = clamp(br - _ThGradeP0.x + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee);
        c *= max(br - _ThGradeP0.x, soft) / max(br, 1e-4);
        return float4(max(c, 0.0), 1.0); // pal:ok
    }

    float4 FragDown(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(ThGradeDown13(input.texcoord, _ThGradeSrcSize.zw, false), 1.0); // pal:ok
    }

    float4 FragUp(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 t = _ThGradeLowSize.zw;
        float3 lo = ThGradeLowTap(uv) * 4.0
                  + (ThGradeLowTap(uv + float2(-t.x, 0.0)) + ThGradeLowTap(uv + float2(t.x, 0.0))
                   + ThGradeLowTap(uv + float2(0.0, -t.y)) + ThGradeLowTap(uv + float2(0.0, t.y))) * 2.0
                  + (ThGradeLowTap(uv + float2(-t.x, -t.y)) + ThGradeLowTap(uv + float2(t.x, -t.y))
                   + ThGradeLowTap(uv + float2(-t.x, t.y)) + ThGradeLowTap(uv + float2(t.x, t.y)));
        lo *= 1.0 / 16.0;   // 3x3 tent
        float3 hi = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
        return float4(lerp(hi, lo, _ThGradeP0.x), 1.0); // pal:ok
    }

    float4 FragGrade(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        // unit screen uv (scale and any y-flip removed) for the bloom texture and the vignette
        float2 sbScale = float2(abs(_BlitScaleBias.x) > 1e-6 ? _BlitScaleBias.x : 1.0, abs(_BlitScaleBias.y) > 1e-6 ? _BlitScaleBias.y : 1.0);
        float2 uvUnit = (uv - _BlitScaleBias.zw) / sbScale;

        // 1. exposure + bloom (the bloom was thresholded on exposed values, so it is added after exposure)
        float3 c = ThGradeSanitize(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb);
        float3 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_ThGradeLow, sampler_LinearClamp), uvUnit,
                                              _ThGradeLowSize, (1.0).xx, 0u).rgb;   // bicubic: no blocky half-res diamonds
        c = c * _ThGradeP0.x + max(bloom, 0.0) * _ThGradeP0.y;

        // 2. saturation in linear HDR around luminance (URP convention: factor 1 + Saturation / 100)
        float l = ThGradeLuma(c);
        c = max(l + (c - l) * _ThGradeP0.z, 0.0);

        // 3. tone curve (identity below 0.76: palette colours display as authored)
        c = ThGradeNeutral(c, _ThGradeP2.z, _ThGradeP2.w);

        // 4. contrast in display space (fixes 0, 0.5 and 1)
        c = ThGradeSrgbToLinear(ThGradeContrast(ThGradeLinearToSrgb(c), _ThGradeP0.w));

        // 5. vignette and the title / ending fade, in float, before the dither
        c *= ThGradeVignette(uvUnit, _ThGradeP1.y, _ThGradeP1.x) * _ThGradeP1.z;

        // 6. sRGB code space + TPDF blue-noise dither of +-amplitude LSB, faded inside the last half LSB at both
        //    ends so pure black (fades) and pure white stay exact (research R6 2.2). The target is R8G8B8A8_SRGB:
        //    we return the LINEAR value of the dithered code, the GPU re-encodes it and rounds (one quantiser).
        float3 s = ThGradeLinearToSrgb(c);
        float n = ThGradeTpdf(input.positionCS.xy) * (_ThGradeP1.w / 255.0);
        float3 edge = saturate(min(s, 1.0 - s) * 510.0);
        s = saturate(s + n * edge);
        return float4(ThGradeSrgbToLinear(s), 1.0); // pal:ok
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ThreadBloomPrefilter"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "ThreadBloomDown"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma vertex Vert
            #pragma fragment FragDown
            ENDHLSL
        }

        Pass
        {
            Name "ThreadBloomUp"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma vertex Vert
            #pragma fragment FragUp
            ENDHLSL
        }

        Pass
        {
            Name "ThreadGrade"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma editor_sync_compilation
            #pragma vertex Vert
            #pragma fragment FragGrade
            ENDHLSL
        }
    }
    Fallback Off
}
