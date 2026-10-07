using UnityEngine;
using UnityEngine.Rendering;

namespace Saga.Rendering
{
    // renders the world camera into a low-res RT for this camera to upscale; disabled = full-res
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class PixelCamera : MonoBehaviour
    {
        [Tooltip("The gameplay camera that renders the scene into the internal RT. Leave empty to use Camera.main.")]
        [SerializeField] Camera worldCamera;

        [Header("Grid-locking")]
        [Tooltip("Snap the camera to the internal-pixel grid each frame so world features stop crawling across " +
                 "pixel boundaries. The rounded-away sub-pixel remainder is offset at the upscale, so motion stays smooth.")]
        [SerializeField] bool enableSnapping = true;

        public static PixelCamera Active { get; private set; }
        public static RTHandle InternalRTHandle { get; private set; }
        // built RT size including overscan; zero while the rig is off so consumers fall back to screen size
        public static int InternalWidth { get; private set; }
        public static int InternalHeight { get; private set; }
        // source-UV mapping for the upscale blit: overscan crop + sub-pixel loss offset + platform V-flip
        public static Vector4 BlitScaleBias { get; private set; } = new Vector4(1f, 1f, 0f, 0f);
        // letterboxed present area in screen pixels; whole-pixel origin so texel edges land on pixel edges
        public static Rect PresentViewport { get; private set; }

        // the loss offset is under one pixel, so one pixel of margin always has content to slide into view
        const int Overscan = 1;

        Camera presentCamera;
        Camera activeWorldCamera;
        RenderTexture internalRT;
        int builtWidth;
        int builtHeight;
        int displayedWidth;
        int displayedHeight;
        // display pixels per rendered pixel; always a whole number so every texel covers a k x k block
        int displayScale = 1;

        Vector3 unsnappedPosition;
        bool snappedThisRender;
        Vector2Int targetSize = new(480, 270);

        // rendered size; driven by SagaDisplaySettings
        public Vector2Int TargetSize
        {
            get => targetSize;
            set => targetSize = Vector2Int.Max(value, new Vector2Int(2, 2));
        }

        void OnEnable()
        {
            presentCamera = GetComponent<Camera>();
            activeWorldCamera = worldCamera != null ? worldCamera : Camera.main;
            if (activeWorldCamera == null || activeWorldCamera == presentCamera)
            {
                Debug.LogError("[PixelCamera] Needs a world camera other than the one this component is on.", this);
                enabled = false;
                return;
            }

            // draws no geometry of its own; the upscale covers every screen pixel, so no clear either
            presentCamera.cullingMask = 0;
            presentCamera.clearFlags = CameraClearFlags.Nothing;
            presentCamera.orthographic = true;
            presentCamera.enabled = true;
            activeWorldCamera.orthographic = true;

            Active = this;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (activeWorldCamera != null) activeWorldCamera.targetTexture = null;
            // with no RT to present, a second base camera would only risk overwriting the world camera's frame
            if (presentCamera != null) presentCamera.enabled = false;
            ReleaseTarget();
        }

        void LateUpdate()
        {
            if (activeWorldCamera == null) return;

            int screenWidth = Mathf.Max(1, Screen.width);
            int screenHeight = Mathf.Max(1, Screen.height);
            // largest multiple that fits both axes; a screen smaller than the target floors to 0, so clamp to 1 and crop
            displayScale = Mathf.Max(1, Mathf.Min(screenWidth / targetSize.x, screenHeight / targetSize.y));
            displayedWidth = targetSize.x;
            displayedHeight = targetSize.y;

            int presentWidth = displayedWidth * displayScale;
            int presentHeight = displayedHeight * displayScale;
            // integer division: a half-pixel origin would put every texel edge mid-pixel and the upscale would blend it
            PresentViewport = new Rect((screenWidth - presentWidth) / 2, (screenHeight - presentHeight) / 2, presentWidth, presentHeight);

            EnsureTarget(displayedWidth + Overscan * 2, displayedHeight + Overscan * 2);
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != activeWorldCamera || builtHeight <= 0) return;

            Vector3 position = activeWorldCamera.transform.position;
            Vector3 snapped = Snap(position, out float lossPixelsX, out float lossPixelsY);

            if (snapped != position)
            {
                unsnappedPosition = position;
                activeWorldCamera.transform.position = snapped;
                snappedThisRender = true;
            }

            BlitScaleBias = BuildScaleBias(lossPixelsX, lossPixelsY);
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != activeWorldCamera || !snappedThisRender) return;
            activeWorldCamera.transform.position = unsnappedPosition;
            snappedThisRender = false;
        }

        // unchanged when no rig is active or snapping is off, so callers work in scenes without one
        public static Vector3 SnapWorldPosition(Vector3 position) => SnapWorldPosition(position, out _, out _);

        public static Vector3 SnapWorldPosition(Vector3 position, out float lossPixelsX, out float lossPixelsY)
        {
            var active = Active;
            if (active == null)
            {
                lossPixelsX = lossPixelsY = 0f;
                return position;
            }
            return active.Snap(position, out lossPixelsX, out lossPixelsY);
        }

        Vector3 Snap(Vector3 position, out float lossPixelsX, out float lossPixelsY)
        {
            lossPixelsX = lossPixelsY = 0f;

            // world height of one RT pixel, recomputed each call so it tracks ortho zoom and RT rebuilds
            float unitsPerPixel = (activeWorldCamera != null && builtHeight > 0) ? (activeWorldCamera.orthographicSize * 2f) / builtHeight : 0f;
            if (!enableSnapping || unitsPerPixel <= 0f) return position;

            // the pixel grid lies in the view plane, so snap along camera right/up; world-axis snapping still crawls
            Vector3 right = activeWorldCamera.transform.right;
            Vector3 up = activeWorldCamera.transform.up;

            float alongRight = Vector3.Dot(position, right);
            float alongUp = Vector3.Dot(position, up);
            float snappedRight = Mathf.Round(alongRight / unitsPerPixel) * unitsPerPixel;
            float snappedUp = Mathf.Round(alongUp / unitsPerPixel) * unitsPerPixel;

            lossPixelsX = (alongRight - snappedRight) / unitsPerPixel;
            lossPixelsY = (alongUp - snappedUp) / unitsPerPixel;

            return position + (snappedRight - alongRight) * right + (snappedUp - alongUp) * up;
        }

        Vector4 BuildScaleBias(float lossPixelsX, float lossPixelsY)
        {
            float rtWidth = builtWidth;
            float rtHeight = builtHeight;
            if (rtWidth <= 0f || rtHeight <= 0f) return new Vector4(1f, 1f, 0f, 0f);

            // whole display pixels only: a fractional shift puts a texel edge inside every screen pixel and blurs motion near 1:1
            float shiftX = Mathf.Round(lossPixelsX * displayScale) / (float)displayScale;
            float shiftY = Mathf.Round(lossPixelsY * displayScale) / (float)displayScale;

            float scaleX = displayedWidth / rtWidth;
            float scaleY = displayedHeight / rtHeight;
            float biasX = (Overscan + shiftX) / rtWidth;
            float biasY = (Overscan + shiftY) / rtHeight;

            // top-origin APIs sample a camera RT upside down relative to the backbuffer
            if (SystemInfo.graphicsUVStartsAtTop)
            {
                biasY += scaleY;
                scaleY = -scaleY;
            }
            return new Vector4(scaleX, scaleY, biasX, biasY);
        }

        void EnsureTarget(int width, int height)
        {
            if (internalRT != null && builtWidth == width && builtHeight == height)
            {
                // something else may have cleared the target, e.g. a domain reload in edit mode
                if (activeWorldCamera.targetTexture != internalRT) activeWorldCamera.targetTexture = internalRT;
                return;
            }

            ReleaseTarget();

            // URP requires a depth format on a camera's output texture; the upscale samples colour only
            internalRT = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "PixelCamera_Internal",
                filterMode = FilterMode.Point,
                // MSAA fights the hard-edge look and the outline pass
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            internalRT.Create();

            InternalRTHandle = RTHandles.Alloc(internalRT);
            activeWorldCamera.targetTexture = internalRT;
            builtWidth = InternalWidth = width;
            builtHeight = InternalHeight = height;
        }

        void ReleaseTarget()
        {
            if (InternalRTHandle != null)
            {
                InternalRTHandle.Release();
                InternalRTHandle = null;
            }
            if (internalRT != null)
            {
                internalRT.Release();
                if (Application.isPlaying) Destroy(internalRT);
                else DestroyImmediate(internalRT);
                internalRT = null;
            }
            builtWidth = builtHeight = 0;
            InternalWidth = InternalHeight = 0;
        }
    }
}
