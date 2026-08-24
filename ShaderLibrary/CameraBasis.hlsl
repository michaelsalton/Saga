#ifndef SAGA_CAMERA_BASIS_INCLUDED
#define SAGA_CAMERA_BASIS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float4 _SagaCamRight; // .xyz = camera right,   .w = ortho HALF-width  in world units
float4 _SagaCamUp;    // .xyz = camera up,      .w = ortho HALF-height in world units
float4 _SagaCamFwd;   // .xyz = camera forward, .w unused

void SagaCameraRay(float2 uv, out float3 origin, out float3 dir)
{
    float2 ndc = uv * 2.0 - 1.0;
    origin = _WorldSpaceCameraPos
           + _SagaCamRight.xyz * (ndc.x * _SagaCamRight.w)
           + _SagaCamUp.xyz    * (ndc.y * _SagaCamUp.w);
    dir = _SagaCamFwd.xyz;
}

float3 SagaCameraWorldPos(float2 uv, float eyeDepth)
{
    float3 o, d;
    SagaCameraRay(uv, o, d);
    return o + d * eyeDepth;
}

#endif // SAGA_CAMERA_BASIS_INCLUDED
