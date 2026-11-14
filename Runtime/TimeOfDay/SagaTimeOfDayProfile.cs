using System;
using UnityEngine;

namespace Saga.Rendering
{
    // the look of a day: what the sun and moon do at each hour. Where and when the day happens lives on SagaTimeOfDay.
    [CreateAssetMenu(menuName = "Saga/Rendering/Time Of Day Profile", fileName = "TimeOfDayProfile")]
    public class SagaTimeOfDayProfile : ScriptableObject
    {
        [Header("Sun (curves keyed by solar hour, 0-24)")]
        [Tooltip("Light intensity by hour. The horizon fade is applied on top, so this doesn't need to reach zero at sunrise and sunset.")]
        public AnimationCurve sunIntensity = new AnimationCurve(
            new Keyframe(5f, 0.3f), new Keyframe(9f, 1f), new Keyframe(12f, 1.2f), new Keyframe(15f, 1f), new Keyframe(19f, 0.3f));

        [Tooltip("Blackbody temperature in Kelvin by hour. Low is orange, ~6500 is neutral, high is blue.")]
        public AnimationCurve sunTemperature = new AnimationCurve(
            new Keyframe(5f, 2200f), new Keyframe(8f, 4500f), new Keyframe(12f, 5800f), new Keyframe(16f, 4500f), new Keyframe(19f, 2200f));

        [Tooltip("Filter multiplied over the temperature color, left to right across 0-24h. Blackbody can't make pinks or purples; this can.")]
        public Gradient sunTint = DefaultSunTint();

        [Tooltip("Shadow darkness by hour. 1 is fully dark.")]
        public AnimationCurve sunShadowStrength = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(24f, 1f));

        [Header("Moon (sits opposite the sun)")]
        [Min(0f)] public float moonIntensity = 0.15f;
        [Range(1500f, 20000f)] public float moonTemperature = 6500f;
        [Tooltip("Real moonlight is warm. Games tint it blue because night vision shifts toward blue.")]
        public Color moonTint = new Color(0.7f, 0.8f, 1f);
        [Range(0f, 1f)] public float moonShadowStrength = 0.6f;

        [Header("Ambient (derived from the sky above and the lit ground below)")]
        [Tooltip("Multiplier on all ambient light by hour. 1 is what the sky and ground physically deliver.")]
        public AnimationCurve ambientIntensity = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(24f, 1f));

        [Tooltip("Color of the ground around the camera. Sun and sky landing on it reflect up onto undersides, eaves and shaded walls.")]
        public Color groundAlbedo = new Color(0.45f, 0.38f, 0.3f);

        [Tooltip("Scale on the light reflected up off the ground. 0 leaves undersides black.")]
        [Range(0f, 2f)] public float bounceStrength = 1f;

        [Header("Sky (gradients run left to right across 0-24h)")]
        [Tooltip("Color straight overhead.")]
        public Gradient skyZenith = NightDayGradient(
            new Color(0.01f, 0.015f, 0.04f), new Color(0.25f, 0.3f, 0.5f), new Color(0.22f, 0.45f, 0.85f), new Color(0.2f, 0.22f, 0.45f));

        [Tooltip("Color of the band along the horizon.")]
        public Gradient skyHorizon = NightDayGradient(
            new Color(0.03f, 0.04f, 0.08f), new Color(0.95f, 0.6f, 0.45f), new Color(0.7f, 0.82f, 0.95f), new Color(0.95f, 0.5f, 0.35f));

        [Tooltip("Color below the horizon line, where the far ground would be.")]
        public Gradient skyGround = NightDayGradient(
            new Color(0.02f, 0.02f, 0.03f), new Color(0.25f, 0.2f, 0.2f), new Color(0.4f, 0.38f, 0.35f), new Color(0.25f, 0.18f, 0.18f));

        [Tooltip("Multiplier on all three sky gradients by hour.")]
        public AnimationCurve skyIntensity = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(24f, 1f));

        [Tooltip("How far the horizon band climbs. Lower keeps it thin, 1 is a straight blend from horizon to zenith.")]
        [Range(0.05f, 1f)] public float horizonFalloff = 0.35f;

        [Tooltip("Angular radius of the sun disc in degrees. The real sun is 0.27, too small to survive a pixelated camera.")]
        [Range(0.1f, 10f)] public float sunDiscRadius = 1.5f;
        [Tooltip("Degrees of soft edge outside the disc radius.")]
        [Range(0f, 5f)] public float sunDiscSoftness = 0.3f;
        [Tooltip("HDR brightness of the disc. Above 1 so it blooms and reads as a light source.")]
        [Min(0f)] public float sunDiscIntensity = 20f;

        [Tooltip("Brightness of the haze glow around the sun.")]
        [Min(0f)] public float sunGlowIntensity = 1f;
        [Tooltip("How tightly the glow hugs the sun. 0 spreads it evenly over the sky, near 1 makes a tight halo.")]
        [Range(0f, 0.99f)] public float sunGlowAnisotropy = 0.75f;

        [Tooltip("Angular radius of the moon disc in degrees.")]
        [Range(0.1f, 10f)] public float moonDiscRadius = 1.2f;
        [Min(0f)] public float moonDiscIntensity = 2f;

        [Header("Fog")]
        [Tooltip("Fog takes the sky's horizon color so distant geometry melts into the horizon band. This scales it; 1 matches the sky exactly.")]
        [Min(0f)] public float fogBrightness = 1f;

        [Tooltip("Fog density by hour. Only used by the Exponential and Exponential Squared fog modes; Linear ignores it.")]
        public AnimationCurve fogDensity = new AnimationCurve(new Keyframe(0f, 0.005f), new Keyframe(24f, 0.005f));

        [Header("Shadow bias (curves keyed by elevation of the lit body, 0-90 degrees)")]
        [Tooltip("Pushes the shadow test toward the light, in shadow-map texels. Higher hides acne at low sun but lifts shadows off their casters.")]
        public AnimationCurve depthBias = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(15f, 0.3f), new Keyframe(45f, 0.1f), new Keyframe(90f, 0.1f));

        [Tooltip("Shrinks casters along their normals, in shadow-map texels. Higher hides acne on slopes but thins the shadows of posts and fences.")]
        public AnimationCurve normalBias = new AnimationCurve(new Keyframe(0f, 1.5f), new Keyframe(15f, 0.8f), new Keyframe(45f, 0.5f), new Keyframe(90f, 0.5f));

        [Header("Transitions")]
        [Tooltip("Degrees of elevation over which a body fades in from the horizon. The light swaps from sun to moon at 0, where both are dark, so the jump is hidden.")]
        [Range(0.5f, 20f)] public float horizonFadeDegrees = 6f;

        [Tooltip("Degrees the light must turn before it's moved again. Rotating every frame makes shadow edges crawl, because the shadow map's texel grid turns with it.")]
        [Range(0f, 2f)] public float stepDegrees = 0.25f;

        // controllers listen so edits to the asset show up without touching the scene
        public static event Action<SagaTimeOfDayProfile> Changed;

        void OnValidate()
        {
            Changed?.Invoke(this);
        }

        // night holds until ~5am and returns after ~7pm; keys sit at the same hours as the sun tint's dawn and dusk
        static Gradient NightDayGradient(Color night, Color dawn, Color noon, Color dusk)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(night, 0f),
                    new GradientColorKey(night, 0.2f),
                    new GradientColorKey(dawn, 0.27f),
                    new GradientColorKey(noon, 0.5f),
                    new GradientColorKey(dusk, 0.73f),
                    new GradientColorKey(night, 0.8f),
                    new GradientColorKey(night, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        static Gradient DefaultSunTint()
        {
            var gradient = new Gradient();
            var dawnPink = new Color(1f, 0.82f, 0.78f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(dawnPink, 0.21f),
                    new GradientColorKey(Color.white, 0.33f),
                    new GradientColorKey(Color.white, 0.67f),
                    new GradientColorKey(dawnPink, 0.79f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
