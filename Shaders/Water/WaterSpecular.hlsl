#ifndef SAGA_WATER_SPECULAR_INCLUDED
#define SAGA_WATER_SPECULAR_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Water/WaterRefraction.hlsl"

// Sun sparkle: a thresholded slope term on a wave normal that has been given HIGH-FREQUENCY DETAIL.
//
// ANCHORED ON THE SUN, NOT ON A HALF-VECTOR. Step 09 shipped this as Blinn-Phong, H = normalize(L + V),
// on the reasoning that ortho + directional makes H a scene constant. It does -- but it is a constant
// that CHANGES EVERY TIME THE CAMERA TURNS, and IsometricCamera yaws freely at a locked 30 deg pitch.
// Across the eight snapped headings |H.xz| swung 0.23 .. 0.76, which moved the sparkles, thinned them,
// and around yaw 135 extinguished them outright. A specular lobe is view-dependent by definition, so
// no amount of normalization fixes that; the drive axis itself has to stop moving. L is fixed by the
// sun alone, so |L.xz| = 0.6428 and L.y = 0.766 are genuine constants and the field reads identically
// from every heading. See 09_SpecularHighlights.md, Amendment 2.

static const float SAGA_SPEC_MIN_AXIS_XZ = 0.15;  // guards a near-zenith sun, where axis -> up and reach -> 0
static const float SAGA_SPEC_SUN_GATE = 8.0;      // glint reaches full once the sun clears ~7 deg

static const int SAGA_SPARKLE_COUNT = 6;

// float4(dir.x, dir.z, frequencyMul, speedMul). Golden-angle direction fan and mutually irrational
// frequencies, same idiom and for the same reason as SAGA_CAUSTIC_WAVE (WaterCaustics.hlsl:12-22): no two
// directions ever align into a visible axis, which is exactly what the hashed cell grid failed to avoid.
static const float4 SAGA_SPARKLE_WAVE[SAGA_SPARKLE_COUNT] =
{
    float4( 1.0000,  0.0000, 1.000,  0.90),
    float4(-0.7374,  0.6755, 1.212, -1.17),
    float4( 0.0876, -0.9962, 1.469,  1.05),
    float4( 0.6083,  0.7937, 1.780, -1.31),
    float4(-0.9847, -0.1744, 2.157,  1.22),
    float4( 0.8434, -0.5373, 2.614, -0.94),
};

// The drive axis and the two derived quantities everything else needs. All three are scene constants.
struct SagaSpecGeometry
{
    float3 axis;       // the direction a slope must lean toward to glint. The sun -- no view term
    float2 axisDirXZ;  // its horizontal direction: the only direction a (horizontal) slope can reach
    float invRefTilt;  // scales a slope into multiples of typical wave slope
};

SagaSpecGeometry SagaWaterSpecGeometry()
{
    float3 axis = GetMainLight().direction;

    // |axis.xz| is 0.69 at a 45 deg sun and ~0.99 near the horizon. Folding it into the normalization is
    // what keeps the knobs put when the SUN is re-aimed instead of silently retuning the sparkle density.
    float axisXZ = length(axis.xz);

    SagaSpecGeometry g;
    g.axis = axis;

    // Two uses, two separate guards, deliberately. The near-zenith clamp belongs ONLY on the
    // normalization -- feeding a clamped length into the direction as well (as this did through 09)
    // leaves axisDirXZ SHORTER than unit whenever the guard trips, silently scaling the micro-normal
    // down with it, so the term fades out just as the geometry that needs the guard arrives.
    g.axisDirXZ = axis.xz * rcp(max(axisXZ, 1e-5));
    g.invRefTilt = rcp(SAGA_REFRACTION_REF_TILT * max(axisXZ, SAGA_SPEC_MIN_AXIS_XZ));
    return g;
}

// Wave slope projected onto the sun axis, in multiples of typical wave slope. The metre-scale bias.
float SagaWaterSpecDrive(float3 finalNormal)
{
    SagaSpecGeometry g = SagaWaterSpecGeometry();

    // The HORIZONTAL projection, not the re-based dot. 09 used dot(N, axis) - axis.y, which expands to
    // dot(N.xz, axis.xz) + (N.y - 1) * axis.y. Only the first term is horizontal and only the first term
    // is what invRefTilt's 1/|axis.xz| cancels; the second survives as a negative bias of
    // |N.xz|^2 * axis.y / (2 * REF_TILT * |axis.xz|). Under the old half-vector that ratio ran 0.85 to
    // 4.27 with camera yaw -- worth -3.3 of drive on a 0.3 slope at the worst heading, and worst
    // precisely ON THE CRESTS, where the sparkles are supposed to live. Dropping it makes the
    // normalization exact rather than first-order, and costs one op less.
    return dot(finalNormal.xz, g.axis.xz) * g.invRefTilt;
}

// Slope of the micro-normal field, projected onto the same axis and in the same units, so it adds
// straight onto the drive. THIS is where the pixel-scale frequency comes from.
float SagaWaterSparkleDetail(float2 xz, float2 axisDirXZ)
{
    float k0 = TWO_PI * rcp(max(_SparkleWaveLength, 1e-3));
    float sum = 0.0;
    float norm = 0.0;

    [unroll]
    for (int i = 0; i < SAGA_SPARKLE_COUNT; i++)
    {
        float4 w = SAGA_SPARKLE_WAVE[i];
        float k = k0 * w.z;

        // Irrational per-wave phase, or every wave crosses zero at the world origin at once and welds a
        // permanent feature to world XZ (0,0) -- see WaterCaustics.hlsl:54.
        float phase = frac(i * 0.6180339887) * TWO_PI;

        // Only the component along axisDirXZ reaches the drive axis.
        float p = dot(w.xy, axisDirXZ);

        // Slope of cos(k * s) is -sin(k * s) * k. k is dropped from the amplitude on purpose:
        // _SparkleStrength is denominated in slope directly, so retuning _SparkleWaveLength changes
        // feature SIZE without changing contrast. Drift is inside the k multiply, so _SparkleSpeed
        // stays in metres/second across wavelengths.
        sum += -sin(k * (dot(xz, w.xy) + _Time.y * (_SparkleSpeed * w.w)) + phase) * p;
        norm += p * p;
    }

    // Constant standard deviation regardless of WHICH WAY the axis points, not merely of how many waves
    // there are (WaterCaustics.hlsl:64 only needed the latter). The golden-angle fan is even mod 360 but
    // NOT mod 180 -- its directions land at 0, 10, 52.5, 95, 137.5, 147.5 deg mod 180 -- so sum(p*p)
    // swings 2.29 .. 3.72 as the axis rotates. That is a 27% swing in sigma, and through a hard cutoff
    // sitting ~2 sigma out it is a several-fold change in sparkle density. sum(p*p) averages 3.0, where
    // rsqrt(2 * norm) is exactly rcp(sqrt(SAGA_SPARKLE_COUNT)) -- so _SparkleStrength keeps its meaning.
    // Now that the axis is the sun this is uniform and hoisted, but it is what makes the density hold
    // still when the sun is re-aimed, which is the remaining reason the term needs it.
    return sum * rsqrt(max(2.0 * norm, 1e-4)) * _SparkleStrength;
}

float SagaWaterSparkleDrive(float3 finalNormal, float3 positionWS)
{
    SagaSpecGeometry g = SagaWaterSpecGeometry();

    float waveDrive = dot(finalNormal.xz, g.axis.xz) * g.invRefTilt;
    float detail = SagaWaterSparkleDetail(positionWS.xz, g.axisDirXZ);

    // Summed, not multiplied, and thresholded ONCE. A detail peak lights only where the wave slope is
    // already leaning toward the sun, so density follows the big waves for free -- no separate envelope.
    return waveDrive + detail;
}

half3 SagaWaterSpecular(float3 finalNormal, float3 positionWS)
{
    float drive = SagaWaterSparkleDrive(finalNormal, positionWS);

    // Cutoff, then clamp, then power -- in that order. The cutoff+clamp is what makes the exponent usable:
    // it maps the slice of the drive ABOVE _SparkleCutoff onto a full [0,1], so pow() has a real domain.
    // The exponent is NOT redundant with the cutoff here, because this output is continuous rather than
    // binary -- it shrinks the lit area toward the peaks, giving an intense core with a 1-2 step halo.
    float t = saturate((drive - _SparkleCutoff) * rcp(max(_SparkleFalloff, 1e-4)));
    float glint = pow(t, max(_SparklePower, 1e-3));

    // No glint with the sun below the surface. Hard ramp, not a smooth N.L: a smooth factor would fade the
    // sparkles through the dim steps the posterize cannot resolve. Saturated to 1.0 at the live sun.
    glint *= saturate(dot(finalNormal, GetMainLight().direction) * SAGA_SPEC_SUN_GATE);

    // Authored color, NOT GetMainLight().color -- nothing else in this shader is lit by the main light,
    // and that uniform carries the light's intensity (2 live), which is a rig decision, not an art one.
    return glint * _SpecIntensity * _SpecColor.rgb;
}

#endif // SAGA_WATER_SPECULAR_INCLUDED
