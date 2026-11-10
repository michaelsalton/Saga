using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class ControlHint : Label
    {
        public const string UssClassName = "control-hint";

        [UxmlAttribute]
        public string Action { get; set; }

        public ControlHint()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            // the event is static, so a hint left subscribed after its screen closes would never be freed
            RegisterCallback<AttachToPanelEvent>(_ => { ActiveInput.Changed += Refresh; Refresh(); });
            RegisterCallback<DetachFromPanelEvent>(_ => ActiveInput.Changed -= Refresh);
        }

        void Refresh()
        {
            var action = ActiveInput.Actions?.FindAction(Action);
            text = action != null ? ActiveInput.BindingText(action) : string.Empty;
        }
    }
}
