using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// URP renderer feature that presents the pixel camera's internal RT to the screen: a single
    /// point-sampled upscale blit, centered and integer-scaled, with letterbox bars. Add it to the URP
    /// Renderer used by the composite camera (it self-guards to only run on a camera carrying
    /// <see cref="PixelCameraComposite"/>, so it's harmless on the world/UI cameras that share the renderer).
    ///
    /// Authored against RenderGraph (URP 17 / Unity 6) so later feature passes — outlines, fog, quantize —
    /// slot into the same model without a rewrite. Those later passes run on the WORLD camera at internal
    /// res; this feature is only the final present.
    /// </summary>
    public class PixelCameraRenderFeature : ScriptableRendererFeature
    {
        [Tooltip("Assign the Saga/PixelCameraUpscale shader. The material is created and owned by the feature.")]
        [SerializeField] Shader upscaleShader;

        Material upscaleMaterial;
        PixelCameraUpscalePass pass;

        public override void Create()
        {
            pass = new PixelCameraUpscalePass { renderPassEvent = RenderPassEvent.AfterRendering };
            if (upscaleShader != null)
                upscaleMaterial = CoreUtils.CreateEngineMaterial(upscaleShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (upscaleMaterial == null) return;
            pass.Setup(upscaleMaterial);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(upscaleMaterial);
            upscaleMaterial = null;
        }
    }
}
