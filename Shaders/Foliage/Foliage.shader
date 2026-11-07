Shader "Saga/Foliage"
{
    Properties
    {
        [ToggleUI] _CastShadows ("Cast Shadows", Float) = 1
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows ("Receive Shadows", Float) = 1

        [Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1)

        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 8)) = 1
        [NoScaleOffset] _ORMMap ("ORM (R=AO G=Rough)", 2D) = "white" {}
        _Roughness ("Roughness",  Range(0, 1)) = 0.5
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [ToggleOff(_SPECULAR_AA_OFF)] _SpecularAA ("Specular Anti-Aliasing", Float) = 1
        [ToggleOff(_MULTISCATTER_OFF)] _MultiScatter ("Multiple Scattering Compensation", Float) = 1
        [ToggleOff(_ENERGY_BALANCE_OFF)] _EnergyBalance ("Diffuse/Specular Energy Balance", Float) = 1
        [ToggleOff(_SPECULAR_OCCLUSION_OFF)] _SpecularOcclusion ("Specular/Horizon Occlusion", Float) = 1

        [ToggleOff] _EnvironmentReflections ("Environment Reflections", Float) = 1

        // ToggleOff so the default needs no keyword: a fresh material sways by height without the inspector touching it
        [ToggleOff(_WIND_HEIGHT_FALLOFF_OFF)] _WindHeightFalloff ("Sway Increases With Height", Float) = 1
        _BranchResponse ("Branch Response", Range(0, 1)) = 1
        _FlutterResponse ("Leaf Flutter Response", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // Standard's passes, with wind applied through SAGA_MODIFY_POSITION_WS.
        // Depth and shadow passes cull like the forward pass so two-sided leaves cast and occlude from both sides.
        HLSLINCLUDE
        #include "Packages/com.saltbox.saga/Shaders/Foliage/FoliageInput.hlsl"
        ENDHLSL

        // --------------------------------------------------------------
        // Forward lit — full PBR material
        // --------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // opaque only: leaves cut out with alpha clip, since blended leaves can't write depth and re-sort every frame they sway
            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_vertex _WIND_HEIGHT_FALLOFF_OFF
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local_vertex _WIND_HEIGHT_FALLOFF_OFF
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local_vertex _WIND_HEIGHT_FALLOFF_OFF
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local_vertex _WIND_HEIGHT_FALLOFF_OFF
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
    CustomEditor "Saga.Rendering.Editor.FoliageShaderGUI"
}
