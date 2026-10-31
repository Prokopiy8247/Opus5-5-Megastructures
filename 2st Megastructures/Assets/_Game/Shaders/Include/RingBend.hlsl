// RingBend.hlsl - Part 14.7 H1. Rolls flat world space onto the ring cylinder (axis parallel to X, radius R,
// centre line at y = R) relative to the camera z. Called in the vertex stage of EVERY pass (forward, shadow
// caster, depth only, depth normals) of every custom shader, after object-to-world and before world-to-clip,
// so the passes match bit for bit. Twin: Scripts/Runtime/Core/RingBend.cs.
#ifndef RING_BEND_INCLUDED
#define RING_BEND_INCLUDED

#include "RingCommon.hlsl"

// Bend angle for a flat world position (rad).
float RingBendTheta(float3 positionWS)
{
    return (positionWS.z - _RingCam.z) * (1.0 / RING_R);
}

// Stable form: B.y = h cos t + 2 R sin^2(t / 2); B.z = C.z + (R - h) sin t.
float3 RingBendPosition(float3 positionWS)
{
    float t = RingBendTheta(positionWS);
    float h = clamp(positionWS.y, -50.0, 0.9 * RING_R);
    float st, ct;
    sincos(t, st, ct);
    float sh = sin(0.5 * t);
    return float3(positionWS.x, h * ct + 2.0 * RING_R * sh * sh, _RingCam.z + (RING_R - h) * st);
}

// Rotation about +X by the bend angle (normals, tangents, the key vector).
float3 RingBendRotate(float3 n, float t)
{
    float s, c;
    sincos(t, s, c);
    return float3(n.x, n.y * c + n.z * s, -n.y * s + n.z * c);
}

// Convenience: bends position and normal together. Returns the bent position; outputs the bend angle.
float3 RingBendPN(float3 positionWS, inout float3 normalWS, out float theta)
{
    theta = RingBendTheta(positionWS);
    normalWS = RingBendRotate(normalWS, theta);
    return RingBendPosition(positionWS);
}

#endif
