using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

namespace Saga.UI
{
    // keyed to the stack, not to one menu: any blocking screen pauses the game, so each menu only has to push itself
    [DisallowMultipleComponent]
    public class GameplaySuspension : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        [SerializeField] UIRoot uiRoot;

        readonly List<InputAction> suspendedActions = new();
        InputActionMap playerMap;
        InputAction closeMenuAction;
        CursorLockMode lockStateBeforeSuspend;
        bool cursorVisibleBeforeSuspend;
        float timeScaleBeforeSuspend;

        void Awake()
        {
            playerMap = controls.FindActionMap("Player", throwIfNotFound: true);
            closeMenuAction = controls.FindAction("UI/CloseMenu", throwIfNotFound: true);
        }

        void OnEnable()
        {
            closeMenuAction.performed += OnCloseMenuPerformed;
            uiRoot.Screens.BlockingChanged += OnBlockingChanged;
        }

        void OnDisable()
        {
            closeMenuAction.performed -= OnCloseMenuPerformed;
            closeMenuAction.Disable();
            uiRoot.Screens.BlockingChanged -= OnBlockingChanged;
        }

        void OnCloseMenuPerformed(InputAction.CallbackContext _) => uiRoot.Screens.PopAll();

        void OnBlockingChanged(bool blocking)
        {
            if (blocking) Suspend();
            else Resume();
        }

        void Suspend()
        {
            // remember exactly what was live; playerMap.Enable() would also wake actions other scripts had turned off
            foreach (var action in playerMap)
            {
                if (!action.enabled) continue;
                suspendedActions.Add(action);
                action.Disable();
            }
            // enabled mid-press it ignores the held button that opened the menu, so that press can't also close it
            closeMenuAction.Enable();
            lockStateBeforeSuspend = Cursor.lockState;
            cursorVisibleBeforeSuspend = Cursor.visible;
            timeScaleBeforeSuspend = Time.timeScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }

        void Resume()
        {
            closeMenuAction.Disable();
            foreach (var action in suspendedActions) action.Enable();
            suspendedActions.Clear();
            Cursor.lockState = lockStateBeforeSuspend;
            Cursor.visible = cursorVisibleBeforeSuspend;
            Time.timeScale = timeScaleBeforeSuspend;
        }
    }
}
