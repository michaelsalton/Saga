using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    public class ScreenStack
    {
        // on the layer while the mouse is driving: USS highlights what's hovered instead of what's focused
        public const string PointerModeClass = "pointer-mode";

        readonly List<UIScreen> screens = new();
        VisualElement layer;
        int lastPushFrame = -1;

        public event Action<bool> BlockingChanged;

        public UIScreen Top => screens.Count > 0 ? screens[^1] : null;
        public bool IsBlocking => screens.Exists(screen => screen.BlocksGameplayInput);

        public void AttachLayer(VisualElement layerElement)
        {
            layer = layerElement;
            layer.RegisterCallback<NavigationCancelEvent>(OnCancel);
            // trickle-down: settings rows stop left/right from bubbling, and the layer must still see it
            layer.RegisterCallback<PointerMoveEvent>(_ => layer.AddToClassList(PointerModeClass), TrickleDown.TrickleDown);
            layer.RegisterCallback<NavigationMoveEvent>(_ => layer.RemoveFromClassList(PointerModeClass), TrickleDown.TrickleDown);
            layer.RegisterCallback<NavigationSubmitEvent>(_ => layer.RemoveFromClassList(PointerModeClass), TrickleDown.TrickleDown);
            // a live reload throws the old layer away; carry any open screens over to the new one
            foreach (var screen in screens) layer.Add(screen.Root);
            // a screen pushed before the panel loaded asked for focus while it had no panel to focus in
            Top?.Show();
        }

        public void Push(UIScreen screen)
        {
            bool wasBlocking = IsBlocking;
            Top?.Hide();
            screens.Add(screen);
            screen.Stack = this;
            screen.NotifyOpened();
            // null until the panel's first load; AttachLayer adds whatever was pushed before then
            layer?.Add(screen.Root);
            screen.Show();
            lastPushFrame = Time.frameCount;
            NotifyIfBlockingChanged(wasBlocking);
        }

        public void Pop()
        {
            if (Top == null) return;
            bool wasBlocking = IsBlocking;
            var closing = Top;
            screens.RemoveAt(screens.Count - 1);
            closing.Root.RemoveFromHierarchy();
            closing.NotifyClosed();
            Top?.Show();
            NotifyIfBlockingChanged(wasBlocking);
        }

        public void PopAll()
        {
            if (Top == null) return;
            bool wasBlocking = IsBlocking;
            // top first, so each screen closes before the one that opened it
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                screens[i].Root.RemoveFromHierarchy();
                screens[i].NotifyClosed();
            }
            screens.Clear();
            NotifyIfBlockingChanged(wasBlocking);
        }

        void OnCancel(NavigationCancelEvent cancelEvent)
        {
            // the Esc press that opened a screen reaches the UI as Cancel later in the same frame
            if (Time.frameCount == lastPushFrame) return;
            if (Top != null && !Top.HandleBack()) Pop();
            cancelEvent.StopPropagation();
        }

        void NotifyIfBlockingChanged(bool wasBlocking)
        {
            if (IsBlocking != wasBlocking) BlockingChanged?.Invoke(IsBlocking);
        }
    }
}
