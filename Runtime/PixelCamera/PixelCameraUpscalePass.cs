using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    // presents PixelCamera's internal RT full-screen; runs only on the camera carrying PixelCamera
    public class PixelCameraUpscalePass : ScriptableRenderPass
    {
        static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");

        Material material;

        public void Setup(Material upscaleMaterial)
        {
            material = upscaleMaterial;
        }

        class PassData
        {
            public RTHandle source;
            public Material material;
            public Vector4 scaleBias;
            public Rect viewport;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            // the world camera renders into the RT, and other cameras have no business running this blit
            if (!cameraData.camera.TryGetComponent<PixelCamera>(out _)) return;

            RTHandle rt = PixelCamera.InternalRTHandle;
            if (rt == null || rt.rt == null) return;

            material.SetVector(SourceSizeId, new Vector4(rt.rt.width, rt.rt.height, 1f / rt.rt.width, 1f / rt.rt.height));

            var resourceData = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("PixelCamera Upscale", out var passData);
            // bound directly rather than imported: importing a color+depth RT trips RenderGraph validation
            passData.source = rt;
            passData.material = material;
            passData.scaleBias = PixelCamera.BlitScaleBias;
            passData.viewport = PixelCamera.PresentViewport;

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.AllowPassCulling(false);
            // Blitter binds _BlitTexture / _BlitScaleBias as global state
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc<PassData>(Execute);
        }

        static void Execute(PassData data, RasterGraphContext context)
        {
            // the present camera never clears, so the bars would keep whatever was last drawn there
            context.cmd.ClearRenderTarget(false, true, Color.black);
            context.cmd.SetViewport(data.viewport);
            Blitter.BlitTexture(context.cmd, data.source, data.scaleBias, data.material, 0);
        }
    }
}
