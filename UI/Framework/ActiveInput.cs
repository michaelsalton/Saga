using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga.UI
{
    public enum InputKind { KeyboardMouse, Gamepad }

    public static class ActiveInput
    {
        public static InputKind Current { get; private set; }
        public static InputActionAsset Actions { get; private set; }
        public static event Action Changed;

        // statics survive between play sessions when domain reload is off, so reset them before each one
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            Current = InputKind.KeyboardMouse;
            Actions = null;
            Changed = null;
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
        }

        // hints built before the asset arrived showed nothing, so they need a second pass
        public static void SetActions(InputActionAsset actions)
        {
            Actions = actions;
            Changed?.Invoke();
        }

        public static string BindingText(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (MatchesCurrent(action.bindings[i].effectivePath)) return action.GetBindingDisplayString(i);
            }
            return action.GetBindingDisplayString();
        }

        static bool MatchesCurrent(string path)
        {
            if (Current == InputKind.Gamepad) return path.StartsWith("<Gamepad>");
            // a mouse button is a keyboard-and-mouse hint too
            return path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>");
        }

        static void OnActionChange(object actionOrMap, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            var device = (actionOrMap as InputAction)?.activeControl?.device;
            if (device == null) return;
            var kind = device is Gamepad ? InputKind.Gamepad : InputKind.KeyboardMouse;
            if (kind == Current) return;
            Current = kind;
            Changed?.Invoke();
        }
    }
}
