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
                headersRegistered = true;
            }

            headers.DrawHeaders(materialEditor, (Material)materialEditor.target);

            // the default inspector draws these at the bottom; a custom one loses them unless asked
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
        }

        void DrawSurfaceOptions(Material material)
        {
        }

        void DrawSurfaceInputs(Material material)
        {
            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            MaterialProperty baseColor = FindProperty("_BaseColor", properties);
            materialEditor.TexturePropertySingleLine(new GUIContent("Albedo"), baseMap, baseColor);
            materialEditor.TextureScaleOffsetProperty(baseMap);

            materialEditor.ShaderProperty(FindProperty("_NormalScale", properties), "Normal Scale");
        }
    }
}
