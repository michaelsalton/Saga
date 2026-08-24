using UnityEngine;
using UnityEngine.InputSystem;

namespace Saga
{
    /// <summary>
    /// Drop a prefab in and it spawns at this object's position, recentred on its visual bounds so it
    /// rotates around its middle (not its pivot). It previews in the SCENE VIEW in edit mode and in play
    /// mode. In play mode: left-drag spins it (turntable inspection), scroll zooms, right-click resets.
    /// Swap the prefab at any time and it respawns.
    ///
    /// The preview is spawned with HideAndDontSave so it never serializes into your scene or shows in the
    /// hierarchy. Reads the Input System devices directly (no action asset). Drag interaction is play-mode
    /// only — in the scene view it just displays (rotate the analyzer GameObject itself to look around it).
    /// </summary>
    [ExecuteAlways]
    public class ObjectAnalyzer : MonoBehaviour
    {
        [Header("Subject")]
        [Tooltip("The prefab to display. Change this at any time and it respawns (edit or play mode).")]
        [SerializeField] GameObject prefab;

        [Header("Rotate (play mode)")]
        [Tooltip("Degrees per pixel of left-drag.")]
        [Range(0.05f, 2f)]
        [SerializeField] float rotateSensitivity = 0.4f;

        [Header("Zoom (play mode)")]
        [SerializeField] bool allowZoom = true;
        [Range(0.01f, 0.5f)]
        [SerializeField] float zoomSensitivity = 0.1f;
        [Tooltip("Min/max scale multiplier applied to the subject.")]
        [SerializeField] Vector2 zoomRange = new Vector2(0.25f, 4f);

        [Header("Inertia (play mode)")]
        [Tooltip("How quickly the coast spin slows after release. Higher = stops sooner (heavier friction); " +
                 "lower = coasts longer / feels lighter.")]
        [Range(0.5f, 12f)]
        [SerializeField] float spinDamping = 4f;
        [Tooltip("Max coast speed (deg/sec) a flick can impart, so a hard fling doesn't spin out of control.")]
        [SerializeField] float maxSpinSpeed = 720f;

        Transform pivot;
        GameObject instance;
        GameObject spawnedFrom;   // which prefab the current instance came from
        float zoom = 1f;
        Vector2 spin;             // coast angular velocity, deg/sec (x = yaw, y = pitch)

        void OnEnable() => Respawn();
        void OnDisable() => Clear();

        void Update()
        {
            if (prefab != spawnedFrom) Respawn();   // hot-swap the prefab field
            if (!Application.isPlaying) return;       // edit mode: display only, no mouse interaction
            if (pivot == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;

            // Left-drag = grab-and-spin. Yaw around world up, pitch around the viewing camera's right axis.
            // (To drag the object through space instead, replace these two Rotate calls with a
            //  pivot.position += camera-plane translation from mouse.delta.)
            if (mouse.leftButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * rotateSensitivity;   // degrees this frame
                pivot.Rotate(Vector3.up, -d.x, Space.World);
                pivot.Rotate(right, d.y, Space.World);

                // Track angular velocity (deg/sec) for the coast after release. Smoothed so a flick reads
                // cleanly and holding still before letting go leaves ~0 (no accidental fling).
                Vector2 vel = d / dt;
                spin = Vector2.Lerp(spin, vel, 1f - Mathf.Exp(-25f * dt));
                spin = Vector2.ClampMagnitude(spin, maxSpinSpeed);
            }
            else if (spin.sqrMagnitude > 0.01f)
            {
                // Coast: keep spinning under the last velocity, bleeding it off like friction (the "weight").
                pivot.Rotate(Vector3.up, -spin.x * dt, Space.World);
                pivot.Rotate(right, spin.y * dt, Space.World);
                spin *= Mathf.Exp(-spinDamping * dt);
                if (spin.sqrMagnitude < 0.01f) spin = Vector2.zero;
            }

            // Right-click resets orientation, spin + zoom.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                pivot.localRotation = Quaternion.identity;
                spin = Vector2.zero;
                zoom = 1f;
                ApplyZoom();
            }

            // Scroll zooms by scaling the subject.
            if (allowZoom)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    zoom = Mathf.Clamp(zoom * (1f + Mathf.Sign(scroll) * zoomSensitivity), zoomRange.x, zoomRange.y);
                    ApplyZoom();
                }
            }
        }

        [ContextMenu("Respawn")]
        public void Respawn()
        {
            Clear();
            spawnedFrom = prefab;
            if (prefab == null) return;

            // A pivot at this object's position; rotating/scaling the pivot inspects the subject.
            // HideAndDontSave keeps the preview out of the saved scene and the hierarchy.
            pivot = new GameObject("AnalyzerPivot") { hideFlags = HideFlags.HideAndDontSave }.transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;

            instance = Instantiate(prefab, pivot);
            instance.hideFlags = HideFlags.HideAndDontSave;
            RecentreOnBounds();
            zoom = 1f;
            ApplyZoom();
        }

        // Offset the instance so the centre of its combined renderer bounds sits at the pivot origin,
        // so a freshly-dropped prefab with an off-centre pivot still spins around its visual middle.
        void RecentreOnBounds()
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            instance.transform.position -= b.center - pivot.position;
        }

        void ApplyZoom()
        {
            if (pivot != null) pivot.localScale = Vector3.one * zoom;
        }

        void Clear()
        {
            DestroySafe(instance);
            if (pivot != null) DestroySafe(pivot.gameObject);
            instance = null;
            pivot = null;
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Only respawn in edit mode when the PREFAB itself changed (not when tweaking other fields),
            // and defer it: Instantiate/Destroy aren't allowed directly inside OnValidate.
            if (Application.isPlaying || prefab == spawnedFrom) return;
            UnityEditor.EditorApplication.delayCall += DeferredRespawn;
        }

        void DeferredRespawn()
        {
            if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
            Respawn();
        }
#endif
    }
}
