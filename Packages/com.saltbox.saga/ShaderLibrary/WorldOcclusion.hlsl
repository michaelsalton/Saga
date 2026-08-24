#ifndef SAGA_WORLD_OCCLUSION_INCLUDED
#define SAGA_WORLD_OCCLUSION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Dither.hlsl"

float4 _OccCenter;
float4 _OccAxis;

float _OccRadius;
float _OccFeather;
float _OccDepthBias;
float _OccDepthFeather;
float _OccGroundY;
float _OccRise;
float _OccRiseFeather;
float _OccCoreCoverage;
float _OccHoleOnset;
float _OccOutlineSuppress;

void SagaOcclusionSplit(float3 positionWS, out float along, out float r)
{
    float3 d = positionWS - _OccCenter.xyz;
    along = dot(d, _OccAxis.xyz);
    r     = length(d - _OccAxis.xyz * along);
}

half SagaOcclusionDisc(float3 positionWS)
{
    [branch] if (_OccRadius < 0.001)
        return 0.0h;

    float along, r;
    SagaOcclusionSplit(positionWS, along, r);

    return (half)(1.0 - smoothstep(_OccRadius, _OccRadius + max(_OccFeather, 1e-4), r));
}

half SagaOcclusionMask(float3 positionWS)
{
    [branch] if (_OccRadius < 0.001)
        return 0.0h;

    float along, r;
    SagaOcclusionSplit(positionWS, along, r);

    half radial = (half)(1.0 - smoothstep(_OccRadius, _OccRadius + max(_OccFeather, 1e-4), r));
    [branch] if (radial <= 0.0h)
        return 0.0h;

    float front = saturate((-along - _OccDepthBias) * rcp(max(_OccDepthFeather, 1e-4)));
    float rise = saturate((positionWS.y - _OccGroundY - _OccRise) * rcp(max(_OccRiseFeather, 1e-4)));

    return saturate(radial * (half)front * (half)rise);
}

void SagaOcclusionClip(half mask, float2 positionSS)
{
    [branch] if (_OccRadius < 0.001)
        return;

    half open     = smoothstep((half)_OccHoleOnset, 1.0h, mask);
    half coverage = lerp(1.0h, (half)_OccCoreCoverage, open);

    clip(coverage - SagaBayer4x4(positionSS));
}

#endif
