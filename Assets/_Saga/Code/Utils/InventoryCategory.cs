using System.Collections.Generic;
using _Saga.Code.BaseClasses;
using _Saga.Code.DataType;

namespace _Saga.Code.Utils
{
    [System.Serializable]
    public class InventoryCategory
    {
        public ItemCategory category;
        public List<BaseItem> items;
    }

}