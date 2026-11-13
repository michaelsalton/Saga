using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saga.Rendering.Editor
{
    // colours HierarchyFolder rows; both paths only touch the Hierarchy window, so the Scene view never sees it
    [InitializeOnLoad]
    static class HierarchyFolderDrawer
    {
        static HierarchyFolderDrawer()
        {
            HierarchyWindow.BindViewItem += OnBindViewItem;
            HierarchyWindow.UnbindViewItem += OnUnbindViewItem;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += DrawLegacyRow;
        }

        // the UI Toolkit Hierarchy: rows are built once, then styled when a node is bound to them
        static void OnBindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            // rows are recycled as you scroll, so one that coloured a folder last time may now show an ordinary object
            ClearStyle(item);
            if (item.Handler is not HierarchyGameObjectHandler handler) return;

            HierarchyNode node = item.Node;
            GameObject gameObject = handler.GetGameObject(in node);
            if (gameObject == null || !gameObject.TryGetComponent(out HierarchyFolder folder)) return;

            item.RowContainer.style.backgroundColor = folder.color;
            item.Name.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        static void OnUnbindViewItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item) => ClearStyle(item);

        // StyleKeyword.Null removes the inline value, handing the property back to the window's stylesheet
        static void ClearStyle(HierarchyViewItem item)
        {
            item.RowContainer.style.backgroundColor = StyleKeyword.Null;
            item.Name.style.unityFontStyleAndWeight = StyleKeyword.Null;
        }

        // the old IMGUI Hierarchy redraws every row each repaint, so it paints directly
        static void DrawLegacyRow(EntityId entityId, Rect rowRect)
        {
            var gameObject = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (gameObject == null || !gameObject.TryGetComponent(out HierarchyFolder folder)) return;
            // painting over a selected row would hide the selection highlight
            if (Selection.Contains(entityId)) return;

            EditorGUI.DrawRect(rowRect, folder.color);
            // the fill covers Unity's own label, so the name is drawn again on top
            EditorGUI.LabelField(rowRect, gameObject.name, EditorStyles.boldLabel);
        }
    }
}
