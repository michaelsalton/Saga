#ifndef SAGA_GROUND_INPUT_INCLUDED
#define SAGA_GROUND_INPUT_INCLUDED

#define SAGA_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    // The block down to _SpecEdge mirrors LitInput.hlsl, which this file pre-empts via the
    // SAGA_LIT_INPUT_INCLUDED define above. Shared code reads these names, so the two lists must
    // stay in step. Relief is NOT in that set -- LitRelief.hlsl takes its height map and tuning
    // as arguments, so Saga/Lit owns those five floats alone.
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

    float _PatchCount;

    float _PatchOctaves;
    float _PatchGain;
    float _PatchStretch;
    float _PatchAngle;        // degrees

    float _WashScale;         // metres, wavelength
    float _WashStrength;      // fraction of albedo value, +/-

    float4 _PatchColor1;
    float  _PatchScale1;
    float  _PatchThreshold1;
    float  _PatchSeed1;

    float4 _PatchColor2;
    float  _PatchScale2;
    float  _PatchThreshold2;
    float  _PatchSeed2;

    float4 _PatchColor3;
    float  _PatchScale3;
    float  _PatchThreshold3;
    float  _PatchSeed3;

    float4 _PatchColor4;
    float  _PatchScale4;
    float  _PatchThreshold4;
    float  _PatchSeed4;
CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

TEXTURE2D(_ORMMap);
SAMPLER(sampler_ORMMap);

TEXTURE2D(_EmissiveMap);
SAMPLER(sampler_EmissiveMap);

#endif
