using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saga.Rendering.Editor
{
    // copies the Saga template scene to a new path and opens it
    public static class SagaSceneCreator
    {
        // GUID of Saga.unity, so moving the template doesn't break this
        const string TemplateGuid = "4c255d4fb209db74db01509be89299fa";

        [MenuItem("Saga/Create Scene...")]
        static void CreateScene()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(TemplateGuid);
            if (string.IsNullOrEmpty(templatePath))
            {
                Debug.LogError("Saga scene template not found");
                return;
            }

            // false when the save prompt is cancelled
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string activePath = SceneManager.GetActiveScene().path;
            // start the dialog next to the current scene, unless that's untitled or inside the package
            string startFolder = activePath.StartsWith("Assets/")
                ? Path.GetDirectoryName(activePath).Replace('\\', '/')
                : "Assets";

            // empty string means the dialog was cancelled
            string newPath = EditorUtility.SaveFilePanelInProject("Create Saga Scene", "NewScene", "unity", "Name the new scene", startFolder);
            if (string.IsNullOrEmpty(newPath)) return;

            // the dialog already confirmed the overwrite; CopyAsset won't replace an existing file on its own
            if (File.Exists(newPath)) AssetDatabase.DeleteAsset(newPath);

            if (!AssetDatabase.CopyAsset(templatePath, newPath))
            {
                Debug.LogError($"Failed to copy {templatePath} to {newPath}");
                return;
            }

            EditorSceneManager.OpenScene(newPath, OpenSceneMode.Single);
        }
    }
}
