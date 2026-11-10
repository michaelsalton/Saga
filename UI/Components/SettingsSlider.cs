using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [UxmlElement]
    public partial class SettingsSlider : SettingsField<float>
    {
        public const string UssClassName = "settings-slider";
        public const string TrackUssClassName = UssClassName + "__track";
        public const string ValueUssClassName = UssClassName + "__value";
        // a gamepad press moves about this fraction of the range, so any slider is ~20 presses end to end
        const float NudgeFraction = 0.05f;

        readonly Slider track;
        readonly Label valueLabel;
        float step;
        string format = "0.##";

        [UxmlAttribute]
        public float LowValue
        {
            get => track.lowValue;
            set => track.lowValue = value;
        }

        [UxmlAttribute]
        public float HighValue
        {
            get => track.highValue;
            set => track.highValue = value;
        }

        // values snap to multiples of this, counted from LowValue; 0 means continuous
        [UxmlAttribute]
        public float Step
        {
            get => step;
            set => step = Mathf.Max(0f, value);
        }

        // .NET numeric format for the readout: "0", "0.00", or "0%" which also multiplies by 100
        [UxmlAttribute]
        public string Format
        {
            get => format;
            set
            {
                format = value;
                RefreshValueLabel();
            }
        }

        public SettingsSlider() : this(null) { }

        public SettingsSlider(string label) : base(label)
        {
            AddToClassList(UssClassName);
            // the row holds focus; the inner slider is only there to be dragged with the mouse
            track = new Slider { focusable = false };
            track.AddToClassList(TrackUssClassName);
            track.RegisterValueChangedCallback(OnTrackChanged);
            valueLabel = new Label();
            valueLabel.AddToClassList(ValueUssClassName);
            Input.Add(track);
            Input.Add(valueLabel);

            RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
        }

        public override void SetValueWithoutNotify(float newValue)
        {
            float snapped = Snap(newValue);
            base.SetValueWithoutNotify(snapped);
            track.SetValueWithoutNotify(snapped);
            RefreshValueLabel();
        }

        void OnTrackChanged(ChangeEvent<float> changeEvent)
        {
            // this field owns value and raises its own ChangeEvent; letting the inner one bubble would report it twice
            changeEvent.StopPropagation();
            value = Snap(changeEvent.newValue);
        }

        void OnNavigationMove(NavigationMoveEvent moveEvent)
        {
            int direction = HorizontalDirection(moveEvent);
            if (direction == 0) return;
            value = Snap(value + direction * Nudge());
            ConsumeNavigation(moveEvent);
        }

        float Nudge()
        {
            float nudge = Mathf.Abs(HighValue - LowValue) * NudgeFraction;
            // rounded to whole steps, or a snapped slider would land unevenly: 75, 78, 82...
            return step > 0f ? Mathf.Max(step, Mathf.Round(nudge / step) * step) : nudge;
        }

        float Snap(float raw)
        {
            if (step > 0f) raw = LowValue + Mathf.Round((raw - LowValue) / step) * step;
            return Mathf.Clamp(raw, Mathf.Min(LowValue, HighValue), Mathf.Max(LowValue, HighValue));
        }

        void RefreshValueLabel() => valueLabel.text = value.ToString(format, CultureInfo.InvariantCulture);
    }
}
