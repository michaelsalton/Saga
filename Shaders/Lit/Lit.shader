Shader "Saga/Lit"
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

        [Header(Relief)]
        [Toggle(_RELIEF)] _Relief ("Parallax Occlusion Mapping", Float) = 0
        [NoScaleOffset] _HeightMap ("Height Map (R)", 2D) = "white" {}
        _ReliefDepth ("Relief Depth (m under World UV, else UV)", Range(0, 0.3)) = 0.06
        [IntRange] _ReliefSteps ("March Steps", Range(4, 32)) = 12
        _ReliefMin ("Height Remap Min", Range(0, 1)) = 0
        _ReliefMax ("Height Remap Max", Range(0, 1)) = 1

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
        _SpecIntensity ("Intensity (1 = physical, 25 = mirror)", Range(0, 25)) = 0
        _SpecCutoffSmooth ("Smooth Cutoff", Range(0.5, 1)) = 0.98
        _SpecCutoffRough ("Rough Cutoff", Range(0, 1)) = 0
        _SpecEdge ("Edge Softness", Range(0, 0.25)) = 0

        [Header(Emissive)]
        [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissiveIntensity ("Emissive Intensity", Range(0, 8)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.saltbox.saga/Shaders/Lit/LitInput.hlsl"

        #define SAGA_OCCLUDABLE
        ENDHLSL

        // --------------------------------------------------------------
        // Forward lit — full PBR material, ramp-stylized main light
        // --------------------------------------------------------------
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
        // DepthOnly — load-bearing, see the pass file
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
        // Meta — bake-time only
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
