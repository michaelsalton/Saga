using UnityEditor;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    [CustomEditor(typeof(SagaTimeOfDay))]
    class SagaTimeOfDayEditor : UnityEditor.Editor
    {
        SerializedProperty profileProperty;
        SerializedProperty sunLightProperty;
        SerializedProperty reflectionProbeProperty;
        SerializedProperty hourProperty;
        SerializedProperty playingProperty;
        SerializedProperty minutesPerDayProperty;
        SerializedProperty playInEditorProperty;
        SerializedProperty dayOfYearProperty;
        SerializedProperty latitudeProperty;
        SerializedProperty northOffsetProperty;

        UnityEditor.Editor profileEditor;
        bool profileExpanded = true;

        void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
            sunLightProperty = serializedObject.FindProperty("sunLight");
            reflectionProbeProperty = serializedObject.FindProperty("reflectionProbe");
            hourProperty = serializedObject.FindProperty("hour");
            playingProperty = serializedObject.FindProperty("playing");
            minutesPerDayProperty = serializedObject.FindProperty("minutesPerDay");
            playInEditorProperty = serializedObject.FindProperty("playInEditor");
            dayOfYearProperty = serializedObject.FindProperty("dayOfYear");
            latitudeProperty = serializedObject.FindProperty("latitude");
            northOffsetProperty = serializedObject.FindProperty("northOffset");
        }

        void OnDisable()
        {
            if (profileEditor != null) DestroyImmediate(profileEditor);
        }

        // the clock moves without any serialized change, so the slider would freeze unless the inspector redraws itself
        public override bool RequiresConstantRepaint()
        {
            var timeOfDay = (SagaTimeOfDay)target;
            return timeOfDay.Playing && (Application.isPlaying || playInEditorProperty.boolValue);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var timeOfDay = (SagaTimeOfDay)target;

            EditorGUILayout.PropertyField(sunLightProperty);
            EditorGUILayout.PropertyField(reflectionProbeProperty);
            DrawProfileField();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clock", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hourProperty, new GUIContent("Solar Hour", hourProperty.tooltip));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(" ");
                EditorGUILayout.LabelField(FormatClock(hourProperty.floatValue), EditorStyles.largeLabel);
                playingProperty.boolValue = GUILayout.Toggle(playingProperty.boolValue, playingProperty.boolValue ? "Pause" : "Play", "Button", GUILayout.Width(60f));
            }

            EditorGUILayout.PropertyField(minutesPerDayProperty);
            EditorGUILayout.PropertyField(playInEditorProperty);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Date and Place", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(dayOfYearProperty);
            EditorGUILayout.PropertyField(latitudeProperty);
            EditorGUILayout.PropertyField(northOffsetProperty);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            string body = timeOfDay.MoonIsLit ? "Moon" : "Sun";
            EditorGUILayout.HelpBox(
                $"{body} lit.  Sun elevation {timeOfDay.SunElevation:0.0}°, azimuth {timeOfDay.SunAzimuth:0.0}° ({CompassPoint(timeOfDay.SunAzimuth)})",
                MessageType.None);

            DrawProfileInline();
        }

        void DrawProfileField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(profileProperty);
                if (profileProperty.objectReferenceValue == null && GUILayout.Button("New", GUILayout.Width(48f)))
                    CreateProfile();
            }
        }

        void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Time Of Day Profile", "TimeOfDayProfile", "asset", "Save the profile asset");
            if (string.IsNullOrEmpty(path)) return;

            var profile = CreateInstance<SagaTimeOfDayProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            profileProperty.objectReferenceValue = profile;
        }

        // the profile's curves and gradients drawn here so the whole panel is one place; edits land on the asset, so they survive leaving Play mode
        void DrawProfileInline()
        {
            var profile = profileProperty.objectReferenceValue;
            if (profile == null) return;

            EditorGUILayout.Space();
            profileExpanded = EditorGUILayout.InspectorTitlebar(profileExpanded, profile);
            if (!profileExpanded) return;

            CreateCachedEditor(profile, null, ref profileEditor);
            using (new EditorGUI.IndentLevelScope())
                profileEditor.OnInspectorGUI();
        }

        static string FormatClock(float hour)
        {
            int totalMinutes = Mathf.FloorToInt(hour * 60f) % (24 * 60);
            return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
        }

        static string CompassPoint(float azimuth)
        {
            string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return points[Mathf.RoundToInt(azimuth / 45f) % 8];
        }
    }
}
