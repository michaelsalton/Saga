#ifndef SAGA_STANDARD_LIGHTING_INCLUDED
#define SAGA_STANDARD_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"

#include "Packages/com.saltbox.saga/ShaderLibrary/PBR.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"

half3 SagaDirectBRDF(half3 N, half3 V, Light light, half3 diffuseAlbedo, half3 f0, float alpha)
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
    half3 specular = half(distribution * visibility * PI) * fresnel;
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

    Light main = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    // clouds gobo the same sun the shadow map does
    main.shadowAttenuation = min(main.shadowAttenuation, SagaCloudShadow(inputData.positionWS));
    half3 col = SagaDirectBRDF(N, V, main, diffuseAlbedo, f0, alpha);

#if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light add = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        col += SagaDirectBRDF(N, V, add, diffuseAlbedo, f0, alpha);
    LIGHT_LOOP_END
#endif

    col += inputData.bakedGI * diffuseAlbedo * ao;

    half3 reflectDir = reflect(-V, N);
    half3 environment = GlossyEnvironmentReflection(reflectDir, inputData.positionWS, perceptualRoughness, ao, inputData.normalizedScreenSpaceUV);
    col += environment * SagaEnvironmentBRDF(f0, perceptualRoughness, NdotV);

    return col;
}

#endif
