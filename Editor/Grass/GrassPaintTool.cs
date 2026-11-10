using Unity.Collections;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    // paints a SagaGrassField's density mask in the Scene view; the field rebuilds when a stroke ends
    [EditorTool("Paint Grass", typeof(SagaGrassField))]
    public class GrassPaintTool : EditorTool
    {
        const int DefaultMaskResolution = 1024;

        static float brushRadius = 2f;
        static float brushStrength = 0.5f;

        Vector3 lastStampPosition;
        bool stroking;

        // text instead of a built-in icon, since built-in icon names aren't a stable API and a missing one leaves a blank button
        public override GUIContent toolbarIcon => new GUIContent("Grass", "Paint Grass");

        public override void OnActivated() => Undo.undoRedoPerformed += RebuildAfterUndo;
        public override void OnWillBeDeactivated() => Undo.undoRedoPerformed -= RebuildAfterUndo;

        // undo puts the mask's pixels back but nothing tells the field its grass is stale
        void RebuildAfterUndo()
        {
            if (target is SagaGrassField field) field.Rebuild();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (target is not SagaGrassField field) return;
            DrawSettings(field);

            Texture2D mask = field.DensityMask;
            if (mask == null || !mask.isReadable || mask.format != TextureFormat.R8) return;

            // claims mouse clicks so painting doesn't also select whatever is under the cursor
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Event current = Event.current;
            // raw type, so releasing the button outside the Scene view still ends the stroke
            if (current.rawType == EventType.MouseUp && current.button == 0 && stroking)
            {
                stroking = false;
                EditorUtility.SetDirty(mask);
                AssetDatabase.SaveAssetIfDirty(mask);
                field.Rebuild();
            }

            Ray mouseRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!Physics.Raycast(mouseRay, out RaycastHit hit, Mathf.Infinity, field.GroundLayers)) return;

            bool erasing = current.shift;
            Handles.color = erasing ? new Color(1f, 0.3f, 0.2f) : new Color(0.4f, 0.9f, 0.2f);
            Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
            Handles.DrawWireDisc(hit.point, hit.normal, brushRadius * 0.5f);

            if (current.button == 0 && !current.alt && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag))
            {
                if (current.type == EventType.MouseDown)
                {
                    // one undo step per stroke, recorded before the first pixel changes
                    Undo.RegisterCompleteObjectUndo(mask, "Paint Grass");
                    lastStampPosition = hit.point;
                    stroking = true;
                }
                StampAlong(field, mask, lastStampPosition, hit.point, erasing ? 0f : 1f);
                lastStampPosition = hit.point;
                current.Use();
            }

            // keeps the disc following the cursor between mouse events
            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag) window.Repaint();
        }

        void DrawSettings(SagaGrassField field)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 10f, 240f, 110f), EditorStyles.helpBox);
            GUILayout.Label("Paint Grass  (shift erases)", EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.Slider("Radius", brushRadius, 0.1f, 30f);
            brushStrength = EditorGUILayout.Slider("Strength", brushStrength, 0.01f, 1f);

            Texture2D mask = field.DensityMask;
            if (mask == null)
            {
                if (GUILayout.Button("Create Density Mask")) CreateMask(field);
            }
            else if (!mask.isReadable || mask.format != TextureFormat.R8)
            {
                EditorGUILayout.HelpBox("Mask must be a readable R8 texture. Create one here instead.", MessageType.Warning);
            }
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        static void CreateMask(SagaGrassField field)
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Grass Density Mask", field.name + "_GrassDensity", "asset", "Where to save the density mask.");
            if (string.IsNullOrEmpty(path)) return;

            // R8 is one byte per texel, and its NativeArray can be written in place while painting; no mips since only the CPU reads it
            var mask = new Texture2D(DefaultMaskResolution, DefaultMaskResolution, TextureFormat.R8, false, true)
            {
                name = field.name + "_GrassDensity",
                wrapMode = TextureWrapMode.Clamp,
            };
            // starts empty: grass only grows where painted
            NativeArray<byte> texels = mask.GetPixelData<byte>(0);
            for (int i = 0; i < texels.Length; i++) texels[i] = 0;
            mask.Apply(false, false);

            AssetDatabase.CreateAsset(mask, path);
            Undo.RecordObject(field, "Assign Grass Density Mask");
            field.DensityMask = mask;
            EditorUtility.SetDirty(field);
            field.Rebuild();
        }

        // a drag event can land a metre past the last one, so stamps are spaced along the gap instead of leaving a row of dots
        static void StampAlong(SagaGrassField field, Texture2D mask, Vector3 from, Vector3 to, float targetDensity)
        {
            float stampSpacing = brushRadius * 0.25f;
            int stampCount = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / stampSpacing));
            for (int stamp = 1; stamp <= stampCount; stamp++)
                Stamp(field, mask, Vector3.Lerp(from, to, stamp / (float)stampCount), targetDensity);
            // uploads to the GPU only so the asset preview updates; the grass itself rebuilds on mouse up
            mask.Apply(false, false);
        }

        static void Stamp(SagaGrassField field, Texture2D mask, Vector3 center, float targetDensity)
        {
            field.GetFootprint(out Vector3 footprintCenter, out Vector2 footprintSize);
            Vector3 corner = footprintCenter - new Vector3(footprintSize.x, 0f, footprintSize.y) * 0.5f;
            int maskWidth = mask.width;
            int maskHeight = mask.height;
            float metresPerTexelX = footprintSize.x / maskWidth;
            float metresPerTexelZ = footprintSize.y / maskHeight;

            // only the texels under the brush's bounding square are visited
            int firstColumn = Mathf.Max(0, Mathf.FloorToInt((center.x - brushRadius - corner.x) / metresPerTexelX));
            int lastColumn = Mathf.Min(maskWidth - 1, Mathf.CeilToInt((center.x + brushRadius - corner.x) / metresPerTexelX));
            int firstRow = Mathf.Max(0, Mathf.FloorToInt((center.z - brushRadius - corner.z) / metresPerTexelZ));
            int lastRow = Mathf.Min(maskHeight - 1, Mathf.CeilToInt((center.z + brushRadius - corner.z) / metresPerTexelZ));

            NativeArray<byte> texels = mask.GetPixelData<byte>(0);
            float target = targetDensity * 255f;
            for (int row = firstRow; row <= lastRow; row++)
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                // texel centre in world XZ, matching how SagaGrassField samples the mask
                float texelX = corner.x + (column + 0.5f) * metresPerTexelX;
                float texelZ = corner.z + (row + 0.5f) * metresPerTexelZ;
                float distance = Mathf.Sqrt((texelX - center.x) * (texelX - center.x) + (texelZ - center.z) * (texelZ - center.z));
                if (distance > brushRadius) continue;

                // full strength in the middle easing to nothing at the rim, so overlapping stamps build up soft edges instead of hard circles
                float falloff = Mathf.SmoothStep(1f, 0f, distance / brushRadius);
                int index = row * maskWidth + column;
                float current = texels[index];
                texels[index] = (byte)Mathf.RoundToInt(Mathf.MoveTowards(current, target, brushStrength * falloff * 255f));
            }
        }
    }
}
