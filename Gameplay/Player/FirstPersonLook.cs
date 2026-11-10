using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga.Gameplay
{
    // after PlayerMotor moves the body, before any LateUpdate (Billboard) reads the camera
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerMotor))]
    [DisallowMultipleComponent]
    public class FirstPersonLook : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        [Range(1f, 2f)]
        [SerializeField] float eyeHeight = 1.6f;
        // eye stays below the crouched capsule top by more than the near clip plane
        [Range(0.5f, 1.5f)]
        [SerializeField] float crouchedEyeHeight = 0.85f;
        // how quickly the eye follows the capsule when crouching, standing or stepping up a stair
        [Range(1f, 30f)]
        [SerializeField] float crouchSharpness = 12f;

        // the camera this player looks through, handed over by CameraModeController
        public Transform View { get; set; }

        PlayerMotor motor;
        CharacterController controller;
        HeadBob headBob;
        IdleSway idleSway;
        LandingDip landingDip;
        Interactor interactor;
        InputAction lookAction;
        float pitch;
        float currentEyeHeight;
        Vector3 lastBodyPosition;
        bool wasGrounded;

        void OnEnable()
        {
            // looked up here, not Awake: the camera may enable us before this object has awoken
            motor = GetComponent<PlayerMotor>();
            controller = GetComponent<CharacterController>();
            headBob = GetComponent<HeadBob>();
            idleSway = GetComponent<IdleSway>();
            landingDip = GetComponent<LandingDip>();
            interactor = GetComponent<Interactor>();
            lookAction = controls.FindAction("Player/Look", throwIfNotFound: true);

            pitch = 0f;
            lookAction.Enable();
            motor.enabled = true;
            if (interactor != null) interactor.enabled = true;
            SetCursorLocked(true);
            currentEyeHeight = motor.IsCrouching ? crouchedEyeHeight : eyeHeight;
            lastBodyPosition = transform.position;
            wasGrounded = false;
            FollowEyes(0f);
        }

        void OnDisable()
        {
            lookAction.Disable();
            motor.enabled = false;
            if (interactor != null) interactor.enabled = false;
            SetCursorLocked(false);
        }

        void Update()
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            // mouse delta is already a distance; a stick is a rate and needs time to become one
            bool fromMouse = lookAction.activeControl?.device is Mouse;
            var settings = GameSettings.Current;
            Vector2 degrees = fromMouse ? look * settings.MouseSensitivity : look * (settings.StickSensitivity * Time.deltaTime);

            transform.Rotate(0f, degrees.x, 0f, Space.Self);
            pitch += settings.InvertY ? degrees.y : -degrees.y;
            // past ±90 the view flips upside down
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            FollowEyes(Time.deltaTime);
        }

        void FollowEyes(float deltaTime)
        {
            if (View == null) return;
            var settings = GameSettings.Current;
            Vector3 eyeOffset = settings.HeadBob && headBob != null && headBob.enabled ? headBob.Tick(motor, deltaTime) : Vector3.zero;
            // ticked even when hidden, so landings while the setting is off don't bank up and fire when it's turned on
            Vector3 dipOffset = landingDip != null && landingDip.enabled ? landingDip.Tick(deltaTime) : Vector3.zero;
            // same comfort toggle as the bob: both are camera motion the player didn't cause directly
            if (settings.HeadBob) eyeOffset += dipOffset;
            // body frame, not camera frame, so the bob stays vertical when you look at the floor
            float targetEyeHeight = motor.IsCrouching ? crouchedEyeHeight : eyeHeight;
            currentEyeHeight -= StepRise();
            // a run up stairs stacks one hold-back per step; capped so the view never trails more than one step
            currentEyeHeight = Mathf.Max(currentEyeHeight, targetEyeHeight - controller.stepOffset);
            // capsule snaps, eye eases: collision stays exact while the view moves smoothly
            currentEyeHeight = Mathf.Lerp(currentEyeHeight, targetEyeHeight, 1f - Mathf.Exp(-crouchSharpness * deltaTime));
            Vector3 eyePosition = transform.position + transform.up * currentEyeHeight + transform.rotation * eyeOffset;
            Quaternion swayRotation = settings.IdleSway && idleSway != null && idleSway.enabled ? idleSway.Tick(motor, deltaTime) : Quaternion.identity;
            // sway multiplied last so it acts in the view's own frame and never feeds back into pitch
            View.SetPositionAndRotation(eyePosition, transform.rotation * Quaternion.Euler(pitch, 0f, 0f) * swayRotation);
        }

        // how far the controller's step offset lifted the body since last frame, 0 if it didn't
        float StepRise()
        {
            Vector3 bodyPosition = transform.position;
            Vector3 displacement = bodyPosition - lastBodyPosition;
            lastBodyPosition = bodyPosition;
            bool grounded = motor.IsGrounded;
            bool groundedBothFrames = grounded && wasGrounded;
            wasGrounded = grounded;
            // jumps and falls move the eye with the body; only a grounded climb can be a stair
            if (!groundedBothFrames) return 0f;

            float horizontalDistance = new Vector2(displacement.x, displacement.z).magnitude;
            // the most the steepest walkable slope could raise you over that distance; slopes stay unsmoothed
            float slopeRise = horizontalDistance * Mathf.Tan(controller.slopeLimit * Mathf.Deg2Rad);
            float stepRise = displacement.y - slopeRise;
            // taller than any step means a teleport, which should cut, not glide
            if (stepRise <= 0f || stepRise > controller.stepOffset) return 0f;
            return stepRise;
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
