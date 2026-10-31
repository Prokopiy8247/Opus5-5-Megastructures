// RingLitPass.hlsl - the four passes of every lit Ring* shader (ForwardLit, ShadowCaster, DepthOnly, DepthNormals)
// with one shared vertex path: object -> flat world -> (vertex hook) -> RingBend -> clip. See RingLitCore.hlsl.
// Based on URP 17.6 Lit (Lit.shader, LitForwardPass.hlsl, ShadowCasterPass.hlsl, DepthOnlyPass.hlsl,
// LitDepthNormalsPass.hlsl), see docs/research/01_urp_lit_shader_template.md.
#ifndef RING_LIT_PASS_INCLUDED
#define RING_LIT_PASS_INCLUDED

#if defined(RING_PASS_FORWARD)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "RingLighting.hlsl"
#elif defined(RING_PASS_SHADOW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
    float3 _LightDirection;
    float3 _LightPosition;
#endif

struct RingAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float4 color      : COLOR;
    float2 uv0        : TEXCOORD0;
    float2 uv1        : TEXCOORD1;
#if defined(RING_INDIRECT)
    uint instanceID   : SV_InstanceID;
#endif
};

struct RingVaryings
{
    float4 positionCS     : SV_POSITION;
    float3 positionWS     : TEXCOORD0;
    float3 positionFlatWS : TEXCOORD1;
    float3 normalWS       : TEXCOORD2;
    float3 normalFlatWS   : TEXCOORD3;
    float4 tangentWS      : TEXCOORD4;
    float4 color          : TEXCOORD5;
    float4 uv             : TEXCOORD6;
    float4 custom         : TEXCOORD7;
    float  theta          : TEXCOORD8;
};

RingVaryings RingVert(RingAttributes input)
{
    RingVaryings o = (RingVaryings)0;
    RingVertexData v;
    v.positionOS = input.positionOS.xyz;
#if defined(RING_INDIRECT)
    v.instanceID = input.instanceID;
    v.positionWS = input.positionOS.xyz;
    v.normalWS = input.normalOS;
    v.tangentWS = input.tangentOS;
#else
    v.instanceID = 0;
    v.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    v.normalWS = TransformObjectToWorldNormal(input.normalOS);
    v.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
#endif
    v.color = input.color;
    v.uv = float4(input.uv0, input.uv1);
    v.custom = 0;
#if defined(RING_HAS_VERTEX_HOOK)
    RingVertexHook(v);
#endif
    float3 nFlat = normalize(v.normalWS);
#if defined(RING_NO_BEND)
    float theta = 0.0;
    float3 posB = v.positionWS;
    float3 nB = nFlat;
    float3 tB = v.tangentWS.xyz;
#else
    float theta = RingBendTheta(v.positionWS);
    float3 posB = RingBendPosition(v.positionWS);
    float3 nB = RingBendRotate(nFlat, theta);
    float3 tB = RingBendRotate(v.tangentWS.xyz, theta);
#endif

#if defined(RING_PASS_SHADOW)
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirectionWS = normalize(_LightPosition - posB);
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif
    o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(posB, nB, lightDirectionWS)));
#else
    o.positionCS = TransformWorldToHClip(posB);
#endif
    o.positionWS = posB;
    o.positionFlatWS = v.positionWS;
    o.normalWS = nB;
    o.normalFlatWS = nFlat;
    o.tangentWS = float4(tB, v.tangentWS.w);
    o.color = v.color;
    o.uv = v.uv;
    o.custom = v.custom;
    o.theta = theta;
    return o;
}

RingFragData RingMakeFrag(RingVaryings i)
{
    RingFragData f;
    f.positionCS = i.positionCS;
    f.positionWS = i.positionWS;
    f.positionFlatWS = i.positionFlatWS;
    f.normalWS = normalize(i.normalWS);
    f.normalFlatWS = normalize(i.normalFlatWS);
    f.tangentWS = i.tangentWS;
    f.color = i.color;
    f.uv = i.uv;
    f.custom = i.custom;
    f.theta = i.theta;
    f.screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
    float3 v = _RingCam.xyz - i.positionWS;
    f.viewDist = length(v);
    f.viewDirWS = v / max(f.viewDist, 1e-4);
    f.footprint = max(f.viewDist * _RingCam.w, 1e-4);
    return f;
}

#if defined(RING_PASS_FORWARD)
half4 RingFrag(RingVaryings i) : SV_Target0
{
    RingFragData f = RingMakeFrag(i);
#if defined(RING_HAS_CLIP_HOOK)
    RingClipHook(f);
#endif
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
#if !defined(RING_NO_FOG)
    c = RingFog(c, f.positionWS, f.positionFlatWS.y);
#endif
    return half4(c, 1.0h);
}
#elif defined(RING_PASS_DEPTHNORMALS)
void RingFrag(RingVaryings i, out half4 outNormalWS : SV_Target0)
{
#if defined(RING_HAS_CLIP_HOOK)
    RingClipHook(RingMakeFrag(i));
#endif
    outNormalWS = half4(NormalizeNormalPerPixel(normalize(i.normalWS)), 0.0);
}
#elif defined(RING_PASS_DEPTH)
half RingFrag(RingVaryings i) : SV_Target0
{
#if defined(RING_HAS_CLIP_HOOK)
    RingClipHook(RingMakeFrag(i));
#endif
    return i.positionCS.z;
}
#else // shadow
half4 RingFrag(RingVaryings i) : SV_Target0
{
#if defined(RING_HAS_CLIP_HOOK)
    RingClipHook(RingMakeFrag(i));
#endif
    return 0;
}
#endif

#endif
