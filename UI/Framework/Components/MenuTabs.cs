using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class MenuTabs : VisualElement
    {
        public const string UssClassName = "menu-tabs";
        public const string HeaderRowUssClassName = UssClassName + "__header-row";
        public const string HeaderUssClassName = UssClassName + "__header";
        public const string SelectedHeaderUssClassName = HeaderUssClassName + "--selected";
        public const string ContentUssClassName = UssClassName + "__content";

        readonly VisualElement headerRow;
        readonly VisualElement content;
        int selectedIndex;

        public event Action<int> SelectedIndexChanged;

        // UXML children land in the content area, under the generated header row
        public override VisualElement contentContainer => content;

        public int TabCount => content.childCount;
        public MenuTab SelectedTab => selectedIndex < content.childCount ? content[selectedIndex] as MenuTab : null;

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                int clamped = Mathf.Clamp(value, 0, Mathf.Max(0, TabCount - 1));
                if (clamped == selectedIndex) return;
                selectedIndex = clamped;
                ApplySelection();
                SelectedIndexChanged?.Invoke(selectedIndex);
            }
        }

        public MenuTabs()
        {
            AddToClassList(UssClassName);
            headerRow = new VisualElement();
            headerRow.AddToClassList(HeaderRowUssClassName);
            hierarchy.Add(headerRow);
            content = new VisualElement();
            content.AddToClassList(ContentUssClassName);
            hierarchy.Add(content);
            // UXML adds the tabs after the constructor runs, so the headers can only be built once the tree is complete
            RegisterCallback<AttachToPanelEvent>(_ => RebuildHeaders());
        }

        void RebuildHeaders()
        {
            headerRow.Clear();
            for (int i = 0; i < content.childCount; i++)
            {
                int index = i;
                // mouse-only: LB/RB switches tabs, so the gamepad never has to navigate up into the header row
                var header = new Button(() => SelectedIndex = index) { text = (content[i] as MenuTab)?.Label, focusable = false };
                header.AddToClassList(HeaderUssClassName);
                headerRow.Add(header);
            }
            ApplySelection();
        }

        void ApplySelection()
        {
            for (int i = 0; i < content.childCount; i++)
            {
                content[i].style.display = i == selectedIndex ? DisplayStyle.Flex : DisplayStyle.None;
            }
            for (int i = 0; i < headerRow.childCount; i++)
            {
                headerRow[i].EnableInClassList(SelectedHeaderUssClassName, i == selectedIndex);
            }
        }
    }
}
