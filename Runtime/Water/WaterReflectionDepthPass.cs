using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// Linearizes the reflection camera's depth into <see cref="WaterReflection.DepthTarget"/>, so
    /// Saga/Water can tell how far away the thing it is reflecting actually is.
    ///
    /// The target is a PERSISTENT RenderTexture owned by <see cref="WaterReflection"/> and imported here,
    /// not a RenderGraph texture. It has to be: the water reads it during the MAIN camera's render, which
    /// is a separate graph execution, and a transient graph resource does not survive that boundary.
    /// (Contrast <see cref="GroundMaskPass"/>, whose mask is consumed inside the same camera's frame and can
    /// therefore use SetGlobalTextureAfterPass.)
    /// </summary>
    public class WaterReflectionDepthPass : ScriptableRenderPass
    {
        Material material;

        public void Setup(Material mat)
        {
            material = mat;
        }

        class PassData { public Material material; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            RTHandle target = WaterReflection.DepthTarget;
            if (target == null || target.rt == null) return;

            // Gated twice on purpose: this pass lives on the clean renderer, which the ground-colour
            // capture camera also uses. The feature's AddRenderPasses gate keeps ConfigureInput(Depth) off
            // that camera; this one keeps the blit off it even if the feature is ever re-wired.
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera != WaterReflection.ActiveReflectionCamera) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.cameraDepthTexture.IsValid()) return;

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Water Reflection Depth",
                                                                         out var passData);
            passData.material = material;

            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
            builder.UseAllGlobalTextures(true);
            builder.SetRenderAttachment(renderGraph.ImportTexture(target), 0);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);   // Blitter sets its own globals
            builder.SetRenderFunc<PassData>(static (PassData d, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), d.material, 0));
        }
    }
}
