using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Saga.Rendering
{
    public class GroundMaskPass : ScriptableRenderPass
    {
        static readonly int GroundMaskTexId = Shader.PropertyToID("_GroundMaskTex");

        static readonly ShaderTagId[] ShaderTags =
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        Material overrideMaterial;
        int layerMask;

        public void Setup(Material maskMat, LayerMask layers)
        {
            overrideMaterial = maskMat;
            layerMask = layers;
        }

        class PassData { public RendererListHandle list; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (overrideMaterial == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var renderingData = frameData.Get<UniversalRenderingData>();

            var desc = cameraData.cameraTargetDescriptor;
            var td = new TextureDesc(Mathf.Max(1, desc.width), Mathf.Max(1, desc.height))
            {
                format = GraphicsFormat.R8_UNorm,
                msaaSamples= MSAASamples.None,
                depthBufferBits = DepthBits.None,
                clearBuffer = true,
                clearColor = Color.clear,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "_GroundMaskTex",
            };
            TextureHandle maskRT = renderGraph.CreateTexture(td);

            var sorting = new SortingSettings(cameraData.camera) { criteria = cameraData.defaultOpaqueSortFlags };
            var draw = new DrawingSettings(ShaderTags[0], sorting)
            {
                overrideMaterial          = overrideMaterial,
                overrideMaterialPassIndex = 0,
                perObjectData             = PerObjectData.None,
            };
            for (int i = 1; i < ShaderTags.Length; i++)
                draw.SetShaderPassName(i, ShaderTags[i]);

            var filter = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            var param = new RendererListParams(renderingData.cullResults, draw, filter);

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Ground Mask", out var passData);

            passData.list = renderGraph.CreateRendererList(param);

            builder.SetRenderAttachment(maskRT, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.UseAllGlobalTextures(true);
            if (passData.list.IsValid())
            {
                builder.UseRendererList(passData.list);
            }

            builder.SetGlobalTextureAfterPass(maskRT, GroundMaskTexId);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<PassData>(static (PassData d, RasterGraphContext ctx) =>
            {
                if (d.list.IsValid())
                    ctx.cmd.DrawRendererList(d.list);
            });
        }
    }
}
