using System;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class PauseScreen : UIScreen
    {
        readonly Button saveButton;
        readonly Button resumeButton;
        readonly string saveLabel;
        readonly Func<bool> save;

        public PauseScreen(VisualTreeAsset template, UIScreen settingsScreen, Func<bool> save, Action quitToMenu) : base(template)
        {
            this.save = save;
            saveButton = Root.Q<Button>("save");
            resumeButton = Root.Q<Button>("resume");
            saveLabel = saveButton.text;
            resumeButton.clicked += Close;
            saveButton.clicked += Save;
            Root.Q<Button>("settings").clicked += () => Open(settingsScreen);
            Root.Q<Button>("quit").clicked += quitToMenu;
        }

        void Save()
        {
            bool saved = save();
            // the label is the only confirmation, so a failed write has to say so
            saveButton.text = saved ? "Saved" : "Save failed";
            // time is stopped while paused, so a second save would write the same game; a failed one stays clickable to retry
            if (!saved) return;
            // a disabled button can't keep focus, so hand it to Resume before greying out, or the gamepad has nothing selected
            if (saveButton.focusController?.focusedElement == saveButton) resumeButton.Focus();
            saveButton.SetEnabled(false);
        }

        // the next pause starts at "Save", not at the last result
        protected override void OnClosed()
        {
            saveButton.text = saveLabel;
            saveButton.SetEnabled(true);
        }
    }
}
