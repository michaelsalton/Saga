Shader "Saga/OutlineControl"
{
    // Per-object control output for the outline pass. Used as the draw material by OutlineControlPass: the
    // renderers owned by an enabled OutlineControl component are re-drawn (depth-tested against the live
    // scene depth in-shader, ZWrite Off) into the RGBA8 _OutlineControl texture. Nothing else is drawn here,
    // and the texture clears to "not outlined", so the outline is strictly opt-in via that component.
    //
    //   R = object ID (0..1)        -> idEdge separation in the kernel
    //   G = normal/crease weight    -> multiplies the normal term (0 = kill internal crease spaghetti)
    //   B = depth/silhouette weight -> multiplies the depth term
    //   A = threshold bias          -> 0.5 neutral; scales the kernel thresholds (higher = thinner)
    //
    // No Properties block on purpose: the values arrive as a GLOBAL set immediately before each draw (see
    // OutlineControlPass). CommandBuffer.DrawRenderer takes no MaterialPropertyBlock, and a material property
    // of the same name would shadow the global with the material's own value.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineControl"
            Tags { "LightMode" = "UniversalForward" }   // the pass is drawn explicitly by index, tag is cosmetic

            ZWrite Off            // never disturb scene depth
            ZTest Always          // no depth attachment bound; occlusion is done in-shader (clip vs scene depth)
            Cull Back

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // xyz w = id, normal weight, depth weight, threshold bias. Bound per draw via SetGlobalVector, so
            // it must stay OUTSIDE any CBUFFER (a UnityPerMaterial member would be fed by the material, not
            // the global). This shader is not SRP-batched anyway.
            float4 _OutlineControlValues;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Occlusion against the RESOLVED scene depth (no depth attachment is bound, so this works
                // whether or not the scene is multisampled). Discard fragments hidden behind opaque geometry —
                // including un-outlined occluders — so the control texture never bleeds through walls. It also
                // makes draw order here irrelevant: only the frontmost surface survives per pixel.
                float sceneRaw = LoadSceneDepth(uint2(IN.positionHCS.xy));
                float fragRaw  = IN.positionHCS.z;
            #if UNITY_REVERSED_Z
                clip(fragRaw - sceneRaw + 1e-4); // reversed-Z: nearer = larger; discard if frag is behind scene
            #else
                clip(sceneRaw - fragRaw + 1e-4);
            #endif
                return (half4)_OutlineControlValues;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
