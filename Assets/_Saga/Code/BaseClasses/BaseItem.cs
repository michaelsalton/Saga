using UnityEngine;
using System.Collections.Generic;
using _Saga.Code.DataType;

namespace _Saga.Code.BaseClasses
{
    public class BaseItem : MonoBehaviour
    {
        [Header("Item Info")]
        [SerializeField] private string itemName;
        [SerializeField] private string itemDescription;
        [SerializeField] private Sprite itemIcon;
        [SerializeField] private ItemCategory category;

        [Header("Item Properties")]
        [SerializeField] private int maxStackSize = 1;
        [SerializeField] private int currentStackSize = 1;
        [SerializeField] private float weight = 1.0f;

        [Header("Resource Data")]
        [SerializeField] private List<SalvageItem> resourceTags;

        [Header("World Appearance")]
        [SerializeField] private GameObject worldMesh;
        
        public string ItemName => itemName;
        public string ItemDescription => itemDescription;
        public Sprite ItemIcon => itemIcon;
        public ItemCategory Category => category;
        public int MaxStackSize => maxStackSize;
        public int CurrentStackSize => currentStackSize;
        public float Weight => weight;
        public List<SalvageItem> ResourceTags => resourceTags;
        public GameObject WorldMesh => worldMesh;
        
        public BaseItem()
        {
        }
        public BaseItem(string name, string description, Sprite icon, int maxStackSize, int currentStackSize, float weight, List<SalvageItem> resourceTags)
        {
            itemName = name;
            itemDescription = description;
            itemIcon = icon;
            this.maxStackSize = maxStackSize;
            this.currentStackSize = currentStackSize;
            this.weight = weight;
            this.resourceTags = resourceTags;
        }
        
        public float GetStackWeight() 
        {
            return weight * currentStackSize;
        }
        
        public float GetItemWeight() 
        {
            return weight;
        }
        
        public List<SalvageItem> GetSalvageData() 
        {
            return resourceTags;
        }
        
        public GameObject GetWorldPickupMesh() 
        {
            return worldMesh;
        }
        
        public int GetMaxStackSize() 
        {
            return maxStackSize;
        }
        
        public int GetCurrentStackSize() 
        {
            return currentStackSize;
        }
        
        public int AddToStack(int amount) 
        {
            if (currentStackSize == maxStackSize)
            {
                return amount;
            }

            var remains = Mathf.Clamp(amount - maxStackSize - currentStackSize, 0, amount);
            currentStackSize += amount - remains;
            return remains;
        }
        
        public int RemoveFromStack(int amount) 
        {
            if (currentStackSize < maxStackSize)
            {
                var removed = currentStackSize;
                currentStackSize = 0;
                return amount - removed;
            }

            currentStackSize -= amount;
            return 0;
        }
        
        public void SetStackSize(int amount)
        {
            currentStackSize = amount;
        }

        public bool AreItemsSame(BaseItem other)
        {
            if (other == null)
            {
                return false;
            }
            return itemName == other.itemName && maxStackSize == other.maxStackSize;
        }
    }
}