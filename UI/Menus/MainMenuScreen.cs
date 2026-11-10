using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class MainMenuScreen : UIScreen
    {
        readonly Button continueButton;
        readonly Button newGameButton;

        // a disabled button can't take focus, so with no save the menu opens on New Game
        protected override Focusable DefaultFocus => continueButton.enabledSelf ? continueButton : newGameButton;

        public MainMenuScreen(VisualTreeAsset template, UIScreen settingsScreen, bool canContinue, Action onContinue, Action onNewGame) : base(template)
        {
            continueButton = Root.Q<Button>("continue");
            newGameButton = Root.Q<Button>("new-game");
            continueButton.SetEnabled(canContinue);
            continueButton.clicked += onContinue;
            newGameButton.clicked += onNewGame;
            Root.Q<Button>("settings").clicked += () => Open(settingsScreen);
            Root.Q<Button>("quit").clicked += Quit;
        }

        // the bottom of the stack; letting Back pop it would leave an empty screen
        public override bool HandleBack() => true;

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
