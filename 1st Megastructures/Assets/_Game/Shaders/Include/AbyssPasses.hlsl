#ifndef ABYSS_PASSES_INCLUDED
#define ABYSS_PASSES_INCLUDED

// =====================================================================================================================
// AbyssPasses.hlsl - vertex stage and depth-type passes shared by the opaque Megastructure shaders (Surface, Grating,
// Facade). One vertex function serves every pass, so ThinLine expansion, _AbFade and the distance fade are identical in
// ForwardLit, ShadowCaster, DepthOnly and DepthNormals (no silhouette or dither mismatch between colour and depth).
//
// The including shader's UnityPerMaterial CBUFFER must declare: float _AbFade; float _ThinLine; float4 _DistFade.
// Optional hook: #define ABYSS_DEPTH_ALPHA and implement  float AbyssDepthAlpha(AbVaryings input)  before including
// this file to clip depth-type passes by a coverage value (dithered with the static fade noise), e.g. the grating.
// Requires Core.hlsl and AbyssCommon.hlsl.
// =====================================================================================================================

struct AbAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 color      : COLOR;       // R = AO, G = edge wear, B = generator flag (facade glass), A = free
    float2 uv0        : TEXCOORD0;   // metres where a recipe needs them (ConcreteCyl arc length, floor lateral offset)
    float4 uv1        : TEXCOORD1;   // ThinLine: xyz axis direction, w radius (object space)
    float4 uv2        : TEXCOORD2;   // ThinLine (optional): xyz point on the axis (object space), w = 1
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct AbVaryings
{
    float4 positionCS                    : SV_POSITION;
    float3 positionWS                    : TEXCOORD0;
    float3 normalWS                      : TEXCOORD1;
    float3 patternLocal                  : TEXCOORD2;  // rotated object-space position (small values, precise ddx/ddy)
    nointerpolation float3 patternOrigin : TEXCOORD3;  // object origin mod 1332 m
    float4 color                         : TEXCOORD4;  // vertex colour
    float4 uv                            : TEXCOORD5;  // xy = uv0, z = ThinLine coverage, w = 0
    nointerpolation float4 objectAxisX   : TEXCOORD6;  // xyz = object +X axis in world (unit), w = object scale
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#if defined(ABYSS_DEPTH_ALPHA)
float AbyssDepthAlpha(AbVaryings input);   // implemented by the including shader (after this include)
#endif

#if defined(AB_SHADOW_CASTER_PASS)
float3 _LightDirection;   // set by ShadowUtils.SetupShadowCasterConstantBuffer
float3 _LightPosition;
#endif

// Shared vertex function: world position (with ThinLine expansion), pattern coordinates, clip position for the pass.
AbVaryings AbVert(AbAttributes input)
{
    AbVaryings output = (AbVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float coverage;
    positionWS = AbThinLineExpand(positionWS, normalWS, input.uv1, input.uv2, _ThinLine, coverage);

    output.positionWS = positionWS;
    output.normalWS = normalWS;
    output.patternLocal = AbRotatedLocal(input.positionOS.xyz);   // the pattern ignores the ThinLine push
    output.patternOrigin = AbObjectOriginWrapped();
    output.color = input.color;
    output.uv = float4(input.uv0, coverage, 0.0);
    float3 ax = mul((float3x3)UNITY_MATRIX_M, float3(1.0, 0.0, 0.0));
    float axLen = max(length(ax), 1e-6);
    output.objectAxisX = float4(ax / axLen, axLen);

#if defined(AB_SHADOW_CASTER_PASS)
  #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDir = AbSafeNormalize(_LightPosition - positionWS, normalWS);
  #else
    float3 lightDir = _LightDirection;
  #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir)));
#else
    output.positionCS = TransformWorldToHClip(positionWS);
#endif
    return output;
}

// Tier fades (and the optional coverage hook) for depth-type passes, identical to the colour pass.
void AbDepthClip(AbVaryings input)
{
    AbFadeClip(_AbFade, input.positionCS.xy);
    AbDistFadeClip(_DistFade, input.positionWS, input.positionCS.xy);
#if defined(ABYSS_DEPTH_ALPHA)
    float a = AbyssDepthAlpha(input);
    clip(a - AbFadeNoise(input.positionCS.xy));
#endif
}

half4 AbShadowFrag(AbVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    AbDepthClip(input);
    return 0;
}

half4 AbDepthOnlyFrag(AbVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    AbDepthClip(input);
    return half4(input.positionCS.z, 0.0, 0.0, 0.0);
}

half4 AbDepthNormalsFrag(AbVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    AbDepthClip(input);
    float3 n = AbSafeNormalize(input.normalWS, float3(0.0, 1.0, 0.0));   // NaN guard (degenerate interpolated normal)
    n = IS_FRONT_VFACE(face, n, -n);
#if defined(_GBUFFER_NORMALS_OCT)
    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0.0);
#else
    return half4(n, 0.0);
#endif
}

#endif // ABYSS_PASSES_INCLUDED
