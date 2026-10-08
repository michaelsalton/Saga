#ifndef SAGA_STANDARD_DEPTHONLY_PASS_INCLUDED
#define SAGA_STANDARD_DEPTHONLY_PASS_INCLUDED

#pragma multi_compile_instancing

struct Attributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthVert(Attributes IN)
{
    Varyings OUT = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
    return OUT;
}

half4 DepthFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
    return 0;
}

#endif
