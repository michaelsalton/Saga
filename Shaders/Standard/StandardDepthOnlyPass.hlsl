#ifndef SAGA_STANDARD_DEPTHONLY_PASS_INCLUDED
#define SAGA_STANDARD_DEPTHONLY_PASS_INCLUDED

#pragma multi_compile_instancing

#pragma shader_feature_local_fragment _ALPHATEST_ON

struct Attributes
{
    float4 positionOS : POSITION;
    // unused by Standard; SAGA_MODIFY_POSITION_WS can displace along it, and depth must match the forward pass
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthVert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    SAGA_MODIFY_POSITION_WS(positionWS, IN);
    OUT.positionHCS = TransformWorldToHClip(positionWS);
    return OUT;
}

half4 DepthFrag(Varyings IN) : SV_TARGET
{
    SagaAlphaClip(IN.uv);
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    return 0;
}

#endif
