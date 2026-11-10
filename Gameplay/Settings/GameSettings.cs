using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.Gameplay
{
    [Serializable]
    public class GameSettings : INotifyBindablePropertyChanged
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public static GameSettings Current { get; private set; }

        [SerializeField] FullScreenMode windowMode;
        [SerializeField] Vector2Int resolution;
        [SerializeField] bool verticalSync;
        // 0 means uncapped
        [SerializeField] int maxFrameRate;
        // exposure in stops: +1 doubles the light reaching the image, -1 halves it
        [SerializeField] float brightness;
        // multiplier on the size of every UI element
        [SerializeField] float uiScale = 1f;
        // tiers, 0 Low to 3 Ultra; ints rather than an enum because binding can't convert enum to the stepper's index
        [SerializeField] int textureQuality = 3;
        [SerializeField] int shadowQuality = 2;
        [SerializeField] bool ambientOcclusion = true;
        [SerializeField] float fieldOfView = 75f;
        [SerializeField] bool headBob = true;
        [SerializeField] bool idleSway = true;
        // degrees per pixel of mouse travel
        [SerializeField] float mouseSensitivity = 0.1f;
        // degrees per second at full stick deflection
        [SerializeField] float stickSensitivity = 180f;
        [SerializeField] bool invertY;
        [SerializeField] float masterVolume = 1f;

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;
        // same notification as propertyChanged, without pulling UI Toolkit types into gameplay code
        public event Action<string> Changed;

        [CreateProperty]
        public FullScreenMode WindowMode
        {
            get => windowMode;
            set { if (Set(ref windowMode, value)) ApplyDisplayMode(); }
        }

        [CreateProperty]
        public Vector2Int Resolution
        {
            get => resolution;
            set { if (Set(ref resolution, value)) ApplyDisplayMode(); }
        }

        [CreateProperty]
        public bool VerticalSync
        {
            get => verticalSync;
            set { if (Set(ref verticalSync, value)) ApplyVerticalSync(); }
        }

        [CreateProperty]
        public int MaxFrameRate
        {
            get => maxFrameRate;
            set { if (Set(ref maxFrameRate, value)) ApplyFrameRate(); }
        }

        [CreateProperty]
        public float Brightness
        {
            get => brightness;
            set { if (Set(ref brightness, value)) GraphicsQuality.ApplyBrightness(brightness); }
        }

        // stored here, applied by the UI: the panel it scales belongs to the UI layer
        [CreateProperty]
        public float UiScale
        {
            get => uiScale;
            set => Set(ref uiScale, value);
        }

        [CreateProperty]
        public int TextureQuality
        {
            get => textureQuality;
            set { if (Set(ref textureQuality, value)) GraphicsQuality.ApplyTextureQuality(textureQuality); }
        }

        [CreateProperty]
        public int ShadowQuality
        {
            get => shadowQuality;
            set { if (Set(ref shadowQuality, value)) GraphicsQuality.ApplyShadowQuality(shadowQuality); }
        }

        [CreateProperty]
        public bool AmbientOcclusion
        {
            get => ambientOcclusion;
            set { if (Set(ref ambientOcclusion, value)) GraphicsQuality.ApplyAmbientOcclusion(ambientOcclusion); }
        }

        [CreateProperty]
        public float FieldOfView
        {
            get => fieldOfView;
            set => Set(ref fieldOfView, value);
        }

        [CreateProperty]
        public bool HeadBob
        {
            get => headBob;
            set => Set(ref headBob, value);
        }

        [CreateProperty]
        public bool IdleSway
        {
            get => idleSway;
            set => Set(ref idleSway, value);
        }

        [CreateProperty]
        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => Set(ref mouseSensitivity, value);
        }

        [CreateProperty]
        public float StickSensitivity
        {
            get => stickSensitivity;
            set => Set(ref stickSensitivity, value);
        }

        [CreateProperty]
        public bool InvertY
        {
            get => invertY;
            set => Set(ref invertY, value);
        }

        [CreateProperty]
        public float MasterVolume
        {
            get => masterVolume;
            set { if (Set(ref masterVolume, value)) ApplyMasterVolume(); }
        }

        // before any scene loads, so every Awake can already read Current
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadAtStartup()
        {
            Current = Load();
            Current.ApplyDisplayMode();
            Current.ApplyVerticalSync();
            Current.ApplyFrameRate();
            Current.ApplyMasterVolume();
            GraphicsQuality.ApplyTextureQuality(Current.textureQuality);
            GraphicsQuality.ApplyShadowQuality(Current.shadowQuality);
            GraphicsQuality.ApplyAmbientOcclusion(Current.ambientOcclusion);
            GraphicsQuality.ApplyBrightness(Current.brightness);
        }

        static GameSettings Load()
        {
            // first launch has no file, so the engine's current state becomes the defaults
            var settings = new GameSettings
            {
                windowMode = Screen.fullScreenMode,
                resolution = new Vector2Int(Screen.width, Screen.height),
                verticalSync = QualitySettings.vSyncCount > 0,
                maxFrameRate = Mathf.Max(0, Application.targetFrameRate),
            };
            if (!File.Exists(FilePath)) return settings;
            try
            {
                // overwrite, not replace: fields added after the file was written keep their defaults
                JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), settings);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameSettings] Couldn't read {FilePath}, using defaults: {exception.Message}");
            }
            return settings;
        }

        // what Reset restores; display is borderless at the monitor's native size rather than whatever the window is now
        public static GameSettings CreateDefaults() => new()
        {
            windowMode = FullScreenMode.FullScreenWindow,
            resolution = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height),
            verticalSync = true,
        };

        // a JSON round trip copies the values without the event subscribers
        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));

        // through the setters, so each value that differs is applied to the engine and reaches the bound UI
        public void CopyFrom(GameSettings source)
        {
            WindowMode = source.windowMode;
            Resolution = source.resolution;
            VerticalSync = source.verticalSync;
            MaxFrameRate = source.maxFrameRate;
            Brightness = source.brightness;
            UiScale = source.uiScale;
            TextureQuality = source.textureQuality;
            ShadowQuality = source.shadowQuality;
            AmbientOcclusion = source.ambientOcclusion;
            FieldOfView = source.fieldOfView;
            HeadBob = source.headBob;
            IdleSway = source.idleSway;
            MouseSensitivity = source.mouseSensitivity;
            StickSensitivity = source.stickSensitivity;
            InvertY = source.invertY;
            MasterVolume = source.masterVolume;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, prettyPrint: true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameSettings] Couldn't write {FilePath}: {exception.Message}");
            }
        }

        // mode and size go together: switching to windowed without a size keeps the fullscreen size as the window
        void ApplyDisplayMode()
        {
            if (resolution.x <= 0 || resolution.y <= 0) return;
            Screen.SetResolution(resolution.x, resolution.y, windowMode);
        }

        void ApplyVerticalSync() => QualitySettings.vSyncCount = verticalSync ? 1 : 0;

        // -1 is Unity's "no cap"; with VSync on the display's refresh rate wins and this is ignored
        void ApplyFrameRate() => Application.targetFrameRate = maxFrameRate > 0 ? maxFrameRate : -1;

        void ApplyMasterVolume() => AudioListener.volume = masterVolume;

        bool Set<T>(ref T field, T value, [CallerMemberName] string property = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
            Changed?.Invoke(property);
            return true;
        }
    }
}
