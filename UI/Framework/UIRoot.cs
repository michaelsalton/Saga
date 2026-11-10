using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [RequireComponent(typeof(PanelRenderer))]
    [DisallowMultipleComponent]
    public class UIRoot : MonoBehaviour
    {
        // exists before OnEnable so other scripts can subscribe in their own OnEnable
        public ScreenStack Screens { get; } = new();
        public HudLayer Hud { get; } = new();

        // UXML can't reference an asset, so control hints look their actions up in this one
        [SerializeField] InputActionAsset controls;

        PanelRenderer panelRenderer;
        PanelSettings runtimePanelSettings;
        Vector2Int authoredReferenceResolution;
        float scale = 1f;
        int loadedVersion = -1;

        // multiplies the size of everything on the panel; only meaningful in Scale With Screen Size mode
        public float Scale
        {
            get => scale;
            set
            {
                scale = Mathf.Max(0.1f, value);
                if (runtimePanelSettings == null) return;
                // the scaler zooms by screen / reference, so a smaller reference resolution makes everything bigger
                Vector2 reference = new Vector2(authoredReferenceResolution.x, authoredReferenceResolution.y) / scale;
                runtimePanelSettings.referenceResolution = Vector2Int.RoundToInt(reference);
            }
        }

        void Awake()
        {
            panelRenderer = GetComponent<PanelRenderer>();
            if (controls != null) ActiveInput.SetActions(controls);
            if (panelRenderer.panelSettings == null) return;
            // the asset is project data; runtime changes like Scale go to a copy so Play mode never saves them into it
            runtimePanelSettings = Instantiate(panelRenderer.panelSettings);
            authoredReferenceResolution = runtimePanelSettings.referenceResolution;
            panelRenderer.panelSettings = runtimePanelSettings;
        }

        void OnDestroy()
        {
            if (runtimePanelSettings != null) Destroy(runtimePanelSettings);
        }

        void OnEnable() => panelRenderer.RegisterUIReloadCallback(OnUIReload);

        void OnDisable() => panelRenderer.UnregisterUIReloadCallback(OnUIReload);

        void OnUIReload(PanelRenderer _, VisualElement root, int version)
        {
            // a disable/enable cycle keeps the same tree and version; only a real rebuild needs new layers
            if (version == loadedVersion) return;
            loadedVersion = version;
            Hud.AttachLayer(root.Q("hud-layer"));
            Screens.AttachLayer(root.Q("screen-layer"));
        }
    }
}
