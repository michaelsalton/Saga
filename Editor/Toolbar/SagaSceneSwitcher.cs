using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saga.Rendering.Editor
{
    // toolbar dropdown named after the open scene, listing every scene in Build Settings
    [InitializeOnLoad]
    public static class SagaSceneSwitcher
    {
        const string ElementPath = "Saga/Scenes";

        static SagaSceneSwitcher()
        {
            // the toolbar caches the element Create returned, so the label stays stale until it's told to rebuild
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => MainToolbar.Refresh(ElementPath);
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement Create()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            // a new scene that's never been saved has no name yet
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled";
            var content = new MainToolbarContent($"Scene: {sceneName}", "Open a scene from Build Settings");
            return new MainToolbarDropdown(content, ShowMenu);
        }

        static void ShowMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            string activePath = SceneManager.GetActiveScene().path;
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0) menu.AddDisabledItem(new GUIContent("No scenes in Build Settings"));

            foreach (EditorBuildSettingsScene scene in scenes)
            {
                var label = new GUIContent(Path.GetFileNameWithoutExtension(scene.path));
                // unticked in Build Settings: still openable here, but SceneManager.LoadScene can't reach it in a build
                if (!scene.enabled) label.text += " (not in build)";
                bool isActive = scene.path == activePath;

                // EditorSceneManager.OpenScene throws in play mode; runtime loading goes through SceneManager instead
                if (EditorApplication.isPlayingOrWillChangePlaymode) menu.AddDisabledItem(label, isActive);
                else menu.AddItem(label, isActive, () => Open(scene.path));
            }

            menu.DropDown(buttonRect);
        }

        static void Open(string path)
        {
            // false when the save prompt is cancelled, which should leave you in the current scene
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
