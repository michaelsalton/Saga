using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Saga.World
{
    // extrudes flat material strips along every spline in a container; the mesh is rebuilt on enable and never saved with the scene
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Saga/World/Road")]
    public class SagaRoad : MonoBehaviour
    {
        [System.Serializable]
        public struct Strip
        {
            [Tooltip("Metres across the road.")]
            [Min(0.01f)] public float width;

            public Material material;

            [Tooltip("Flips the texture across this strip, so one edge texture serves both sides of the road.")]
            public bool mirror;
        }

        [Tooltip("The spline to follow. Defaults to one on this GameObject. If it lives on another object, moving that object doesn't rebuild the road; use Rebuild from the context menu.")]
        [SerializeField] SplineContainer container;

        [Tooltip("Laid left to right looking along the spline, centred on it. Each strip is its own submesh and material slot.")]
        [SerializeField] List<Strip> strips = new List<Strip>
        {
            new Strip { width = 4f, mirror = true },
            new Strip { width = 4f },
            new Strip { width = 4f },
        };

        [Tooltip("Metres of road per texture repeat along its length. Equal to a strip's width keeps a square texture's texels square.")]
        [Min(0.01f)]
        [SerializeField] float tileLength = 4f;

        [Tooltip("Cross-sections per metre of spline. Straight roads need very few; tight curves start to look faceted below about 1.")]
        [Range(0.05f, 8f)]
        [SerializeField] float segmentsPerMetre = 1f;

        Mesh mesh;

        void OnEnable()
        {
            if (container == null) container = GetComponent<SplineContainer>();
            mesh = new Mesh { name = "SagaRoad", hideFlags = HideFlags.DontSave };
            GetComponent<MeshFilter>().sharedMesh = mesh;

            Spline.Changed += OnSplineChanged;
#if UNITY_EDITOR
            // undo writes spline data back directly without raising Spline.Changed
            UnityEditor.Undo.undoRedoPerformed += Rebuild;
#endif
            Rebuild();
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed -= Rebuild;
#endif
            if (TryGetComponent(out MeshFilter filter)) filter.sharedMesh = null;
            DestroyObject(mesh);
        }

        // deferred because assigning renderer materials inside OnValidate logs SendMessage warnings
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
#endif
        }

        // fires for every spline in every scene, so only rebuild for ours
        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (container == null) return;
            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                if (container.Splines[splineIndex] != spline) continue;
                Rebuild();
                return;
            }
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (mesh == null) return;
            mesh.Clear();
            if (container == null || strips.Count == 0) return;

            float totalWidth = 0f;
            foreach (Strip strip in strips) totalWidth += strip.width;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangleLists = new List<int>[strips.Count];
            for (int i = 0; i < strips.Count; i++) triangleLists[i] = new List<int>();

            // the road is flat, so every vertex shares one normal and nothing gets averaged across strips
            Vector3 localUp = transform.InverseTransformDirection(Vector3.up);

            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                float length = container.CalculateLength(splineIndex);
                if (length <= 0f) continue;
                int ringCount = Mathf.Max(2, Mathf.CeilToInt(length * segmentsPerMetre) + 1);

                // sampled once and shared, so neighbouring strips' border vertices land on identical positions and no cracks open on bends
                var centres = new Vector3[ringCount];
                var rights = new Vector3[ringCount];
                Vector3 previousForward = Vector3.forward;
                for (int ring = 0; ring < ringCount; ring++)
                {
                    // the package's spline t is already distance-normalized, so even t steps are even metres
                    float splineT = ring / (float)(ringCount - 1);
                    container.Evaluate(splineIndex, splineT, out float3 position, out float3 tangent, out float3 _);

                    // flattened so knot rotation and slopes can't bank the road sideways
                    Vector3 forward = Vector3.ProjectOnPlane(tangent, Vector3.up);
                    // a vertical tangent has no ground direction, so hold the last one
                    forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : previousForward;
                    previousForward = forward;

                    centres[ring] = position;
                    rights[ring] = Vector3.Cross(Vector3.up, forward);
                }

                float stripStart = -totalWidth * 0.5f;
                for (int i = 0; i < strips.Count; i++)
                {
                    Strip strip = strips[i];
                    // each strip gets its own copy of its border vertices, since one vertex can't carry v = 1 for one strip and v = 0 for the next
                    int firstVertex = vertices.Count;

                    for (int ring = 0; ring < ringCount; ring++)
                    {
                        float distance = ring / (float)(ringCount - 1) * length;
                        for (int side = 0; side < 2; side++)
                        {
                            float offset = stripStart + side * strip.width;
                            // Evaluate returns world space, the mesh lives in this object's local space
                            vertices.Add(transform.InverseTransformPoint(centres[ring] + rights[ring] * offset));
                            normals.Add(localUp);
                            float across = strip.mirror ? 1 - side : side;
                            // u runs along the road because the road textures are authored with their lines along u
                            uvs.Add(new Vector2(distance / tileLength, across));
                        }
                    }

                    List<int> triangles = triangleLists[i];
                    for (int ring = 0; ring < ringCount - 1; ring++)
                    {
                        int currentLeft = firstVertex + ring * 2;
                        int nextLeft = currentLeft + 2;
                        // clockwise seen from above, so the top face is the front face
                        triangles.Add(currentLeft);
                        triangles.Add(nextLeft);
                        triangles.Add(nextLeft + 1);
                        triangles.Add(currentLeft);
                        triangles.Add(nextLeft + 1);
                        triangles.Add(currentLeft + 1);
                    }

                    stripStart += strip.width;
                }
            }

            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = strips.Count;
            for (int i = 0; i < strips.Count; i++) mesh.SetTriangles(triangleLists[i], i);
            // normal maps in Saga/Standard need tangents; built from how u changes across each triangle, so they follow the road around bends
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // one material slot per submesh, matched by index
            var materials = new Material[strips.Count];
            for (int i = 0; i < strips.Count; i++) materials[i] = strips[i].material;
            GetComponent<MeshRenderer>().sharedMaterials = materials;
        }

        static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
