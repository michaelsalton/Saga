#ifndef SAGA_STANDARD_INPUT_INCLUDED
#define SAGA_STANDARD_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;

    float _NormalScale;

    float _Roughness;

    float _Metallic;
    float _OcclusionStrength;

    float4 _EmissionColor;
    float _EmissiveIntensity;

    float _WorldUV;
    float _MetresPerTile;
    float _TriplanarSharpness;

    float _POM;
    float _POMDepth;
    float _POMSteps;
    float _POMMin;
    float _POMMax;
CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

TEXTURE2D(_ORMMap);
SAMPLER(sampler_ORMMap);

TEXTURE2D(_EmissiveMap);
SAMPLER(sampler_EmissiveMap);

TEXTURE2D(_HeightMap);
SAMPLER(sampler_HeightMap);

#endif
