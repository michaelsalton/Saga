using UnityEngine;

namespace Saga.Gameplay
{
    [DisallowMultipleComponent]
    public class IdleSway : MonoBehaviour
    {
        // rough peak drift in degrees per axis
        [Range(0f, 2f)]
        [SerializeField] float pitchAmplitude = 0.4f;
        [Range(0f, 2f)]
        [SerializeField] float yawAmplitude = 0.4f;
        [Range(0f, 2f)]
        [SerializeField] float rollAmplitude = 0.2f;
        // noise features per second; higher is twitchier
        [Range(0.05f, 2f)]
        [SerializeField] float frequency = 0.3f;
        // horizontal speed at which the sway has fully faded out
        [SerializeField] float fadeOutSpeed = 1f;
        [Range(1f, 30f)]
        [SerializeField] float blendSharpness = 4f;

        float noiseTime;
        float intensity = 1f;

        public Quaternion Tick(PlayerMotor motor, float deltaTime)
        {
            noiseTime += deltaTime * frequency;

            Vector3 velocity = motor.Velocity;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float targetIntensity = 1f - Mathf.Clamp01(horizontalSpeed / fadeOutSpeed);
            // framerate-independent ease, same form as HeadBob
            intensity = Mathf.Lerp(intensity, targetIntensity, 1f - Mathf.Exp(-blendSharpness * deltaTime));

            // one noise row per axis, ten units apart so the axes drift independently
            float pitch = Centered(noiseTime, 0.5f) * pitchAmplitude;
            float yaw = Centered(noiseTime, 10.5f) * yawAmplitude;
            float roll = Centered(noiseTime, 20.5f) * rollAmplitude;
            return Quaternion.Euler(new Vector3(pitch, yaw, roll) * intensity);
        }

        // recenter 0..1 noise so the drift orbits the aim point instead of sitting off to one side
        static float Centered(float x, float row) => (Mathf.PerlinNoise(x, row) - 0.5f) * 2f;
    }
}
