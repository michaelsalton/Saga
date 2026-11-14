#ifndef SAGA_STANDARD_DEPTHONLY_PASS_INCLUDED
#define SAGA_STANDARD_DEPTHONLY_PASS_INCLUDED

#pragma multi_compile_instancing

#pragma shader_feature_local_fragment _ALPHATEST_ON
// both stages: the vertex shader only ships the tangent frame when the fragment needs to march
#pragma shader_feature_local _POM_SILHOUETTE

#include "Packages/com.saltbox.saga/Shaders/Standard/StandardPOM.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    // unused by Standard; SAGA_MODIFY_POSITION_WS can displace along it, and depth must match the forward pass
    float3 normalOS : NORMAL;
#if defined(_POM_SILHOUETTE)
    float4 tangentOS : TANGENT;
#endif
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
#if defined(_POM_SILHOUETTE)
    float3 positionWS : TEXCOORD1;
    half3  normalWS : TEXCOORD2;
    half4  tangentWS : TEXCOORD3;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthVert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    SAGA_MODIFY_POSITION_WS(positionWS, IN);
    OUT.positionHCS = TransformWorldToHClip(positionWS);
    // after the hook, which may rewrite the uv along with the position
    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
#if defined(_POM_SILHOUETTE)
    // same frame the forward pass builds, so both marches start from identical varyings
    VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
    OUT.positionWS = positionWS;
    OUT.normalWS = half3(normalInputs.normalWS);
    OUT.tangentWS = half4(normalInputs.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
#endif
    return OUT;
}

half4 DepthFrag(Varyings IN, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_TARGET
{
    SAGA_ALPHA_CLIP(IN.uv);
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#if defined(_POM_SILHOUETTE)
    // without this the prepass writes depth for notches the forward pass discards, and whatever draws behind them later fails the depth test
    half faceSign = IS_FRONT_VFACE(frontFace, 1.0h, -1.0h);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
    SagaPOMSilhouetteClip(SagaStandardPOMUV(IN.uv, IN.normalWS, IN.tangentWS, viewDirWS, faceSign, ddx(IN.uv), ddy(IN.uv)));
#endif
    return 0;
}

#endif
