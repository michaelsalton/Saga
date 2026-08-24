Shader "Saga/WaterReflectionDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "WaterReflectionDepth"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // Set by WaterReflection.cs AFTER the oblique skew has landed on the projection matrix.
            //
            // Why not SagaOrthoEyeDepth. That helper decodes raw depth as a plain near..far ramp, which is
            // correct for the world camera and WRONG here: CalculateObliqueMatrix (WaterReflection.cs)
            // replaces row 2 of the projection with the water clip plane, so device depth becomes affine in
            // eye x AND y as well as z. Decoding it with the ramp yields a value with a false diagonal
            // gradient across the screen, and the distance fade downstream reads as a wipe rather than a
            // distance. Inverting the ACTUAL matrix is the only decode that survives the skew -- which is
            // also why this runs here, on the reflection camera, rather than in the water fragment.
            float4x4 _WaterReflectionInvVP;

            // Row 2 of the reflection camera's worldToCameraMatrix, negated: dot(float4(ws, 1), this) is
            // -viewPos.z, i.e. eye depth in metres. Explicit rather than UNITY_MATRIX_V because
            // ShaderLibrary/Depth.hlsl:11 warns the auto-bound matrices are not dependable inside Blitter passes.
            float4 _WaterReflectionEyeAxis;

            float Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float raw = SampleSceneDepth(input.texcoord);

                // Nothing drawn -> the clear color, i.e. sky. 0 is the sentinel the water tests for; sky is
                // the BACKDROP rather than a very distant object, and treating it as distant would fade the
                // whole open surface out. See 08 Amendment, decision 4.
            #if UNITY_REVERSED_Z
                if (raw <= 0.0) return 0.0;
            #else
                if (raw >= 1.0) return 0.0;
            #endif

                float3 positionWS = ComputeWorldSpacePosition(input.texcoord, raw, _WaterReflectionInvVP);
                return max(dot(float4(positionWS, 1.0), _WaterReflectionEyeAxis), 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
