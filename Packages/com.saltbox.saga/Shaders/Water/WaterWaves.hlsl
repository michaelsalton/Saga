#ifndef SAGA_WATER_WAVES_INCLUDED
#define SAGA_WATER_WAVES_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"

static const int SAGA_WAVE_COUNT = 4;

struct SagaWave
{
    float2 center;
    float waveLength;
    float amplitude;
    float speed;
    float steepness;
};

struct SagaWaveResult
{
    float3 position;
    float3 normal;
    float3 binormal;
    float3 tangent;
};

static const float4 SAGA_WAVE_SHAPE[SAGA_WAVE_COUNT] =
{
    float4(-12.0, -10.0, 14.0, 0.55),
    float4(15.0, 6.0, 9.0, 0.45),
    float4(-4.0, 14.0, 5.5, 0.35),
    float4(8.0, -16.0, 3.5, 0.30),
};

static const float SAGA_WAVE_LENGTH_TOTAL = 32.0;
static const float SAGA_WAVE_LENGTH_MAX = 14.0;

SagaWave SagaGetWave(int i)
{
    float4 s = SAGA_WAVE_SHAPE[i];

    SagaWave w;
    w.center = s.xy;
    w.waveLength = max(s.z, 1e-3);
    w.steepness = saturate(s.w) * saturate(_WaveAmplitude / 1.5);

    w.amplitude = _WaveAmplitude * (w.waveLength / SAGA_WAVE_LENGTH_TOTAL);
    w.speed = _WaveSpeed * sqrt(w.waveLength / SAGA_WAVE_LENGTH_MAX);

    return w;
}

float SagaWaveEdgeDampen(float2 uv1)
{
    float2 d = min(uv1, 1.0 - uv1);
    float edge01 = saturate(min(d.x, d.y) / max(_EdgeDampenWidth, 1e-4));
    edge01 = edge01 * edge01 * (3.0 - 2.0 * edge01);
    return lerp(1.0, edge01, _DampeningFactor);
}

SagaWaveResult SagaCalculateWaves(float3 wavePosition, float edgeDampen)
{
    float2 dirs[SAGA_WAVE_COUNT];
    float dists[SAGA_WAVE_COUNT];
    float fades[SAGA_WAVE_COUNT];
    float3 offset = 0;

    [unroll]
    for (int i = 0; i < SAGA_WAVE_COUNT; i++)
    {
        SagaWave wave = SagaGetWave(i);

        float2 toVertex = wavePosition.xz - wave.center;
        float dist = length(toVertex);
        dirs[i] = toVertex / max(dist, 1e-4);
        dists[i] = dist;
        fades[i] = saturate(dist / max(wave.waveLength * 0.5, 1e-4));

        float frequency = 2.0 / wave.waveLength;
        float phaseConstant = wave.speed * frequency;
        float qi = wave.steepness / max(wave.amplitude * frequency * SAGA_WAVE_COUNT, 1e-4);
        float damp = fades[i] * edgeDampen;

        float rad = frequency * dist - _Time.y * phaseConstant;
        float sinR = sin(rad);
        float cosR = cos(rad);

        offset.x += qi * wave.amplitude * dirs[i].x * cosR * damp;
        offset.z += qi * wave.amplitude * dirs[i].y * cosR * damp;
        offset.y += wave.amplitude * sinR * damp;
    }

    float3 displaced = wavePosition + offset;

    float3 nTerm = 0;
    float3 bTerm = 0;
    float3 tTerm = 0;

    [unroll]
    for (int i = 0; i < SAGA_WAVE_COUNT; i++)
    {
        SagaWave wave = SagaGetWave(i);
        float2 dir = dirs[i];

        float frequency = 2.0 / wave.waveLength;
        float phaseConstant = wave.speed * frequency;
        float qi = wave.steepness / max(wave.amplitude * frequency * SAGA_WAVE_COUNT, 1e-4);
        float wa = frequency * wave.amplitude * fades[i] * edgeDampen;

        float radN = frequency * dists[i] - _Time.y * phaseConstant;
        float sinN = sin(radN);
        float cosN = cos(radN);

        bTerm += float3(qi * dir.x * dir.x * wa * sinN, dir.x * wa * cosN,
                        qi * dir.x * dir.y * wa * sinN);
        tTerm += float3(qi * dir.x * dir.y * wa * sinN, dir.y * wa * cosN,
                        qi * dir.y * dir.y * wa * sinN);
        nTerm += float3(dir.x * wa * cosN, qi * wa * sinN, dir.y * wa * cosN);
    }

    SagaWaveResult result;
    result.position = displaced;
    result.binormal = normalize(float3(1.0 - bTerm.x, bTerm.y, -bTerm.z));
    result.tangent = normalize(float3(-tTerm.x, tTerm.y, 1.0 - tTerm.z));
    result.normal = normalize(float3(-nTerm.x, 1.0 - nTerm.y, -nTerm.z));
    return result;
}

#endif
