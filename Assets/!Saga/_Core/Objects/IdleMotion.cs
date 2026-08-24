using UnityEngine;

namespace Saga
{
    /// <summary>
    /// Decorative idle motion for an object: spin it in place and/or hover it up and down.
    /// Each behaviour is independent — enable either or both. Hover is computed as an offset from
    /// the object's starting local position, so it never drifts and is unaffected by the spin.
    /// </summary>
    public class IdleMotion : MonoBehaviour
    {
        [Header("Spin")]
        [Tooltip("Rotate the object in place.")]
        [SerializeField] bool spin = true;
        [Tooltip("Axes to spin around, in the object's local space. Tick more than one for a diagonal spin.")]
        [SerializeField] bool spinX = false;
        [SerializeField] bool spinY = true;
        [SerializeField] bool spinZ = false;
        [Tooltip("Spin speed in degrees per second. Negative reverses direction.")]
        [Range(-360f, 360f)]
        [SerializeField] float spinSpeed = 90f;

        [Header("Hover")]
        [Tooltip("Bob the object up and down.")]
        [SerializeField] bool hover = true;
        [Tooltip("Axes to bob along, in the parent's space. Tick more than one for a diagonal bob.")]
        [SerializeField] bool hoverX = false;
        [SerializeField] bool hoverY = true;
        [SerializeField] bool hoverZ = false;
        [Tooltip("Peak distance from the rest position, in world units.")]
        [Range(0f, 2f)]
        [SerializeField] float hoverAmplitude = 0.25f;
        [Tooltip("Bobs per second.")]
        [Range(0f, 5f)]
        [SerializeField] float hoverFrequency = 1f;

        [Header("Sync")]
        [Tooltip("Give each instance a random starting phase so a field of objects doesn't bob/idle in lockstep.")]
        [SerializeField] bool randomizePhase = true;

        Vector3 startLocalPos;
        float phase;

        Vector3 SpinAxis => new Vector3(spinX ? 1f : 0f, spinY ? 1f : 0f, spinZ ? 1f : 0f);
        Vector3 HoverAxis => new Vector3(hoverX ? 1f : 0f, hoverY ? 1f : 0f, hoverZ ? 1f : 0f);

        void OnEnable()
        {
            // Capture rest position fresh each enable so the hover stays anchored even if the object moved.
            startLocalPos = transform.localPosition;
            phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        }

        void OnDisable()
        {
            // Restore the rest position so a disabled object isn't frozen mid-bob.
            if (hover) transform.localPosition = startLocalPos;
        }

        void Update()
        {
            if (spin)
            {
                Vector3 axis = SpinAxis;
                if (axis != Vector3.zero)
                    transform.Rotate(axis.normalized * (spinSpeed * Time.deltaTime), Space.Self);
            }

            if (hover)
            {
                Vector3 axis = HoverAxis;
                if (axis != Vector3.zero)
                {
                    float offset = Mathf.Sin(Time.time * hoverFrequency * Mathf.PI * 2f + phase) * hoverAmplitude;
                    transform.localPosition = startLocalPos + axis.normalized * offset;
                }
            }
        }
    }
}
