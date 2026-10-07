using UnityEngine;
using UnityEngine.Serialization;

namespace Saga.Rendering
{
    // One place to switch the pixel camera and the posterize independently of each other.
    [DisallowMultipleComponent]
    public class SagaDisplaySettings : MonoBehaviour
    {
        [Header("Cameras")]
        [Tooltip("The gameplay camera. Leave empty to use Camera.main. Quantize runs on this camera only.")]
        [SerializeField] Camera worldCamera;
        [FormerlySerializedAs("pixelCameraController")]
        [FormerlySerializedAs("controller")]
        [SerializeField] PixelCamera pixelCamera;

        public enum PixelResolution { Res320x180, Res384x216, Res480x270, Res640x360, Res960x540 }

        // every entry divides 1920x1080, 2560x1440 or 3840x2160 evenly; on other screens it letterboxes
        static readonly Vector2Int[] ResolutionSizes =
        {
            new(320, 180), new(384, 216), new(480, 270), new(640, 360), new(960, 540),
        };

        [Header("Pixel Camera")]
        [Tooltip("On: render at low internal res and upscale. Off: the world camera renders straight to the screen.")]
        [SerializeField] bool pixelMode = true;
        [Tooltip("Internal render size. Presented at the largest whole multiple that fits the screen, letterboxed.")]
        [SerializeField] PixelResolution resolution = PixelResolution.Res480x270;

        [Header("Color Quantization")]
        [Tooltip("Snap colors to a uniform per-channel grid (posterize). Works with the pixel camera on or off.")]
        [SerializeField] bool quantize = true;
        [Range(2, 64)]
        [Tooltip("Levels per channel. Lower = stronger limited-palette look. Quantized in sRGB so steps look even.")]
        [SerializeField] int quantizeLevels = 20;
        [Tooltip("Ordered Bayer dither before quantizing, to break banding. Does nothing while Quantize is off.")]
        [SerializeField] bool dither = true;
        [Range(0f, 1f)]
        [Tooltip("Dither amount, as a fraction of one quantization step.")]
        [SerializeField] float ditherStrength = 0.6f;

        public static SagaDisplaySettings Active { get; private set; }

        public Camera WorldCamera => worldCamera != null ? worldCamera : Camera.main;
        public bool Quantize => quantize;
        public int QuantizeLevels => Mathf.Clamp(quantizeLevels, 2, 64);
        // dither only offsets values before the rounding step, so without quantize there is nothing for it to break up
        public bool Dither => quantize && dither;
        public float DitherStrength => ditherStrength;

        public Vector2Int Resolution => ResolutionSizes[(int)resolution];

        bool appliedPixelMode;

        void OnEnable()
        {
            Active = this;
            ApplyPixelMode();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            // runs before PixelCamera.LateUpdate sizes the RT, so a slider change lands the same frame
            if (pixelCamera != null) pixelCamera.TargetSize = Resolution;
            // polled instead of OnValidate, which can't safely toggle components
            if (pixelMode != appliedPixelMode) ApplyPixelMode();
        }

        void ApplyPixelMode()
        {
            if (pixelCamera != null)
            {
                pixelCamera.TargetSize = Resolution;
                pixelCamera.enabled = pixelMode;
                // a rig that starts disabled never runs OnDisable, so its camera would stay on without this
                pixelCamera.GetComponent<Camera>().enabled = pixelMode;
            }
            appliedPixelMode = pixelMode;
        }
    }
}
