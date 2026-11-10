using Saga.Rendering;
using UnityEngine;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class MenuBackdropBlur : MonoBehaviour
    {
        [SerializeField] UIRoot uiRoot;
        [SerializeField] float fadeDuration = 0.15f;

        float target;

        void OnEnable()
        {
            uiRoot.Screens.BlockingChanged += OnBlockingChanged;
            // a screen pushed before this enabled has already fired its change
            target = uiRoot.Screens.IsBlocking ? 1f : 0f;
        }

        void OnDisable()
        {
            uiRoot.Screens.BlockingChanged -= OnBlockingChanged;
            // the static outlives the scene, so a quit mid-fade would otherwise leave the next scene blurred
            BackdropBlur.Amount = 0f;
        }

        void OnBlockingChanged(bool blocking) => target = blocking ? 1f : 0f;

        void Update()
        {
            // pausing sets time scale to 0, so scaled delta time would freeze the fade on its first frame
            BackdropBlur.Amount = Mathf.MoveTowards(BackdropBlur.Amount, target, Time.unscaledDeltaTime / fadeDuration);
        }
    }
}
