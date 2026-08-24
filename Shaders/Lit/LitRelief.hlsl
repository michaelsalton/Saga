#ifndef SAGA_LIT_RELIEF_INCLUDED
#define SAGA_LIT_RELIEF_INCLUDED

// Parallax Occlusion Mapping Algorithm

struct SagaReliefParams
{
    float depth;
    float steps;
    float remapMin;
    float remapMax;
};

float SagaReliefHeight(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaReliefParams relief,
                       float2 uv, float2 dx, float2 dy)
{
    float raw = SAMPLE_TEXTURE2D_GRAD(heightMap, samplerHeightMap, uv, dx, dy).r;
    return saturate((raw - relief.remapMin) * rcp(max(relief.remapMax - relief.remapMin, 1e-4)));
}

float2 SagaReliefOffset(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaReliefParams relief,
                        float2 uv, float3 vTS, float2 dx, float2 dy)
{
    float2 shear  = vTS.xy * rcp(max(abs(vTS.z), 0.15)) * relief.depth;

    int    steps  = max((int)relief.steps, 1);
    float  stepH  = rcp((float)steps);
    float2 stepUV = shear * stepH;

    float  rayH  = 1.0;
    float2 curUV = uv;
    float  curH  = SagaReliefHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), relief, curUV, dx, dy);

    float  prevRayH = rayH;
    float2 prevUV   = curUV;
    float  prevH    = curH;

    [loop]
    for (int i = 0; i < steps && curH < rayH; i++)
    {
        prevRayH = rayH;  prevUV = curUV;  prevH = curH;

        rayH  -= stepH;
        curUV -= stepUV;
        curH   = SagaReliefHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), relief, curUV, dx, dy);
    }

    float a = prevRayH - prevH;
    float b = curH - rayH;
    return lerp(prevUV, curUV, saturate(a * rcp(max(a + b, 1e-5))));
}

#endif
