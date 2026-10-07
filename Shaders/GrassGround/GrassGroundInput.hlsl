#ifndef SAGA_GRASS_GROUND_INPUT_INCLUDED
#define SAGA_GRASS_GROUND_INPUT_INCLUDED

#define SAGA_STANDARD_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    // The block down to _TriplanarSharpness mirrors StandardInput.hlsl, which this file pre-empts via the
    // SAGA_STANDARD_INPUT_INCLUDED define above. Shared code reads these names, so the two lists must
    // stay in step. POM is NOT in that set -- StandardPOM.hlsl takes its height map and tuning
    // as arguments, so Saga/Standard owns those five floats alone.
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
