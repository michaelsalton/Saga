using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class GlassPass : ScriptableRenderPass
    {
        static readonly int SceneColorId = Shader.PropertyToID("_SagaSceneColor");

        const int GlassShaderPass = 0;

        readonly struct DrawItem
        {
            public readonly Renderer renderer;
            public readonly Material[] materials;
            public readonly float viewDepth;

            public DrawItem(Renderer r, Material[] m, float depth)
            {
                renderer = r;
                materials = m;
                viewDepth = depth;
            }
        }

        readonly List<DrawItem> drawList = new List<DrawItem>();

        class GrabData
        {
            public TextureHandle source;
        }

        class DrawData
        {
            public Renderer renderer;
            public Material[] materials;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera == null) return;

            BuildSortedDrawList(cameraData.camera);
            if (drawList.Count == 0) return;

            var desc = cameraData.cameraTargetDescriptor;
            var td = new TextureDesc(Mathf.Max(1, desc.width), Mathf.Max(1, desc.height))
            {
                format          = desc.graphicsFormat,
                msaaSamples     = MSAASamples.None,
                depthBufferBits = DepthBits.None,
                clearBuffer     = false, // the blit covers every pixel
                filterMode      = FilterMode.Bilinear,
                wrapMode        = TextureWrapMode.Clamp,
                name            = "_SagaSceneColor",
            };

            for (int i = 0; i < drawList.Count; i++)
            {
                TextureHandle sceneColor = renderGraph.CreateTexture(td);

                using (var builder = renderGraph.AddRasterRenderPass<GrabData>("Saga Glass Grab", out var grabData))
                {
                    grabData.source = resourceData.activeColorTexture;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(sceneColor, 0);

                    builder.SetGlobalTextureAfterPass(sceneColor, SceneColorId);

                    builder.AllowGlobalStateModification(true); // Blitter sets _BlitTexture / _BlitScaleBias
                    builder.SetRenderFunc<GrabData>(ExecuteGrab);
                }

                using (var builder = renderGraph.AddRasterRenderPass<DrawData>("Saga Glass", out var drawData))
                {
                    drawData.renderer = drawList[i].renderer;
                    drawData.materials = drawList[i].materials;

                    builder.UseTexture(sceneColor, AccessFlags.Read);

                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                    builder.SetRenderFunc<DrawData>(ExecuteDraw);
                }
            }
        }

        void BuildSortedDrawList(Camera camera)
        {
            drawList.Clear();

            Vector3 cameraPosition = camera.transform.position;
            Vector3 cameraForward = camera.transform.forward;

            var surfaces = GlassSurface.Active;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                if (surface == null) continue;

                var targets = surface.Targets;
                for (int j = 0; j < targets.Count; j++)
                {
                    var renderer = targets[j].renderer;
                    if (renderer == null || !renderer.enabled) continue;
                    if (!renderer.gameObject.activeInHierarchy || !renderer.isVisible) continue;
                    if (targets[j].materials.Length == 0) continue;

                    float depth = Vector3.Dot(renderer.bounds.center - cameraPosition, cameraForward);
                    InsertFarthestFirst(new DrawItem(renderer, targets[j].materials, depth));
                }
            }
        }

        void InsertFarthestFirst(DrawItem item)
        {
            int i = drawList.Count;
            drawList.Add(item);

            while (i > 0 && drawList[i - 1].viewDepth < item.viewDepth)
            {
                drawList[i] = drawList[i - 1];
                i--;
            }

            drawList[i] = item;
        }

        static void ExecuteGrab(GrabData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
        }

        static void ExecuteDraw(DrawData data, RasterGraphContext context)
        {
            if (data.renderer == null) return;

            for (int i = 0; i < data.materials.Length; i++)
            {
                if (data.materials[i] == null) continue;
                context.cmd.DrawRenderer(data.renderer, data.materials[i], i, GlassShaderPass);
            }
        }
    }
}
