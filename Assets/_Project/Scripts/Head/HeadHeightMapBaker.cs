using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Bakes the top surface of the head into a height map: rows along the body, columns around the tube (phi).
    /// Where <see cref="HeadNeckProfiler"/> measures one slice at the neck, this measures the whole head, so the player can walk on it.
    /// The triangles of the meshes are sampled densely and the highest point (largest distance from the tube centre) of every cell wins.
    /// Overhangs are ignored: only the surface seen from outside counts.
    /// </summary>
    public static class HeadHeightMapBaker
    {
        private const int MaxStepsPerTriangleEdge = 256;

        /// <summary>
        /// Bakes the head as it is placed at the given theta.
        /// </summary>
        /// <param name="cellSize">Size of a cell along the body in world units. Rows are spread evenly over the head.</param>
        /// <param name="columns">Number of cells around the tube.</param>
        /// <param name="heights">Heights above the base tube, [row, column]. Row 0 is the rear end of the head.</param>
        /// <param name="headLength">Length covered by the rows, in radians of theta.</param>
        public static bool TryBake(TorusWorld world, float theta, IReadOnlyList<Renderer> renderers, float cellSize, int columns, out float[,] heights, out float headLength)
        {
            heights = null;
            headLength = 0f;

            var frame = new Frame(world, theta);
            var triangles = CollectTriangles(world, frame, renderers, out float minForward, out float maxForward);
            if (triangles.Count == 0 || maxForward - minForward <= 0f)
                return false;

            int rows = Mathf.Max(2, Mathf.CeilToInt((maxForward - minForward) / Mathf.Max(0.01f, cellSize)) + 1);
            float rowStep = (maxForward - minForward) / (rows - 1);
            var radii = new float[rows, columns];

            float sampleStep = Mathf.Min(rowStep, world.MinorRadius * TorusGeometry.TwoPi / columns) * 0.5f;
            for (int i = 0; i < triangles.Count; i += 3)
                SampleTriangle(triangles[i], triangles[i + 1], triangles[i + 2], minForward, rowStep, sampleStep, radii);

            if (FillEmptyCells(radii) == false)
                return false;

            heights = new float[rows, columns];
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    heights[row, column] = radii[row, column] - world.MinorRadius;

            headLength = (maxForward - minForward) / world.MajorRadius;
            return true;
        }

        // Frame of the tube cross-section at theta: x outward, y up, z along the body.
        private readonly struct Frame
        {
            private readonly Vector3 center;
            private readonly Vector3 outward;
            private readonly Vector3 up;
            private readonly Vector3 forward;

            public Frame(TorusWorld world, float theta)
            {
                center = TorusGeometry.GetCenterPoint(world.MajorRadius, theta);
                forward = TorusGeometry.GetThetaDirection(theta);
                outward = TorusGeometry.GetNormal(theta, 0f);
                up = TorusGeometry.GetNormal(theta, Mathf.PI * 0.5f);
            }

            public Vector3 ToFrame(Vector3 worldLocalPoint)
            {
                var local = worldLocalPoint - center;
                return new Vector3(Vector3.Dot(local, outward), Vector3.Dot(local, up), Vector3.Dot(local, forward));
            }
        }

        private static List<Vector3> CollectTriangles(TorusWorld world, Frame frame, IReadOnlyList<Renderer> renderers, out float minForward, out float maxForward)
        {
            minForward = float.MaxValue;
            maxForward = float.MinValue;

            var points = new List<Vector3>();
            var vertices = new List<Vector3>();
            var framed = new List<Vector3>();
            var indices = new List<int>();
            foreach (var renderer in renderers)
            {
                var mesh = HeadNeckProfiler.GetMesh(renderer);
                if (mesh == null)
                    continue;

                vertices.Clear();
                framed.Clear();
                mesh.GetVertices(vertices);
                foreach (var vertex in vertices)
                {
                    var point = frame.ToFrame(world.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
                    framed.Add(point);
                    minForward = Mathf.Min(minForward, point.z);
                    maxForward = Mathf.Max(maxForward, point.z);
                }

                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    indices.Clear();
                    mesh.GetIndices(indices, subMesh);
                    for (int i = 0; i + 2 < indices.Count; i += 3)
                    {
                        points.Add(framed[indices[i]]);
                        points.Add(framed[indices[i + 1]]);
                        points.Add(framed[indices[i + 2]]);
                    }
                }
            }

            return points;
        }

        private static void SampleTriangle(Vector3 a, Vector3 b, Vector3 c, float minForward, float rowStep, float sampleStep, float[,] radii)
        {
            float longestEdge = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
            int steps = Mathf.Clamp(Mathf.CeilToInt(longestEdge / Mathf.Max(0.001f, sampleStep)), 1, MaxStepsPerTriangleEdge);

            for (int i = 0; i <= steps; i++)
            {
                for (int j = 0; j <= steps - i; j++)
                {
                    float weightB = (float)i / steps;
                    float weightC = (float)j / steps;
                    var point = a + (b - a) * weightB + (c - a) * weightC;
                    Store(point, minForward, rowStep, radii);
                }
            }
        }

        private static void Store(Vector3 point, float minForward, float rowStep, float[,] radii)
        {
            int rows = radii.GetLength(0);
            int columns = radii.GetLength(1);

            int row = Mathf.Clamp(Mathf.RoundToInt((point.z - minForward) / rowStep), 0, rows - 1);
            float phi = Mathf.Atan2(point.y, point.x);
            int column = Mathf.RoundToInt(Mathf.Repeat(phi, TorusGeometry.TwoPi) / TorusGeometry.TwoPi * columns) % columns;
            float radius = Mathf.Sqrt(point.x * point.x + point.y * point.y);
            radii[row, column] = Mathf.Max(radii[row, column], radius);
        }

        /// <summary>Fills empty columns in every row by interpolating around the tube, and rows without any data by copying the nearest filled row.</summary>
        private static bool FillEmptyCells(float[,] radii)
        {
            int rows = radii.GetLength(0);
            int columns = radii.GetLength(1);

            var filledRows = new bool[rows];
            var line = new float[columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                    line[column] = radii[row, column];

                if (HeadNeckProfiler.FillEmptyBins(line) == false)
                    continue;

                filledRows[row] = true;
                for (int column = 0; column < columns; column++)
                    radii[row, column] = line[column];
            }

            for (int row = 0; row < rows; row++)
            {
                if (filledRows[row])
                    continue;

                int source = FindNearestFilledRow(filledRows, row);
                if (source < 0)
                    return false;

                for (int column = 0; column < columns; column++)
                    radii[row, column] = radii[source, column];
            }

            return true;
        }

        private static int FindNearestFilledRow(bool[] filledRows, int row)
        {
            for (int distance = 1; distance < filledRows.Length; distance++)
            {
                if (row - distance >= 0 && filledRows[row - distance])
                    return row - distance;

                if (row + distance < filledRows.Length && filledRows[row + distance])
                    return row + distance;
            }

            return -1;
        }
    }
}
