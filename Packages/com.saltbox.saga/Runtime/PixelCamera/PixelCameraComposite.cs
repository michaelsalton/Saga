using UnityEngine;

namespace Saga.Rendering
{
    /// <summary>
    /// Marks the camera that presents the internal pixel buffer to the screen, and carries the live
    /// downsample slider. The <see cref="PixelCameraRenderFeature"/> only runs its upscale blit on a
    /// camera carrying this component, so it never fires on the world camera (which renders into the RT)
    /// or on UI cameras.
    ///
    /// This camera draws nothing of its own — it exists purely to give the upscale pass a backbuffer
    /// target. It must sit at a higher depth than the world camera so it renders after the RT is filled.
    ///
    /// <see cref="PixelScale"/> drives the world camera's internal resolution: it renders at
    /// display / pixelScale and upscales by exactly that integer, so every rendered pixel becomes a
    /// uniform pixelScale x pixelScale block (no mixels).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class PixelCameraComposite : MonoBehaviour
    {
        [Header("Downsample")]
        [Range(2, 10)]
        [Tooltip("How big each rendered pixel is on screen, in display pixels. Higher = chunkier. " +
                 "The world camera renders at display / pixelScale and upscales by exactly this integer.")]
        [SerializeField] int pixelScale = 4;
        [Tooltip("Constrain pixelScale to even values so it only moves in 2x increments.")]
        [SerializeField] bool evenOnly = true;

        [Header("Color & Quantization")]
        [Tooltip("Snap colors to a uniform per-channel grid (posterize) as the final look. " +
                 "Off = clean color, byte-identical to no quantization.")]
        [SerializeField] bool quantize = true;
        [Range(2, 64)]
        [Tooltip("Levels per channel (the uniform grid resolution). 32 = subtle (~32k colors); lower = a " +
                 "stronger limited-palette look. Quantized in perceptual (sRGB) space so steps look even.")]
        [SerializeField] int quantizeLevels = 20;
        [Tooltip("Break banding with a grid-locked 4x4 Bayer dither applied before quantizing.")]
        [SerializeField] bool dither = true;
        [Range(0f, 1f)]
        [Tooltip("Dither amount, as a fraction of one quantization step.")]
        [SerializeField] float ditherStrength = 0.6f;

        /// <summary>The active composite, found by the world-side controller to read the downsample amount.</summary>
        public static PixelCameraComposite Active { get; private set; }

        /// <summary>The downsample / upscale factor actually used: at least 2, rounded up to even when constrained.</summary>
        public int PixelScale
        {
            get
            {
                int s = Mathf.Max(2, pixelScale);
                if (evenOnly && (s & 1) == 1) s++; // round up to the next even step
                return s;
            }
        }

        /// <summary>Whether the final upscale should quantize colors to the uniform grid.</summary>
        public bool Quantize => quantize;

        /// <summary>Levels per channel for the uniform quantization grid.</summary>
        public int QuantizeLevels => Mathf.Clamp(quantizeLevels, 2, 64);

        /// <summary>Whether grid-locked Bayer dithering is applied before quantization.</summary>
        public bool Dither => dither;

        /// <summary>Dither amount as a fraction of one quantization step.</summary>
        public float DitherStrength => ditherStrength;

        void OnEnable()
        {
            Active = this;
            var cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = 0;                        // draws no geometry itself
            cam.clearFlags = CameraClearFlags.Nothing;  // the upscale pass clears to the letterbox color
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void OnValidate()
        {
            // Snap the serialized value to the constraint so the inspector reflects what's actually used.
            if (evenOnly && (pixelScale & 1) == 1) pixelScale++;
            pixelScale = Mathf.Clamp(pixelScale, 2, 16);
        }
    }
}
