using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class SettingsToggle : SettingsField<bool>
    {
        public const string UssClassName = "settings-toggle";
        public const string BoxUssClassName = UssClassName + "__box";
        public const string CheckUssClassName = UssClassName + "__check";
        public const string CheckedUssClassName = UssClassName + "--checked";

        public SettingsToggle() : this(null) { }

        public SettingsToggle(string label) : base(label)
        {
            AddToClassList(UssClassName);
            var box = new VisualElement();
            box.AddToClassList(BoxUssClassName);
            var check = new VisualElement();
            check.AddToClassList(CheckUssClassName);
            box.Add(check);
            Input.Add(box);

            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<NavigationSubmitEvent>(OnSubmit);
        }

        public override void SetValueWithoutNotify(bool newValue)
        {
            base.SetValueWithoutNotify(newValue);
            EnableInClassList(CheckedUssClassName, newValue);
        }

        void OnClick(ClickEvent clickEvent)
        {
            value = !value;
            clickEvent.StopPropagation();
        }

        void OnSubmit(NavigationSubmitEvent submitEvent)
        {
            value = !value;
            submitEvent.StopPropagation();
        }
    }
}
