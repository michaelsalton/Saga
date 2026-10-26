#ifndef SAGA_STANDARD_SHADOWCASTER_PASS_INCLUDED
#define SAGA_STANDARD_SHADOWCASTER_PASS_INCLUDED

#pragma multi_compile_instancing
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

#pragma shader_feature_local_fragment _ALPHATEST_ON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 GetShadowPositionHClip(Attributes IN)
{
    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

Varyings ShadowVert(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    OUT.positionHCS = GetShadowPositionHClip(IN);
    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
    return OUT;
}

half4 ShadowFrag(Varyings IN) : SV_TARGET
{
    SagaAlphaClip(IN.uv);
    return 0;
}

#endif
