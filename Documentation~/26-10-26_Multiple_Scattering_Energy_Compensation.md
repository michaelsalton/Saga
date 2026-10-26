- Multiple scattering energy compensation

- GGX with Smith visibility models light bouncing off one microfacet. On a rough surface, much of the light hits a second microfacet before escaping, and the single-scatter model drops that light. You'd see it as rough metals (rough gold, cast iron) rendering visibly darker and duller than they should. Smooth surfaces are barely affected. Filament and UE fix it with a small extra term; look up "Kulla-Conty" or Filament's "energy compensation." It costs a few ALU ops (math instructions) and reuses SagaEnvironmentBRDF.

- Practical multiple scattering compensation for microfacet models
Emmanuel Turquin∗
Industrial Light & Magic