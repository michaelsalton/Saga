using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Saga.Rendering
{
    /// <summary>
    /// Renders the opted-in objects into the per-object outline CONTROL texture (<c>_OutlineControl</c>), at
    /// internal resolution. Runs at <see cref="RenderPassEvent.AfterRenderingOpaques"/> on the world camera,
    /// ahead of OutlinePass, and publishes the texture as a global the outline kernel samples.
    ///
    /// Channels (RGBA8): R = object ID (idEdge separation), G = normal/crease term weight, B = depth/silhouette
    /// term weight, A = threshold bias (0.5 = neutral). The RT is CLEARED to "not outlined" (0, 0, 0, 0.5) —
    /// no id, zero weights — so this is an OPT-IN model: geometry that is never drawn here gets no line.
    ///
    /// The draw set is the registry of enabled <see cref="OutlineControl"/> components, NOT a layer-filtered
    /// renderer list: an object without the script is skipped by this pass outright, costing nothing at all.
    /// Per-object values are bound as a global immediately before each draw, because CommandBuffer.DrawRenderer
    /// takes no MaterialPropertyBlock — hence the <c>AllowGlobalStateModification</c> below.
    ///
    /// Occlusion is done IN-SHADER against the resolved <c>_CameraDepthTexture</c> (LoadSceneDepth + clip in
    /// Saga/OutlineControl), NOT by binding the scene depth as an attachment. Binding the depth attachment
    /// would force this RT to match the scene's MSAA sample count, and an MSAA control target can't be cleanly
    /// point-sampled for IDs. This keeps the control RT MSAA-free regardless of the pipeline MSAA setting, and
    /// makes draw order here irrelevant — only the frontmost surface survives per pixel.
    /// </summary>
    public class OutlineControlPass : ScriptableRenderPass
    {
        static readonly int ControlTexId = Shader.PropertyToID("_OutlineControl");
        static readonly int ControlValuesId = Shader.PropertyToID("_OutlineControlValues");

        // Clear = "not outlined": no id, both term weights zero. Alpha stays 0.5 because A is the threshold
        // bias (the kernel reads thrScale = a * 2), not an enable flag — 0 there would mean "infinitely
        // sensitive", not "off".
        static readonly Color NoOutline = new Color(0f, 0f, 0f, 0.5f);

        Material overrideMaterial;

        readonly struct DrawItem
        {
            public readonly Renderer renderer;
            public readonly int subMeshCount;
            public readonly Vector4 values;
            public DrawItem(Renderer r, int count, Vector4 v) { renderer = r; subMeshCount = count; values = v; }
        }

        // Reused across frames: URP records and executes the graph per camera, so one buffer is enough and
        // this keeps the pass allocation-free.
        readonly List<DrawItem> drawList = new List<DrawItem>();

        public void Setup(Material controlMat)
        {
            overrideMaterial = controlMat;
        }

        class PassData
        {
            public Material material;
            public List<DrawItem> items;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (overrideMaterial == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            // Control RT: internal-res RGBA8, MSAA off, no depth of its own, point/clamp, cleared to the
            // not-outlined default. Built via TextureDesc (not the public CreateRenderGraphTexture overload)
            // so we can set a non-black clear color for the neutral threshold bias in A.
            var desc = cameraData.cameraTargetDescriptor;
            int w = Mathf.Max(1, desc.width);
            int h = Mathf.Max(1, desc.height);
            var td = new TextureDesc(w, h)
            {
                format          = GraphicsFormat.R8G8B8A8_UNorm,
                msaaSamples     = MSAASamples.None,
                depthBufferBits = DepthBits.None,
                clearBuffer     = true,
                clearColor      = NoOutline,
                filterMode      = FilterMode.Point,
                wrapMode        = TextureWrapMode.Clamp,
                name            = "_OutlineControl",
            };
            TextureHandle controlRT = renderGraph.CreateTexture(td);

            // Flatten the component registry into this frame's draw set. isVisible was updated by this
            // camera's culling earlier in the frame, so off-screen renderers drop out with no pop-in.
            drawList.Clear();
            var controls = OutlineControl.Active;
            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                if (control == null) continue;

                var values = control.Values;
                var targets = control.Targets;
                for (int j = 0; j < targets.Count; j++)
                {
                    var r = targets[j].renderer;
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy && r.isVisible)
                        drawList.Add(new DrawItem(r, targets[j].subMeshCount, values));
                }
            }

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Outline Control", out var passData);

            passData.material = overrideMaterial;
            passData.items    = drawList;

            builder.SetRenderAttachment(controlRT, 0, AccessFlags.Write);
            // No depth attachment: occlusion is in-shader (clip vs resolved _CameraDepthTexture), so this RT
            // stays MSAA-free even though the scene renders multisampled. UseAllGlobalTextures lets the
            // control shader sample _CameraDepthTexture (kept available by ConfigureInput(Depth)).
            builder.UseAllGlobalTextures(true);
            // Required for the per-draw SetGlobalVector below.
            builder.AllowGlobalStateModification(true);

            // Publish for OutlinePass (which already does UseAllGlobalTextures(true)). Keep the pass alive so
            // the clear + global binding happen even with zero outlined objects — then the kernel reads pure
            // "not outlined" everywhere and draws nothing, instead of sampling a stale or unbound texture.
            builder.SetGlobalTextureAfterPass(controlRT, ControlTexId);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc<PassData>(static (PassData d, RasterGraphContext ctx) =>
            {
                var items = d.items;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    ctx.cmd.SetGlobalVector(ControlValuesId, item.values);

                    // One draw per submesh: DrawRenderer without an index only emits submesh 0, which would
                    // punch holes in the mask on multi-material meshes.
                    for (int s = 0; s < item.subMeshCount; s++)
                        ctx.cmd.DrawRenderer(item.renderer, d.material, s, 0);
                }
            });
        }
    }
}
