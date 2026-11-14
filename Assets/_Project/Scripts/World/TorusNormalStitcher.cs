using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Averages normals of vertices lying on borders of segment meshes (between segments and on the
    /// seam around the tube), because every mesh calculates normals of its own border vertices alone.
    /// </summary>
    public static class TorusNormalStitcher
    {
        public static void Stitch(IReadOnlyList<Mesh> meshes, int sidesResolution)
        {
            int columns = sidesResolution + 1;
            int segmentCount = meshes.Count;

            var allNormals = new Vector3[segmentCount][];
            var sums = new Dictionary<(int ring, int column), Vector3>();

            for (int segment = 0; segment < segmentCount; segment++)
            {
                allNormals[segment] = meshes[segment].normals;
                int rows = meshes[segment].vertexCount / columns;

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        if (IsBorder(row, column, rows, columns) == false)
                            continue;

                        var key = GetKey(segment, row, column, rows, segmentCount, sidesResolution);
                        sums.TryGetValue(key, out var sum);
                        sums[key] = sum + allNormals[segment][row * columns + column];
                    }
                }
            }

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int rows = meshes[segment].vertexCount / columns;

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        if (IsBorder(row, column, rows, columns) == false)
                            continue;

                        var key = GetKey(segment, row, column, rows, segmentCount, sidesResolution);
                        allNormals[segment][row * columns + column] = sums[key].normalized;
                    }
                }

                meshes[segment].normals = allNormals[segment];
            }
        }

        private static bool IsBorder(int row, int column, int rows, int columns)
        {
            return row == 0 || row == rows - 1 || column == 0 || column == columns - 1;
        }

        /// <summary>Vertices with the same key lie in the same place of the torus.</summary>
        private static (int ring, int column) GetKey(int segment, int row, int column, int rows, int segmentCount, int sidesResolution)
        {
            return (GetRingId(segment, row, rows, segmentCount), column % sidesResolution);
        }

        private static int GetRingId(int segment, int row, int rows, int segmentCount)
        {
            if (row == 0)
                return segment;

            if (row == rows - 1)
                return (segment + 1) % segmentCount;

            return segmentCount + segment * rows + row;
        }
    }
}
