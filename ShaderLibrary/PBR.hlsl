#ifndef SAGA_PBR_INCLUDED
#define SAGA_PBR_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#define SAGA_DIELECTRIC_F0 0.04h
#define SAGA_MIN_ROUGHNESS 0.08h
#define SAGA_SPECULAR_AA_VARIANCE 0.25
#define SAGA_SPECULAR_AA_THRESHOLD 0.2

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

// Karis analytic fit of the split-sum DFG term: specular albedo = f0 * x + y
half2 SagaEnvironmentBRDFScaleBias(half perceptualRoughness, half NdotV)
{
    const half4 slope = half4(-1.0h, -0.0275h, -0.572h, 0.022h);
    const half4 intercept = half4(1.0h, 0.0425h, 1.04h, -0.04h);
    half4 fit = perceptualRoughness * slope + intercept;
    half grazing = min(fit.x * fit.x, exp2(-9.28h * NdotV)) * fit.x + fit.y;
    return half2(-1.04h, 1.04h) * grazing + fit.zw;
}

half3 SagaEnvironmentBRDF(half3 f0, half2 scaleBias)
{
    return f0 * scaleBias.x + scaleBias.y;
}

// multiscatter (Turquin/Filament): rescale single-scatter lobe so a white furnace reflects everything
half3 SagaEnergyCompensation(half3 f0, half2 scaleBias)
{
    // albedo of the lobe at Fresnel = 1, the fraction single scattering keeps
    half singleScatterAlbedo = scaleBias.x + scaleBias.y;
    return 1.0h + f0 * (1.0h / singleScatterAlbedo - 1.0h);
}

// Lagarde/Frostbite: AO reshaped by view angle and lobe width; collapses to ao when rough
half SagaSpecularOcclusion(half NdotV, half ao, float alpha)
{
    return saturate(pow(NdotV + ao, half(exp2(-16.0 * alpha - 1.0))) - 1.0h + ao);
}

// Russell/Marmoset: fade reflections whose direction dips below the real (vertex) surface
half SagaHorizonOcclusion(half3 reflectDir, half3 geometricNormalWS)
{
    // 1.3: fully faded by ~50 degrees below the horizon instead of at 90
    half horizon = saturate(1.0h + 1.3h * dot(reflectDir, geometricNormalWS));
    return horizon * horizon;
}

// geometric specular AA (Kaplanyan-Tokuyoshi): widen GGX lobe by how much N changes across pixel quad
half SagaSpecularAA(half perceptualRoughness, half3 normalWS)
{
    float3 normalDx = ddx(float3(normalWS));
    float3 normalDy = ddy(float3(normalWS));
    float variance = SAGA_SPECULAR_AA_VARIANCE * (dot(normalDx, normalDx) + dot(normalDy, normalDy));
    
    // gaussian slope variances add under convolution, alpha^2 = 2 * variance (Beckman relation)
    float kernelAlphaSq = min(2.0 * variance, SAGA_SPECULAR_AA_THRESHOLD * SAGA_SPECULAR_AA_THRESHOLD);
    float alpha = perceptualRoughness * perceptualRoughness;

    // back from alpha^2 to perceptual roughness
    return half(sqrt(sqrt(saturate(alpha * alpha + kernelAlphaSq))));
}

#endif
