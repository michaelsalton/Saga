using _Saga.Code.Components;
using _Saga.Code.Interfaces;
using UnityEngine;

namespace _Saga.Code.BaseClasses
{
    public class BaseCharacter : MonoBehaviour
    {
        protected StatlineComponent Statline;
        protected InventoryComponent Inventory;
        
        protected virtual void Awake()
        {
            Statline = GetComponent<StatlineComponent>();
            Inventory = GetComponent<InventoryComponent>();
        }

        protected virtual bool CanSprint()
        {
            return Statline.HasEnoughStaminaToSprint();
        }
        
        protected virtual bool CanJump()
        {
            return Statline.HasEnoughStaminaToJump();
        }
        
        protected virtual void HasJumped()
        {
            Statline.HasJumped();
        }
        
        protected virtual void SetSprinting(bool isSprinting)
        {
            Statline.SetSprinting(isSprinting);
        }
        
        protected virtual void SetSneaking(bool isSneaking)
        {
            Statline.SetSneaking(isSneaking);
        }

        protected bool CheckIfCanInteract(GameObject item)
        {
            var interactableObject = item.GetComponent<IInteractionInterface>();
            if (interactableObject == null)
            {
                return false;
            }
            return interactableObject.IsInteractable;
        }

        public InventoryComponent GetInventory()
        {
            return Inventory;
        }
    }
}
