using UnityEngine;
using System.Collections.Generic;
using _Saga.Code.BaseClasses;
using _Saga.Code.DataType;

namespace _Saga.Code.Items
{
    public class ConsumableItem : BaseItem
    {
        [Header("Consumable Properties")]
        [SerializeField] private float healthChange;
        [SerializeField] private float staminaChange;
        [SerializeField] private float hungerChange;
        [SerializeField] private float thirstChange;

        public float HealthChange => healthChange;
        public float StaminaChange => staminaChange;
        public float HungerChange => hungerChange;
        public float ThirstChange => thirstChange;

        public ConsumableItem() { }

        public ConsumableItem(
            string name, string description, Sprite icon,
            int maxStackSize, int currentStackSize, float weight,
            List<SalvageItem> resourceTags,
            float healthChange, float staminaChange, float hungerChange, float thirstChange
        ) : base(name, description, icon, maxStackSize, currentStackSize, weight, resourceTags)
        {
            this.healthChange = healthChange;
            this.staminaChange = staminaChange;
            this.hungerChange = hungerChange;
            this.thirstChange = thirstChange;
        }

        public void Consume()
        {
            Debug.Log($"Consumed {ItemName}. Health {healthChange}, Stamina {staminaChange}, Hunger {hungerChange}, Thirst {thirstChange}.");
            RemoveFromStack(1);
        }
    }
}