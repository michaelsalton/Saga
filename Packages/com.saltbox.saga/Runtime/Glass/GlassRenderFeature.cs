using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    // URP renderer feature that owns the Saga/Glass draw. Glass is tagged LightMode "SagaGlass" so URP's own
    // transparent pass skips it, and the draw set is the GlassSurface registry rather than a layer filter --
    // so a glass object renders only while it carries an enabled GlassSurface AND this feature is on the
    // renderer. See GlassPass for why glass is drawn one object at a time.
    //
    // Order matters at a shared injection point: features run in renderer-asset list order, so this sits
    // ABOVE GodrayRenderFeature (also event 500) to get rays landing on the glass surface.
    public class GlassRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("When the grabs and the glass draws happen. AfterRenderingTransparents (500) is the " +
                     "default and the reason is what has to be in the first grab.\n\n" +
                     "Water and grass are Transparent-queue (3000) and the outline composites at 450, so 500 " +
                     "is the first point where all three are in the camera color and can be refracted. " +
                     "Moving this earlier drops whatever has not drawn yet back out of the glass.\n\n" +
                     "Do NOT use AfterRendering or AfterRenderingPostProcessing — by then the active target " +
                     "has switched to the backbuffer and the pass self-guards to nothing.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;
        }

        [SerializeField] Settings settings = new Settings();

        GlassPass pass;

        public override void Create()
        {
            pass = new GlassPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            pass.renderPassEvent = settings.injectionPoint;

            // The glass shader samples _CameraDepthTexture for its refraction foreground clamp. The RP asset
            // already forces the depth texture on, but declaring it here is the ConfigureInput contract.
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass = null;
        }
    }
}
