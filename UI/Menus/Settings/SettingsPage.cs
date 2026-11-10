using Saga.Gameplay;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class SettingsPage : UIScreen
    {
        readonly GameSettings settings;
        readonly Button confirmButton;
        readonly Button backButton;
        // what Back restores: the values as of opening the page, or as of the last Confirm
        GameSettings committedValues;

        // reset copies this page's settings out of the defaults it's handed, so it can't touch other pages
        public SettingsPage(VisualTreeAsset template, object dataSource, GameSettings settings, Action<GameSettings> reset) : base(template)
        {
            this.settings = settings;
            // every control binds to the page's data source
            Root.dataSource = dataSource;
            confirmButton = Root.Q<Button>("confirm");
            backButton = Root.Q<Button>("back");
            Root.Q<Button>("reset").clicked += () => reset(GameSettings.CreateDefaults());
            confirmButton.clicked += Confirm;
            backButton.clicked += Close;
        }

        protected override void OnOpened()
        {
            committedValues = settings.Clone();
            settings.Changed += OnSettingChanged;
            RefreshConfirm();
        }

        // every exit lands here: the Back button, Cancel, and a full close from the pause key
        protected override void OnClosed()
        {
            settings.Changed -= OnSettingChanged;
            settings.CopyFrom(committedValues);
        }

        void Confirm()
        {
            settings.Save();
            committedValues = settings.Clone();
            // a disabled button can't keep focus, so hand it to Back before greying out, or the gamepad has nothing selected
            if (confirmButton.focusController?.focusedElement == confirmButton) backButton.Focus();
            RefreshConfirm();
        }

        void OnSettingChanged(string _) => RefreshConfirm();

        // compared as JSON so a setting changed and changed back counts as nothing pending
        void RefreshConfirm() => confirmButton.SetEnabled(JsonUtility.ToJson(settings) != JsonUtility.ToJson(committedValues));
    }
}
