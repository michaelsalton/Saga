using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    // the "Saga" dropdown in the editor's main toolbar; tools register with [MenuItem("Saga/...")] and appear here and in the menu bar
    public static class SagaToolbarButton
    {
        const string ElementPath = "Saga/Tools";
        const string MenuRoot = "Saga/";

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement Create()
        {
            var content = new MainToolbarContent("Saga", "Saga tools");
            return new MainToolbarDropdown(content, ShowMenu);
        }

        // pops up the menu bar's Saga submenu, so MenuItem paths are the only registry
        static void ShowMenu(Rect buttonRect) => EditorUtility.DisplayPopupMenu(buttonRect, MenuRoot, null);
    }
}
