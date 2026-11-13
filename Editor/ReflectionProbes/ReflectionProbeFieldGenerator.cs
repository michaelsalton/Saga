using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Saga.Rendering.Editor
{
    // places baked reflection probes by greedy set cover: each probe claims every candidate it can serve under the parallax error budget
    public static class ReflectionProbeFieldGenerator
    {
        const string ProbeNamePrefix = "Generated Reflection Probe";
        const string ProgressTitle = "Reflection Probe Field";

        // matches a new probe's far clip, so anything further reads as sky in the capture too
        const float SkyDistance = 1000f;
        const float SurfaceOffset = 0.01f;
        const float OcclusionTolerance = 0.05f;
        const float FloorNormalMinimum = 0.7f;

        // a capture pressed against a wall is mostly that wall, and can't serve anywhere else
        const float WallClearance = 0.5f;

        // a few rays leak out through windows and doors in any real room
        const float EnclosedSkyFraction = 0.05f;

        // box projection treats the box as the walls, so interior padding has to stay small
        const float EnclosedBlendDistance = 0.5f;
        const float GoldenAngle = 2.39996323f;

        enum SampleKind : byte { Sky, Floor, Wall }

        struct Sample
        {
            public Vector3 point;
            public Vector3 normal;
            public SampleKind kind;
        }

        class Candidate
        {
            public Vector3 position;
            public float floorHeight;
            public float ceilingHeight;
            public Sample[] samples;
            public bool enclosed;
            public Vector3 wallMin;
            public Vector3 wallMax;
        }

        public static int Generate(SagaReflectionProbeField field)
        {
            Scene geometryScene = EditorSceneManager.NewPreviewScene();
            try
            {
                Bounds geometryBounds;
                int colliderCount = BuildCollisionCopy(field, geometryScene, out geometryBounds);
                if (colliderCount == 0)
                {
                    Debug.LogWarning("Reflection Probe Field: no Reflection Probe Static meshes inside the volume.", field);
                    return 0;
                }

                PhysicsScene physics = geometryScene.GetPhysicsScene();
                Vector3[] directions = BuildDirections(field.RayCount);

                List<Candidate> candidates = ScatterCandidates(field, physics, geometryBounds.max.y + 1f);
                if (candidates.Count == 0)
                {
                    Debug.LogWarning("Reflection Probe Field: found no floor to stand on. Check that the volume contains Reflection Probe Static ground.", field);
                    return 0;
                }

                if (!SurveyCandidates(physics, candidates, directions)) return 0;

                float[,] errors = MeasureErrors(field, physics, candidates, directions);
                if (errors == null) return 0;

                List<int> probeIndices = SelectProbes(errors, candidates.Count);
                WriteProbes(field, candidates, errors, probeIndices);

                // URP drops anything past this many visible probes
                if (probeIndices.Count > 64) Debug.LogWarning($"Reflection Probe Field: placed {probeIndices.Count} probes, and URP renders at most 64 at once. Raise Max Error if they can all be on screen together.", field);
                return probeIndices.Count;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.ClosePreviewScene(geometryScene);
            }
        }

        // copies exactly what a baked probe will render, in a private physics scene, so gameplay colliders and meshes without colliders can't skew the survey
        static int BuildCollisionCopy(SagaReflectionProbeField field, Scene geometryScene, out Bounds geometryBounds)
        {
            Bounds volume = field.Volume;
            geometryBounds = new Bounds();
            int colliderCount = 0;

            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!IsProbeGeometry(renderer, field.gameObject.scene)) continue;
                if (!renderer.bounds.Intersects(volume)) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                GameObject copy = new GameObject(renderer.name) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(copy, geometryScene);
                copy.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                copy.transform.localScale = renderer.transform.lossyScale;
                copy.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;

                if (colliderCount == 0) geometryBounds = renderer.bounds;
                else geometryBounds.Encapsulate(renderer.bounds);
                colliderCount++;
            }

            Physics.SyncTransforms();
            return colliderCount;
        }

        // the renderers probes are placed against; fitting and generating share it so they agree on what the scene is
        public static bool IsProbeGeometry(MeshRenderer renderer, Scene scene)
        {
            if (!renderer.enabled || renderer.gameObject.scene != scene) return false;
            if (!GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.ReflectionProbeStatic)) return false;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null;
        }

        public static bool TryGetSceneBounds(Scene scene, out Bounds sceneBounds)
        {
            sceneBounds = new Bounds();
            bool found = false;
            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!IsProbeGeometry(renderer, scene)) continue;
                // a default Bounds sits at the origin, so encapsulating into it would drag the box to (0,0,0)
                if (found) sceneBounds.Encapsulate(renderer.bounds);
                else sceneBounds = renderer.bounds;
                found = true;
            }
            return found;
        }

        // Fibonacci sphere: a golden-angle spiral spreads directions evenly with no clumping at the poles
        static Vector3[] BuildDirections(int count)
        {
            Vector3[] directions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float height = 1f - (i + 0.5f) * 2f / count;
                float radius = Mathf.Sqrt(1f - height * height);
                float angle = i * GoldenAngle;
                directions[i] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            }
            return directions;
        }

        static List<Candidate> ScatterCandidates(SagaReflectionProbeField field, PhysicsScene physics, float skyHeight)
        {
            Bounds volume = field.Volume;
            float spacing = field.CandidateSpacing;
            float lowestFloor = float.PositiveInfinity;
            List<Candidate> candidates = new List<Candidate>();
            Collider[] overlaps = new Collider[1];

            for (float columnX = volume.min.x + spacing * 0.5f; columnX < volume.max.x; columnX += spacing)
            {
                for (float columnZ = volume.min.z + spacing * 0.5f; columnZ < volume.max.z; columnZ += spacing)
                {
                    Vector3 cursor = new Vector3(columnX, skyHeight, columnZ);

                    // one column can hold several floors, so keep descending past each hit
                    while (cursor.y > volume.min.y && physics.Raycast(cursor, Vector3.down, out RaycastHit hit, cursor.y - volume.min.y))
                    {
                        cursor.y = hit.point.y - SurfaceOffset;
                        if (hit.normal.y < FloorNormalMinimum) continue;

                        Vector3 position = hit.point + Vector3.up * field.CaptureHeight;
                        if (physics.Raycast(hit.point + Vector3.up * SurfaceOffset, Vector3.up, field.CaptureHeight)) continue;
                        if (physics.OverlapSphere(position, WallClearance, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore) > 0) continue;
                        if (IsInsideSolid(physics, position, skyHeight)) continue;

                        float ceilingHeight = volume.max.y;
                        if (physics.Raycast(position, Vector3.up, out RaycastHit ceiling, SkyDistance)) ceilingHeight = Mathf.Min(ceiling.point.y, volume.max.y);
                        candidates.Add(new Candidate { position = position, floorHeight = hit.point.y, ceilingHeight = ceilingHeight });
                        lowestFloor = Mathf.Min(lowestFloor, hit.point.y);
                    }
                }
            }

            // measured from real ground after the scan, so the volume's bottom can sit anywhere below it
            float walkableTop = lowestFloor + field.WalkableHeight;
            candidates.RemoveAll(candidate => candidate.floorHeight > walkableTop);
            return candidates;
        }

        // mesh colliders only report front faces, so inside a closed solid a descending ray crosses more faces than an ascending one
        static bool IsInsideSolid(PhysicsScene physics, Vector3 position, float skyHeight)
        {
            Vector3 skyPoint = new Vector3(position.x, skyHeight, position.z);
            return CountCrossings(physics, skyPoint, Vector3.down, position.y) > CountCrossings(physics, position, Vector3.up, skyHeight);
        }

        static int CountCrossings(PhysicsScene physics, Vector3 origin, Vector3 direction, float endHeight)
        {
            int crossings = 0;
            Vector3 cursor = origin;
            float remaining = Mathf.Abs(endHeight - origin.y);

            // RaycastAll reports one hit per collider, which undercounts a building that is a single mesh
            while (remaining > 0f && physics.Raycast(cursor, direction, out RaycastHit hit, remaining))
            {
                crossings++;
                cursor = hit.point + direction * SurfaceOffset;
                remaining -= hit.distance + SurfaceOffset;
            }
            return crossings;
        }

        static bool SurveyCandidates(PhysicsScene physics, List<Candidate> candidates, Vector3[] directions)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i % 16 == 0 && EditorUtility.DisplayCancelableProgressBar(ProgressTitle, "Surveying candidates", (float)i / candidates.Count)) return false;

                Candidate candidate = candidates[i];
                candidate.samples = new Sample[directions.Length];
                int skyCount = 0;

                for (int j = 0; j < directions.Length; j++)
                {
                    if (physics.Raycast(candidate.position, directions[j], out RaycastHit hit, SkyDistance))
                    {
                        SampleKind kind = hit.normal.y >= FloorNormalMinimum ? SampleKind.Floor : SampleKind.Wall;
                        candidate.samples[j] = new Sample { point = hit.point, normal = hit.normal, kind = kind };
                    }
                    else
                    {
                        candidate.samples[j] = new Sample { kind = SampleKind.Sky };
                        skyCount++;
                    }
                }
                candidate.enclosed = (float)skyCount / directions.Length < EnclosedSkyFraction;
                candidate.wallMin = new Vector3(WallAlong(physics, candidate.position, Vector3.left).x, 0f, WallAlong(physics, candidate.position, Vector3.back).z);
                candidate.wallMax = new Vector3(WallAlong(physics, candidate.position, Vector3.right).x, 0f, WallAlong(physics, candidate.position, Vector3.forward).z);
            }
            return true;
        }

        // a miss returns a point far enough away that it never snaps a box face
        static Vector3 WallAlong(PhysicsScene physics, Vector3 position, Vector3 axis)
        {
            return physics.Raycast(position, axis, out RaycastHit hit, SkyDistance) ? hit.point : position + axis * SkyDistance;
        }

        // errors[served, probe]; infinity means that probe can't serve that candidate
        static float[,] MeasureErrors(SagaReflectionProbeField field, PhysicsScene physics, List<Candidate> candidates, Vector3[] directions)
        {
            int count = candidates.Count;
            float[,] errors = new float[count, count];
            float radiusSquared = field.CoverageRadius * field.CoverageRadius;

            for (int i = 0; i < count; i++)
            {
                if (i % 4 == 0 && EditorUtility.DisplayCancelableProgressBar(ProgressTitle, "Measuring parallax error", (float)i / count)) return null;

                for (int j = 0; j < count; j++)
                {
                    if (i == j) errors[i, j] = 0f;
                    else if ((candidates[i].position - candidates[j].position).sqrMagnitude > radiusSquared) errors[i, j] = float.PositiveInfinity;
                    else errors[i, j] = CoverageError(physics, candidates[i], candidates[j], directions, field.MaxError);
                }
            }
            return errors;
        }

        static float CoverageError(PhysicsScene physics, Candidate served, Candidate probe, Vector3[] directions, float maxError)
        {
            float errorBudget = maxError * directions.Length;
            float errorSum = 0f;

            for (int i = 0; i < directions.Length; i++)
            {
                Sample sample = served.samples[i];
                if (sample.kind == SampleKind.Sky)
                {
                    // sky has no parallax, so it only fails if the probe's view of it is blocked
                    if (probe.samples[i].kind != SampleKind.Sky) errorSum += 1f;
                }
                // sliding along a flat floor sees the same floor, so its parallax barely shows
                else if (sample.kind == SampleKind.Wall)
                {
                    errorSum += SurfaceError(physics, sample, probe, directions[i]);
                }
                if (errorSum > errorBudget) return float.PositiveInfinity;
            }
            return errorSum / directions.Length;
        }

        static float SurfaceError(PhysicsScene physics, Sample sample, Candidate probe, Vector3 direction)
        {
            Vector3 toHit = sample.point - probe.position;
            float hitDistance = toHit.magnitude;

            // behind the surface, where a one-sided wall wouldn't block the visibility ray below
            if (Vector3.Dot(toHit, sample.normal) >= 0f) return 1f;

            // the probe captured some other surface here, and no projection can recover this one
            if (physics.Raycast(probe.position, toHit / hitDistance, hitDistance - OcclusionTolerance)) return 1f;

            // box projection cancels the angular error on walls the box matches
            if (probe.enclosed) return 0f;

            // 0 when both points see the surface along the same direction
            return Mathf.Clamp01(Vector3.Angle(direction, toHit) / 90f);
        }

        static List<int> SelectProbes(float[,] errors, int count)
        {
            bool[] covered = new bool[count];
            int remaining = count;
            List<int> probeIndices = new List<int>();

            // every candidate can serve itself, so each pass covers at least one
            while (remaining > 0)
            {
                int bestIndex = -1;
                int bestGain = 0;
                for (int j = 0; j < count; j++)
                {
                    int gain = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (!covered[i] && !float.IsPositiveInfinity(errors[i, j])) gain++;
                    }
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestIndex = j;
                    }
                }

                probeIndices.Add(bestIndex);
                for (int i = 0; i < count; i++)
                {
                    if (!covered[i] && !float.IsPositiveInfinity(errors[i, bestIndex]))
                    {
                        covered[i] = true;
                        remaining--;
                    }
                }
            }
            return probeIndices;
        }

        static void WriteProbes(SagaReflectionProbeField field, List<Candidate> candidates, float[,] errors, List<int> probeIndices)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Reflection Probes");
            RemoveGeneratedProbes(field);

            List<int>[] assigned = AssignToNearestError(errors, candidates.Count, probeIndices);
            Bounds[] boxes = new Bounds[probeIndices.Count];
            for (int i = 0; i < probeIndices.Count; i++) boxes[i] = FitBox(field, candidates, probeIndices[i], assigned[i]);

            // biggest first, so a room's small box gets a higher importance than the street box overlapping it
            int[] order = new int[probeIndices.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (first, second) => Volume(boxes[second]).CompareTo(Volume(boxes[first])));

            for (int rank = 0; rank < order.Length; rank++)
            {
                int index = order[rank];
                Candidate candidate = candidates[probeIndices[index]];

                GameObject probeObject = new GameObject($"{ProbeNamePrefix} {rank}");
                Undo.RegisterCreatedObjectUndo(probeObject, "Generate Reflection Probes");
                probeObject.transform.SetParent(field.transform, false);
                probeObject.transform.position = candidate.position;

                ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = field.Resolution;
                probe.hdr = true;
                probe.boxProjection = candidate.enclosed;
                probe.blendDistance = candidate.enclosed ? EnclosedBlendDistance : field.BlendDistance;
                probe.importance = rank + 1;
                probe.size = boxes[index].size;
                probe.center = boxes[index].center - candidate.position;
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        // only touches children this generator named, so hand-placed probes under the field survive a rerun
        static void RemoveGeneratedProbes(SagaReflectionProbeField field)
        {
            for (int i = field.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = field.transform.GetChild(i);
                if (child.name.StartsWith(ProbeNamePrefix) && child.GetComponent<ReflectionProbe>() != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        static List<int>[] AssignToNearestError(float[,] errors, int count, List<int> probeIndices)
        {
            List<int>[] assigned = new List<int>[probeIndices.Count];
            for (int i = 0; i < assigned.Length; i++) assigned[i] = new List<int>();

            for (int i = 0; i < count; i++)
            {
                int bestProbe = 0;
                float bestError = float.PositiveInfinity;
                for (int j = 0; j < probeIndices.Count; j++)
                {
                    float error = errors[i, probeIndices[j]];
                    if (error < bestError)
                    {
                        bestError = error;
                        bestProbe = j;
                    }
                }
                assigned[bestProbe].Add(i);
            }
            return assigned;
        }

        static Bounds FitBox(SagaReflectionProbeField field, List<Candidate> candidates, int probeIndex, List<int> assigned)
        {
            Candidate probe = candidates[probeIndex];
            float halfCell = field.CandidateSpacing * 0.5f;
            Vector3 boxMin = probe.position;
            Vector3 boxMax = probe.position;

            foreach (int index in assigned)
            {
                Candidate candidate = candidates[index];
                boxMin = Vector3.Min(boxMin, new Vector3(candidate.position.x - halfCell, candidate.floorHeight, candidate.position.z - halfCell));
                boxMax = Vector3.Max(boxMax, new Vector3(candidate.position.x + halfCell, candidate.ceilingHeight, candidate.position.z + halfCell));
            }

            if (probe.enclosed)
            {
                float spacing = field.CandidateSpacing;
                boxMin.x = SnapToWall(boxMin.x, Median(candidates, assigned, candidate => candidate.wallMin.x), spacing);
                boxMin.z = SnapToWall(boxMin.z, Median(candidates, assigned, candidate => candidate.wallMin.z), spacing);
                boxMax.x = SnapToWall(boxMax.x, Median(candidates, assigned, candidate => candidate.wallMax.x), spacing);
                boxMax.z = SnapToWall(boxMax.z, Median(candidates, assigned, candidate => candidate.wallMax.z), spacing);
            }

            Bounds box = new Bounds();
            box.SetMinMax(boxMin, boxMax);

            // weight reaches 0 at the box face, so the fade needs room outside the surfaces and neighbouring boxes must overlap by it
            box.Expand((probe.enclosed ? EnclosedBlendDistance : field.BlendDistance) * 2f);
            return box;
        }

        // box projection treats each face as a wall, but a wall far past this cluster is the end of a corridor and belongs to a neighbour
        static float SnapToWall(float face, float wall, float spacing) => Mathf.Abs(wall - face) <= spacing ? wall : face;

        // median, so one ray through a doorway or into a pillar can't move the wall
        static float Median(List<Candidate> candidates, List<int> assigned, Func<Candidate, float> selector)
        {
            List<float> values = new List<float>(assigned.Count);
            foreach (int index in assigned) values.Add(selector(candidates[index]));
            values.Sort();
            return values[values.Count / 2];
        }

        static float Volume(Bounds box) => box.size.x * box.size.y * box.size.z;
    }

    [CustomEditor(typeof(SagaReflectionProbeField))]
    public class SagaReflectionProbeFieldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Fit To Scene Bounds")) FitToSceneBounds((SagaReflectionProbeField)target);
            if (GUILayout.Button("Generate Probes"))
            {
                int probeCount = ReflectionProbeFieldGenerator.Generate((SagaReflectionProbeField)target);
                Debug.Log($"Reflection Probe Field: placed {probeCount} probes. Bake them from the Lighting window.", target);
            }
        }

        void FitToSceneBounds(SagaReflectionProbeField field)
        {
            if (!ReflectionProbeFieldGenerator.TryGetSceneBounds(field.gameObject.scene, out Bounds sceneBounds))
            {
                Debug.LogWarning("Reflection Probe Field: no enabled MeshRenderers marked Reflection Probe Static in this scene.", field);
                return;
            }

            Transform fieldTransform = field.transform;
            Transform[] probes = new Transform[fieldTransform.childCount];
            Vector3[] probePositions = new Vector3[probes.Length];
            for (int i = 0; i < probes.Length; i++)
            {
                probes[i] = fieldTransform.GetChild(i);
                probePositions[i] = probes[i].position;
            }

            Undo.RecordObject(fieldTransform, "Fit Probe Field To Scene");
            Undo.RecordObjects(probes, "Fit Probe Field To Scene");
            fieldTransform.position = sceneBounds.center;
            // the children are already-generated probes; moving the parent would carry them off the spots they were placed for
            for (int i = 0; i < probes.Length; i++) probes[i].position = probePositions[i];

            serializedObject.Update();
            serializedObject.FindProperty("size").vector3Value = sceneBounds.size;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
