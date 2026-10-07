#ifndef SAGA_PBR_INCLUDED
#define SAGA_PBR_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#define SAGA_DIELECTRIC_F0 0.04h
#define SAGA_MIN_ROUGHNESS 0.08h

// GGX normal distribution aka Trowbridge-Reitz
float SagaDistributionGGX(float NdotH, float alpha)
{
    float alphaSq = alpha * alpha;
    float denominator = NdotH * NdotH * (alphaSq - 1.0) + 1.0;
    return alphaSq / (PI * denominator * denominator);
}

// height-correlated Smith masking-shadowing with N dot L of Cook-Torrance
float SagaVisibilitySmithGGX(float NdotL, float NdotV, float alpha)
{
    float alphaSq = alpha * alpha;
    float viewTerm = NdotL * sqrt(NdotV * NdotV * (1.0 - alphaSq) + alphaSq);
    float lightTerm = NdotV * sqrt(NdotL * NdotL * (1.0 - alphaSq) + alphaSq);
    return 0.5 / max(viewTerm + lightTerm, 1e-5);
}

// shclicks fresnel approximation
half3 SagaFresnelSchlick(half3 f0, half cosTheta)
{
    return f0 + (1.0h - f0) * pow(1.0h - saturate(cosTheta), 5.0h);
}

half3 SagaEnvironmentBRDF(half3 f0, half perceptualRoughness, half NdotV)
{
    const half4 slope = half4(-1.0h, -0.0275h, -0.572h, 0.022h);
    const half4 intercept = half4(1.0h, 0.0425h, 1.04h, -0.04h);
    half4 fit = perceptualRoughness * slope + intercept;
    half grazing = min(fit.x * fit.x, exp2(-9.28h * NdotV)) * fit.x + fit.y;
    half2 scaleBias = half2(-1.04h, 1.04h) * grazing + fit.zw;
    return f0 * scaleBias.x + scaleBias.y;
}

#endif
