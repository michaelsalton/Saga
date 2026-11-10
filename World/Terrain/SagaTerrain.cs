using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saga.World
{
    // builds the island as a grid of chunk meshes sampled from SagaIslandShape; chunks are rebuilt on enable and never saved with the scene
    [ExecuteAlways]
    [AddComponentMenu("Saga/World/Terrain")]
    public class SagaTerrain : MonoBehaviour
    {
        // 254 quads is 255 vertices a side, the most that fits a 16-bit index buffer
        const int MaxQuadsPerChunk = 254;

        [SerializeField] Material material;
        [SerializeField] SagaIslandShape shape = new SagaIslandShape();

        [Header("Mesh")]
        [Tooltip("Metres per side of the square generated area, centred on this transform. Should reach past the coast onto the flat seabed.")]
        [Min(1f)]
        [SerializeField] float terrainSize = 700f;

        [Tooltip("Metres per side of one chunk. Rounded to a whole number of vertex spacings and capped so a chunk fits a 16-bit index buffer. Smaller chunks cull tighter but cost more draw calls.")]
        [Min(1f)]
        [SerializeField] float chunkSize = 64f;

        [Tooltip("Metres between vertices. Keep it under half the finest octave's wavelength (Feature Size / Lacunarity^(Octaves-1)) or that octave turns into spikes.")]
        [Range(0.1f, 4f)]
        [SerializeField] float vertexSpacing = 0.5f;

        [Tooltip("World metres per texture repeat on uv0.")]
        [Min(0.0001f)]
        [SerializeField] float metresPerTile = 2f;

        [Tooltip("Cooks a MeshCollider per chunk. The slowest part of a rebuild; turn off while tuning the shape.")]
        [SerializeField] bool generateColliders = true;

        [Header("City")]
        [Tooltip("The city root. Its footprint is the combined bounds of every renderer under it, and its pivot Y is taken as street level. Rotate it in 90 degree steps relative to the terrain. Moving it doesn't regenerate; use Generate from the context menu.")]
        [SerializeField] GameObject city;

        [Tooltip("Metres of flat ground at city height around the footprint.")]
        [Min(0f)]
        [SerializeField] float shoulder = 4f;

        [Tooltip("Metres over which the flat ground blends back into natural terrain. Max slope is about 1.5 x height difference / this.")]
        [Min(0.01f)]
        [SerializeField] float blendWidth = 60f;

        [Tooltip("Metres the terrain drops under the city so it can never show through.")]
        [Min(0f)]
        [SerializeField] float sink = 0.5f;

        [Header("Layers")]
        [Tooltip("Metres above the water where sand gives way to grass. Weights go to the Saga/Terrain shader as vertex colour: R sand, G grass, B rock, A seabed.")]
        [SerializeField] float beachTop = 1.5f;

        [Tooltip("Metres over which sand fades into grass.")]
        [Min(0.01f)]
        [SerializeField] float beachBlend = 1f;

        [Tooltip("Metres relative to the water where sand gives way to seabed. Negative is underwater.")]
        [SerializeField] float seabedTop = -1.5f;

        [Tooltip("Metres over which sand fades into seabed.")]
        [Min(0.01f)]
        [SerializeField] float seabedBlend = 1.5f;

        [Tooltip("Steepness where rock starts to show: 0 is flat, 1 is a vertical wall. 0.3 is about 45 degrees.")]
        [Range(0f, 1f)]
        [SerializeField] float cliffStart = 0.2f;

        [Tooltip("Steepness over which ground fades fully into rock.")]
        [Min(0.01f)]
        [SerializeField] float cliffBlend = 0.1f;

        [Tooltip("Metres per wavelength of the noise that wobbles every layer boundary, so none of them follow a contour line.")]
        [Min(1f)]
        [SerializeField] float boundaryNoiseSize = 40f;

        [Tooltip("How far the noise moves the boundaries: this many metres for the height boundaries, a tenth of it in steepness for the cliff boundary.")]
        [Min(0f)]
        [SerializeField] float boundaryNoiseAmount = 1f;

        readonly List<GameObject> chunks = new List<GameObject>();
        float gridHalfExtent;
        bool hasCity;
        float cityGroundHeight;
        Vector2 cityMin;
        Vector2 cityMax;

        void OnEnable() => Generate();
        void OnDisable() => Release();

        // deferred because objects can't be created or destroyed inside OnValidate
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Generate(); };
#endif
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            Release();

            int quadsPerChunk = Mathf.Clamp(Mathf.RoundToInt(chunkSize / vertexSpacing), 1, MaxQuadsPerChunk);
            int chunksPerSide = Mathf.Max(1, Mathf.CeilToInt(terrainSize / (quadsPerChunk * vertexSpacing)));
            // quantised to whole chunks, so the grid can come out slightly larger than Terrain Size
            gridHalfExtent = chunksPerSide * quadsPerChunk * vertexSpacing * 0.5f;

            hasCity = city != null && ResolveCity();

            for (int chunkZ = 0; chunkZ < chunksPerSide; chunkZ++)
            for (int chunkX = 0; chunkX < chunksPerSide; chunkX++)
                chunks.Add(BuildChunk(chunkX, chunkZ, quadsPerChunk));
        }

        GameObject BuildChunk(int chunkX, int chunkZ, int quads)
        {
            int verticesPerSide = quads + 1;
            // one extra ring of samples outside the chunk, so edge normals see the neighbour's slope instead of a cliff
            int samplesPerSide = verticesPerSide + 2;
            int firstColumn = chunkX * quads;
            int firstRow = chunkZ * quads;

            var heights = new float[samplesPerSide * samplesPerSide];
            for (int row = 0; row < samplesPerSide; row++)
            for (int column = 0; column < samplesPerSide; column++)
            {
                heights[row * samplesPerSide + column] = SampleHeight(
                    GridPosition(firstColumn + column - 1, cityMin.x, cityMax.x),
                    GridPosition(firstRow + row - 1, cityMin.y, cityMax.y));
            }

            var vertices = new Vector3[verticesPerSide * verticesPerSide];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var layerWeights = new Color[vertices.Length];
            for (int row = 0; row < verticesPerSide; row++)
            for (int column = 0; column < verticesPerSide; column++)
            {
                int index = row * verticesPerSide + column;
                int sample = (row + 1) * samplesPerSide + column + 1;
                float positionX = GridPosition(firstColumn + column, cityMin.x, cityMax.x);
                float positionZ = GridPosition(firstRow + row, cityMin.y, cityMax.y);

                // the sink is applied here and not in the height grid, so vertices on the city edge don't take a tilted normal from their sunk neighbours
                float height = IsUnderCity(positionX, positionZ) ? heights[sample] - sink : heights[sample];
                vertices[index] = new Vector3(positionX, height, positionZ);
                // central differences: the cross of the X and Z tangents with their shared 2*spacing factor divided out; from the function rather than RecalculateNormals so seams match
                normals[index] = new Vector3(
                    heights[sample - 1] - heights[sample + 1],
                    2f * vertexSpacing,
                    heights[sample - samplesPerSide] - heights[sample + samplesPerSide]).normalized;
                uvs[index] = new Vector2(positionX, positionZ) / metresPerTile;
                layerWeights[index] = LayerWeights(positionX, positionZ, height, normals[index]);
            }

            var triangles = new int[quads * quads * 6];
            int triangle = 0;
            for (int row = 0; row < quads; row++)
            for (int column = 0; column < quads; column++)
            {
                int bottomLeft = row * verticesPerSide + column;
                int topLeft = bottomLeft + verticesPerSide;
                // clockwise seen from above, so the top face is the front face
                triangles[triangle++] = bottomLeft;
                triangles[triangle++] = topLeft;
                triangles[triangle++] = bottomLeft + 1;
                triangles[triangle++] = bottomLeft + 1;
                triangles[triangle++] = topLeft;
                triangles[triangle++] = topLeft + 1;
            }

            var mesh = new Mesh { name = $"SagaTerrain {chunkX},{chunkZ}", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(layerWeights);
            mesh.SetTriangles(triangles, 0);
            // normal maps and POM in Saga/Standard need tangents
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var chunk = new GameObject(mesh.name) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            chunk.transform.SetParent(transform, false);
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
            chunk.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (generateColliders) chunk.AddComponent<MeshCollider>().sharedMesh = mesh;
            return chunk;
        }

        // from the global vertex index, so a vertex shared by two chunks lands on bit-identical coordinates in both
        float GridPosition(int gridIndex, float cityEdgeLow, float cityEdgeHigh)
        {
            float position = gridIndex * vertexSpacing - gridHalfExtent;
            if (!hasCity) return position;

            // pull the nearest grid line onto each city edge so no triangle straddles the border; the half-open range means exactly one line moves per edge
            float halfCell = vertexSpacing * 0.5f;
            float fromLow = position - cityEdgeLow;
            if (fromLow > -halfCell && fromLow <= halfCell) return cityEdgeLow;
            float fromHigh = position - cityEdgeHigh;
            if (fromHigh > -halfCell && fromHigh <= halfCell) return cityEdgeHigh;
            return position;
        }

        bool ResolveCity()
        {
            Renderer[] renderers = city.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[{nameof(SagaTerrain)}] City has no renderers to take a footprint from.", this);
                return false;
            }

            // street level, in the same terrain-local space the shape is sampled in
            cityGroundHeight = transform.InverseTransformPoint(city.transform.position).y;

            // measured in terrain space rather than from Renderer.bounds, which is a world-axis box that grows when the city is rotated
            cityMin = new Vector2(float.MaxValue, float.MaxValue);
            cityMax = new Vector2(float.MinValue, float.MinValue);
            Matrix4x4 worldToTerrain = transform.worldToLocalMatrix;
            foreach (Renderer cityRenderer in renderers)
            {
                Bounds localBounds = cityRenderer.localBounds;
                Matrix4x4 rendererToTerrain = worldToTerrain * cityRenderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 cornerSign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 cornerInTerrain = rendererToTerrain.MultiplyPoint3x4(localBounds.center + Vector3.Scale(localBounds.extents, cornerSign));
                    cityMin = Vector2.Min(cityMin, new Vector2(cornerInTerrain.x, cornerInTerrain.z));
                    cityMax = Vector2.Max(cityMax, new Vector2(cornerInTerrain.x, cornerInTerrain.z));
                }
            }

            // a box is only flush with the grid when its sides run along the grid axes, so only quarter turns are exact
            float relativeYaw = Mathf.DeltaAngle(transform.eulerAngles.y, city.transform.eulerAngles.y);
            if (Mathf.Abs(Mathf.DeltaAngle(0f, relativeYaw * 4f)) > 0.01f)
                Debug.LogWarning($"[{nameof(SagaTerrain)}] City is rotated {relativeYaw:0.##} degrees relative to the terrain; its edges will cut across terrain triangles. Rotate the terrain to match it.", this);

            Vector2 citySize = cityMax - cityMin;
            Debug.Log($"[{nameof(SagaTerrain)}] City footprint {citySize.x:0.#} x {citySize.y:0.#} m at street level {cityGroundHeight:0.##} m.", this);
            return true;
        }

        // r sand, g grass, b rock, a seabed; each layer claims its share of what the layers before it left, so the four always sum to 1
        Color LayerWeights(float positionX, float positionZ, float height, Vector3 normal)
        {
            // centred on 0 so the boundaries move both ways; the offset keeps it out of step with the island's own noise near the origin
            float boundaryNoise = (Mathf.PerlinNoise(positionX / boundaryNoiseSize + 731.7f, positionZ / boundaryNoiseSize + 219.3f) - 0.5f) * 2f * boundaryNoiseAmount;
            // 0 on flat ground, 1 on a vertical wall
            float steepness = 1f - normal.y;

            float seabed = 1f - Mathf.SmoothStep(0f, 1f, (height - (seabedTop + boundaryNoise)) / seabedBlend);
            float rock = (1f - seabed) * Mathf.SmoothStep(0f, 1f, (steepness - (cliffStart + boundaryNoise * 0.1f)) / cliffBlend);
            float sandShare = 1f - Mathf.SmoothStep(0f, 1f, (height - (beachTop + boundaryNoise)) / beachBlend);
            float flatGround = 1f - seabed - rock;
            return new Color(flatGround * sandShare, flatGround * (1f - sandShare), rock, seabed);
        }

        // box signed distance in metres: negative inside, positive outside, Euclidean past the corners so the blend rounds them off
        float CitySignedDistance(float positionX, float positionZ)
        {
            float overX = Mathf.Max(cityMin.x - positionX, positionX - cityMax.x);
            float overZ = Mathf.Max(cityMin.y - positionZ, positionZ - cityMax.y);
            return new Vector2(Mathf.Max(overX, 0f), Mathf.Max(overZ, 0f)).magnitude + Mathf.Min(Mathf.Max(overX, overZ), 0f);
        }

        // the tolerance keeps vertices on the boundary flush despite float error in the transformed bounds
        bool IsUnderCity(float positionX, float positionZ) =>
            hasCity && CitySignedDistance(positionX, positionZ) < -0.01f * vertexSpacing;

        // flush at city height across the footprint; the sink is applied per vertex in BuildChunk
        float SampleHeight(float positionX, float positionZ)
        {
            float naturalHeight = shape.Height(positionX, positionZ);
            if (!hasCity) return naturalHeight;

            float signedDistance = CitySignedDistance(positionX, positionZ);
            if (signedDistance < 0f) return cityGroundHeight;

            float blend = Mathf.SmoothStep(0f, 1f, (signedDistance - shoulder) / blendWidth);
            return Mathf.Lerp(cityGroundHeight, naturalHeight, blend);
        }

        void Release()
        {
            foreach (GameObject chunk in chunks)
            {
                if (chunk == null) continue;
                DestroyObject(chunk.GetComponent<MeshFilter>().sharedMesh);
                DestroyObject(chunk);
            }
            chunks.Clear();
        }

        static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
