#ifndef SAGA_CLOUD_SHADOWS_INCLUDED
#define SAGA_CLOUD_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Noise.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Quantize.hlsl"

float _CloudStrength;     // 0 = off (exact). Deepest the mask reaches is 1 - this.
float _CloudScale;        // metres per noise cell of the BASE octave. ~8 -- see the scale table.
float _CloudCoverage;     // 0 = clear, 1 = overcast. Remapped internally; see the sigma table.
float _CloudSoftness;     // width of the coverage ramp, in density units. Cloud EDGE hardness.
float _CloudSteps;        // banding: N steps -> N+1 levels. 1 = effectively unstepped.
float _CloudStepSoftness; // gradient across each step, in units of one step. 0 = hard.
float4 _CloudOffset;      // .xy = wind scroll offset in WORLD METRES (integrated in C# double). .zw unused.

float2 SagaCloudPlaneXZ(float3 positionWS)
{
    float3 L = _MainLightPosition.xyz; // unit vector pointing TOWARD the light
    float ly = max(L.y, 0.15);

    float2 xz = positionWS.xz - L.xz * (positionWS.y * rcp(ly));

    xz -= _CloudOffset.xy;

    return xz * rcp(max(_CloudScale, 0.01));
}

half SagaCloudDensity(float3 positionWS)
{
    float2 p = SagaCloudPlaneXZ(positionWS);

    half f = (half)SagaFbm(p, 3, 0.5);
    half d = f * 0.5h + 0.5h;
    half threshold = lerp(0.85h, 0.15h, saturate(_CloudCoverage));
    half density = saturate((d - threshold) * rcp(max(_CloudSoftness, 1e-3h)));
    half n = max(_CloudSteps, 1.0h);
    density = SagaSoftQuantize(density * n, _CloudStepSoftness, 0.5h) * rcp(n);

    return saturate(density);
}

half SagaCloudShadow(float3 positionWS)
{
    [branch] if (_CloudStrength < 0.001h)
        return 1.0h;

    return 1.0h - SagaCloudDensity(positionWS) * _CloudStrength;
}

#endif
