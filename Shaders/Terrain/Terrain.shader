Shader "Saga/Terrain"
{
    Properties
    {
        [Header(Sand  vertex colour R)]
        _SandTint ("Tint", Color) = (0.76, 0.69, 0.52, 1)
        [NoScaleOffset] _SandBaseMap ("Albedo", 2D) = "white" {}
        [Normal][NoScaleOffset] _SandNormalMap ("Normal", 2D) = "bump" {}
        _SandNormalScale ("Normal Scale", Range(0, 4)) = 1
        [NoScaleOffset] _SandORMMap ("ORM (R=AO G=Rough)", 2D) = "white" {}
        [NoScaleOffset] _SandHeightMap ("Height (R)", 2D) = "gray" {}
        _SandMetresPerTile ("Metres Per Texture Repeat", Range(0.25, 32)) = 4

        [Header(Grass  vertex colour G)]
        _GrassTint ("Tint", Color) = (0.32, 0.45, 0.2, 1)
        [NoScaleOffset] _GrassBaseMap ("Albedo", 2D) = "white" {}
        [Normal][NoScaleOffset] _GrassNormalMap ("Normal", 2D) = "bump" {}
        _GrassNormalScale ("Normal Scale", Range(0, 4)) = 1
        [NoScaleOffset] _GrassORMMap ("ORM (R=AO G=Rough)", 2D) = "white" {}
        [NoScaleOffset] _GrassHeightMap ("Height (R)", 2D) = "gray" {}
        _GrassMetresPerTile ("Metres Per Texture Repeat", Range(0.25, 32)) = 4

        [Header(Rock  vertex colour B  triplanar)]
        _RockTint ("Tint", Color) = (0.45, 0.43, 0.4, 1)
        [NoScaleOffset] _RockBaseMap ("Albedo", 2D) = "white" {}
        [Normal][NoScaleOffset] _RockNormalMap ("Normal", 2D) = "bump" {}
        _RockNormalScale ("Normal Scale", Range(0, 4)) = 1
        [NoScaleOffset] _RockORMMap ("ORM (R=AO G=Rough)", 2D) = "white" {}
        [NoScaleOffset] _RockHeightMap ("Height (R)", 2D) = "gray" {}
        _RockMetresPerTile ("Metres Per Texture Repeat", Range(0.25, 32)) = 6

        [Header(Seabed  vertex colour A)]
        _SeabedTint ("Tint", Color) = (0.55, 0.5, 0.4, 1)
        [NoScaleOffset] _SeabedBaseMap ("Albedo", 2D) = "white" {}
        [Normal][NoScaleOffset] _SeabedNormalMap ("Normal", 2D) = "bump" {}
        _SeabedNormalScale ("Normal Scale", Range(0, 4)) = 1
        [NoScaleOffset] _SeabedORMMap ("ORM (R=AO G=Rough)", 2D) = "white" {}
        [NoScaleOffset] _SeabedHeightMap ("Height (R)", 2D) = "gray" {}
        _SeabedMetresPerTile ("Metres Per Texture Repeat", Range(0.25, 32)) = 4

        [Header(Blending)]
        _HeightBlendDepth ("Height Blend Depth", Range(0.01, 1)) = 0.2
        _TriplanarSharpness ("Rock Triplanar Sharpness", Range(1, 16)) = 4
        _Roughness ("Roughness", Range(0, 1)) = 1
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // every pass includes this first, so they all see the same UnityPerMaterial layout and the SRP Batcher accepts the shader
        HLSLINCLUDE
        #include "Packages/com.saltbox.saga/Shaders/Terrain/TerrainInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex TerrainVert
            #pragma fragment TerrainFrag
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Terrain/TerrainForwardPass.hlsl"
            ENDHLSL
        }

        // Standard's geometry passes as they are: they only read position and normal, and never clip since alpha test is off
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
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardShadowCasterPass.hlsl"
            ENDHLSL
        }

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
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardDepthOnlyPass.hlsl"
            ENDHLSL
        }

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
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
