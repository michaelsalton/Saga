using System.Collections.Generic;
using _Saga.Code.BaseClasses;
using _Saga.Code.Components;
using _Saga.Code.DataType;
using UnityEngine;

namespace _Saga.Code.UI
{
    public class UIInventoryController : MonoBehaviour
    {
        [Header("Inventory Component (Player Object)")]
        [SerializeField] private InventoryComponent inventory;
        
        [Header("UI Components")]
        [SerializeField] private GameObject inventoryItemPrefab;
        [SerializeField] private Transform inventoryScrollContent;
        
        public void ShowConsumables() => ShowCategory(ItemCategory.Consumable);
        public void ShowEquipables() => ShowCategory(ItemCategory.Equipable);
        public void ShowMaterials() => ShowCategory(ItemCategory.Material);
        public void ShowArtifacts() => ShowCategory(ItemCategory.Artifact);
        public void ShowJunk() => ShowCategory(ItemCategory.Junk);
        
        private void OnEnable()
        {
            ShowCategory(ItemCategory.Consumable);
        }
        
        private void ShowCategory(ItemCategory category)
        {
            foreach (Transform child in inventoryScrollContent) Destroy(child.gameObject);

            var items = InventoryComponent.Instance.GetItemsByCategory(category);
            foreach (var item in items)
            {
                var itemObj = Instantiate(inventoryItemPrefab, inventoryScrollContent);
                itemObj.GetComponent<UIInventoryItem>().Setup(item);
            }
        }
    }
}