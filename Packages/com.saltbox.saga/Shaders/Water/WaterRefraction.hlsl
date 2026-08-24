#ifndef SAGA_WATER_REFRACTION_INCLUDED
#define SAGA_WATER_REFRACTION_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"

struct SagaRefractionResult
{
    half3 color;
    float2 uv;
    float surfaceEye;   // eye depth of the water surface itself. Reflection measures its path from here
    float columnEye;    // path length along the view ray (Beer-Lambert absorption)
    float columnDepth;  // vertical metres of water (rig-independent shallow/deep ramps)
    float columnDepthAtUV;
    float3 floorWS;
};

static const float SAGA_REFRACTION_REF_TILT = 0.06;

float2 SagaSurfaceScreenOffset(float3 finalNormal, float distortionPx)
{
    float3 deviation = finalNormal - float3(0.0, 1.0, 0.0);
    float2 dirVS = TransformWorldToViewDir(deviation).xy;

    float2 offsetPx = dirVS * (distortionPx * rcp(SAGA_REFRACTION_REF_TILT));
    return offsetPx * rcp(GetScaledScreenParams().xy);
}

float2 SagaRefractionOffset(float3 finalNormal)
{
    return SagaSurfaceScreenOffset(finalNormal, _RefractionDistortion);
}

float SagaRefractionEdgeFade(float2 screenUV)
{
    float2 f = smoothstep(0.0, max(_RefractionEdgeFade, 1e-4), min(screenUV, 1.0 - screenUV));
    return min(f.x, f.y);
}

float SagaWaterDepthAtUV(float2 uv, float surfaceWorldY)
{
    float3 ws = ComputeWorldSpacePosition(uv, SagaSampleRawDepth(uv), UNITY_MATRIX_I_VP);
    return surfaceWorldY - ws.y;
}

SagaRefractionResult SagaCalculateRefraction(float2 screenUV, float surfaceRawDepth,
                                             float surfaceWorldY, float3 finalNormal)
{
    SagaRefractionResult o;
    float surfaceEye = SagaOrthoEyeDepth(surfaceRawDepth);
    float sceneEye = SagaOrthoEyeDepth(SagaSampleRawDepth(screenUV));
    o.surfaceEye = surfaceEye;
    o.columnEye = max(sceneEye - surfaceEye, 0.0);

    o.columnDepth = o.columnEye * abs(GetViewForwardDir().y);

    float2 offset = SagaRefractionOffset(finalNormal) * SagaRefractionEdgeFade(screenUV);

    float2 probeUV = screenUV + offset * saturate(o.columnDepth / _RefractionFullDepth);
    float tapDepth = SagaWaterDepthAtUV(probeUV, surfaceWorldY);

    o.uv = screenUV + offset * saturate(min(tapDepth, o.columnDepth) / _RefractionFullDepth);

    float3 floorWS = ComputeWorldSpacePosition(o.uv, SagaSampleRawDepth(o.uv), UNITY_MATRIX_I_VP);
    o.floorWS = floorWS;
    o.columnDepthAtUV = max(surfaceWorldY - floorWS.y, 0.0);

    o.color = SampleSceneColor(o.uv) * _WaterRefractionColor.rgb;

    return o;
}

#endif
