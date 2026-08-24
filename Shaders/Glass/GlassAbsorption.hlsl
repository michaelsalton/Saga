#ifndef SAGA_GLASS_ABSORPTION_INCLUDED
#define SAGA_GLASS_ABSORPTION_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"

static const float SAGA_GLASS_MIN_COS = 0.05;

half3 SagaGlassTransmittance(float3 normalWS, float3 viewDirWS)
{
    float cosTheta = max(abs(dot(normalWS, viewDirWS)), SAGA_GLASS_MIN_COS);
    float path = _ThicknessMetres * rcp(cosTheta);

    half3 sigma = (1.0h - half3(_TransmissionTint.rgb)) * half(_AbsorptionDensity);

    return exp(-sigma * half(path));
}

#endif // SAGA_GLASS_ABSORPTION_INCLUDED
