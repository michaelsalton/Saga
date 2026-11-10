using Saga.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class InteractionPromptHud : MonoBehaviour
    {
        // a class toggle instead of display, so the USS opacity transition can fade it
        const string VisibleClass = "interaction-prompt--visible";

        [SerializeField] InputActionAsset controls;
        [SerializeField] UIRoot uiRoot;
        [SerializeField] Interactor interactor;
        [SerializeField] VisualTreeAsset promptTemplate;

        InputAction interactAction;
        VisualElement root;
        VisualElement prompt;
        Label keyLabel;
        Label verbLabel;

        void Awake()
        {
            interactAction = controls.FindAction("Player/Interact", throwIfNotFound: true);
            root = promptTemplate.Instantiate();
            // a TemplateContainer has no size of its own; the layer class stretches it over the screen
            root.AddToClassList("layer");
            root.pickingMode = PickingMode.Ignore;
            prompt = root.Q(className: "interaction-prompt");
            keyLabel = root.Q<Label>("key");
            verbLabel = root.Q<Label>("verb");
            uiRoot.Hud.Add(root);
        }

        void OnDestroy() => uiRoot.Hud.Remove(root);

        void OnEnable() => interactor.FocusChanged += OnFocusChanged;

        void OnDisable() => interactor.FocusChanged -= OnFocusChanged;

        void OnFocusChanged(IInteractable target)
        {
            prompt.EnableInClassList(VisibleClass, target != null);
            // read on every focus so a rebind shows up without a restart
            if (target != null) keyLabel.text = KeyboardBindingText();
        }

        void LateUpdate()
        {
            // polled, not evented: a door's verb flips on use while focus stays on the same door
            if (interactor.Focused != null) verbLabel.text = interactor.Focused.Prompt;
        }

        string KeyboardBindingText()
        {
            // the asset has no control schemes to mask by, so pick the keyboard binding by its path
            for (int i = 0; i < interactAction.bindings.Count; i++)
            {
                if (interactAction.bindings[i].effectivePath.StartsWith("<Keyboard>")) return interactAction.GetBindingDisplayString(i);
            }
            return interactAction.GetBindingDisplayString();
        }
    }
}
