#ifndef SAGA_GLASS_REFRACTION_INCLUDED
#define SAGA_GLASS_REFRACTION_INCLUDED

#include "Packages/com.saltbox.saga/Shaders/Glass/GlassInput.hlsl"

// ---------------------------------------------------------------------------------------------------
// SCREEN-SPACE REFRACTION.
//
// Correct refraction bends the view ray at the interface by Snell's law and follows it until it hits
// geometry. You cannot do that against a colour texture, because that texture is indexed by SCREEN
// POSITION and not by world ray -- there is nothing to intersect. So the technique substitutes a 2D
// screen offset for the ray bend and samples the scene colour copy at screenUV + offset. It reads
// correctly because the eye only checks that the background WARPS WHERE THE SURFACE CURVES; it never
// checks the amount.
//
// WHAT IS IN THE TEXTURE. SampleSceneColor reads _CameraOpaqueTexture, the snapshot URP takes after the
// opaque queue and before transparents. Saga/Grass and Saga/Water are BOTH transparent-queue, so neither
// is in it: glass in front of water shows the lakebed, not the water. Two panes of glass in front of one
// another have the same problem with each other -- the nearer one composites the same snapshot and so
// erases the farther one. Neither is fixable inside this file.
// ---------------------------------------------------------------------------------------------------

// Metres of clearance over which the offset ramps back in behind a foreground object. A hard cut leaves
// a 1-texel seam tracing that object's silhouette, which at 480x270 is a visible jagged line.
static const float SAGA_GLASS_CLAMP_RANGE = 0.15;

struct SagaGlassRefraction
{
    half3 color;       // scene colour behind the glass, already absorbed
    float2 uv;         // where it was actually sampled from
    float surfaceEye;  // eye depth of the glass surface itself
};

// The screen direction of the shift, derived rather than guessed:
//
// Under ortho the view ray travels along -viewForward, which in VIEW space is (0,0,-1). Its component
// tangential to the surface is d - (d.n)n, whose screen part works out to n.z * n.xy -- so the ray leans
// along +n.xy. Entering a denser medium bends the ray TOWARD the normal, which SHORTENS exactly that
// tangential component, so the ray exits the slab displaced backwards along it. Hence the minus sign.
//
// |n.xy| is 0 looking straight into a surface and 1 at its silhouette, which is why the distortion piles
// up at a bottle's rim and vanishes through its middle. That also makes _RefractionDistortion readable
// as "pixels of shift at the rim".
//
// UNVERIFIED, THE Y SIGN. The derivation above is in VIEW space, and URP y-flips its projection when
// rendering into an RT on D3D (ShaderLibrary/CameraBasis.hlsl:46), so view +y and screen +v need not agree. The x
// axis is unaffected. To check: put a hard horizontal edge behind a sphere of glass and crank
// _RefractionDistortion. The rim distortion should pull the background TOWARD the sphere's centre all the
// way round; if it pulls outward at the top and bottom but inward at the sides, negate dirVS.y here.
float2 SagaGlassRefractionOffset(float3 normalWS)
{
    float2 dirVS = TransformWorldToViewDir(normalWS).xy;
    float2 offsetPx = -dirVS * _RefractionDistortion;
    return offsetPx * rcp(GetScaledScreenParams().xy);
}

// screenUV + offset can leave the screen, where the sampler clamps and smears one edge texel across the
// whole offset region. Fade the offset out over the border instead.
float SagaGlassRefractionEdgeFade(float2 screenUV)
{
    float2 f = smoothstep(0.0, max(_RefractionEdgeFade, 1e-4), min(screenUV, 1.0 - screenUV));
    return min(f.x, f.y);
}

SagaGlassRefraction SagaGlassRefract(float2 screenUV, float surfaceRawDepth, float3 normalWS,
                                     half3 transmittance)
{
    SagaGlassRefraction o;
    o.surfaceEye = SagaOrthoEyeDepth(surfaceRawDepth);

    float2 offset = SagaGlassRefractionOffset(normalWS) * SagaGlassRefractionEdgeFade(screenUV);

    float probeEye = SagaOrthoEyeDepth(SagaSampleRawDepth(screenUV + offset));
    offset *= saturate((probeEye - o.surfaceEye) * rcp(SAGA_GLASS_CLAMP_RANGE));

    o.uv = screenUV + offset;
    o.color = SampleSceneColor(o.uv) * transmittance;

    return o;
}

#endif // SAGA_GLASS_REFRACTION_INCLUDED
