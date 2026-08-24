using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga
{
    /// <summary>
    /// Debug free-fly camera. WASD moves in the look direction (Q/E down/up), Left Shift boosts. Two
    /// rotation modes: hold Right Mouse for free-look (rotate in place), or Left-drag to orbit a point in
    /// front — grab-and-drag the scene around, like a 3D viewer. Reads the Input System low-level devices
    /// directly so it needs no action asset, and uses unscaled time so it still works when the game is paused.
    ///
    /// Drop it on a camera and enable the component to take over. If the camera has a CinemachineBrain,
    /// it's disabled while this is active and restored when you disable the component, so you can toggle
    /// debug flying on the main camera without fighting the rig.
    /// </summary>
    public class DebugFlyCamera : MonoBehaviour
    {
        [Header("Move")]
        [Tooltip("Base movement speed in units per second.")]
        [Range(1f, 50f)]
        [SerializeField] float moveSpeed = 8f;
        [Tooltip("Speed multiplier while Left Shift is held.")]
        [Range(1f, 10f)]
        [SerializeField] float sprintMultiplier = 3f;

        [Header("Look")]
        [Tooltip("Mouse-look sensitivity (degrees per pixel of mouse movement).")]
        [Range(0.01f, 1f)]
        [SerializeField] float lookSensitivity = 0.1f;
        [Tooltip("Invert vertical look.")]
        [SerializeField] bool invertY = false;
        [Tooltip("Only rotate while the right mouse button is held (otherwise look is always active).")]
        [SerializeField] bool holdRightMouseToLook = true;
        [Tooltip("Lock and hide the cursor while looking or orbiting.")]
        [SerializeField] bool lockCursorWhileLooking = true;

        [Header("Orbit")]
        [Tooltip("Orbit sensitivity (degrees per pixel of drag).")]
        [Range(0.01f, 1f)]
        [SerializeField] float orbitSensitivity = 0.2f;
        [Tooltip("Distance in front of the camera to the orbit pivot, in world units.")]
        [Range(1f, 100f)]
        [SerializeField] float orbitDistance = 12f;

        CinemachineBrain brain;
        float yaw;
        float pitch;

        void OnEnable()
        {
            // Seed yaw/pitch from the current orientation so enabling doesn't snap the view.
            SeedYawPitchFromTransform();

            // Take control away from Cinemachine while flying, if present.
            if (TryGetComponent(out brain)) brain.enabled = false;
        }

        void OnDisable()
        {
            if (brain != null) brain.enabled = true;
            if (lockCursorWhileLooking) SetCursorLocked(false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            bool looking = mouse != null && (!holdRightMouseToLook || mouse.rightButton.isPressed);
            bool orbiting = mouse != null && !looking && mouse.leftButton.isPressed;
            if (lockCursorWhileLooking) SetCursorLocked(looking || orbiting);

            // Re-seed yaw/pitch when a look begins so free-look picks up wherever orbiting (or anything
            // else) last left the camera, instead of snapping back to the old yaw/pitch.
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) SeedYawPitchFromTransform();

            // ---- Look (rotate in place) ----
            if (looking)
            {
                Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
                yaw += delta.x;
                pitch += invertY ? delta.y : -delta.y;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            // ---- Orbit (drag the scene around a pivot in front) ----
            else if (orbiting)
            {
                Vector2 delta = mouse.delta.ReadValue() * orbitSensitivity;
                Vector3 pivot = transform.position + transform.forward * orbitDistance;

                // Horizontal: swing around world up. Signs give a "grab the scene" feel — content follows
                // the cursor (drag right and the world turns right).
                transform.RotateAround(pivot, Vector3.up, delta.x);

                // Vertical: tilt around the camera's right axis, but reject the step if it would flip over
                // the pole (no explicit pitch angle to clamp when orbiting, so snapshot-and-revert).
                Vector3 prevPos = transform.position;
                Quaternion prevRot = transform.rotation;
                transform.RotateAround(pivot, transform.right, -delta.y);
                if (Mathf.Abs(transform.forward.y) > 0.999f)
                {
                    transform.position = prevPos;
                    transform.rotation = prevRot;
                }
            }

            // ---- Move ----
            if (keyboard == null) return;

            Vector3 dir = Vector3.zero;
            if (keyboard.wKey.isPressed) dir += transform.forward;
            if (keyboard.sKey.isPressed) dir -= transform.forward;
            if (keyboard.dKey.isPressed) dir += transform.right;
            if (keyboard.aKey.isPressed) dir -= transform.right;
            if (keyboard.eKey.isPressed) dir += Vector3.up;   // world up, so vertical stays vertical at any pitch
            if (keyboard.qKey.isPressed) dir -= Vector3.up;

            if (dir != Vector3.zero)
            {
                float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? sprintMultiplier : 1f);
                transform.position += dir.normalized * (speed * Time.unscaledDeltaTime);
            }
        }

        void SeedYawPitchFromTransform()
        {
            Vector3 e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;   // map 0..360 to a signed pitch
            pitch = Mathf.Clamp(pitch, -89f, 89f);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
