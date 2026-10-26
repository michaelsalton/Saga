#ifndef SAGA_STANDARD_LIGHTING_INCLUDED
#define SAGA_STANDARD_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"

#include "Packages/com.saltbox.saga/ShaderLibrary/PBR.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"

half3 SagaDirectBRDF(half3 N, half3 V, Light light, half3 diffuseAlbedo, half3 f0, float alpha, half3 energyCompensation)
{
    half3 L = light.direction;
    half3 H = SafeNormalize(L + V);
    half NdotL = saturate(dot(N, L));
    half NdotV = max(dot(N, V), 1e-4h);
    float NdotH = saturate(dot(N, H));
    half LdotH = saturate(dot(L, H));

    float distribution = SagaDistributionGGX(NdotH, alpha);
    float visibility = SagaVisibilitySmithGGX(NdotL, NdotV, alpha);
    half3 fresnel = SagaFresnelSchlick(f0, LdotH);
    // Unity's light colours already carry Lambert's missing 1/PI, so specular is scaled to match
    half3 specular = half(distribution * visibility * PI) * fresnel * energyCompensation;
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * NdotL);

    return (diffuseAlbedo + specular) * radiance;
}

half3 SagaStandardLighting(InputData inputData, half3 albedo, half metallic, half perceptualRoughness, half ao)
{
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;
    half NdotV = max(dot(N, V), 1e-4h);

    half3 diffuseAlbedo = albedo * (1.0h - SAGA_DIELECTRIC_F0) * (1.0h - metallic);
    half3 f0 = lerp((half3)SAGA_DIELECTRIC_F0, albedo, metallic);
    perceptualRoughness = max(perceptualRoughness, SAGA_MIN_ROUGHNESS);
    float alpha = perceptualRoughness * perceptualRoughness;

    half2 scaleBias = SagaEnvironmentBRDFScaleBias(perceptualRoughness, NdotV);

    #if defined(_MULTISCATTER_OFF)
        half3 energyCompensation = 1.0h;
    #else
        half3 energyCompensation = SagaEnergyCompensation(f0, scaleBias);
    #endif

    Light main = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    // clouds gobo the same sun the shadow map does
    main.shadowAttenuation = min(main.shadowAttenuation, SagaCloudShadow(inputData.positionWS));
    half3 col = SagaDirectBRDF(N, V, main, diffuseAlbedo, f0, alpha, energyCompensation);

#if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light add = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        col += SagaDirectBRDF(N, V, add, diffuseAlbedo, f0, alpha, energyCompensation);
    LIGHT_LOOP_END
#endif

    half3 specularAlbedo = SagaEnvironmentBRDF(f0, scaleBias) * energyCompensation;
    #if defined(_ENERGY_BALANCE_OFF)
        half3 ambientDiffuseAlbedo = diffuseAlbedo;
    #else
        // light the specular lobe reflects off the surface never enters it to scatter back out as diffuse
        half3 ambientDiffuseAlbedo = albedo * (1.0h - metallic) * (1.0h - specularAlbedo);
    #endif
    col += inputData.bakedGI * ambientDiffuseAlbedo * ao;

    half3 reflectDir = reflect(-V, N);
    half3 environment = GlossyEnvironmentReflection(reflectDir, inputData.positionWS, perceptualRoughness, ao, inputData.normalizedScreenSpaceUV);
    col += environment * specularAlbedo;

    return col;
}

#endif
