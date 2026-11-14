Shader "Saga/Terrain"
{
    Properties
    {
        // filled by the terrain engine by name; declared so unset slots get sane defaults (red control = all layer 0)
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Normal0 ("Normal 0", 2D) = "bump" {}
        [HideInInspector] _Normal1 ("Normal 1", 2D) = "bump" {}
        [HideInInspector] _Normal2 ("Normal 2", 2D) = "bump" {}
        [HideInInspector] _Normal3 ("Normal 3", 2D) = "bump" {}
        [HideInInspector] _Mask0 ("Mask 0", 2D) = "grey" {}
        [HideInInspector] _Mask1 ("Mask 1", 2D) = "grey" {}
        [HideInInspector] _Mask2 ("Mask 2", 2D) = "grey" {}
        [HideInInspector] _Mask3 ("Mask 3", 2D) = "grey" {}
        [HideInInspector][Gamma] _Metallic0 ("Metallic 0", Range(0, 1)) = 0
        [HideInInspector][Gamma] _Metallic1 ("Metallic 1", Range(0, 1)) = 0
        [HideInInspector][Gamma] _Metallic2 ("Metallic 2", Range(0, 1)) = 0
        [HideInInspector][Gamma] _Metallic3 ("Metallic 3", Range(0, 1)) = 0
        [HideInInspector] _Smoothness0 ("Smoothness 0", Range(0, 1)) = 0.5
        [HideInInspector] _Smoothness1 ("Smoothness 1", Range(0, 1)) = 0.5
        [HideInInspector] _Smoothness2 ("Smoothness 2", Range(0, 1)) = 0.5
        [HideInInspector] _Smoothness3 ("Smoothness 3", Range(0, 1)) = 0.5
        [HideInInspector] _TerrainHolesTexture ("Holes Map", 2D) = "white" {}

        // Standard's passes run every uv through TRANSFORM_TEX(uv, _BaseMap); undeclared, its tiling would be zero
        [HideInInspector] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // basemap slot the engine expects to exist
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "grey" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "TerrainCompatible"="True" }

        // Standard's passes, with the heightmap placed through SAGA_MODIFY_POSITION_WS, the splat blend through
        // SAGA_MODIFY_SURFACE, and terrain holes through SAGA_ALPHA_CLIP.
        HLSLINCLUDE
        // patches share the terrain's one matrix and one lightmap; height comes from the heightmap texture
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
        #include "Packages/com.saltbox.saga/Shaders/Terrain/TerrainInput.hlsl"
        ENDHLSL

        // --------------------------------------------------------------
        // Forward lit
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
            // SagaSampleTerrainSurface must be compiled before Frag, which expands SAGA_MODIFY_SURFACE
            #include "Packages/com.saltbox.saga/Shaders/Terrain/TerrainSurface.hlsl"
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

        // scene-view click selection on the terrain
        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
