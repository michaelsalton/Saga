#ifndef SAGA_DEPTH_INCLUDED
#define SAGA_DEPTH_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float SagaSampleRawDepth(float2 uv)
{
    return SampleSceneDepth(uv);
}

float SagaOrthoEyeDepth(float rawDepth)
{
#if UNITY_REVERSED_Z
    return _ProjectionParams.z - (_ProjectionParams.z - _ProjectionParams.y) * rawDepth; // raw=1 near, 0 far
#else
    return _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * rawDepth; // GL: raw=0 near, 1 far
#endif
}

float SagaEyeDepth(float rawDepth)
{
    return (unity_OrthoParams.w == 1.0) ? SagaOrthoEyeDepth(rawDepth)
                                        : LinearEyeDepth(rawDepth, _ZBufferParams);
}

float3 SagaViewPos(float2 uv, float rawDepth)
{
    float2 ndc = uv * 2.0 - 1.0;
    return float3(ndc.x * unity_OrthoParams.x * 0.5,
                  ndc.y * unity_OrthoParams.y * 0.5,
                  -SagaOrthoEyeDepth(rawDepth));
}

float3 SagaReconstructViewNormal(float2 uv, float2 texel)
{
    float2 du = float2(texel.x, 0.0);
    float2 dv = float2(0.0, texel.y);

    float3 pC = SagaViewPos(uv,      SagaSampleRawDepth(uv));
    float3 pR = SagaViewPos(uv + du, SagaSampleRawDepth(uv + du));
    float3 pL = SagaViewPos(uv - du, SagaSampleRawDepth(uv - du));
    float3 pD = SagaViewPos(uv + dv, SagaSampleRawDepth(uv + dv));
    float3 pU = SagaViewPos(uv - dv, SagaSampleRawDepth(uv - dv));

    float eC = -pC.z, eR = -pR.z, eL = -pL.z, eD = -pD.z, eU = -pU.z;
    float eRR = SagaOrthoEyeDepth(SagaSampleRawDepth(uv + 2.0 * du));
    float eLL = SagaOrthoEyeDepth(SagaSampleRawDepth(uv - 2.0 * du));
    float eDD = SagaOrthoEyeDepth(SagaSampleRawDepth(uv + 2.0 * dv));
    float eUU = SagaOrthoEyeDepth(SagaSampleRawDepth(uv - 2.0 * dv));

    float errR = abs(2.0 * eR - eRR - eC);
    float errL = abs(2.0 * eL - eLL - eC);
    float errD = abs(2.0 * eD - eDD - eC);
    float errU = abs(2.0 * eU - eUU - eC);

    float3 dpx = (errR <= errL) ? (pR - pC) : (pC - pL);
    float3 dpy = (errD <= errU) ? (pD - pC) : (pC - pU);
    return normalize(cross(dpx, dpy));
}

#endif // SAGA_DEPTH_INCLUDED
