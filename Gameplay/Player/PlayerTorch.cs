using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Saga.Gameplay
{
    // after FirstPersonLook places the camera, so the light uses this frame's view instead of trailing a frame behind
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(FirstPersonLook))]
    [DisallowMultipleComponent]
    public class PlayerTorch : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        // the asset reference from when this field held a scene light carries over
        [FormerlySerializedAs("torchLight")]
        [SerializeField] Light torchPrefab;
        // camera-local; off the eye so shadows land beside objects instead of hiding straight behind them
        [SerializeField] Vector3 holdOffset = new Vector3(0.3f, -0.35f, 0.4f);
        [SerializeField] bool startsLit;

        FirstPersonLook look;
        InputAction torchAction;
        Light torchLight;

        void Awake()
        {
            look = GetComponent<FirstPersonLook>();
            torchAction = controls.FindAction("Player/Torch", throwIfNotFound: true);
            if (startsLit) SetLit(true);
        }

        // per-action, so GameplaySuspension's suspend/resume covers this too
        void OnEnable() => torchAction.Enable();

        void OnDisable() => torchAction.Disable();

        // the instance isn't parented to the player, so nothing else would clean it up if the player goes away mid-scene
        void OnDestroy()
        {
            if (torchLight != null) Destroy(torchLight.gameObject);
        }

        void Update()
        {
            if (torchAction.WasPressedThisFrame()) SetLit(torchLight == null || !torchLight.enabled);
            if (torchLight == null || !torchLight.enabled) return;

            // the camera isn't parented to the player; CameraModeController hands it over at runtime
            Transform view = look.View;
            if (view == null) return;
            torchLight.transform.SetPositionAndRotation(view.TransformPoint(holdOffset), view.rotation);
        }

        void SetLit(bool lit)
        {
            // spawned on first use and kept; a disabled Light drops out of URP's visible-light list, so an unlit torch costs nothing
            if (torchLight == null)
            {
                if (!lit) return;
                torchLight = Instantiate(torchPrefab);
            }
            torchLight.enabled = lit;
        }
    }
}
