using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Saga.World
{
    // packs a Unity Terrain's layers and splatmaps into the texture arrays Saga/TerrainUnity reads, so the shader takes any number of layers in one pass
    [ExecuteAlways]
    [RequireComponent(typeof(Terrain))]
    [AddComponentMenu("Saga/World/Terrain Layer Arrays")]
    public class SagaTerrainLayerArrays : MonoBehaviour
    {
        // must match SAGA_MAX_TERRAIN_LAYERS in TerrainUnityInput.hlsl
        const int MaxLayers = 32;

        static readonly int AlbedoMapsId = Shader.PropertyToID("_LayerAlbedoMaps");
        static readonly int NormalMapsId = Shader.PropertyToID("_LayerNormalMaps");
        static readonly int MaskMapsId = Shader.PropertyToID("_LayerMaskMaps");
        static readonly int WeightsId = Shader.PropertyToID("_LayerWeights");
        static readonly int TintsId = Shader.PropertyToID("_LayerTints");
        static readonly int SettingsId = Shader.PropertyToID("_LayerSettings");
        static readonly int LayerCountId = Shader.PropertyToID("_LayerCount");
        static readonly int WeightsResolutionId = Shader.PropertyToID("_LayerWeightsResolution");

        Terrain terrain;
        MaterialPropertyBlock properties;

        Texture2DArray albedoMaps;
        Texture2DArray normalMaps;
        Texture2DArray maskMaps;
        Texture2DArray weights;

        // always full length: a property block fixes an array's size the first time it's set, so a shorter first set would cap the layer count
        readonly Vector4[] tints = new Vector4[MaxLayers];
        readonly Vector4[] settings = new Vector4[MaxLayers];

        // what the arrays were built from, so edits in the terrain inspector trigger a rebuild
        Texture[] builtSources = new Texture[0];
        int builtAlphamapCount;
        int builtAlphamapResolution;
        int layerCount;

        void OnEnable()
        {
            terrain = GetComponent<Terrain>();
            properties ??= new MaterialPropertyBlock();
            TerrainCallbacks.textureChanged += OnTerrainTextureChanged;
            Rebuild();
        }

        void OnDisable()
        {
            TerrainCallbacks.textureChanged -= OnTerrainTextureChanged;
            ReleaseArrays();
            properties.Clear();
            terrain.SetSplatMaterialPropertyBlock(properties);
        }

        // layers only change while editing; at runtime, call Rebuild after swapping them
        void Update()
        {
            if (Application.isPlaying || terrain.terrainData == null)
                return;

            if (SourcesChanged())
                Rebuild();
            else
                PushSettings();
        }

        public void Rebuild()
        {
            ReleaseArrays();
            layerCount = 0;

            TerrainData data = terrain.terrainData;
            if (data == null)
                return;

            TerrainLayer[] layers = data.terrainLayers;
            builtSources = CollectSources(layers);
            builtAlphamapCount = data.alphamapTextureCount;
            builtAlphamapResolution = data.alphamapResolution;

            int count = Mathf.Min(layers.Length, MaxLayers);
            if (layers.Length > MaxLayers)
                Debug.LogWarning($"{name}: terrain has {layers.Length} layers; Saga/TerrainUnity draws the first {MaxLayers}.", this);

            if (count == 0 || !LayersComplete(layers, count))
            {
                PushSettings();
                return;
            }

            Texture2D[] albedoSources = new Texture2D[count];
            Texture2D[] normalSources = new Texture2D[count];
            Texture2D[] maskSources = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                albedoSources[i] = layers[i].diffuseTexture;
                normalSources[i] = layers[i].normalMapTexture;
                maskSources[i] = layers[i].maskMapTexture;
            }

            albedoMaps = BuildLayerArray(albedoSources, layers, "Diffuse", true);
            normalMaps = BuildLayerArray(normalSources, layers, "Normal Map", false);
            maskMaps = BuildLayerArray(maskSources, layers, "Mask Map", false);
            if (albedoMaps == null || normalMaps == null || maskMaps == null)
            {
                ReleaseArrays();
                PushSettings();
                return;
            }

            BuildWeights(data);
            layerCount = count;
            PushSettings();
        }

        bool LayersComplete(TerrainLayer[] layers, int count)
        {
            for (int i = 0; i < count; i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null || layer.diffuseTexture == null || layer.normalMapTexture == null || layer.maskMapTexture == null)
                {
                    string layerName = layer == null ? "(empty)" : layer.name;
                    Debug.LogError($"{name}: terrain layer {i} {layerName} needs a Diffuse, a Normal Map and a Mask Map (R AO, G roughness, B height).", this);
                    return false;
                }
            }
            return true;
        }

        // CopyTexture moves raw blocks, so every slice must share the first one's size, format and mip count, and the format's sRGB flag decides how the shader decodes all of them
        Texture2DArray BuildLayerArray(Texture2D[] sources, TerrainLayer[] layers, string slotName, bool expectSrgb)
        {
            Texture2D first = sources[0];
            for (int i = 0; i < sources.Length; i++)
            {
                Texture2D source = sources[i];
                if (GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat) != expectSrgb)
                {
                    Debug.LogError($"{name}: {slotName} {source.name} on layer {layers[i].name} must be imported with sRGB {(expectSrgb ? "on" : "off")}.", this);
                    return null;
                }
                if (source.width != first.width || source.height != first.height || source.graphicsFormat != first.graphicsFormat || source.mipmapCount != first.mipmapCount)
                {
                    Debug.LogError($"{name}: {slotName} {source.name} on layer {layers[i].name} is {source.width}x{source.height} {source.graphicsFormat} with {source.mipmapCount} mips; "
                                   + $"every layer's {slotName} must match {first.name} at {first.width}x{first.height} {first.graphicsFormat} with {first.mipmapCount} mips.", this);
                    return null;
                }
            }

            TextureCreationFlags flags = first.mipmapCount > 1 ? TextureCreationFlags.MipChain : TextureCreationFlags.None;
            Texture2DArray array = new Texture2DArray(first.width, first.height, sources.Length, first.graphicsFormat, flags, first.mipmapCount)
            {
                name = $"{name} {slotName} Array",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = first.filterMode,
                anisoLevel = first.anisoLevel,
            };

            // GPU-side copy, so the sources don't need Read/Write enabled; calling Apply afterwards would upload the array's empty CPU copy over it
            for (int i = 0; i < sources.Length; i++)
                Graphics.CopyTexture(sources[i], 0, array, i);
            return array;
        }

        void BuildWeights(TerrainData data)
        {
            Texture2D[] alphamaps = data.alphamapTextures;
            int resolution = data.alphamapResolution;

            // weights are data, so the array is linear whatever the splatmaps are flagged as
            GraphicsFormat format = GraphicsFormatUtility.GetLinearFormat(alphamaps[0].graphicsFormat);
            weights = new Texture2DArray(resolution, resolution, alphamaps.Length, format, TextureCreationFlags.None, 1)
            {
                name = $"{name} Weights Array",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int i = 0; i < alphamaps.Length; i++)
                Graphics.CopyTexture(alphamaps[i], 0, 0, weights, i, 0);
        }

        // the brush rewrites the splatmaps on the GPU while painting, so only the touched rectangle is copied across
        void OnTerrainTextureChanged(Terrain changedTerrain, string textureName, RectInt texelRegion, bool synched)
        {
            if (changedTerrain != terrain || textureName != TerrainData.AlphamapTextureName || weights == null)
                return;

            TerrainData data = terrain.terrainData;
            if (data.alphamapTextureCount != builtAlphamapCount || data.alphamapResolution != builtAlphamapResolution)
            {
                Rebuild();
                return;
            }

            RectInt region = ClampToTexture(texelRegion, data.alphamapResolution);
            if (region.width <= 0 || region.height <= 0)
                return;

            Texture2D[] alphamaps = data.alphamapTextures;
            for (int i = 0; i < alphamaps.Length; i++)
                Graphics.CopyTexture(alphamaps[i], 0, 0, region.x, region.y, region.width, region.height, weights, i, 0, region.x, region.y);
        }

        static RectInt ClampToTexture(RectInt region, int resolution)
        {
            int xMin = Mathf.Clamp(region.xMin, 0, resolution);
            int yMin = Mathf.Clamp(region.yMin, 0, resolution);
            int xMax = Mathf.Clamp(region.xMax, 0, resolution);
            int yMax = Mathf.Clamp(region.yMax, 0, resolution);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        void PushSettings()
        {
            TerrainLayer[] layers = terrain.terrainData != null ? terrain.terrainData.terrainLayers : new TerrainLayer[0];
            for (int i = 0; i < layerCount; i++)
            {
                TerrainLayer layer = layers[i];
                // the Terrain Layer's colour remap max acts as a tint, as URP's terrain reads it
                tints[i] = layer.diffuseRemapMax;
                // triplanar has no single uv axis, so tile size x sets the repeat on every projection and tile offset is unused
                settings[i] = new Vector4(1f / Mathf.Max(layer.tileSize.x, 1e-4f), layer.normalScale, 0f, 0f);
            }

            terrain.GetSplatMaterialPropertyBlock(properties);
            properties.SetVectorArray(TintsId, tints);
            properties.SetVectorArray(SettingsId, settings);
            properties.SetFloat(LayerCountId, layerCount);
            if (layerCount > 0)
            {
                properties.SetTexture(AlbedoMapsId, albedoMaps);
                properties.SetTexture(NormalMapsId, normalMaps);
                properties.SetTexture(MaskMapsId, maskMaps);
                properties.SetTexture(WeightsId, weights);
                properties.SetFloat(WeightsResolutionId, builtAlphamapResolution);
            }
            terrain.SetSplatMaterialPropertyBlock(properties);
        }

        bool SourcesChanged()
        {
            TerrainData data = terrain.terrainData;
            if (data.alphamapTextureCount != builtAlphamapCount || data.alphamapResolution != builtAlphamapResolution)
                return true;

            Texture[] current = CollectSources(data.terrainLayers);
            if (current.Length != builtSources.Length)
                return true;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != builtSources[i])
                    return true;
            }
            return false;
        }

        // three entries per layer: diffuse, normal, mask
        static Texture[] CollectSources(TerrainLayer[] layers)
        {
            int count = Mathf.Min(layers.Length, MaxLayers);
            Texture[] sources = new Texture[count * 3];
            for (int i = 0; i < count; i++)
            {
                TerrainLayer layer = layers[i];
                sources[i * 3] = layer != null ? layer.diffuseTexture : null;
                sources[i * 3 + 1] = layer != null ? layer.normalMapTexture : null;
                sources[i * 3 + 2] = layer != null ? layer.maskMapTexture : null;
            }
            return sources;
        }

        void ReleaseArrays()
        {
            DestroyArray(ref albedoMaps);
            DestroyArray(ref normalMaps);
            DestroyArray(ref maskMaps);
            DestroyArray(ref weights);
        }

        static void DestroyArray(ref Texture2DArray array)
        {
            if (array == null)
                return;
            if (Application.isPlaying)
                Destroy(array);
            else
                DestroyImmediate(array);
            array = null;
        }
    }
}
