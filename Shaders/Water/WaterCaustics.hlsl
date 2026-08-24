#ifndef SAGA_WATER_CAUSTICS_INCLUDED
#define SAGA_WATER_CAUSTICS_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"

static const int SAGA_CAUSTIC_COUNT = 8;

// float4(dir.x, dir.z, frequencyMul, speedMul).
// Directions are a GOLDEN-ANGLE fan (137.508 deg apart) so no two ever align into a visible axis --
// this is what stops the net reading as woven. Frequencies are phi^(i/3), mutually irrational,
// spanning ~1.6 octaves. Speed signs alternate so the net churns in place instead of translating.
static const float4 SAGA_CAUSTIC_WAVE[SAGA_CAUSTIC_COUNT] =
{
    float4( 1.0000,  0.0000, 1.000,  0.80),
    float4(-0.7374,  0.6755, 1.174, -1.23),
    float4( 0.0876, -0.9962, 1.378,  0.97),
    float4( 0.6083,  0.7937, 1.618, -1.40),
    float4(-0.9847, -0.1744, 1.900,  1.13),
    float4( 0.8434, -0.5373, 2.231, -0.86),
    float4(-0.2596,  0.9657, 2.618,  1.30),
    float4(-0.4613, -0.8872, 3.074, -1.03),
};

// Low-frequency vector field used to bend the sample position, so filaments meander and branch instead
// of running straight. Two sines per axis is enough; a second full field would cost 8 more sin.
float2 SagaCausticWarp(float2 xz, float frequency)
{
    float t = _Time.y * _CausticSpeed * 0.37;   // drifts slower than the net itself
    float2 p = xz * (frequency * 0.31);         // ~3x the wavelength of the pattern

    return float2(sin(p.y + t) + 0.7 * sin(p.x * 1.43 - t),
                  sin(p.x - t) + 0.7 * sin(p.y * 1.43 + t));
}

// Ridged sum of SAGA_CAUSTIC_COUNT incommensurate directional sines. Returns [0,1], peaking on the
// zero-set of the sum -- a branching filament net.
float SagaCausticPattern(float2 xz, float sharpness)
{
    float frequency = TWO_PI * rcp(max(_CausticWaveLength, 1e-3));

    // Scaled by _CausticWaveLength so the warp stays proportional -- retuning scale won't retune warp.
    xz += SagaCausticWarp(xz, frequency) * (_CausticWarp * _CausticWaveLength);

    float h = 0.0;

    [unroll]
    for (int i = 0; i < SAGA_CAUSTIC_COUNT; i++)
    {
        float4 w = SAGA_CAUSTIC_WAVE[i];
        float k = frequency * w.z;

        // Irrational per-wave phase. Without it every wave crosses zero at the world origin at once,
        // welding a permanent bright focus to world XZ (0,0).
        float phase = frac(i * 0.6180339887) * TWO_PI;

        // k * (distance along dir + drift) => a phase velocity of _CausticSpeed * w.w metres/second,
        // so retuning _CausticWaveLength does not change how fast the net churns.
        h += sin(k * (dot(xz, w.xy) + _Time.y * (_CausticSpeed * w.w)) + phase);
    }

    // Normalize by 2*sqrt(N), not N, so h keeps a constant standard deviation (~0.354) as the wave
    // count changes -- dividing by N would wash the contrast out as waves are added. At N=4 this is
    // exactly the old rcp(4), so the count is a free knob.
    h *= rcp(2.0 * sqrt((float)SAGA_CAUSTIC_COUNT));

    // saturate() is load-bearing, not decoration: h exceeds 1 on ~0.5% of pixels at this normalizer,
    // and pow() of a negative base is NaN. Those pixels sit furthest from any filament, so clamping
    // them to 0 is the correct direction.
    return pow(saturate(1.0 - abs(h)), max(sharpness, 1e-2));
}

// columnDepth is VERTICAL metres of water (WaterRefraction.hlsl:11) -- the right measure here, because
// caustic attenuation tracks how far light travelled DOWN, not along the view ray.
half3 SagaCaustics(float3 floorWS, float columnDepth)
{
    // The caustic seen at floorWS was focused by the surface point the sun ray passed through, which
    // sits up-sun by columnDepth * L.xz/L.y. GetMainLight().direction points TOWARD the light, so
    // L.y > 0 for a sun above; the clamp keeps a near-horizon sun from launching the offset.
    float3 L = GetMainLight().direction;
    float2 xz = floorWS.xz + (L.xz * rcp(max(L.y, 0.25))) * columnDepth;

    float d01 = saturate(columnDepth * rcp(max(_CausticFullDepth, 1e-4)));

    // Per the brief's stated aesthetic: deeper => tighter AND dimmer. (Physically it is the other way
    // round; flip the two d01 terms if the inversion reads wrong on screen.)
    float sharpness = _CausticSharpness * (1.0 + _CausticDepthTighten * d01);
    float pattern = SagaCausticPattern(xz, sharpness);

    return pattern * (1.0 - d01) * _CausticStrength * _CausticColor.rgb;
}

#endif
