using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class GameMenuController : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        [SerializeField] UIRoot uiRoot;
        [SerializeField] VisualTreeAsset gameMenuTemplate;

        InputAction openAction;
        GameMenuScreen gameMenu;

        void Awake()
        {
            openAction = controls.FindAction("Player/GameMenu", throwIfNotFound: true);
            var previousTabAction = controls.FindAction("UI/PreviousTab", throwIfNotFound: true);
            var nextTabAction = controls.FindAction("UI/NextTab", throwIfNotFound: true);
            gameMenu = new GameMenuScreen(gameMenuTemplate, previousTabAction, nextTabAction);
        }

        void OnEnable()
        {
            openAction.performed += OnOpenPerformed;
            openAction.Enable();
        }

        void OnDisable()
        {
            openAction.performed -= OnOpenPerformed;
            openAction.Disable();
        }

        void OnOpenPerformed(InputAction.CallbackContext _) => uiRoot.Screens.Push(gameMenu);
    }
}
