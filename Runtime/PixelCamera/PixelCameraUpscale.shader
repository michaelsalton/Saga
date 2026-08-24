Shader "Saga/PixelCameraUpscale"
{
    // Fullscreen point-sampled upscale for the pixel camera. Driven by Blitter.BlitTexture from
    // PixelCameraUpscalePass: the core Blit.hlsl supplies Vert/Varyings, _BlitTexture and the point
    // sampler. The whole source-UV mapping (overscan crop + sub-pixel loss offset + V-flip) is baked
    // into _BlitScaleBias by PixelCameraController, which Vert applies.
    //
    // The final color & quantization stage is folded in here: since this is a POINT upscale (one internal
    // texel per display block), quantizing the sampled texel is identical to quantizing at internal res,
    // and dither indexed by the sampled internal-texel coord stays grid-locked. When _QuantizeEnabled is 0
    // the fragment is a plain tap (byte-identical to no quantization).
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _QuantizeEnabled;
            float _QuantizeLevels;
            float _DitherEnabled;
            float _DitherStrength;
            float2 _InternalRes;

            // 4x4 ordered Bayer matrix, normalized to (0,1).
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
                // texcoord already has the crop/offset/flip applied by Vert via _BlitScaleBias.
                // Point sampling everywhere — a single bilinear tap reintroduces the blur this all exists to avoid.
                // _BlitTexture is TEXTURE2D_X (stereo/array), so sample through the _X macro.
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                if (_QuantizeEnabled > 0.5)
                {
                    // Quantize in perceptual (sRGB) space so the uniform grid gives EVEN visual steps. The RT
                    // is linear HDR, so clamp to [0,1] and gamma-encode first, then decode back to linear; the
                    // backbuffer re-applies linear->sRGB on store, landing the clean steps on screen.
                    float3 enc = LinearToSRGB(saturate(col.rgb));
                    float levels = max(2.0, _QuantizeLevels);

                    if (_DitherEnabled > 0.5)
                    {
                        // Index Bayer by the SAMPLED internal-RT texel so the pattern locks to the content/grid
                        // (moves with surfaces, never swims on the monitor).
                        int2 ip = int2(floor(input.texcoord * _InternalRes));
                        float threshold = Bayer4[(ip.y & 3) * 4 + (ip.x & 3)];
                        float stepSize = _DitherStrength / (levels - 1.0);
                        enc += (threshold - 0.5) * stepSize;
                    }

                    float3 q = floor(saturate(enc) * (levels - 1.0) + 0.5) / (levels - 1.0);
                    col.rgb = SRGBToLinear(q);
                }

                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
