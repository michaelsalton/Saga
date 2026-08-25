using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    // ------------------------------------------------------------------------------------------------
    // THE GRAB CHAIN.
    //
    // Saga/Glass, Saga/Water and Saga/Grass are all the same kind of object: a fragment that reads a copy of
    // the scene colour, does its own transparency in the shader, and returns alpha 1. They sit in the
    // Transparent queue only because the copy they read (_CameraOpaqueTexture) does not exist until the
    // opaque queue closes. That makes them mutually blind -- none of them is in the copy the others read,
    // and each one's alpha-1 write erases whatever an earlier one put in the framebuffer. The outline is a
    // third casualty: it composites at event 450, after URP takes the opaque copy and before the transparent
    // queue draws, so it is in neither the copy nor the surviving framebuffer.
    //
    // This pass breaks glass out of that cohort. It runs after the transparent queue, so water, grass and the
    // outline are all in the camera colour by the time it starts, and it copies that colour into
    // _SagaSceneColor for the glass to refract. Refracting a copy that already contains the pixel underneath
    // is also what makes the alpha-1 write correct rather than destructive.
    //
    // One grab is not enough, because glass has to see GLASS. A single copy taken before any pane draws
    // contains no panes, so the nearer of two overlapping panes would refract a texture the farther one is
    // missing from and then overwrite the pixel where it landed -- the original bug, one level down. So the
    // grab is interleaved: sort the panes back-to-front, and for each one re-grab the colour as it stands,
    // then draw that pane alone. Pane n's grab therefore contains panes 0..n-1 with the absorption of every
    // pane between them already accumulated. The technique is DEPTH PEELING, and it is exact at any depth.
    //
    // Cost is 2N render passes for N glass renderers on screen -- a grab and a draw each. At 480x270 the
    // copies themselves are trivial (~2 MB of traffic apiece); what you pay for is the pass switches, since
    // each grab breaks pass merging. Panes that do not overlap in screen space could share a grab, which
    // would collapse a wall of windows to a single pair of passes. Not implemented.
    //
    // Glass is not in URP's transparent draw at all: its pass is tagged LightMode "SagaGlass", which URP does
    // not recognise. Nothing draws it but this. Note that DrawRenderer addresses the pass by INDEX, so the
    // tag's only job here is keeping URP's own draw off it.
    // ------------------------------------------------------------------------------------------------
    public class GlassPass : ScriptableRenderPass
    {
        static readonly int SceneColorId = Shader.PropertyToID("_SagaSceneColor");

        // Glass.shader has one pass, and DrawRenderer takes its index rather than matching a LightMode tag.
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

        // Reused across frames. URP records the graph per camera, so one buffer is enough and the pass stays
        // allocation-free.
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

            // Nothing to grab once the active target is the backbuffer. If glass ever disappears from the
            // scene view, this guard is why -- that camera fell back to rendering straight to the backbuffer
            // instead of through an intermediate texture.
            if (resourceData.isActiveTargetBackBuffer) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.camera == null) return;

            BuildSortedDrawList(cameraData.camera);
            if (drawList.Count == 0) return;

            // Size and format come off the internal RT so each grab is a like-for-like copy of what it reads
            // and screenUV indexes it identically. Inheriting graphicsFormat is what keeps HDR values from
            // being clipped on the way into the glass.
            //
            // MSAA off: the glass samples this as a plain Texture2D, not a Texture2DMS.
            //
            // Clamp, not Repeat: an offset UV that leaves the screen must smear the edge rather than wrap to
            // the far side. GlassRefraction fades the offset out over that border regardless, but a wrapped
            // sample would put the opposite edge of the frame inside the glass.
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
                // A fresh handle per pane, because a graph texture cannot be written and read by the same
                // pass. RenderGraph aliases the pooled memory once it sees each one is written then read
                // once, so N handles is not N allocations.
                TextureHandle sceneColor = renderGraph.CreateTexture(td);

                using (var builder = renderGraph.AddRasterRenderPass<GrabData>("Saga Glass Grab", out var grabData))
                {
                    grabData.source = resourceData.activeColorTexture;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(sceneColor, 0);

                    // Rebound once per pane. The registry is add-order keyed, so the draw pass added
                    // immediately below is the consumer that sees this particular copy.
                    builder.SetGlobalTextureAfterPass(sceneColor, SceneColorId);

                    builder.AllowGlobalStateModification(true); // Blitter sets _BlitTexture / _BlitScaleBias
                    builder.SetRenderFunc<GrabData>(ExecuteGrab);
                }

                using (var builder = renderGraph.AddRasterRenderPass<DrawData>("Saga Glass", out var drawData))
                {
                    drawData.renderer = drawList[i].renderer;
                    drawData.materials = drawList[i].materials;

                    builder.UseTexture(sceneColor, AccessFlags.Read);

                    // The glass samples _CameraDepthTexture for its refraction foreground clamp. This is the
                    // copy, not the attachment bound below.
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                    // Read-only: the glass pass is ZWrite Off, but ZTest LEqual still needs the opaque depth
                    // bound or glass draws through the walls it is standing behind.
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                    builder.SetRenderFunc<DrawData>(ExecuteDraw);
                }
            }
        }

        // Flatten the registry into this frame's draw set, farthest pane first. isVisible was updated by this
        // camera's culling earlier in the frame, so off-screen panes drop out and cost nothing.
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

                    // Distance along the VIEW AXIS, not to the camera. Under ortho every view ray is parallel
                    // to cameraForward, so projecting onto it is the depth order -- radial distance would sort
                    // panes at the screen edge as further away than they are.
                    //
                    // Bounds centre, the same proxy Unity's own transparent sort uses, with the same failure:
                    // two interpenetrating panes can still come out in the wrong order.
                    float depth = Vector3.Dot(renderer.bounds.center - cameraPosition, cameraForward);
                    InsertFarthestFirst(new DrawItem(renderer, targets[j].materials, depth));
                }
            }
        }

        // Insertion sort, farthest first, so each pane's grab already contains everything behind it. N is the
        // glass renderers visible in a 17.8x10 m viewport, small enough that this beats List.Sort and unlike
        // List.Sort(Comparison) it allocates nothing per frame.
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

            // One draw per submesh: DrawRenderer without an index only emits submesh 0, which would leave
            // holes in a multi-material pane.
            for (int i = 0; i < data.materials.Length; i++)
            {
                if (data.materials[i] == null) continue;
                context.cmd.DrawRenderer(data.renderer, data.materials[i], i, GlassShaderPass);
            }
        }
    }
}
