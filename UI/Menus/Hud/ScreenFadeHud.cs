using Saga.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.UI
{
    [DisallowMultipleComponent]
    public class ScreenFadeHud : MonoBehaviour
    {
        [SerializeField] UIRoot uiRoot;
        [SerializeField] PlayerTravel travel;

        VisualElement fade;
        float shownBlackness;

        void Awake()
        {
            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.AddToClassList("layer");
            fade.AddToClassList("screen-fade");
            uiRoot.Hud.Add(fade);
        }

        void OnDestroy() => uiRoot.Hud.Remove(fade);

        void LateUpdate()
        {
            float blackness = travel.Blackness;
            if (blackness == shownBlackness) return;
            // the HUD layer stacks by child order, so this re-tops itself over the prompt each time it starts
            if (shownBlackness == 0f) fade.BringToFront();
            shownBlackness = blackness;
            fade.style.opacity = blackness;
        }
    }
}
