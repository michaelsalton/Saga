using UnityEngine.UIElements;

namespace Saga.UI
{
    public abstract class SettingsField<TValue> : BaseField<TValue>
    {
        public const string RowUssClassName = "settings-row";

        protected VisualElement Input { get; }

        protected SettingsField(string label) : base(label, null)
        {
            AddToClassList(RowUssClassName);
            AddToClassList(UIScreen.MenuItemClass);
            // the row itself holds focus, so :focus styles the whole row and navigation events land here
            focusable = true;
            delegatesFocus = false;
            Input = this.Q(className: inputUssClassName);
        }

        protected static int HorizontalDirection(NavigationMoveEvent moveEvent) => moveEvent.direction switch
        {
            NavigationMoveEvent.Direction.Left => -1,
            NavigationMoveEvent.Direction.Right => 1,
            _ => 0,
        };

        // left/right was used to change the value, so focus must not also move to a neighbour
        protected void ConsumeNavigation(NavigationMoveEvent moveEvent)
        {
            focusController?.IgnoreEvent(moveEvent);
            moveEvent.StopPropagation();
        }
    }
}
