using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    public class StandardShaderGUI : ShaderGUI
    {
        // one bit per header; the list stores open/closed state as a mask
        [Flags]
        enum Expandable : uint
        {
            SurfaceOptions = 1 << 0,
            SurfaceInputs = 1 << 1,
            WorldUV = 1 << 2,
            POM = 1 << 3,
            Emission = 1 << 4,
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
                headers.RegisterHeaderScope(new GUIContent("World Space UVs"), (uint)Expandable.WorldUV, DrawWorldUV);
                headers.RegisterHeaderScope(new GUIContent("Parallax Occlusion Mapping"), (uint)Expandable.POM, DrawPOM);
                headers.RegisterHeaderScope(new GUIContent("Emission"), (uint)Expandable.Emission, DrawEmission);
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
            DrawProperty("_CastShadows");
            DrawProperty("_ReceiveShadows");
        }

        void DrawSurfaceInputs(Material material)
        {
            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("Albedo"), baseMap, FindProperty("_BaseColor", properties));
            materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), FindProperty("_NormalMap", properties), FindProperty("_NormalScale", properties));
            materialEditor.TexturePropertySingleLine(new GUIContent("ORM"), FindProperty("_ORMMap", properties));
            DrawProperty("_Roughness");
            DrawProperty("_Metallic");
            DrawProperty("_OcclusionStrength");

            MaterialProperty worldUV = FindProperty("_WorldUV", properties);
            // triplanar UVs come from world position and Metres Per Repeat, so _BaseMap_ST is unused there
            if (worldUV.floatValue < 0.5f || worldUV.hasMixedValue)
                materialEditor.TextureScaleOffsetProperty(baseMap);
        }

        void DrawWorldUV(Material material)
        {
            DrawProperty("_WorldUV");
            if (!IsEnabled("_WorldUV"))
                return;

            EditorGUI.indentLevel++;
            DrawProperty("_MetresPerTile");
            DrawProperty("_TriplanarSharpness");
            EditorGUI.indentLevel--;
        }

        void DrawPOM(Material material)
        {
            DrawProperty("_POM");
            if (!IsEnabled("_POM"))
                return;

            EditorGUI.indentLevel++;
            materialEditor.TexturePropertySingleLine(new GUIContent("Height Map"), FindProperty("_HeightMap", properties));
            DrawProperty("_POMDepth");
            DrawProperty("_POMSteps");
            DrawProperty("_POMMin");
            DrawProperty("_POMMax");
            EditorGUI.indentLevel--;
        }

        void DrawEmission(Material material)
        {
            DrawProperty("_Emission");
            if (!IsEnabled("_Emission"))
                return;

            EditorGUI.indentLevel++;
            // the HDR variant opens the intensity picker; the plain single-line one would clamp the color to 0-1
            materialEditor.TexturePropertyWithHDRColor(new GUIContent("Emissive Map"), FindProperty("_EmissiveMap", properties), FindProperty("_EmissionColor", properties), false);
            DrawProperty("_EmissiveIntensity");
            // Realtime / Baked / None: whether the glow lights other surfaces through GI
            materialEditor.LightmapEmissionFlagsProperty(MaterialEditor.kMiniTextureFieldLabelIndentLevel, true);
            EditorGUI.indentLevel--;
        }

        public override void ValidateMaterial(Material material)
        {
            MaterialGlobalIlluminationFlags flags = material.globalIlluminationFlags;
            // the lightmapper skips a material's Meta emission entirely while EmissiveIsBlack is set
            if (material.IsKeywordEnabled("_EMISSION"))
                flags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            else
                flags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.globalIlluminationFlags = flags;

            // URP's shadow-map pass skips any material whose ShadowCaster pass is disabled
            material.SetShaderPassEnabled("ShadowCaster", material.GetFloat("_CastShadows") > 0.5f);
        }
    }
}
