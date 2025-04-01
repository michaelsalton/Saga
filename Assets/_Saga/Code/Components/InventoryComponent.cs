using System.Collections.Generic;
using UnityEngine;
using _Saga.Code.DataType;
using _Saga.Code.BaseClasses;
using _Saga.Code.Utils;

namespace _Saga.Code.Components
{
    public class InventoryComponent : MonoBehaviour
    {
        public static InventoryComponent Instance { get; private set; }

        [SerializeField] private float maxWeight = 100f;
        [SerializeField] private float currentWeight = 0f;
        [SerializeField] private List<InventoryCategory> items = new();
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            InitializeCategories();
        }

        private void InitializeCategories()
        {
            // Ensure all categories are initialized
            foreach (ItemCategory category in System.Enum.GetValues(typeof(ItemCategory)))
            {
                // Check if category exists, otherwise add it
                if (!items.Exists(i => i.category == category))
                {
                    items.Add(new InventoryCategory { category = category, items = new List<BaseItem>() });
                }
            }
        }

        private bool IsOverCarryWeight(float itemWeight)
        {
            return currentWeight + itemWeight > maxWeight;
        }

        public bool AddItem(BaseItem item)
        {
            if (item == null)
            {
                return false;
            }
            Debug.Log($"Adding Item {item.ItemName}");
            var itemWeight = item.GetStackWeight();
            Debug.Log($"Item Weight: {itemWeight}");

            if (IsOverCarryWeight(itemWeight))
            {
                return false;
            }

            var categoryItemList = items.Find(i => i.category == item.Category).items;
            foreach (var existingItem in categoryItemList)
            {
                if (existingItem.AreItemsSame(item))
                {
                    var remaining = existingItem.AddToStack(item.GetCurrentStackSize());
                    if (remaining > 0)
                    {
                        item.SetStackSize(remaining);
                        categoryItemList.Insert(0, item);
                    }
                    currentWeight += itemWeight;
                    return true;
                }
            }

            categoryItemList.Insert(0, item);
            currentWeight += itemWeight;
            Debug.Log($"Added {item.ItemName}");
            return true;
        }
        
        public List<BaseItem> GetItemsByCategory(ItemCategory category)
        {
            var categoryItems = items.Find(i => i.category == category);
            return categoryItems != null ? categoryItems.items : new List<BaseItem>();
        }
    }
}