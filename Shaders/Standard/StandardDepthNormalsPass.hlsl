#ifndef SAGA_STANDARD_DEPTHNORMALS_PASS_INCLUDED
#define SAGA_STANDARD_DEPTHNORMALS_PASS_INCLUDED

#pragma multi_compile_instancing

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    half3  normalWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthNormalsVert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
    OUT.normalWS = half3(TransformObjectToWorldNormal(IN.normalOS));
    return OUT;
}

half4 DepthNormalsFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0h);
}

#endif
