using System.Collections.Generic;
using UnityEngine;

namespace Saga.World
{
    public static class PoissonDisc
    {
        public static List<Vector2> Sample(Vector2 size, float radius, int attempts,
            System.Random rng, int maxSamples = 20000)
        {
            var samples = new List<Vector2>();
            if (size.x <= 0f || size.y <= 0f || radius <= 0f || rng == null) return samples;

            float cell = radius / Mathf.Sqrt(2f);
            int cols = Mathf.Max(1, Mathf.CeilToInt(size.x / cell));
            int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / cell));
            var grid = new int[cols * rows];

            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            var active = new List<int>();
            float r2 = radius * radius;

            var first = new Vector2((float)rng.NextDouble() * size.x, (float)rng.NextDouble() * size.y);
            samples.Add(first);
            active.Add(0);
            grid[Cell(first, cell, cols)] = 0;

            while (active.Count > 0 && samples.Count < maxSamples)
            {
                int slot = rng.Next(active.Count);
                Vector2 parent = samples[active[slot]];
                bool placed = false;

                for (int a = 0; a < attempts; a++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist  = radius * Mathf.Sqrt(1f + 3f * (float)rng.NextDouble());
                    Vector2 p = parent + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                    if (p.x < 0f || p.x >= size.x || p.y < 0f || p.y >= size.y) continue;
                    if (!FarEnough(p, samples, grid, cell, cols, rows, r2)) continue;

                    grid[Cell(p, cell, cols)] = samples.Count;
                    active.Add(samples.Count);
                    samples.Add(p);
                    placed = true;
                    break;
                }

                if (!placed) active.RemoveAt(slot);
            }
            return samples;
        }

        static int Cell(Vector2 p, float cell, int cols) => (int)(p.y / cell) * cols + (int)(p.x / cell);

        static bool FarEnough(Vector2 p, List<Vector2> samples, int[] grid,
                              float cell, int cols, int rows, float r2)
        {
            int cx = (int)(p.x / cell), cy = (int)(p.y / cell);

            int x0 = Mathf.Max(cx - 2, 0), x1 = Mathf.Min(cx + 2, cols - 1);
            int y0 = Mathf.Max(cy - 2, 0), y1 = Mathf.Min(cy + 2, rows - 1);

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int s = grid[y * cols + x];
                    if (s >= 0 && (samples[s] - p).sqrMagnitude < r2) return false;
                }
            return true;
        }
    }
}
