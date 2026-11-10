using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Saga.World
{
    // bends repeated copies of a modelled mesh along every spline in a container, keeping its UVs; the mesh is rebuilt on enable and never saved with the scene
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Saga/World/Spline Mesh")]
    public class SagaSplineMesh : MonoBehaviour
    {
        [Tooltip("The spline to follow. Defaults to one on this GameObject or a parent. If it lives elsewhere, moving it doesn't rebuild the mesh; use Rebuild from the context menu.")]
        [SerializeField] SplineContainer container;

        [Tooltip("Modelled with its length along local Z, X pointing away from the spline, Y up. Needs Read/Write enabled on import or player builds can't rebuild it.")]
        [SerializeField] Mesh sourceMesh;

        [Tooltip("One per submesh of the source mesh, matched by index.")]
        [SerializeField] Material[] materials = new Material[1];

        [Tooltip("Right and Left are looking along the spline. Left is the same mesh turned 180 degrees, so the face that points at the spline on the right still points at it on the left.")]
        [SerializeField] SplineSide side = SplineSide.Both;

        [Tooltip("Metres from the spline to the source mesh's pivot. Half the road width puts a curb whose pivot is on its road-side face flush with the road edge.")]
        [SerializeField] float sideOffset = 6f;

        Mesh mesh;

        void OnEnable()
        {
            if (container == null) container = GetComponentInParent<SplineContainer>();
            mesh = new Mesh { name = "SagaSplineMesh", hideFlags = HideFlags.DontSave };
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
            if (container == null || sourceMesh == null) return;
            if (!sourceMesh.isReadable && Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(SagaSplineMesh)}] {sourceMesh.name} needs Read/Write enabled in its import settings.", this);
                return;
            }

            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector4[] sourceTangents = sourceMesh.tangents;
            Vector2[] sourceUvs = sourceMesh.uv;
            bool hasNormals = sourceNormals.Length == sourceVertices.Length;
            bool hasTangents = sourceTangents.Length == sourceVertices.Length;
            bool hasUvs = sourceUvs.Length == sourceVertices.Length;

            int subMeshCount = sourceMesh.subMeshCount;
            var sourceTriangles = new int[subMeshCount][];
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++) sourceTriangles[subMesh] = sourceMesh.GetTriangles(subMesh);

            Bounds bounds = sourceMesh.bounds;
            float pieceLength = bounds.size.z;
            if (pieceLength <= 0f) return;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var triangleLists = new List<int>[subMeshCount];
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++) triangleLists[subMesh] = new List<int>();

            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                float length = container.CalculateLength(splineIndex);
                if (length <= 0f) continue;

                int pieceCount = Mathf.Max(1, Mathf.RoundToInt(length / pieceLength));
                // whole pieces, stretched slightly so the last one ends exactly at the spline's end
                float stretch = length / (pieceCount * pieceLength);

                for (int pass = 0; pass < 2; pass++)
                {
                    // the left copy walks the spline backwards, which turns the whole mesh 180 degrees and keeps its winding
                    bool backwards = pass == 1;
                    if (backwards && side == SplineSide.Right) continue;
                    if (!backwards && side == SplineSide.Left) continue;

                    for (int piece = 0; piece < pieceCount; piece++)
                    {
                        int firstVertex = vertices.Count;
                        for (int i = 0; i < sourceVertices.Length; i++)
                        {
                            Vector3 source = sourceVertices[i];
                            // the end vertices of neighbouring pieces map to the same distance, so the pieces meet with no gap
                            float distance = (piece * pieceLength + source.z - bounds.min.z) * stretch;
                            float splineT = Mathf.Clamp01(distance / length);
                            if (backwards) splineT = 1f - splineT;
                            container.Evaluate(splineIndex, splineT, out float3 position, out float3 tangent, out float3 _);

                            // flattened so pieces stay upright on slopes and knot rotation can't roll them
                            Vector3 forward = Vector3.ProjectOnPlane(tangent, Vector3.up);
                            forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
                            if (backwards) forward = -forward;
                            Vector3 right = Vector3.Cross(Vector3.up, forward);

                            // Evaluate returns world space, the mesh lives in this object's local space
                            Vector3 world = (Vector3)position + right * (sideOffset + source.x) + Vector3.up * source.y;
                            vertices.Add(transform.InverseTransformPoint(world));

                            // the source's x/y/z become the curve's right/up/forward here; normals and tangents turn with it or lighting keeps facing the unbent direction
                            Quaternion frame = Quaternion.LookRotation(forward, Vector3.up);
                            if (hasNormals) normals.Add(transform.InverseTransformDirection(frame * sourceNormals[i]));
                            if (hasTangents)
                            {
                                Vector3 bentTangent = transform.InverseTransformDirection(frame * (Vector3)sourceTangents[i]);
                                // w is the handedness sign of the bitangent, which a rotation doesn't change
                                tangents.Add(new Vector4(bentTangent.x, bentTangent.y, bentTangent.z, sourceTangents[i].w));
                            }
                            if (hasUvs) uvs.Add(sourceUvs[i]);
                        }

                        // same triangles every copy, shifted onto this copy's vertices
                        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                            foreach (int index in sourceTriangles[subMesh]) triangleLists[subMesh].Add(firstVertex + index);
                    }
                }
            }

            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            if (hasUvs) mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subMeshCount;
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++) mesh.SetTriangles(triangleLists[subMesh], subMesh);
            if (hasNormals) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            // normal maps in Saga/Standard need tangents; rebuilt from the UVs when the source didn't import any
            if (hasTangents) mesh.SetTangents(tangents);
            else if (hasUvs) mesh.RecalculateTangents();
            mesh.RecalculateBounds();

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
