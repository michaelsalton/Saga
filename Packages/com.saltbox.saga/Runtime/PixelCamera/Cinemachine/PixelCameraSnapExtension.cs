using Unity.Cinemachine;
using UnityEngine;

namespace Saga.Rendering
{
    /// <summary>
    /// DEPRECATED — inert. Camera grid-snapping moved into <see cref="PixelCameraController"/>, where it
    /// runs at render time (begin/endCameraRendering) and works for ANY camera driver — Cinemachine, the
    /// debug fly cam, scripted shots — not just when the brain is active. Snapping here as well would
    /// double-snap and cancel the Phase-2 loss offset.
    ///
    /// This stub remains only so scenes that already had the component attached don't break with a missing
    /// script. It does nothing. Safe to remove from the rig and delete this file once detached.
    /// </summary>
    public class PixelCameraSnapExtension : CinemachineExtension
    {
        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            // Intentionally empty — see class summary.
        }
    }
}
