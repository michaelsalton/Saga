#ifndef SAGA_GRASS_INPUT_INCLUDED
#define SAGA_GRASS_INPUT_INCLUDED

#include "Packages/com.saltbox.saga/ShaderLibrary/Depth.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float  _Cutoff;
    float _GroundHeight;
    float _GroundProbeTolerance;

    float _AccentCount;
    float _AccentSeed;
    float _SizeJitter;

    float  _AccentChance1;
    float  _AccentWidth1;
    float  _AccentHeight1;
    float4 _AccentColor1;
    float  _AccentTint1;

    float  _AccentChance2;
    float  _AccentWidth2;
    float  _AccentHeight2;
    float4 _AccentColor2;
    float  _AccentTint2;

    float  _AccentChance3;
    float  _AccentWidth3;
    float  _AccentHeight3;
    float4 _AccentColor3;
    float  _AccentTint3;

    float _WindBend;          // degrees at a full gust; 0 = feature off
    float _WindSpeed;         // metres per second
CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_GrassGroundColorTex);
SAMPLER(sampler_GrassGroundColorTex);
float4 _GrassGroundColorRect;

TEXTURE2D(_GrassGroundMaskTex);
SAMPLER(sampler_GrassGroundMaskTex);
float _GrassGroundMaskValid;

float2 GrassGroundUV(float2 worldXZ)
{
    return (worldXZ - _GrassGroundColorRect.xy) * _GrassGroundColorRect.zw;
}

bool IsGrassGround(float2 worldXZ)
{
    return SAMPLE_TEXTURE2D_LOD(_GrassGroundColorTex, sampler_GrassGroundColorTex,
                                GrassGroundUV(worldXZ), 0).a >= 0.5;
}

float4 WorldToScreenNDC(float3 positionWS)
{
    float4 positionCS = TransformWorldToHClip(positionWS);
    float4 ndc = positionCS * 0.5;
    return float4(float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w, positionCS.zw);
}

#endif
