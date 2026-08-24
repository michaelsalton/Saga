using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// C# half of <c>ShaderLibrary/CameraBasis.hlsl</c>: computes and publishes the orthographic camera basis
    /// that world-position reconstruction needs inside Blitter / fullscreen passes, where the inverse
    /// view-projection matrices are not dependable (see the header of
    /// <c>ShaderLibrary/Depth.hlsl</c>).
    ///
    /// Every pass that reconstructs a world position must publish the basis ITSELF — either with
    /// <see cref="Bind"/> (globals, for a pass that already modifies global state) or with
    /// <see cref="Apply"/> (onto its own material). GodrayPass and OutlinePass share
    /// <c>RenderPassEvent.AfterRenderingTransparents</c>, so neither can assume the other ran first.
    /// </summary>
    public static class SagaCameraBasis
    {
        static readonly int CamRightId = Shader.PropertyToID("_SagaCamRight");
        static readonly int CamUpId    = Shader.PropertyToID("_SagaCamUp");
        static readonly int CamFwdId   = Shader.PropertyToID("_SagaCamFwd");

        /// <summary>
        /// Camera right/up/forward in world space, with the orthographic HALF-extents packed into
        /// right.w and up.w.
        ///
        /// The half-extents are derived from the SAME projection matrix the depth buffer was rasterised
        /// with, so a reconstruction can never disagree with the depth it unprojects. Deliberately not
        /// <c>unity_OrthoParams</c>: its header documents .x/.y as the camera's "width"/"height" while
        /// the engine fills them from <c>orthographicSize</c> — a half-extent — and nothing in Unity's
        /// own shaders reads them, so the convention is unverifiable from source. Getting it wrong
        /// silently scales every reconstructed world position by 2.
        ///
        /// The ROTATION comes from the transform, which is safe. The ORIGIN deliberately does not: it is
        /// <c>_WorldSpaceCameraPos</c> on the shader side, because PixelCameraController grid-snaps the
        /// camera position every frame and the global is what URP actually rendered with.
        /// </summary>
        public static void Compute(UniversalCameraData cameraData,
                                   out Vector4 camRight, out Vector4 camUp, out Vector4 camFwd)
        {
            var cam = cameraData.camera;
            var t = cam.transform;

            var proj = cameraData.GetProjectionMatrix();
            float halfW = Mathf.Abs(proj.m00) > 1e-6f
                        ? 1f / Mathf.Abs(proj.m00)
                        : cam.orthographicSize * cam.aspect;
            float halfH = Mathf.Abs(proj.m11) > 1e-6f
                        ? 1f / Mathf.Abs(proj.m11)
                        : cam.orthographicSize;

            camRight = new Vector4(t.right.x,   t.right.y,   t.right.z,   halfW);
            camUp    = new Vector4(t.up.x,      t.up.y,      t.up.z,      halfH);
            camFwd   = new Vector4(t.forward.x, t.forward.y, t.forward.z, 0f);
        }

        /// <summary>Publish the basis as shader globals. Needs AllowGlobalStateModification(true).</summary>
        public static void Bind(RasterCommandBuffer cmd, Vector4 camRight, Vector4 camUp, Vector4 camFwd)
        {
            cmd.SetGlobalVector(CamRightId, camRight);
            cmd.SetGlobalVector(CamUpId,    camUp);
            cmd.SetGlobalVector(CamFwdId,   camFwd);
        }

        /// <summary>
        /// Publish the basis onto one material instead of the global state. Preferred for a pass that
        /// owns its material, because it cannot be disturbed by — or disturb — another pass at the same
        /// injection point.
        /// </summary>
        public static void Apply(Material material, Vector4 camRight, Vector4 camUp, Vector4 camFwd)
        {
            material.SetVector(CamRightId, camRight);
            material.SetVector(CamUpId,    camUp);
            material.SetVector(CamFwdId,   camFwd);
        }
    }
}
