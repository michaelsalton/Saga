using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GeneratePlane : MonoBehaviour
{
    [Header("Size")]
    [Min(0.1f)] public float size = 50f;

    [Header("Subdivision")]
    [Tooltip("Segments per side. Also the resolution the hole outlines are sampled at — a hole edge " +
             "can never be finer than one quad.")]
    [Min(1)] public int resolution = 200;

    [Header("UVs")]
    [Tooltip("World metres per texture repeat on uv0. ABSOLUTE, so texture density survives a change " +
             "to size or resolution — unlike a repeats-across-the-plane count. Keep the material's " +
             "Tiling at 1,1 or it multiplies on top of this (which is the way to get non-square " +
             "tiling if you ever need it). uv1 is always a global 0..1 across the whole plane.")]
    [Min(0.0001f)] public float metresPerTile = 2f;

    [Header("Holes — traced from scene geometry")]
    [Tooltip("Colliders on these layers punch a hole matching their TOP-DOWN SILHOUETTE, whatever " +
             "shape that is. Set to Nothing to disable. Objects need a COLLIDER — tick Generate " +
             "Colliders on an imported model, or add one by hand. Put cutters on their own layer: if " +
             "this mask ever includes a collider on the plane itself, the plane erases itself.")]
    public LayerMask holeMask = 0;

    [Tooltip("How far above the plane the downward probe starts (and twice this far it travels). " +
             "Must clear the tallest cutter.")]
    public float probeHeight = 50f;

    [Tooltip("Corners of a quad that must be over an object before the quad is dropped. 4 = the hole " +
             "is one quad SMALLER than the silhouette, so the plane tucks under the object's lip and " +
             "hides the stair-stepped cut edge. 1 = one quad LARGER, leaving a visible gap.")]
    [Range(1, 4)] public int cornersToCut = 4;

    [Header("Holes — manual boxes")]
    [Tooltip("Extra footprints removed regardless of colliders. Use empty GameObjects scaled to the " +
             "area you want gone. Rotation and non-uniform scale are respected.")]
    public Transform[] holes;

    [Tooltip("World metres. Grows (+) or shrinks (-) every manual box.")]
    public float holePadding = 0f;

    // A manual cutter is a unit cube under its own transform, so testing reduces to converting the
    // world point into cutter-local space and comparing half-extents. Rotation and non-uniform scale
    // come along for free in the matrix.
    struct Cutter
    {
        public Matrix4x4 toLocal;
        public float halfX, halfZ;   // local half-extents, padding already folded in
    }

    void Start() => Generate();

    [ContextMenu("Generate")]
    public void Generate()
    {
        int n = resolution + 1;
        float step = size / resolution;
        Vector3 origin = new Vector3(-size * 0.5f, 0f, -size * 0.5f);

        var verts   = new Vector3[n * n];
        var uv0     = new Vector2[n * n];   // tiling, in metres/repeat
        var uv1     = new Vector2[n * n];   // global 0..1 across the whole plane
        var normals = new Vector3[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;

                // Object-space metres from the plane's corner, so the tiling is phase-locked to the
                // mesh and does not slide when the plane is moved. Note it does NOT compensate for
                // transform scale — scaling the object stretches the texture with it.
                float mx = x * step;
                float mz = y * step;

                verts[i]   = origin + new Vector3(mx, 0f, mz);
                uv0[i]     = new Vector2(mx, mz) / metresPerTile;
                uv1[i]     = new Vector2(x / (float)resolution, y / (float)resolution);
                normals[i] = Vector3.up;   // flat plane: RecalculateNormals would only rediscover +y,
                                           // and would leave hole-orphaned verts with a zero normal
            }
        }

        bool[] blocked = ProbeGeometry(verts, n);
        var cutters = BuildCutters();
        bool hasCutters = cutters.Count > 0;

        // Holes make the triangle count data-dependent, so collect rather than index a fixed array.
        // Vertices are left in place: an unreferenced vert costs memory but keeps the index maths
        // identical to the hole-free case.
        var tris = new List<int>(resolution * resolution * 6);
        int dropped = 0;

        // CW winding viewed from +y so the top face is front-facing (normal +y).
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = y * n + x;

                if (blocked != null)
                {
                    // Count corners standing over geometry. Thresholding here rather than at the
                    // probe is what turns a silhouette into an ERODED or DILATED silhouette.
                    int covered = (blocked[i]         ? 1 : 0) + (blocked[i + 1]         ? 1 : 0)
                                + (blocked[i + n]     ? 1 : 0) + (blocked[i + n + 1]     ? 1 : 0);
                    if (covered >= cornersToCut) { dropped++; continue; }
                }

                if (hasCutters)
                {
                    // Quad CENTRE in WORLD space — the grid is object space, cutters are world volumes.
                    Vector3 centre = transform.TransformPoint(
                        origin + new Vector3((x + 0.5f) * step, 0f, (y + 0.5f) * step));

                    if (IsCut(cutters, centre)) { dropped++; continue; }
                }

                tris.Add(i);
                tris.Add(i + n);
                tris.Add(i + 1);
                tris.Add(i + 1);
                tris.Add(i + n);
                tris.Add(i + n + 1);
            }
        }

        // Reuse the existing mesh instead of orphaning it — regenerating while dialling in hole
        // placement would otherwise leak one full mesh per press. The name check keeps this from ever
        // clearing an imported mesh asset if the filter is pointed somewhere unexpected.
        var filter = GetComponent<MeshFilter>();
        var mesh = filter.sharedMesh;
        if (mesh == null || mesh.name != "GeneratePlane")
        {
            mesh = new Mesh { name = "GeneratePlane" };
            filter.sharedMesh = mesh;
        }
        else mesh.Clear();

        if (verts.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        // Report it: "the hole didn't appear" is almost always a zero here, which points at the layer
        // mask or a missing collider rather than at the geometry.
        Debug.Log($"[{nameof(GeneratePlane)}] {resolution * resolution - dropped} quad(s) kept, " +
                  $"{dropped} removed by holes.", this);
    }

    // One downward probe per GRID VERTEX — corners are shared between quads, so this is ~4x fewer
    // raycasts than probing each quad's corners, and it is what makes cornersToCut cheap. The result
    // is the object's TOP-DOWN SILHOUETTE, which is the correct footprint for a hole no matter how
    // concave, rotated, or multi-part the geometry is.
    bool[] ProbeGeometry(Vector3[] verts, int n)
    {
        if (holeMask.value == 0) return null;

        var blocked = new bool[n * n];
        float distance = probeHeight * 2f;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 world = transform.TransformPoint(verts[i]);
            blocked[i] = Physics.Raycast(world + Vector3.up * probeHeight, Vector3.down,
                                         distance, holeMask, QueryTriggerInteraction.Ignore);
        }
        return blocked;
    }

    List<Cutter> BuildCutters()
    {
        var list = new List<Cutter>();
        if (holes == null) return list;

        foreach (var h in holes)
        {
            if (h == null) continue;
            Vector3 s = h.lossyScale;

            // Local half-extent of a unit cube is 0.5. holePadding is authored as a WORLD distance,
            // so divide it by that axis' scale to land in the same local units.
            list.Add(new Cutter
            {
                toLocal = h.worldToLocalMatrix,
                halfX = 0.5f + holePadding / Mathf.Max(Mathf.Abs(s.x), 1e-4f),
                halfZ = 0.5f + holePadding / Mathf.Max(Mathf.Abs(s.z), 1e-4f),
            });
        }
        return list;
    }

    static bool IsCut(List<Cutter> cutters, Vector3 worldPoint)
    {
        for (int i = 0; i < cutters.Count; i++)
        {
            Vector3 p = cutters[i].toLocal.MultiplyPoint3x4(worldPoint);

            // Y IS IGNORED ON PURPOSE. This plane sits at y=0 while the thing being cut around (the
            // pool) sits metres below it, so a 3D containment test would never fire. A hole is a
            // FOOTPRINT, not a volume.
            if (Mathf.Abs(p.x) <= cutters[i].halfX && Mathf.Abs(p.z) <= cutters[i].halfZ)
                return true;
        }
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (holes == null) return;

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
        foreach (var h in holes)
        {
            if (h == null) continue;
            Vector3 s = h.lossyScale;

            // Draw the PADDED footprint, so what you see is what actually gets removed.
            Gizmos.matrix = h.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(
                1f + 2f * holePadding / Mathf.Max(Mathf.Abs(s.x), 1e-4f), 1f,
                1f + 2f * holePadding / Mathf.Max(Mathf.Abs(s.z), 1e-4f)));
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
