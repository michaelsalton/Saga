- Each frame the compute shader runes one thread per blade
- A thread tests whether its blade could be on screen
- Survivors write their blade index into a list
- Then the draw call reads how many instances to draw from a GPU buffer, oso the CPU never needs to know the count

- Blades get culled if theyre fully outside any of the frustrums planes
- Uses bounding spheres instead of the blades root point, using a property of Bezier curves, this is a convex hull property