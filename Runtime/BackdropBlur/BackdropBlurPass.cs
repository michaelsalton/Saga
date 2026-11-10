using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    public class BackdropBlurPass : ScriptableRenderPass
    {
        const int DownPass = 0;
        const int UpPass = 1;
        const int CompositePass = 2;

        static readonly int SourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
        static readonly int BlurredTextureId = Shader.PropertyToID("_BlurredTexture");
        static readonly int BlurAmountId = Shader.PropertyToID("_BlurAmount");

        Material material;
        int levels;

        public BackdropBlurPass()
        {
            // after tonemapping, so a bright light blurs as a 0..1 value instead of smearing its HDR intensity into a blob
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            // keeps camera color in an intermediate texture past post-processing, since the backbuffer can't be sampled
            requiresIntermediateTexture = true;
        }

        public void Setup(Material blurMaterial, int levelCount)
        {
            material = blurMaterial;
            levels = levelCount;
        }

        class PassData
        {
            public TextureHandle source;
            public TextureHandle blurred;
            public Vector2Int sourceSize;
            public Material material;
            public int shaderPass;
            public float amount;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            float amount = BackdropBlur.Amount;
            if (material == null || amount <= 0f) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc description = renderGraph.GetTextureDesc(source);
            description.clearBuffer = false;
            description.msaaSamples = MSAASamples.None;
            description.sizeMode = TextureSizeMode.Explicit;
            int fullWidth = description.width;
            int fullHeight = description.height;

            var chain = new TextureHandle[levels + 1];
            var sizes = new Vector2Int[levels + 1];
            chain[0] = source;
            sizes[0] = new Vector2Int(fullWidth, fullHeight);
            for (int i = 1; i <= levels; i++)
            {
                sizes[i] = new Vector2Int(Mathf.Max(1, fullWidth >> i), Mathf.Max(1, fullHeight >> i));
                description.width = sizes[i].x;
                description.height = sizes[i].y;
                description.name = "_SagaBlurLevel" + i;
                chain[i] = renderGraph.CreateTexture(description);
            }

            for (int i = 1; i <= levels; i++)
                AddBlit(renderGraph, chain[i - 1], sizes[i - 1], chain[i], DownPass, "Saga Blur Down");
            // overwrites each level's down result on the way back up; the level below has already consumed it
            for (int i = levels - 1; i >= 1; i--)
                AddBlit(renderGraph, chain[i + 1], sizes[i + 1], chain[i], UpPass, "Saga Blur Up");

            description.width = fullWidth;
            description.height = fullHeight;
            description.name = "_SagaBackdropBlurred";
            TextureHandle destination = renderGraph.CreateTexture(description);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Saga Blur Composite", out var passData))
            {
                passData.source = source;
                passData.blurred = chain[1];
                passData.material = material;
                passData.amount = amount;
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(chain[1], AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc<PassData>(ExecuteComposite);
            }

            // later passes and URP's final blit read the blurred copy
            resourceData.cameraColor = destination;
        }

        void AddBlit(RenderGraph renderGraph, TextureHandle source, Vector2Int sourceSize, TextureHandle destination, int shaderPass, string name)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>(name, out var passData);
            passData.source = source;
            passData.sourceSize = sourceSize;
            passData.material = material;
            passData.shaderPass = shaderPass;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc<PassData>(ExecuteBlit);
        }

        static void ExecuteBlit(PassData data, RasterGraphContext context)
        {
            // set at execute time: a material value set while recording would hold only the last level's size by the time any of these draw
            var texelSize = new Vector4(1f / data.sourceSize.x, 1f / data.sourceSize.y, data.sourceSize.x, data.sourceSize.y);
            context.cmd.SetGlobalVector(SourceTexelSizeId, texelSize);
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.shaderPass);
        }

        static void ExecuteComposite(PassData data, RasterGraphContext context)
        {
            context.cmd.SetGlobalTexture(BlurredTextureId, data.blurred);
            context.cmd.SetGlobalFloat(BlurAmountId, data.amount);
            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, CompositePass);
        }
    }
}
