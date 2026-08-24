using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga
{
    /// <summary>How the camera is being driven.</summary>
    public enum CameraMode
    {
        Fly,
        Isometric,
    }

    /// <summary>
    /// Owns which camera mode is live. Each mode is a self-contained driver component on this camera
    /// (<see cref="DebugFlyCamera"/>, <see cref="IsometricCamera"/>) that this controller enables one at
    /// a time, along with the projection that mode needs. The HUD only calls <see cref="CycleMode"/> and
    /// listens to <see cref="ModeChanged"/>.
    ///
    /// Both modes drive the camera with WASD, so Cinemachine is held off while either is live — add a
    /// Follow mode here (an enum case plus a branch in <see cref="ApplyMode"/> that leaves the brain
    /// enabled) to hand control back to a rig.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraModeController : MonoBehaviour
    {
        [Tooltip("Mode the camera starts in.")]
        [SerializeField] CameraMode defaultMode = CameraMode.Isometric;
        [Tooltip("Vertical field of view used while flying. Fly mode switches the camera to " +
                 "perspective — in orthographic, moving along the view axis produces no zoom at all, " +
                 "so W/S would look like they do nothing.")]
        [Range(20f, 100f)]
        [SerializeField] float flyFieldOfView = 60f;
        [Tooltip("Input System binding path for the key that cycles to the next mode.")]
        [SerializeField] string cycleKeyBinding = "<Keyboard>/f1";

        /// <summary>The mode currently driving the camera.</summary>
        public CameraMode Current { get; private set; }

        /// <summary>Raised whenever a mode is applied, including the initial one on enable.</summary>
        public event Action<CameraMode> ModeChanged;

        Camera cam;
        CinemachineBrain brain;
        DebugFlyCamera flyCamera;
        IsometricCamera isoCamera;
        InputAction cycleAction;

        bool projectionCached;
        bool cachedOrthographic;
        float cachedOrthographicSize;
        float cachedFieldOfView;

        void Awake()
        {
            cam = GetComponent<Camera>();
            brain = GetComponent<CinemachineBrain>();
            flyCamera = GetComponent<DebugFlyCamera>();
            isoCamera = GetComponent<IsometricCamera>();

            if (flyCamera == null || isoCamera == null)
                Debug.LogWarning("[CameraModeController] Missing a driver component on this camera — " +
                                 "add both DebugFlyCamera and IsometricCamera (leave them disabled; " +
                                 "this controller turns the right one on).", this);

            cycleAction = new InputAction("CycleCameraMode", InputActionType.Button);
            if (!string.IsNullOrEmpty(cycleKeyBinding)) cycleAction.AddBinding(cycleKeyBinding);
            cycleAction.performed += OnCyclePerformed;
        }

        void OnEnable()
        {
            ApplyMode(defaultMode);
            cycleAction.Enable();
        }

        void OnDisable()
        {
            cycleAction.Disable();

            // Put the scene back the way it was, so removing this component leaves no trace.
            if (flyCamera != null) flyCamera.enabled = false;
            if (isoCamera != null) isoCamera.enabled = false;
            RestoreProjection();
            if (brain != null) brain.enabled = true;
        }

        void OnDestroy()
        {
            if (cycleAction == null) return;
            cycleAction.performed -= OnCyclePerformed;
            cycleAction.Dispose();
            cycleAction = null;
        }

        void OnCyclePerformed(InputAction.CallbackContext _) => CycleMode();

        /// <summary>Switch to the next mode in the rotation.</summary>
        public void CycleMode()
        {
            SetMode(Current == CameraMode.Fly ? CameraMode.Isometric : CameraMode.Fly);
        }

        public void SetMode(CameraMode mode)
        {
            if (mode == Current) return;
            ApplyMode(mode);
        }

        void ApplyMode(CameraMode mode)
        {
            Current = mode;

            // Stand both drivers down first, THEN force the brain off — DebugFlyCamera.OnDisable hands
            // control back to Cinemachine, which would otherwise steal a frame on the way out of Fly.
            if (flyCamera != null) flyCamera.enabled = false;
            if (isoCamera != null) isoCamera.enabled = false;
            if (brain != null) brain.enabled = false;

            ApplyProjection(mode);

            switch (mode)
            {
                case CameraMode.Fly:
                    if (flyCamera != null) flyCamera.enabled = true;
                    break;
                case CameraMode.Isometric:
                    if (isoCamera != null) isoCamera.enabled = true;
                    break;
            }

            ModeChanged?.Invoke(mode);
        }

        void ApplyProjection(CameraMode mode)
        {
            if (cam == null) return;

            if (mode == CameraMode.Fly)
            {
                CacheProjection();
                cam.orthographic = false;
                cam.fieldOfView = flyFieldOfView;
                return;
            }

            RestoreProjection();
            cam.orthographic = true;   // isometric is orthographic by definition, whatever we restored
        }

        void CacheProjection()
        {
            if (projectionCached) return;
            projectionCached = true;
            cachedOrthographic = cam.orthographic;
            cachedOrthographicSize = cam.orthographicSize;
            cachedFieldOfView = cam.fieldOfView;
        }

        void RestoreProjection()
        {
            if (!projectionCached || cam == null) return;
            cam.orthographic = cachedOrthographic;
            cam.orthographicSize = cachedOrthographicSize;
            cam.fieldOfView = cachedFieldOfView;
        }
    }
}
