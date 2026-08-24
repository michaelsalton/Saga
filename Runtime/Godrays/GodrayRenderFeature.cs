using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class GodrayRenderFeature : ScriptableRendererFeature
    {
        [Tooltip("Assign the Saga/Godray shader. The material is created and owned by the feature.")]
        [SerializeField] Shader godrayShader;

        [Tooltip("When the shafts draw. AfterRenderingTransparents (500) is correct and is what the outline " +
                 "uses. Do NOT use BeforeRenderingTransparents (450) — water draws in the transparent queue " +
                 "returning alpha 1 and would cover the shafts entirely. Do NOT use AfterRendering or " +
                 "AfterRenderingPostProcessing — by then the active target has switched to the backbuffer.")]
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

        Material material;
        GodrayPass pass;

        public override void Create()
        {
            pass = new GodrayPass();
            if (godrayShader != null)
                material = CoreUtils.CreateEngineMaterial(godrayShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null) return;

            pass.renderPassEvent = injectionPoint;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            pass.Setup(material);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }
    }
}
