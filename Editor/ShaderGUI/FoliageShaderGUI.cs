using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saga.Rendering.Editor
{
    public class FoliageShaderGUI : ShaderGUI
    {
        // one bit per header; the list stores open/closed state as a mask
        [Flags]
        enum Expandable : uint
        {
            SurfaceOptions = 1 << 0,
            SurfaceInputs = 1 << 1,
            Reflections = 1 << 2,
            AA = 1 << 3,
            Quality = 1 << 4,
            Wind = 1 << 5,
        }

        readonly MaterialHeaderScopeList headers = new MaterialHeaderScopeList(uint.MaxValue);
        bool headersRegistered;
        MaterialEditor materialEditor;
        MaterialProperty[] properties;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // Unity hands over a fresh property array every call, so the draw callbacks read this frame's
            this.materialEditor = materialEditor;
            this.properties = properties;

            if (!headersRegistered)
            {
                headers.RegisterHeaderScope(new GUIContent("Surface Options"), (uint)Expandable.SurfaceOptions, DrawSurfaceOptions);
                headers.RegisterHeaderScope(new GUIContent("Surface Inputs"), (uint)Expandable.SurfaceInputs, DrawSurfaceInputs);
                headers.RegisterHeaderScope(new GUIContent("Wind"), (uint)Expandable.Wind, DrawWind);
                headers.RegisterHeaderScope(new GUIContent("Anti-Aliasing"), (uint)Expandable.AA, DrawAA);
                headers.RegisterHeaderScope(new GUIContent("Reflections"), (uint)Expandable.Reflections, DrawReflections);
                headers.RegisterHeaderScope(new GUIContent("Quality Settings"), (uint)Expandable.Quality, DrawQuality);
                headersRegistered = true;
            }

            headers.DrawHeaders(materialEditor, (Material)materialEditor.target);

            // the default inspector draws these at the bottom; a custom one loses them unless asked
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
        }

        void DrawProperty(string propertyName)
        {
            MaterialProperty property = FindProperty(propertyName, properties);
            materialEditor.ShaderProperty(property, property.displayName);
        }

        bool IsEnabled(string togglePropertyName)
        {
            MaterialProperty toggle = FindProperty(togglePropertyName, properties);
            // a multi-selection that disagrees counts as on, so its settings stay reachable
            return toggle.floatValue > 0.5f || toggle.hasMixedValue;
        }

        void DrawSurfaceOptions(Material material)
        {
            DrawProperty("_AlphaClip");
            if (IsEnabled("_AlphaClip"))
            {
                EditorGUI.indentLevel++;
                DrawProperty("_Cutoff");
                EditorGUI.indentLevel--;
            }
            DrawProperty("_Cull");
            DrawProperty("_CastShadows");
            DrawProperty("_ReceiveShadows");
        }

        void DrawSurfaceInputs(Material material)
        {
            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, FindProperty("_BaseColor", properties));
            materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), FindProperty("_NormalMap", properties), FindProperty("_NormalScale", properties));
            materialEditor.TexturePropertySingleLine(new GUIContent("ORM"), FindProperty("_ORMMap", properties));
            DrawProperty("_Roughness");
            DrawProperty("_OcclusionStrength");
            materialEditor.TextureScaleOffsetProperty(baseMap);
        }

        void DrawWind(Material material)
        {
            DrawProperty("_WindHeightFalloff");
            DrawProperty("_BranchResponse");
            DrawProperty("_FlutterResponse");
        }

        void DrawAA(Material material)
        {
            DrawProperty("_SpecularAA");
        }

        void DrawReflections(Material material)
        {
            DrawProperty("_EnvironmentReflections");
        }

        void DrawQuality(Material material)
        {
            DrawProperty("_MultiScatter");
            DrawProperty("_EnergyBalance");
            DrawProperty("_SpecularOcclusion");
        }

        public override void ValidateMaterial(Material material)
        {
            // no emission, so the lightmapper can skip this material's Meta emission
            material.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            // URP's shadow-map pass skips any material whose ShadowCaster pass is disabled
            material.SetShaderPassEnabled("ShadowCaster", material.GetFloat("_CastShadows") > 0.5f);

            // cutouts draw after solid opaques, so the cheap solid pixels fill depth first and hide clipped ones behind them
            bool alphaClip = material.GetFloat("_AlphaClip") > 0.5f;
            material.renderQueue = (int)(alphaClip ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
        }
    }
}
