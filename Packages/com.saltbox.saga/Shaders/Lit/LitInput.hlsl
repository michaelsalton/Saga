#ifndef SAGA_LIT_INPUT_INCLUDED
#define SAGA_LIT_INPUT_INCLUDED

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

    float _Bands;
    float4 _ShadowTint;
    float _NdlWrap;
    float _AddLightBands;
    float _BandSoftness;   // gradient width as a fraction of one band; 0 = hard cel
    float _BandShadow;     // 0 = occlusion multiplies after quantizing (old), 1 = inside it
    float _GIBands;        // quantize baked GI / ambient into N levels; 0 = off (smooth, as before)
    float _GIRange;        // irradiance that maps to the top GI band -- lower it if spill vanishes

    float _WorldUV;
    float _MetresPerTile;
    float _TriplanarSharpness;

    float4 _SpecColor;
    float _SpecIntensity;
    float _SpecCutoffSmooth;
    float _SpecCutoffRough;
    float _SpecEdge;

    float _Relief;
    float _ReliefDepth;
    float _ReliefSteps;
    float _ReliefMin;
    float _ReliefMax;
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
