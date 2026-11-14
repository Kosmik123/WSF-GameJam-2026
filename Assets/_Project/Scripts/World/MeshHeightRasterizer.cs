using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Projects a mesh from above (along Y) onto a rectangular grid of heights and vertex colors.</summary>
    public static class MeshHeightRasterizer
    {
        private const float Epsilon = 1e-4f;

        /// <summary>
        /// The grid covers the XZ bounds of the mesh (grid x = mesh X, grid y = mesh Z),
        /// nodes lie on the bounds edges. Where triangles overlap, the highest one wins.
        /// Cells not covered by any triangle take the average of their neighbours.
        /// Row-major: index = z * resolutionX + x.
        /// </summary>
        /// <param name="resolutionX">Grid nodes along mesh X (along the segment).</param>
        /// <param name="resolutionZ">Grid nodes along mesh Z (around the tube).</param>
        /// <param name="colors">Vertex colors interpolated at the grid nodes, or null if the mesh has none.</param>
        public static bool TryRasterize(Mesh mesh, int resolutionX, int resolutionZ, out float[] heights, out Color[] colors)
        {
            heights = null;
            colors = null;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            if (vertices.Length == 0 || triangles.Length < 3)
                return false;

            var vertexColors = mesh.colors;
            var hasColors = vertexColors.Length == vertices.Length;

            GetBounds(vertices, out float minX, out float maxX, out float minZ, out float maxZ);
            float sizeX = maxX - minX;
            float sizeZ = maxZ - minZ;
            if (sizeX < Epsilon || sizeZ < Epsilon)
                return false;

            float scaleX = (resolutionX - 1) / sizeX;
            float scaleZ = (resolutionZ - 1) / sizeZ;
            var resultHeights = new float[resolutionX * resolutionZ];
            var resultColors = hasColors ? new Color[resolutionX * resolutionZ] : null;
            var covered = new bool[resolutionX * resolutionZ];

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
                int xEnd = Mathf.Min(resolutionX - 1, Mathf.FloorToInt(Mathf.Max(ax, bx, cx)));
                int zStart = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(az, bz, cz)));
                int zEnd = Mathf.Min(resolutionZ - 1, Mathf.FloorToInt(Mathf.Max(az, bz, cz)));

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
                        int index = z * resolutionX + x;
                        if (covered[index] && height <= resultHeights[index])
                            continue;

                        resultHeights[index] = height;
                        covered[index] = true;
                        if (hasColors)
                            resultColors[index] = weightA * vertexColors[triangles[t]] + weightB * vertexColors[triangles[t + 1]] + weightC * vertexColors[triangles[t + 2]];
                    }
                }
            }

            FillGaps(resultHeights, resultColors, covered, resolutionX, resolutionZ);
            heights = resultHeights;
            colors = resultColors;
            return true;
        }

        /// <summary>
        /// Counts the distinct X and Z positions of the vertices. For a mesh that is a regular grid of vertices
        /// these are the numbers of its columns (along X) and rows (along Z).
        /// </summary>
        public static bool TryGetVertexGridSize(Mesh mesh, out int columnsX, out int rowsZ)
        {
            columnsX = 0;
            rowsZ = 0;
            var vertices = mesh.vertices;
            if (vertices.Length == 0)
                return false;

            GetBounds(vertices, out float minX, out float maxX, out float minZ, out float maxZ);
            float toleranceX = Mathf.Max(Epsilon, (maxX - minX) * 1e-3f);
            float toleranceZ = Mathf.Max(Epsilon, (maxZ - minZ) * 1e-3f);

            var distinctX = new HashSet<int>();
            var distinctZ = new HashSet<int>();
            foreach (var vertex in vertices)
            {
                distinctX.Add(Mathf.RoundToInt((vertex.x - minX) / toleranceX));
                distinctZ.Add(Mathf.RoundToInt((vertex.z - minZ) / toleranceZ));
            }

            columnsX = distinctX.Count;
            rowsZ = distinctZ.Count;
            return true;
        }

        private static void GetBounds(Vector3[] vertices, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;
            foreach (var vertex in vertices)
            {
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minZ = Mathf.Min(minZ, vertex.z);
                maxZ = Mathf.Max(maxZ, vertex.z);
            }
        }

        private static void FillGaps(float[] heights, Color[] colors, bool[] covered, int resolutionX, int resolutionZ)
        {
            var filledIndices = new List<int>();
            var filledHeights = new List<float>();
            var filledColors = new List<Color>();
            int passes = Mathf.Max(resolutionX, resolutionZ);

            for (int pass = 0; pass < passes; pass++)
            {
                filledIndices.Clear();
                filledHeights.Clear();
                filledColors.Clear();

                for (int z = 0; z < resolutionZ; z++)
                {
                    for (int x = 0; x < resolutionX; x++)
                    {
                        int index = z * resolutionX + x;
                        if (covered[index])
                            continue;

                        float heightSum = 0f;
                        var colorSum = Color.clear;
                        int count = 0;
                        AddNeighbour(heights, colors, covered, resolutionX, resolutionZ, x - 1, z, ref heightSum, ref colorSum, ref count);
                        AddNeighbour(heights, colors, covered, resolutionX, resolutionZ, x + 1, z, ref heightSum, ref colorSum, ref count);
                        AddNeighbour(heights, colors, covered, resolutionX, resolutionZ, x, z - 1, ref heightSum, ref colorSum, ref count);
                        AddNeighbour(heights, colors, covered, resolutionX, resolutionZ, x, z + 1, ref heightSum, ref colorSum, ref count);

                        if (count == 0)
                            continue;

                        filledIndices.Add(index);
                        filledHeights.Add(heightSum / count);
                        filledColors.Add(colorSum / count);
                    }
                }

                if (filledIndices.Count == 0)
                    return;

                // Applied after the pass, so a gap is filled from its edges inwards evenly.
                for (int i = 0; i < filledIndices.Count; i++)
                {
                    heights[filledIndices[i]] = filledHeights[i];
                    if (colors != null)
                        colors[filledIndices[i]] = filledColors[i];

                    covered[filledIndices[i]] = true;
                }
            }
        }

        private static void AddNeighbour(float[] heights, Color[] colors, bool[] covered, int resolutionX, int resolutionZ, int x, int z, ref float heightSum, ref Color colorSum, ref int count)
        {
            if (x < 0 || z < 0 || x >= resolutionX || z >= resolutionZ)
                return;

            int index = z * resolutionX + x;
            if (covered[index] == false)
                return;

            heightSum += heights[index];
            if (colors != null)
                colorSum += colors[index];

            count++;
        }
    }
}
