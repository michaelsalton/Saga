#ifndef SAGA_WATER_DEPTH_COLOR_INCLUDED
#define SAGA_WATER_DEPTH_COLOR_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"

float SagaWaterDepth01(float columnDepth)
{
    return saturate(columnDepth * rcp(max(_WaterDepthMax, 1e-4)));
}

half3 SagaWaterDepthColor(float columnDepth)
{
    return lerp(_WaterShallowColor.rgb, _WaterDeepColor.rgb, SagaWaterDepth01(columnDepth));
}

#endif
