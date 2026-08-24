#ifndef SAGA_LIT_DEPTHONLY_PASS_INCLUDED
#define SAGA_LIT_DEPTHONLY_PASS_INCLUDED

#pragma multi_compile_instancing

// Only shaders that opt in (Saga/Lit) pull this in. Saga/Ground compiles this same file without the
// define and keeps its original interpolator count exactly.
#ifdef SAGA_OCCLUDABLE
#include "Packages/com.saltbox.saga/ShaderLibrary/WorldOcclusion.hlsl"
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
#ifdef SAGA_OCCLUDABLE
    float3 positionWS : TEXCOORD0;
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
    OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
#ifdef SAGA_OCCLUDABLE
    OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
#endif
    return OUT;
}

half4 DepthFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef SAGA_OCCLUDABLE
    SagaOcclusionClip(SagaOcclusionMask(IN.positionWS), IN.positionHCS.xy);
#endif
    return 0;
}

#endif
