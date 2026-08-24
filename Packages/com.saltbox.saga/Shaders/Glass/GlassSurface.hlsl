#ifndef SAGA_GLASS_SURFACE_INCLUDED
#define SAGA_GLASS_SURFACE_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"

static const int SAGA_GLASS_WAVE_COUNT = 3;

static const float4 SAGA_GLASS_WAVE[SAGA_GLASS_WAVE_COUNT] =
{
    float4( 1.0000,  0.0000, 1.000, 0.0),
    float4(-0.7374,  0.6755, 1.469, 0.0),
    float4( 0.0876, -0.9962, 2.157, 0.0),
};

float2 SagaGlassWaviness(float2 uv)
{
    float k0 = TWO_PI * _WavinessScale;
    float2 slope = float2(0.0, 0.0);

    [unroll]
    for (int i = 0; i < SAGA_GLASS_WAVE_COUNT; i++)
    {
        float4 w = SAGA_GLASS_WAVE[i];
        float k = k0 * w.z;
        float phase = frac(i * 0.6180339887) * TWO_PI;
        slope += -sin(k * dot(uv, w.xy) + phase) * w.xy;
    }

    return slope * rcp((float)SAGA_GLASS_WAVE_COUNT) * _WavinessSlope;
}

float3 SagaGlassNormal(float2 uv, half3 normalWS, half4 tangentWS)
{
    half3 nGeomWS = normalize(normalWS);
    half3 tWS = normalize(tangentWS.xyz);
    half3 bWS = tangentWS.w * cross(nGeomWS, tWS);
    half3x3 tbn = half3x3(tWS, bWS, nGeomWS);

    half3 normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap)), half(_NormalScale));

    [branch] if (_WavinessSlope >= 1e-4)
    {
        normalTS.xy += half2(SagaGlassWaviness(uv));
        normalTS = normalize(normalTS);
    }

    return normalize(TransformTangentToWorld(normalTS, tbn));
}

#endif
