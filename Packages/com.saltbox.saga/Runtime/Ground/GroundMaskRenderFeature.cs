using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class GroundMaskRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Assign the Saga/GroundMask shader. The override material is created and owned by the " +
                     "feature.")]
            public Shader maskShader;

            [Tooltip("Layers drawn into the mask. MUST match GroundColorMap's Ground Mask — the baked map and " +
                     "the live probe have to agree on what 'ground' means. Default is Ground (layer 3).")]
            public LayerMask groundLayers = 1 << 3;

            [Tooltip("When the mask is produced. AfterRenderingOpaques is the only sane choice: it is after " +
                     "the depth copy the mask shader tests against, and before the Transparent queue where " +
                     "the grass consumes it.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingOpaques;
        }

        [SerializeField] Settings settings = new Settings();
        static readonly int MaskValidId = Shader.PropertyToID("_GroundMaskValid");

        Material maskMaterial;
        GroundMaskPass pass;

        public override void Create()
        {
            pass = new GroundMaskPass();
            if (settings.maskShader != null)
                maskMaterial = CoreUtils.CreateEngineMaterial(settings.maskShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (maskMaterial == null) return;

            Shader.SetGlobalFloat(MaskValidId, 1f);

            pass.renderPassEvent = settings.injectionPoint;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            pass.Setup(maskMaterial, settings.groundLayers);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            Shader.SetGlobalFloat(MaskValidId, 0f);
            CoreUtils.Destroy(maskMaterial);
            maskMaterial = null;
            pass = null;
        }
    }
}
