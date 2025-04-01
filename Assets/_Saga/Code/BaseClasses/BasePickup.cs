using _Saga.Code.Player;
using UnityEngine;

namespace _Saga.Code.BaseClasses
{
    public class BasePickup : BaseInteractable
    {
        [SerializeField] private BaseItem inventoryItem; 
        public BaseItem InventoryItem => inventoryItem;
        
        public override void Interact(PlayerCharacter caller)
        {
            if (!IsInteractable)
            {
                return;
            }
            if (caller == null || caller.GetInventory() == null)
            {
                return;
            }
            caller.GetInventory().AddItem(inventoryItem);
        }
    }
}
