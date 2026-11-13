using UnityEditor;
using UnityEngine;

namespace Saga.Rendering.Editor
{
    static class ReflectionProbeTools
    {
        [MenuItem("Saga/Reflection Probes/Generate Field")]
        static void GenerateSelectedField()
        {
            var field = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<SagaReflectionProbeField>()
                : null;

            if (field == null)
            {
                Debug.LogWarning("Reflection Probe Field: select a GameObject with a SagaReflectionProbeField first.");
                return;
            }

            int probeCount = ReflectionProbeFieldGenerator.Generate(field);
            Debug.Log($"Reflection Probe Field: placed {probeCount} probes. Bake them from the Lighting window.", field);
        }
    }
}
