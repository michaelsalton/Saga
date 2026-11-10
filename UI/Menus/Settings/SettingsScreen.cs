using Saga.Gameplay;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class SettingsScreen : UIScreen
    {
        public SettingsScreen(VisualTreeAsset template, UIScreen display, UIScreen graphics, UIScreen controls, UIScreen audio) : base(template)
        {
            Root.Q<Button>("display").clicked += () => Open(display);
            Root.Q<Button>("graphics").clicked += () => Open(graphics);
            Root.Q<Button>("controls").clicked += () => Open(controls);
            Root.Q<Button>("audio").clicked += () => Open(audio);
            Root.Q<Button>("back").clicked += Close;
        }

        // one builder for every menu that offers settings, so the pause and main menus can't drift apart
        public static SettingsScreen Create(GameSettings settings, UIRoot uiRoot, VisualTreeAsset settingsTemplate,
            VisualTreeAsset displayTemplate, VisualTreeAsset graphicsTemplate, VisualTreeAsset controlsTemplate, VisualTreeAsset audioTemplate)
        {
            var displayPage = new SettingsPage(displayTemplate, new DisplaySettingsViewModel(settings), settings, defaults =>
            {
                settings.WindowMode = defaults.WindowMode;
                settings.Resolution = defaults.Resolution;
                settings.VerticalSync = defaults.VerticalSync;
                settings.MaxFrameRate = defaults.MaxFrameRate;
                settings.Brightness = defaults.Brightness;
                settings.UiScale = defaults.UiScale;
            });
            // rescaling the panel mid-drag would move the slider out from under the cursor, so it applies on leaving the page
            displayPage.Closed += () => uiRoot.Scale = settings.UiScale;

            var graphicsPage = new SettingsPage(graphicsTemplate, settings, settings, defaults =>
            {
                settings.TextureQuality = defaults.TextureQuality;
                settings.ShadowQuality = defaults.ShadowQuality;
                settings.AmbientOcclusion = defaults.AmbientOcclusion;
                settings.FieldOfView = defaults.FieldOfView;
            });

            var controlsPage = new SettingsPage(controlsTemplate, settings, settings, defaults =>
            {
                settings.MouseSensitivity = defaults.MouseSensitivity;
                settings.StickSensitivity = defaults.StickSensitivity;
                settings.InvertY = defaults.InvertY;
                settings.HeadBob = defaults.HeadBob;
                settings.IdleSway = defaults.IdleSway;
            });

            var audioPage = new SettingsPage(audioTemplate, settings, settings, defaults =>
            {
                settings.MasterVolume = defaults.MasterVolume;
            });

            return new SettingsScreen(settingsTemplate, displayPage, graphicsPage, controlsPage, audioPage);
        }
    }
}
