using Saga.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    // GameSettings stores a mode, a size and a cap; the page needs choice lists and indexes, so this translates between them
    public class DisplaySettingsViewModel : INotifyBindablePropertyChanged
    {
        // ExclusiveFullScreen is Windows-only; other platforms fall back to a borderless window
        static readonly FullScreenMode[] WindowModes =
        {
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed,
        };
        // 0 is uncapped
        static readonly int[] FrameRates = { 30, 60, 120, 144, 165, 240, 0 };

        readonly GameSettings settings;
        readonly List<Vector2Int> resolutions;

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        [CreateProperty]
        public List<string> WindowModeChoices { get; } = new() { "Fullscreen", "Borderless", "Windowed" };

        [CreateProperty]
        public List<string> ResolutionChoices { get; }

        [CreateProperty]
        public List<string> FrameRateChoices { get; }

        [CreateProperty]
        public int WindowModeIndex
        {
            get => Array.IndexOf(WindowModes, settings.WindowMode);
            set => settings.WindowMode = WindowModes[value];
        }

        [CreateProperty]
        public int ResolutionIndex
        {
            get => resolutions.IndexOf(settings.Resolution);
            set => settings.Resolution = resolutions[value];
        }

        [CreateProperty]
        public int FrameRateIndex
        {
            get => Array.IndexOf(FrameRates, settings.MaxFrameRate);
            set => settings.MaxFrameRate = FrameRates[value];
        }

        [CreateProperty]
        public bool VerticalSync
        {
            get => settings.VerticalSync;
            set => settings.VerticalSync = value;
        }

        [CreateProperty]
        public float Brightness
        {
            get => settings.Brightness;
            set => settings.Brightness = value;
        }

        [CreateProperty]
        public float UiScale
        {
            get => settings.UiScale;
            set => settings.UiScale = value;
        }

        public DisplaySettingsViewModel(GameSettings settings)
        {
            this.settings = settings;

            // the monitor lists each size once per refresh rate; the picker only offers sizes
            var sizes = Screen.resolutions.Select(mode => new Vector2Int(mode.width, mode.height));
            // a resized window or the Editor's Game view isn't a monitor mode, but it's still the current size
            resolutions = sizes
                .Append(settings.Resolution)
                .Distinct()
                // smallest first, so the slider grows left to right like every other slider
                .OrderBy(size => size.x)
                .ThenBy(size => size.y)
                .ToList();
            ResolutionChoices = resolutions.Select(size => $"{size.x} x {size.y}").ToList();
            FrameRateChoices = FrameRates.Select(rate => rate > 0 ? rate.ToString() : "Unlimited").ToList();

            settings.Changed += OnSettingChanged;
        }

        void OnSettingChanged(string setting)
        {
            // a change to the stored value has to reach the page under the name the page binds to
            string property = setting switch
            {
                nameof(GameSettings.WindowMode) => nameof(WindowModeIndex),
                nameof(GameSettings.Resolution) => nameof(ResolutionIndex),
                nameof(GameSettings.MaxFrameRate) => nameof(FrameRateIndex),
                nameof(GameSettings.VerticalSync) => nameof(VerticalSync),
                nameof(GameSettings.Brightness) => nameof(Brightness),
                nameof(GameSettings.UiScale) => nameof(UiScale),
                _ => null,
            };
            if (property != null) propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }
    }
}
