Shader "Hidden/Saga/BackdropBlur"
{
    // dual Kawase blur: halve resolution with a 5-tap filter per level, then double back up with an 8-tap ring
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // set per draw from the pass, so they live outside any material cbuffer
        float4 _SourceTexelSize;
        float _BlurAmount;
        TEXTURE2D_X(_BlurredTexture);

        // forced linear sampler: every tap relies on bilinear averaging, whatever filter the texture was created with
        half4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        }
        ENDHLSL

        Pass
        {
            Name "Down"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown

            half4 FragDown(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                // a half-res pixel centre sits on a corner of four source texels; one texel further is the next corner, so each tap averages another 2x2
                float2 offset = _SourceTexelSize.xy;
                half4 sum = SampleSource(uv) * 4.0;
                sum += SampleSource(uv + float2(-offset.x, -offset.y));
                sum += SampleSource(uv + float2( offset.x, -offset.y));
                sum += SampleSource(uv + float2(-offset.x,  offset.y));
                sum += SampleSource(uv + float2( offset.x,  offset.y));
                return sum / 8.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Up"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUp

            half4 FragUp(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 halfTexel = _SourceTexelSize.xy * 0.5;
                // a ring of taps around the pixel: axis taps reach further but count half, which approximates a Gaussian falloff
                half4 sum = SampleSource(uv + float2(-halfTexel.x * 2.0, 0.0));
                sum += SampleSource(uv + float2( halfTexel.x * 2.0, 0.0));
                sum += SampleSource(uv + float2(0.0, -halfTexel.y * 2.0));
                sum += SampleSource(uv + float2(0.0,  halfTexel.y * 2.0));
                sum += SampleSource(uv + float2(-halfTexel.x,  halfTexel.y)) * 2.0;
                sum += SampleSource(uv + float2( halfTexel.x,  halfTexel.y)) * 2.0;
                sum += SampleSource(uv + float2(-halfTexel.x, -halfTexel.y)) * 2.0;
                sum += SampleSource(uv + float2( halfTexel.x, -halfTexel.y)) * 2.0;
                return sum / 12.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 sharp = SampleSource(input.texcoord);
                half4 blurred = SAMPLE_TEXTURE2D_X(_BlurredTexture, sampler_LinearClamp, input.texcoord);
                return lerp(sharp, blurred, _BlurAmount);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
