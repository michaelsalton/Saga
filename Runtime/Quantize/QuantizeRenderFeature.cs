using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class QuantizeRenderFeature : ScriptableRendererFeature
    {
        [Tooltip("Assign the Saga/Quantize shader. The material is created and owned by the feature.")]
        [SerializeField] Shader quantizeShader;

        [Tooltip("When the posterize runs. AfterRenderingPostProcessing makes it the last colour step, after bloom " +
                 "and tonemapping. If the console reports the backbuffer was active, move it earlier.")]
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        Material material;
        QuantizePass pass;

        public override void Create()
        {
            pass = new QuantizePass();
            if (quantizeShader != null)
                material = CoreUtils.CreateEngineMaterial(quantizeShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null) return;

            pass.renderPassEvent = injectionPoint;
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
