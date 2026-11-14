Shader "Saga/Standard"
{
    Properties
    {
        [ToggleUI] _CastShadows ("Cast Shadows", Float) = 1
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Receive Shadows", Float) = 1

        [Enum(Opaque, 0, Transparent, 1)] _Surface ("Surface Type", Float) = 0
        [Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1)

        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 10)) = 1
        [NoScaleOffset] _ORMMap ("ORM (R=AO G=Rough B=Metal)", 2D) = "white" {}
        _Roughness ("Roughness",  Range(0, 1)) = 0.5
        _Metallic ("Metallic",   Range(0, 1)) = 0
        _OcclusionStrength ("Occlusion Strength", Range(0, 10)) = 1

        [ToggleOff(_SPECULAR_AA_OFF)] _SpecularAA ("Specular Anti-Aliasing", Float) = 1
        [ToggleOff(_MULTISCATTER_OFF)] _MultiScatter ("Multiple Scattering Compensation", Float) = 1
        [ToggleOff(_ENERGY_BALANCE_OFF)] _EnergyBalance ("Diffuse/Specular Energy Balance", Float) = 1
        [ToggleOff(_SPECULAR_OCCLUSION_OFF)] _SpecularOcclusion ("Specular/Horizon Occlusion", Float) = 1

        _DetailMask ("Detail Mask (A)", 2D) = "white" {}
        _DetailAlbedoMap ("Detail Albedo", 2D) = "linearGrey" {}
        _DetailAlbedoMapScale ("Detail Albedo Scale", Range(0, 2)) = 1
        [Normal] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalMapScale ("Detail Normal Scale", Range(0, 8)) = 1

        [Toggle(_WORLD_UV)] _WorldUV ("World Space UVs (triplanar)", Float) = 0
        _MetresPerTile ("Metres Per Texture Repeat", Range(0.05, 16)) = 1
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 1

        [Toggle(_POM_ON)] _POM ("Parallax Occlusion Mapping", Float) = 0
        [NoScaleOffset] _HeightMap ("Height Map (R)", 2D) = "white" {}
        _POMDepth ("POM Depth (m under World UV, else UV)", Range(0, 0.3)) = 0
        [IntRange] _POMSteps ("March Steps (Grazing)", Range(1, 64)) = 16
        [IntRange] _POMMinSteps ("March Steps (Head-On)", Range(1, 64)) = 4
        _POMMin ("Height Remap Min", Range(0, 1)) = 0
        _POMMax ("Height Remap Max", Range(0, 1)) = 1
        [ToggleUI] _POMSilhouette ("Silhouette Clipping (mesh UV 0-1 only)", Float) = 0
        [ToggleUI] _POMShadow ("Self-Shadowing (main light, mesh UV only)", Float) = 0
        _POMShadowHardness ("Shadow Hardness", Range(1, 50)) = 8

        [ToggleOff] _EnvironmentReflections ("Environment Reflections", Float) = 1

        [Toggle(_EMISSION)] _Emission ("Emission", Float) = 0
        [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissiveIntensity ("Emissive Intensity", Range(0, 8)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.saltbox.saga/Shaders/Standard/StandardInput.hlsl"
        ENDHLSL

        // --------------------------------------------------------------
        // Forward lit — full PBR material
        // --------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardForwardPass.hlsl"
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
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardShadowCasterPass.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------------------
        // DepthOnly
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
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardDepthOnlyPass.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------------------
        // DepthNormals
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
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // --------------------------------------------------------------
        // Meta
        // --------------------------------------------------------------
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment SagaFragmentMeta
            #include_with_pragmas "Packages/com.saltbox.saga/Shaders/Standard/StandardMetaPass.hlsl"
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "Saga.Rendering.Editor.StandardShaderGUI"
}
