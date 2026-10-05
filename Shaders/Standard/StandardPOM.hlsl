#ifndef SAGA_STANDARD_POM_INCLUDED
#define SAGA_STANDARD_POM_INCLUDED

// Parallax Occlusion Mapping Algorithm

struct SagaPOMParams
{
    float depth;
    float steps;
    float remapMin;
    float remapMax;
};

float SagaPOMHeight(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaPOMParams pom,
                    float2 uv, float2 dx, float2 dy)
{
    float raw = SAMPLE_TEXTURE2D_GRAD(heightMap, samplerHeightMap, uv, dx, dy).r;
    return saturate((raw - pom.remapMin) * rcp(max(pom.remapMax - pom.remapMin, 1e-4)));
}

float2 SagaPOMOffset(TEXTURE2D_PARAM(heightMap, samplerHeightMap), SagaPOMParams pom,
                     float2 uv, float3 vTS, float2 dx, float2 dy)
{
    float2 shear  = vTS.xy * rcp(max(abs(vTS.z), 0.15)) * pom.depth;

    int    steps  = max((int)pom.steps, 1);
    float  stepH  = rcp((float)steps);
    float2 stepUV = shear * stepH;

    float  rayH  = 1.0;
    float2 curUV = uv;
    float  curH  = SagaPOMHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), pom, curUV, dx, dy);

    float  prevRayH = rayH;
    float2 prevUV   = curUV;
    float  prevH    = curH;

    [loop]
    for (int i = 0; i < steps && curH < rayH; i++)
    {
        prevRayH = rayH;  prevUV = curUV;  prevH = curH;

        rayH  -= stepH;
        curUV -= stepUV;
        curH   = SagaPOMHeight(TEXTURE2D_ARGS(heightMap, samplerHeightMap), pom, curUV, dx, dy);
    }

    float a = prevRayH - prevH;
    float b = curH - rayH;
    return lerp(prevUV, curUV, saturate(a * rcp(max(a + b, 1e-5))));
}

#endif
