#ifndef SAGA_GLASS_FRESNEL_INCLUDED
#define SAGA_GLASS_FRESNEL_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Quantize.hlsl"

half SagaGlassFresnel(float3 normalWS, float3 positionWS)
{
    float3 V = GetWorldSpaceNormalizeViewDir(positionWS);
    half cosTheta = half(saturate(abs(dot(normalWS, V))));
    half f = half(pow(max(1.0h - cosTheta, 0.0h), half(_FresnelPower)));

    [branch] if (_ReflectBands >= 0.5)
    {
        half n = max(half(_ReflectBands), 1.0h);
        f = SagaSoftQuantize(f * n, half(_ReflectBandSoftness), 0.5h) * rcp(n);
    }

    return lerp(half(_ReflectMin), half(_ReflectMax), saturate(f));
}

#endif // SAGA_GLASS_FRESNEL_INCLUDED
