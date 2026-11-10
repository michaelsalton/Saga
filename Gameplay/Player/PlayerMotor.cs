using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Saga.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        // renamed from walkSpeed: this is the default jog, walking is now a fraction of it
        [FormerlySerializedAs("walkSpeed")]
        [Range(0.5f, 10f)]
        [SerializeField] float moveSpeed = 4f;
        [Range(0.1f, 1f)]
        [SerializeField] float walkMultiplier = 0.5f;
        [Range(1f, 4f)]
        [SerializeField] float sprintMultiplier = 1.8f;
        [Range(0.6f, 1.6f)]
        [SerializeField] float crouchHeight = 1f;
        [Range(0.1f, 1f)]
        [SerializeField] float crouchMultiplier = 0.5f;
        // what can stop you standing up; untick the player's own layer
        [SerializeField] LayerMask standBlockers = ~0;
        [Range(0.1f, 3f)]
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -20f;
        // velocity change per second; high is snappy, low is icy
        [SerializeField] float groundAcceleration = 40f;
        // low so you can steer mid-air but not reverse a jump
        [SerializeField] float airAcceleration = 8f;

        CharacterController controller;
        InputAction moveAction;
        InputAction sprintAction;
        InputAction jumpAction;
        InputAction walkAction;
        InputAction crouchAction;
        bool walking;
        float standingHeight;
        Vector3 horizontalVelocity;
        float verticalVelocity;

        // displacement from the last Move after collisions; zero when pushing into a wall
        // the controller reports its last Move forever, so a disabled motor would look like it's still running
        public Vector3 Velocity => enabled ? controller.velocity : Vector3.zero;
        public bool IsGrounded => controller.isGrounded;
        public bool IsCrouching { get; private set; }
        // fall speed in m/s at the moment of touchdown, always positive
        public event Action<float> Landed;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            standingHeight = controller.height;
            moveAction = controls.FindAction("Player/Move", throwIfNotFound: true);
            sprintAction = controls.FindAction("Player/Sprint", throwIfNotFound: true);
            jumpAction = controls.FindAction("Player/Jump", throwIfNotFound: true);
            walkAction = controls.FindAction("Player/Walk", throwIfNotFound: true);
            crouchAction = controls.FindAction("Player/Crouch", throwIfNotFound: true);
        }

        // per-action, so disabling the motor doesn't also kill Look on the same map
        void OnEnable()
        {
            moveAction.Enable();
            sprintAction.Enable();
            jumpAction.Enable();
            walkAction.Enable();
            crouchAction.Enable();
        }

        void OnDisable()
        {
            moveAction.Disable();
            sprintAction.Disable();
            jumpAction.Disable();
            walkAction.Disable();
            crouchAction.Disable();
            horizontalVelocity = Vector3.zero;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            UpdateCrouch();
            Vector2 moveInput = moveAction.ReadValue<Vector2>();

            // yaw lives on this transform, so these axes are always flat
            Vector3 wishDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            // edge, not level: IsPressed would flip the toggle every frame the key is held
            if (walkAction.WasPressedThisFrame()) walking = !walking;
            float speed = moveSpeed * SpeedMultiplier();
            Vector3 targetVelocity = wishDirection * speed;

            bool grounded = controller.isGrounded;
            float acceleration = grounded ? groundAcceleration : airAcceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * deltaTime);

            // keep pressing into the floor so next frame's Move still reports grounded
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            // v = sqrt(2gh): the launch speed whose arc peaks at exactly jumpHeight
            if (grounded && !IsCrouching && jumpAction.WasPressedThisFrame()) verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
            verticalVelocity += gravity * deltaTime;

            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            CollisionFlags hits = controller.Move(velocity * deltaTime);
            // controller.velocity is clipped by the floor on this frame; verticalVelocity still holds the real fall speed
            if (!grounded && controller.isGrounded) Landed?.Invoke(-verticalVelocity);

            // bonked a ceiling: drop instead of hovering against it for the rest of the arc
            if ((hits & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            // the controller keeps its own copy of the position and the next Move would put you back
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            // velocity is world-space, so a sprint into the door would carry on in the old direction
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        void UpdateCrouch()
        {
            bool wantsCrouch = crouchAction.IsPressed();
            if (wantsCrouch && !IsCrouching) SetHeight(crouchHeight, true);
            // released under a low ceiling: stay down and retry every frame until there's room
            else if (!wantsCrouch && IsCrouching && HasHeadroom()) SetHeight(standingHeight, false);
        }

        void SetHeight(float height, bool crouching)
        {
            controller.height = height;
            // keep the feet planted; shrinking around the middle would lift them off the floor
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            IsCrouching = crouching;
        }

        bool HasHeadroom()
        {
            // narrower than the capsule so a wall you're brushing against doesn't count as a ceiling
            float probeRadius = controller.radius * 0.9f;
            // from the crouched capsule's top sphere, through the height standing would add
            Vector3 origin = transform.position + Vector3.up * (crouchHeight - controller.radius);
            float distance = standingHeight - crouchHeight;
            return !Physics.SphereCast(origin, probeRadius, Vector3.up, out _, distance, standBlockers, QueryTriggerInteraction.Ignore);
        }

        float SpeedMultiplier()
        {
            // crouch beats sprint beats walk
            if (IsCrouching) return crouchMultiplier;
            if (sprintAction.IsPressed()) return sprintMultiplier;
            return walking ? walkMultiplier : 1f;
        }
    }
}
