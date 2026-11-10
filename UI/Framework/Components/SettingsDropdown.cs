using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class SettingsDropdown : SettingsField<int>
    {
        public const string UssClassName = "settings-dropdown";
        public const string ValueUssClassName = UssClassName + "__value";
        public const string ArrowUssClassName = UssClassName + "__arrow";
        public const string OverlayUssClassName = UssClassName + "__overlay";
        public const string ListUssClassName = UssClassName + "__list";
        public const string OptionUssClassName = UssClassName + "__option";
        public const string HighlightedUssClassName = OptionUssClassName + "--highlighted";
        const float ListMaxHeight = 360f;

        readonly Label valueLabel;
        readonly VisualElement overlay;
        readonly ScrollView list;
        List<string> choices = new();
        int highlightedIndex;

        // set from code or a ToTarget binding; UXML attributes can't hold lists
        [CreateProperty]
        public List<string> Choices
        {
            get => choices;
            set
            {
                choices = value ?? new List<string>();
                RefreshValueLabel();
            }
        }

        // fixed lists go straight into UXML as "Low,Medium,High"; lists built at runtime come through Choices
        [UxmlAttribute]
        public string Options
        {
            get => string.Join(",", choices);
            set => Choices = string.IsNullOrEmpty(value) ? new List<string>() : value.Split(',').Select(option => option.Trim()).ToList();
        }

        public bool IsOpen => overlay.parent != null;

        public SettingsDropdown() : this(null) { }

        public SettingsDropdown(string label) : base(label)
        {
            AddToClassList(UssClassName);
            Input.Add(CreateArrow("<"));
            valueLabel = new Label();
            valueLabel.AddToClassList(ValueUssClassName);
            Input.Add(valueLabel);
            Input.Add(CreateArrow(">"));

            // full-screen and transparent: catches clicks outside the list and blocks the rows behind it
            overlay = new VisualElement { focusable = true };
            overlay.AddToClassList(OverlayUssClassName);
            list = new ScrollView();
            list.AddToClassList(ListUssClassName);
            list.style.maxHeight = ListMaxHeight;
            overlay.Add(list);

            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<NavigationSubmitEvent>(OnSubmit);
            RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
            // the screen can be torn down with the list open, e.g. Start closing the whole menu
            RegisterCallback<DetachFromPanelEvent>(_ => overlay.RemoveFromHierarchy());
            overlay.RegisterCallback<PointerDownEvent>(OnOverlayPointerDown);
            overlay.RegisterCallback<NavigationMoveEvent>(OnListNavigationMove);
            overlay.RegisterCallback<NavigationSubmitEvent>(OnListSubmit);
            overlay.RegisterCallback<NavigationCancelEvent>(OnListCancel);
        }

        public override void SetValueWithoutNotify(int newValue)
        {
            base.SetValueWithoutNotify(newValue);
            RefreshValueLabel();
        }

        public void Open()
        {
            if (IsOpen || choices.Count == 0) return;

            // the list goes on the screen root, not inside this row, so it draws over the rows below it
            VisualElement host = this;
            while (host.parent != null && !host.ClassListContains(UIScreen.ScreenClass)) host = host.parent;

            list.Clear();
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                var option = new Label(choices[i]);
                option.AddToClassList(OptionUssClassName);
                option.RegisterCallback<PointerEnterEvent>(_ => Highlight(index));
                option.RegisterCallback<ClickEvent>(clickEvent =>
                {
                    clickEvent.StopPropagation();
                    Choose(index);
                });
                list.Add(option);
            }

            host.Add(overlay);
            Rect inputRect = host.WorldToLocal(Input.worldBound);
            list.style.left = inputRect.x;
            list.style.width = inputRect.width;
            // opens below the row unless that would run off the bottom of the screen
            list.style.top = Mathf.Max(0f, Mathf.Min(inputRect.yMax, host.layout.height - ListMaxHeight));

            // the list was rebuilt, so the old highlight index may not exist any more
            highlightedIndex = Mathf.Clamp(value, 0, choices.Count - 1);
            Highlight(highlightedIndex);
            // deferred a tick: the overlay was just added and can't take focus or scroll until it's laid out
            overlay.schedule.Execute(() =>
            {
                overlay.Focus();
                list.ScrollTo(list.contentContainer[highlightedIndex]);
            });
        }

        public void Close()
        {
            if (!IsOpen) return;
            // focus first: removing the focused overlay with nowhere to go would make the screen restore its default focus
            Focus();
            overlay.RemoveFromHierarchy();
        }

        void Choose(int index)
        {
            value = index;
            Close();
        }

        void Highlight(int index)
        {
            if (index < 0 || index >= list.contentContainer.childCount) return;
            list.contentContainer[highlightedIndex].RemoveFromClassList(HighlightedUssClassName);
            highlightedIndex = index;
            var option = list.contentContainer[index];
            option.AddToClassList(HighlightedUssClassName);
            list.ScrollTo(option);
        }

        void OnClick(ClickEvent clickEvent)
        {
            Open();
            clickEvent.StopPropagation();
        }

        void OnSubmit(NavigationSubmitEvent submitEvent)
        {
            Open();
            submitEvent.StopPropagation();
        }

        void OnNavigationMove(NavigationMoveEvent moveEvent)
        {
            // closed, left/right steps through the options without opening the list
            int direction = HorizontalDirection(moveEvent);
            if (direction == 0 || choices.Count == 0) return;
            value = Mathf.Clamp(value + direction, 0, choices.Count - 1);
            ConsumeNavigation(moveEvent);
        }

        void OnOverlayPointerDown(PointerDownEvent pointerEvent)
        {
            if (pointerEvent.target != overlay) return;
            Close();
            pointerEvent.StopPropagation();
        }

        void OnListNavigationMove(NavigationMoveEvent moveEvent)
        {
            if (moveEvent.direction == NavigationMoveEvent.Direction.Up) Highlight(highlightedIndex - 1);
            else if (moveEvent.direction == NavigationMoveEvent.Direction.Down) Highlight(highlightedIndex + 1);
            // every direction is swallowed, so focus can't escape to the rows behind the list
            ConsumeNavigation(moveEvent);
        }

        void OnListSubmit(NavigationSubmitEvent submitEvent)
        {
            Choose(highlightedIndex);
            submitEvent.StopPropagation();
        }

        void OnListCancel(NavigationCancelEvent cancelEvent)
        {
            Close();
            // stopped here, or it would bubble on to the screen stack and close the whole page
            cancelEvent.StopPropagation();
        }

        void RefreshValueLabel()
        {
            bool hasChoice = value >= 0 && value < choices.Count;
            valueLabel.text = hasChoice ? choices[value] : "-";
        }

        static Label CreateArrow(string glyph)
        {
            var arrow = new Label(glyph);
            arrow.AddToClassList(ArrowUssClassName);
            return arrow;
        }
    }
}
