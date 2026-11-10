using Saga.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class PauseController : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        [SerializeField] UIRoot uiRoot;
        [SerializeField] SaveController saveController;
        // must be in Build Profiles' scene list or the load fails
        [SerializeField] string menuScene = "MainMenu";
        [Header("Screens")]
        [SerializeField] VisualTreeAsset pauseTemplate;
        [SerializeField] VisualTreeAsset settingsTemplate;
        [SerializeField] VisualTreeAsset displayTemplate;
        [SerializeField] VisualTreeAsset graphicsTemplate;
        [SerializeField] VisualTreeAsset controlsTemplate;
        [SerializeField] VisualTreeAsset audioTemplate;

        readonly List<InputAction> suspendedActions = new();
        InputActionMap playerMap;
        InputAction pauseAction;
        InputAction closeMenuAction;
        PauseScreen pauseScreen;
        CursorLockMode lockStateBeforePause;
        bool cursorVisibleBeforePause;
        float timeScaleBeforePause;

        void Awake()
        {
            playerMap = controls.FindActionMap("Player", throwIfNotFound: true);
            pauseAction = playerMap.FindAction("Pause", throwIfNotFound: true);
            closeMenuAction = controls.FindAction("UI/CloseMenu", throwIfNotFound: true);

            var settingsScreen = SettingsScreen.Create(GameSettings.Current, uiRoot,
                settingsTemplate, displayTemplate, graphicsTemplate, controlsTemplate, audioTemplate);
            pauseScreen = new PauseScreen(pauseTemplate, settingsScreen, Save, QuitToMenu);
        }

        // Start, not Awake: UIRoot copies its panel settings in its own Awake, and the order between the two isn't fixed
        void Start() => uiRoot.Scale = GameSettings.Current.UiScale;

        bool Save() => saveController != null && saveController.Save();

        void QuitToMenu()
        {
            // through the normal close path, so time scale and cursor are restored before the scene goes
            uiRoot.Screens.PopAll();
            SceneManager.LoadScene(menuScene);
        }

        void OnEnable()
        {
            pauseAction.performed += OnPausePerformed;
            closeMenuAction.performed += OnCloseMenuPerformed;
            pauseAction.Enable();
            uiRoot.Screens.BlockingChanged += OnBlockingChanged;
        }

        void OnDisable()
        {
            pauseAction.performed -= OnPausePerformed;
            closeMenuAction.performed -= OnCloseMenuPerformed;
            pauseAction.Disable();
            closeMenuAction.Disable();
            uiRoot.Screens.BlockingChanged -= OnBlockingChanged;
        }

        void OnPausePerformed(InputAction.CallbackContext _) => uiRoot.Screens.Push(pauseScreen);

        void OnCloseMenuPerformed(InputAction.CallbackContext _) => uiRoot.Screens.PopAll();

        void OnBlockingChanged(bool blocking)
        {
            if (blocking) SuspendGameplay();
            else ResumeGameplay();
        }

        void SuspendGameplay()
        {
            // remember exactly what was live; playerMap.Enable() would also wake actions other scripts had turned off
            foreach (var action in playerMap)
            {
                if (!action.enabled) continue;
                suspendedActions.Add(action);
                action.Disable();
            }
            // enabled mid-press it ignores the held Start that opened the menu, so that press can't also close it
            closeMenuAction.Enable();
            lockStateBeforePause = Cursor.lockState;
            cursorVisibleBeforePause = Cursor.visible;
            timeScaleBeforePause = Time.timeScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }

        void ResumeGameplay()
        {
            closeMenuAction.Disable();
            foreach (var action in suspendedActions) action.Enable();
            suspendedActions.Clear();
            Cursor.lockState = lockStateBeforePause;
            Cursor.visible = cursorVisibleBeforePause;
            Time.timeScale = timeScaleBeforePause;
        }
    }
}
