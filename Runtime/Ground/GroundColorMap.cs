using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Ground Color Map")]
    [DisallowMultipleComponent]
    public class GroundColorMap : MonoBehaviour
    {
        [Header("Captured area (world XZ)")]
        [Tooltip("Minimum corner of the captured rectangle, in world X/Z. Anything outside the rectangle " +
                 "clamps to the nearest edge texel rather than going black — but it will be visibly wrong, " +
                 "so cover every place a ground-color sprite can stand. Use 'Fit To Ground Bounds'.")]
        [SerializeField] Vector2 worldOrigin = new Vector2(-32f, -32f);

        [Tooltip("Size of the captured rectangle along world X and Z. Must be positive.")]
        [SerializeField] Vector2 worldSize = new Vector2(64f, 64f);

        [Tooltip("Height the capture camera sits at. Must clear the tallest ground geometry — anything " +
                 "above the camera is behind its near plane and vanishes from the map.")]
        [SerializeField] float captureHeight = 100f;

        [Header("What gets photographed")]
        [Tooltip("Layers rendered into the map. Ground + GroundMask by default. CRITICAL: never include a " +
                 "layer holding ground-color sprites — they would sample the map they are being baked " +
                 "into, freezing one frame of garbage in. A warning fires at capture time if that happens.")]
        [SerializeField] LayerMask groundMask = (1 << 3) | (1 << 7);   // Ground, GroundMask

        [Tooltip("Color of the map wherever no ground was drawn. Sprites standing over a hole (or outside " +
                 "the rectangle, via edge clamp) get this. Do not leave it white. ALPHA IS IGNORED — the " +
                 "capture forces it to 0 so the channel can carry the ground-coverage mask.")]
        [SerializeField] Color emptyColor = new Color(0.28f, 0.24f, 0.19f, 1f);

        [Header("Resolution")]
        [Tooltip("Map texels per world unit. Drives both dimensions, so texels stay square on a " +
                 "non-square rectangle. Higher = finer ground-color detail under a sprite.")]
        [Range(1f, 128f)]
        [SerializeField] float texelsPerUnit = 16f;

        [Tooltip("Ceiling on either dimension, so a large world can't silently allocate a huge " +
                 "RenderTexture. If a dimension clamps here, the log line reports the effective density.")]
        [SerializeField] int maxResolution = 2048;

        [Tooltip("Bilinear-filter the map, blending ground colors smoothly under a sprite. Off (Point) " +
                 "everywhere else in this project's render targets, and off matches the hard-edged look.")]
        [SerializeField] bool smoothSampling;

        [Header("Plumbing")]
        [Tooltip("Index of a feature-less URP renderer in the URP asset's renderer list. 1 = " +
                 "GroundCapture_Renderer. Use -1 to capture through the default renderer, which drags the " +
                 "outline and pixel-upscale passes into the map.")]
        [SerializeField] int cleanRendererIndex = 1;

        [Tooltip("Frames to wait after enable before the first capture. Ground built in Start/Awake (or " +
                 "by a mesh generator) does not exist on frame 0. Raise this if the map comes out empty.")]
        [Range(0, 10)]
        [SerializeField] int warmupFrames = 1;

        [Tooltip("Flip the map along Z. Camera up is +Z and the shader maps v to Z, so this should be off " +
                 "— but if ground colors come out mirrored north/south, this is the one switch to try.")]
        [SerializeField] bool flipV;

        [Tooltip("Log the resolution, density and world rectangle on every capture.")]
        [SerializeField] bool logOnCapture = true;

        static readonly int GroundTexID  = Shader.PropertyToID("_GroundColorTex");
        static readonly int GroundRectID = Shader.PropertyToID("_GroundColorRect");
        static readonly int CloudStrengthID = Shader.PropertyToID("_CloudStrength");

        /// <summary>The published map, or null before the first capture. Read-only for other systems.</summary>
        public static RenderTexture GroundTexture { get; private set; }

        /// <summary>The published (originX, originZ, 1/sizeX, 1/sizeZ) mapping, matching the shader.</summary>
        public static Vector4 GroundRect { get; private set; }

        RenderTexture rt;
        int builtWidth, builtHeight;
        bool capturePending;
        int framesUntilCapture;

        void OnEnable()
        {
            RequestCapture();
        }

        void OnDisable()
        {
            Shader.SetGlobalTexture(GroundTexID, Texture2D.grayTexture);
            GroundTexture = null;
            ReleaseRT();
        }

        void OnValidate()
        {
            worldSize.x = Mathf.Max(0.01f, worldSize.x);
            worldSize.y = Mathf.Max(0.01f, worldSize.y);
            maxResolution = Mathf.Clamp(maxResolution, 16, 8192);
            captureHeight = Mathf.Max(0.01f, captureHeight);

            if (isActiveAndEnabled) RequestCapture();
        }

        void Update()
        {
            if (!capturePending) return;
            if (framesUntilCapture-- > 0) return;
            capturePending = false;
            Capture();
        }

        /// <summary>Queue a re-capture for the next tick, after the configured warm-up.</summary>
        public void RequestCapture()
        {
            capturePending = true;
            framesUntilCapture = Mathf.Max(0, warmupFrames);
        }

        [ContextMenu("Capture Now")]
        void CaptureNow()
        {
            capturePending = false;
            Capture();
        }

        void Capture()
        {
            if (GraphicsSettings.currentRenderPipeline == null) return;

            WarnAboutSelfCapture();
            EnsureRT();
            if (rt == null) return;

            // This map is a static snapshot of LIT ground, so anything live in the lighting gets frozen
            // into it. With clouds enabled, one frame of cloud pattern would be baked in permanently and
            // every grass fallback lookup would carry a stale shadow that never moves. Suppress for the
            // duration of the render; read the value back rather than referencing CloudShadowVolume, so
            // this stays decoupled. Safe because SubmitRenderRequest is synchronous.
            float cloudRestore = Shader.GetGlobalFloat(CloudStrengthID);
            Shader.SetGlobalFloat(CloudStrengthID, 0f);

            var go = new GameObject("GroundColorCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                go.transform.SetPositionAndRotation(
                    new Vector3(worldOrigin.x + worldSize.x * 0.5f,
                                captureHeight,
                                worldOrigin.y + worldSize.y * 0.5f),
                    Quaternion.Euler(90f, 0f, 0f));

                cam.orthographic = true;
                cam.orthographicSize = worldSize.y * 0.5f;
                cam.aspect = worldSize.x / worldSize.y;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = captureHeight * 2f;
                cam.cullingMask = groundMask;
                cam.clearFlags = CameraClearFlags.SolidColor;

                cam.backgroundColor = new Color(emptyColor.r, emptyColor.g, emptyColor.b, 0f);
                cam.allowHDR = true;
                cam.allowMSAA = false;
                cam.useOcclusionCulling = false;
                cam.enabled = false;

                var data = cam.GetUniversalAdditionalCameraData();
                if (cleanRendererIndex >= 0) data.SetRenderer(cleanRendererIndex);
                data.renderPostProcessing = false;
                data.requiresColorOption  = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.renderShadows = true;

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                }
            }
            finally
            {
                Shader.SetGlobalFloat(CloudStrengthID, cloudRestore);

                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            Publish();
        }

        void Publish()
        {
            GroundTexture = rt;
            GroundRect    = BuildRect();
            Shader.SetGlobalTexture(GroundTexID, rt);
            Shader.SetGlobalVector(GroundRectID, GroundRect);

            if (!logOnCapture) return;
            Debug.Log($"[{nameof(GroundColorMap)}] Captured {builtWidth}x{builtHeight} " +
                      $"({builtWidth / worldSize.x:0.#} x {builtHeight / worldSize.y:0.#} texels/unit) over " +
                      $"X [{worldOrigin.x:0.##}, {worldOrigin.x + worldSize.x:0.##}] " +
                      $"Z [{worldOrigin.y:0.##}, {worldOrigin.y + worldSize.y:0.##}].", this);
        }

        Vector4 BuildRect()
        {
            float v0 = worldOrigin.y;
            float sv = 1f / worldSize.y;
            if (flipV) { v0 = worldOrigin.y + worldSize.y; sv = -sv; }
            return new Vector4(worldOrigin.x, v0, 1f / worldSize.x, sv);
        }

        const string GroundColorShaderName = "Saga/Grass";

        void WarnAboutSelfCapture()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (var r in renderers)
            {
                if ((groundMask.value & (1 << r.gameObject.layer)) == 0) continue;

                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;
                    if (mat.shader.name != GroundColorShaderName) continue;

                    Debug.LogWarning(
                        $"[{nameof(GroundColorMap)}] '{r.name}' uses {GroundColorShaderName} and sits on " +
                        $"layer '{LayerMask.LayerToName(r.gameObject.layer)}', which is INSIDE Ground Mask. " +
                        $"It will sample the map it is being baked into. Move it to the 'Grass' layer (or " +
                        $"any layer outside the mask).", r);
                    return;
                }
            }
        }

        void EnsureRT()
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(worldSize.x * texelsPerUnit), 16, maxResolution);
            int h = Mathf.Clamp(Mathf.RoundToInt(worldSize.y * texelsPerUnit), 16, maxResolution);
            var filter = smoothSampling ? FilterMode.Bilinear : FilterMode.Point;

            if (rt != null && builtWidth == w && builtHeight == h)
            {
                rt.filterMode = filter;
                return;
            }

            ReleaseRT();

            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "GroundColorMap",
                filterMode = filter,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            rt.Create();

            builtWidth = w;
            builtHeight = h;
        }

        void ReleaseRT()
        {
            if (rt == null) return;
            rt.Release();
            if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
            rt = null;
            builtWidth = builtHeight = 0;
        }

        [ContextMenu("Fit To Ground Bounds")]
        void FitToGroundBounds()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            bool any = false;
            Bounds total = default;

            foreach (var r in renderers)
            {
                if ((groundMask.value & (1 << r.gameObject.layer)) == 0) continue;
                if (!any) { total = r.bounds; any = true; }
                else total.Encapsulate(r.bounds);
            }

            if (!any)
            {
                Debug.LogWarning($"[{nameof(GroundColorMap)}] No active renderers on the ground mask — " +
                                 $"nothing to fit to. Check Ground Mask.", this);
                return;
            }

            const float margin = 1f;
            worldOrigin = new Vector2(total.min.x - margin, total.min.z - margin);
            worldSize   = new Vector2(total.size.x + margin * 2f, total.size.z + margin * 2f);
            captureHeight = Mathf.Max(captureHeight, total.max.y + 10f);

            RequestCapture();
        }

        void OnDrawGizmosSelected()
        {
            var centre = new Vector3(worldOrigin.x + worldSize.x * 0.5f,
                                     transform.position.y,
                                     worldOrigin.y + worldSize.y * 0.5f);
            var size = new Vector3(worldSize.x, 0f, worldSize.y);

            Gizmos.color = new Color(0.5f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(centre, size);

            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
            var eye = new Vector3(centre.x, captureHeight, centre.z);
            Gizmos.DrawLine(eye, centre);
            Gizmos.DrawWireSphere(eye, Mathf.Max(worldSize.x, worldSize.y) * 0.02f);
        }
    }
}
