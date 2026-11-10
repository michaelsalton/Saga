using Saga.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class InteractionPromptHud : MonoBehaviour
    {
        // a class toggle instead of display, so the USS opacity transition can fade it
        const string VisibleClass = "interaction-prompt--visible";

        [SerializeField] UIRoot uiRoot;
        [SerializeField] Interactor interactor;
        [SerializeField] VisualTreeAsset promptTemplate;

        VisualElement root;
        VisualElement prompt;
        Label verbLabel;

        void Awake()
        {
            root = promptTemplate.Instantiate();
            // a TemplateContainer has no size of its own; the layer class stretches it over the screen
            root.AddToClassList("layer");
            root.pickingMode = PickingMode.Ignore;
            prompt = root.Q(className: "interaction-prompt");
            verbLabel = root.Q<Label>("verb");
            uiRoot.Hud.Add(root);
        }

        void OnDestroy() => uiRoot.Hud.Remove(root);

        void OnEnable() => interactor.FocusChanged += OnFocusChanged;

        void OnDisable() => interactor.FocusChanged -= OnFocusChanged;

        void OnFocusChanged(IInteractable target) => prompt.EnableInClassList(VisibleClass, target != null);

        void LateUpdate()
        {
            // polled, not evented: a door's verb flips on use while focus stays on the same door
            if (interactor.Focused != null) verbLabel.text = interactor.Focused.Prompt;
        }
    }
}
