Shader "Saga/Grass"
{
    Properties
    {
        _BaseColor ("Tip Color", Color) = (0.45, 0.6, 0.2, 1)
        _RootColor ("Root Color", Color) = (0.08, 0.15, 0.04, 1)
        _Roughness ("Roughness", Range(0, 1)) = 0.6
        _Translucency ("Translucency", Range(0, 1)) = 0.5
        _ForwardScatter ("Forward Scatter", Range(0, 4)) = 1
        _ForwardScatterPower ("Forward Scatter Tightness", Range(1, 32)) = 8
        _GroundNormalBlend ("Ground Normal Blend", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #define SAGA_EXTRA_MATERIAL_PROPERTIES float4 _RootColor; float _Translucency; float _ForwardScatter; float _ForwardScatterPower; float _GroundNormalBlend;
        #include "Packages/com.saltbox.saga/Shaders/Standard/StandardLighting.hlsl"
        #include "Packages/com.saltbox.saga/Shaders/Grass/GrassBlade.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            // structured buffers in the vertex stage need shader model 4.5
            #pragma target 4.5
            #pragma vertex GrassVert
            #pragma fragment GrassFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.saltbox.saga/Shaders/Grass/GrassForwardPass.hlsl"
            ENDHLSL
        }
    }
}
