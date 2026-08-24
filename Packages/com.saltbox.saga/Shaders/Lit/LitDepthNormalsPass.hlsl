#ifndef SAGA_LIT_DEPTHNORMALS_PASS_INCLUDED
#define SAGA_LIT_DEPTHNORMALS_PASS_INCLUDED

#pragma multi_compile_instancing

// Nothing consumes DepthNormals today, and it is cut anyway: an unclipped variant of a pass that
// already exists is exactly the sort of thing that stays correct until someone turns it on.
#ifdef SAGA_OCCLUDABLE
#include "Packages/com.saltbox.saga/ShaderLibrary/WorldOcclusion.hlsl"
#endif

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
#ifdef SAGA_OCCLUDABLE
    float3 positionWS : TEXCOORD1;
#endif
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
#ifdef SAGA_OCCLUDABLE
    OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
#endif
    return OUT;
}

half4 DepthNormalsFrag(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef SAGA_OCCLUDABLE
    // Same contract as DepthOnly: clip early (nothing here is sampled), and compute coverage through
    // the exact same call so it can never drift from the forward pass.
    SagaOcclusionClip(SagaOcclusionMask(IN.positionWS), IN.positionHCS.xy);
#endif
    return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0h);
}

#endif
