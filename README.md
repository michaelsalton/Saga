# Saga
 
Saga is a custom stylized rendering pipeline for Unity's Universal Render Pipeline (URP 17). Its goal is to make fully 3D scenes read like hand-crafted isometric pixel art without giving up real-time lighting, reflections, and atmosphere. Instead of pixelating a finished frame, Saga treats the low-resolution look as a first-class constraint. The camera, shading model, outlines, water, glass, and atmospheric effects are all built to hold up at a native 480×270 internal resolution, and each system is exposed as an independent, tunable component.
 
## Pixel camera
 
The core of the pipeline is a dedicated pixel camera system. The world renders at a fixed internal resolution defined in a shared config asset. A custom renderer feature then upscales it to the display at a clean integer pixel scale, with letterboxing so pixels never stretch unevenly. The camera controller snaps movement to the pixel grid, which removes the shimmer and edge crawl you usually get when a low-resolution camera moves through a 3D scene. The final composite pass can quantize color to a fixed number of levels for a restrained, pixel-art palette, with optional ordered dithering to smooth gradients.
 
## Ramp-based stylized shading
 
Saga replaces URP's Lit shader with a "PBR-lite" toon shading model. Materials keep a physically grounded setup, with albedo, normal maps, packed ORM textures, roughness, and metallic. Lighting is resolved through configurable light bands or a multi-row ramp texture rather than a smooth falloff. Diffuse lighting uses wrapped N·L and adjustable band softness. Specular highlights are banded too, with separate cutoffs for rough and smooth surfaces, so metals and glossy materials still read as distinct from matte ones. Ambient and global illumination are banded as well, keeping indirect light consistent with the direct lighting. Shadows use a configurable tint rather than going flat black, and the shader supports world-space triplanar UVs so architecture can be textured without hand-authored UVs. The shader also handles emissive maps with an intensity control, which feeds into HDR bloom for neon and lit-window effects.
 
## Depth and normal outlines
 
A screen-space outline renderer feature detects edges from both the depth and normal buffers, with separate thresholds for silhouettes and interior creases. Outlines can be lit by the scene rather than drawn as a flat color, and they pick up a shadow tint in unlit areas. Artists get per-object control through an outline control mask. Each object can be assigned an ID and its own depth and normal weights and threshold bias, so a busy mesh can be toned down while a hero object keeps a crisp silhouette.
 
## Stylized water
 
Water is the most developed system in the pipeline. The surface is a tiled, tessellated grid mesh with animated vertex waves, and depth-based coloring that blends from shallow to deep tones. Refraction distorts the scene beneath the surface and fades out near edges and with depth to avoid artifacts at the shoreline. Animated underwater caustics are projected onto submerged geometry, with controls for warp, sharpness, wavelength, and how quickly they fade with depth.
 
Reflections come from a dedicated planar reflection camera. It uses an oblique clip plane at an automatically detected water level and renders through a separate lightweight renderer, so reflections don't inherit post-processing or pixel compositing. Reflections fade and blur with distance and fall back to a sky color far from the camera. Shoreline and intersection foam is driven by foam caster and mask components, with noise-broken edges. On top of that, the surface adds banded specular highlights, fresnel, and animated sparkles that glint across the water in a pixel-art-friendly way. A small floater component lets props like pool tubes drift around a patch of water and bob on the swell.
 
## Glass and transparency
 
Glass has its own renderer feature and shader. It models transmission tint, thickness-based absorption, refraction with edge fading, and banded reflections. It also has sharp glint highlights to sell the material at low resolution. The glass shader supports stepped relief mapping from height maps, which gives textured and frosted panes real surface depth. Glass surfaces are registered with the pipeline so transparent objects sort and composite correctly against each other and against the rest of the scene.
 
## Atmosphere: cloud shadows, god rays and mist
 
A cloud shadow volume projects soft, wind-driven cloud shadows across the world. You can adjust scale, coverage, and edge softness, and stepped shading keeps the shadows consistent with the banded lighting. The same volume drives volumetric god rays that shine through the gaps between clouds and are shadowed by them. It also drives a height-based mist layer with configurable floor, thickness, and falloff, so low areas can sit in fog while elevated areas stay clear.
 
## Ground color capture and occlusion cutaway
 
Saga captures the ground layer from above into a world-space color map at a configurable texel density. Other systems can sample this map, which lets grass, scattered sprites, and props blend into the color of the ground beneath them instead of floating on top of it. A ground mask renderer feature separates ground geometry from everything else in screen space.
 
For an isometric camera, where buildings and foliage often block the view, the pipeline includes a world occlusion volume. It carves a feathered, see-through opening around a point of interest, such as the player, on selected layers only. Radius, feather, height, and depth bias are all adjustable, and outlines can be suppressed inside the cutaway so the hole reads cleanly.
