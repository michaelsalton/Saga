using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Saga.Rendering
{
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/World Occlusion")]
    [DisallowMultipleComponent]
    public class WorldOcclusionVolume : MonoBehaviour
    {
        [Header("Eligibility")]
        [Tooltip("Which RENDERING LAYERS can be cut. This is the opt-in list: a renderer whose " +
                 "Rendering Layers (MeshRenderer > Additional Settings) share no bit with this is " +
                 "skipped outright, however deep inside the disc it sits.\n\n" +
                 "Name a layer for it first in Project Settings > Tags and Layers > Rendering " +
                 "Layers, then tick that layer on the objects you want dissolvable. A renderer " +
                 "defaults to layer 0 only, so EVERYTHING is opted out until you tick it -- " +
                 "including the walls and props that were being cut before.\n\n" +
                 "ADD the bit rather than replacing the existing one: if lights are using rendering " +
                 "layers, clearing layer 0 would drop the object out of its lighting too.\n\n" +
                 "Only reaches shaders that define SAGA_OCCLUDABLE (Saga/Lit). Saga/Ground and " +
                 "Saga/Grass never cut regardless of what is ticked.")]
        [SerializeField] RenderingLayerMask occludableLayers = 1 << 1;

        [Header("Anchor")]
        [Tooltip("What the circle follows. Leave EMPTY for the fake player: the point where the " +
                 "camera's centre view ray meets the ground plane.")]
        [SerializeField] Transform target;

        [Tooltip("The WORLD camera -- the one that renders into the internal RT, never the composite. " +
                 "Leave empty for Camera.main.")]
        [SerializeField] Camera worldCamera;

        [Tooltip("World Y the fake anchor sits on. Only used when Target is empty.")]
        [SerializeField] float groundHeight;

        [Tooltip("Metres to lift the anchor off the ground. NOT cosmetic: the cut plane passes " +
                 "through the anchor PERPENDICULAR to the view axis, so an anchor on the ground puts " +
                 "a standing player's whole body on the cut side. Chest height is right.")]
        [Range(0f, 3f)]
        [SerializeField] float centerHeight = 0.9f;

        [Header("Disc")]
        [Tooltip("Radius in METRES, measured perpendicular to the view axis -- which is why it is " +
                 "exactly a screen circle under the ortho camera at any zoom and any heading.\n\n" +
                 "0 is an EXACT off switch: every shader entry point returns before touching dither " +
                 "or clip.\n\n" +
                 "The camera sees 17.8 x 10 m, so 2 is about 40 percent of the view height.")]
        [Range(0f, 6f)]
        [SerializeField] float radius = 2f;

        [Tooltip("Metres of radial falloff outside the radius. Also the width of the band the " +
                 "outline suppression fades across.")]
        [Range(0.05f, 3f)]
        [SerializeField] float feather = 0.6f;

        [Tooltip("Where the cut plane sits, in metres along the view axis measured from the anchor. " +
                 "This is the ONLY control over how much depth the cut catches: the region is a " +
                 "half-space, unbounded toward the camera, so there is no far end to extend. Lower " +
                 "this to catch more.\n\n" +
                 "POSITIVE puts the plane between the anchor and the camera, so the anchor itself is " +
                 "never cut -- once a player exists it must exceed their half-thickness along the " +
                 "view axis or a Saga/Lit player will dissolve itself. NEGATIVE puts the plane " +
                 "BEHIND the anchor, which catches far more but will eat the player too.")]
        [Range(-6f, 3f)]
        [SerializeField] float depthBias = 0.5f;

        [Tooltip("Metres over which the depth gate ramps in, so a surface straddling the cut plane " +
                 "fades instead of snapping.")]
        [Range(0.01f, 3f)]
        [SerializeField] float depthFeather = 0.5f;

        [Header("Ground")]
        [Tooltip("Metres above the anchor's ground plane before ANYTHING can be cut, and the answer " +
                 "to 'stop punching holes in the lawn'.\n\n" +
                 "The depth gate cannot express this on its own: it is a plane perpendicular to the " +
                 "view axis, so a grass tuft directly in front of the anchor crosses it exactly like " +
                 "a wall does. This gates on world Y instead, so short geometry is immune at any " +
                 "depth.\n\n" +
                 "Set it to the height of the tallest thing you never want punctured -- 0.5 clears " +
                 "grass tufts and kerbs. Note the flip side: the bottom of this many metres of a " +
                 "TALL occluder also stays solid, so a hedge can still hide a player's feet.")]
        [Range(0f, 3f)]
        [SerializeField] float rise = 0.5f;

        [Tooltip("Metres over which the height gate ramps in. Wide enough to read as a fade rather " +
                 "than a horizontal slice through the occluder.")]
        [Range(0.01f, 3f)]
        [SerializeField] float riseFeather = 0.5f;

        [Header("Dissolve")]
        [Tooltip("Fraction of pixels KEPT at the centre of the disc. 0 is a clean hard-clipped " +
                 "window; raise it to leave a stipple you can still read the cut surface through.")]
        [Range(0f, 1f)]
        [SerializeField] float coreCoverage = 0.2f;

        [Tooltip("Mask value at which the stipple STARTS opening. Below it the surface is kept " +
                 "whole, so the gap between it and 1 is how much of the radial falloff the dissolve " +
                 "ramp gets: raise it for a harder rim, lower it to spread the stipple outward.")]
        [Range(0f, 0.95f)]
        [SerializeField] float holeOnset = 0.35f;

        [Header("Integration")]
        [Tooltip("How far Outline.shader mutes its edges inside the disc. The stipple alternates " +
                 "prop and background depth on adjacent pixels, so at 0 the depth edge detector " +
                 "fills the whole disc with ink. Suppression fades out at the rim, so a line framing " +
                 "the hole survives at any value.")]
        [Range(0f, 1f)]
        [SerializeField] float outlineSuppress = 1f;

        static readonly int CenterID        = Shader.PropertyToID("_OccCenter");
        static readonly int AxisID          = Shader.PropertyToID("_OccAxis");

        static readonly int RadiusID        = Shader.PropertyToID("_OccRadius");
        static readonly int FeatherID       = Shader.PropertyToID("_OccFeather");
        static readonly int DepthBiasID     = Shader.PropertyToID("_OccDepthBias");
        static readonly int DepthFeatherID  = Shader.PropertyToID("_OccDepthFeather");
        static readonly int GroundYID       = Shader.PropertyToID("_OccGroundY");
        static readonly int RiseID          = Shader.PropertyToID("_OccRise");
        static readonly int RiseFeatherID   = Shader.PropertyToID("_OccRiseFeather");
        static readonly int CoreCoverageID  = Shader.PropertyToID("_OccCoreCoverage");
        static readonly int HoleOnsetID     = Shader.PropertyToID("_OccHoleOnset");
        static readonly int OutlineSupID    = Shader.PropertyToID("_OccOutlineSuppress");
        static readonly int LayerMaskID     = Shader.PropertyToID("_OccLayerMask");

        void OnEnable()
        {
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
            Shader.SetGlobalFloat(RadiusID, 0f);
        }

        void OnValidate()
        {
            if (isActiveAndEnabled) Publish();
        }

        void LateUpdate()
        {
            if (Application.isPlaying) Publish();
        }

        Camera ResolveCamera() => worldCamera != null ? worldCamera : Camera.main;

        Vector3 ResolveAnchor(Camera cam)
        {
            if (target != null) return target.position;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Mathf.Abs(ray.direction.y) < 1e-4f) return ray.origin;

            return ray.origin + ray.direction * ((groundHeight - ray.origin.y) / ray.direction.y);
        }

        void Publish()
        {
            var cam = ResolveCamera();
            if (cam == null)
            {
                Shader.SetGlobalFloat(RadiusID, 0f);
                return;
            }

            Vector3 anchor = ResolveAnchor(cam);
            Vector3 center = PixelCameraController.SnapWorldPosition(anchor + Vector3.up * centerHeight);

            Shader.SetGlobalVector(CenterID, center);
            Shader.SetGlobalVector(AxisID,   cam.transform.forward);

            Shader.SetGlobalFloat(RadiusID,       radius);
            Shader.SetGlobalFloat(FeatherID,      Mathf.Max(1e-4f, feather));
            Shader.SetGlobalFloat(DepthBiasID,    depthBias);
            Shader.SetGlobalFloat(DepthFeatherID, Mathf.Max(1e-4f, depthFeather));
            Shader.SetGlobalFloat(GroundYID,      anchor.y);
            Shader.SetGlobalFloat(RiseID,         rise);
            Shader.SetGlobalFloat(RiseFeatherID,  Mathf.Max(1e-4f, riseFeather));
            Shader.SetGlobalFloat(CoreCoverageID, coreCoverage);
            Shader.SetGlobalFloat(HoleOnsetID,    holeOnset);
            Shader.SetGlobalFloat(OutlineSupID,   outlineSuppress);

            // SetGlobalInteger, NOT SetGlobalFloat: this is a 32-bit mask and a float cannot hold
            // the high bits exactly.
            Shader.SetGlobalInteger(LayerMaskID, (int)occludableLayers);
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying) return;
            Publish();
        }

        void OnDrawGizmosSelected()
        {
            var cam = ResolveCamera();
            if (cam == null || radius <= 0.001f) return;

            Vector3 anchor = ResolveAnchor(cam);
            Vector3 c   = PixelCameraController.SnapWorldPosition(anchor + Vector3.up * centerHeight);
            Vector3 fwd = cam.transform.forward;
            Vector3 rt  = cam.transform.right;
            Vector3 up  = cam.transform.up;
            Vector3 start = c - fwd * depthBias;
            Vector3 end   = start - fwd * 8f;

            Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.9f);

            const int Segments = 48;
            Vector3 prevA = Vector3.zero, prevB = Vector3.zero;
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 off = (rt * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
                Vector3 pa = start + off;
                Vector3 pb = end + off;
                if (i > 0)
                {
                    Gizmos.DrawLine(prevA, pa);
                    Gizmos.DrawLine(prevB, pb);
                    if (i % 12 == 0) Gizmos.DrawLine(pa, pb);
                }
                prevA = pa; prevB = pb;
            }

            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.7f);
            Vector3 floor = new Vector3(c.x, anchor.y + rise, c.z);
            for (int i = 0; i < Segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / Segments;
                float a1 = (i + 1) * Mathf.PI * 2f / Segments;
                Gizmos.DrawLine(floor + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius,
                                floor + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius);
            }

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(c, 0.08f);
        }
#endif
    }
}
