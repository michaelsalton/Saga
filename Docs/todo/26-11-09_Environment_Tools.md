# Environment Tools

Most environment toolsets have three layers: **placement helpers** (put one thing down well), **distributors** (place many things from a rule), and **runtime representation** (how all that placed stuff gets drawn without killing the frame). `LinePrefabSpawner` (`Assets/!Vice/Art/Prefabs/Tools/World/`) is already a small distributor, and it already uses non-destructive generation, which is the main idea behind the second layer.

## 1. Placement helpers

These are the tools you reach for a hundred times a day.

- **Drop / snap to surface.** Raycast down from the object (or from the mouse in the Scene view) and move it to the hit point. The naive version puts the *pivot* at the hit point. That's only right if every prefab's pivot sits at its base. If one doesn't, it floats or sinks. The standard fix is to use the renderer's bounds minimum, or an authored "ground offset."
- **Align to normal.** The surface normal is the direction the surface faces at the hit point. Rotating the object's up to match it is right for rocks and decals and wrong for trees and lamp posts: on a slope they lean like they're falling over. Most tools blend between world-up and the normal, set per prefab (`Vector3.Slerp(Vector3.up, hit.normal, alignAmount)`).
- **Randomize on place.** Random yaw (rotation around the vertical axis), a scale range, and maybe a small tilt. This is the cheapest way to break up repetition, since ten identical trees at the same rotation read as copy-paste instantly.
- **Prefab brush / palette.** A Scene-view tool where you pick a set of prefabs and click or drag to paint them, with all of the above applied. In Unity you build this with the `EditorTool` API plus `Handles` and an Overlay panel.

Unity already ships some of this: Polybrush does prefab scattering and vertex painting, ProBuilder does blockout geometry, and Terrain has detail painting. Use them as references even if you build your own.

## 2. Distributors (procedural placement)

### Splines

A **spline** is a smooth curve defined by a few control points. It's the standard tool for anything linear: roads, kerbs, fences, power lines, rows of parked cars, street lights. There are two uses:

- **Spline instancing:** repeat a prefab along the curve. `LinePrefabSpawner` is the straight-line special case of this. The things it would need to become a spline tool:
  - Spacing by distance (one every 8 m) instead of by count, so lengthening a road adds lamps instead of stretching the gaps.
  - Align-to-tangent, so objects face along the curve.
  - A lateral offset (a sidewalk sits 3 m from the road centreline).
- **Spline mesh deformation:** bend a mesh's vertices along the curve, which is how roads, kerbs and pipes become continuous. A mesh can only bend at its vertices, so a kerb modelled as one long box with no subdivisions bends like a stiff plank, faceted at the ends and straight in between.

Unity's `com.unity.splines` package has `SplineContainer` for the curve math, plus `SplineInstantiate` and `SplineExtrude`. Building on it saves writing the curve evaluation and the editor handles.

### Area scatter

Fill a region (box, polygon, painted mask) with points, then put a prefab at each point. Where the points come from controls the look:

| Method | What it does | What you'd see |
|---|---|---|
| Uniform random | Each point is independent | Clumps and bald patches. Random distributions clump more than people expect. |
| Jittered grid (stratified sampling) | One random point per grid cell | Mostly even, but the grid shows through faintly at low jitter |
| **Poisson disk sampling** | Random points that never come closer than a minimum distance | Natural, even-but-irregular spacing. The standard choice. **Bridson's algorithm** is the usual implementation. |

That even-but-irregular look is called **blue noise**. The same idea shows up in rendering for dithering and sample patterns.

After the points come **filters**, which reject or thin points by rule: slope too steep, height below the waterline, inside an exclusion zone (no palm trees in the road), or a density texture. A pipeline of points → filters → spawn is the core of almost every serious system: Houdini, Unreal's PCG framework, and the systems in the talks under Reading.

### Determinism and hand edits

`LinePrefabSpawner` already handles the first problem, determinism: same seed, same output, plus separate random streams so one parameter doesn't reshuffle another. Two more problems come up as these tools grow:

1. **Hand edits get wiped.** An artist nudges one spawned car, a parameter changes, regeneration destroys it. The standard answers are a **bake** button (detach the output and stop regenerating), or a stored override list the generator reapplies after each rebuild.
2. **Sequential random streams break locality in area scatter.** If point 40's random values depend on how many draws came before it, adding one point near the start shifts every point after it, and the whole field changes when you nudge a boundary. The fix is to **hash each point's position** (or its grid cell) into its own seed, so a point's prefab and rotation depend only on where it is. The line spawner doesn't hit this badly because index *i* always gets the *i*-th pick. Poisson scatter does.

### Modular kits

Buildings and interiors usually come from a **modular kit**: pieces authored on a shared grid (walls 4 m wide, floors 3 m tall) so they snap together. This is mostly an asset-authoring discipline. The tooling is grid snapping, plus optionally **sockets** (named attachment points on each piece, so a door frame knows where it connects). For a street-level city like Vice, a facade generator (pick ground floor + N middle floors + roof from a kit, seeded per building) is a common semi-procedural step.

## 3. Surface variation

These are tools that make placed things look less repeated. Some of them touch the stack.

- **Decals:** projected textures (cracks, stains, puddles, road paint) that break up tiling. URP has a Decal Projector, but it reaches surfaces through the **DBuffer** (decal buffer: a screen-space texture the decals render into first, which surface shaders then read). URP's Lit shader samples it. A custom PBR shader only does if it includes the decal sampling code and the `_DBUFFER_MRT*` keywords. Whether `Saga/Standard` does is unverified. If it doesn't, decals will silently do nothing on Saga materials.
- **Vertex painting / layer blending:** paint per-vertex colour that a shader uses as a mask to blend materials, like sand drifting onto a sidewalk or grime at wall bases. **Height-blended layers** use each texture's height map to decide which layer wins, which is why the transition looks like sand settling into cracks instead of a soft crossfade.

## 4. Runtime representation

Every prefab instance as a GameObject is fine for hundreds and painful for tens of thousands. Each one costs CPU time for culling (deciding it's off-screen), transform updates, and a draw call (one CPU→GPU "draw this mesh" command). The standard split:

- **Interactive or unique things** (cars, the arcade, anything with a script or collider that matters) stay GameObjects.
- **Clutter** (pebbles, litter, bushes, small rocks) is stored as data: a list of transform matrices per mesh. It's drawn with **GPU instancing** (one draw call renders many copies), usually `Graphics.RenderMeshIndirect` with GPU-side culling. Saga's Grass system presumably works this way already (unverified), so a scatter tool's output can target the same path.

Past that come **LODs** (lower-detail meshes at distance) and **impostors** (a camera-facing card showing a pre-rendered picture of the object), for far-away trees and buildings.

## Build order

1. [ ] **Snap-to-surface + randomize-on-place** as an `EditorTool`. Small, used constantly.
2. [ ] **Spline instancer**, replacing the line spawner, with spacing-by-distance and an offset. Streets are the main structure of the level.
3. [ ] **Poisson area scatter with exclusion volumes and position-hashed seeds**, for beach props and vegetation.
4. [ ] **Decal support in Saga's shaders.** The rendering-side lesson in the set.
5. [ ] **Instanced output mode** for the scatter tool, once clutter counts get high.

## Reading

Titles are from memory and may be slightly off.

- Joel Burgess, *Skyrim's Modular Level Design* (GDC 2013)
- *GPU-Based Procedural Placement in Horizon Zero Dawn* (GDC 2017): filters and density maps evaluated on the GPU
- *Procedural World Generation of Far Cry 5* (GDC 2018): Houdini-driven rule-based placement
- Robert Bridson, *Fast Poisson Disk Sampling in Arbitrary Dimensions* (2007), a two-page paper
