using UnityEngine;

namespace Saga.Rendering
{
    public static class WaterSurface
    {
        const int WaveCount = 4;
        const float WaveLengthTotal = 32f;   // SAGA_WAVE_LENGTH_TOTAL
        const float WaveLengthMax = 14f;     // SAGA_WAVE_LENGTH_MAX

        // SAGA_WAVE_SHAPE, verbatim. xy = object-space centre, z = wavelength in metres, w = steepness.
        static readonly Vector4[] WaveShape =
        {
            new Vector4(-12f, -10f, 14f,  0.55f),
            new Vector4( 15f,   6f,  9f,  0.45f),
            new Vector4( -4f,  14f,  5.5f, 0.35f),
            new Vector4(  8f, -16f,  3.5f, 0.30f),
        };

        static readonly int WaveAmplitudeId   = Shader.PropertyToID("_WaveAmplitude");
        static readonly int WaveSpeedId       = Shader.PropertyToID("_WaveSpeed");
        static readonly int DampeningFactorId = Shader.PropertyToID("_DampeningFactor");
        static readonly int EdgeDampenWidthId = Shader.PropertyToID("_EdgeDampenWidth");

        public static float ShaderTime =>
            Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

        /// <summary>The four wave-shaping values, read off the water material's UnityPerMaterial block.</summary>
        public struct Settings
        {
            public float waveAmplitude;
            public float waveSpeed;
            public float dampeningFactor;
            public float edgeDampenWidth;

            public static Settings FromMaterial(Material m) => new Settings
            {
                waveAmplitude   = m.GetFloat(WaveAmplitudeId),
                waveSpeed       = m.GetFloat(WaveSpeedId),
                dampeningFactor = m.GetFloat(DampeningFactorId),
                edgeDampenWidth = m.GetFloat(EdgeDampenWidthId),
            };
        }

        public static float RestHeight(WaterGrid grid, Vector3 worldPos)
        {
            Transform t = grid.transform;
            Vector3 posOS = t.InverseTransformPoint(worldPos);
            return t.TransformPoint(new Vector3(posOS.x, 0f, posOS.z)).y;
        }

        public static bool TrySample(WaterGrid grid, Vector3 worldPos, float time,
                                     out float surfaceY, out Vector3 normalWS)
        {
            surfaceY = 0f;
            normalWS = Vector3.up;

            if (grid == null) return false;

            var renderer = grid.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return false;

            var settings = Settings.FromMaterial(renderer.sharedMaterial);
            Transform t = grid.transform;
            Vector3 posOS = t.InverseTransformPoint(worldPos);

            float size = Mathf.Max(grid.size, 1e-4f);
            var uv1 = new Vector2(posOS.x / size + 0.5f, posOS.z / size + 0.5f);

            Evaluate(settings, new Vector3(posOS.x, 0f, posOS.z), uv1, time,
                     out Vector3 displacedOS, out Vector3 normalOS);

            surfaceY = t.TransformPoint(displacedOS).y;
            normalWS = t.TransformDirection(normalOS).normalized;
            return true;
        }

        public static void Evaluate(in Settings s, Vector3 positionOS, Vector2 uv1, float time,
                                    out Vector3 displacedOS, out Vector3 normalOS)
        {
            float edgeDampen = EdgeDampen(s, uv1);

            Vector3 offset = Vector3.zero;
            Vector3 nTerm = Vector3.zero;

            for (int i = 0; i < WaveCount; i++)
            {
                Vector4 shape = WaveShape[i];

                var center = new Vector2(shape.x, shape.y);
                float waveLength = Mathf.Max(shape.z, 1e-3f);
                float steepness = Mathf.Clamp01(shape.w) * Mathf.Clamp01(s.waveAmplitude / 1.5f);
                float amplitude = s.waveAmplitude * (waveLength / WaveLengthTotal);
                float speed = s.waveSpeed * Mathf.Sqrt(waveLength / WaveLengthMax);

                var toVertex = new Vector2(positionOS.x - center.x, positionOS.z - center.y);
                float dist = toVertex.magnitude;
                Vector2 dir = toVertex / Mathf.Max(dist, 1e-4f);

                // Radial fade: each wave flattens out at its own centre, which is what stops the circular
                // form from pinching into a spike there.
                float fade = Mathf.Clamp01(dist / Mathf.Max(waveLength * 0.5f, 1e-4f));

                float frequency = 2f / waveLength;
                float phaseConstant = speed * frequency;
                float qi = steepness / Mathf.Max(amplitude * frequency * WaveCount, 1e-4f);
                float damp = fade * edgeDampen;

                float rad = frequency * dist - time * phaseConstant;
                float sinR = Mathf.Sin(rad);
                float cosR = Mathf.Cos(rad);

                offset.x += qi * amplitude * dir.x * cosR * damp;
                offset.z += qi * amplitude * dir.y * cosR * damp;
                offset.y += amplitude * sinR * damp;

                float wa = frequency * amplitude * damp;
                nTerm.x += dir.x * wa * cosR;
                nTerm.y += qi * wa * sinR;
                nTerm.z += dir.y * wa * cosR;
            }

            displacedOS = positionOS + offset;
            normalOS = new Vector3(-nTerm.x, 1f - nTerm.y, -nTerm.z).normalized;
        }

        static float EdgeDampen(in Settings s, Vector2 uv1)
        {
            var d = new Vector2(Mathf.Min(uv1.x, 1f - uv1.x), Mathf.Min(uv1.y, 1f - uv1.y));
            float edge01 = Mathf.Clamp01(Mathf.Min(d.x, d.y) / Mathf.Max(s.edgeDampenWidth, 1e-4f));
            edge01 = edge01 * edge01 * (3f - 2f * edge01);   // smoothstep, matching the HLSL's hand-rolled one
            return Mathf.Lerp(1f, edge01, s.dampeningFactor);
        }
    }
}
