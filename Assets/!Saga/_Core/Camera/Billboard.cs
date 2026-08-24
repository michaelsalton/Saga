using UnityEngine;

namespace Saga
{
    /// <summary>
    /// Turns an object to face the camera — the classic trick for sprites/quads living in a 3D world.
    /// Attach to any sprite prop (e.g. the ones scattered by ScatterVolume).
    ///
    /// Tuned for Saga's ORTHOGRAPHIC angled top-down camera: under orthographic projection every view
    /// ray is parallel, so a billboard must align to the camera's ROTATION, not point at its position
    /// (pointing at the position would fan edge sprites inward). Because that rotation is identical for
    /// every billboard on screen, it's computed once per frame and shared across all instances — a whole
    /// scattered forest costs one calculation plus one transform write each.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Saga/Billboard")]
    public class Billboard : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Stay upright, only yaw to face the camera. Best for ground props (trees, NPCs).</summary>
            Upright,
            /// <summary>Match the camera fully so the sprite plane is parallel to the screen (coins, pickups, FX).</summary>
            Full,
        }

        [Tooltip("Upright: stays vertical and only spins to face the camera (natural for trees/characters). " +
                 "Full: tilts to lie flat against the screen (good for pickups, particles, FX).")]
        [SerializeField] Mode mode = Mode.Upright;

        [Tooltip("Add a 180° flip if the art ends up showing its back / mirrored (quads and sprites differ " +
                 "on which side is the front face).")]
        [SerializeField] bool flip = false;

        [Tooltip("Camera to face. Leave empty to use Camera.main (the shared, per-frame fast path).")]
        [SerializeField] Camera cameraOverride;

        // Shared per-frame cache: in ortho the facing rotation is the same for all billboards, so we
        // compute it once and let every instance read it. Keyed by frame so it refreshes each tick.
        static int s_cachedFrame = -1;
        static bool s_hasCamera;
        static Quaternion s_upright;
        static Quaternion s_full;

        void LateUpdate()
        {
            Quaternion upright, full;
            bool ok;

            if (cameraOverride != null)
            {
                ok = ComputeRotations(cameraOverride, out upright, out full);   // override: compute locally
            }
            else
            {
                if (s_cachedFrame != Time.frameCount)                            // main camera: shared cache
                {
                    s_cachedFrame = Time.frameCount;
                    s_hasCamera = ComputeRotations(Camera.main, out s_upright, out s_full);
                }
                ok = s_hasCamera;
                upright = s_upright;
                full = s_full;
            }

            if (!ok) return;

            Quaternion rot = mode == Mode.Upright ? upright : full;
            if (flip) rot *= Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = rot;
        }

        static bool ComputeRotations(Camera cam, out Quaternion upright, out Quaternion full)
        {
            upright = Quaternion.identity;
            full = Quaternion.identity;
            if (cam == null) return false;

            Transform ct = cam.transform;
            full = ct.rotation;

            // Upright: keep world up, yaw to the camera's horizontal heading. If the camera looks straight
            // down there's no heading to use, so fall back to the full facing.
            Vector3 fwd = ct.forward;
            fwd.y = 0f;
            upright = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd, Vector3.up) : full;
            return true;
        }
    }
}
