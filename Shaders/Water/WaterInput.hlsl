#ifndef SAGA_WATER_INPUT_INCLUDED
#define SAGA_WATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Depth.hlsl"

CBUFFER_START(UnityPerMaterial)

    // Colors
    float4 _WaterShallowColor;
    float4 _WaterDeepColor;
    float _WaterOpacity;
    float _WaterDepthMax;
    float4 _WaterRefractionColor;

    // Scrolling normal details
    float4 _WaterNormalMap1_ST;
    float4 _WaterNormalMap2_ST;
    float2 _NormalMapScrollSpeed;

    // Refraction
    float _RefractionDistortion;   // internal pixels, at SAGA_REFRACTION_REF_TILT of normal tilt
    float _RefractionFullDepth;    // metres of water depth at which distortion reaches maximum
    float _RefractionEdgeFade;

    // Caustics
    float4 _CausticColor;
    float _CausticStrength;
    float _CausticWaveLength;
    float _CausticWarp;            // domain warp, as a fraction of _CausticWaveLength
    float _CausticSpeed;
    float _CausticSharpness;
    float _CausticFullDepth;
    float _CausticDepthTighten;

    // Planar Reflection
    float _ReflectionDistortion;
    float _ReflectionMin;
    float _ReflectionMax;
    float _FresnelPower;

    // Distance-based reflection.
    float _ReflectionFadeStart;      // metres of reflected path before anything changes
    float _ReflectionFadeDistance;   // metres over which the far look is fully reached
    float _ReflectionFarSky;         // how far distant content washes into the mirror's own sky color
    float _ReflectionFarOpacity;     // reflection weight multiplier at max distance. 1 = no opacity fade
    float _ReflectionFarDistortion;  // multiplier on _ReflectionDistortion at max distance
    float _ReflectionFarBlur;        // blur radius at max distance, in whole reflection texels

    // Sun sparkle.
    float4 _SpecColor;
    float _SpecIntensity;      // amplitude; 0 is the off switch (no keyword, no second variant)
    float _SparkleWaveLength;  // metres per micro-normal wave -- FEATURE SIZE. ~0.3 m is ~8 internal px
    float _SparkleStrength;    // micro-normal slope, in multiples of typical wave slope. The sparkle/band mix
    float _SparkleSpeed;       // micro-normal drift, metres/second. Supplies the twinkle
    float _SparkleCutoff;      // drive below this is black. Sets DENSITY
    float _SparkleFalloff;     // drive range above the cutoff mapped onto [0,1] before the exponent
    float _SparklePower;       // exponent on that ramp: higher = smaller, more intense cores

    // Foam. Every length is in METRES -- the field being thresholded is a horizontal world distance.
    float4 _FoamColor;
    float _FoamOpacity;      // 0 is the off switch: the tap loop is branched out entirely
    float _FoamWidth;        // collar width, metres. Also sets the search radius (x1.3)
    float _FoamPierce;       // metres a surface must stand above the STILL-water line to count fully.
                             // Doubles as the fade range: a surface halfway up it gets half coverage
    float _FoamSoftness;     // metres of ramp at the outer edge. Small = crisp rim, but the rim can
                             // only be as crisp as the tap spacing (~0.077 m at 48 taps) is accurate
    float _FoamNoiseSize;    // metres per noise wave -- FEATURE SIZE of the broken edge
    float _FoamNoiseAmount;  // metres of edge wobble; 1 sigma is 0.354x this
    float _FoamNoiseSpeed;   // noise drift, metres/second

    // Tessellation
    float _TessellationFactor;
    float _DampeningFactor;

    // Waves (Circular Gernster)
    float _WaveAmplitude;
    float _WaveSpeed;
    float _EdgeDampenWidth;

CBUFFER_END

TEXTURE2D(_WaterNormalMap1);
TEXTURE2D(_WaterNormalMap2);
TEXTURE2D(_WaterNoiseMap);
TEXTURE2D(_WaterReflectionTex);
TEXTURE2D(_WaterReflectionDepthTex);

float4 _WaterReflectionScaleBias;
float4 _WaterReflectionTexelSize;   // (1/w, 1/h, w, h) of the reflection RT. The blur radius is in these
float4 _WaterReflectionSkyColor;    // the mirror's clear color, linear. What distant reflections wash INTO
float _WaterReflectionDepthValid;   // 1 only while WaterReflectionDepthFeature is installed and running

#endif
