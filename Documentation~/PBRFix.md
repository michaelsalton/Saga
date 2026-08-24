1. Give F0 a real value; demote _SpecColor to a tint
Wrong now: lerp(_SpecColor.rgb, albedo, metallic) uses _SpecColor (default white) where the reflectance belongs.

Concept: F0 is "what fraction of light bounces straight off the surface when you look at it head-on." Every non-metal — skin, plastic, wood, stone, paint — sits at about 4%. That's not a coincidence; it falls out of the index of refraction being ~1.5 for basically all dielectrics. Metals are different: they reflect 50–95%, and they tint what they reflect (gold reflects yellow, copper orange). So a metal's F0 is its albedo colour. That single fact is the whole reason the metallic workflow works with one slider: metallic=0 means "F0 is 4% grey, albedo is the diffuse colour", metallic=1 means "F0 is the albedo colour, there is no diffuse".

The observable signature: plastic gets a white highlight even if it's red; metal gets a red highlight if it's red. If your red plastic has a red highlight, F0 is wrong.


// F0 = reflectance at head-on incidence. all dielectrics sit near 4%; metals reflect their own
// colour, which is why a metal highlight is tinted and a plastic one is white
half3 f0 = lerp(kDielectricSpec.rgb * _SpecColor.rgb, albedo, metallic);
kDielectricSpec is half4(0.04, 0.04, 0.04, 0.96), already in scope via the URP Lighting.hlsl include at line 4. _SpecColor survives as a multiplier on the dielectric end so you can still push a warm or cool highlight, but it now scales a real number instead of replacing it.

Migration cost: dielectric highlights get ~25× dimmer, so existing materials will look like the highlight vanished. Widen the slider in Toon.shader:41 to Range(0, 25) and re-dial. Intensity 25 is where a dielectric reaches a physically-impossible 100% reflection — that's your "how much am I cheating" readout.

2. Make the diffuse pay for the specular
Wrong now: albedo * (1.0h - metallic) only accounts for the metal case. A dielectric keeps 100% of its diffuse and gets a highlight on top.

Concept: light hitting a surface splits two ways — some reflects off the boundary immediately (specular), the rest refracts into the material, scatters around, and comes back out coloured (diffuse). It's one budget. If 4% left as specular, only 96% is available to become diffuse. Skipping the multiply invents energy: the surface emits more light than it received.


// light that reflected off the surface never got inside to scatter, so diffuse only gets what
// specular left behind: 96% for a dielectric, nothing for a metal
half oneMinusReflectivity = kDielectricSpec.a * (1.0h - metallic);
half3 diffuseAlbedo = albedo * oneMinusReflectivity;
URP writes this as OneMinusReflectivityMetallic(metallic) in BRDF.hlsl — same arithmetic, go read it. Expect the whole scene to dim ~4%. That's correct, not a regression.

3. Pay the metal back — environment specular
Wrong now: at metallic=1 the diffuse is zero, which zeroes the banded main light, the GI at line 61, and every additional light at line 76. Nothing adds the specular back. Metal renders black.

Concept: taking the diffuse away from a metal is only honest because a metal replaces it with a mirror image of its surroundings. In a normal PBR pipeline that's a reflection probe or cubemap sampled along the reflection vector, blurred by roughness — the technique is called IBL (image-based lighting), usually via the split-sum approximation. The stack has no probe, so use baked GI as a zero-detail stand-in: it's the right average colour arriving from the environment, just with all directionality thrown away.


// no reflection probe in the stack, so baked GI stands in for the environment a shiny surface
// mirrors -- this is the energy fix 2 took out of the diffuse, handed back as reflection
col += gi * f0 * ao;
Place it right after the existing col += gi * diffuseAlbedo * ao;. Note it's ungated: a dielectric gets gi * 0.04, invisible and correct. A metal gets full ambient tinted by its own colour, so chrome reads as flat sky-coloured instead of black.

Missing, not broken: a flat ambient metal still won't read as metallic, because metal looks metallic through variation in what it reflects. A two-tone sky/ground gradient sampled by reflect(-V, N).y would fix that — separate job.

4. Make roughness dim the highlight, not just widen it
Wrong now: roughness only moves cutoff, so a rough surface gets a bigger blob at identical peak brightness.

Concept: roughness describes how scattered the surface's microscopic facets are. A polished surface aims nearly all its reflected light into one tight direction; a rough one sprays the same amount over a wide cone. Same energy, more area, so the peak must fall. This is why real materials go from a sharp bright glint to a broad dull sheen and never to a broad bright one. The factor that enforces it in a real BRDF is the normalization term in the distribution function.

Your cutoff already encodes lobe width: the lit region is the set of directions where N·H > cutoff, and the solid angle of that spherical cap is 2π(1 − cutoff). So area is proportional to 1 − cutoff, and brightness should scale by its inverse.


half SagaToonSpecular(half3 N, half3 L, half3 V, half roughness)
{
    half3 H = normalize(L + V);
    half ndh = saturate(dot(N, H));
    half cutoff = lerp(_SpecCutoffSmooth, _SpecCutoffRough, roughness);
    half mask = smoothstep(cutoff - _SpecEdge, cutoff + _SpecEdge, ndh);

    // the blob is a spherical cap of solid angle 2*pi*(1 - cutoff), so a wider one must be dimmer
    // to carry the same energy. sqrt softens the full ~50x falloff to a stylized ~7x
    half area    = max(1.0h - cutoff, 1e-3h);
    half refArea = max(1.0h - _SpecCutoffSmooth, 1e-3h);
    return mask * sqrt(refArea * rcp(area));
}
The smooth cutoff is the reference point, so roughness 0 stays at brightness 1 and only rougher surfaces darken. Side effect worth knowing: moving the _SpecCutoffSmooth slider now shifts overall highlight brightness. Drop the sqrt for the physically exact falloff if the stylized version reads too hot.

5. Additional lights get a highlight too
Wrong now: the light loop only adds diffuseAlbedo * add.color * q * ao. A metal standing under a torch is still black after fix 3 gave it ambient, because point lights contribute nothing to it.


        // metals have no diffuse at all, so without this a point light cannot light them
        half addSpec = SagaToonSpecular(N, add.direction, V, roughness) * atten;
        col += addSpec * f0 * add.color * _SpecIntensity;
Goes inside LIGHT_LOOP_BEGIN, after the existing col +=. Costs one normalize and one smoothstep per light per pixel.

6. The meta pass bakes metals as if they were diffuse
Wrong now: ToonLitMetaPass.hlsl:13 hands the raw albedo to the lightmapper. A chrome wall bakes as a bright white diffuse bouncer, spilling light into the lightmap that a real mirror would have bounced in one direction instead.

Concept: the meta pass tells the offline lightmapper "how much light does this surface scatter onward, and in what colour." It only understands diffuse bouncing. A metal's real answer is "almost none diffusely, a lot specularly" — unrepresentable, so the standard dodge is to fold a fraction of the specular in, weighted by roughness, since a rough metal's reflection is the closest thing to diffuse spread.


    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
    half3 orm = SAMPLE_TEXTURE2D(_ORMMap, sampler_ORMMap, IN.uv).rgb;
    half metallic = saturate(orm.b * half(_Metallic));
    half roughness = saturate(orm.g * half(_Roughness));

    half3 f0 = lerp(kDielectricSpec.rgb * _SpecColor.rgb, albedo, metallic);

    // the lightmapper only models diffuse bouncing, so a metal's mirror reflection is faked as
    // diffuse in proportion to roughness -- a rough metal spreads light most like a diffuse one
    metaInput.Albedo = albedo * kDielectricSpec.a * (1.0h - metallic) + f0 * roughness * 0.5h;
That roughness * 0.5 weighting is what URP's own LitMetaPass.hlsl does — I'm recalling that from memory, so open the file and confirm before copying the constant. kDielectricSpec needs BRDF.hlsl in scope here; the meta pass currently only pulls UniversalMetaPass.hlsl, so you may need the include or a literal 0.04/0.96.

Assembled order in SagaToonLighting

half oneMinusReflectivity = kDielectricSpec.a * (1.0h - metallic);
half3 diffuseAlbedo = albedo * oneMinusReflectivity;
half3 f0 = lerp(kDielectricSpec.rgb * _SpecColor.rgb, albedo, metallic);
then unchanged banding, then col += gi * diffuseAlbedo * ao; followed by the new col += gi * f0 * ao;, then swap specTint for f0 in the direct highlight, then fix 5 in the loop.

Still absent after all six: Fresnel. Reflectance climbs from F0 to 100% at grazing angles on every material, which is why a puddle is glassy at a distance and transparent at your feet. Adding it means a pow(1 - saturate(dot(N, V)), 5) term (Schlick's approximation) — but the fixed-geometry trap applies: a top-down ortho camera holds N·V nearly constant on flat ground, so Fresnel mostly won't vary there.
