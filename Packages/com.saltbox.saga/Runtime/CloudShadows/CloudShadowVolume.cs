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

        [Header("Godrays")]
        [Tooltip("Master intensity, and now a HARD CEILING: the march returns an in-scattered fraction in " +
                 "[0,1), so this times the light colour is the most the pass can add to any pixel. At " +
                 "light intensity 2, 0.23 caps the add at ~0.3. TUNE THIS AT COVERAGE 0 -- a clear sky is " +
                 "the worst case, because every sample along the ray is lit and the term maxes out " +
                 "everywhere; adding cloud only ever reduces it. 0 is an EXACT off switch: the march " +
                 "returns before stepping, so no noise and no shadow taps are evaluated. This scalar also " +
                 "absorbs the scattering PHASE term, which is a CONSTANT in this scene: the camera and sun " +
                 "are both fixed, so the scattering angle is always 23 degrees.")]
        [Range(0f, 4f)]
        [SerializeField] float godrayIntensity;

        [Tooltip("Mist EXTINCTION per metre, uniform through the slab. Sets how much of the Intensity " +
                 "ceiling the slab actually reaches -- it cannot scale brightness past that ceiling.\n\n" +
                 "ALSO CONTROLS SHAFT EVENNESS, which is the reason this range is small. Optical depth is " +
                 "density x span (span = 2x Mist Thickness at the 30 degree camera pitch), and light " +
                 "reaching the FAR end of the shaft is attenuated by everything in front of it -- so a high " +
                 "value front-loads the shaft near the camera even though the density itself is uniform. At " +
                 "0.15 over a 22 m span the far end contributes only 4 percent of the near end. Keep this " +
                 "low for an even shaft: 0.02 leaves the far end at ~64 percent, 0.015 at ~72 percent.\n\n" +
                 "Scene fog is OFF and unrelated -- do NOT enable Unity fog to make this work; it would " +
                 "tint every surface without adding a single shaft.")]
        [Range(0f, 0.2f)]
        [SerializeField] float godrayDensity = 0.02f;

        [Tooltip("World Y of the slab FLOOR -- the bottom of the volume the shafts live in, and where " +
                 "mist reaches full density. The slab is anchored HERE, in the world, and the depth " +
                 "buffer only clips it. That is what keeps a shaft in a fixed place as the camera moves: " +
                 "anchoring the slab to depth instead made it a shell that bent with the terrain.")]
        [SerializeField] float mistFloor;

        [Tooltip("SHAFT HEIGHT: world metres from the floor up to the slab ceiling. Mist density is UNIFORM " +
                 "through the slab -- no height falloff -- so the shaft is equally dense end to end and " +
                 "reaches the full height set here.\n\n" +
                 "UPPER LIMIT is smear, not height. An unclipped ray crosses thickness / |camFwd.y| metres " +
                 "(2x this at the 30 degree camera pitch) and the cloud field slides 0.47 m per metre of " +
                 "span, fixed by the 23 degree camera-sun separation -- so each pixel averages about 0.95x " +
                 "this value of cloud detail. Past roughly 11 that exceeds one 10.8 m cloud cell and the " +
                 "shafts flatten into a wash.")]
        [Range(1f, 60f)]
        [SerializeField] float mistThickness = 11f;

        [Tooltip("Metres below the ceiling over which density eases to zero, so the shaft tops fade out " +
                 "instead of ending on a hard line. Density stays FLAT everywhere below this band, so the " +
                 "shaft is still uniform over the part you actually see -- this is not a gradient along the " +
                 "whole shaft.\n\n" +
                 "Needed because the ceiling is a hard boundary in 3D: a lit column of air stops dead where " +
                 "it crosses that plane, and a perfectly flat profile projects that as a visible edge " +
                 "across every shaft top. Roughly a third of Mist Thickness is enough to hide it. Set to 0 " +
                 "for the hard lid back.")]
        [Range(0f, 30f)]
        [SerializeField] float mistFadeTop = 4f;

        [Tooltip("How much scene geometry (trees, props) occludes the shafts, via the main light shadow " +
                 "map. 0 = clouds only. Sweeping this to 0 is how you tell cascade banding apart from " +
                 "the cloud field.")]
        [Range(0f, 1f)]
        [SerializeField] float godrayShadowStrength = 1f;

        [Tooltip("March steps across the span. Cost is this many cloud evaluations plus this many shadow " +
                 "taps per pixel, so it is the whole performance story. Scale with Mist Thickness: at the " +
                 "default 5 (a 10 m span) even 8 steps clear Nyquist on the smallest cloud octave.")]
        [Range(4, 64)]
        [SerializeField] int godraySteps = 24;

        [Tooltip("Hard cap on metres of air integrated per ray. The slab bounds the integral now, so " +
                 "this is purely defensive -- it only bites if the camera is pitched near-horizontal, " +
                 "where a ray almost parallel to the slab would cross unbounded air and blow up both " +
                 "brightness and step size. Also the knob for pulling the march inside the nearer shadow " +
                 "cascades if banding shows up.")]
        [Range(2f, 120f)]
        [SerializeField] float godrayMaxSpan = 40f;

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

        // Godrays (Lighting/Godrays.hlsl). Same contract as the cloud uniforms: declared at file scope in
        // the shader, never inside UnityPerMaterial, or the SRP Batcher silently ignores these writes.
        static readonly int GodrayIntensityID = Shader.PropertyToID("_GodrayIntensity");
        static readonly int GodrayDensityID   = Shader.PropertyToID("_GodrayDensity");
        static readonly int MistFloorID       = Shader.PropertyToID("_MistFloor");
        static readonly int MistThicknessID   = Shader.PropertyToID("_MistThickness");
        static readonly int MistFadeTopID     = Shader.PropertyToID("_MistFadeTop");
        static readonly int GodrayShadowID    = Shader.PropertyToID("_GodrayShadowStrength");
        static readonly int GodrayStepsID     = Shader.PropertyToID("_GodraySteps");
        static readonly int GodrayMaxSpanID   = Shader.PropertyToID("_GodrayMaxSpan");

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
            // Leave the scene in the exact OFF state rather than with stale uniforms. Both features gate
            // on these two floats, so zeroing them is a complete shutdown -- no noise, no march.
            Shader.SetGlobalFloat(StrengthID, 0f);
            Shader.SetGlobalFloat(GodrayIntensityID, 0f);
        }

        void OnValidate()
        {
            scale = Mathf.Max(0.01f, scale);
#if UNITY_EDITOR
            WarnIfCeilingBehindCamera();
#endif
            if (isActiveAndEnabled) Publish();
        }

#if UNITY_EDITOR
        /// <summary>
        /// The slab ceiling only exists in FRONT of the view if it sits below every ray origin. Under ortho
        /// those origins span camY +/- orthographicSize * |up.y|, so a ceiling above the lowest of them is
        /// behind the camera for part or all of the frame: tCeil goes negative, the march silently falls
        /// back to the near plane, and Mist Fade Top stops doing anything at all. Easy to hit (any large
        /// Mist Thickness does it) and invisible without this warning, because the shafts still render --
        /// they just get cut by the near plane instead of fading at the ceiling.
        /// </summary>
        void WarnIfCeilingBehindCamera()
        {
            var c = Camera.main;
            if (c == null || !c.orthographic) return;

            float lowestOrigin = c.transform.position.y - c.orthographicSize * Mathf.Abs(c.transform.up.y);
            float ceiling      = mistFloor + mistThickness;
            if (ceiling <= lowestOrigin) return;

            Debug.LogWarning(
                $"[CloudShadowVolume] Mist ceiling is at Y={ceiling:0.##}, but the camera's lowest view ray " +
                $"starts at Y={lowestOrigin:0.##}. The ceiling is behind the view, so Mist Fade Top does " +
                $"nothing and the shaft tops are cut by the near plane instead. Keep " +
                $"Mist Floor + Mist Thickness below {lowestOrigin:0.##} " +
                $"(i.e. Mist Thickness <= {Mathf.Max(0f, lowestOrigin - mistFloor):0.##}).", this);
        }
#endif

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

            Shader.SetGlobalFloat(GodrayIntensityID, godrayIntensity);
            Shader.SetGlobalFloat(GodrayDensityID,   godrayDensity);
            Shader.SetGlobalFloat(MistFloorID,       mistFloor);
            Shader.SetGlobalFloat(MistThicknessID,   Mathf.Max(0.01f, mistThickness));
            Shader.SetGlobalFloat(MistFadeTopID,     Mathf.Max(0f, mistFadeTop));
            Shader.SetGlobalFloat(GodrayShadowID,    godrayShadowStrength);
            Shader.SetGlobalFloat(GodrayStepsID,     godraySteps);
            Shader.SetGlobalFloat(GodrayMaxSpanID,   Mathf.Max(0.01f, godrayMaxSpan));
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
