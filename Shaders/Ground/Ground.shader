Shader "Saga/Ground"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.8, 0.8, 0.8, 1)

        [Header(Surface)]
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        [NoScaleOffset] _ORMMap ("ORM (R=AO G=Rough B=Metal)", 2D) = "white" {}
        _Roughness ("Roughness",  Range(0, 1)) = 0.5
        _Metallic ("Metallic",   Range(0, 1)) = 0
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(World Space UVs)]
        [Toggle(_WORLD_UV)] _WorldUV ("World Space UVs (triplanar)", Float) = 0
        _MetresPerTile ("Metres Per Texture Repeat", Range(0.05, 16)) = 1
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 6

        [Header(Cel Shading)]
        _Bands ("Light Bands", Range(2, 8)) = 2
        _ShadowTint ("Shadow Tint", Color) = (0.5, 0.55, 0.65, 1)
        _NdlWrap ("N dot L Wrap", Range(0, 1)) = 1
        _AddLightBands ("Additional Light Bands", Range(1, 8)) = 3
        _BandSoftness ("Band Softness", Range(0, 1)) = 0
        _BandShadow ("Band the Shadow", Range(0, 1)) = 0
        [IntRange] _GIBands ("Baked GI Bands (0 = smooth)", Range(0, 8)) = 0
        _GIRange ("GI Range (irradiance at top band)", Range(0.05, 4)) = 0.5

        [Header(Specular)]
        [HDR] _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecIntensity ("Intensity (1 = physical, 25 = mirror)", Range(0, 25)) = 25
        _SpecCutoffSmooth ("Smooth Cutoff", Range(0.5, 1)) = 0.98
        _SpecCutoffRough ("Rough Cutoff", Range(0, 1)) = 0.5
        _SpecEdge ("Edge Softness", Range(0, 0.25)) = 0.02

        [Header(Emissive)]
        [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissiveIntensity ("Emissive Intensity", Range(0, 8)) = 1

        [Header(Ground Color Patches)]
        [IntRange] _PatchCount ("Active Patch Layers", Range(0, 4)) = 4

        [Header(Patch Noise Character)]
        [IntRange] _PatchOctaves ("Octaves (3+ or you get circles)", Range(1, 8)) = 5
        _PatchGain ("Octave Gain (roughness)", Range(0.2, 0.8)) = 0.6
        _PatchStretch ("Stretch (1 = round)", Range(1, 8)) = 5.5
        _PatchAngle ("Stretch Angle (degrees)", Range(0, 180)) = 0

        [Header(Ground Tonal Wash)]
        _WashScale ("Wash Size (metres)", Range(1, 64)) = 12
        _WashStrength ("Wash Strength", Range(0, 0.5)) = 0.12

        [Header(Patch Layers)]
        _PatchColor1 ("Layer 1 Color", Color) = (0.034, 0.122, 0.005, 1)
        _PatchScale1 ("Layer 1 Scale (metres)", Range(1.5, 64)) = 6.0
        _PatchThreshold1 ("Layer 1 Threshold", Range(-0.75, 0.75)) = -0.20
        _PatchSeed1 ("Layer 1 Seed", Range(0, 64)) = 0

        _PatchColor2 ("Layer 2 Color", Color) = (0.050, 0.164, 0.006, 1)
        _PatchScale2 ("Layer 2 Scale (metres)", Range(1.5, 64)) = 3.5
        _PatchThreshold2 ("Layer 2 Threshold", Range(-0.75, 0.75)) = 0.05
        _PatchSeed2 ("Layer 2 Seed", Range(0, 64)) = 1

        _PatchColor3 ("Layer 3 Color", Color) = (0.071, 0.213, 0.009, 1)
        _PatchScale3 ("Layer 3 Scale (metres)", Range(1.5, 64)) = 2.4
        _PatchThreshold3 ("Layer 3 Threshold", Range(-0.75, 0.75)) = 0.20
        _PatchSeed3 ("Layer 3 Seed", Range(0, 64)) = 2

        _PatchColor4 ("Layer 4 Color", Color) = (0.100, 0.291, 0.014, 1)
        _PatchScale4 ("Layer 4 Scale (metres)", Range(1.5, 64)) = 1.8
        _PatchThreshold4 ("Layer 4 Threshold", Range(-0.75, 0.75)) = 0.35
        _PatchSeed4 ("Layer 4 Seed", Range(0, 64)) = 3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.saltbox.saga/Shaders/Ground/GroundInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.saltbox.saga/Shaders/Ground/GroundPatches.hlsl"

            #define SAGA_MODIFY_SURFACE(surf, positionWS) \
                surf.albedo = SagaApplyGroundPatches(surf.albedo, (positionWS).xz)

            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Lit/LitForwardPass.hlsl"

            ENDHLSL
        }

        // --------------------------------------------------------------
        // Shadow caster
        // --------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Lit/LitShadowCasterPass.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------------------
        // DepthOnly — LOAD-BEARING. Grass.shader and the water stack both
        // sample scene depth, and GroundMaskPass draws the ground with
        // ZTest Equal against it. Do not drop this pass.
        // --------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Lit/LitDepthOnlyPass.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------------------
        // DepthNormals — behaviour no-op today, see the pass file
        // --------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Lit/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------------------
        // Meta — bake-time only. Note it does NOT get the patches: the meta
        // Varyings carry UV only, with no world position to index the field
        // by, so GI bounce reads the flat _BaseColor. Deliberate, and the
        // reasoning is in LitMetaPass.hlsl.
        // --------------------------------------------------------------
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment SagaFragmentMeta
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Lit/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
