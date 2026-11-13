using UnityEditor;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    // re-rolls heading and uniform scale across the selection so repeated props don't read as copies
    public class RandomizeTransformWindow : EditorWindow
    {
        const string PrefsPrefix = "Saga.RandomizeTransform.";

        bool randomizeRotation = true;
        float minYaw = 0f;
        float maxYaw = 360f;
        bool randomizeScale = true;
        float minScale = 0.85f;
        float maxScale = 1.15f;

        [MenuItem("Saga/Level Design/Randomize Transform...")]
        static void Open() => GetWindow<RandomizeTransformWindow>(true, "Randomize Transform");

        void OnEnable()
        {
            randomizeRotation = EditorPrefs.GetBool(PrefsPrefix + "Rotation", randomizeRotation);
            minYaw = EditorPrefs.GetFloat(PrefsPrefix + "MinYaw", minYaw);
            maxYaw = EditorPrefs.GetFloat(PrefsPrefix + "MaxYaw", maxYaw);
            randomizeScale = EditorPrefs.GetBool(PrefsPrefix + "Scale", randomizeScale);
            minScale = EditorPrefs.GetFloat(PrefsPrefix + "MinScale", minScale);
            maxScale = EditorPrefs.GetFloat(PrefsPrefix + "MaxScale", maxScale);
        }

        void OnDisable()
        {
            EditorPrefs.SetBool(PrefsPrefix + "Rotation", randomizeRotation);
            EditorPrefs.SetFloat(PrefsPrefix + "MinYaw", minYaw);
            EditorPrefs.SetFloat(PrefsPrefix + "MaxYaw", maxYaw);
            EditorPrefs.SetBool(PrefsPrefix + "Scale", randomizeScale);
            EditorPrefs.SetFloat(PrefsPrefix + "MinScale", minScale);
            EditorPrefs.SetFloat(PrefsPrefix + "MaxScale", maxScale);
        }

        // the Apply button's enabled state depends on the selection, which changes outside this window
        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            randomizeRotation = EditorGUILayout.ToggleLeft("Randomize Heading", randomizeRotation);
            using (new EditorGUI.DisabledScope(!randomizeRotation))
            {
                minYaw = EditorGUILayout.FloatField("Min Degrees", minYaw);
                maxYaw = EditorGUILayout.FloatField("Max Degrees", maxYaw);
            }

            EditorGUILayout.Space();
            randomizeScale = EditorGUILayout.ToggleLeft("Randomize Scale", randomizeScale);
            using (new EditorGUI.DisabledScope(!randomizeScale))
            {
                minScale = Mathf.Max(0.01f, EditorGUILayout.FloatField("Min Multiplier", minScale));
                maxScale = Mathf.Max(minScale, EditorGUILayout.FloatField("Max Multiplier", maxScale));
            }

            EditorGUILayout.Space();
            Transform[] selected = Selection.transforms;
            using (new EditorGUI.DisabledScope(selected.Length == 0 || (!randomizeRotation && !randomizeScale)))
            {
                if (GUILayout.Button($"Apply To {selected.Length} Selected")) Apply(selected);
            }
        }

        void Apply(Transform[] selected)
        {
            Undo.RecordObjects(selected, "Randomize Transform");
            foreach (Transform target in selected)
            {
                if (randomizeRotation) target.rotation = RandomHeading(target);
                if (randomizeScale) target.localScale = BaseScale(target) * Random.Range(minScale, maxScale);
            }
        }

        // splits the rotation into tilt and heading, then replaces only the heading, so a surface-aligned object stays aligned
        Quaternion RandomHeading(Transform target)
        {
            // the smallest rotation that takes world up to the object's up; everything else in the rotation is heading
            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, target.up);
            return tilt * Quaternion.Euler(0f, Random.Range(minYaw, maxYaw), 0f);
        }

        // multiplying the current scale would compound on every click and drift; the prefab's own scale is a fixed base
        static Vector3 BaseScale(Transform target)
        {
            Transform source = PrefabUtility.IsAnyPrefabInstanceRoot(target.gameObject)
                ? PrefabUtility.GetCorrespondingObjectFromSource(target)
                : null;
            return source != null ? source.localScale : Vector3.one;
        }
    }
}
