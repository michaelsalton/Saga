# Saga feature ideas — 2026-10-07

Candidate additions after the cleanup that removed Glass, Godrays, GrassGround, WorldOcclusion and Grass.
Ideas, not commitments. Ordered roughly by how much each one pays back for the effort.

Suggested starting order: **lighting debug views** first (it makes everything after it debuggable), then
**perspective-correct depth** (a prerequisite for most screen-space work now that the camera isn't ortho).

## Debug and look-dev tools

### Lighting debug views
A global shader keyword plus a dropdown that makes `Saga/Standard` output one term at a time:

- normals
- roughness, metallic, AO
- GGX distribution (D)
- Fresnel (F)
- visibility / masking-shadowing (G)
- direct lighting only
- indirect lighting only

Seeing D alone teaches more about roughness than the equation does. URP's equivalent is the Rendering
Debugger; this one would cover Saga's own BRDF.

### Look-dev scene
A grid of spheres sweeping roughness 0→1 across and metallic 0→1 down, under a switchable environment.
The standard way to validate a BRDF. Problems like rough metals coming out too dark show up immediately.

### Furnace test
A white sphere in a uniformly white environment should vanish into the background. If it stays visible,
the BRDF is gaining or losing energy. The standard check for an energy-conservation bug.

### Texel density / mip debug view
Color surfaces by how many texels cover each screen pixel, so textures that are too blurry or too dense
stand out.

## Rendering features

Roughly easiest to hardest.

### 1. Perspective-correct depth
Not a feature, but a prerequisite. `Runtime/Outline/Outline.shader` and `Shaders/Water/WaterRefraction.hlsl`
call `SagaOrthoEyeDepth` directly, which reads raw depth as linear. Under perspective, raw depth is
non-linear (it crowds toward the far plane), so:

- outline depth thresholds misbehave with distance
- `SagaCameraWorldPos` reconstructs the wrong world position, so lit outlines sample shadows and clouds
  at the wrong point
- the water's depth fade is off

`ShaderLibrary/Depth.hlsl` already has a function that handles both camera types (around line 23); the
call sites need to switch to it. The outline control mask is documented as ortho-only too.

### 2. Tonemapping and exposure
PBR lighting outputs HDR values (brightness above 1.0). A tonemapper decides how those map onto the
screen. Techniques to read up on: ACES, AgX, Khronos PBR Neutral. Writing one shows why a sunlit white
wall shouldn't simply clip.

### 3. Saga-owned image-based lighting
Specular currently comes from URP's reflection probes plus an analytic environment BRDF fit
(`SagaEnvironmentBRDF`). Owning it means:

- prefiltering a cubemap per roughness level
- baking a BRDF lookup texture

Search: "split-sum approximation" (Karis, UE4). This is the indirect half of PBR, currently borrowed from
URP.

### 4. Screen-space ambient occlusion (SSAO / GTAO)
Darken creases using the depth buffer the outline pass already reads. `Saga/Standard` has an AO input
that currently only comes from the ORM texture.

### 5. Decals
Project albedo, normal and roughness onto existing surfaces: puddles, grime, cracks. URP ships decals,
but writing a box-projected one teaches world-position reconstruction from depth.

### 6. Height fog / volumetric fog
Pairs naturally with cloud shadows. Ray-marched fog shadowed by the shadow map would partly rebuild what
the deleted godrays did, but camera-correct.

### 7. Screen-space reflections (SSR)
Ray-march the depth buffer for reflections on wet or glossy floors. Water uses a planar reflection camera
instead; SSR is the general-purpose alternative, with well-known failure cases worth learning.

## Gameplay utilities (`Saga.Gameplay`)

### Free-fly camera: switch to Input Actions
`FreeFlyCamera` reads `Keyboard.current` / `Mouse.current` directly. Moving to `InputActionProperty`
fields would allow rebinding and gamepad support, with an option to reference Vice's
`PlayerControls.inputactions`.

- 2DVector composite for WASD, 1DAxis composite for Q/E
- `Vector3.ClampMagnitude` instead of `normalized`, so analog input keeps partial speed
- Enable embedded actions in `OnEnable`, but don't disable referenced shared actions
- Mouse delta still skips `deltaTime`; a gamepad stick needs it

### Orbit / follow camera
Companion to the fly camera. Techniques: critically damped spring (`Mathf.SmoothDamp`), camera collision.

### Screen shake / camera impulse
Small and reusable in any game.

## Housekeeping

- `Documentation~/PBRFix.md` says Fresnel is still missing, but `SagaDirectBRDF` applies
  `SagaFresnelSchlick`. The doc is probably stale. Not checked further.
- `package.json` description is still the broken `"Stylized ."`.
- `ShaderLibrary/Dither.hlsl` is unused since WorldOcclusion was removed.
- Removed features still need cleanup in the Unity Editor: missing renderer features in `PC_Renderer.asset`,
  missing components in `Vice.unity` / `Water.unity`, and the orphaned `VICE_M_Grass.mat`.
