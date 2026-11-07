#ifndef SAGA_FOLIAGE_INPUT_INCLUDED
#define SAGA_FOLIAGE_INPUT_INCLUDED

// both must exist before Standard's input and passes are included, since the macros are expanded inside them
#define SAGA_EXTRA_MATERIAL_PROPERTIES float _BranchResponse; float _FlutterResponse;
#define SAGA_MODIFY_POSITION_WS(positionWS, IN) positionWS = SagaFoliageWind(positionWS, TransformObjectToWorldNormal(IN.normalOS))

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Wind.hlsl"

// 0 at the mesh's lowest point, 1 at its highest, from the renderer's world-space bounds in UnityPerDraw
float SagaFoliageHeight01(float3 positionWS)
{
#if defined(_WIND_HEIGHT_FALLOFF_OFF)
    return 1.0;
#else
    float bottom = unity_RendererBounds_Min.y;
    float top = unity_RendererBounds_Max.y;
    return saturate((positionWS.y - bottom) * rcp(max(top - bottom, 1e-4)));
#endif
}

// 0 on the trunk axis, 1 at the edge of the canopy
float SagaFoliageRadius01(float3 positionWS, float3 pivotWS)
{
    float2 extent = unity_RendererBounds_Max.xz - unity_RendererBounds_Min.xz;
    float halfWidth = max(max(extent.x, extent.y) * 0.5, 1e-4);
    return saturate(distance(positionWS.xz, pivotWS.xz) * rcp(halfWidth));
}

float3 SagaFoliageWind(float3 positionWS, float3 normalWS)
{
    // translation column of the model matrix; identity under static batching, so foliage must not be batching-static
    float3 pivotWS = GetObjectToWorldMatrix()._m03_m13_m23;
    float height01 = SagaFoliageHeight01(positionWS);
    float radius01 = SagaFoliageRadius01(positionWS, pivotWS);

    // squared so the base barely moves and the tip carries the lean, the way a stem curves instead of hinging
    float bendWeight = Sq(height01);
    // outer canopy swings most; the height factor keeps low spreading roots still
    float branchWeight = radius01 * height01 * _BranchResponse;
    // per material, so a trunk submesh can bend with its tree without its bark rippling
    float flutterWeight = height01 * _FlutterResponse;
    return SagaApplyWind(positionWS, normalWS, pivotWS, bendWeight, branchWeight, flutterWeight);
}

#endif
