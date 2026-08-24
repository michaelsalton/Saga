using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga
{
    /// <summary>
    /// Detached isometric camera: a fixed downward pitch with WASD panning over the ground plane
    /// (Left Shift boosts), and middle-mouse drag to spin the view around the point under screen
    /// centre — on release it eases to the nearest <see cref="snapAngle"/> multiple, so the camera
    /// always comes to rest on one of the eight isometric headings.
    ///
    /// Built as a sibling to <see cref="DebugFlyCamera"/>: reads the Input System's low-level devices
    /// directly so it needs no action asset, and runs on unscaled time so it still works when paused.
    /// Enable/disable the component to hand control over; <see cref="CameraModeController"/> does that.
    /// </summary>
    public class IsometricCamera : MonoBehaviour
    {
        [Header("Pan")]
        [Tooltip("Pan speed in units per second, along the camera's ground-projected axes.")]
        [Range(1f, 60f)]
        [SerializeField] float panSpeed = 12f;
        [Tooltip("Pan speed multiplier while Left Shift is held.")]
        [Range(1f, 10f)]
        [SerializeField] float sprintMultiplier = 3f;

        [Header("Rotate")]
        [Tooltip("Middle-drag sensitivity (degrees of yaw per pixel of drag).")]
        [Range(0.01f, 1f)]
        [SerializeField] float rotateSensitivity = 0.25f;
        [Tooltip("Rotation is snapped to multiples of this angle when the drag ends. 45 gives the " +
                 "eight classic isometric headings; 0 disables snapping.")]
        [SerializeField] float snapAngle = 45f;
        [Tooltip("Seconds the snap takes to ease into place after releasing the drag.")]
        [Range(0f, 1f)]
        [SerializeField] float snapDuration = 0.15f;

        [Header("Framing")]
        [Tooltip("Degrees the camera tilts down from horizontal. Held constant — dragging only yaws.")]
        [Range(5f, 89f)]
        [SerializeField] float pitch = 30f;
        [Tooltip("Height of the plane the camera orbits over. The pivot is where the view ray crosses it.")]
        [SerializeField] float groundHeight = 0f;
        [Tooltip("Pivot distance used when the view ray runs near-parallel to the ground plane.")]
        [Range(1f, 100f)]
        [SerializeField] float fallbackFocusDistance = 18f;

        bool rotating;
        bool snapping;
        Vector3 pivot;          // ground point the active drag / snap turns around
        float snapStartYaw;
        float snapTargetYaw;
        float snapTimer;

        float Yaw => transform.eulerAngles.y;

        void OnEnable()
        {
            rotating = false;
            snapping = false;

            // Whatever pose the previous mode left behind, come up on a true isometric angle.
            LevelToIsometric();
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // ---- Rotate (middle-drag, snaps to the nearest step on release) ----
            if (mouse != null)
            {
                if (mouse.middleButton.wasPressedThisFrame)
                {
                    rotating = true;
                    snapping = false;              // a fresh grab cancels an in-flight snap
                    pivot = ResolvePivot();
                }
                else if (mouse.middleButton.wasReleasedThisFrame && rotating)
                {
                    rotating = false;
                    BeginSnap();
                }

                // Yaw only — vertical drag is ignored so the isometric pitch stays locked.
                if (rotating)
                    transform.RotateAround(pivot, Vector3.up, mouse.delta.ReadValue().x * rotateSensitivity);
            }

            if (snapping) TickSnap();

            // ---- Pan (over the ground plane, along the camera's heading) ----
            if (keyboard == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 dir = Vector3.zero;
            if (keyboard.wKey.isPressed) dir += forward;
            if (keyboard.sKey.isPressed) dir -= forward;
            if (keyboard.dKey.isPressed) dir += right;
            if (keyboard.aKey.isPressed) dir -= right;

            if (dir == Vector3.zero) return;

            float speed = panSpeed * (keyboard.leftShiftKey.isPressed ? sprintMultiplier : 1f);
            Vector3 step = dir.normalized * (speed * Time.unscaledDeltaTime);
            transform.position += step;

            // Carry the pivot along, so panning mid-drag doesn't pull the rotation off its centre.
            pivot += step;
        }

        // Re-place the camera on the configured pitch and the nearest snap angle without moving the
        // point it's looking at: same pivot, same distance, corrected rotation.
        void LevelToIsometric()
        {
            Vector3 p = ResolvePivot();
            float distance = Vector3.Distance(transform.position, p);
            Quaternion rot = Quaternion.Euler(pitch, SnappedYaw(), 0f);
            transform.SetPositionAndRotation(p - rot * Vector3.forward * distance, rot);
        }

        // Where the view ray meets the ground plane. Falls back to a fixed distance in front when the
        // ray is near-parallel to the plane (or points away from it) and there's no usable crossing.
        Vector3 ResolvePivot()
        {
            Vector3 pos = transform.position;
            Vector3 fwd = transform.forward;

            if (fwd.y < -0.01f)
            {
                float t = (pos.y - groundHeight) / -fwd.y;
                if (t > 0f) return pos + fwd * t;
            }
            return pos + fwd * fallbackFocusDistance;
        }

        void BeginSnap()
        {
            snapStartYaw = Yaw;
            snapTargetYaw = SnappedYaw();
            snapTimer = 0f;

            if (snapDuration <= 0f)
            {
                RotateYawTo(snapTargetYaw);
                return;
            }
            snapping = true;
        }

        void TickSnap()
        {
            snapTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(snapTimer / snapDuration);
            float eased = t * t * (3f - 2f * t);   // smoothstep — eases out as it lands
            RotateYawTo(Mathf.LerpAngle(snapStartYaw, snapTargetYaw, eased));
            if (t >= 1f) snapping = false;
        }

        // Apply only the yaw still outstanding, about the cached pivot, so the focus point stays put.
        void RotateYawTo(float yaw)
        {
            transform.RotateAround(pivot, Vector3.up, Mathf.DeltaAngle(Yaw, yaw));
        }

        float SnappedYaw() => snapAngle > 0f ? Mathf.Round(Yaw / snapAngle) * snapAngle : Yaw;
    }
}
