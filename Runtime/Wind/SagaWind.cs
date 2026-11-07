using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Saga.Rendering
{
    // publishes the global wind uniforms consumed by ShaderLibrary/Wind.hlsl; one per scene
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Wind")]
    [DisallowMultipleComponent]
    public class SagaWind : MonoBehaviour
    {
        [Tooltip("Direction the wind blows toward, as a compass angle in world XZ (degrees). " +
                 "Same convention as Cloud Shadows, so matching angles make clouds drift with the bend.")]
        [Range(0f, 360f)]
        [SerializeField] float windAngle = 45f;

        [Tooltip("Metres the top of a plant leans downwind in average wind. 0 is still air for the lean.")]
        [Range(0f, 1f)]
        [SerializeField] float strength = 0.15f;

        [Header("Gusts")]
        [Tooltip("How much gusts and lulls vary the wind. 0 = uniform everywhere, 1 = lulls go fully still and gusts double it.")]
        [Range(0f, 1f)]
        [SerializeField] float gustiness = 0.6f;

        [Tooltip("Metres per noise cell of the gust field -- roughly the size of one gust patch.")]
        [Range(1f, 40f)]
        [SerializeField] float gustScale = 8f;

        [Tooltip("Metres per second the gust patches travel downwind.")]
        [Range(0f, 10f)]
        [SerializeField] float gustSpeed = 2f;

        [Header("Sway")]
        [Tooltip("Rocking cycles per second of the whole plant around its lean.")]
        [Range(0f, 3f)]
        [SerializeField] float swayFrequency = 0.4f;

        [Header("Branches")]
        [Tooltip("Metres the outer canopy swings side to side in average wind.")]
        [Range(0f, 0.3f)]
        [SerializeField] float branchAmplitude = 0.03f;

        [Tooltip("Branch swing cycles per second.")]
        [Range(0f, 5f)]
        [SerializeField] float branchFrequency = 1.1f;

        [Header("Leaves")]
        [Tooltip("Metres leaves flutter along their normal in average wind; grows with the square of the gust.")]
        [Range(0f, 0.1f)]
        [SerializeField] float flutterAmplitude = 0.01f;

        [Tooltip("Flutter cycles per second in calm air. Gusts crossfade to a faster flutter.")]
        [Range(0f, 10f)]
        [SerializeField] float flutterFrequency = 3f;

        [Header("Editor")]
        [Tooltip("Animate in the scene view without entering Play mode.")]
        [SerializeField] bool animateInEditor = true;

        // second sway wave runs at sqrt(3) times the first, so the two never line up and the rock never repeats
        const double SecondarySwayRatio = 1.7320508;
        const double GustFlutterRatio = 2.5;

        static readonly int DirectionID        = Shader.PropertyToID("_WindDirection");
        static readonly int StrengthID         = Shader.PropertyToID("_WindStrength");
        static readonly int GustinessID        = Shader.PropertyToID("_WindGustiness");
        static readonly int GustScaleID        = Shader.PropertyToID("_WindGustScale");
        static readonly int GustOffsetID       = Shader.PropertyToID("_WindGustOffset");
        static readonly int PhasesID           = Shader.PropertyToID("_WindPhases");
        static readonly int FlutterPhasesID    = Shader.PropertyToID("_WindFlutterPhases");
        static readonly int BranchAmplitudeID  = Shader.PropertyToID("_WindBranchAmplitude");
        static readonly int FlutterAmplitudeID = Shader.PropertyToID("_WindFlutterAmplitude");

        // one integrated phase per layer: time * frequency snaps when frequency changes, and a shared phase times a non-integer ratio jumps at every wrap
        double swayPhase, secondarySwayPhase, branchPhase, calmFlutterPhase, gustFlutterPhase;
        double gustOffsetX, gustOffsetZ;
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
            // every layer's amplitude at zero leaves each vertex exactly where the mesh put it
            Shader.SetGlobalFloat(StrengthID, 0f);
            Shader.SetGlobalFloat(BranchAmplitudeID, 0f);
            Shader.SetGlobalFloat(FlutterAmplitudeID, 0f);
        }

        void OnValidate()
        {
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

        // phases are in cycles, and the shader's waves repeat every cycle, so dropping whole cycles is invisible
        static double Advance(double phase, double frequency, double deltaTime)
        {
            return (phase + frequency * deltaTime) % 1.0;
        }

        void Integrate()
        {
            double now = Now();
            double deltaTime = now - lastTime;
            lastTime = now;

            // a domain reload or a long editor pause can hand us a huge or negative delta
            if (deltaTime <= 0.0 || deltaTime > 1.0) return;

            swayPhase = Advance(swayPhase, swayFrequency, deltaTime);
            secondarySwayPhase = Advance(secondarySwayPhase, swayFrequency * SecondarySwayRatio, deltaTime);
            branchPhase = Advance(branchPhase, branchFrequency, deltaTime);
            calmFlutterPhase = Advance(calmFlutterPhase, flutterFrequency, deltaTime);
            gustFlutterPhase = Advance(gustFlutterPhase, flutterFrequency * GustFlutterRatio, deltaTime);

            double radians = windAngle * Mathf.Deg2Rad;
            gustOffsetX += System.Math.Sin(radians) * gustSpeed * deltaTime;
            gustOffsetZ += System.Math.Cos(radians) * gustSpeed * deltaTime;
        }

        void Publish()
        {
            float radians = windAngle * Mathf.Deg2Rad;
            Shader.SetGlobalVector(DirectionID, new Vector4(Mathf.Sin(radians), Mathf.Cos(radians), 0f, 0f));
            Shader.SetGlobalFloat(StrengthID, strength);
            Shader.SetGlobalFloat(GustinessID, gustiness);
            Shader.SetGlobalFloat(GustScaleID, gustScale);
            Shader.SetGlobalVector(GustOffsetID, new Vector4((float)gustOffsetX, (float)gustOffsetZ, 0f, 0f));
            Shader.SetGlobalVector(PhasesID, new Vector4((float)swayPhase, (float)secondarySwayPhase, (float)branchPhase, 0f));
            Shader.SetGlobalVector(FlutterPhasesID, new Vector4((float)calmFlutterPhase, (float)gustFlutterPhase, 0f, 0f));
            Shader.SetGlobalFloat(BranchAmplitudeID, branchAmplitude);
            Shader.SetGlobalFloat(FlutterAmplitudeID, flutterAmplitude);
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying) return;

            Integrate();
            Publish();

            // the scene view only redraws when something is dirty, and shader globals don't count
            if (animateInEditor && (strength > 0.0001f || branchAmplitude > 0.0001f || flutterAmplitude > 0.0001f))
                SceneView.RepaintAll();
        }
#endif
    }
}
