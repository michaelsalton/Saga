using Saga.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] UIRoot uiRoot;
        [SerializeField] string title;
        // must be in Build Profiles' scene list or the load fails
        [SerializeField] string newGameScene;
        [Header("Screens")]
        [SerializeField] VisualTreeAsset mainMenuTemplate;
        [SerializeField] VisualTreeAsset settingsTemplate;
        [SerializeField] VisualTreeAsset displayTemplate;
        [SerializeField] VisualTreeAsset graphicsTemplate;
        [SerializeField] VisualTreeAsset controlsTemplate;
        [SerializeField] VisualTreeAsset audioTemplate;

        MainMenuScreen mainMenu;
        SaveGame save;

        void Awake()
        {
            // read once up front: Continue is enabled only if the file actually loads, not merely exists
            save = SaveGame.Load();
            var settingsScreen = SettingsScreen.Create(GameSettings.Current, uiRoot,
                settingsTemplate, displayTemplate, graphicsTemplate, controlsTemplate, audioTemplate);
            mainMenu = new MainMenuScreen(mainMenuTemplate, settingsScreen, title, save != null, OnContinue, OnNewGame);
        }

        void Start()
        {
            // Start, not Awake: UIRoot copies its panel settings in its own Awake, and the order between the two isn't fixed
            uiRoot.Scale = GameSettings.Current.UiScale;
            uiRoot.Screens.Push(mainMenu);
        }

        void OnContinue()
        {
            BeginLoading();
            GameSession.Continue(save);
        }

        void OnNewGame()
        {
            BeginLoading();
            GameSession.NewGame(newGameScene);
        }

        // the load is async, so a second click would queue a second load on top of the first
        void BeginLoading() => mainMenu.Root.SetEnabled(false);
    }
}
