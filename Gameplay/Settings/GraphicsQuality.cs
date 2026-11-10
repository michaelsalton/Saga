using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Gameplay
{
    // tiers 0..3 are Low, Medium, High, Ultra, in the same order as the stepper options in GraphicsSettings.uxml
    public static class GraphicsQuality
    {
        public const int TierCount = 4;
        // above any scene volume, so a level's own exposure can't silently cancel the player's choice
        const float BrightnessVolumePriority = 1000f;

        // how many top mips are never uploaded: 2 = quarter size, 1 = half, 0 = full
        static readonly int[] MipmapLimits = { 2, 1, 0, 0 };
        // samples along a texture's stretched footprint at grazing angles; 0 is off
        static readonly int[] AnisotropicLevels = { 0, 2, 8, 16 };

        // High matches Saga_RPAsset as authored, so the default tier looks the same as before
        static readonly float[] ShadowDistances = { 25f, 40f, 50f, 80f };
        static readonly int[] ShadowCascadeCounts = { 1, 2, 4, 4 };
        static readonly int[] MainLightShadowResolutions = { 256, 512, 512, 1024 };
        static readonly int[] AdditionalLightShadowResolutions = { 256, 256, 512, 1024 };

        static UniversalRenderPipelineAsset runtimeAsset;
        static RenderPipelineAsset originalQualityAsset;
        static RenderPipelineAsset originalDefaultAsset;
        static ScreenSpaceAmbientOcclusion ambientOcclusion;
        static bool ambientOcclusionWasActive;
        static ColorAdjustments colorAdjustments;

        public static void ApplyTextureQuality(int tier)
        {
            tier = Mathf.Clamp(tier, 0, TierCount - 1);
            QualitySettings.globalTextureMipmapLimit = MipmapLimits[tier];

            int anisotropicLevel = AnisotropicLevels[tier];
            QualitySettings.anisotropicFiltering = anisotropicLevel > 0 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
            // forced on with min = max, every texture gets exactly this level whatever its import setting says
            if (anisotropicLevel > 0) Texture.SetGlobalAnisotropicFilteringLimits(anisotropicLevel, anisotropicLevel);
        }

        public static void ApplyShadowQuality(int tier)
        {
            var pipeline = RuntimePipelineAsset();
            if (pipeline == null) return;
            tier = Mathf.Clamp(tier, 0, TierCount - 1);
            pipeline.shadowDistance = ShadowDistances[tier];
            pipeline.shadowCascadeCount = ShadowCascadeCounts[tier];
            pipeline.mainLightShadowmapResolution = MainLightShadowResolutions[tier];
            pipeline.additionalLightsShadowmapResolution = AdditionalLightShadowResolutions[tier];
        }

        public static void ApplyAmbientOcclusion(bool enabled)
        {
            var feature = FindAmbientOcclusion();
            if (feature != null) feature.SetActive(enabled);
        }

        public static void ApplyBrightness(float exposure)
        {
            if (colorAdjustments == null) CreateBrightnessVolume();
            colorAdjustments.postExposure.value = exposure;
        }

        static UniversalRenderPipelineAsset RuntimePipelineAsset()
        {
            if (runtimeAsset != null) return runtimeAsset;
            // the active asset is a file in the Saga package; setting its properties in Play mode would rewrite it on disk
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset source) return null;

            runtimeAsset = Object.Instantiate(source);
            originalQualityAsset = QualitySettings.renderPipeline;
            originalDefaultAsset = GraphicsSettings.defaultRenderPipeline;
            // a quality level's asset overrides the default one, so the clone takes whichever slot is in use
            if (originalQualityAsset != null) QualitySettings.renderPipeline = runtimeAsset;
            else GraphicsSettings.defaultRenderPipeline = runtimeAsset;
            Application.quitting += RestoreProjectAssets;
            return runtimeAsset;
        }

        static ScreenSpaceAmbientOcclusion FindAmbientOcclusion()
        {
            if (ambientOcclusion != null) return ambientOcclusion;
            var pipeline = RuntimePipelineAsset();
            if (pipeline == null) return null;
            foreach (var rendererData in pipeline.rendererDataList)
            {
                if (rendererData == null) continue;
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature is not ScreenSpaceAmbientOcclusion found) continue;
                    ambientOcclusion = found;
                    // the clone shares renderer data with the original, so this toggles the real asset and is undone on quit
                    ambientOcclusionWasActive = found.isActive;
                    return found;
                }
            }
            return null;
        }

        static void CreateBrightnessVolume()
        {
            // built in memory, so no profile asset in the project ever gets edited
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.postExposure.overrideState = true;

            // Default layer, which the camera's volume mask includes
            var volumeObject = new GameObject("Brightness Volume");
            Object.DontDestroyOnLoad(volumeObject);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = BrightnessVolumePriority;
            volume.sharedProfile = profile;
        }

        // in the Editor this runs on leaving Play mode, so the project is never left pointing at or altered by runtime state
        static void RestoreProjectAssets()
        {
            Application.quitting -= RestoreProjectAssets;
            if (ambientOcclusion != null) ambientOcclusion.SetActive(ambientOcclusionWasActive);
            ambientOcclusion = null;
            if (originalQualityAsset != null) QualitySettings.renderPipeline = originalQualityAsset;
            else GraphicsSettings.defaultRenderPipeline = originalDefaultAsset;
            Object.Destroy(runtimeAsset);
            runtimeAsset = null;
        }
    }
}
