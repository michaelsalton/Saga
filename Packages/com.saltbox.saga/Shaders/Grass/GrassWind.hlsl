#ifndef SAGA_GRASS_WIND_INCLUDED
#define SAGA_GRASS_WIND_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Grass/GrassInput.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Noise.hlsl"

#define SAGA_WIND_DIRECTION   45.0      // degrees; 0 = +X, 90 = +Z. Blows TOWARD this heading
#define SAGA_WIND_SPREAD       0.125    // multiple of PI between the two fields' headings
#define SAGA_WIND_SCALE_1      6.0      // metres
#define SAGA_WIND_SCALE_2      3.5      // metres
#define SAGA_WIND_SPEED_2      1.6667   // field 2 speed, as a multiple of _WindSpeed
#define SAGA_WIND_PHASE        1.5      // seconds of per-tuft time offset
#define SAGA_WIND_FPS          7.0      // frames per second

float SagaWindEnvelope(float2 pivotXZ, float t)
{
    float a0 = radians(SAGA_WIND_DIRECTION);
    float dA = SAGA_WIND_SPREAD * PI;

    float2 d1 = float2(cos(a0 - dA), sin(a0 - dA));
    float2 d2 = float2(cos(a0 + dA), sin(a0 + dA));
    float2 uv1 = (pivotXZ - d1 * (t * _WindSpeed)) * rcp(SAGA_WIND_SCALE_1);
    float2 uv2 = (pivotXZ - d2 * (t * _WindSpeed * SAGA_WIND_SPEED_2)) * rcp(SAGA_WIND_SCALE_2);
    float n1 = SagaValueNoise(uv1) * 0.5 + 0.5;
    float n2 = SagaValueNoise(uv2) * 0.5 + 0.5;

    return saturate(n1 * n2);
}

float3 SagaWindAxis()
{
    float a0 = radians(SAGA_WIND_DIRECTION);
    return float3(sin(a0), 0.0, -cos(a0));
}

float SagaWindBend(float2 pivotXZ, float phase01)
{
    float t = _Time.y + phase01 * SAGA_WIND_PHASE;
    t = floor(t * SAGA_WIND_FPS) * rcp(SAGA_WIND_FPS);

    return radians(_WindBend) * SagaWindEnvelope(pivotXZ, t);
}

#endif
