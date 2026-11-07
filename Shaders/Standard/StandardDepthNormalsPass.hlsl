#ifndef SAGA_STANDARD_DEPTHNORMALS_PASS_INCLUDED
#define SAGA_STANDARD_DEPTHNORMALS_PASS_INCLUDED

#pragma multi_compile_instancing

#pragma shader_feature_local_fragment _ALPHATEST_ON

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    half3  normalWS : TEXCOORD0;
    float2 uv : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
    SAGA_MODIFY_POSITION_WS(positionWS, IN);
    OUT.positionHCS = TransformWorldToHClip(positionWS);
    OUT.normalWS = half3(TransformObjectToWorldNormal(IN.normalOS));
    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
    return OUT;
}

half4 DepthNormalsFrag(Varyings IN) : SV_TARGET
{
    SagaAlphaClip(IN.uv);
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0h);
}

#endif
