#ifndef SAGA_GLASS_REFRACTION_INCLUDED
#define SAGA_GLASS_REFRACTION_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"

static const float SAGA_GLASS_CLAMP_RANGE = 0.15;

struct SagaGlassRefraction
{
    half3 color;       // scene colour behind the glass, already absorbed
    float2 uv;         // where it was actually sampled from
    float surfaceEye;  // eye depth of the glass surface itself
};

float2 SagaGlassRefractionOffset(float3 normalWS)
{
    float2 dirVS = TransformWorldToViewDir(normalWS).xy;
    float2 offsetPx = -dirVS * _RefractionDistortion;
    return offsetPx * rcp(GetScaledScreenParams().xy);
}

float SagaGlassRefractionEdgeFade(float2 screenUV)
{
    float2 f = smoothstep(0.0, max(_RefractionEdgeFade, 1e-4), min(screenUV, 1.0 - screenUV));
    return min(f.x, f.y);
}

SagaGlassRefraction SagaGlassRefract(float2 screenUV, float surfaceRawDepth, float3 normalWS,
                                     half3 transmittance)
{
    SagaGlassRefraction o;
    o.surfaceEye = SagaOrthoEyeDepth(surfaceRawDepth);

    float2 offset = SagaGlassRefractionOffset(normalWS) * SagaGlassRefractionEdgeFade(screenUV);

    float probeEye = SagaOrthoEyeDepth(SagaSampleRawDepth(screenUV + offset));
    offset *= saturate((probeEye - o.surfaceEye) * rcp(SAGA_GLASS_CLAMP_RANGE));

    o.uv = screenUV + offset;
    // Not SampleSceneColor: _CameraOpaqueTexture predates the transparent queue, so it has no water, grass or outline in it
    o.color = SAMPLE_TEXTURE2D_X(_SagaSceneColor, sampler_SagaSceneColor, o.uv).rgb * transmittance;

    return o;
}

#endif // SAGA_GLASS_REFRACTION_INCLUDED
