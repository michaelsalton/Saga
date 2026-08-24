using System.Collections.Generic;
using UnityEngine;

namespace Saga.World
{
    [AddComponentMenu("Saga/World/Poisson Disc Spawner")]
    public class PoissonDiscSpawner : MonoBehaviour
    {
        public const int MaxPoints = 990000;

        const float Packing = 0.7f;

        [Header("Area (metres) — drag the box handles in the Scene view")]
        [Tooltip("Footprint on the local XZ plane, centred on this transform. TRANSFORM SCALE IS " +
                 "IGNORED on purpose: the area is authored in metres so density means the same thing " +
                 "on every spawner. Rotation IS respected.")]
        public Vector2 areaSize = new Vector2(20f, 20f);

        [Header("Density")]
        [Tooltip("How many points you want. The minimum-distance radius is SOLVED to hit this, so " +
                 "the actual count lands within a few percent — the inspector readout is the truth.")]
        [Range(1, 99000)] public int targetCount = 200;

        [Tooltip("Same seed = same layout, every time. Change it to reroll.")]
        public int seed = 12345;

        [Tooltip("Candidates tried around each point before it is retired (Bridson's k). 30 is the " +
                 "reference value; lower is faster and leaves slightly bigger gaps.")]
        [Range(4, 60)] public int attemptsPerPoint = 30;

        [Header("Surface")]
        [Tooltip("The layer points are allowed to land on — pick Ground. Every point raycasts DOWN " +
                 "onto it, so points follow uneven ground and a MISS DISCARDS THE POINT, which is " +
                 "what keeps GeneratePlane's holes empty for free. Leave as Nothing to place " +
                 "everything flat at this object's Y instead. Needs real COLLIDERS: a generated " +
                 "plane has none unless you add a MeshCollider.")]
        public LayerMask layer = 0;

        [Tooltip("How far above the area the downward probe starts (and twice this far it travels). " +
                 "Must clear the tallest thing you want to land on.")]
        public float probeHeight = 50f;

        [Tooltip("Lifts every point off the surface, to stop z-fighting or sunken pivots.")]
        public float surfaceOffset = 0f;

        [Header("Prefabs")]
        [Tooltip("One is picked per point, uniformly at random.")]
        public GameObject[] prefabs;

        [Tooltip("Random Y rotation per instance. NOTE: has no effect on billboarded sprite props — " +
                 "Billboard.cs overwrites transform.rotation every LateUpdate. Use Horizontal Flip Chance " +
                 "to vary those instead.")]
        public bool randomYaw = true;

        [Tooltip("Fraction of instances mirrored left-to-right, so a single sprite reads as two. 0.5 = an " +
                 "even split, 0 = none. Applied as a NEGATIVE X SCALE, which is what survives billboarding " +
                 "(Billboard.cs writes only rotation). The sprite material must be Cull Off — Saga/Grass is " +
                 "— or mirrored instances turn their backs and vanish.")]
        [Range(0f, 1f)] public float horizontalFlipChance = 0.5f;

        [Tooltip("Uniform scale multiplier, picked between these two. Both 1 = no variation.")]
        public Vector2 scaleJitter = Vector2.one;

        [Tooltip("Tilt each instance to stand along the surface normal. Usually OFF for this game — " +
                 "billboarded sprite props (see Billboard.cs) want to stay upright.")]
        public bool alignToNormal = false;

        [Header("Preview")]
        [Tooltip("Draw a radius/2 disc per point. Discs may TOUCH but never OVERLAP — that is the " +
                 "Poisson guarantee made visible, and the fastest way to judge density.")]
        public bool showExclusionDiscs = true;

        [Tooltip("Cap on discs drawn in the Scene view, so a 5000-point preview stays interactive. " +
                 "The readout says when it truncates. Spawning is never truncated.")]
        [Range(100, 5000)] public int previewDrawLimit = 99000;

        [HideInInspector] public Transform spawnedRoot;

        readonly List<Vector3> points  = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        float derivedRadius;
        int rejected;

        public IReadOnlyList<Vector3> Points  => points;
        public IReadOnlyList<Vector3> Normals => normals;
        public float DerivedRadius => derivedRadius;
        public int Rejected => rejected;

        bool built;
        Vector2 lastSize; int lastCount, lastSeed, lastAttempts, lastLayer;
        float lastProbe, lastOffset;
        Vector3 lastPos; Quaternion lastRot;

        public Matrix4x4 AreaMatrix => Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        public void InvalidatePreview() => built = false;
        void OnValidate() => InvalidatePreview();

        public void EnsurePreview()
        {
            if (built && lastSize == areaSize && lastCount == targetCount && lastSeed == seed &&
                lastAttempts == attemptsPerPoint && lastLayer == layer.value &&
                lastProbe == probeHeight && lastOffset == surfaceOffset &&
                lastPos == transform.position && lastRot == transform.rotation) return;

            Rebuild();

            built = true;
            lastSize = areaSize; lastCount = targetCount; lastSeed = seed;
            lastAttempts = attemptsPerPoint; lastLayer = layer.value;
            lastProbe = probeHeight; lastOffset = surfaceOffset;
            lastPos = transform.position; lastRot = transform.rotation;
        }

        void Rebuild()
        {
            points.Clear();
            normals.Clear();
            rejected = 0;

            var samples = Solve();
            Matrix4x4 m = AreaMatrix;
            var corner = new Vector3(-areaSize.x * 0.5f, 0f, -areaSize.y * 0.5f);
            bool probe = layer.value != 0;
            float distance = probeHeight * 2f;

            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 world = m.MultiplyPoint3x4(corner + new Vector3(samples[i].x, 0f, samples[i].y));

                if (!probe)
                {
                    points.Add(world + Vector3.up * surfaceOffset);
                    normals.Add(Vector3.up);
                    continue;
                }

                if (Physics.Raycast(world + Vector3.up * probeHeight, Vector3.down, out RaycastHit hit,
                                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    (layer.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    points.Add(hit.point + Vector3.up * surfaceOffset);
                    normals.Add(hit.normal);
                }
                else rejected++;
            }
        }

        List<Vector2> Solve()
        {
            float area = Mathf.Max(areaSize.x * areaSize.y, 1e-4f);
            float r = Mathf.Sqrt(Packing * area / Mathf.Max(1, targetCount));
            List<Vector2> pts = null;

            for (int pass = 0; pass < 3; pass++)
            {
                pts = PoissonDisc.Sample(areaSize, r, attemptsPerPoint, new System.Random(seed), MaxPoints);
                if (pts.Count == 0) break;

                float error = pts.Count / (float)targetCount;
                if (Mathf.Abs(error - 1f) <= 0.05f) break;
                r *= Mathf.Sqrt(error);
            }
            derivedRadius = r;

            var trim = new System.Random(seed + 1);
            while (pts != null && pts.Count > targetCount)
            {
                int i = trim.Next(pts.Count);
                pts[i] = pts[pts.Count - 1];
                pts.RemoveAt(pts.Count - 1);
            }
            return pts ?? new List<Vector2>();
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = AreaMatrix;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, 0f, areaSize.y));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
