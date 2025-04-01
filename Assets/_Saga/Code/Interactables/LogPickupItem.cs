using _Saga.Code.BaseClasses;
using _Saga.Code.Player;
using UnityEngine;

namespace _Saga.Code.Interactables
{
    public class LogPickupItem : BasePickup
    {
        [SerializeField] private float interactDelay = 0.1f;

        private LayerMask _groundLayer;
        private bool _isOnGround = false;
        
        private void Start()
        {
            IsInteractable = false;
            _groundLayer = LayerMask.NameToLayer("Ground");
            Invoke(nameof(EnableInteraction), interactDelay);
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer != _groundLayer)
            {
                return;
            }
            _isOnGround = true;
        }

        private void EnableInteraction()
        {
            IsInteractable = true;
        }
        public override void Interact(PlayerCharacter caller)
        {
            if (!IsInteractable || !_isOnGround)
            {
                return;
            }
            base.Interact(caller);
            Destroy(gameObject);
        }
    }
}