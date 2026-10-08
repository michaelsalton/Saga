using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Saga.Rendering
{
    /// <summary>
    /// Publishes the global cloud-shadow mask uniforms consumed by Lighting/CloudShadows.hlsl.
    /// One per scene. Animates in the scene view without entering Play mode.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Cloud Shadows")]
    [DisallowMultipleComponent]
    public class CloudShadowVolume : MonoBehaviour
    {
        [Header("Shadow")]
        [Tooltip("How dark the deepest cloud shadow gets. 0 is an EXACT off switch -- the shader " +
                 "returns before evaluating any noise. Keep below a real cast shadow's depth.")]
        [Range(0f, 1f)]
        [SerializeField] float strength = 0.4f;

        [Header("Shape")]
        [Tooltip("Metres per noise cell of the base octave -- the cloud wavelength. THE critical " +
                 "value: the camera only sees ~17.8 m, so a realistic 60 m cloud makes the whole " +
                 "screen pulse uniformly instead of showing clouds. ~8 is right here.")]
        [Range(2f, 60f)]
        [SerializeField] float scale = 8f;

        [Tooltip("0 = clear sky, 1 = overcast. Remapped internally onto a threshold in [0.85, 0.15] " +
                 "because the noise has sigma ~= 0.148 about 0.5 -- raw values outside roughly " +
                 "[0.2, 0.8] would be all-clear or all-overcast.")]
        [Range(0f, 1f)]
        [SerializeField] float coverage = 0.5f;

        [Tooltip("Width of the coverage ramp -- cloud EDGE hardness. Small = crisp cutouts.")]
        [Range(0.01f, 0.6f)]
        [SerializeField] float edgeSoftness = 0.15f;

        [Header("Banding")]
        [Tooltip("Stepped banding. N steps give N+1 levels. At the recommended scale each band is " +
                 "~13 internal pixels, far above the ~2 px visibility floor.")]
        [Range(1, 8)]
        [SerializeField] int steps = 4;

        [Tooltip("Gradient across each step, in units of one step. 0 = hard steps.")]
        [Range(0f, 1f)]
        [SerializeField] float stepSoftness = 0f;

        [Header("Wind")]
        [Tooltip("Direction the clouds drift, as a compass angle in world XZ (degrees).")]
        [Range(0f, 360f)]
        [SerializeField] float windAngle = 45f;

        [Tooltip("Metres per second. At 27 internal px/m, 0.5 m/s crosses the 480 px screen in ~36 s.")]
        [Range(0f, 5f)]
        [SerializeField] float windSpeed = 0.5f;

        [Header("Editor")]
        [Tooltip("Animate in the scene view without entering Play mode. Forces a scene repaint each " +
                 "editor tick while the clouds are actually moving.")]
        [SerializeField] bool animateInEditor = true;

        static readonly int StrengthID     = Shader.PropertyToID("_CloudStrength");
        static readonly int ScaleID        = Shader.PropertyToID("_CloudScale");
        static readonly int CoverageID     = Shader.PropertyToID("_CloudCoverage");
        static readonly int SoftnessID     = Shader.PropertyToID("_CloudSoftness");
        static readonly int StepsID        = Shader.PropertyToID("_CloudSteps");
        static readonly int StepSoftnessID = Shader.PropertyToID("_CloudStepSoftness");
        static readonly int OffsetID       = Shader.PropertyToID("_CloudOffset");

        // Integrated in double so a long session cannot lose precision, and so changing wind speed
        // or direction takes effect smoothly instead of jumping the phase (decision 10).
        double offsetX, offsetZ;
        double lastTime;

        void OnEnable()
        {
            lastTime = Now();
            Publish();
#if UNITY_EDITOR
            EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
            // Leave the scene in the exact OFF state rather than with stale uniforms. The shader gates
            // on this float, so zeroing it is a complete shutdown -- no noise is evaluated.
            Shader.SetGlobalFloat(StrengthID, 0f);
        }

        void OnValidate()
        {
            scale = Mathf.Max(0.01f, scale);
            if (isActiveAndEnabled) Publish();
        }

        void Update()
        {
            if (Application.isPlaying) { Integrate(); Publish(); }
        }

        static double Now()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return EditorApplication.timeSinceStartup;
#endif
            return Time.timeAsDouble;
        }

        void Integrate()
        {
            double now = Now();
            double dt  = now - lastTime;
            lastTime = now;

            // A domain reload or a long editor pause can hand us a huge or negative delta.
            if (dt <= 0.0 || dt > 1.0) return;

            double rad = windAngle * Mathf.Deg2Rad;
            offsetX += System.Math.Sin(rad) * windSpeed * dt;
            offsetZ += System.Math.Cos(rad) * windSpeed * dt;
        }

        void Publish()
        {
            Shader.SetGlobalFloat(StrengthID,     strength);
            Shader.SetGlobalFloat(ScaleID,        Mathf.Max(0.01f, scale));
            Shader.SetGlobalFloat(CoverageID,     coverage);
            Shader.SetGlobalFloat(SoftnessID,     edgeSoftness);
            Shader.SetGlobalFloat(StepsID,        steps);
            Shader.SetGlobalFloat(StepSoftnessID, stepSoftness);
            Shader.SetGlobalVector(OffsetID, new Vector4((float)offsetX, (float)offsetZ, 0f, 0f));
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying) return;

            Integrate();
            Publish();

            // [ExecuteAlways] Update() does not tick reliably in edit mode, and the scene view will
            // not redraw on its own when nothing is dirty -- so drive both explicitly. Gated, so an
            // idle or disabled volume costs nothing.
            if (animateInEditor && strength > 0.001f && windSpeed > 0.001f)
                SceneView.RepaintAll();
        }
#endif
    }
}
