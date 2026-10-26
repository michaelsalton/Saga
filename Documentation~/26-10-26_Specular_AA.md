- Specular anti-aliasing
  - Normal map has detail smaller than a pixel viewed at a distance
  - Mipmapping averages the normals, but the BRDF doesn't know the averaged surface should look rouger
  - Highlights end up sparkling and crawling as the camera moves
  - The fix raises roughness whereever normals vary quicjly across a pixel
  - Toksvig, Kaplanyan-Hoffman, geometric specular AA

- No mips (or mips too sharp): the pixel takes one sample out of that varied patch. Whether that sample's normal happens to point at the half-vector H decides whether the pixel is blinding or black. Move the camera half a pixel and you get a different sample, so the highlight crawls.
- With mips: the mip averages the normals into a flatter normal. The tight lobe of a smooth surface then sits on that flat normal, so you get one hot, sharp highlight where the real surface would show a broad, dim sheen. It still flickers at mip transitions.

- Fix is to widen the lobe by the normal variance the pixel can't resolve. The distribution a pixel sees is the materials NDF (normal distribution function: the spread of the microfacet normals) convolved with the spread of normals across the pixel's footprint. If both are treated as Gaussians in slope space, their variances add

-  Geometric / screen-space specular AA (Kaplanyan 2016, Tokuyoshi & Kaplanyan 2019)