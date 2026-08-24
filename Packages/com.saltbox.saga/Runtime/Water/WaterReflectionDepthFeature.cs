using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// Captures the reflection camera's linearized depth for distance-based reflections.
    ///
    /// Goes on the SAME feature-less renderer <see cref="WaterReflection"/> already points its mirror camera
    /// at (GroundCapture_Renderer, index 1). Step 08 chose that renderer precisely for having no features,
    /// and <see cref="GroundColorMap"/>'s capture camera shares it — so the camera gate below is what
    /// preserves that guarantee. For any camera other than the live reflection camera this feature enqueues
    /// nothing at all, which also keeps ConfigureInput(Depth) — and the depth prepass it forces — off the
    /// ground capture.
    ///
    /// Nothing breaks if this feature is absent: the water's depth sample reads 0, every distance dial
    /// becomes a no-op, and reflections behave exactly as they did before the amendment.
    /// </summary>
    public class WaterReflectionDepthFeature : ScriptableRendererFeature
    {
        [Tooltip("Assign the Saga/WaterReflectionDepth shader. The material is created and owned by the " +
                 "feature.")]
        [SerializeField] Shader depthShader;

        [Tooltip("When the depth is linearized. AfterRenderingOpaques is correct: it is after URP has " +
                 "produced the depth texture and after everything opaque has written to it. The reflection " +
                 "camera draws nothing transparent that should occlude a reflection.")]
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;

        static readonly int DepthValidId = Shader.PropertyToID("_WaterReflectionDepthValid");

        Material material;
        WaterReflectionDepthPass pass;

        public override void Create()
        {
            pass = new WaterReflectionDepthPass();
            if (depthShader != null)
                material = CoreUtils.CreateEngineMaterial(depthShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null) return;

            Camera reflCam = WaterReflection.ActiveReflectionCamera;
            if (reflCam == null || renderingData.cameraData.camera != reflCam) return;
            if (WaterReflection.DepthTarget == null) return;

            // Published here rather than in WaterReflection.cs so it tracks the FEATURE being installed and
            // reachable, not merely the component existing. Same shape as GroundMaskRenderFeature's
            // _GroundMaskValid.
            Shader.SetGlobalFloat(DepthValidId, 1f);

            pass.renderPassEvent = injectionPoint;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            pass.Setup(material);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            Shader.SetGlobalFloat(DepthValidId, 0f);
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }
    }
}
