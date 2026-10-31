// RingLitCore.hlsl - included FIRST (in HLSLINCLUDE) by every lit Ring* shader, before the material's
// UnityPerMaterial CBUFFER and its hooks. Declares the data structs the hooks see.
//
// A material shader provides, in its HLSLINCLUDE block after including this file:
//   CBUFFER_START(UnityPerMaterial) ... CBUFFER_END          (identical in every pass: SRP Batcher)
//   void RingSurfaceHook(inout RingFragData f, inout SurfaceData s)   (always)
//   optional: #define RING_HAS_VERTEX_HOOK + void RingVertexHook(inout RingVertexData v)
//             (flat-space displacement BEFORE the bend; runs identically in all four passes)
//   optional: #define RING_HAS_CLIP_HOOK + void RingClipHook(RingFragData f)   (clip() in all passes)
//   optional: #define RING_INDIRECT  (RenderMeshIndirect draws: SV_InstanceID reaches the vertex hook, and the hook
//             must write a world position itself; unity_ObjectToWorld is not applied)
//   optional: #define RING_NO_BEND (geometry that must not roll onto the cylinder, e.g. outside-only content)
// Then each pass does: #define RING_PASS_FORWARD | RING_PASS_SHADOW | RING_PASS_DEPTH | RING_PASS_DEPTHNORMALS and
// #include "Include/RingLitPass.hlsl".
#ifndef RING_LIT_CORE_INCLUDED
#define RING_LIT_CORE_INCLUDED

#define _ENVIRONMENTREFLECTIONS_OFF 1
#define _SCREENSPACEREFLECTIONS_OFF 1

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "RingBend.hlsl"
#include "RingAtmosphere.hlsl"
#include "RingGround.hlsl"

struct RingVertexData
{
    float3 positionOS;
    float3 positionWS;   // flat world position (hook may displace it)
    float3 normalWS;     // flat world normal
    float4 tangentWS;    // flat world tangent (w = sign)
    float4 color;        // vertex colour (R = AO, G = edge wear by convention)
    float4 uv;           // xy = uv0, zw = uv1
    float4 custom;       // free, interpolated to the fragment
    uint instanceID;
};

struct RingFragData
{
    float4 positionCS;
    float3 positionWS;      // bent (render space)
    float3 positionFlatWS;  // unbent world position
    float3 normalWS;        // bent normal (hook may perturb it; keep it normalised)
    float3 normalFlatWS;    // unbent normal
    float4 tangentWS;       // bent tangent
    float4 color;
    float4 uv;
    float4 custom;
    float theta;            // bend angle
    float2 screenUV;
    float3 viewDirWS;
    float viewDist;
    float footprint;        // approximate pixel footprint (m) at the fragment
};

// Perturbs the bent normal with a tangent-space-free flat-space offset (dn in flat world space)
float3 RingPerturbNormal(RingFragData f, float3 dnFlat)
{
    float3 n = normalize(f.normalFlatWS + dnFlat);
    return normalize(RingBendRotate(n, f.theta));
}

#endif
