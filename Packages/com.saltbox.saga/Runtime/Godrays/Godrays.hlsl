#ifndef SAGA_GODRAYS_INCLUDED
#define SAGA_GODRAYS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/Depth.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/CameraBasis.hlsl"
#include "Packages/com.saltbox.saga/ShaderLibrary/CloudShadows.hlsl"

float _GodrayIntensity;      // master scale. 0 = off (exact). ALSO absorbs the constant phase term.
float _GodrayDensity;        // mist EXTINCTION per metre, UNIFORM through the slab (not a brightness scale)
float _MistFloor;            // world Y of the slab FLOOR -- bottom of the volume
float _MistThickness;        // world metres from the floor up to the slab CEILING
float _MistFadeTop;          // metres below the ceiling over which density eases to 0. 0 = hard lid.
float _GodrayShadowStrength; // 0 = clouds only, 1 = full geometry occlusion
float _GodraySteps;          // march steps across the span
float _GodrayMaxSpan;        // hard cap on metres of air integrated (near-horizontal ray guard)

// The camera basis and the ray / world-position reconstruction moved to ShaderLibrary/CameraBasis.hlsl when
// the outline became the second consumer -- the promotion decision 7 of the plan said to make once that
// happened. Everything that used to be argued here (ortho half-extents from C# rather than
// unity_OrthoParams, origin from _WorldSpaceCameraPos rather than the grid-snapped transform) moved with
// it, unchanged. GodrayPass still publishes the basis itself; it may not assume the outline ran first.
//
// Kept as thin wrappers so the march below reads exactly as it did.
void SagaGodrayRay(float2 uv, out float3 origin, out float3 dir)
{
    SagaCameraRay(uv, origin, dir);
}

float3 SagaGodrayWorldPos(float2 uv, float eyeDepth)
{
    return SagaCameraWorldPos(uv, eyeDepth);
}

// Mist density: FLAT at 1 through the slab, easing to 0 only across the top _MistFadeTop metres.
//
// The flat part is the point -- the shaft is equally dense everywhere you can actually see it. But the
// slab ceiling is a hard boundary in 3D: a lit column of air stops dead where it crosses that plane, and
// with a perfectly flat profile that projects to a hard edge across the shaft tops. Grading the WHOLE
// slab (an exponential, or a taper over the full thickness) fixes the edge but makes the shaft visibly
// weaker toward the top, which is not what is wanted. Confining the fade to a band just under the
// ceiling gives both: uniform mist along the shaft, and no lid.
//
// Set _MistFadeTop to 0 for the hard lid back.
half SagaMistDensity(float3 p)
{
    float ceilY = _MistFloor + max(_MistThickness, 0.01);
    float below = ceilY - p.y;                      // metres below the ceiling
    float fade  = max(_MistFadeTop, 1e-4);
    return (half)smoothstep(0.0, 1.0, saturate(below * rcp(fade)));
}

half3 SagaGodrays(float2 uv)
{
    [branch] if (_GodrayIntensity < 0.0001)
        return (half3)0.0h;

    float3 rayO, rayD;
    SagaGodrayRay(uv, rayO, rayD);

    // -----------------------------------------------------------------------------------------------
    // The integration volume is a WORLD-ANCHORED horizontal slab, y in [floor, floor + thickness], and
    // the depth buffer only CLIPS it. This is what pins the shafts to a place in the world.
    //
    // The previous form anchored the slab to the depth buffer instead (end = the surface, start = end -
    // marchLength). Two things followed, and both made the shafts read as unanchored: the slab became a
    // shell that bent and stepped with the terrain, so a shaft had no consistent base or top; and every
    // pixel whose depth exceeded the distance cap -- including ALL sky pixels, where depth is the far
    // plane -- fell back to a plane at a fixed distance from the CAMERA, so those shafts translated
    // rigidly with the view while the rest stayed put.
    //
    // ORTHO consequence worth knowing: every pixel shares one ray direction, so every UNCLIPPED ray
    // crosses exactly the same span, _MistThickness / |_SagaCamFwd.y|. There is no grazing-ray case
    // here -- span varies only where geometry clips it. All shaft structure comes from the cloud field
    // and the shadow map varying ALONG the ray, never from the span.
    // -----------------------------------------------------------------------------------------------
    float dy = rayD.y;

    // Guard a near-horizontal ray WITHOUT flipping its sign; the span cap below bounds the result.
    float safeDy = (abs(dy) < 1e-4) ? ((dy >= 0.0) ? 1e-4 : -1e-4) : dy;
    float invDy  = rcp(safeDy);

    float tCeil  = (_MistFloor + max(_MistThickness, 0.01) - rayO.y) * invDy;
    float tFloor = (_MistFloor                             - rayO.y) * invDy;

    float eyeDepth = SagaOrthoEyeDepth(SagaSampleRawDepth(uv));

    float start = max(_ProjectionParams.y, min(tCeil, tFloor));
    float end   = min(eyeDepth,            max(tCeil, tFloor));

    // A ray almost parallel to the slab would otherwise cross unbounded air, blowing up both the
    // brightness and the step size.
    end = min(end, start + max(_GodrayMaxSpan, 0.01));

    float span = end - start;

    // Now also the early-out wherever the slab is fully occluded or fully behind the camera: geometry
    // poking above the ceiling, or the whole slab out of view. Geometry BELOW the floor does not land
    // here -- those rays still cross the full slab, which is correct.
    [branch] if (span <= 0.0)
        return (half3)0.0h;

    int   n  = (int)max(_GodraySteps, 1.0);
    float dt = span * rcp((float)n);

    // Beer-Lambert single scatter. accum is BOUNDED BY 1: what a step can contribute is capped by the
    // light still left after the mist IN FRONT of it, so the sum cannot exceed 1 however thick the slab
    // or however clear the sky. The old form summed vis*density*dt raw -- unbounded, with _MistThickness
    // / _GodrayDensity / _GodrayIntensity as separate un-clamped multipliers on brightness. Dropping
    // coverage to 0 took every vis to 1 and the term to its maximum everywhere, which on a Blend One One
    // pass is a full-frame white-out rather than a shaft.
    //
    float accum = 0.0;   // in-scattered fraction of the light reaching the eye
    float trans = 1.0;   // transmittance from the eye to the current sample

    [loop]
    for (int i = 0; i < n; i++)
    {
        float3 p = rayO + rayD * (start + ((float)i + 0.5) * dt);
        half vis = 1.0h - SagaCloudDensity(p);

        [branch] if (_GodrayShadowStrength > 0.001)
        {
            half g = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
            vis *= lerp(1.0h, g, (half)_GodrayShadowStrength);
        }

        float a = 1.0 - exp(-_GodrayDensity * (float)SagaMistDensity(p) * dt);
        float w = trans * a;

        accum += w * (float)vis;
        trans -= w;
    }

    // accum is in [0,1), so _GodrayIntensity * _MainLightColor is now a HARD CEILING on what this pass
    // can add to a pixel. Tune intensity at coverage 0 -- clear sky is the worst case, since every
    // sample is lit; any cloud only ever reduces the term.
    return (half3)(accum * _GodrayIntensity) * _MainLightColor.rgb;
}

#endif
