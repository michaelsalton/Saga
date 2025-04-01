using _Saga.Code.Player;
using UnityEngine;

namespace _Saga.Code.BaseClasses
{
    public class BaseRegrowingHarvestableCrop : BaseRegrowingHarvestable
    {
        [SerializeField] private BaseItem inventoryItem; 
        
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
            base.Harvest();
            caller.GetInventory().AddItem(inventoryItem);
        }
    }
}