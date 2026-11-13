using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vice.World
{
    [AddComponentMenu("Vice/World/Line Prefab Spawner")]
    public class LinePrefabSpawner : MonoBehaviour
    {
        [Tooltip("One is picked per point, uniformly at random.")]
        public List<GameObject> prefabs = new List<GameObject>();

        [Tooltip("Line runs from this transform along its local +Z.")]
        [Min(0f)] public float length = 20f;

        [Tooltip("Spawns are spread evenly and include both endpoints.")]
        [Min(1)] public int count = 5;

        [Tooltip("Same seed = same prefab picks, every time. Change it to reroll.")]
        public int seed = 0;

        [Tooltip("Each spawn slides up to this many metres forward or back along the line. Clamped to the line's ends.")]
        [Min(0f)] public float alongJitter = 0f;

        [Tooltip("Each spawn shifts up to this many metres left or right of the line.")]
        [Min(0f)] public float sideJitter = 0f;

        public float gizmoRadius = 0.5f;

        // inputs the current children were built from, saved with the scene
        [SerializeField, HideInInspector] string spawnedSignature;

        Vector3 GetLocalPoint(int index)
        {
            // a single spawn sits mid-line; otherwise spawns span both endpoints
            float along = count == 1 ? 0.5f : index / (float)(count - 1);
            return Vector3.forward * (along * length);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // creating or destroying objects is refused inside OnValidate, so defer to the next editor tick
            EditorApplication.delayCall += Respawn;
        }

        void Respawn()
        {
            // the deferred call can outlive the component, e.g. after removing it or closing the scene
            if (this == null || Application.isPlaying) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;

            // OnValidate also fires on scene load and recompile; only rebuild when an input changed
            string signature = BuildSignature();
            if (signature == spawnedSignature) return;
            spawnedSignature = signature;
            Build(false);
        }

        string BuildSignature()
        {
            var parts = new List<string>
            {
                length.ToString("R"), count.ToString(), seed.ToString(),
                alongJitter.ToString("R"), sideJitter.ToString("R")
            };
            // asset paths stay stable across editor sessions, unlike instance ids
            foreach (GameObject prefab in prefabs)
                parts.Add(prefab == null ? "" : AssetDatabase.GetAssetPath(prefab));
            return string.Join("|", parts);
        }

        [ContextMenu("Respawn")]
        void ForceRespawn()
        {
            spawnedSignature = BuildSignature();
            Build(true);
        }

        [ContextMenu("Clear")]
        void ClearWithUndo() => Clear(true);

        void Build(bool recordUndo)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Clear(recordUndo);
            if (prefabs.Count == 0) return;

            // same seed gives the same picks every respawn
            var random = new System.Random(seed);
            // separate stream so changing jitter never reshuffles which prefab lands where
            var offsetRandom = new System.Random(seed + 1);
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = prefabs[random.Next(prefabs.Count)];
                Vector3 position = GetLocalPoint(i);
                position.z = Mathf.Clamp(position.z + RandomSigned(offsetRandom) * alongJitter, 0f, length);
                position.x += RandomSigned(offsetRandom) * sideJitter;
                if (prefab == null) continue;

                // keeps the prefab link so edits to the source prefab still propagate
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
                instance.transform.localPosition = position;
                if (recordUndo) Undo.RegisterCreatedObjectUndo(instance, "Spawn Along Line");
            }

            // one ctrl+z undoes the whole spawn instead of one instance at a time
            if (recordUndo) Undo.CollapseUndoOperations(undoGroup);
        }

        // maps NextDouble's [0, 1) onto [-1, 1) so offsets go both ways
        static float RandomSigned(System.Random random) => (float)random.NextDouble() * 2f - 1f;

        void Clear(bool recordUndo)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (recordUndo) Undo.DestroyObjectImmediate(child);
                else DestroyImmediate(child);
            }
        }
#endif

        void OnDrawGizmos()
        {
            // draw in local space so the line rotates and moves with the object
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * length);

            for (int i = 0; i < count; i++)
            {
                Vector3 point = GetLocalPoint(i);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(point, gizmoRadius);

                // the rectangle this spawn can land anywhere inside
                Gizmos.color = Color.cyan;
                if (alongJitter > 0f || sideJitter > 0f)
                    Gizmos.DrawWireCube(point, new Vector3(sideJitter * 2f, 0f, alongJitter * 2f));
            }
        }
    }
}
