Shader "Saga/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // written by SagaTimeOfDay as globals, so they stay outside UnityPerMaterial; colors arrive already linear
            float4 _SagaSkyZenithColor;
            float4 _SagaSkyHorizonColor;
            float4 _SagaSkyGroundColor;
            float _SagaSkyHorizonFalloff;
            // unit vectors pointing from the camera toward each body
            float4 _SagaSunDirection;
            float4 _SagaMoonDirection;
            float4 _SagaSunDiscColor;
            float4 _SagaSunGlowColor;
            float4 _SagaMoonDiscColor;
            // x = cos(disc radius), y = cos(disc radius + soft edge), z = glow anisotropy
            float4 _SagaSunParams;
            float4 _SagaMoonParams;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewDirection : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // the skybox mesh is centred on the camera and never rotated, so a vertex's position is the direction to it
                OUT.viewDirection = IN.positionOS.xyz;
                return OUT;
            }

            // Henyey-Greenstein phase divided by its peak: 1 looking straight at the body, falling off away from it
            float ForwardScatter(float cosAngle, float anisotropy)
            {
                float denominator = 1.0 + anisotropy * anisotropy - 2.0 * anisotropy * cosAngle;
                return pow(1.0 - anisotropy, 3.0) / pow(max(denominator, 1e-5), 1.5);
            }

            float Disc(float cosAngle, float4 params)
            {
                // cosine falls as the angle grows, so the soft edge's cosine is the lower bound
                return smoothstep(params.y, params.x, cosAngle);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 viewDirection = normalize(IN.viewDirection);
                float height = viewDirection.y;

                // a power below 1 rises fast off zero, so the horizon color stays a thin band and the zenith fills the rest
                float zenithBlend = pow(saturate(height), _SagaSkyHorizonFalloff);
                float3 color = lerp(_SagaSkyHorizonColor.rgb, _SagaSkyZenithColor.rgb, zenithBlend);
                // ~6 degrees of blend below the horizon so the line isn't a hard edge
                color = lerp(color, _SagaSkyGroundColor.rgb, smoothstep(0.0, 0.1, -height));

                float sunCos = dot(viewDirection, _SagaSunDirection.xyz);
                float moonCos = dot(viewDirection, _SagaMoonDirection.xyz);

                color += _SagaSunGlowColor.rgb * ForwardScatter(sunCos, _SagaSunParams.z);

                // discs sit behind the ground; the glow is haze in front of it and is left unmasked
                float aboveHorizon = smoothstep(-0.005, 0.005, height);
                color += _SagaSunDiscColor.rgb * Disc(sunCos, _SagaSunParams) * aboveHorizon;
                color += _SagaMoonDiscColor.rgb * Disc(moonCos, _SagaMoonParams) * aboveHorizon;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
