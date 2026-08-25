using System.Collections.Generic;
using UnityEngine;

namespace Saga.Rendering
{
    // ------------------------------------------------------------------------------------------------
    // Opt-in marker for Saga/Glass renderers, and the registry GlassPass draws from.
    //
    // Glass has to be drawn ONE OBJECT AT A TIME. Each pane needs a copy of the camera colour that already
    // contains the panes behind it, so a grab has to be interleaved between every draw. A RendererListHandle
    // is opaque -- there is no walking it item by item -- so the draw set has to live on this side of the
    // graph as a plain list. That is the whole reason this component exists.
    //
    // Same shape as OutlineControl, and the same consequence: a glass object WITHOUT this component is not
    // drawn at all.
    // ------------------------------------------------------------------------------------------------
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GlassSurface : MonoBehaviour
    {
        public readonly struct Target
        {
            public readonly Renderer renderer;

            // One material per submesh, resolved at Refresh time. CommandBuffer.DrawRenderer takes the
            // material explicitly, and Renderer.sharedMaterials returns a fresh copy on every access.
            public readonly Material[] materials;

            public Target(Renderer r, Material[] m) { renderer = r; materials = m; }
        }

        static readonly List<GlassSurface> ActiveList = new List<GlassSurface>();
        static readonly List<Renderer> RendererScratch = new List<Renderer>();
        static readonly List<Material> MaterialScratch = new List<Material>();

        public static IReadOnlyList<GlassSurface> Active => ActiveList;

        [Tooltip("Also draw renderers on child GameObjects — glass props usually keep their MeshRenderer on " +
                 "a child. A child carrying its own GlassSurface is left to that component.")]
        public bool includeChildRenderers = true;

        readonly List<Target> targets = new List<Target>();

        public IReadOnlyList<Target> Targets => targets;

        void OnEnable()
        {
            if (!ActiveList.Contains(this)) ActiveList.Add(this);
            Refresh();
        }

        void OnDisable()
        {
            // Leaving the registry is the entire teardown. No GPU state is cached anywhere, so the object
            // stops being drawn on the very next frame.
            ActiveList.Remove(this);
            targets.Clear();
        }

        void OnValidate() { if (isActiveAndEnabled) Refresh(); }

        void OnTransformChildrenChanged() { if (isActiveAndEnabled && includeChildRenderers) Refresh(); }

        // Re-resolve the renderers this component owns and their per-submesh materials. Needed after a child
        // hierarchy change AND after a material swap, since the materials are cached here.
        public void Refresh()
        {
            targets.Clear();

            if (includeChildRenderers)
            {
                GetComponentsInChildren(true, RendererScratch);
                for (int i = 0; i < RendererScratch.Count; i++)
                {
                    var r = RendererScratch[i];
                    if (r != null && OwnerOf(r) == this)
                        targets.Add(new Target(r, MaterialsOf(r)));
                }
                RendererScratch.Clear();
            }
            else
            {
                var r = GetComponent<Renderer>();
                if (r != null) targets.Add(new Target(r, MaterialsOf(r)));
            }
        }

        // Nearest GlassSurface at or above the renderer, so nested components never fight over one renderer.
        static GlassSurface OwnerOf(Renderer r)
        {
            for (var t = r.transform; t != null; t = t.parent)
            {
                var c = t.GetComponent<GlassSurface>();
                if (c != null) return c;
            }
            return null;
        }

        // Clamped to the mesh's real submesh count, not the material slot count. Extra material slots re-draw
        // the last submesh, and asking DrawRenderer for a submesh the mesh does not have is invalid.
        static Material[] MaterialsOf(Renderer r)
        {
            MaterialScratch.Clear();
            r.GetSharedMaterials(MaterialScratch);

            int count = Mathf.Min(MaterialScratch.Count, SubMeshCount(r));
            var materials = new Material[Mathf.Max(0, count)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = MaterialScratch[i];

            MaterialScratch.Clear();
            return materials;
        }

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
