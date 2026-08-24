using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    /// <summary>
    /// URP renderer feature that draws resolution-independent 1px outlines on the pixel camera. It runs on
    /// the WORLD camera (the one rendering the scene into the internal RT), at internal resolution, and
    /// composites the line into the camera color BEFORE the <see cref="PixelCameraRenderFeature"/> present
    /// blit — so the outline goes through the same posterize + pixel grid as the rest of the frame.
    ///
    /// The edge test (<c>Saga/Outline</c>) reconstructs the surface from <c>_CameraDepthTexture</c> alone
    /// (declared via <see cref="ScriptableRenderPass.ConfigureInput"/>), so it needs no DepthNormals prepass
    /// and no shader changes to Saga/Lit. Add this feature to the same URP Renderer as the pixel
    /// camera; it self-guards to the world camera, so it's harmless on the composite/UI cameras.
    /// </summary>
    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Assign the Saga/Outline shader. The material is created and owned by the feature.")]
            public Shader outlineShader;

            [Header("Line color")]
            [Tooltip("Flat line color (RGB) + opacity (A), alpha-blended over the pixel where an edge fires. " +
                     "Set white for white outlines, black for ink, or any tint. Composited before the posterize, " +
                     "so white snaps to the top palette step. Lower the alpha for a translucent line.")]
            public Color outlineColor = Color.white;

            [Header("Thresholds")]
            [Tooltip("World-unit depth gap (slope-aware) that counts as a silhouette / occlusion edge. " +
                     "Lower = more lines. Scale to your scene's world units.")]
            [Range(0f, 1f)] public float depthThreshold = 0.15f;

            [Tooltip("Normal difference (1 - dot, range 0..1) that counts as a crease. Lower = more crease " +
                     "lines; raise toward 1 to effectively disable the crease term.")]
            [Range(0f, 1f)] public float normalThreshold = 0.35f;

            [Header("Lighting")]
            [Tooltip("How far the line follows the shading of the surface it traces. 0 = flat line, the " +
                     "behavior before this existed. 1 = a fully shadowed surface gets a fully tinted line.\n\n" +
                     "Reads the main light's shadow map and the cloud mask at the line's own texel, using " +
                     "the same calls Saga/Lit makes, so the two cannot disagree. REALTIME shadows only — " +
                     "a screen-space pass has no lightmap UVs, so a light set to Baked will shade the " +
                     "surface but not its outline. Requires an orthographic camera; ignored otherwise.")]
            [Range(0f, 1f)] public float lightingStrength = 1f;

            [Tooltip("Multiplies the line color where the surface it sits on is fully in shadow. The " +
                     "default matches the Saga/Lit material's Shadow Tint, so lines and surfaces drift " +
                     "the same way; pick something darker for heavier ink in shade.")]
            public Color shadowTint = new Color(0.5f, 0.55f, 0.65f, 1f);

            [Header("Scheduling")]
            [Tooltip("When the outline draws. BeforeRenderingTransparents is the default, and the reason is " +
                     "GRASS OCCLUSION.\n\n" +
                     "Grass is a Transparent-queue cutout with ZWrite Off, so it lands in neither the depth " +
                     "buffer nor _CameraDepthTexture — the edge kernel cannot see it at all, and a line " +
                     "composited after the transparent queue paints straight over any tuft standing in front " +
                     "of an outlined object. Compositing BEFORE that queue lets grass hide the line with the " +
                     "ZTest LEqual it already does: a tuft nearer than the surface covers that stretch of " +
                     "line, one behind it is depth-rejected and the line survives. No extra pass, no mask.\n\n" +
                     "Trade-off: EVERYTHING in the transparent queue now covers the line, water included. " +
                     "Water is a ZWrite-Off alpha-1 replace, so an outline on a submerged surface is erased. " +
                     "That is usually what you want; if it is not, move to AfterRenderingTransparents (500) " +
                     "or BeforeRenderingPostProcessing (550) to put the line back on top of everything and " +
                     "accept grass drawing behind it.\n\n" +
                     "Do NOT use AfterRendering or AfterRenderingPostProcessing — by then the active target " +
                     "has switched to the backbuffer.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

            [Header("Control mask (per-object)")]
            [Tooltip("The per-object control texture, which is what makes outlines OPT-IN: only objects carrying " +
                     "an enabled OutlineControl component are outlined, and that component tunes their terms " +
                     "(kill creases on detailed meshes, add ID separation, bias thresholds). Turning this OFF is " +
                     "a debug switch — it falls back to uniform outlines on EVERYTHING.")]
            public bool useControlMask = true;

            [Tooltip("Assign the Saga/OutlineControl shader. The override control material is created from it. " +
                     "Required whenever the control mask is on.")]
            public Shader controlShader;
        }

        [SerializeField] Settings settings = new Settings();

        const string ControlKeyword = "_OUTLINE_CONTROL";

        Material material;
        Material controlMaterial;
        OutlinePass pass;
        OutlineControlPass controlPass;

        public override void Create()
        {
            pass = new OutlinePass();
            controlPass = new OutlineControlPass();
            if (settings.outlineShader != null)
                material = CoreUtils.CreateEngineMaterial(settings.outlineShader);
            if (settings.controlShader != null)
                controlMaterial = CoreUtils.CreateEngineMaterial(settings.controlShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null) return;

            bool control = settings.useControlMask && controlMaterial != null;
            CoreUtils.SetKeyword(material, ControlKeyword, control);

            // Opt-in model: without the control texture the kernel falls back to full weights and outlines
            // EVERYTHING. If the mask was asked for but the control shader is unassigned, draw nothing at all
            // rather than flooding the frame with lines on every surface in the scene.
            if (settings.useControlMask && !control) return;

            // Enqueue the control producer FIRST: the global-texture registry is keyed by add-order, so
            // _OutlineControl must be registered before the outline pass (its consumer) is added.
            if (control)
            {
                controlPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                // The control shader samples _CameraDepthTexture for in-shader occlusion; declare it so URP
                // produces/copies the depth texture before this pass runs.
                controlPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                controlPass.Setup(controlMaterial);
                renderer.EnqueuePass(controlPass);
            }

            pass.renderPassEvent = settings.injectionPoint;
            // Depth-only: normals are reconstructed from depth in the shader, which sidesteps the DepthNormals
            // prepass (Saga/Lit has a DepthNormals pass, but nothing writes a usable normals RT). Declared in the feature,
            // per the ConfigureInput contract.
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            pass.Setup(material, settings);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(controlMaterial);
            material = null;
            controlMaterial = null;
            pass = null;
            controlPass = null;
        }
    }
}
