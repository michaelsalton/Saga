using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Saga.Rendering
{
    // drives the scene's directional light from a solar hour and date; one per scene
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Time Of Day")]
    [DisallowMultipleComponent]
    public class SagaTimeOfDay : MonoBehaviour
    {
        [SerializeField] SagaTimeOfDayProfile profile;

        [Tooltip("Directional light this drives. Its rotation, color, temperature, intensity and shadow strength get overwritten, so tune them on the profile.")]
        [SerializeField] Light sunLight;

        [Tooltip("Probe re-captured whenever the sun steps. Forced to Realtime / Via Scripting, since this component decides when it renders.")]
        [SerializeField] ReflectionProbe reflectionProbe;

        [Tooltip("Local solar time: 12 is when the sun is highest. Ignores time zones and the equation of time.")]
        [Range(0f, 24f)]
        [SerializeField] float hour = 10f;

        [Tooltip("Advance the clock on its own.")]
        [SerializeField] bool playing;

        [Tooltip("Real minutes for one full 24h day.")]
        [Min(0.1f)]
        [SerializeField] float minutesPerDay = 24f;

        [Tooltip("Advance the clock in the scene view without entering Play mode.")]
        [SerializeField] bool playInEditor;

        [Header("Date and place")]
        [Tooltip("1 = Jan 1. Sets how high the sun climbs and where it rises.")]
        [Range(1, 365)]
        [SerializeField] int dayOfYear = 172;

        [Tooltip("Degrees north of the equator. D'Hanis, Texas is about 29.35.")]
        [Range(-90f, 90f)]
        [SerializeField] float latitude = 29.35f;

        [Tooltip("Compass bearing of world +Z, in degrees. 0 means +Z is north and +X is east.")]
        [Range(0f, 360f)]
        [SerializeField] float northOffset;

        public SagaTimeOfDayProfile Profile => profile;
        public float SunElevation { get; private set; }
        public float SunAzimuth { get; private set; }
        public bool MoonIsLit => SunElevation < 0f;

        public float Hour
        {
            get => hour;
            set { hour = Mathf.Repeat(value, 24f); needsApply = true; }
        }

        public bool Playing
        {
            get => playing;
            set => playing = value;
        }

        static readonly int SkyZenithColorID     = Shader.PropertyToID("_SagaSkyZenithColor");
        static readonly int SkyHorizonColorID    = Shader.PropertyToID("_SagaSkyHorizonColor");
        static readonly int SkyGroundColorID     = Shader.PropertyToID("_SagaSkyGroundColor");
        static readonly int SkyHorizonFalloffID  = Shader.PropertyToID("_SagaSkyHorizonFalloff");
        static readonly int SunDirectionID       = Shader.PropertyToID("_SagaSunDirection");
        static readonly int MoonDirectionID      = Shader.PropertyToID("_SagaMoonDirection");
        static readonly int SunDiscColorID       = Shader.PropertyToID("_SagaSunDiscColor");
        static readonly int SunGlowColorID       = Shader.PropertyToID("_SagaSunGlowColor");
        static readonly int MoonDiscColorID      = Shader.PropertyToID("_SagaMoonDiscColor");
        static readonly int SunParamsID          = Shader.PropertyToID("_SagaSunParams");
        static readonly int MoonParamsID         = Shader.PropertyToID("_SagaMoonParams");

        Quaternion appliedRotation;
        bool hasApplied;
        bool needsApply = true;
        double lastTime;

        // a time-sliced render spans several frames; starting another mid-way would restart it, so the newest request waits
        int probeRenderID = -1;
        bool probeRenderPending;

        // the sky as last published to the shader, kept so the ambient pass integrates exactly what's on screen
        Color skyZenithLinear;
        Color skyHorizonLinear;
        Color sunGlowLinear;
        Vector3 towardSun;

        void Reset()
        {
            sunLight = RenderSettings.sun != null ? RenderSettings.sun : GetComponent<Light>();
        }

        void OnEnable()
        {
            lastTime = Now();
            hasApplied = false;
            needsApply = true;
            SagaTimeOfDayProfile.Changed += OnProfileChanged;
#if UNITY_EDITOR
            EditorApplication.update += EditorTick;
#endif
        }

        void OnDisable()
        {
            SagaTimeOfDayProfile.Changed -= OnProfileChanged;
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        // deferred rather than applied here: OnValidate runs mid-deserialization, where touching other objects can warn or be lost
        void OnValidate()
        {
            needsApply = true;
        }

        void OnProfileChanged(SagaTimeOfDayProfile changed)
        {
            if (changed == profile) needsApply = true;
        }

        void Update()
        {
            if (Application.isPlaying) Tick();
        }

        static double Now()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return EditorApplication.timeSinceStartup;
#endif
            return Time.timeAsDouble;
        }

        void Tick()
        {
            double now = Now();
            double deltaTime = now - lastTime;
            lastTime = now;

            bool advancing = playing && (Application.isPlaying || playInEditor);
            // a domain reload or a long editor pause can hand us a huge or negative delta
            if (advancing && deltaTime > 0.0 && deltaTime < 1.0)
                hour = Mathf.Repeat(hour + (float)deltaTime * 24f / (minutesPerDay * 60f), 24f);

            Evaluate();
            UpdateProbe();
        }

        void UpdateProbe()
        {
            if (reflectionProbe == null) return;

            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;

            bool rendering = probeRenderID != -1 && !reflectionProbe.IsFinishedRendering(probeRenderID);
#if UNITY_EDITOR
            // time slicing advances one slice per rendered frame, and the editor only renders frames when something repaints
            if (rendering && !Application.isPlaying) SceneView.RepaintAll();
#endif
            if (!probeRenderPending || rendering) return;

            probeRenderID = reflectionProbe.RenderProbe();
            probeRenderPending = false;
        }

        void Evaluate()
        {
            if (profile == null || sunLight == null) return;

            ComputeSunPosition(hour, dayOfYear, latitude, out float elevation, out float azimuth);
            SunElevation = elevation;
            SunAzimuth = azimuth;

            // the moon is the antipode of the sun, so exactly one of them is above the horizon
            float bodyElevation = MoonIsLit ? -elevation : elevation;
            float bodyAzimuth = MoonIsLit ? azimuth + 180f : azimuth;

            // world yaw = compass bearing minus the bearing of +Z; the extra 180 aims the light away from the body, the way its light travels
            float yaw = bodyAzimuth + 180f - northOffset;
            Quaternion rotation = Quaternion.Euler(bodyElevation, yaw, 0f);

            bool turnedEnough = !hasApplied || Quaternion.Angle(rotation, appliedRotation) >= profile.stepDegrees;
            if (!needsApply && !turnedEnough) return;

            Apply(rotation, bodyElevation);
        }

        void Apply(Quaternion rotation, float bodyElevation)
        {
            // reaches zero at the horizon, so a light that's about to point upward through the ground is already dark
            float horizonFade = Mathf.SmoothStep(0f, 1f, bodyElevation / profile.horizonFadeDegrees);

            sunLight.transform.rotation = rotation;
            sunLight.useColorTemperature = true;

            if (MoonIsLit)
            {
                sunLight.colorTemperature = profile.moonTemperature;
                sunLight.color = profile.moonTint;
                sunLight.intensity = profile.moonIntensity * horizonFade;
                sunLight.shadowStrength = profile.moonShadowStrength;
            }
            else
            {
                sunLight.colorTemperature = profile.sunTemperature.Evaluate(hour);
                sunLight.color = profile.sunTint.Evaluate(hour / 24f);
                sunLight.intensity = Mathf.Max(0f, profile.sunIntensity.Evaluate(hour)) * horizonFade;
                sunLight.shadowStrength = Mathf.Clamp01(profile.sunShadowStrength.Evaluate(hour));
            }

            ApplyShadowBias(bodyElevation);
            // ambient integrates the sky, so the sky's values have to be current first
            ApplySky();
            ApplyAmbient(bodyElevation);
            ApplyFog();
            // the probe captures the sky just published, so it's queued after the globals change
            probeRenderPending = true;

            appliedRotation = rotation;
            hasApplied = true;
            needsApply = false;

#if UNITY_EDITOR
            if (!Application.isPlaying) SceneView.RepaintAll();
#endif
        }

        // keyed on elevation, not hour: acne comes from the angle the light grazes the ground at, whatever time that happens
        void ApplyShadowBias(float bodyElevation)
        {
            // URP reads the Light's own bias only once it's told to stop using the pipeline asset's
            if (sunLight.TryGetComponent(out UniversalAdditionalLightData lightData))
                lightData.usePipelineSettings = false;

            float elevation = Mathf.Clamp(bodyElevation, 0f, 90f);
            sunLight.shadowBias = Mathf.Max(0f, profile.depthBias.Evaluate(elevation));
            sunLight.shadowNormalBias = Mathf.Max(0f, profile.normalBias.Evaluate(elevation));
        }

        // writes the global ambient probe that bakedGI falls back to for renderers with no lightmap and no probe data
        void ApplyAmbient(float bodyElevation)
        {
            Vector3[] directions = AmbientSampleDirections();

            // URP lights a white surface by color * N.L with no 1/pi, so this sum is what an upward face receives from the sky
            Color skyOnGround = Color.black;
            foreach (Vector3 direction in directions)
                if (direction.y > 0f) skyOnGround += SkyRadiance(direction) * (direction.y * AmbientSampleWeight);

            // the same filter, temperature and intensity URP feeds the shader for this light
            Color directColor = sunLight.color.linear * Mathf.CorrelatedColorTemperatureToRGB(sunLight.colorTemperature) * sunLight.intensity;
            // flat ground catches sin(elevation) of the beam: Lambert's cosine law, with the angle measured from the horizon
            float directOnGround = Mathf.Sin(Mathf.Max(0f, bodyElevation) * Mathf.Deg2Rad);
            // the lower hemisphere is the nearby ground, glowing with whatever sun and sky land on it
            Color groundRadiance = profile.groundAlbedo.linear * (directColor * directOnGround + skyOnGround) * profile.bounceStrength;

            float intensity = Mathf.Max(0f, profile.ambientIntensity.Evaluate(hour));
            float weight = AmbientSampleWeight * AmbientCalibration() * intensity;

            var ambient = new SphericalHarmonicsL2();
            foreach (Vector3 direction in directions)
            {
                Color radiance = direction.y >= 0f ? SkyRadiance(direction) : groundRadiance;
                // direction is toward the source: each sample is light arriving from that part of the sky or ground
                ambient.AddDirectionalLight(direction, radiance, weight);
            }

            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = ambient;
        }

        // mirrors Frag in Saga/Sky minus the discs: the sun disc is the direct light already, and a disc a few degrees wide would flicker as it crossed between samples
        Color SkyRadiance(Vector3 direction)
        {
            float zenithBlend = Mathf.Pow(Mathf.Clamp01(direction.y), profile.horizonFalloff);
            Color color = Color.LerpUnclamped(skyHorizonLinear, skyZenithLinear, zenithBlend);
            float sunCos = Vector3.Dot(direction, towardSun);
            return color + sunGlowLinear * ForwardScatter(sunCos, profile.sunGlowAnisotropy);
        }

        // same Henyey-Greenstein-over-its-peak as the sky shader; change both together
        static float ForwardScatter(float cosAngle, float anisotropy)
        {
            float denominator = 1f + anisotropy * anisotropy - 2f * anisotropy * cosAngle;
            return Mathf.Pow(1f - anisotropy, 3f) / Mathf.Pow(Mathf.Max(denominator, 1e-5f), 1.5f);
        }

        // each sample stands for 4pi/N steradians; dividing by pi to match URP's light convention leaves 4/N
        const int AmbientSampleCount = 256;
        const float AmbientSampleWeight = 4f / AmbientSampleCount;

        static Vector3[] ambientSampleDirections;
        static float ambientCalibration;

        static Vector3[] AmbientSampleDirections()
        {
            if (ambientSampleDirections != null) return ambientSampleDirections;

            var directions = new Vector3[AmbientSampleCount];
            // each point turns the golden angle (~137.5 degrees) from the last, an irrational fraction of a turn, so points never line up into rows
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < AmbientSampleCount; i++)
            {
                // equal steps in height cover equal areas of a sphere (Archimedes' hat-box theorem), so the spacing is even
                float height = 1f - (i + 0.5f) * 2f / AmbientSampleCount;
                float ringRadius = Mathf.Sqrt(1f - height * height);
                float angle = goldenAngle * i;
                directions[i] = new Vector3(Mathf.Cos(angle) * ringRadius, height, Mathf.Sin(angle) * ringRadius);
            }
            ambientSampleDirections = directions;
            return directions;
        }

        // a uniform white sky summed through the samples must equal AddAmbientLight(white); the ratio absorbs whatever scale AddDirectionalLight uses internally
        static float AmbientCalibration()
        {
            if (ambientCalibration > 0f) return ambientCalibration;

            var sampled = new SphericalHarmonicsL2();
            foreach (Vector3 direction in AmbientSampleDirections())
                sampled.AddDirectionalLight(direction, Color.white, AmbientSampleWeight);

            var reference = new SphericalHarmonicsL2();
            reference.AddAmbientLight(Color.white);

            ambientCalibration = reference[0, 0] / sampled[0, 0];
            return ambientCalibration;
        }

        // SetGlobalVector rather than SetGlobalColor: these are already linear, and SetGlobalColor would convert them a second time
        void ApplySky()
        {
            float gradientTime = hour / 24f;
            float intensity = Mathf.Max(0f, profile.skyIntensity.Evaluate(hour));
            skyZenithLinear = profile.skyZenith.Evaluate(gradientTime).linear * intensity;
            skyHorizonLinear = profile.skyHorizon.Evaluate(gradientTime).linear * intensity;
            Shader.SetGlobalVector(SkyZenithColorID, skyZenithLinear);
            Shader.SetGlobalVector(SkyHorizonColorID, skyHorizonLinear);
            Shader.SetGlobalVector(SkyGroundColorID, profile.skyGround.Evaluate(gradientTime).linear * intensity);
            Shader.SetGlobalFloat(SkyHorizonFalloffID, profile.horizonFalloff);

            // pitching up by the elevation at the sun's bearing points from the camera toward the sun, below the horizon included
            towardSun = Quaternion.Euler(-SunElevation, SunAzimuth - northOffset, 0f) * Vector3.forward;
            Shader.SetGlobalVector(SunDirectionID, towardSun);
            Shader.SetGlobalVector(MoonDirectionID, -towardSun);

            // the sun's own color, read from the profile because the Light holds the moon's values at night
            Color sunColor = profile.sunTint.Evaluate(gradientTime).linear * Mathf.CorrelatedColorTemperatureToRGB(profile.sunTemperature.Evaluate(hour));
            Color moonColor = profile.moonTint.linear * Mathf.CorrelatedColorTemperatureToRGB(profile.moonTemperature);

            // the glow is haze the sun lights, so it outlasts sunset and dies out over the fade band below the horizon
            float twilight = Mathf.SmoothStep(0f, 1f, (SunElevation + profile.horizonFadeDegrees) / profile.horizonFadeDegrees);

            Shader.SetGlobalVector(SunDiscColorID, sunColor * profile.sunDiscIntensity);
            sunGlowLinear = sunColor * (profile.sunGlowIntensity * twilight);
            Shader.SetGlobalVector(SunGlowColorID, sunGlowLinear);
            Shader.SetGlobalVector(MoonDiscColorID, moonColor * profile.moonDiscIntensity);
            Shader.SetGlobalVector(SunParamsID, DiscParams(profile.sunDiscRadius, profile.sunDiscSoftness, profile.sunGlowAnisotropy));
            Shader.SetGlobalVector(MoonParamsID, DiscParams(profile.moonDiscRadius, profile.sunDiscSoftness, 0f));
        }

        // MixFog lerps distant pixels toward this color, so matching the sky's horizon hides where geometry ends and sky begins
        void ApplyFog()
        {
            float intensity = Mathf.Max(0f, profile.skyIntensity.Evaluate(hour)) * profile.fogBrightness;
            Color horizonLinear = profile.skyHorizon.Evaluate(hour / 24f).linear * intensity;
            // the scale happens in linear to match the sky; fogColor is authored in sRGB and Unity converts it back on upload
            RenderSettings.fogColor = horizonLinear.gamma;
            RenderSettings.fogDensity = Mathf.Max(0f, profile.fogDensity.Evaluate(hour));
        }

        // cosines go to the shader so it compares against a dot product instead of taking an acos per pixel
        static Vector4 DiscParams(float radiusDegrees, float softnessDegrees, float anisotropy)
        {
            float innerCos = Mathf.Cos(radiusDegrees * Mathf.Deg2Rad);
            float outerCos = Mathf.Cos((radiusDegrees + softnessDegrees) * Mathf.Deg2Rad);
            return new Vector4(innerCos, outerCos, anisotropy, 0f);
        }

        // elevation above the horizon and compass azimuth (0 = north, 90 = east), both in degrees
        public static void ComputeSunPosition(float solarHour, int dayOfYear, float latitudeDegrees, out float elevation, out float azimuth)
        {
            // sun's angle north of the equator; peaks at +23.44 on the June solstice
            float declination = -23.44f * Mathf.Cos(Mathf.Deg2Rad * (360f / 365f) * (dayOfYear + 10));
            // the earth turns 15 degrees an hour, measured from solar noon
            float hourAngle = 15f * (solarHour - 12f);

            float latitudeRad = latitudeDegrees * Mathf.Deg2Rad;
            float declinationRad = declination * Mathf.Deg2Rad;
            float hourAngleRad = hourAngle * Mathf.Deg2Rad;

            float sinElevation = Mathf.Sin(latitudeRad) * Mathf.Sin(declinationRad)
                               + Mathf.Cos(latitudeRad) * Mathf.Cos(declinationRad) * Mathf.Cos(hourAngleRad);
            elevation = Mathf.Asin(Mathf.Clamp(sinElevation, -1f, 1f)) * Mathf.Rad2Deg;

            // Meeus's form measures from south, positive westward; atan2 keeps the morning/afternoon side without a branch
            float fromSouth = Mathf.Atan2(
                Mathf.Sin(hourAngleRad),
                Mathf.Cos(hourAngleRad) * Mathf.Sin(latitudeRad) - Mathf.Tan(declinationRad) * Mathf.Cos(latitudeRad));
            azimuth = Mathf.Repeat(fromSouth * Mathf.Rad2Deg + 180f, 360f);
        }

#if UNITY_EDITOR
        // Update under ExecuteAlways only fires when the scene changes, so the editor clock needs its own pump
        void EditorTick()
        {
            if (Application.isPlaying) return;
            Tick();
        }
#endif
    }
}
