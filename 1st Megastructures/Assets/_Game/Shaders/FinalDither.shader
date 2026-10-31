// =====================================================================================================================
// Megastructure/FinalDither - the ONE display-referred dither of the pipeline (MASTER_PROMPT 1.4, 8.2), plus the picture
// fade. Used by URP's FullScreenPassRendererFeature (injection AfterRenderingPostProcessing, fetchColorBuffer on,
// pass 0) with camera dithering OFF and no FXAA (so nothing after this pass dithers or quantises except the final 8-bit
// write). Input _BlitTexture: the post-processed (tonemapped) LINEAR [0, 1] colour of the camera target.
//   s = LinearToSRGB(saturate(c));
//   s += TPDF(frac(IGN(p) + AB_DITHER_ROW_SHIFT p.y)) * _AbDitherParams.x / 255, p = pixel [+ frame offset]
//                                                                          (when _AbDitherParams.w = 1)
//   s *= _AbPictureFade                                                    (0 = black: no dither shows on black frames)
//   out = SRGBToLinear(max(s, 0))   (the sRGB back buffer / target re-encodes and quantises)
// _AbDitherParams = (amplitude LSB, temporal 0/1, frame index mod 64, enabled 0/1); the static/temporal choice is the
// encode test's (11.12). TPDF: one uniform value reshaped to a triangular distribution on [-1, 1] (as URP's
// ApplyDithering), i.e. peak +/- amplitude LSB. _AbPictureFade is a global the bootstrap/FadeController always sets.
// Requires a 64-bit (R16G16B16A16) HDR colour target: a B10G11R11 intermediate would quantise the dither away.
//
// Row decorrelation (g01 KS7d, BandScore): plain IGN advances by 52.9829189 x 0.00583715 = 0.3092692 per pixel row, and
// 13 x 0.3092692 = 4.02, so every vertical line of IGN nearly repeats after 13 rows. On a slow vertical gradient (about
// 1 level per 20-30 px) that near-period locks in and leaves runs of 13-15 identical 8-bit values (BandScore limit 12 at
// 1080p) although the dither is present. The uniform value is therefore IGN plus a per-row phase AB_DITHER_ROW_SHIFT,
// which moves the vertical step to 0.19: any 6 consecutive rows leave no gap wider than 0.19 on the unit circle, and the
// set of uniform values that changes the rounding is one arc of at least 0.25 (both TPDF tails at a level centre), so
// it is hit at least once per 6 rows. Worst run on any gradient 6 px instead of 15 (CPU mirror in
// ShaderTests.FinalDitherBreaksVerticalRuns); horizontal runs and the 2x2 / 4x4 blue-noise energy are unchanged or
// better. Same amplitude, same TPDF, same temporal frame offset.
// =====================================================================================================================
Shader "Megastructure/FinalDither"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FinalDither"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FinalDitherFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Assets/_Game/Shaders/Include/AbyssCommon.hlsl"

            // Per-row phase added to IGN: vertical step 0.3092692 - 0.1192692 = 0.19 (see the header).
            #define AB_DITHER_ROW_SHIFT -0.1192692

            // Triangular (TPDF) noise in [-1, 1] from a uniform value in [0, 1).
            float AbTriangular(float u)
            {
                float n = u * 2.0 - 1.0;
                return sign(n) * (1.0 - sqrt(saturate(1.0 - abs(n))));
            }

            // Uniform dither value of an integer pixel: IGN decorrelated per row.
            float AbDitherUniform(float2 pixel)
            {
                return frac(AbIGN(pixel) + AB_DITHER_ROW_SHIFT * pixel.y);
            }

            half4 FinalDitherFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).rgb;
                float3 s = LinearToSRGB(saturate(c));
                if (_AbDitherParams.w > 0.5)
                {
                    float2 pixel = floor(input.positionCS.xy);
                    if (_AbDitherParams.y > 0.5) pixel += 5.588238 * fmod(_AbDitherParams.z, 64.0);
                    float n = AbTriangular(AbDitherUniform(pixel));
                    s += n * (_AbDitherParams.x / 255.0);
                }
                s *= _AbPictureFade;
                return half4(SRGBToLinear(max(s, 0.0)), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
