// RingFinalDither - the ONE display-referred dither of the frame (Part 8.2, 14.7 H5, 14.12 "Dither").
// Material of a FullScreenPassRendererFeature (URP 17.6, Render Graph): injection point AfterRenderingPostProcessing,
// fetchColorBuffer = true, requirements = None, passIndex 0. The URP camera "Dithering" flag must be OFF.
//
// Verified in the URP 17.6 / Core 17.6 package source (docs/research/02 section 3):
// - FullScreenPassRendererFeature.cs:165-198 (RecordRenderGraph): with fetchColorBuffer the camera colour is first
//   copied by AddBlitPass into "_CameraColorFullScreenPass" (GetTextureDesc(cameraColor) = RGBA16F with 64-bit HDR
//   precision), then (:200-256) a raster pass draws a full-screen triangle (DrawProcedural, 3 vertices) INTO
//   resourcesData.activeColorTexture, i.e. the camera colour; requiresIntermediateTexture = fetchColorBuffer (:125).
//   ExecuteMainPass (:153-163) clears the MPB and sets only _BlitTexture (the copy) and _BlitScaleBias.
// - UniversalRendererRenderGraph.cs:1404 (hasPassesAfterPostProcessing), :1435 (isTargetBackbuffer requires
//   !hasPassesAfterPostProcessing), :1454 (doSRGBEncoding = isTargetBackbuffer && needsColorEncoding): with this pass
//   at AfterRenderingPostProcessing UberPost writes an intermediate of the camera-colour format and does not
//   sRGB-encode, so the colour read here is LINEAR, tonemapped (Neutral, via the LUT) and display-referred in [0, 1].
// - FinalBlitPass then copies it to the backbuffer with no dither and no grade; on DX11/DX12 the sRGB backbuffer view
//   encodes linear -> sRGB in hardware and quantises to 8 bits. Captures into an RGBA16F target receive the same
//   linear values; their own exact sRGB encode reproduces the dithered code values.
//
// Formula (14.7 H5), static (no frame index, survives video compression):
//   c = LinearToSRGB(col) + (IGN(px) + IGN(px + (5.588, 3.2)) - 1) * (1.5 / 255) * lerp(1, 0.5, saturate((luma - 0.5) * 2))
//   col = SRGBToLinear(c)
// The noise term is monochrome (one value for R, G and B, so it lands in luma and survives 4:2:0).
// luma = Rec.709 luma Y' of the display-encoded colour (14.1: "Luma = Rec.709 on the 8-bit capture").
// NOTE (measured over a 1920 x 1080 grid of pixel centres, see the BF report): IGN is linear inside its frac, so
// IGN(px + (5.588, 3.2)) = frac(IGN(px) + ~0.87) almost everywhere (correlation 0.30). The sum of the two samples of
// the exact formula is therefore close to UNIFORM on [-0.877, 0.877] (variance 0.217) rather than triangular
// (1/6): at 1.5 LSB it is a ~+-1.3 LSB near-uniform dither. _Decorrelate = 1 swaps the coordinates of the second
// sample (IGN(px.yx + (5.588, 3.2)): correlation 1e-4, variance 0.1666 = exact TPDF). Default 0 = the 14.7 H5
// formula verbatim: MASTER_PROMPT's order of authority lets Part 14 override the general rules for "its own ...
// dither variant", so H5 wins over the 8.2 "triangular distribution" wording until the lead records otherwise. The
// encode test (11.12, BandScore on the 8-bit capture and the x264 CRF 23 re-encode) with _Decorrelate 0 and 1
// decides; if 1 is equal or better, set it in RenderingMaterialDefs (lead decision, DEVELOPMENT.md).
// Additions (all neutral at their defaults inside the palette range):
// - _BlackGuard: the amplitude fades to 0 as the brightest encoded channel falls from 1.5 LSB to 0, so true black
//   (load fade, end card) stays exactly 0 instead of a static 0/1-code speckle (research 07 section 4: clipping at
//   black biases the mean). The darkest allowed scene colour (SpaceBlack #02030A, max channel 10 LSB) is unaffected.
//   The same switch guards white per channel: a channel's amplitude fades to 0 over the last amp below 1.0 (the noise
//   can never clip at the top), so clipped whites (sun disc, the EV +2 mouth) stay code 255 instead of a static
//   254 speckle and a clipped channel gets no downward bias, while the other channels of a saturated colour (a
//   sunset gradient with R at 1) keep their full dither (research 07 section 4: fade near 0 or 1).
// - global _RingDitherOff (default 0 = dither on): A/B switch for the encode test (11.12) without touching the asset.
Shader "Megastructure/RingFinalDither"
{
    Properties
    {
        _Amplitude ("Dither amplitude (8-bit sRGB LSB)", Float) = 1.5
        _BlackGuard ("Keep true black and clipped white flat (0..1)", Range(0, 1)) = 1
        _Decorrelate ("Decorrelated second IGN sample (0 = exact 14.7 H5 formula)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "RingFinalDither"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            // URP Core first (globals, XR macros), then the core blit helpers (_BlitTexture, Vert, sampler_PointClamp),
            // the same order as URP/Samples~/URPRenderGraphSamples/BlitWithMaterial/BlitWithMaterial.shader.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Include/RingCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Amplitude;
                float _BlackGuard;
                float _Decorrelate;
            CBUFFER_END

            float _RingDitherOff;   // global, not a material property (0 when never set = dither on)

            // Exact IEC 61966-2-1 transfer functions in full float precision (the hardware sRGB encoder uses the same
            // constants). Branch-free per component (no vector ternary, valid for FXC and DXC).
            float3 RingLinearToSRGB(float3 c)
            {
                c = max(c, 0.0);
                float3 lo = c * 12.92;
                float3 hi = 1.055 * pow(max(c, 0.0031308), 1.0 / 2.4) - 0.055;
                return lerp(hi, lo, step(c, 0.0031308));
            }

            float3 RingSRGBToLinear(float3 c)
            {
                c = max(c, 0.0);
                float3 lo = c * (1.0 / 12.92);
                float3 hi = pow(max((c + 0.055) * (1.0 / 1.055), 0.0), 2.4);
                return lerp(hi, lo, step(c, 0.04045));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // point sample at the pixel (source and destination have the same size, scale-bias handles a flip)
                float4 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);

                float3 s = RingLinearToSRGB(col.rgb);
                float2 px = input.positionCS.xy;                                     // pixel centres (.5), static
                float2 px2 = lerp(px, px.yx, step(0.5, _Decorrelate));               // 0: exact spec formula
                float n = RingIGN(px) + RingIGN(px2 + float2(5.588, 3.2)) - 1.0;     // [-1, 1]
                float luma = dot(saturate(s), float3(0.2126, 0.7152, 0.0722));       // Y' (display-encoded)
                float amp = max(_Amplitude, 0.0) * (1.0 / 255.0) * lerp(1.0, 0.5, saturate((luma - 0.5) * 2.0));
                float guard = saturate(_BlackGuard);
                float peak = max(s.r, max(s.g, s.b));
                amp *= lerp(1.0, saturate(peak * (255.0 / 1.5)), guard);
                amp *= 1.0 - saturate(_RingDitherOff);
                // white guard per channel: |n| <= 1, so s + n * ampC never exceeds 1.0 (amp = 0 gives ampC = 0)
                float3 ampC = amp * lerp(float3(1.0, 1.0, 1.0), saturate((1.0 - s) / max(amp, 1e-6)), guard);

                s = max(s + n * ampC, 0.0);
                return float4(max(RingSRGBToLinear(s), 0.0), col.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
