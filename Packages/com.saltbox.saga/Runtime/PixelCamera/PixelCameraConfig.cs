using UnityEngine;

namespace Saga.Rendering
{
    /// <summary>
    /// Optional settings asset for the pixel camera. Holds only the fallback internal resolution (used by
    /// <see cref="PixelCameraController"/> when no <see cref="PixelCameraComposite"/> is present to drive
    /// the downsample) and the letterbox color.
    ///
    /// The downsample amount is a live slider on <see cref="PixelCameraComposite"/>; the grid-locking knobs
    /// (snap / loss offset / overscan / invert) live on the <see cref="PixelCameraController"/> component.
    /// </summary>
    [CreateAssetMenu(menuName = "Saga/Rendering/Pixel Camera Config", fileName = "PixelCameraConfig")]
    public class PixelCameraConfig : ScriptableObject
    {
        [Header("Internal resolution (fallback)")]
        [Tooltip("Used only if no PixelCameraComposite is active. 480x270 is a clean 16:9 default.")]
        [Min(1)] public int internalWidth = 480;
        [Tooltip("Used only if no PixelCameraComposite is active. Square pixels are assumed.")]
        [Min(1)] public int internalHeight = 270;

        [Header("Presentation")]
        [Tooltip("Fills the letterbox/pillarbox bars when the display aspect doesn't divide evenly.")]
        public Color letterboxColor = Color.black;
    }
}
