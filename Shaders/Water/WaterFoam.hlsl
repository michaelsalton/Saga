#ifndef SAGA_WATER_FOAM_INCLUDED
#define SAGA_WATER_FOAM_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"

static const int   SAGA_FOAM_TAP_COUNT     = 24; // number of probes per pixel
static const float SAGA_FOAM_SEARCH_MARGIN = 1.3;
static const float SAGA_FOAM_GOLDEN_ANGLE  = 2.39996323; // standard angle for a Vodel spiral

static const int SAGA_FOAM_NOISE_COUNT = 6; // number of terms in sum-of-sines noise

// the sine waves used to determine the wave pattern
static const float4 SAGA_FOAM_WAVE[SAGA_FOAM_NOISE_COUNT] =
{
    float4( 1.0000,  0.0000, 1.000,  0.90),
    float4(-0.7374,  0.6755, 1.212, -1.17),
    float4( 0.0876, -0.9962, 1.469,  1.05),
    float4( 0.6083,  0.7937, 1.780, -1.31),
    float4(-0.9847, -0.1744, 2.157,  1.22),
    float4( 0.8434, -0.5373, 2.614, -0.94),
};

// world meters to screen-UV conversion used for the tap loop
struct SagaFoamBasis
{
    float2 pxPerMetreX;
    float2 pxPerMetreY;
    float2 uvPerPixel;
};

// builds and returns the conversion factors
SagaFoamBasis SagaFoamBasisFromRestPlane(float3 restPositionWS)
{
    float2 dPdx = ddx(restPositionWS).xz;
    float2 dPdy = ddy(restPositionWS).xz;

    SagaFoamBasis b;
    b.pxPerMetreX = dPdx * rcp(max(dot(dPdx, dPdx), 1e-12));
    b.pxPerMetreY = dPdy * rcp(max(dot(dPdy, dPdy), 1e-12));
    b.uvPerPixel  = rcp(GetScaledScreenParams().xy);
    return b;
}

// define the foam placement
float SagaFoamCoverage(SagaFoamBasis b, float2 screenUV, float2 originXZ,
                       float restHeight, float edgeOffset)
{
    float width  = max(_FoamWidth, 1e-4);
    float inner  = max(width - _FoamSoftness, 0.0);
    float outer  = max(width, inner + 1e-4);
    float radius = width * SAGA_FOAM_SEARCH_MARGIN;
    float pierce = max(_FoamPierce, 1e-4);

    float coverage = 0.0;

    [unroll]
    for (int i = 0; i < SAGA_FOAM_TAP_COUNT; i++)
    {
        float a = i * SAGA_FOAM_GOLDEN_ANGLE;

        // Spread the taps evenly by area over the search disc. Vogel spiral
        float  r = radius * sqrt((i + 0.5) * rcp((float)SAGA_FOAM_TAP_COUNT));
        float2 D = float2(cos(a), sin(a)) * r;
        float2 uv = screenUV + float2(dot(D, b.pxPerMetreX), dot(D, b.pxPerMetreY)) * b.uvPerPixel;
        float3 hitWS = ComputeWorldSpacePosition(uv, SagaSampleRawDepth(uv), UNITY_MATRIX_I_VP);

        float above = smoothstep(0.0, pierce, hitWS.y - restHeight);
        float dist = length(hitWS.xz - originXZ) + edgeOffset;
        float band = 1.0 - smoothstep(inner, outer, dist);

        float onScreen = all(uv == saturate(uv)) ? 1.0 : 0.0;

        coverage = max(coverage, above * band * onScreen);
    }

    return coverage;
}

// calculate the foam noise
float SagaFoamNoise(float2 xz)
{
    float k0   = TWO_PI * rcp(max(_FoamNoiseSize, 1e-3));
    float sum  = 0.0;
    float norm = 0.0;

    [unroll]
    for (int i = 0; i < SAGA_FOAM_NOISE_COUNT; i++)
    {
        float4 w   = SAGA_FOAM_WAVE[i];
        float  k   = k0 * w.z;
        float  amp = rcp(w.z);

        float phase = frac(i * 0.6180339887) * TWO_PI;

        sum  += amp * sin(k * (dot(xz, w.xy) + _Time.y * (_FoamNoiseSpeed * w.w)) + phase);
        norm += amp * amp;
    }

    return sum * rcp(2.0 * sqrt(norm));
}

// function called in Water shader
half SagaWaterFoam(float2 screenUV, float3 positionWS, float3 restPositionWS)
{
    SagaFoamBasis basis = SagaFoamBasisFromRestPlane(restPositionWS);

    [branch] if (_FoamOpacity <= 0.0)
        return 0.0h;

    float edgeOffset = SagaFoamNoise(restPositionWS.xz) * _FoamNoiseAmount;

    return (half)SagaFoamCoverage(basis, screenUV, positionWS.xz, restPositionWS.y, edgeOffset);
}

#endif
