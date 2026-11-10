using UnityEngine;

namespace Saga.World
{
    // island height as a pure function of XZ in the terrain's local space, so chunks, normals and anything rooted on the ground agree
    [System.Serializable]
    public class SagaIslandShape
    {
        [Header("Island")]
        [Tooltip("Metres from the centre to the coast before Coast Noise moves it.")]
        [Min(1f)]
        public float islandRadius = 250f;

        [Tooltip("Metres the dome rises at the centre, before hills. Together with Island Radius this sets the slope at the coast: 12 over 250 is about 1:20.")]
        public float domeHeight = 12f;

        [Tooltip("Metres the hills add at full strength.")]
        public float hillHeight = 35f;

        [Tooltip("Fraction of the radius over which hills fade in from the coast. Small values let hills crowd the shore and turn beaches into cliffs.")]
        [Range(0.01f, 1f)]
        public float coastFade = 0.25f;

        [Tooltip("Fraction of the radius the coast is pushed in or out. This alone decides the shoreline's shape.")]
        [Range(0f, 0.6f)]
        public float coastNoise = 0.3f;

        [Tooltip("Metres below the water the seabed flattens out. Must be deeper than the water material's Depth Max.")]
        [Min(0f)]
        public float seabedDepth = 15f;

        [Header("Noise")]
        [Tooltip("Metres per wavelength of the lowest octave, roughly the spacing between major hills.")]
        [Min(1f)]
        public float featureSize = 120f;

        [Range(1, 8)]
        public int octaves = 5;

        [Tooltip("Amplitude multiplier per octave. Higher is rougher.")]
        [Range(0f, 1f)]
        public float gain = 0.5f;

        [Tooltip("Frequency multiplier per octave.")]
        [Range(1.5f, 3f)]
        public float lacunarity = 2f;

        [Tooltip("Metres the sample point is displaced by domain warping. Zero gives plain fBm ridges.")]
        [Min(0f)]
        public float warpStrength = 30f;

        public int seed = 1;

        public float Height(float positionX, float positionZ)
        {
            // a fixed offset into Perlin's plane per seed, so different seeds give different islands
            float seedOffset = seed * 17.31f;

            // bends the ridges; the 5.2 and 1.3 shifts decorrelate the two warp axes from each other
            float warpX = (Fbm(positionX + 5.2f * featureSize, positionZ, 2, seedOffset) - 0.5f) * 2f * warpStrength;
            float warpZ = (Fbm(positionX, positionZ + 1.3f * featureSize, 2, seedOffset) - 0.5f) * 2f * warpStrength;
            float sampleX = positionX + warpX;
            float sampleZ = positionZ + warpZ;

            float distance = Mathf.Sqrt(positionX * positionX + positionZ * positionZ) / islandRadius;
            distance += (Fbm(sampleX, sampleZ, 2, seedOffset + 91f) - 0.5f) * 2f * coastNoise;
            // positive inland, zero on the coast, negative at sea
            float land = 1f - distance;

            float hills = Fbm(sampleX, sampleZ, octaves, seedOffset);
            // zero at the coast so hills can't move the shoreline or raise islets offshore
            float hillMask = Mathf.Clamp01(land / coastFade);
            float height = land * domeHeight + hills * hillMask * hillHeight;

            return SmoothMax(height, -seabedDepth, 2f);
        }

        float Fbm(float sampleX, float sampleZ, int octaveCount, float seedOffset)
        {
            float sum = 0f;
            float amplitude = 1f;
            float amplitudeTotal = 0f;
            float frequency = 1f / featureSize;
            for (int i = 0; i < octaveCount; i++)
            {
                // Perlin reads 0.5 on every integer lattice point, so unshifted octaves would all agree at the origin
                float octaveOffset = seedOffset + i * 31.7f;
                sum += amplitude * Mathf.PerlinNoise(sampleX * frequency + octaveOffset, sampleZ * frequency - octaveOffset);
                amplitudeTotal += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            // keeps the result in 0..1 whatever the octave count, so Hill Height stays in metres
            return sum / amplitudeTotal;
        }

        // polynomial smooth maximum: within `blend` metres of the crossing it eases onto the floor instead of creasing
        static float SmoothMax(float first, float second, float blend)
        {
            float weight = Mathf.Clamp01(0.5f + 0.5f * (first - second) / blend);
            return Mathf.Lerp(second, first, weight) + blend * weight * (1f - weight);
        }
    }
}
