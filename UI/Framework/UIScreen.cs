using System;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class UIScreen
    {
        // on every screen root; popups anchor to it so they draw over the whole screen
        public const string ScreenClass = "screen";
        // put on the element a screen focuses first, so UXML decides instead of an override
        public const string DefaultFocusClass = "default-focus";
        // anything a player can navigate to; hovering it with the mouse moves focus there
        public const string MenuItemClass = "menu-item";

        public VisualElement Root { get; }
        public ScreenStack Stack { get; internal set; }
        public virtual bool BlocksGameplayInput => true;
        // an opaque screen covers the scene, so effects drawn on the scene behind it are wasted
        public virtual bool HidesScene => false;
        protected virtual Focusable DefaultFocus => Root.Q(className: DefaultFocusClass);

        // same moment as OnClosed, for code that composes screens instead of subclassing them
        public event Action Closed;

        Focusable lastFocused;

        public UIScreen(VisualTreeAsset template)
        {
            Root = template.Instantiate();
            // a TemplateContainer has no size of its own; this class stretches it over the layer
            Root.AddToClassList(ScreenClass);
            Root.RegisterCallback<FocusOutEvent>(OnFocusOut);
            // hover moves focus, so a gamepad picks up from wherever the mouse last was
            Root.Query(className: MenuItemClass).ForEach(item => item.RegisterCallback<PointerEnterEvent>(_ => item.Focus()));
        }

        // return true to consume Back yourself, e.g. to close an open dropdown first
        public virtual bool HandleBack() => false;

        protected void Open(UIScreen screen) => Stack.Push(screen);

        protected void Close() => Stack.Pop();

        // runs when the screen enters the stack, not when backing out of a sub-screen uncovers it
        protected virtual void OnOpened() { }

        // runs when the screen leaves the stack, not when a sub-screen covers it
        protected virtual void OnClosed() { }

        internal void Show()
        {
            Root.style.display = DisplayStyle.Flex;
            // deferred a tick: an element whose display just changed can't take focus until styles resolve
            Root.schedule.Execute(RestoreFocus);
        }

        internal void Hide()
        {
            // remembered so backing out of a sub-screen lands on the button that opened it
            if (Root.focusController?.focusedElement is VisualElement focused && Root.Contains(focused)) lastFocused = focused;
            Root.style.display = DisplayStyle.None;
        }

        internal void NotifyOpened() => OnOpened();

        internal void NotifyClosed()
        {
            // the next open starts at the default, not wherever this visit ended
            lastFocused = null;
            OnClosed();
            Closed?.Invoke();
        }

        void RestoreFocus() => (lastFocused ?? DefaultFocus)?.Focus();

        void OnFocusOut(FocusOutEvent focusEvent)
        {
            // a click on empty space clears focus and leaves the gamepad nothing to navigate from
            if (focusEvent.relatedTarget == null) Root.schedule.Execute(RestoreFocus);
        }
    }
}
