using UnityEngine;

namespace Saga.Gameplay
{
    [DisallowMultipleComponent]
    public class HeadBob : MonoBehaviour
    {
        // meters walked per footstep
        [Range(0.3f, 2.5f)]
        [SerializeField] float strideLength = 1.2f;
        [Range(0f, 0.15f)]
        [SerializeField] float bobHeight = 0.05f;
        [Range(0f, 0.1f)]
        [SerializeField] float swayWidth = 0.03f;
        // horizontal speed that produces the full bob
        [SerializeField] float fullBobSpeed = 4f;
        // cap on how much harder than full a sprint can bob
        [Range(1f, 2f)]
        [SerializeField] float maxIntensity = 1.4f;
        // how quickly the bob fades in and out on start, stop, and leaving the ground
        [Range(1f, 30f)]
        [SerializeField] float blendSharpness = 10f;

        float stridePhase;
        float intensity;

        void OnDisable()
        {
            intensity = 0f;
        }

        public Vector3 Tick(PlayerMotor player, float deltaTime)
        {
            Vector3 velocity = player.Velocity;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;

            // π radians per footstep, so one full sine cycle is a left-right pair
            stridePhase += horizontalSpeed * deltaTime / strideLength * Mathf.PI;
            // wrap so float precision never degrades over a long session
            stridePhase %= 2f * Mathf.PI;

            float targetIntensity = player.IsGrounded ? Mathf.Min(horizontalSpeed / fullBobSpeed, maxIntensity) : 0f;
            // framerate-independent ease: same curve at 30 or 144 fps
            intensity = Mathf.Lerp(intensity, targetIntensity, 1f - Mathf.Exp(-blendSharpness * deltaTime));

            float sine = Mathf.Sin(stridePhase);
            // sharp dip at foot strike, rounded crest at midstance
            float vertical = -bobHeight * (1f - Mathf.Abs(sine));
            // head drifts over the planted foot, one side per step
            float lateral = swayWidth * sine;
            return new Vector3(lateral, vertical, 0f) * intensity;
        }
    }
}
