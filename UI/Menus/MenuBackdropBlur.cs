using Saga.Rendering;
using UnityEngine;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class MenuBackdropBlur : MonoBehaviour
    {
        [SerializeField] UIRoot uiRoot;
        [SerializeField] float fadeDuration = 0.15f;

        void OnDisable()
        {
            // the static outlives the scene, so a quit mid-fade would otherwise leave the next scene blurred
            BackdropBlur.Amount = 0f;
        }

        void Update()
        {
            var screens = uiRoot.Screens;
            // only the top screen is visible; an opaque one covers the scene, so blurring underneath it is wasted work
            float target = screens.IsBlocking && !(screens.Top?.HidesScene ?? false) ? 1f : 0f;
            // pausing sets time scale to 0, so scaled delta time would freeze the fade on its first frame
            BackdropBlur.Amount = Mathf.MoveTowards(BackdropBlur.Amount, target, Time.unscaledDeltaTime / fadeDuration);
        }
    }
}
