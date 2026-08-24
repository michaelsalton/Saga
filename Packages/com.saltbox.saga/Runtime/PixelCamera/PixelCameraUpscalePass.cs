using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// RenderGraph pass that presents the internal RT to the screen: a single point-sampled blit into the
    /// centered, integer-scaled viewport, with letterbox bars filling the remainder. The source-UV mapping
    /// (crop out of the overscan + sub-pixel loss offset + platform V-flip) is computed by
    /// <see cref="PixelCameraController"/> and arrives ready-made as the blit scaleBias. Runs only on the
    /// composite camera.
    /// </summary>
    public class PixelCameraUpscalePass : ScriptableRenderPass
    {
        static readonly int QuantizeEnabledId = Shader.PropertyToID("_QuantizeEnabled");
        static readonly int QuantizeLevelsId = Shader.PropertyToID("_QuantizeLevels");
        static readonly int DitherEnabledId = Shader.PropertyToID("_DitherEnabled");
        static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");
        static readonly int InternalResId = Shader.PropertyToID("_InternalRes");

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
            public Color letterbox;
            public Rect viewport;
            public Rect fullTarget;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();

            // Present only on the composite camera; the world camera renders into the RT (so it has a
            // target texture) and other cameras have no business running this blit.
            if (!cameraData.camera.TryGetComponent<PixelCameraComposite>(out var composite)) return;

            RTHandle rt = PixelCameraController.InternalRTHandle;
            if (rt == null || rt.rt == null) return;

            // Color & quantization: fold the final-internal-res quantize + grid-locked dither into this
            // point upscale. Each display block samples one internal texel, so quantizing here is identical
            // to quantizing at internal res. When Quantize is off the shader branch is skipped → byte-identical.
            material.SetFloat(QuantizeEnabledId, composite.Quantize ? 1f : 0f);
            material.SetFloat(QuantizeLevelsId, composite.QuantizeLevels);
            material.SetFloat(DitherEnabledId, composite.Quantize && composite.Dither ? 1f : 0f);
            material.SetFloat(DitherStrengthId, composite.DitherStrength);
            material.SetVector(InternalResId, new Vector4(PixelCameraController.InternalWidth, PixelCameraController.InternalHeight, 0f, 0f));

            var resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle dest = resourceData.activeColorTexture;
            var desc = cameraData.cameraTargetDescriptor;

            using var builder = renderGraph.AddRasterRenderPass<PassData>("PixelCamera Upscale", out var passData);
            // Bind the RT directly rather than ImportTexture: it's a persistent external texture already
            // filled by the world camera this frame, and importing a color+depth RT trips RG validation.
            passData.source = rt;
            passData.material = material;
            passData.scaleBias = PixelCameraController.BlitScaleBias;
            passData.letterbox = PixelCameraController.LetterboxColor;
            passData.viewport = PixelCameraController.ViewportRect;
            passData.fullTarget = new Rect(0f, 0f, desc.width, desc.height);

            builder.SetRenderAttachment(dest, 0);
            builder.AllowPassCulling(false);
            // Blitter binds _BlitTexture / _BlitScaleBias as global state.
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc<PassData>(Execute);
        }

        static void Execute(PassData data, RasterGraphContext context)
        {
            var cmd = context.cmd;

            // Paint the whole backbuffer with the letterbox color first, then restrict the upscale draw
            // to the centered integer-scaled rect so the remainder shows as bars.
            cmd.SetViewport(data.fullTarget);
            cmd.ClearRenderTarget(RTClearFlags.Color, data.letterbox, 1f, 0);
            cmd.SetViewport(data.viewport);

            Blitter.BlitTexture(cmd, data.source, data.scaleBias, data.material, 0);
        }
    }
}
