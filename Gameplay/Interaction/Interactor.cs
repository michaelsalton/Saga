using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga.Gameplay
{
    // after FirstPersonLook places the camera, so selection uses this frame's view
    [DefaultExecutionOrder(110)]
    [RequireComponent(typeof(FirstPersonLook))]
    [DisallowMultipleComponent]
    public class Interactor : MonoBehaviour
    {
        [SerializeField] InputActionAsset controls;
        // measured from the eye to the nearest part of the object's collider
        [Range(0.5f, 5f)]
        [SerializeField] float reach = 2.2f;
        // half-angle of the cone around screen centre; anything outside it can't be picked
        [Range(1f, 45f)]
        [SerializeField] float maxAngle = 15f;
        // the current target wins until a rival is this many degrees more centred
        [Range(0f, 10f)]
        [SerializeField] float stickiness = 2f;
        // narrow this to an Interactable layer; the overlap buffer fills with floor and walls otherwise
        [SerializeField] LayerMask interactableLayers = ~0;
        // everything that blocks sight; untick the player's own layer
        [SerializeField] LayerMask occluders = ~0;

        public IInteractable Focused { get; private set; }
        public event Action<IInteractable> FocusChanged;

        // overlap results past this count are silently dropped
        readonly Collider[] candidates = new Collider[32];
        FirstPersonLook look;
        InputAction interactAction;

        void Awake()
        {
            look = GetComponent<FirstPersonLook>();
            interactAction = controls.FindAction("Player/Interact", throwIfNotFound: true);
        }

        // per-action, so PauseController's suspend/resume covers this too
        void OnEnable() => interactAction.Enable();

        void OnDisable()
        {
            interactAction.Disable();
            // otherwise the prompt stays on screen after switching to another camera mode
            SetFocus(null);
        }

        void Update()
        {
            SetFocus(FindTarget());
            if (Focused != null && interactAction.WasPressedThisFrame()) Focused.Interact(this);
        }

        IInteractable FindTarget()
        {
            Transform view = look.View;
            if (view == null) return null;
            Vector3 eye = view.position;
            Vector3 forward = view.forward;

            // triggers included so a small object can carry an oversized invisible hit volume
            int count = Physics.OverlapSphereNonAlloc(eye, reach, candidates, interactableLayers, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestAngle = maxAngle;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = candidates[i];
                // colliders often sit on child meshes while the behaviour lives on the prefab root
                var interactable = candidate.GetComponentInParent<IInteractable>();
                if (interactable == null) continue;

                Vector3 aimPoint = AimPoint(candidate.bounds, eye, forward);
                float angle = Vector3.Angle(forward, aimPoint - eye);
                if (ReferenceEquals(interactable, Focused)) angle -= stickiness;
                if (angle >= bestAngle) continue;
                // last, because it's the only per-candidate ray
                if (!InSight(eye, aimPoint, interactable)) continue;

                best = interactable;
                bestAngle = angle;
            }
            return best;
        }

        // the point of the object closest to the centre of the screen
        static Vector3 AimPoint(Bounds bounds, Vector3 eye, Vector3 forward)
        {
            // how far along the gaze line the object sits
            float depth = Mathf.Max(0f, Vector3.Dot(bounds.center - eye, forward));
            return bounds.ClosestPoint(eye + forward * depth);
        }

        bool InSight(Vector3 eye, Vector3 aimPoint, IInteractable interactable)
        {
            // triggers ignored: an invisible volume shouldn't hide what's behind it
            if (!Physics.Linecast(eye, aimPoint, out RaycastHit hit, occluders, QueryTriggerInteraction.Ignore)) return true;
            return ReferenceEquals(hit.collider.GetComponentInParent<IInteractable>(), interactable);
        }

        void SetFocus(IInteractable target)
        {
            if (ReferenceEquals(target, Focused)) return;
            Focused = target;
            FocusChanged?.Invoke(target);
        }
    }
}
