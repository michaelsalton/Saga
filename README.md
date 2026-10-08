# Saga

Saga is a custom physically based rendering library for Unity's Universal Render Pipeline (URP 17). It replaces URP's Lit shader with its own PBR shading model and adds a small set of renderer features and components on top: outlines, water, and cloud shadows. Each system is an independent, tunable component.

## Standard shader

`Saga/Standard` is a metallic-roughness PBR shader. Direct lighting is Cook-Torrance: a GGX (Trowbridge-Reitz) normal distribution, height-correlated Smith masking-shadowing, and Schlick's Fresnel approximation, applied to the main light and every additional light. Indirect lighting combines baked GI or light probes for diffuse with URP's reflection probes for specular, weighted by an analytic environment BRDF fit, so the indirect term needs no lookup texture. The shared BRDF terms live in `ShaderLibrary/PBR.hlsl`.

Materials take albedo, a normal map, and a packed ORM texture (R = ambient occlusion, G = roughness, B = metallic). The shader also supports world-space triplanar UVs, so architecture can be textured without hand-authored UVs; parallax occlusion mapping from a height map; and emissive maps that feed HDR bloom.

## Depth and normal outlines

A screen-space outline renderer feature detects edges from both the depth and normal buffers, with separate thresholds for silhouettes and interior creases. Outlines can be lit by the scene rather than drawn as a flat color, and they pick up a shadow tint in unlit areas. Artists get per-object control through an outline control mask. Each object can be assigned an ID and its own depth and normal weights and threshold bias, so a busy mesh can be toned down while a hero object keeps a crisp silhouette.

## Water

Water is the most developed system in the library. The surface is a tiled, tessellated grid mesh with animated vertex waves, and depth-based coloring that blends from shallow to deep tones. Refraction distorts the scene beneath the surface and fades out near edges and with depth to avoid artifacts at the shoreline. Animated underwater caustics are projected onto submerged geometry, with controls for warp, sharpness, wavelength, and how quickly they fade with depth.

Reflections come from a dedicated planar reflection camera. It uses an oblique clip plane at an automatically detected water level and renders through a separate lightweight renderer, so reflections don't inherit post-processing. Reflections fade and blur with distance and fall back to a sky color far from the camera. Shoreline and intersection foam is driven by foam caster and mask components, with noise-broken edges. The surface also adds sun specular, Fresnel, and animated sparkles. A small floater component lets props drift around a patch of water and bob on the swell.

## Cloud shadows

A cloud shadow volume projects soft, wind-driven cloud shadows across the world. You can adjust scale, coverage, and edge softness, with optional stepping. The Standard shader folds the cloud mask into the main light's shadow attenuation, so clouds darken the same sun the shadow map does.

## Optional low-resolution presentation

Two features from Saga's earlier pixel-art direction remain and can be switched on or off through `SagaDisplaySettings`. The pixel camera renders the world at a low internal resolution and upscales it to the display at a whole-number scale, snapping the camera to the internal pixel grid so geometry doesn't crawl as it moves. The quantize pass posterizes the final image to a fixed number of color levels, with optional ordered dithering.
