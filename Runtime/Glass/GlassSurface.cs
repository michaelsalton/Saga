using System.Collections.Generic;
using UnityEngine;

namespace Saga.Rendering
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GlassSurface : MonoBehaviour
    {
        public readonly struct Target
        {
            public readonly Renderer renderer;
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
            ActiveList.Remove(this);
            targets.Clear();
        }

        void OnValidate() { if (isActiveAndEnabled) Refresh(); }

        void OnTransformChildrenChanged() { if (isActiveAndEnabled && includeChildRenderers) Refresh(); }

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

        static GlassSurface OwnerOf(Renderer r)
        {
            for (var t = r.transform; t != null; t = t.parent)
            {
                var c = t.GetComponent<GlassSurface>();
                if (c != null) return c;
            }
            return null;
        }

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
