using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga.Gameplay
{
    // Editor-style fly camera: hold right mouse to look, WASD to move, Q/E down/up, Left Shift to boost.
    [AddComponentMenu("Saga/Gameplay/Free Fly Camera")]
    [DisallowMultipleComponent]
    public class FreeFlyCamera : MonoBehaviour
    {
        [Tooltip("Movement speed in world units per second.")]
        [Range(0.5f, 50f)]
        [SerializeField] float moveSpeed = 8f;
        [Tooltip("Speed multiplier while Left Shift is held.")]
        [Range(1f, 10f)]
        [SerializeField] float boostMultiplier = 3f;
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [Range(0.01f, 1f)]
        [SerializeField] float lookSensitivity = 0.1f;

        float yaw;
        float pitch;

        void OnEnable()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            // eulerAngles reports 0..360; looking slightly up comes back as ~350
            pitch = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -89f, 89f);
        }

        void OnDisable()
        {
            // disabling mid-look would otherwise leave the cursor captured
            SetCursorLocked(false);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame) SetCursorLocked(true);
            if (mouse.rightButton.wasReleasedThisFrame) SetCursorLocked(false);

            if (mouse.rightButton.isPressed)
            {
                // already pixels-this-frame, so no deltaTime
                Vector2 mouseDelta = mouse.delta.ReadValue() * lookSensitivity;
                yaw += mouseDelta.x;
                // screen Y is up, but positive pitch tilts the camera down
                pitch = Mathf.Clamp(pitch - mouseDelta.y, -89f, 89f);
                // rebuilt from two angles every frame so roll can never accumulate
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed) direction += transform.forward;
            if (keyboard.sKey.isPressed) direction -= transform.forward;
            if (keyboard.dKey.isPressed) direction += transform.right;
            if (keyboard.aKey.isPressed) direction -= transform.right;
            // world up, so rising stays vertical even while pitched down
            if (keyboard.eKey.isPressed) direction += Vector3.up;
            if (keyboard.qKey.isPressed) direction -= Vector3.up;

            float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? boostMultiplier : 1f);
            // unscaled so it still flies while the game is paused (timeScale 0)
            transform.position += direction.normalized * (speed * Time.unscaledDeltaTime);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
