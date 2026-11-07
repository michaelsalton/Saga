using UnityEngine;
using UnityEngine.Rendering;

namespace Saga.Rendering
{
    // draws a field of procedural blades with one instanced call; blade shape is evaluated in Shaders/Grass/GrassBlade.hlsl
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Grass Field")]
    public class SagaGrassField : MonoBehaviour
    {
        struct GrassBlade
        {
            public Vector3 rootPosition;
            public float facingAngle;
            public float height;
            public float width;
            public float bend;
            public float hash;
        }

        // eight floats; must match the HLSL struct byte for byte
        const int BladeStride = 32;

        [SerializeField] Material material;
        [SerializeField] ComputeShader cullShader;

        [Header("Field")]
        [Tooltip("Optional. When set, the footprint fits this object's renderer bounds and blades root on its top. Field Size is ignored.")]
        [SerializeField] GameObject ground;

        [Tooltip("Footprint in metres on world XZ, centred on this transform. Scale and rotation are ignored. Unused when Ground is set.")]
        [SerializeField] Vector2 fieldSize = new Vector2(20f, 20f);

        [Tooltip("Metres between blades. One blade per cell, jittered inside it.")]
        [Range(0.03f, 1f)]
        [SerializeField] float cellSize = 0.1f;

        [SerializeField] int seed = 12345;

        [Header("Blade")]
        [Range(1, 15)]
        [SerializeField] int segmentCount = 4;

        [SerializeField] float minHeight = 0.3f;
        [SerializeField] float maxHeight = 0.6f;
        [SerializeField] float bladeWidth = 0.04f;

        [Range(0f, 1f)]
        [SerializeField] float minBend = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] float maxBend = 0.5f;

        static readonly int BladesID = Shader.PropertyToID("_Blades");
        static readonly int SegmentCountID = Shader.PropertyToID("_SegmentCount");
        static readonly int VisibleBladeIndicesID = Shader.PropertyToID("_VisibleBladeIndices");
        static readonly int VisibleBladeIndicesOutID = Shader.PropertyToID("_VisibleBladeIndicesOut");
        static readonly int FrustumPlanesID = Shader.PropertyToID("_FrustumPlanes");
        static readonly int BladeCountID = Shader.PropertyToID("_BladeCount");

        GraphicsBuffer bladeBuffer;
        GraphicsBuffer indexBuffer;
        GraphicsBuffer visibleBuffer;
        GraphicsBuffer argsBuffer;
        MaterialPropertyBlock properties;
        int bladeCount;
        int cullKernel;
        Vector3 footprintCenter;
        Vector2 footprintSize;
        readonly Plane[] frustumPlanes = new Plane[6];
        readonly Vector4[] planeVectors = new Vector4[6];

        void OnEnable() => Build();
        void OnDisable() => Release();

        // rebuild when an inspector value changes; deferred because buffers can't be created inside OnValidate
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
        }

        void Build()
        {
            Release();
            properties ??= new MaterialPropertyBlock();

            int[] indices = new int[segmentCount * 6];
            for (int segment = 0; segment < segmentCount; segment++)
            {
                int bottomLeft = segment * 2;
                int index = segment * 6;
                indices[index + 0] = bottomLeft;
                indices[index + 1] = bottomLeft + 1;
                indices[index + 2] = bottomLeft + 2;
                indices[index + 3] = bottomLeft + 1;
                indices[index + 4] = bottomLeft + 3;
                indices[index + 5] = bottomLeft + 2;
            }
            indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, indices.Length, sizeof(int));
            indexBuffer.SetData(indices);

            ComputeFootprint(out footprintCenter, out footprintSize);
            int cellsX = Mathf.Max(1, Mathf.FloorToInt(footprintSize.x / cellSize));
            int cellsZ = Mathf.Max(1, Mathf.FloorToInt(footprintSize.y / cellSize));
            bladeCount = cellsX * cellsZ;
            var blades = new GrassBlade[bladeCount];

            // same seed, same field; without the reset every rebuild would reshuffle the layout
            Random.State previousState = Random.state;
            Random.InitState(seed);
            Vector3 corner = footprintCenter - new Vector3(footprintSize.x, 0f, footprintSize.y) * 0.5f;
            for (int i = 0; i < cellsX; i++)
            for (int j = 0; j < cellsZ; j++)
            {
                // random spot inside the cell, so rows don't line up into visible lanes
                Vector3 offset = new Vector3((i + Random.value) * cellSize, 0f, (j + Random.value) * cellSize);
                float hash = Random.value;
                blades[i * cellsZ + j] = new GrassBlade
                {
                    rootPosition = corner + offset,
                    facingAngle = Random.value * Mathf.PI * 2f,
                    height = Mathf.Lerp(minHeight, maxHeight, hash),
                    width = bladeWidth,
                    bend = Random.Range(minBend, maxBend),
                    hash = hash,
                };
            }
            Random.state = previousState;

            bladeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bladeCount, BladeStride);
            bladeBuffer.SetData(blades);

            // worst case every blade survives
            visibleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, bladeCount, sizeof(uint));
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            // instanceCount is left at zero; CopyCount fills it every frame
            argsBuffer.SetData(new[] { new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = (uint)indexBuffer.count } });
            if (cullShader != null) cullKernel = cullShader.FindKernel("CullBlades");
        }

        void Release()
        {
            bladeBuffer?.Release();
            indexBuffer?.Release();
            visibleBuffer?.Release();
            argsBuffer?.Release();
            bladeBuffer = null;
            indexBuffer = null;
            visibleBuffer = null;
            argsBuffer = null;
        }

        // y of the centre is the height blades root at
        void ComputeFootprint(out Vector3 center, out Vector2 size)
        {
            if (ground != null && TryGetGroundBounds(out Bounds groundBounds))
            {
                center = new Vector3(groundBounds.center.x, groundBounds.max.y, groundBounds.center.z);
                size = new Vector2(groundBounds.size.x, groundBounds.size.z);
                return;
            }
            center = transform.position;
            size = fieldSize;
        }

        bool TryGetGroundBounds(out Bounds bounds)
        {
            Renderer[] renderers = ground.GetComponentsInChildren<Renderer>();
            bounds = default;
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        // wind keeps the tip at its full distance from the root, so it can reach this far up or sideways
        Bounds GetDrawBounds(Vector3 center, Vector2 size)
        {
            float reach = maxHeight * Mathf.Sqrt(1f + maxBend * maxBend);
            float overhang = reach + bladeWidth;
            return new Bounds(
                center + Vector3.up * reach * 0.5f,
                new Vector3(size.x + overhang * 2f, reach, size.y + overhang * 2f));
        }

        void Update()
        {
            Camera cullCamera = Camera.main;
            if (material == null || cullShader == null || bladeBuffer == null || cullCamera == null) return;

            GeometryUtility.CalculateFrustumPlanes(cullCamera, frustumPlanes);
            for (int i = 0; i < 6; i++)
            {
                Vector3 normal = frustumPlanes[i].normal;
                planeVectors[i] = new Vector4(normal.x, normal.y, normal.z, frustumPlanes[i].distance);
            }

            // without the reset the counter carries over from last frame and runs off the end of the buffer
            visibleBuffer.SetCounterValue(0);
            cullShader.SetBuffer(cullKernel, BladesID, bladeBuffer);
            cullShader.SetBuffer(cullKernel, VisibleBladeIndicesOutID, visibleBuffer);
            cullShader.SetVectorArray(FrustumPlanesID, planeVectors);
            cullShader.SetInt(BladeCountID, bladeCount);
            cullShader.Dispatch(cullKernel, Mathf.CeilToInt(bladeCount / 64f), 1, 1);
            // instanceCount is the second uint of the args, 4 bytes in
            GraphicsBuffer.CopyCount(visibleBuffer, argsBuffer, sizeof(uint));

            properties.SetBuffer(BladesID, bladeBuffer);
            properties.SetBuffer(VisibleBladeIndicesID, visibleBuffer);
            properties.SetFloat(SegmentCountID, segmentCount);

            // the compute pass culls per blade; these bounds still gate the whole field per camera
            var renderParams = new RenderParams(material)
            {
                worldBounds = GetDrawBounds(footprintCenter, footprintSize),
                matProps = properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
            };
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, indexBuffer, argsBuffer);
        }

        void OnDrawGizmosSelected()
        {
            // recomputed live, so it shows where the next rebuild will put the grass
            ComputeFootprint(out Vector3 center, out Vector2 size);

            Gizmos.color = new Color(0.4f, 0.9f, 0.2f);
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0f, size.y));

            // the box the whole field is culled against before the compute pass runs
            Bounds drawBounds = GetDrawBounds(center, size);
            Gizmos.color = new Color(0.4f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(drawBounds.center, drawBounds.size);
        }
    }
}
