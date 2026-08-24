Shader "Saga/Outline"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ _OUTLINE_CONTROL

            // Main light shadows, so a line can be shaded by whatever shades the surface it traces.
            // _SHADOWS_SOFT is declared to MATCH LitForwardPass.hlsl:4-7 exactly -- same keyword set
            // means the same filtering code path, which is what guarantees the line and the surface
            // never disagree along a terminator. (Godrays omit it on purpose; PCF-filtering a point in
            // mid-air is meaningless. Here the tap lands on a real surface, so it must match.)
            //
            // _MAIN_LIGHT_SHADOWS_SCREEN is deliberately NOT declared, following Godray.shader:18. That
            // path routes TransformWorldToShadowCoord through GetWorldToHClipMatrix(), and this file's
            // whole premise is that camera matrices are not dependable inside a Blitter pass. No
            // ScreenSpaceShadows feature exists on PC_Renderer today; if one is ever added, this variant
            // simply loses MAIN_LIGHT_CALCULATE_SHADOWS and the line falls back to flat rather than
            // reconstructing from an unbound matrix.
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.saltbox.saga/ShaderLibrary/Depth.hlsl"
            #include "Packages/com.saltbox.saga/ShaderLibrary/CameraBasis.hlsl"
            #include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"
            #include "Packages/com.saltbox.saga/ShaderLibrary/WorldOcclusion.hlsl"

            half4  _OutlineColor;     // line color (rgb) + opacity (a); e.g. (1,1,1,1) = opaque white line
            float  _DepthThreshold;   // world-unit depth gap (slope-aware) counting as a silhouette
            float  _NormalThreshold;  // 1-dot normal difference counting as a crease (0..1)
            float4 _OutlineTexel;     // xy = 1/internalRes, zw = internalRes

            half4  _OutlineShadowTint; // rgb multiplies the line where the surface is fully shadowed
            float  _OutlineLighting;   // 0 = flat line (exactly the pre-lighting behavior), 1 = full response

        #if defined(_OUTLINE_CONTROL)
            TEXTURE2D(_OutlineControl);
            SAMPLER(sampler_OutlineControl);
        #endif

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv    = input.texcoord;
                float2 texel = _OutlineTexel.xy;
                float2 offR  = float2(texel.x, 0);   // +u neighbor = screen RIGHT
                float2 offD  = float2(0, texel.y);   // +v neighbor = screen UP (URP y-flips projections into RTs on D3D)

                half normalW = 1.0h, depthW = 1.0h, thrScale = 1.0h, idEdge = 0.0h;

            #if defined(_OUTLINE_CONTROL)
                half4 ctrlC = SAMPLE_TEXTURE2D(_OutlineControl, sampler_OutlineControl, uv);
                // Opt-in: most of the screen clears to (0,0,0,0.5), where no term can fire (both weights 0,
                // and idEdge needs ctrlC.r ABOVE a neighbor's id, which 0 never is). Bail before the depth
                // fetches and normal reconstructions: exact, and alpha 0 is a no-op under the blend.
                if (ctrlC.r + ctrlC.g + ctrlC.b == 0.0h)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                normalW  = ctrlC.g;
                depthW   = ctrlC.b;
                thrScale = ctrlC.a * 2.0h;   // A: 0.5 neutral -> x1; higher = thinner, lower = more sensitive
            #endif

                float eC = SagaOrthoEyeDepth(SagaSampleRawDepth(uv));
                float eR = SagaOrthoEyeDepth(SagaSampleRawDepth(uv + offR));
                float eD = SagaOrthoEyeDepth(SagaSampleRawDepth(uv + offD));
                float eL = SagaOrthoEyeDepth(SagaSampleRawDepth(uv - offR));
                float eU = SagaOrthoEyeDepth(SagaSampleRawDepth(uv - offD));
                float eRR = SagaOrthoEyeDepth(SagaSampleRawDepth(uv + offR + offR));
                float eDD = SagaOrthoEyeDepth(SagaSampleRawDepth(uv + offD + offD));
                float3 nC = SagaReconstructViewNormal(uv, texel);

            #if defined(_OUTLINE_CONTROL)
                half4 ctrlR = SAMPLE_TEXTURE2D(_OutlineControl, sampler_OutlineControl, uv + offR);
                half4 ctrlD = SAMPLE_TEXTURE2D(_OutlineControl, sampler_OutlineControl, uv + offD);
                half4 ctrlL = SAMPLE_TEXTURE2D(_OutlineControl, sampler_OutlineControl, uv - offR);
                half4 ctrlU = SAMPLE_TEXTURE2D(_OutlineControl, sampler_OutlineControl, uv - offD);
            #endif

                float facing    = max(abs(nC.z), 0.05);
                float depthTol  = (_DepthThreshold * thrScale) / facing;
                float nearDelta = max(max(eR - eC, eL - eC), max(eD - eC, eU - eC));
                float depthDisc = step(depthTol, nearDelta);      // geometric discontinuity (unweighted)
                float depthEdge = depthDisc * depthW;             // per-object weighted

            #if defined(_OUTLINE_CONTROL)
                half idR = abs(eR - eC) < depthTol ? ctrlR.r : ctrlC.r;
                half idD = abs(eD - eC) < depthTol ? ctrlD.r : ctrlC.r;
                half idL = abs(eL - eC) < depthTol ? ctrlL.r : ctrlC.r;
                half idU = abs(eU - eC) < depthTol ? ctrlU.r : ctrlC.r;
                idEdge = step(0.5h / 255.0h, ctrlC.r - min(min(idR, idD), min(idL, idU)));
            #endif

                float3 nR = SagaReconstructViewNormal(uv + offR, texel);
                float3 nD = SagaReconstructViewNormal(uv + offD, texel);
                float  normalDiff = max(1.0 - saturate(dot(nC, nR)), 1.0 - saturate(dot(nC, nD)));
                float  depthDeltaAbs  = max(abs(eR - eC), abs(eD - eC));
                float  footprintDelta = max(depthDeltaAbs, max(abs(eRR - eR), abs(eDD - eD)));
                float  nearDepthEdge  = step(depthTol, footprintDelta);
                float  normalEdge = step(_NormalThreshold * thrScale, normalDiff) * (1.0 - nearDepthEdge) * normalW;
                float edge = saturate(depthEdge + normalEdge + idEdge);

                // No line here: bail before the world reconstruction and the shadow taps below, so the
                // lighting costs only on the handful of texels that actually carry one. Safe under a
                // branch -- nothing past this point takes a mip derivative (the shadow tap is a
                // comparison sample, the cloud field is procedural noise).
                if (edge <= 0.0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                // World occlusion. The cutout's dither stipple alternates prop depth and background
                // depth on ADJACENT pixels, so the kernel above fires almost everywhere inside the
                // disc and would fill it with solid ink. Mute it there.
                //
                // SagaOcclusionDisc, NOT SagaOcclusionMask: the radial term without the depth gate.
                // posWS is reconstructed from the DEPTH BUFFER, which inside the cut holds prop depth
                // on kept pixels and background depth on discarded ones -- opposite depth-gate
                // results, but identical radial values, because r is constant along a view ray under
                // ortho. The full mask would flicker between neighbouring pixels; this cannot. And
                // because the term falls to 0 at the rim, it LEAVES a line framing the hole while
                // killing the interior noise.
                //
                // Both operands are uniforms, so this is a free scalar jump when the feature is off,
                // and the duplicated world reconstruction only costs the edge texels inside the disc.
                [branch] if (_OccOutlineSuppress > 0.0 && _OccRadius > 0.001)
                {
                    edge *= 1.0 - saturate(_OccOutlineSuppress *
                                           (float)SagaOcclusionDisc(SagaCameraWorldPos(uv, eC)));
                    if (edge <= 0.0)
                        return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                half3 lineRGB = _OutlineColor.rgb;

                [branch] if (_OutlineLighting > 0.0)
                {
                    // The kernel puts the line on the NEAR side of a discontinuity (nearDelta compares
                    // neighbors AGAINST the center, so it only fires where the center is the closer
                    // surface). The center tap therefore IS the object the outline belongs to -- no
                    // search needed, and a silhouette never picks up the background's lighting.
                    float3 posWS = SagaCameraWorldPos(uv, eC);

                    // The same call SagaLitLighting reaches through GetMainLight: same cascade
                    // selection, same PCF, same distance fade. Anything cheaper and the line can
                    // disagree with the surface it borders -- which is exactly the artifact this
                    // feature exists to remove. shadowMask 1 / occlusionProbeChannels 0 = realtime
                    // only; a screen-space pass has no lightmap UVs, so baked shadows cannot reach
                    // here (see the tooltip on OutlineRenderFeature.Settings.lightingStrength).
                    half sun   = MainLightShadow(TransformWorldToShadowCoord(posWS), posWS,
                                                 half4(1.0h, 1.0h, 1.0h, 1.0h), half4(0.0h, 0.0h, 0.0h, 0.0h));
                    half cloud = SagaCloudShadow(posWS);

                    // min, not multiply -- LitLighting.hlsl:58-67. The cloud occludes the SAME sun
                    // as the shadow map, so stacking them would darken cloud-over-shadow twice and the
                    // cloud pattern would read as muddy detail inside cast shadows.
                    half shadow = min(sun, cloud);

                    // Left SMOOTH on purpose. At _BandShadow 0, SagaCelBand returns band * occ, so the
                    // surface's own response to occlusion is LINEAR and only the present blit's
                    // posterize quantizes it -- and the outline composites before that same posterize.
                    // Thresholding here would land the line on a different palette step from the
                    // surface it traces.
                    lineRGB = lerp(lineRGB, lineRGB * _OutlineShadowTint.rgb,
                                   (1.0h - shadow) * (half)_OutlineLighting);
                }

                return half4(lineRGB, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
