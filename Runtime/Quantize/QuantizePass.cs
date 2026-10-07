using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class QuantizePass : ScriptableRenderPass
    {
        static readonly int QuantizeLevelsId = Shader.PropertyToID("_QuantizeLevels");
        static readonly int DitherEnabledId = Shader.PropertyToID("_DitherEnabled");
        static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");

        Material material;
        bool warnedBackBuffer;

        public QuantizePass()
        {
            // keeps camera color in an intermediate texture past post-processing, since the backbuffer can't be sampled
            requiresIntermediateTexture = true;
        }

        public void Setup(Material quantizeMaterial)
        {
            material = quantizeMaterial;
        }

        class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var settings = SagaDisplaySettings.Active;
            if (material == null || settings == null || !settings.Quantize) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera != settings.WorldCamera) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                if (!warnedBackBuffer)
                {
                    Debug.LogWarning("[QuantizePass] Camera color is already the backbuffer at this injection point; quantize skipped. Try an earlier injection point.");
                    warnedBackBuffer = true;
                }
                return;
            }

            material.SetFloat(QuantizeLevelsId, settings.QuantizeLevels);
            material.SetFloat(DitherEnabledId, settings.Dither ? 1f : 0f);
            material.SetFloat(DitherStrengthId, settings.DitherStrength);

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc description = renderGraph.GetTextureDesc(source);
            description.name = "_SagaQuantized";
            description.clearBuffer = false;
            description.msaaSamples = MSAASamples.None;
            TextureHandle destination = renderGraph.CreateTexture(description);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Quantize", out var passData))
            {
                passData.source = source;
                passData.material = material;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0);
                // Blitter sets _BlitTexture / _BlitScaleBias
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc<PassData>(Execute);
            }

            // later passes and URP's final blit read the posterized copy
            resourceData.cameraColor = destination;
        }

        static void Execute(PassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
        }
    }
}
