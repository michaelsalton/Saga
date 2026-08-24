#ifndef SAGA_GLASS_INPUT_INCLUDED
#define SAGA_GLASS_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

#include "Packages/com.saltbox.saga/ShaderLibrary/Depth.hlsl"

CBUFFER_START(UnityPerMaterial)

    // Surface normal
    float4 _NormalMap_ST;
    float _NormalScale;
    float _WavinessSlope;   // peak tangent-space slope of the procedural waviness. 0 is the off switch
    float _WavinessScale;   // waves per UV unit. FREQUENCY: raising it shrinks features, not contrast

    // Absorption (Beer-Lambert)
    float4 _TransmissionTint;   // the colour you want to see THROUGH the glass, not an absorption spectrum
    float _AbsorptionDensity;   // how fast the tint is reached per metre of path
    float _ThicknessMetres;     // slab thickness. The chord is this over cos(incidence)

    // Screen-space refraction
    float _RefractionDistortion;  // internal pixels of shift at GRAZING incidence (|normal.xy| == 1)
    float _RefractionEdgeFade;    // UV width of the screen-border fade that stops edge-clamp smearing

    // Reflection + Fresnel
    float4 _ReflectColor;
    float _ReflectMin;          // reflectance head-on. 0.04 is physical for glass
    float _ReflectMax;          // reflectance at grazing. 1.0 is physical
    float _FresnelPower;        // 5 is Schlick; lower widens the rim
    float _ReflectBands;        // quantize the Fresnel ramp into N levels. 0 = smooth
    float _ReflectBandSoftness; // gradient across each band, in units of one band. 0 = hard

    // Sun glint
    float4 _GlintColor;
    float _GlintIntensity;  // 0 is the off switch (no keyword, no second variant)
    float _GlintCutoff;     // drive below this is black. Sets how much of the surface can glint
    float _GlintFalloff;    // drive range above the cutoff mapped onto [0,1] before the exponent
    float _GlintPower;      // exponent on that ramp: higher = smaller, more intense core

    // Composite
    float _CompositeBlend;  // 1 = replace the framebuffer, below 1 lets pre-transparent content bleed

CBUFFER_END

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

#endif
