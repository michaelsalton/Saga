using UnityEngine;

namespace Saga.Gameplay
{
    [RequireComponent(typeof(PlayerMotor))]
    [DisallowMultipleComponent]
    public class LandingDip : MonoBehaviour
    {
        // slower touchdowns are curbs and grounded flicker on slopes, not landings
        [Range(0f, 10f)]
        [SerializeField] float minimumImpactSpeed = 3f;
        // a longer fall past this dips no deeper
        [Range(1f, 30f)]
        [SerializeField] float maximumImpactSpeed = 15f;
        // downward eye speed per m/s of impact; a normal jump lands at about 6.6 m/s
        [Range(0f, 1f)]
        [SerializeField] float impactKick = 0.4f;
        // how fast the spring recovers; the cycles per second it would bounce at with no damping
        [Range(0.5f, 8f)]
        [SerializeField] float frequency = 2.5f;
        // 1 settles without overshoot, below 1 rebounds past level once before settling
        [Range(0.1f, 1.5f)]
        [SerializeField] float dampingRatio = 0.7f;

        // explicit spring steps blow up when a step is long relative to the spring's period
        const float MaximumStep = 1f / 120f;

        PlayerMotor motor;
        float offset;
        float velocity;

        void Awake() => motor = GetComponent<PlayerMotor>();

        void OnEnable() => motor.Landed += OnLanded;

        void OnDisable()
        {
            motor.Landed -= OnLanded;
            offset = 0f;
            velocity = 0f;
        }

        void OnLanded(float impactSpeed)
        {
            if (impactSpeed < minimumImpactSpeed) return;
            // a kick to velocity, not to offset, so the dip starts at zero and grows instead of teleporting the eye down
            velocity -= Mathf.Min(impactSpeed, maximumImpactSpeed) * impactKick;
        }

        public Vector3 Tick(float deltaTime)
        {
            float angularFrequency = 2f * Mathf.PI * frequency;
            float stiffness = angularFrequency * angularFrequency;
            float damping = 2f * dampingRatio * angularFrequency;
            // fixed substeps, so a frame hitch can't make the spring overshoot and explode
            while (deltaTime > 0f)
            {
                float step = Mathf.Min(deltaTime, MaximumStep);
                // Hooke's law pulls toward level, damping drags against the motion
                velocity += (-stiffness * offset - damping * velocity) * step;
                // velocity first, then position with the new velocity: semi-implicit Euler, stable where plain Euler gains energy
                offset += velocity * step;
                deltaTime -= step;
            }
            return new Vector3(0f, offset, 0f);
        }
    }
}
