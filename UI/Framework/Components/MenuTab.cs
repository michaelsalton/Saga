using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class MenuTab : VisualElement
    {
        public const string UssClassName = "menu-tab";

        // the header text; read when MenuTabs builds its header row
        [UxmlAttribute]
        public string Label { get; set; }

        public MenuTab() => AddToClassList(UssClassName);
    }
}
