Shader "Saga/Quantize"
{
    // posterize + ordered dither at whatever resolution the world camera renders: internal RT or full screen
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Quantize"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _QuantizeLevels;
            float _DitherEnabled;
            float _DitherStrength;

            // 4x4 ordered Bayer matrix, normalized to (0,1)
            static const float Bayer4[16] =
            {
                 0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                 3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
            };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                // the grid is uniform in sRGB so the steps look even; the color is linear HDR, hence clamp then encode
                float3 encoded = LinearToSRGB(saturate(col.rgb));
                float levels = max(2.0, _QuantizeLevels);

                if (_DitherEnabled > 0.5)
                {
                    // the pixel being written is the grid cell, at internal res or full res alike
                    int2 pixel = int2(input.positionCS.xy);
                    float threshold = Bayer4[(pixel.y & 3) * 4 + (pixel.x & 3)];
                    encoded += (threshold - 0.5) * (_DitherStrength / (levels - 1.0));
                }

                float3 stepped = floor(saturate(encoded) * (levels - 1.0) + 0.5) / (levels - 1.0);
                col.rgb = SRGBToLinear(stepped);
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
