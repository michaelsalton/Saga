using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Saga.Rendering
{
    [AddComponentMenu("Saga/Rendering/Water Reflection")]
    [DisallowMultipleComponent]
    public class WaterReflection : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The camera the mirror is derived from. Leave empty to use Camera.main.")]
        [SerializeField] Camera worldCamera;

        [Header("Reflected scene")]
        [Tooltip("Layers rendered into the reflection. This object's OWN layer is always stripped so the " +
                 "water never reflects itself — keep the water on a layer of its own (4 'Water').")]
        [SerializeField] LayerMask reflectionMask = ~0;
        [Tooltip("What the reflection shows where nothing is reflected. Under an orthographic camera " +
                 "every reflection ray is parallel, so empty sky resolves to this ONE flat color across " +
                 "the whole surface — it is not a gradient. Do not leave it white.\n\n" +
                 "Also published as _WaterReflectionSkyColor: it is what distant reflections wash INTO.")]
        [SerializeField] Color skyColor = new Color(0.36f, 0.55f, 0.78f, 1f);
        [Tooltip("Index of a feature-less URP renderer (no outline / pixel passes) in the URP asset's " +
                 "renderer list. Usually 1 (GroundCapture_Renderer). Use -1 to keep the default renderer.")]
        [SerializeField] int cleanRendererIndex = 1;

        [Header("Water plane")]
        [Tooltip("Take the mirror plane's height from this object's Renderer bounds, which is correct for " +
                 "a flat water mesh no matter how the transform is parented or offset. Turn off to type " +
                 "the world Y in by hand. Getting this wrong displaces every reflection by twice the " +
                 "error and lets submerged geometry survive the clip — check the gizmo.")]
        [SerializeField] bool autoWaterLevel = true;
        [Tooltip("World Y of the water surface, used when Auto Water Level is off.")]
        [SerializeField] float waterLevel;

        [Header("Quality")]
        [Tooltip("Reflection RT size = internal render size / this. 1 = full internal res, 2 = half.\n\n" +
                 "Note for distance-based reflections: the depth capture is divided too, so above 1 the " +
                 "reflected objects' silhouettes get a texel or two of wrong distance around their edges.")]
        [Range(1, 4)]
        [SerializeField] int resolutionDivisor = 1;
        [Tooltip("Skew the reflection camera's near plane onto the water so geometry below the surface is " +
                 "clipped out. Turn OFF only to A/B whether the clip is working: with it off, a " +
                 "half-submerged object shows TWO overlapping mirror images instead of one.")]
        [SerializeField] bool obliqueClip = true;
        [Tooltip("How far to push the oblique clip plane below the water surface. Negative = below. Too " +
                 "shallow and below-water geometry bleeds in; too deep and the shoreline shows a seam.")]
        [SerializeField] float clipPlaneOffset = -0.05f;
        [Tooltip("Flip the reflection vertically. Whether the reflection RT and the main render target " +
                 "agree on V depends on the graphics API and on whether the main camera renders into a " +
                 "RenderTexture. If the reflection comes out upside down, toggle this.")]
        [SerializeField] bool flipV;

        [Header("Distance")]
        [Tooltip("Capture the mirror's depth as metres of eye depth, so Saga/Water can fade, distort and " +
                 "blur a reflection by how far the REFLECTED OBJECT is from the surface.\n\n" +
                 "Needs WaterReflectionDepthFeature on the clean renderer as well as this toggle. With " +
                 "either missing the water reads 0 everywhere, treats it as sky, and every _ReflectionFar* " +
                 "dial becomes a no-op — reflections look exactly as they did before. Costs one extra " +
                 "RenderTexture and one depth prepass on the mirror camera.")]
        [SerializeField] bool captureDepth = true;

        static readonly int ReflectionTexID       = Shader.PropertyToID("_WaterReflectionTex");
        static readonly int ReflectionScaleBiasID = Shader.PropertyToID("_WaterReflectionScaleBias");
        static readonly int ReflectionTexelSizeID = Shader.PropertyToID("_WaterReflectionTexelSize");
        static readonly int ReflectionSkyColorID  = Shader.PropertyToID("_WaterReflectionSkyColor");
        static readonly int ReflectionDepthTexID  = Shader.PropertyToID("_WaterReflectionDepthTex");
        static readonly int ReflectionDepthValid  = Shader.PropertyToID("_WaterReflectionDepthValid");
        static readonly int ReflectionInvVPID     = Shader.PropertyToID("_WaterReflectionInvVP");
        static readonly int ReflectionEyeAxisID   = Shader.PropertyToID("_WaterReflectionEyeAxis");

        /// <summary>
        /// The live mirror camera, so <see cref="WaterReflectionDepthFeature"/> can tell it apart from the
        /// other cameras that share the clean renderer. Null when no reflection is running.
        /// </summary>
        public static Camera ActiveReflectionCamera { get; private set; }

        /// <summary>
        /// Where <see cref="WaterReflectionDepthPass"/> writes the linearized mirror depth. A persistent
        /// handle, because the water consumes it during a LATER camera's render graph.
        /// </summary>
        public static RTHandle DepthTarget { get; private set; }

        Camera mainCam;
        Camera reflCam;
        RenderTexture rt;
        RenderTexture depthRT;
        int builtWidth, builtHeight;
        bool cullingInverted;

        void OnEnable()
        {
            mainCam = worldCamera != null ? worldCamera : Camera.main;
            if (mainCam == null)
            {
                Debug.LogError($"[{nameof(WaterReflection)}] No world camera assigned and no Camera.main.", this);
                enabled = false;
                return;
            }

            EnsureCamera();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering   += OnEndCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering   -= OnEndCameraRendering;
            RestoreCulling();                       // never leave the global inverted
            if (reflCam != null) reflCam.enabled = false;
            if (ActiveReflectionCamera == reflCam) ActiveReflectionCamera = null;
        }

        void OnDestroy()
        {
            RestoreCulling();
            if (ActiveReflectionCamera == reflCam) ActiveReflectionCamera = null;
            if (reflCam != null)
            {
                if (Application.isPlaying) Destroy(reflCam.gameObject);
                else DestroyImmediate(reflCam.gameObject);
                reflCam = null;
            }
            ReleaseRT();
        }

        void OnValidate()
        {
            resolutionDivisor = Mathf.Clamp(resolutionDivisor, 1, 4);

            // Park the manual field on the auto value so switching to manual starts from the right height
            // rather than snapping the mirror plane to y = 0.
            if (autoWaterLevel) waterLevel = ResolvePlaneY();

            if (isActiveAndEnabled && reflCam != null) EnsureCamera();
        }

        // --- Per-frame mirror setup -----------------------------------------------------------------

        void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera camera)
        {
            if (camera != reflCam || mainCam == null) return;

            // Sized here, not in LateUpdate: PixelCameraController rebuilds the internal RT in ITS
            // LateUpdate, and the ordering between the two is undefined.
            EnsureRT();
            if (rt == null) return;

            Shader.SetGlobalVector(ReflectionScaleBiasID,
                flipV ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f));

            // Grid-lock: mirror the SNAPPED position. No-ops cleanly in scenes with no pixel rig.
            Vector3 mainPos = PixelCameraController.SnapWorldPosition(mainCam.transform.position);
            float planeY = ResolvePlaneY();

            // Position only, no rotation: a mirror basis is left-handed and no Quaternion can express it,
            // so the matrices below stay authoritative. The position still matters -- URP feeds it to
            // _WorldSpaceCameraPos, which reflected objects read for view-dependent shading (and which
            // matters far more under perspective than under ortho).
            reflCam.transform.position = new Vector3(mainPos.x, 2f * planeY - mainPos.y, mainPos.z);

            Matrix4x4 camToWorld = Matrix4x4.TRS(mainPos, mainCam.transform.rotation, Vector3.one);
            Matrix4x4 mainView   = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * camToWorld.inverse;
            reflCam.worldToCameraMatrix = mainView * ReflectionMatrix(new Vector4(0f, 1f, 0f, -planeY));

            // Copy the main camera's projection VERBATIM -- ortho or perspective -- then skew the near
            // plane onto the water. Copying the matrix rather than orthographicSize / fieldOfView keeps
            // this correct in either mode and picks up any custom projection. Screen-space sampling only
            // works while the two projections agree, so this is the load-bearing line.
            // CalculateObliqueMatrix reads the camera's CURRENT projectionMatrix, so the copy must land
            // first -- and ResetProjectionMatrix() must NOT run after it, or the copy is discarded.
            reflCam.orthographic     = mainCam.orthographic;
            reflCam.aspect           = mainCam.aspect;
            reflCam.nearClipPlane    = mainCam.nearClipPlane;
            reflCam.farClipPlane     = mainCam.farClipPlane;
            reflCam.projectionMatrix = mainCam.projectionMatrix;

            if (obliqueClip)
            {
                Vector4 clipPlane = CameraSpacePlane(reflCam.worldToCameraMatrix,
                                                     new Vector3(0f, planeY, 0f), Vector3.up, clipPlaneOffset);
                reflCam.projectionMatrix = reflCam.CalculateObliqueMatrix(clipPlane);
            }

            PublishDepthDecode();

            GL.invertCulling = true;
            cullingInverted = true;
        }

        /// <summary>
        /// The two uniforms WaterReflectionDepth.shader needs to turn raw device depth into metres.
        /// MUST run after the oblique skew has landed on projectionMatrix — the whole point of inverting the
        /// real matrix is that it carries that skew, and a pre-skew copy would decode to a diagonal gradient.
        /// </summary>
        void PublishDepthDecode()
        {
            Matrix4x4 view = reflCam.worldToCameraMatrix;

            // Eye depth is -viewPos.z, and viewPos.z is row 2 of the view matrix dotted with (ws, 1).
            Vector4 r2 = view.GetRow(2);
            Shader.SetGlobalVector(ReflectionEyeAxisID, new Vector4(-r2.x, -r2.y, -r2.z, -r2.w));

            // renderIntoTexture: true -- the mirror always renders into an RT, which is what decides the
            // reversed-Z / y-flip conventions baked into the GPU projection matrix.
            Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(reflCam.projectionMatrix, true);
            Shader.SetGlobalMatrix(ReflectionInvVPID, (gpuProj * view).inverse);

            // The RT holds linear values, so the wash target has to be the linear form of the same color
            // the camera cleared with -- otherwise distant reflections drift off the sky they sit against.
            Shader.SetGlobalVector(ReflectionSkyColorID,
                QualitySettings.activeColorSpace == ColorSpace.Linear ? skyColor.linear : skyColor);
        }

        void OnEndCameraRendering(ScriptableRenderContext ctx, Camera camera)
        {
            if (camera != reflCam) return;
            RestoreCulling();
        }

        void RestoreCulling()
        {
            if (!cullingInverted) return;
            GL.invertCulling = false;
            cullingInverted = false;
        }

        // --- Math -----------------------------------------------------------------------------------

        /// <summary>
        /// World Y of the mirror plane. Auto mode reads the Renderer's world-space bounds centre, which for
        /// a flat water mesh IS the surface — and unlike transform.position.y it survives the mesh being
        /// offset inside the object or the object being parented under a prefab root at a different height.
        /// Wave displacement is a shader effect and never enters the bounds, so this stays the base plane.
        /// </summary>
        float ResolvePlaneY()
        {
            if (!autoWaterLevel) return waterLevel;
            return TryGetComponent(out Renderer r) ? r.bounds.center.y : transform.position.y;
        }

        // Draws the resolved mirror plane and the clip plane under it, so a wrong water level is visible
        // rather than something you deduce from a bad reflection.
        void OnDrawGizmosSelected()
        {
            float planeY = ResolvePlaneY();
            Vector3 centre = new Vector3(transform.position.x, planeY, transform.position.z);

            Vector3 size = TryGetComponent(out Renderer r)
                ? new Vector3(r.bounds.size.x, 0f, r.bounds.size.z)
                : new Vector3(10f, 0f, 10f);

            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(centre, size);

            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
            Gizmos.DrawWireCube(centre + Vector3.up * clipPlaneOffset, size * 0.98f);
        }

        // Householder reflection about plane p = (nx, ny, nz, d), n normalized.
        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x; m.m01 =    - 2f * p.x * p.y; m.m02 =    - 2f * p.x * p.z; m.m03 = -2f * p.w * p.x;
            m.m10 =    - 2f * p.y * p.x; m.m11 = 1f - 2f * p.y * p.y; m.m12 =    - 2f * p.y * p.z; m.m13 = -2f * p.w * p.y;
            m.m20 =    - 2f * p.z * p.x; m.m21 =    - 2f * p.z * p.y; m.m22 = 1f - 2f * p.z * p.z; m.m23 = -2f * p.w * p.z;
            return m;
        }

        // World plane -> the reflection camera's space, where CalculateObliqueMatrix expects it.
        static Vector4 CameraSpacePlane(Matrix4x4 worldToCam, Vector3 pos, Vector3 normal, float offset)
        {
            Vector3 p = worldToCam.MultiplyPoint(pos + normal * offset);   // offset < 0 pushes it below
            Vector3 n = worldToCam.MultiplyVector(normal).normalized;
            return new Vector4(n.x, n.y, n.z, -Vector3.Dot(p, n));
        }

        // --- Resources ------------------------------------------------------------------------------

        void EnsureCamera()
        {
            if (reflCam == null)
            {
                var go = new GameObject("WaterReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
                reflCam = go.AddComponent<Camera>();
            }

            var data = reflCam.GetUniversalAdditionalCameraData();
            if (cleanRendererIndex >= 0) data.SetRenderer(cleanRendererIndex);
            data.renderPostProcessing = false;
            data.requiresColorOption  = CameraOverrideOption.Off;  // nothing reflected reads the copies

            // The only reason the mirror needs a depth texture is the distance capture. Re-asserted here
            // rather than set once, so toggling captureDepth in the Inspector takes effect via OnValidate.
            data.requiresDepthOption = captureDepth ? CameraOverrideOption.On : CameraOverrideOption.Off;

            reflCam.enabled             = true;                  // URP draws it in the normal camera order
            reflCam.depth               = mainCam.depth - 10f;   // ...before the world camera
            reflCam.orthographic        = mainCam.orthographic;  // re-asserted per frame in OnBeginCameraRendering
            reflCam.clearFlags          = CameraClearFlags.SolidColor;
            reflCam.backgroundColor     = skyColor;
            reflCam.allowHDR            = mainCam.allowHDR;
            reflCam.allowMSAA           = false;
            reflCam.useOcclusionCulling = false;                 // baked data is invalid for the mirror
            reflCam.cullingMask         = reflectionMask & ~(1 << gameObject.layer);

            // Published before any render, so the depth feature can identify this camera on its first frame.
            ActiveReflectionCamera = reflCam;
        }

        void EnsureRT()
        {
            int srcW = PixelCameraController.InternalWidth  > 0 ? PixelCameraController.InternalWidth  : Screen.width;
            int srcH = PixelCameraController.InternalHeight > 0 ? PixelCameraController.InternalHeight : Screen.height;

            int div = Mathf.Max(1, resolutionDivisor);
            int w = Mathf.Max(16, srcW / div);
            int h = Mathf.Max(16, srcH / div);

            bool wantDepth = captureDepth;
            if (rt != null && builtWidth == w && builtHeight == h && (depthRT != null) == wantDepth)
            {
                if (reflCam.targetTexture != rt) reflCam.targetTexture = rt;
                return;
            }

            ReleaseRT();

            rt = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "WaterReflection",
                filterMode = FilterMode.Point,   // no bilinear, anywhere
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            rt.Create();

            reflCam.targetTexture = rt;
            builtWidth = w;
            builtHeight = h;
            Shader.SetGlobalTexture(ReflectionTexID, rt);

            // Pushed explicitly rather than relying on Unity's automatic _TexelSize companion, which is
            // documented for material properties and not for Shader.SetGlobalTexture. The blur radius is
            // denominated in these texels, so a silently-zero value would disable it with no error.
            Shader.SetGlobalVector(ReflectionTexelSizeID, new Vector4(1f / w, 1f / h, w, h));

            if (!wantDepth)
            {
                Shader.SetGlobalFloat(ReflectionDepthValid, 0f);
                return;
            }

            // RFloat, not a depth format: the pass writes METRES of eye depth as colour, already decoded
            // through the oblique projection. Nothing downstream ever wants the raw device value.
            depthRT = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat)
            {
                name = "WaterReflectionDepth",
                filterMode = FilterMode.Point,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
            };
            depthRT.Create();

            DepthTarget = RTHandles.Alloc(depthRT);
            Shader.SetGlobalTexture(ReflectionDepthTexID, depthRT);
        }

        void ReleaseRT()
        {
            if (DepthTarget != null)
            {
                RTHandles.Release(DepthTarget);
                DepthTarget = null;
            }

            if (depthRT != null)
            {
                depthRT.Release();
                if (Application.isPlaying) Destroy(depthRT); else DestroyImmediate(depthRT);
                depthRT = null;
                Shader.SetGlobalFloat(ReflectionDepthValid, 0f);
            }

            if (rt == null) return;
            if (reflCam != null) reflCam.targetTexture = null;
            rt.Release();
            if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
            rt = null;
            builtWidth = builtHeight = 0;
        }
    }
}
