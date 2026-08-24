#ifndef SAGA_WATER_REFLECTION_INCLUDED
#define SAGA_WATER_REFLECTION_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Water/WaterInput.hlsl"
#include "Packages/com.saltbox.saga/Shaders/Water/WaterRefraction.hlsl"

// Hard ceiling on the blur radius. A blur wider than this at 27 internal px/metre is not "soft", it is a
// different image -- and the plus-shaped kernel below turns into a visible cross once its arms separate.
static const float SAGA_REFLECTION_MAX_BLUR_PX = 4.0;

// GetWorldSpaceNormalizeViewDir is projection-agnostic: per-pixel vector to the eye under perspective,
// the constant view-forward under ortho. Hardcoding the constant would be correct only under ortho,
// where every view ray is parallel.
float SagaWaterFresnel(float3 finalNormal, float3 positionWS)
{
    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    float cosTheta = saturate(dot(finalNormal, viewDirWS));
    return lerp(_ReflectionMin, _ReflectionMax, pow(1.0 - cosTheta, _FresnelPower));
}

// Screen UV -> the reflection RT's own UV, carrying the runtime V-flip.
float2 SagaReflectionUV(float2 screenUV)
{
    return screenUV * _WaterReflectionScaleBias.xy + _WaterReflectionScaleBias.zw;
}

// METRES ALONG THE REFLECTED RAY from this fragment to whatever it reflects. Exact, and it needs no trig.
//
// The mirror camera shares the main camera's projection and its view matrix is mainView * R, so an object Q
// reached at distance d along the reflected ray has its mirror image at exactly P + d * viewForward. Under
// ortho eye depth advances 1:1 along viewForward, so the mirror's eye depth of Q exceeds the main camera's
// eye depth of P by exactly d -- for any camera pitch and any yaw. Same subtraction as columnEye at
// WaterRefraction.hlsl:52, just against the mirror instead of the scene.
//
// 0 means SKY. Nothing was drawn there, so there is no reflected object to be far away, and fading it would
// empty the open water instead of washing distant clutter -- the opposite of the intent. Returning 0 keeps
// the sky fully reflective. It is also the graceful-degradation path: with the depth feature absent the
// texture reads 0 everywhere and every _ReflectionFar* dial becomes a no-op.
float SagaReflectionPathLength(float2 screenUV, float surfaceEye)
{
    if (_WaterReflectionDepthValid < 0.5) return 0.0;

    float reflEye = SAMPLE_TEXTURE2D(_WaterReflectionDepthTex, sampler_PointClamp,
                                     SagaReflectionUV(screenUV)).r;
    if (reflEye <= 0.0) return 0.0;

    return max(reflEye - surfaceEye, 0.0);
}

// 0 at and below _ReflectionFadeStart, 1 once _ReflectionFadeDistance further out. One ramp drives all four
// far-field dials, so they cannot disagree about where "far" begins.
float SagaReflectionDistance01(float pathLength)
{
    return saturate((pathLength - _ReflectionFadeStart) * rcp(max(_ReflectionFadeDistance, 1e-3)));
}

// Five POINT taps on a plus, at a WHOLE-TEXEL radius.
//
// Not a mip, deliberately. The reflection RT is FilterMode.Point with useMipMap off (WaterReflection.cs) and
// the stack is point-sampled end to end; a trilinear mip fetch lands between internal pixels and reads as
// smeared rather than blurred, which is the same mistake as filtering a pixel-art sprite. Quantizing the
// radius keeps every tap on the internal grid. At radius 0 all five taps coincide, so this returns exactly
// the single-tap result -- the near field is bit-identical to the pre-amendment sample.
half3 SagaSampleReflection(float2 screenUV, float radiusPx)
{
    float2 rtUV = SagaReflectionUV(screenUV);
    float r = floor(min(radiusPx, SAGA_REFLECTION_MAX_BLUR_PX) + 0.5);
    float2 d = r * _WaterReflectionTexelSize.xy;

    half3 sum = SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_PointClamp, saturate(rtUV)).rgb;
    sum += SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_PointClamp, saturate(rtUV + float2( d.x, 0.0))).rgb;
    sum += SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_PointClamp, saturate(rtUV - float2( d.x, 0.0))).rgb;
    sum += SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_PointClamp, saturate(rtUV + float2( 0.0, d.y))).rgb;
    sum += SAMPLE_TEXTURE2D(_WaterReflectionTex, sampler_PointClamp, saturate(rtUV - float2( 0.0, d.y))).rgb;

    return sum * (1.0 / 5.0);
}

// The reflection colour, plus the weight multiplier the Fresnel blend should be scaled by.
half3 SagaWaterReflection(float2 screenUV, float3 finalNormal, float surfaceEye, out float distanceWeight)
{
    // Same tilt calibration as refraction, opposite sign -- the two terms must not slide as one sheet.
    float2 baseOffset = SagaSurfaceScreenOffset(finalNormal, _ReflectionDistortion)
                      * SagaRefractionEdgeFade(screenUV);

    // Chicken-and-egg: the distortion amount depends on the distance, and the distance is read at the UV the
    // distortion produces. Resolved exactly the way refraction resolves the same loop
    // (WaterRefraction.hlsl:56-59) -- probe at the near-field offset, then re-offset with what it reported.
    // One extra tap, and being off by a few texels only mis-scales a distortion nobody can measure.
    float probePath = SagaReflectionPathLength(saturate(screenUV - baseOffset), surfaceEye);
    float t = SagaReflectionDistance01(probePath);

    float2 uv = saturate(screenUV - baseOffset * lerp(1.0, _ReflectionFarDistortion, t));

    half3 reflection = SagaSampleReflection(uv, t * _ReflectionFarBlur);

    // Wash distant content into the mirror's OWN sky colour rather than toward the water. This is the lead
    // dial: real water desaturates its reflection toward the horizon colour with distance, and because the
    // mirror clears to a flat skyColor (08 decision 4) that colour is exactly what a fully-washed pixel
    // would have contained anyway. Dropping the WEIGHT instead reveals the depth colour underneath and
    // reads as "the water stopped being reflective" -- available below, but as the smaller second lever.
    reflection = lerp(reflection, _WaterReflectionSkyColor.rgb, t * _ReflectionFarSky);

    distanceWeight = lerp(1.0, _ReflectionFarOpacity, t);
    return reflection;
}

#endif
