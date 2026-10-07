Shader "Saga/PixelCameraUpscale"
{
    // pixel-art upscale at any ratio: flat inside each texel, box-filtered only where a screen pixel straddles two
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelCameraUpscale"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // xy = texels, zw = 1 / texels
            float4 _SourceSize;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // width of one screen pixel measured in source texels; capped at 1 since this only magnifies
                float2 pixelFootprint = clamp(fwidth(input.texcoord) * _SourceSize.xy, 1e-5, 1.0);
                // left edge of this screen pixel's footprint, in texel units
                float2 footprintStart = input.texcoord * _SourceSize.xy - 0.5 * pixelFootprint;
                // fraction of the footprint spilling into the next texel: zero for interior pixels
                float2 spill = saturate((frac(footprintStart) - (1.0 - pixelFootprint)) / pixelFootprint);
                // bilinear between this texel's centre and the next weights them by exactly that coverage
                float2 sampleUV = (floor(footprintStart) + 0.5 + spill) * _SourceSize.zw;

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
