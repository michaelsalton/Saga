using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class GodrayPass : ScriptableRenderPass
    {
        Material material;

        public void Setup(Material mat)
        {
            material = mat;
        }

        class PassData
        {
            public Material material;
            public Vector4 camRight;
            public Vector4 camUp;
            public Vector4 camFwd;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game) return;
            if (cameraData.camera == null || !cameraData.camera.orthographic) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Godrays", out var passData);

            passData.material = material;

            // Basis + ortho half-extents. The derivation moved to SagaCameraBasis when the outline
            // became a second consumer; the reasoning behind it lives in that file's summary.
            SagaCameraBasis.Compute(cameraData, out passData.camRight, out passData.camUp, out passData.camFwd);

            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

            if (resourceData.mainShadowsTexture.IsValid())
                builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);

            builder.UseAllGlobalTextures(true);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true); // Blitter's own globals, plus the basis set below
            builder.SetRenderFunc<PassData>(Execute);
        }

        static void Execute(PassData data, RasterGraphContext context)
        {
            SagaCameraBasis.Bind(context.cmd, data.camRight, data.camUp, data.camFwd);
            Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
        }
    }
}
