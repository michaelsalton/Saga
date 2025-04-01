using System.Collections.Generic;
using _Saga.Code.BaseClasses;
using _Saga.Code.Components;
using _Saga.Code.Interfaces;
using _Saga.Code.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Saga.Code.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StatlineComponent))]
    [RequireComponent(typeof(InputSystem))]
    public class PlayerCharacter : BaseCharacter
    {
        public static PlayerCharacter Instance { get; private set; }

        [SerializeField] private GameObject inventory;
        [SerializeField] private float interactionRadius = 5f;
        
        private InputSystem _inputSystem;
        private CharacterController _characterController;
        private Vector3 _input;
        private float _currentSpeed;
        private Vector3 _velocity;
        private float _rotationSpeed = 360f;
        private float _gravity = -9.81f;
        private bool _canInteractWithObject = false;
        private List<Collider> _activeTriggers = new List<Collider>();
        private Collider _closestTrigger = null;

        protected override void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            base.Awake();
            inventory.SetActive(false);
            _inputSystem = new InputSystem();
            _characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            SetupInteractionTrigger();
        }

        private void SetupInteractionTrigger()
        {
            var interactionTrigger = gameObject.AddComponent<SphereCollider>();
            interactionTrigger.isTrigger = true;
            interactionTrigger.radius = interactionRadius;
        }

        private void OnEnable()
        {
            _inputSystem.Player.Enable();
        }

        private void OnDisable()
        {
            _inputSystem.Player.Disable();
        }
        
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
        
        private void Update()
        {
            if (BlockPlayerInput())
            {
                return;
            }
            HandleGravity();
            GatherInput();
            Look();
            Move();
        }

        private void HandleGravity()
        {
            if (_characterController.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            if (!_characterController.isGrounded)
            {
                _velocity.y += _gravity * Time.deltaTime;
            }
        }

        private void GatherInput()
        {
            var input = _inputSystem.Player.Move.ReadValue<Vector2>();
            _input = new Vector3(input.x, 0, input.y);
        }
        
        private void Move()
        {
            if (Statline.GetIsSprinting())
            {
                _currentSpeed = Statline.GetSprintSpeed();
            }
            else if (Statline.GetIsSneaking())
            {
                _currentSpeed = Statline.GetSneakSpeed();
            }
            else
            {
                _currentSpeed = Statline.GetWalkSpeed();
            }
            var moveDirection = _input.normalized.ConvertToIsometric() + _velocity;
            var movement = _currentSpeed * Time.deltaTime * moveDirection;
            _characterController.Move(movement);
        }

        private void Look()
        {
            if (_input == Vector3.zero)
            {
                return;
            }
            var rotation = Quaternion.LookRotation(_input.ConvertToIsometric(), Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, _rotationSpeed * Time.deltaTime);   
        }

        private void Jump()
        {
            if (!CanJump() || !_characterController.isGrounded)
            {
                return;
            }
            _velocity.y = Mathf.Sqrt(Statline.GetJumpHeight() * -2f * _gravity);
            HasJumped();
        }
        
        public void OnJump(InputAction.CallbackContext context)
        {
            if (BlockPlayerInput())
            {
                return;
            }
            Jump();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (BlockPlayerInput())
            {
                return;
            }
            if (Statline.GetIsSprinting())
            {
                SetSprinting(false);
                return;
            }
            SetSprinting(true);
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (Statline.GetIsSneaking())
            {
                SetSneaking(false);
                return;
            }
            SetSneaking(true);
        }
        
        private bool BlockPlayerInput()
        {
            return inventory.gameObject.activeInHierarchy;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!CheckIfCanInteract(other.gameObject))
            {
                return;
            }
            _activeTriggers.Add(other);
            UpdateClosestTrigger();
            _canInteractWithObject = _closestTrigger != null;
        }
        
        private void OnTriggerExit(Collider other)
        {
            _activeTriggers.Remove(other);
            UpdateClosestTrigger();
            _canInteractWithObject = _closestTrigger != null;
        }
        
        private void UpdateClosestTrigger()
        {
            _activeTriggers.RemoveAll(trigger => trigger == null || trigger.gameObject == null);
            var closestDistance = float.MaxValue;
            _closestTrigger = null;
            foreach (var trigger in _activeTriggers)
            {
                if (trigger == null || trigger.gameObject == null)
                {
                    continue;
                }
                var distance = GetXZDistance(trigger);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    _closestTrigger = trigger;
                }
            }
            _canInteractWithObject = _closestTrigger != null;
            if (_closestTrigger != null)
            {
                Debug.Log("Closest Trigger: " + _closestTrigger.name);
            }
        }
        
        private float GetXZDistance(Collider trigger)
        {
            var playerPosition = transform.position;
            var triggerPosition = trigger.transform.position;
            var dx = playerPosition.x - triggerPosition.x;
            var dz = playerPosition.z - triggerPosition.z;
            return dx * dx + dz * dz;
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!_closestTrigger)
            {
                return;
            }
            if (!_canInteractWithObject)
            {
                return;
            }
            var interactable = _closestTrigger.GetComponent<IInteractionInterface>();
            if (interactable == null)
            {
                return;
            }
            interactable.Interact(this);
        }

        public void OnInventory(InputAction.CallbackContext context)
        {
            ToggleInventory();
        }

        private void ToggleInventory()
        {
            inventory.SetActive(!inventory.activeSelf);
        }
    }
}
