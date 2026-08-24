using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// RenderGraph pass that composites the <c>Saga/Outline</c> edge detect into the world camera's active
    /// color (the internal RT) with an alpha blend. Reads <c>_CameraDepthTexture</c> (requested via
    /// <see cref="ScriptableRenderPass.ConfigureInput"/> on the feature) and writes the color attachment;
    /// the outline never samples color (the blend reads dst in hardware), so there is no read/write hazard.
    ///
    /// Runs only on the world camera that draws the scene into the internal RT — the composite present
    /// camera renders to the backbuffer (guarded out below), and scene-view/preview cameras are skipped.
    /// </summary>
    public class OutlinePass : ScriptableRenderPass
    {
        static readonly int OutlineColorId      = Shader.PropertyToID("_OutlineColor");
        static readonly int DepthThresholdId    = Shader.PropertyToID("_DepthThreshold");
        static readonly int NormalThresholdId   = Shader.PropertyToID("_NormalThreshold");
        static readonly int OutlineTexelId      = Shader.PropertyToID("_OutlineTexel");
        static readonly int OutlineShadowTintId = Shader.PropertyToID("_OutlineShadowTint");
        static readonly int OutlineLightingId   = Shader.PropertyToID("_OutlineLighting");

        Material material;
        OutlineRenderFeature.Settings settings;

        public void Setup(Material mat, OutlineRenderFeature.Settings s)
        {
            material = mat;
            settings = s;
        }

        class PassData
        {
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            // World camera only: skip scene view / preview / reflection probes.
            if (cameraData.cameraType != CameraType.Game) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            // The composite present camera targets the backbuffer; the world camera targets the internal RT.
            // Guarding on this also keeps us from ever drawing after the final blit's switch-to-backbuffer.
            if (resourceData.isActiveTargetBackBuffer) return;

            // Live settings -> material. Texel comes from the actual render target (the internal RT, incl.
            // overscan), so the kernel samples exact 1px neighbors regardless of the downsample slider.
            var desc = cameraData.cameraTargetDescriptor;
            int w = Mathf.Max(1, desc.width);
            int h = Mathf.Max(1, desc.height);

            material.SetColor(OutlineColorId, settings.outlineColor);
            material.SetFloat(DepthThresholdId, Mathf.Max(0f, settings.depthThreshold));
            material.SetFloat(NormalThresholdId, settings.normalThreshold);
            material.SetVector(OutlineTexelId, new Vector4(1f / w, 1f / h, w, h));

            // Shading the line means unprojecting its texel back to a world position, and
            // SagaCameraBasis is ORTHOGRAPHIC-only. Scene view and previews are already filtered out
            // above, but a game camera could still be switched to perspective — fall back to the flat
            // line rather than lighting it from a position that would be wrong.
            var cam = cameraData.camera;
            bool lit = settings.lightingStrength > 0f && cam != null && cam.orthographic;

            material.SetColor(OutlineShadowTintId, settings.shadowTint);
            material.SetFloat(OutlineLightingId, lit ? Mathf.Clamp01(settings.lightingStrength) : 0f);

            if (lit)
            {
                // Published onto our OWN material rather than as globals. GodrayPass shares this
                // injection point and publishes the identical basis globally; keeping ours local means
                // the two can never race on renderer-asset ordering. See Core/SagaCameraBasis.cs.
                SagaCameraBasis.Compute(cameraData, out var camRight, out var camUp, out var camFwd);
                SagaCameraBasis.Apply(material, camRight, camUp, camFwd);
            }

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Outline", out var passData);

            passData.material = material;

            // Read depth: schedules the depth resource before this pass. _CameraDepthTexture stays globally
            // bound (ConfigureInput(Depth)); UseAllGlobalTextures lets SampleSceneDepth read that global here.
            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

            // Explicit dependency on the shadow map, so it is scheduled and kept alive this far into the
            // frame. UseAllGlobalTextures below would cover it (URP registers _MainLightShadowmapTexture
            // via SetGlobalTextureAfterPass), but naming it is what GodrayPass does and it makes the
            // requirement legible. Invalid whenever the main light casts no shadows — the shader's
            // keyword variant then has no MAIN_LIGHT_CALCULATE_SHADOWS and simply reads 1.
            if (resourceData.mainShadowsTexture.IsValid())
                builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);

            builder.UseAllGlobalTextures(true);

            // Write into the world camera's active color (internal RT). Prior scene content is loaded (not
            // cleared), so the alpha blend composites the line over the real pixels.
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true); // Blitter sets _BlitTexture / _BlitScaleBias globals
            builder.SetRenderFunc<PassData>(Execute);
        }

        static void Execute(PassData data, RasterGraphContext context)
        {
            // Fullscreen alpha-blended line composite using the source-less Blitter overload (binds no
            // _BlitTexture; the shader samples _CameraDepthTexture). scaleBias (1,1,0,0): identity uv into
            // the internal RT — no flip, because the sampled depth and the destination live in the same
            // internal-RT space (where +v is screen-up on D3D, since URP y-flips projections into RTs).
            Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
        }
    }
}
