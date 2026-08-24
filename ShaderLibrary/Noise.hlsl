#ifndef SAGA_NOISE_INCLUDED
#define SAGA_NOISE_INCLUDED

float SagaHash21(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float SagaValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = p - i;
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float a = SagaHash21(i);
    float b = SagaHash21(i + float2(1.0, 0.0));
    float c = SagaHash21(i + float2(0.0, 1.0));
    float d = SagaHash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 2.0 - 1.0;
}

static const float2x2 SAGA_FBM_ROT = float2x2(0.8, -0.6, 0.6, 0.8);

float SagaFbm(float2 p, int octaves, float gain)
{
    float sum = 0.0;
    float amp = 1.0;
    float norm = 0.0;

    [loop]
    for (int i = 0; i < octaves; i++)
    {
        sum  += amp * SagaValueNoise(p);
        norm += amp;
        amp  *= gain;
        p = mul(SAGA_FBM_ROT, p) * 2.0;
    }

    return sum * rcp(max(norm, 1e-4));
}

#endif
