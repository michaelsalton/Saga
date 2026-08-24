#ifndef SAGA_GROUND_PATCHES_INCLUDED
#define SAGA_GROUND_PATCHES_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Ground/GroundInput.hlsl"

// SagaHash21 / SagaValueNoise / SagaFbm moved to ShaderLibrary/Noise.hlsl so
// Grass.shader can reach them too -- it cannot include this file, because GroundInput.hlsl
// above opens its own UnityPerMaterial CBUFFER. See Grass/Plans/02_GrassAccents.md.
#include "Packages/com.saltbox.saga/ShaderLibrary/Noise.hlsl"

float2 SagaPatchDomain(float2 xz, float scale, float seed)
{
    float s, c;
    sincos(radians(_PatchAngle), s, c);

    float2 q = float2(xz.x * c - xz.y * s, xz.x * s + xz.y * c);
    q.x *= rcp(max(_PatchStretch, 1e-3));
    q *= rcp(max(scale, 1e-3));

    return q + seed * 37.31;
}

float SagaPatchValue(float2 xz, float scale, float seed)
{
    float2 q = SagaPatchDomain(xz, scale, seed);

    return SagaFbm(q, (int)max(_PatchOctaves, 1.0), _PatchGain);
}

half3 SagaGroundWash(half3 c, float2 xz)
{
    float w = SagaFbm(xz * rcp(max(_WashScale, 1e-3)), 3, 0.5);
    return c * (half)(1.0 + w * _WashStrength);
}

half3 SagaApplyGroundPatches(half3 albedo, float2 xz)
{
    half3 c = albedo;

    [branch] if (_PatchCount >= 0.5)
    {
        if (SagaPatchValue(xz, _PatchScale1, _PatchSeed1) > _PatchThreshold1) c = half3(_PatchColor1.rgb);

        [branch] if (_PatchCount >= 1.5)
        {
            if (SagaPatchValue(xz, _PatchScale2, _PatchSeed2) > _PatchThreshold2) c = half3(_PatchColor2.rgb);
        }
        [branch] if (_PatchCount >= 2.5)
        {
            if (SagaPatchValue(xz, _PatchScale3, _PatchSeed3) > _PatchThreshold3) c = half3(_PatchColor3.rgb);
        }
        [branch] if (_PatchCount >= 3.5)
        {
            if (SagaPatchValue(xz, _PatchScale4, _PatchSeed4) > _PatchThreshold4) c = half3(_PatchColor4.rgb);
        }
    }

    [branch] if (_WashStrength > 0.001) c = SagaGroundWash(c, xz);

    return c;
}

#endif
