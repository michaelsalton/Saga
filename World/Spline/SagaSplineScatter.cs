using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Saga.World
{
    // spawns prefab instances in evenly spaced slots along every spline in a container; instances are saved children, rebuilt in the editor whenever an input or the spline changes
    [ExecuteAlways]
    [AddComponentMenu("Saga/World/Spline Scatter")]
    public class SagaSplineScatter : MonoBehaviour
    {
        public enum Facing { AlongRoad, FaceRoad, Random }

        [System.Serializable]
        public struct Item
        {
            public GameObject prefab;

            [Tooltip("Relative chance against the other items. 3 and 1 is 75% and 25%.")]
            [Min(0f)] public float weight;
        }

        [Tooltip("The spline to follow. Defaults to one on this GameObject or a parent.")]
        [SerializeField] SplineContainer container;

        [Tooltip("Every child of this GameObject is owned by the scatter and destroyed on rebuild. Hand edits to them don't survive.")]
        [SerializeField] List<Item> items = new List<Item> { new Item { weight = 1f } };

        [Tooltip("Right and Left are looking along the spline. The left side uses the flipped frame, so a prop that faces the road on the right faces it on the left too.")]
        [SerializeField] SplineSide side = SplineSide.Both;

        [Tooltip("Metres from the spline to each prop's pivot.")]
        [SerializeField] float sideOffset = 7f;

        [Tooltip("Metres above the spline, e.g. the sidewalk's height above the road.")]
        [SerializeField] float heightOffset = 0f;

        [Header("Distribution")]
        [Tooltip("Metres between slots. Sets the rhythm; changing it reshuffles everything, since the slots themselves move.")]
        [Min(0.1f)]
        [SerializeField] float spacing = 10f;

        [Tooltip("Chance each slot gets a prop. Lowering it only removes props; the ones left keep their prefab, position and rotation.")]
        [Range(0f, 1f)]
        [SerializeField] float fill = 1f;

        [Tooltip("How far a prop can slide along the road, as a fraction of Spacing. 1 lets neighbours touch but never swap order.")]
        [Range(0f, 1f)]
        [SerializeField] float jitter = 0f;

        [Tooltip("Metres kept clear at the start of each spline, e.g. for an intersection.")]
        [Min(0f)]
        [SerializeField] float startMargin = 0f;

        [Tooltip("Metres kept clear at the end of each spline.")]
        [Min(0f)]
        [SerializeField] float endMargin = 0f;

        [Header("Variation")]
        [Tooltip("Along Road points the prefab's +Z along the spline (cars). Face Road points +Z at the spline (lamps, signs). Random spins it freely (trees).")]
        [SerializeField] Facing facing = Facing.AlongRoad;

        [Tooltip("Degrees of random turn around the vertical, either way, added on top of Facing.")]
        [Range(0f, 180f)]
        [SerializeField] float yawJitter = 0f;

        [Tooltip("Uniform scale picked between these, multiplied onto the prefab's own scale.")]
        [SerializeField] Vector2 scaleRange = Vector2.one;

        [Tooltip("Same seed, same result. Change it to reroll.")]
        [SerializeField] int seed = 0;

        // inputs and spline knots the current children were built from, saved with the scene
        [SerializeField, HideInInspector] string spawnedSignature;

#if UNITY_EDITOR
        bool rebuildQueued;

        void OnEnable()
        {
            if (container == null) container = GetComponentInParent<SplineContainer>();
            Spline.Changed += OnSplineChanged;
            // undo writes spline data back directly without raising Spline.Changed
            Undo.undoRedoPerformed += QueueRebuild;
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            Undo.undoRedoPerformed -= QueueRebuild;
        }

        void OnValidate() => QueueRebuild();

        // fires for every spline in every scene, so only rebuild for ours
        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (container == null) return;
            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                if (container.Splines[splineIndex] != spline) continue;
                QueueRebuild();
                return;
            }
        }

        // objects can't be created inside OnValidate or a spline drag, and a drag raises many changes per frame, so they collapse into one deferred rebuild
        void QueueRebuild()
        {
            if (rebuildQueued) return;
            rebuildQueued = true;
            EditorApplication.delayCall += RebuildIfChanged;
        }

        void RebuildIfChanged()
        {
            rebuildQueued = false;
            // the deferred call can outlive the component, e.g. after removing it or closing the scene
            if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;

            // OnValidate also fires on scene load and recompile; only rebuild when something the layout depends on changed
            string signature = BuildSignature();
            if (signature == spawnedSignature) return;
            spawnedSignature = signature;
            Build(false);
            // changes made without undo don't dirty the scene, so the new children and signature would be silently dropped on save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        [ContextMenu("Rebuild")]
        void ForceRebuild()
        {
            spawnedSignature = BuildSignature();
            Build(true);
        }

        [ContextMenu("Clear")]
        void ClearWithUndo() => Clear(true);

        string BuildSignature()
        {
            var signature = new StringBuilder();
            signature.Append(side).Append('|').Append(sideOffset.ToString("R")).Append('|').Append(heightOffset.ToString("R"));
            signature.Append('|').Append(spacing.ToString("R")).Append('|').Append(fill.ToString("R")).Append('|').Append(jitter.ToString("R"));
            signature.Append('|').Append(startMargin.ToString("R")).Append('|').Append(endMargin.ToString("R"));
            signature.Append('|').Append(facing).Append('|').Append(yawJitter.ToString("R")).Append('|').Append(scaleRange.ToString("R")).Append('|').Append(seed);

            // asset paths stay stable across editor sessions, unlike instance ids
            foreach (Item item in items)
                signature.Append('|').Append(item.prefab == null ? "" : AssetDatabase.GetAssetPath(item.prefab)).Append(':').Append(item.weight.ToString("R"));

            if (container == null) return signature.ToString();
            foreach (Spline spline in container.Splines)
            {
                signature.Append("|spline").Append(spline.Closed);
                for (int i = 0; i < spline.Count; i++)
                {
                    BezierKnot knot = spline[i];
                    signature.Append('|').Append(knot.Position).Append(knot.TangentIn).Append(knot.TangentOut).Append(knot.Rotation).Append(spline.GetTangentMode(i));
                }
            }
            return signature.ToString();
        }

        void Build(bool recordUndo)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Clear(recordUndo);
            if (container == null) return;

            float totalWeight = 0f;
            foreach (Item item in items) totalWeight += item.weight;
            if (totalWeight <= 0f) return;

            for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
            {
                float length = container.CalculateLength(splineIndex);
                float usableLength = length - startMargin - endMargin;
                if (length <= 0f || usableLength < 0f) continue;
                int slotCount = Mathf.FloorToInt(usableLength / spacing) + 1;

                for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    bool left = sideIndex == 1;
                    if (left && side == SplineSide.Right) continue;
                    if (!left && side == SplineSide.Left) continue;

                    for (int slot = 0; slot < slotCount; slot++)
                    {
                        // one generator per slot, so a slot's rolls never depend on how many props came before it
                        var random = new System.Random(SlotSeed(splineIndex, sideIndex, slot));
                        // every roll is drawn even for empty slots, so lowering fill never changes the props that remain
                        float fillRoll = (float)random.NextDouble();
                        float itemRoll = (float)random.NextDouble() * totalWeight;
                        float jitterRoll = (float)random.NextDouble() * 2f - 1f;
                        float yawRoll = (float)random.NextDouble() * 2f - 1f;
                        float scaleRoll = (float)random.NextDouble();
                        if (fillRoll >= fill) continue;

                        GameObject prefab = PickPrefab(itemRoll);
                        if (prefab == null) continue;

                        // half the spacing either way is the most a prop can slide without passing its neighbour
                        float distance = startMargin + slot * spacing + jitterRoll * jitter * spacing * 0.5f;
                        distance = Mathf.Clamp(distance, startMargin, length - endMargin);
                        container.Evaluate(splineIndex, distance / length, out float3 position, out float3 tangent, out float3 _);

                        // flattened so props stay upright on slopes, matching the road and curbs
                        Vector3 forward = Vector3.ProjectOnPlane(tangent, Vector3.up);
                        forward = forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
                        // the left side uses the flipped frame, so right points away from the road on both sides
                        if (left) forward = -forward;
                        Vector3 right = Vector3.Cross(Vector3.up, forward);

                        Quaternion rotation = facing switch
                        {
                            Facing.FaceRoad => Quaternion.LookRotation(-right, Vector3.up),
                            Facing.Random => Quaternion.AngleAxis(yawRoll * 180f, Vector3.up),
                            _ => Quaternion.LookRotation(forward, Vector3.up),
                        };
                        rotation = Quaternion.AngleAxis(yawRoll * yawJitter, Vector3.up) * rotation;

                        // keeps the prefab link so edits to the source prefab still propagate
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
                        instance.transform.SetPositionAndRotation((Vector3)position + right * sideOffset + Vector3.up * heightOffset, rotation);
                        instance.transform.localScale = prefab.transform.localScale * Mathf.Lerp(scaleRange.x, scaleRange.y, scaleRoll);
                        if (recordUndo) Undo.RegisterCreatedObjectUndo(instance, "Spline Scatter");
                    }
                }
            }

            // one ctrl+z undoes the whole rebuild instead of one instance at a time
            if (recordUndo) Undo.CollapseUndoOperations(undoGroup);
        }

        // a fixed integer hash; System.HashCode is reseeded every editor session and would reshuffle the layout on each restart
        int SlotSeed(int splineIndex, int sideIndex, int slot)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash = hash * 486187739u + (uint)splineIndex;
                hash = hash * 486187739u + (uint)sideIndex;
                hash = hash * 486187739u + (uint)slot;
                // murmur3 finalizer: neighbouring slots would otherwise get consecutive seeds, and System.Random's first rolls from consecutive seeds are correlated
                hash ^= hash >> 16;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 13;
                hash *= 0xc2b2ae35u;
                hash ^= hash >> 16;
                return (int)hash;
            }
        }

        // walks the weights until the roll lands inside one
        GameObject PickPrefab(float itemRoll)
        {
            foreach (Item item in items)
            {
                if (itemRoll < item.weight) return item.prefab;
                itemRoll -= item.weight;
            }
            return items[items.Count - 1].prefab;
        }

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
    }
}
