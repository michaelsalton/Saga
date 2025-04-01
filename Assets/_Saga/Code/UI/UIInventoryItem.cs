using _Saga.Code.BaseClasses;
using _Saga.Code.DataType;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Saga.Code.UI
{
    public class UIInventoryItem : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private TMP_Text itemQuantity;

        public void Setup(BaseItem item)
        {
            itemIcon.sprite = item.ItemIcon;
            itemName.text = item.ItemName;
            itemQuantity.text = item.CurrentStackSize > 1 ? $"x{item.CurrentStackSize}" : "";
        }
    }
}