using UnityEngine;

namespace Saga.Rendering
{
    // marks the volume that gets reflection probes; the editor generator writes them as children of this object
    [AddComponentMenu("Saga/Rendering/Reflection Probe Field")]
    public class SagaReflectionProbeField : MonoBehaviour
    {
        [Tooltip("Volume in metres on world axes, centred on this transform. Scale and rotation are ignored.")]
        [SerializeField] Vector3 size = new Vector3(160f, 30f, 160f);

        [Tooltip("Metres above the lowest floor in the volume that still count as walkable. Keeps probes off rooftops. Raise it for multi-storey interiors.")]
        [Min(0f)]
        [SerializeField] float walkableHeight = 3f;

        [Tooltip("Metres between candidate positions. Smaller finds better placements, and generation time grows with the fourth power of the reduction.")]
        [Min(0.5f)]
        [SerializeField] float candidateSpacing = 6f;

        [Tooltip("Metres above the floor that probes capture from. Must clear the generator's 0.5 m wall clearance.")]
        [Min(0.6f)]
        [SerializeField] float captureHeight = 1.7f;

        [Tooltip("Rays per candidate used to survey what it sees.")]
        [Range(32, 1024)]
        [SerializeField] int rayCount = 128;

        [Tooltip("Furthest a probe may serve a candidate from, in metres.")]
        [Min(1f)]
        [SerializeField] float coverageRadius = 60f;

        [Tooltip("Parallax error a probe may have at the points it serves. 0 is a perfect match, 1 means every surface is hidden from the probe. Lower places more probes.")]
        [Range(0.01f, 1f)]
        [SerializeField] float maxError = 0.15f;

        [Tooltip("Metres over which outdoor probes fade into their neighbours.")]
        [Min(0f)]
        [SerializeField] float blendDistance = 3f;

        [SerializeField] int resolution = 128;

        public Bounds Volume => new Bounds(transform.position, size);
        public float WalkableHeight => walkableHeight;
        public float CandidateSpacing => candidateSpacing;
        public float CaptureHeight => captureHeight;
        public int RayCount => rayCount;
        public float CoverageRadius => coverageRadius;
        public float MaxError => maxError;
        public float BlendDistance => blendDistance;
        public int Resolution => resolution;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
