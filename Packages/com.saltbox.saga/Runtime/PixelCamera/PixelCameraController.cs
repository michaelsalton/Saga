using UnityEngine;
using UnityEngine.Rendering;

namespace Saga.Rendering
{
    /// <summary>
    /// World-side brain of the pixel camera. Lives on the gameplay camera (Camera.main) and:
    ///   - allocates the internal RenderTexture (Point filter, MSAA off, depth attached) at the
    ///     downsample resolution plus an overscan margin, and points the camera at it, so the whole URP
    ///     pipeline + every later feature pass renders at internal res;
    ///   - frames the upscale: the displayed region is integer-scaled and centered, remainder letterboxed;
    ///   - GRID-LOCKS the camera (snap-then-offset). Just before this camera renders, it snaps the
    ///     transform to the internal-pixel grid along the camera's right/up axes (kills crawl), records
    ///     the rounded-away sub-pixel "loss", then restores the transform right after. The loss is applied
    ///     at the presentation stage instead — the upscale blit's crop window is shifted by it, so motion
    ///     stays smooth. Snap and offset live at different stages so they don't cancel.
    ///
    /// Doing the snap at render time (not in a Cinemachine extension) means it works no matter what drives
    /// the camera — Cinemachine, the debug fly cam, scripted shots — and gameplay never sees the snap.
    ///
    /// The grid-locking knobs live here on the component (right where you A/B them). The optional config
    /// asset only supplies the fallback resolution + letterbox color.
    ///
    /// State is published through static members consumed by <see cref="PixelCameraUpscalePass"/>. Two-camera
    /// rig: the world camera renders into the RT; a camera carrying <see cref="PixelCameraComposite"/> presents
    /// it. This component drives the world camera by reference, so it can live on any object — including the
    /// composite, to keep all the pixel-camera settings in one place.
    /// </summary>
    [DisallowMultipleComponent]
    public class PixelCameraController : MonoBehaviour
    {
        [Tooltip("The gameplay camera that renders the scene into the internal RT. Leave empty to use " +
                 "Camera.main. This need not be the object the component is on — e.g. put the component on " +
                 "the composite and point this at your main camera.")]
        [SerializeField] Camera worldCamera;

        [Tooltip("Optional. Supplies the fallback internal resolution (when no composite is present) and the " +
                 "letterbox color. Grid-locking settings live on this component, below.")]
        [SerializeField] PixelCameraConfig config;

        [Header("Grid-locking")]
        [Tooltip("A/B toggle: snap the camera to the internal-pixel grid each frame so world features stop " +
                 "crawling across pixel boundaries when the camera moves. The rounded-away sub-pixel " +
                 "remainder is offset at the upscale, so motion still reads smooth.")]
        [SerializeField] bool enableSnapping = true;

        /// <summary>The internal RT (includes overscan), bound by the upscale pass each frame.</summary>
        public static RTHandle InternalRTHandle { get; private set; }

        /// <summary>Centered, integer-scaled destination rect of the DISPLAYED region on the display (pixels).</summary>
        public static Rect ViewportRect { get; private set; }

        /// <summary>The integer upscale factor currently in use.</summary>
        public static int Scale { get; private set; }

        /// <summary>Actual built RT size, including overscan (used for UPP and the crop math).</summary>
        public static int InternalWidth { get; private set; }
        public static int InternalHeight { get; private set; }

        /// <summary>Bar color for the non-integer remainder.</summary>
        public static Color LetterboxColor { get; private set; }

        public static PixelCameraController Active { get; private set; }

        /// <summary>
        /// Source-UV mapping for the upscale blit (xy = scale, zw = bias): crops the displayed region out
        /// of the overscanned RT, shifted by the sub-pixel loss, with the platform V-flip folded in.
        /// </summary>
        public static Vector4 BlitScaleBias { get; private set; } = new Vector4(1f, 1f, 0f, 0f);

        // Pixels rendered beyond the displayed region so the sub-pixel offset always has content to slide
        // into view (no exposed edge sliver). The loss is under one pixel, so 1 is enough.
        const int Overscan = 1;

        Camera cam;
        RenderTexture internalRT;
        int builtWidth, builtHeight;       // RT size incl. overscan

        int displayedWidth, displayedHeight;

        Vector3 unsnappedPos;
        bool snappedThisRender;

        void OnEnable()
        {
            cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogError("[PixelCameraController] No world camera assigned and no Camera.main found.", this);
                enabled = false;
                return;
            }
            cam.orthographic = true;
            Active = this;
            LetterboxColor = config != null ? config.letterboxColor : Color.black;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (cam != null) cam.targetTexture = null;
            ReleaseTarget();
        }

        void LateUpdate()
        {
            if (cam == null) return;
            LetterboxColor = config != null ? config.letterboxColor : Color.black;

            int dw = Mathf.Max(1, Screen.width);
            int dh = Mathf.Max(1, Screen.height);
            ResolveTargetSize(dw, dh, out displayedWidth, out displayedHeight, out int scale);

            EnsureTarget(displayedWidth + Overscan * 2, displayedHeight + Overscan * 2);

            // The viewport shows only the displayed region — the overscan margin stays hidden.
            int vw = displayedWidth * scale;
            int vh = displayedHeight * scale;
            Scale = scale;
            ViewportRect = new Rect((dw - vw) * 0.5f, (dh - vh) * 0.5f, vw, vh);
        }

        // Resolves the DISPLAYED internal size and integer upscale factor. A composite's pixelScale slider
        // drives the downsample; otherwise fall back to the config's fixed resolution fit to the display.
        void ResolveTargetSize(int dw, int dh, out int w, out int h, out int scale)
        {
            var composite = PixelCameraComposite.Active;
            if (composite != null)
            {
                scale = composite.PixelScale;
                w = Mathf.Max(1, Mathf.CeilToInt(dw / (float)scale));
                h = Mathf.Max(1, Mathf.CeilToInt(dh / (float)scale));
            }
            else
            {
                w = Mathf.Max(1, config != null ? config.internalWidth : 480);
                h = Mathf.Max(1, config != null ? config.internalHeight : 270);
                scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min((float)dw / w, (float)dh / h)));
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera camera)
        {
            if (camera != cam || builtHeight <= 0) return;

            Vector3 pos = cam.transform.position;
            Vector3 snapped = Snap(pos, out float lossPxX, out float lossPxY);

            if (snapped != pos)
            {
                unsnappedPos = pos;
                cam.transform.position = snapped;
                snappedThisRender = true;
            }

            BlitScaleBias = BuildScaleBias(lossPxX, lossPxY);
        }

        /// <summary>
        /// Snap a world position to the internal-pixel grid the same way the world camera is grid-locked
        /// each frame. Returns pos unchanged when there is no controller, no RT yet, or snapping is off,
        /// so callers work unmodified in scenes with no pixel rig.
        /// </summary>
        public static Vector3 SnapWorldPosition(Vector3 pos) => SnapWorldPosition(pos, out _, out _);

        /// <summary>As above, and reports what was rounded away, in internal pixels ([-0.5, 0.5]).</summary>
        public static Vector3 SnapWorldPosition(Vector3 pos, out float lossPxX, out float lossPxY)
        {
            var c = Active;
            if (c == null) { lossPxX = lossPxY = 0f; return pos; }
            return c.Snap(pos, out lossPxX, out lossPxY);
        }

        Vector3 Snap(Vector3 pos, out float lossPxX, out float lossPxY)
        {
            lossPxX = lossPxY = 0f;

            // UPP = world height of one RT pixel, recomputed each call so it tracks ortho zoom / RT changes.
            float upp = (cam != null && builtHeight > 0) ? (cam.orthographicSize * 2f) / builtHeight : 0f;
            if (!enableSnapping || upp <= 0f) return pos;

            // Snap in CAMERA space (right/up), not world XYZ — the pixel grid is aligned to the view
            // plane, so for an angled iso camera world-axis snapping still crawls. Leave forward alone.
            Vector3 right = cam.transform.right;
            Vector3 up = cam.transform.up;

            float alongRight = Vector3.Dot(pos, right);
            float alongUp = Vector3.Dot(pos, up);
            float snappedRight = Mathf.Round(alongRight / upp) * upp;
            float snappedUp = Mathf.Round(alongUp / upp) * upp;

            // Loss = what we rounded away, in internal pixels ([-0.5, 0.5]).
            lossPxX = (alongRight - snappedRight) / upp;
            lossPxY = (alongUp - snappedUp) / upp;

            return pos + (snappedRight - alongRight) * right + (snappedUp - alongUp) * up;
        }

        void OnEndCameraRendering(ScriptableRenderContext ctx, Camera camera)
        {
            if (camera != cam || !snappedThisRender) return;
            cam.transform.position = unsnappedPos;
            snappedThisRender = false;
        }

        // Crop the displayed region out of the overscanned RT, shifted by the sub-pixel loss, V-flipped
        // for top-origin graphics APIs (where the camera RT samples upside down vs the backbuffer).
        Vector4 BuildScaleBias(float lossPxX, float lossPxY)
        {
            float rw = builtWidth, rh = builtHeight;
            if (rw <= 0f || rh <= 0f) return new Vector4(1f, 1f, 0f, 0f);

            float scaleX = displayedWidth / rw;
            float scaleY = displayedHeight / rh;
            float biasX = (Overscan + lossPxX) / rw;
            float biasY = (Overscan + lossPxY) / rh;

            if (SystemInfo.graphicsUVStartsAtTop)
            {
                // Mirror V: uv.y = texcoord.y * (-scaleY) + (biasY + scaleY).
                biasY += scaleY;
                scaleY = -scaleY;
            }
            return new Vector4(scaleX, scaleY, biasX, biasY);
        }

        void EnsureTarget(int w, int h)
        {
            if (internalRT != null && builtWidth == w && builtHeight == h)
            {
                // Re-assert in case something else cleared the target (e.g. domain reload in edit mode).
                if (cam.targetTexture != internalRT) cam.targetTexture = internalRT;
                return;
            }

            ReleaseTarget();

            // Depth attached (24 bits): URP requires a camera's Output Texture to have a depth/stencil
            // format. The upscale pass samples this RT's color directly via Blitter (it does NOT
            // ImportTexture it), so RenderGraph's "color+depth not allowed on import" validation never runs.
            internalRT = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "PixelCamera_Internal",
                filterMode = FilterMode.Point,   // no bilinear, anywhere
                antiAliasing = 1,                // MSAA off (fights the hard-edge look + the edge pass)
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            internalRT.Create();

            InternalRTHandle = RTHandles.Alloc(internalRT);
            cam.targetTexture = internalRT;
            builtWidth = InternalWidth = w;
            builtHeight = InternalHeight = h;
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
        }
    }
}
