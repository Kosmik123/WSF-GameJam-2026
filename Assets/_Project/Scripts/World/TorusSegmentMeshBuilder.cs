using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.World
{
    public static class TorusSegmentMeshBuilder
    {
        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>
        /// Builds the segment on the base tube, without terrain: it is added later by <see cref="TorusSegmentProfile.Deform"/>.
        /// uv.x is the position along the segment (0-1) and is used by the deformation.
        /// </summary>
        /// <param name="lengthResolution">Subdivisions along the segment.</param>
        /// <param name="sidesResolution">Subdivisions around the tube. Must be equal for all segments so borders match.</param>
        public static Mesh Build(TorusSurface surface, int segmentIndex, int lengthResolution, int sidesResolution)
        {
            int rows = lengthResolution + 1;
            int columns = sidesResolution + 1;
            float thetaStart = segmentIndex * surface.SegmentAngle;

            var vertices = new Vector3[rows * columns];
            var normals = new Vector3[rows * columns];
            var uvs = new Vector2[rows * columns];

            for (int row = 0; row < rows; row++)
            {
                float alongT = (float)row / lengthResolution;
                float theta = thetaStart + alongT * surface.SegmentAngle;

                for (int column = 0; column < columns; column++)
                {
                    float aroundT = (float)column / sidesResolution;
                    float phi = aroundT * TwoPi;
                    int vertex = row * columns + column;

                    vertices[vertex] = surface.GetBasePoint(theta, phi);
                    normals[vertex] = surface.GetBaseNormal(theta, phi);
                    uvs[vertex] = new Vector2(alongT, aroundT);
                }
            }

            var triangles = new int[lengthResolution * sidesResolution * 6];
            int index = 0;
            for (int row = 0; row < lengthResolution; row++)
            {
                for (int column = 0; column < sidesResolution; column++)
                {
                    int a = row * columns + column;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    triangles[index++] = a;
                    triangles[index++] = c;
                    triangles[index++] = b;
                    
                    triangles[index++] = b;
                    triangles[index++] = c;
                    triangles[index++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = $"TorusSegment_{segmentIndex}",
                indexFormat = IndexFormat.UInt32,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
