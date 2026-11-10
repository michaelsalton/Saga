using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class BackdropBlurRenderFeature : ScriptableRendererFeature
    {
        [Tooltip("Assign the Hidden/Saga/BackdropBlur shader. The material is created and owned by the feature.")]
        [SerializeField] Shader blurShader;

        [Tooltip("Each level halves resolution and roughly doubles the blur radius.")]
        [SerializeField, Range(1, 8)] int levels = 4;

        Material material;
        BackdropBlurPass pass;

        public override void Create()
        {
            pass = new BackdropBlurPass();
            if (blurShader != null)
                material = CoreUtils.CreateEngineMaterial(blurShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || BackdropBlur.Amount <= 0f) return;

            pass.Setup(material, levels);
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
