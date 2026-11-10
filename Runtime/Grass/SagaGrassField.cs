using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saga.Rendering
{
    // draws a field of procedural blades with one instanced call; blade shape is evaluated in Shaders/Grass/GrassBlade.hlsl
    [ExecuteAlways]
    // after default-order components, so ground built in their OnEnable (SagaTerrain's chunks) has colliders by the time blades raycast onto it
    [DefaultExecutionOrder(100)]
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
        [Tooltip("Optional. When set, the footprint fits this object's renderer bounds. Field Size is ignored.")]
        [SerializeField] GameObject ground;

        [Tooltip("Footprint in metres on world XZ, centred on this transform. Scale and rotation are ignored. Unused when Ground is set.")]
        [SerializeField] Vector2 fieldSize = new Vector2(20f, 20f);

        [Tooltip("Metres between blades. One candidate blade per cell, jittered inside it.")]
        [Range(0.03f, 1f)]
        [SerializeField] float cellSize = 0.1f;

        [SerializeField] int seed = 12345;

        [Tooltip("Physics layers blades root on. Each blade raycasts straight down onto them, so leave out cars, props and anything else grass shouldn't grow on top of. A blade whose ray hits nothing isn't created.")]
        [SerializeField] LayerMask groundLayers = ~0;

        [Tooltip("Metres above the footprint's centre the downward raycasts start. Must clear the highest ground in the field.")]
        [Min(0.1f)]
        [SerializeField] float castHeight = 100f;

        [Header("Painting")]
        [Tooltip("Optional. Stretched over the footprint: white grows grass, black grows none. Paint it with the Paint Grass tool in the Scene view toolbar. Empty means grass everywhere. Needs Read/Write.")]
        [SerializeField] Texture2D densityMask;

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
        float rootMinHeight;
        float rootMaxHeight;
        readonly Plane[] frustumPlanes = new Plane[6];
        readonly Vector4[] planeVectors = new Vector4[6];

        public Texture2D DensityMask
        {
            get => densityMask;
            set => densityMask = value;
        }

        public LayerMask GroundLayers => groundLayers;

        // y of the centre is where the downward raycasts are measured from
        public void GetFootprint(out Vector3 center, out Vector2 size) => ComputeFootprint(out center, out size);

        void OnEnable() => Build();
        void OnDisable() => Release();

        // rebuild when an inspector value changes; deferred because buffers can't be created inside OnValidate
        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
        }

        // also needed after regenerating the ground, since nothing tells the field its ground moved
        [ContextMenu("Rebuild")]
        public void Rebuild() => Build();

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
            Vector3 corner = footprintCenter - new Vector3(footprintSize.x, 0f, footprintSize.y) * 0.5f;
            Color32[] maskPixels = ReadMask();
            var candidates = new List<GrassBlade>();

            // same seed, same field; without the reset every rebuild would reshuffle the layout
            Random.State previousState = Random.state;
            Random.InitState(seed);
            for (int i = 0; i < cellsX; i++)
            for (int j = 0; j < cellsZ; j++)
            {
                // random spot inside the cell, so rows don't line up into visible lanes
                Vector3 offset = new Vector3((i + Random.value) * cellSize, 0f, (j + Random.value) * cellSize);
                float hash = Random.value;
                float facingAngle = Random.value * Mathf.PI * 2f;
                float bend = Random.Range(minBend, maxBend);
                // drawn for every cell, kept or not, so painting never reshuffles the blades that stay
                float densityRoll = Random.value;

                Vector3 rootPosition = corner + offset;
                // the roll is separate from hash, which sets height; thinning by hash would remove the tallest blades first
                if (densityRoll >= SampleDensity(maskPixels, corner, rootPosition)) continue;

                candidates.Add(new GrassBlade
                {
                    rootPosition = rootPosition,
                    facingAngle = facingAngle,
                    height = Mathf.Lerp(minHeight, maxHeight, hash),
                    width = bladeWidth,
                    bend = bend,
                    hash = hash,
                });
            }
            Random.state = previousState;

            List<GrassBlade> blades = RootOnGround(candidates);
            bladeCount = blades.Count;
            // a GraphicsBuffer can't be empty; with no buffer Update draws nothing
            if (bladeCount == 0) return;

            bladeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bladeCount, BladeStride);
            bladeBuffer.SetData(blades);

            // worst case every blade survives
            visibleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, bladeCount, sizeof(uint));
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            // instanceCount is left at zero; CopyCount fills it every frame
            argsBuffer.SetData(new[] { new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = (uint)indexBuffer.count } });
            if (cullShader != null) cullKernel = cullShader.FindKernel("CullBlades");
        }

        // drops each blade straight down onto the ground layers; blades whose ray hits nothing are discarded
        List<GrassBlade> RootOnGround(List<GrassBlade> candidates)
        {
            var blades = new List<GrassBlade>(candidates.Count);
            rootMinHeight = footprintCenter.y;
            rootMaxHeight = footprintCenter.y;
            if (candidates.Count == 0) return blades;

            // colliders moved since the last physics step aren't where queries see them until synced
            Physics.SyncTransforms();
            float castTop = footprintCenter.y + castHeight;
            var query = new QueryParameters(groundLayers, false, QueryTriggerInteraction.Ignore, false);
            // filled as a managed array first: a using local is read-only, and writing a NativeArray's indexer counts as modifying the struct
            var commandList = new RaycastCommand[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 rootPosition = candidates[i].rootPosition;
                commandList[i] = new RaycastCommand(new Vector3(rootPosition.x, castTop, rootPosition.z), Vector3.down, query, castHeight * 2f);
            }
            using var commands = new NativeArray<RaycastCommand>(commandList, Allocator.TempJob);
            using var hits = new NativeArray<RaycastHit>(candidates.Count, Allocator.TempJob);
            // one batched job spread across worker threads; a Physics.Raycast per blade would stall the editor for seconds
            RaycastCommand.ScheduleBatch(commands, hits, 256, 1).Complete();

            rootMinHeight = float.MaxValue;
            rootMaxHeight = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (hits[i].collider == null) continue;
                GrassBlade blade = candidates[i];
                blade.rootPosition.y = hits[i].point.y;
                rootMinHeight = Mathf.Min(rootMinHeight, blade.rootPosition.y);
                rootMaxHeight = Mathf.Max(rootMaxHeight, blade.rootPosition.y);
                blades.Add(blade);
            }
            if (blades.Count == 0)
            {
                rootMinHeight = footprintCenter.y;
                rootMaxHeight = footprintCenter.y;
            }
            return blades;
        }

        Color32[] ReadMask()
        {
            if (densityMask == null) return null;
            if (!densityMask.isReadable)
            {
                Debug.LogWarning($"[{nameof(SagaGrassField)}] Density mask {densityMask.name} needs Read/Write enabled; ignoring it.", this);
                return null;
            }
            return densityMask.GetPixels32();
        }

        // bilinear over the mask's red channel, so blade density ramps smoothly across a texel instead of in square steps
        float SampleDensity(Color32[] maskPixels, Vector3 corner, Vector3 position)
        {
            if (maskPixels == null) return 1f;
            int maskWidth = densityMask.width;
            int maskHeight = densityMask.height;

            // texel centres sit half a texel in from the footprint's edge
            float texelX = (position.x - corner.x) / footprintSize.x * maskWidth - 0.5f;
            float texelZ = (position.z - corner.z) / footprintSize.y * maskHeight - 0.5f;
            int leftColumn = Mathf.Clamp(Mathf.FloorToInt(texelX), 0, maskWidth - 1);
            int bottomRow = Mathf.Clamp(Mathf.FloorToInt(texelZ), 0, maskHeight - 1);
            int rightColumn = Mathf.Min(leftColumn + 1, maskWidth - 1);
            int topRow = Mathf.Min(bottomRow + 1, maskHeight - 1);
            float blendX = Mathf.Clamp01(texelX - leftColumn);
            float blendZ = Mathf.Clamp01(texelZ - bottomRow);

            float bottom = Mathf.Lerp(maskPixels[bottomRow * maskWidth + leftColumn].r, maskPixels[bottomRow * maskWidth + rightColumn].r, blendX);
            float top = Mathf.Lerp(maskPixels[topRow * maskWidth + leftColumn].r, maskPixels[topRow * maskWidth + rightColumn].r, blendX);
            return Mathf.Lerp(bottom, top, blendZ) / 255f;
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

        // spans the lowest root to the highest tip; wind keeps the tip at its full distance from the root, so it can reach this far up or sideways
        Bounds GetDrawBounds()
        {
            float reach = maxHeight * Mathf.Sqrt(1f + maxBend * maxBend);
            float overhang = reach + bladeWidth;
            float top = rootMaxHeight + reach;
            return new Bounds(
                new Vector3(footprintCenter.x, (rootMinHeight + top) * 0.5f, footprintCenter.z),
                new Vector3(footprintSize.x + overhang * 2f, top - rootMinHeight, footprintSize.y + overhang * 2f));
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
                worldBounds = GetDrawBounds(),
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

            // the box the whole field is culled against before the compute pass runs, from the last build
            if (bladeBuffer == null) return;
            Bounds drawBounds = GetDrawBounds();
            Gizmos.color = new Color(0.4f, 0.9f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(drawBounds.center, drawBounds.size);
        }
    }
}
