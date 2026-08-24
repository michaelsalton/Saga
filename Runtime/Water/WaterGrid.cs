using System.Collections.Generic;
using UnityEngine;

namespace Saga.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Saga/Rendering/Water Grid")]

    public class WaterGrid : MonoBehaviour
    {
        [Header("World Size")]
        [Tooltip("World-space extent (both X and Z). Mesh is centered on the origin")]
        [Min(0.01f)] public float size = 50f;

        [Header("Subdivision")]
        [Tooltip("Quads along X. More = smoother vertex waves")]
        [Min(1)] public int xTiles = 50;
        [Tooltip("Quads along Z")]
        [Min(1)] public int zTiles = 50;

        [Header("UV tiling (uv0)")]
        [Tooltip("How many times the tiling UV set repeats across the grid on X")]
        [Min(0.0001f)] public float texTileX = 5f;
        [Tooltip("How many times the tiling UV set repeats across the grid on Z")]
        [Min(0.0001f)] public float texTileZ = 5f;

        void Start() => Generate();

        [ContextMenu("Generate")]

        public void Generate()
        {
            int rowVerts = xTiles + 1;
            int columnVerts = zTiles + 1;
            int vertCount = rowVerts * columnVerts;

            float stepX = size / xTiles;
            float stepZ = size / zTiles;

            var origin = new Vector3(-size * 0.5f, 0f, -size * 0.5f);

            var verts = new Vector3[vertCount];
            var uv0 = new Vector2[vertCount]; // tiling uv
            var uv1 = new Vector2[vertCount]; // global uv
            var tris = new int[xTiles * zTiles * 6];

            for (int z = 0; z < columnVerts; z++)
            {
                for (int x = 0; x < rowVerts; x++)
                {
                    int i = z * rowVerts + x;
                    float u = x / (float)xTiles;
                    float v = z / (float)zTiles;

                    verts[i] = origin + new Vector3(x * stepX, 0f, z * stepZ);
                    uv0[i] = new Vector2(u * texTileX, v * texTileZ);
                    uv1[i] = new Vector2(u,v);
                }
            }

            int t = 0;

            for (int z = 0; z < zTiles; z++)
            {
                for (int x = 0; x < xTiles; x++)
                {
                    int i = z * rowVerts + x;
                    tris[t++] = i;
                    tris[t++] = i + rowVerts;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + rowVerts;
                    tris[t++] = i + rowVerts + 1;
                }
            }

            var mesh = new Mesh { name = "WaterGrid" };

            if (vertCount > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

    }
}
