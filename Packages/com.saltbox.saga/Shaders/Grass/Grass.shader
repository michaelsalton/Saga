Shader "Saga/Grass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Sprite (only alpha is used as mask)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _GroundHeight ("Ground Plane Height (world Y)", Float) = 0
        _GroundProbeTolerance ("Ground Probe Tolerance (world units)", Range(0.01, 2)) = 0.25

        [Header(Per Tuft Size Jitter)]
        _SizeJitter ("Size Jitter (+- fraction)", Range(0, 0.5)) = 0

        [Header(Accent Tufts)]
        [IntRange] _AccentCount ("Active Accent Classes", Range(0, 3)) = 0
        _AccentSeed ("Accent Seed", Range(0, 64)) = 0

        _AccentChance1 ("Class 1 Chance", Range(0, 1)) = 0.08
        _AccentWidth1  ("Class 1 Width",  Range(0.5, 1.6)) = 1
        _AccentHeight1 ("Class 1 Height", Range(0.5, 1.6)) = 1.3
        _AccentColor1  ("Class 1 Color (lit)", Color) = (0.62, 0.72, 0.35, 1)
        _AccentTint1   ("Class 1 Tint",   Range(0, 1)) = 0.35

        _AccentChance2 ("Class 2 Chance", Range(0, 1)) = 0.06
        _AccentWidth2  ("Class 2 Width",  Range(0.5, 1.6)) = 0.9
        _AccentHeight2 ("Class 2 Height", Range(0.5, 1.6)) = 0.8
        _AccentColor2  ("Class 2 Color (lit)", Color) = (0.05, 0.24, 0.11, 1)
        _AccentTint2   ("Class 2 Tint",   Range(0, 1)) = 0.4

        _AccentChance3 ("Class 3 Chance", Range(0, 1)) = 0.03
        _AccentWidth3  ("Class 3 Width",  Range(0.5, 1.6)) = 1.1
        _AccentHeight3 ("Class 3 Height", Range(0.5, 1.6)) = 1.05
        _AccentColor3  ("Class 3 Color (lit)", Color) = (0.85, 0.78, 0.34, 1)
        _AccentTint3   ("Class 3 Tint",   Range(0, 1)) = 0.8

        [Header(Wind Sway)]
        _WindBend ("Bend at Full Gust (degrees)", Range(0, 45)) = 0
        _WindSpeed ("Wind Speed (m per s)", Range(0, 10)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.saltbox.saga/Shaders/Grass/GrassInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
        #include "Packages/com.saltbox.saga/Shaders/Grass/GrassAccents.hlsl"
        #include "Packages/com.saltbox.saga/Shaders/Grass/GrassWind.hlsl"
        #include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 groundUV : TEXCOORD1;
                float4 feetNDC : TEXCOORD2;
                float2 feetEyeCloud : TEXCOORD3;   // .x = eye depth at the feet, .y = cloud shadow mask
                half4  accent : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pivot = GetVertexPositionInputs(float3(0, 0, 0));

                float3 feetWS = float3(pivot.positionWS.x, _GroundHeight, pivot.positionWS.z);

                SagaGrassAccent acc = SagaResolveGrassAccent(feetWS.xz);

                float3 posOS = IN.positionOS.xyz;
                posOS.x *= acc.scale.x;

                float3 posWS = TransformObjectToWorld(posOS);
                posWS.y = (posWS.y - _GroundHeight) * acc.scale.y + _GroundHeight;

                // Bend downwind in a world vertical plane, about the horizontal axis
                // perpendicular to the wind, pivoted at the feet. Camera-independent, so the
                // motion never vanishes at any camera heading.
                [branch] if (_WindBend > 0.001)
                {
                    float bend = SagaWindBend(feetWS.xz,
                                              SagaGrassRoll(feetWS.xz, SAGA_GRASS_SALT_WIND));

                    float3 k = SagaWindAxis();
                    float3 v = posWS - feetWS;

                    float s, c;
                    sincos(bend, s, c);

                    posWS = feetWS + v * c + cross(k, v) * s + k * (dot(k, v) * (1.0 - c));
                }

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.accent      = acc.tint;

                OUT.groundUV = GroundUV(feetWS.xz);
                OUT.feetNDC  = WorldToScreenNDC(feetWS);

                // Cloud shadow is sampled at the FEET, not per fragment, so a whole tuft agrees with
                // the ground it stands on. Evaluating it here is exact rather than an approximation:
                // feetWS derives from the pivot, so all four quad vertices produce the identical value
                // and the interpolation is a no-op -- for a quarter of the fragment-shader cost.
                OUT.feetEyeCloud = float2(-TransformWorldToView(feetWS).z,
                                          SagaCloudShadow(feetWS));

                if (!IsGround(feetWS.xz))
                    OUT.positionHCS = float4(-2, -2, -2, 1);

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                clip(mask - _Cutoff);

                half cloud = (half)IN.feetEyeCloud.y;

                // The baked map is a static snapshot and CANNOT contain moving cloud, so the mask has
                // to be applied to this branch by hand -- otherwise a tuft on the fallback path stays
                // bright while the ground around it darkens.
                half3 col = SAMPLE_TEXTURE2D(_GroundColorTex, sampler_GroundColorTex, IN.groundUV).rgb
                          * cloud;

                float2 suv = IN.feetNDC.xy / IN.feetNDC.w;

                bool inFront  = IN.feetNDC.w > 0.0;
                bool onScreen = all(suv == saturate(suv));
                bool isGround = _GroundMaskValid >= 0.5 &&
                                SAMPLE_TEXTURE2D(_GroundMaskTex, sampler_GroundMaskTex, suv).r >= 0.5;

                float sceneEye = SagaEyeDepth(SampleSceneDepth(suv));

                // Depth agreement as a RAMP, not a cliff.
                //
                // suv comes off the pivot, so it is identical for all four verts -- when this test flips,
                // the ENTIRE tuft swaps colour source in a single frame. That is what reads as flickering
                // while the camera turns, and the quantity being tested is marginal by construction:
                // feetEye assumes the ground sits exactly at _GroundHeight, sceneEye is a single
                // point-sampled depth texel, and rotating the camera modulates the gap between the two.
                // Anything thresholded there was always going to pop. Fading across the upper half of the
                // tolerance costs one smoothstep and turns the pop into a cross-fade.
                float dEye = abs(sceneEye - IN.feetEyeCloud.x);
                half live = 1.0h - (half)smoothstep(_GroundProbeTolerance * 0.5,
                                                    _GroundProbeTolerance, dEye);

                live *= (inFront && onScreen && isGround) ? 1.0h : 0.0h;

                // Deliberately NOT multiplied by cloud: this is the lit ground pixel, which already
                // carries the mask. Applying it again here is the double-darkening failure mode.
                //
                // Still branched, so a tuft on the fallback path pays no scene-colour fetch. live is
                // uniform across the quad, so the branch stays coherent.
                [branch] if (live > 0.0h)
                    col = lerp(col, SampleSceneColor(suv), live);

                // Accent colours are authored as LIT colours, so they need the mask on BOTH branches.
                col = lerp(col, IN.accent.rgb * cloud, IN.accent.a);

                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
