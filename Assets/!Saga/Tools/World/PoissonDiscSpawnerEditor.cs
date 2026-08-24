using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Saga.World
{
    [CustomEditor(typeof(PoissonDiscSpawner))]
    public class PoissonDiscSpawnerEditor : Editor
    {
        readonly BoxBoundsHandle box = new BoxBoundsHandle
        {
            axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var s = (PoissonDiscSpawner)target;
            s.EnsurePreview();

            EditorGUILayout.Space();
            string readout = $"radius {s.DerivedRadius:0.00} m  ·  {s.Points.Count} points";
            if (s.Rejected > 0) readout += $"\n{s.Rejected} discarded (nothing on the chosen layer below)";
            if (s.Points.Count > s.previewDrawLimit)
                readout += $"\nPreview showing {s.previewDrawLimit} of {s.Points.Count} discs.";
            EditorGUILayout.HelpBox(readout, MessageType.None);

            using (new EditorGUI.DisabledScope(s.prefabs == null || s.prefabs.Length == 0))
                if (GUILayout.Button("Spawn")) Spawn(s);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear")) Clear(s);
                if (GUILayout.Button("Reroll Seed"))
                {
                    Undo.RecordObject(s, "Reroll Seed");
                    s.seed = Random.Range(int.MinValue, int.MaxValue);
                    s.InvalidatePreview();
                }
            }
        }

        protected virtual void OnSceneGUI()
        {
            var s = (PoissonDiscSpawner)target;
            s.EnsurePreview();

            using (new Handles.DrawingScope(new Color(0.4f, 0.9f, 1f), s.AreaMatrix))
            {
                box.center = Vector3.zero;
                box.size = new Vector3(s.areaSize.x, 0f, s.areaSize.y);

                EditorGUI.BeginChangeCheck();
                box.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(s, "Resize Spawn Area");
                    Undo.RecordObject(s.transform, "Resize Spawn Area");
                    Vector3 worldCentre = s.AreaMatrix.MultiplyPoint3x4(box.center);
                    s.areaSize = new Vector2(Mathf.Abs(box.size.x), Mathf.Abs(box.size.z));
                    s.transform.position = worldCentre;
                    s.InvalidatePreview();
                }
            }

            DrawPoints(s);
        }

        static void DrawPoints(PoissonDiscSpawner s)
        {
            var pts = s.Points;
            var nrm = s.Normals;
            int n = Mathf.Min(pts.Count, s.previewDrawLimit);
            float r = s.DerivedRadius * 0.5f;

            for (int i = 0; i < n; i++)
            {
                Vector3 up = s.alignToNormal ? nrm[i] : Vector3.up;

                if (s.showExclusionDiscs)
                {
                    Handles.color = new Color(1f, 0.85f, 0.3f, 0.10f);
                    Handles.DrawSolidDisc(pts[i], up, r);
                    Handles.color = new Color(1f, 0.85f, 0.3f, 0.45f);
                    Handles.DrawWireDisc(pts[i], up, r);
                }

                Handles.color = Color.yellow;
                Handles.DotHandleCap(0, pts[i], Quaternion.identity,
                                     HandleUtility.GetHandleSize(pts[i]) * 0.03f, EventType.Repaint);
            }
        }

        static void Spawn(PoissonDiscSpawner s)
        {
            s.EnsurePreview();
            Clear(s);

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Spawn Poisson Points");

            if (s.spawnedRoot == null)
            {
                var root = new GameObject("Spawned");
                Undo.RegisterCreatedObjectUndo(root, "Spawn Poisson Points");
                Undo.SetTransformParent(root.transform, s.transform, "Spawn Poisson Points");
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                Undo.RecordObject(s, "Spawn Poisson Points");
                s.spawnedRoot = root.transform;
            }

            var rng = new System.Random(s.seed ^ 0x1a2b3c4d);
            var pts = s.Points;
            var nrm = s.Normals;
            int made = 0;

            for (int i = 0; i < pts.Count; i++)
            {
                var prefab = s.prefabs[rng.Next(s.prefabs.Length)];
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s.spawnedRoot);
                Undo.RegisterCreatedObjectUndo(go, "Spawn Poisson Points");

                Quaternion rot = s.alignToNormal
                    ? Quaternion.FromToRotation(Vector3.up, nrm[i])
                    : Quaternion.identity;
                if (s.randomYaw) rot *= Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                Vector3 scale = prefab.transform.localScale *
                    Mathf.Lerp(s.scaleJitter.x, s.scaleJitter.y, (float)rng.NextDouble());

                if (rng.NextDouble() < s.horizontalFlipChance) scale.x = -scale.x;

                go.transform.position = pts[i];
                go.transform.rotation = rot;
                go.transform.localScale = scale;
                made++;
            }

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[{nameof(PoissonDiscSpawner)}] spawned {made} object(s) at radius " +
                      $"{s.DerivedRadius:0.00} m.", s);
        }

        static void Clear(PoissonDiscSpawner s)
        {
            if (s.spawnedRoot == null) return;
            for (int i = s.spawnedRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(s.spawnedRoot.GetChild(i).gameObject);
        }
    }
}
