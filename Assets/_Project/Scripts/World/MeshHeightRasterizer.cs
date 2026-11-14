using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Projects a mesh from above (along Y) onto a square grid of heights.</summary>
    public static class MeshHeightRasterizer
    {
        private const float Epsilon = 1e-4f;

        /// <summary>
        /// The grid covers the XZ bounds of the mesh (grid x = mesh X, grid y = mesh Z),
        /// nodes lie on the bounds edges. Where triangles overlap, the highest one wins.
        /// Cells not covered by any triangle take the average of their neighbours.
        /// </summary>
        public static bool TryRasterize(Mesh mesh, int resolution, out float[] heights)
        {
            heights = null;
            var vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length == 0 || triangles.Length < 3)
                return false;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                minX = Mathf.Min(minX, vertices[i].x);
                maxX = Mathf.Max(maxX, vertices[i].x);
                minZ = Mathf.Min(minZ, vertices[i].z);
                maxZ = Mathf.Max(maxZ, vertices[i].z);
            }

            float sizeX = maxX - minX;
            float sizeZ = maxZ - minZ;
            if (sizeX < Epsilon || sizeZ < Epsilon)
                return false;

            float scaleX = (resolution - 1) / sizeX;
            float scaleZ = (resolution - 1) / sizeZ;
            var result = new float[resolution * resolution];
            var covered = new bool[resolution * resolution];

            for (int t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];

                // Triangle corners in grid space.
                float ax = (a.x - minX) * scaleX;
                float az = (a.z - minZ) * scaleZ;
                float bx = (b.x - minX) * scaleX;
                float bz = (b.z - minZ) * scaleZ;
                float cx = (c.x - minX) * scaleX;
                float cz = (c.z - minZ) * scaleZ;

                float edge1x = bx - ax;
                float edge1z = bz - az;
                float edge2x = cx - ax;
                float edge2z = cz - az;
                float denominator = edge1x * edge2z - edge2x * edge1z;
                if (Mathf.Abs(denominator) < 1e-9f)
                    continue;

                int xStart = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(ax, bx, cx)));
                int xEnd = Mathf.Min(resolution - 1, Mathf.FloorToInt(Mathf.Max(ax, bx, cx)));
                int zStart = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(az, bz, cz)));
                int zEnd = Mathf.Min(resolution - 1, Mathf.FloorToInt(Mathf.Max(az, bz, cz)));

                for (int z = zStart; z <= zEnd; z++)
                {
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        float px = x - ax;
                        float pz = z - az;
                        float weightB = (px * edge2z - edge2x * pz) / denominator;
                        float weightC = (edge1x * pz - px * edge1z) / denominator;
                        float weightA = 1f - weightB - weightC;
                        if (weightA < -Epsilon || weightB < -Epsilon || weightC < -Epsilon)
                            continue;

                        float height = weightA * a.y + weightB * b.y + weightC * c.y;
                        int index = z * resolution + x;
                        if (covered[index] == false || height > result[index])
                        {
                            result[index] = height;
                            covered[index] = true;
                        }
                    }
                }
            }

            FillGaps(result, covered, resolution);
            heights = result;
            return true;
        }

        private static void FillGaps(float[] heights, bool[] covered, int resolution)
        {
            var filledIndices = new List<int>();
            var filledHeights = new List<float>();

            for (int pass = 0; pass < resolution; pass++)
            {
                filledIndices.Clear();
                filledHeights.Clear();

                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        int index = z * resolution + x;
                        if (covered[index])
                            continue;

                        float sum = 0f;
                        int count = 0;
                        AddNeighbour(heights, covered, resolution, x - 1, z, ref sum, ref count);
                        AddNeighbour(heights, covered, resolution, x + 1, z, ref sum, ref count);
                        AddNeighbour(heights, covered, resolution, x, z - 1, ref sum, ref count);
                        AddNeighbour(heights, covered, resolution, x, z + 1, ref sum, ref count);

                        if (count == 0)
                            continue;

                        filledIndices.Add(index);
                        filledHeights.Add(sum / count);
                    }
                }

                if (filledIndices.Count == 0)
                    return;

                // Applied after the pass, so a gap is filled from its edges inwards evenly.
                for (int i = 0; i < filledIndices.Count; i++)
                {
                    heights[filledIndices[i]] = filledHeights[i];
                    covered[filledIndices[i]] = true;
                }
            }
        }

        private static void AddNeighbour(float[] heights, bool[] covered, int resolution, int x, int z, ref float sum, ref int count)
        {
            if (x < 0 || z < 0 || x >= resolution || z >= resolution)
                return;

            int index = z * resolution + x;
            if (covered[index] == false)
                return;

            sum += heights[index];
            count++;
        }
    }
}
