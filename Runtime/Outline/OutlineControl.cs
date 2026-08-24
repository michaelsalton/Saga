using System.Collections.Generic;
using UnityEngine;

namespace Saga.Rendering
{
    /// <summary>
    /// Opt-in outline marker plus per-object outline tuning. A renderer is outlined ONLY while this component
    /// is present and enabled: <see cref="OutlineControlPass"/> walks <see cref="Active"/> and draws those
    /// renderers — and nothing else — into the outline control texture, so an object without the script is
    /// skipped by the pass entirely and the kernel reads the "not outlined" clear value over its pixels.
    ///
    /// The component holds no GPU state of its own; the pass reads <see cref="Values"/> at record time and
    /// binds it per draw. Editing the fields therefore takes effect on the next frame with no Apply() call —
    /// including from script at runtime. Call <see cref="Refresh"/> only after changing the child hierarchy.
    ///
    /// With <see cref="includeChildRenderers"/> the whole sub-hierarchy is covered (characters usually keep
    /// their SkinnedMeshRenderer on a child). A renderer at or under another OutlineControl belongs to that
    /// component, not this one — including when that component is disabled, which reads as "not outlined".
    ///
    /// The headline lever is <see cref="normalWeight"/> = 0: kills a detailed mesh's internal crease spaghetti
    /// while its silhouette/occlusion edges (depth term) survive.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OutlineControl : MonoBehaviour
    {
        /// <summary>A renderer to draw into the control texture, with its submesh count cached — querying it
        /// per frame would allocate, since Renderer.sharedMaterials returns a copy.</summary>
        public readonly struct Target
        {
            public readonly Renderer renderer;
            public readonly int subMeshCount;
            public Target(Renderer r, int count) { renderer = r; subMeshCount = count; }
        }

        static readonly List<OutlineControl> ActiveList = new List<OutlineControl>();
        static readonly List<Renderer> Scratch = new List<Renderer>();

        /// <summary>Every enabled OutlineControl in the scene — the pass's entire draw set.</summary>
        public static IReadOnlyList<OutlineControl> Active => ActiveList;

        [Tooltip("Distinct per-object ID (0..255). Neighbouring outlined objects must differ for the ID/separation " +
                 "term to draw a line between them (e.g. a character flush against a wall). Un-outlined geometry " +
                 "reads as 0, so keep this >= 1 or contact edges against it never appear.")]
        [Range(0, 255)] public int objectId = 1;

        [Tooltip("Weight on the normal/CREASE term. 0 = no internal creases (silhouette only) — the density fix.")]
        [Range(0f, 1f)] public float normalWeight = 0f;

        [Tooltip("Weight on the depth/SILHOUETTE term. Usually 1; lower it for noisy thin/foliage geometry.")]
        [Range(0f, 1f)] public float depthWeight = 1f;

        [Tooltip("Threshold bias. 0.5 = neutral; higher = fewer/thinner lines, lower = more sensitive.")]
        [Range(0f, 1f)] public float thresholdBias = 0.5f;

        [Tooltip("Also outline renderers on child GameObjects. Leave on for characters/props whose renderers " +
                 "live below the root. A child carrying its own OutlineControl is left to that component.")]
        public bool includeChildRenderers = true;

        readonly List<Target> targets = new List<Target>();

        /// <summary>The renderers this component owns, resolved at <see cref="Refresh"/> time.</summary>
        public IReadOnlyList<Target> Targets => targets;

        /// <summary>Packed control value written to the control texture: (id, normal weight, depth weight, bias).
        /// Matches _OutlineControlValues in Saga/OutlineControl and the RGBA channels the kernel reads.</summary>
        public Vector4 Values => new Vector4(
            Mathf.Clamp(objectId, 0, 255) / 255f,
            Mathf.Clamp01(normalWeight),
            Mathf.Clamp01(depthWeight),
            Mathf.Clamp01(thresholdBias));

        void OnEnable()
        {
            if (!ActiveList.Contains(this)) ActiveList.Add(this);
            Refresh();
        }

        void OnDisable()
        {
            // Leaving the registry is the whole teardown: no GPU state is cached anywhere, so the object
            // stops being drawn into the control texture on the very next frame and its outline is gone.
            ActiveList.Remove(this);
            targets.Clear();
        }

        void OnValidate() { if (isActiveAndEnabled) Refresh(); }

        void OnTransformChildrenChanged() { if (isActiveAndEnabled && includeChildRenderers) Refresh(); }

        /// <summary>Re-resolve the renderers this component owns. Needed after the child hierarchy changes;
        /// the outline values themselves are read live by the pass and need no re-push.</summary>
        public void Refresh()
        {
            targets.Clear();

            if (includeChildRenderers)
            {
                GetComponentsInChildren(true, Scratch);
                for (int i = 0; i < Scratch.Count; i++)
                {
                    var r = Scratch[i];
                    if (r != null && OwnerOf(r) == this)
                        targets.Add(new Target(r, SubMeshCount(r)));
                }
                Scratch.Clear();
            }
            else
            {
                var r = GetComponent<Renderer>();
                if (r != null) targets.Add(new Target(r, SubMeshCount(r)));
            }
        }

        /// <summary>Nearest OutlineControl at or above the renderer, so nested components never fight over the
        /// same renderer. Disabled ones still claim ownership — they mean "not outlined".</summary>
        static OutlineControl OwnerOf(Renderer r)
        {
            for (var t = r.transform; t != null; t = t.parent)
            {
                var c = t.GetComponent<OutlineControl>();
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>Real submesh count from the mesh, not the material slot count — extra material slots
        /// re-draw the last submesh, and drawing past the mesh's submesh range is invalid.</summary>
        static int SubMeshCount(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr)
                return smr.sharedMesh != null ? Mathf.Max(1, smr.sharedMesh.subMeshCount) : 1;

            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                return Mathf.Max(1, mf.sharedMesh.subMeshCount);

            return 1;
        }
    }
}
