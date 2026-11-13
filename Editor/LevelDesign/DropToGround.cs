using UnityEditor;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    // rests each selected object on the first collider below it, measured from the lowest point of its meshes rather than its pivot
    static class DropToGround
    {
        const string MenuPath = "Saga/Level Design/Drop To Ground";
        const string AlignedMenuPath = "Saga/Level Design/Drop To Ground (Align To Surface)";
        const float MaxDropDistance = 1000f;

        // a ray that starts exactly on a surface can miss it, so it starts a hair above the object's top
        const float StartClearance = 0.01f;

        [MenuItem(MenuPath)]
        static void Drop() => DropSelection(false);

        [MenuItem(AlignedMenuPath)]
        static void DropAligned() => DropSelection(true);

        [MenuItem(MenuPath, true)]
        static bool CanDrop() => Selection.transforms.Length > 0;

        [MenuItem(AlignedMenuPath, true)]
        static bool CanDropAligned() => Selection.transforms.Length > 0;

        static void DropSelection(bool alignToSurface)
        {
            // top-level scene objects only, so a selected parent and child don't both move
            Transform[] selected = Selection.transforms;
            Undo.RecordObjects(selected, alignToSurface ? "Drop To Ground (Aligned)" : "Drop To Ground");
            // edit-mode moves don't reach the physics scene until synced, so rays would hit where colliders used to be
            Physics.SyncTransforms();

            int missed = 0;
            foreach (Transform target in selected)
            {
                if (!DropOne(target, selected, alignToSurface)) missed++;
            }
            if (missed > 0) Debug.LogWarning($"Drop To Ground: {missed} object(s) had no collider below them and were left in place.");
        }

        static bool DropOne(Transform target, Transform[] selected, bool alignToSurface)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            Bounds worldBounds = WorldBounds(renderers, target.position);
            Vector3 origin = new Vector3(worldBounds.center.x, worldBounds.max.y + StartClearance, worldBounds.center.z);
            if (!TryFindGround(origin, selected, out RaycastHit ground)) return false;

            Vector3 up = Vector3.up;
            if (alignToSurface)
            {
                // rotates the object's own up onto the surface normal, keeping its heading as close as the tilt allows
                target.rotation = Quaternion.FromToRotation(target.up, ground.normal) * target.rotation;
                up = ground.normal;
            }

            // distance from the pivot to the object's lowest point along up; negative when the mesh hangs below the pivot
            float baseOffset = LowestAlong(renderers, target.position, up);
            float pivotHeightAboveGround = Vector3.Dot(target.position - ground.point, up);
            // slide along up only, so the object keeps its place on the surface and just sinks or rises onto it
            target.position += up * (-pivotHeightAboveGround - baseOffset);
            return true;
        }

        static bool TryFindGround(Vector3 origin, Transform[] selected, out RaycastHit ground)
        {
            ground = default;
            float nearestDistance = float.MaxValue;
            bool found = false;

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, MaxDropDistance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                // the object's own colliders, and other selected objects that are about to move anyway
                if (IsPartOfSelection(hit.collider.transform, selected)) continue;
                // RaycastAll returns hits in no particular order
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                ground = hit;
                found = true;
            }
            return found;
        }

        static bool IsPartOfSelection(Transform candidate, Transform[] selected)
        {
            foreach (Transform root in selected)
            {
                if (candidate.IsChildOf(root)) return true;
            }
            return false;
        }

        static Bounds WorldBounds(Renderer[] renderers, Vector3 fallback)
        {
            if (renderers.Length == 0) return new Bounds(fallback, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // projects every corner of every renderer's local box onto up; world AABBs would overstate the base of a tilted object
        static float LowestAlong(Renderer[] renderers, Vector3 pivot, Vector3 up)
        {
            if (renderers.Length == 0) return 0f;
            float lowest = float.MaxValue;
            foreach (Renderer renderer in renderers)
            {
                Bounds local = renderer.localBounds;
                Matrix4x4 localToWorld = renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    // bits of the corner index pick min or max on each axis, enumerating all 8 corners
                    Vector3 pick = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 worldCorner = localToWorld.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, pick));
                    lowest = Mathf.Min(lowest, Vector3.Dot(worldCorner - pivot, up));
                }
            }
            return lowest;
        }
    }
}
