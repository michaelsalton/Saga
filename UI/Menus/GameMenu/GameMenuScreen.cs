using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class GameMenuScreen : UIScreen
    {
        readonly MenuTabs tabs;
        readonly InputAction previousTabAction;
        readonly InputAction nextTabAction;
        // where focus was in each tab, so switching away and back lands on the same item
        readonly Dictionary<MenuTab, Focusable> lastFocusedInTab = new();

        public override bool HidesScene => true;
        protected override Focusable DefaultFocus => FocusTarget(tabs.SelectedTab);

        public GameMenuScreen(VisualTreeAsset template, InputAction previousTabAction, InputAction nextTabAction) : base(template)
        {
            this.previousTabAction = previousTabAction;
            this.nextTabAction = nextTabAction;
            tabs = Root.Q<MenuTabs>();
            Root.Q<Button>("resume").clicked += Close;
            tabs.SelectedIndexChanged += OnSelectedIndexChanged;
            // recorded on every focus instead of at switch time, so it doesn't matter whether hiding a tab drops focus first
            Root.RegisterCallback<FocusInEvent>(OnFocusIn);
            previousTabAction.performed += _ => Step(-1);
            nextTabAction.performed += _ => Step(1);
        }

        protected override void OnOpened()
        {
            previousTabAction.Enable();
            nextTabAction.Enable();
        }

        // the next open starts on the first tab at its default, not wherever this visit ended
        protected override void OnClosed()
        {
            previousTabAction.Disable();
            nextTabAction.Disable();
            tabs.SelectedIndex = 0;
            lastFocusedInTab.Clear();
        }

        void Step(int direction)
        {
            // a sub-screen opened from a tab covers this one; the shoulder buttons aren't ours until it closes
            if (Stack.Top != this || tabs.TabCount == 0) return;
            // wraps, so RB on the last tab lands on the first
            tabs.SelectedIndex = (tabs.SelectedIndex + direction + tabs.TabCount) % tabs.TabCount;
        }

        void OnSelectedIndexChanged(int _)
        {
            // deferred a tick: the tab whose display just changed can't take focus until styles resolve
            Root.schedule.Execute(() => FocusTarget(tabs.SelectedTab)?.Focus());
        }

        void OnFocusIn(FocusInEvent focusEvent)
        {
            if (focusEvent.target is not VisualElement focused) return;
            var tab = focused.GetFirstAncestorOfType<MenuTab>();
            if (tab != null) lastFocusedInTab[tab] = focused;
        }

        Focusable FocusTarget(MenuTab tab)
        {
            if (tab == null) return null;
            return lastFocusedInTab.TryGetValue(tab, out var remembered) ? remembered : tab.Q(className: DefaultFocusClass);
        }
    }
}
