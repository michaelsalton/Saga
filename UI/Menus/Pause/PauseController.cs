using Saga.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

        InputAction pauseAction;
        PauseScreen pauseScreen;

        void Awake()
        {
            pauseAction = controls.FindAction("Player/Pause", throwIfNotFound: true);

            var settingsScreen = SettingsScreen.Create(GameSettings.Current, uiRoot,
                settingsTemplate, displayTemplate, graphicsTemplate, controlsTemplate, audioTemplate);
            pauseScreen = new PauseScreen(pauseTemplate, settingsScreen, Save, QuitToMenu);
        }

        // Start, not Awake: UIRoot copies its panel settings in its own Awake, and the order between the two isn't fixed
        void Start() => uiRoot.Scale = GameSettings.Current.UiScale;

        bool Save() => saveController != null && saveController.Save();

        void QuitToMenu()
        {
            // through the normal close path, so GameplaySuspension restores time scale and cursor before the scene goes
            uiRoot.Screens.PopAll();
            SceneManager.LoadScene(menuScene);
        }

        void OnEnable()
        {
            pauseAction.performed += OnPausePerformed;
            pauseAction.Enable();
        }

        void OnDisable()
        {
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Disable();
        }

        void OnPausePerformed(InputAction.CallbackContext _) => uiRoot.Screens.Push(pauseScreen);
    }
}
