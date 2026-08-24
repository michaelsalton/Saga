#ifndef SAGA_LIT_META_PASS_INCLUDED
#define SAGA_LIT_META_PASS_INCLUDED

#pragma target 2.0
#pragma shader_feature EDITOR_VISUALIZATION

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

#include "Packages/com.saltbox.saga/ShaderLibrary/PBR.hlsl"

half4 SagaFragmentMeta(Varyings IN) : SV_TARGET
{
    MetaInput metaInput = (MetaInput)0;

    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
    half3 orm = SAMPLE_TEXTURE2D(_ORMMap, sampler_ORMMap, IN.uv).rgb;
    half roughness = saturate(orm.g * half(_Roughness));
    half metallic = saturate(orm.b * half(_Metallic));

    half3 f0 = lerp(SAGA_DIELECTRIC_F0, albedo, metallic);

    metaInput.Albedo = albedo * (1.0h - SAGA_DIELECTRIC_F0) * (1.0h - metallic)
                     + f0 * roughness * roughness * 0.5h;

    metaInput.Emission = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, IN.uv).rgb
                       * _EmissionColor.rgb * _EmissiveIntensity;

    return UniversalFragmentMeta(IN, metaInput);
}

#endif
