#ifndef SAGA_WIND_INCLUDED
#define SAGA_WIND_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Noise.hlsl"

// set by SagaWind.cs as globals, so outside UnityPerMaterial
float2 _WindDirection;
float _WindStrength;
float _WindGustiness;
float _WindGustScale;
// metres the gust field has scrolled downwind, integrated in C# double so it doesn't lose precision over a long session
float2 _WindGustOffset;
// each layer's phase in cycles, integrated and wrapped separately in C#: x,y main sway, z branch
float4 _WindPhases;
// x calm flutter, y gusting flutter
float2 _WindFlutterPhases;
float _WindBranchAmplitude;
float _WindFlutterAmplitude;

// Crysis' stand-in for sin: a triangle wave rounded by a cubic, period 1 cycle, output in [-1, 1]
float SagaSmoothTriangleWave(float cycles)
{
    float triangleWave = abs(frac(cycles + 0.5) * 2.0 - 1.0);
    return triangleWave * triangleWave * (3.0 - 2.0 * triangleWave) * 2.0 - 1.0;
}

// 0 in a lull, 2 in a gust, 1 on average; sampled at the pivot so a gust takes the whole plant at once
float SagaWindGust(float3 pivotWS)
{
    float2 gustUV = (pivotWS.xz - _WindGustOffset) * rcp(max(_WindGustScale, 0.01));
    float gustNoise = SagaFbm(gustUV, 2, 0.5) * 0.5 + 0.5;
    // fBm clusters tightly around 0.5, so stretch it or every plant would feel nearly the same gust
    float gust01 = smoothstep(0.35, 0.65, gustNoise);
    return lerp(1.0, gust01 * 2.0, _WindGustiness);
}

float3 SagaApplyWind(float3 positionWS, float3 normalWS, float3 pivotWS, float bendWeight, float branchWeight, float flutterWeight)
{
    float gust = SagaWindGust(pivotWS);
    float3 downwind = float3(_WindDirection.x, 0, _WindDirection.y);
    float3 crosswind = float3(-_WindDirection.y, 0, _WindDirection.x);
    // random per plant, so neighbours caught in the same gust still don't rock in lockstep
    float plantPhase = SagaHash21(pivotWS.xz);
    float3 fromPivot = positionWS - pivotWS;

    // two waves at a ratio that never lines up, so the sway never visibly repeats
    float mainSway = 0.6 * SagaSmoothTriangleWave(_WindPhases.x + plantPhase) + 0.4 * SagaSmoothTriangleWave(_WindPhases.y + plantPhase);
    // mostly a steady lean downwind with a rock around it; the gust scales both
    float3 offset = downwind * (_WindStrength * gust * (0.7 + 0.3 * mainSway) * bendWeight);

    // phase drifts about one cycle per metre across the plant, so nearby vertices swing together like one branch
    float branchPhase = _WindPhases.z + plantPhase + dot(fromPivot, float3(1.3, 0.7, 1.1));
    offset += crosswind * (SagaSmoothTriangleWave(branchPhase) * _WindBranchAmplitude * gust * branchWeight);

    // phase changes every few centimetres, so each leaf flickers on its own
    float leafPhase = plantPhase + dot(fromPivot, float3(7.1, 9.3, 5.7));
    float calmFlutter = SagaSmoothTriangleWave(_WindFlutterPhases.x + leafPhase);
    float gustFlutter = SagaSmoothTriangleWave(_WindFlutterPhases.y + leafPhase);
    // crossfade two fixed-speed waves; multiplying one phase by a gust-dependent speed would make it jump
    float flutter = lerp(calmFlutter, gustFlutter, saturate(gust - 0.5));
    // squared so lulls are nearly still and gusts thrash
    offset += normalWS * (flutter * _WindFlutterAmplitude * gust * gust * flutterWeight);

    float3 bentWS = positionWS + offset;

    // put the vertex back at its original distance from the pivot so stems arc instead of stretching
    float stemLength = length(fromPivot);
    float3 pivotToBent = bentWS - pivotWS;
    return pivotWS + pivotToBent * (stemLength * rsqrt(max(dot(pivotToBent, pivotToBent), 1e-6)));
}
#endif
