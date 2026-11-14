using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Low poly solids of the effects. A closed set of geometric primitives, so an enum is enough.</summary>
    public enum VfxShape
    {
        Octa,
        Tetra,
        Cube,
        Crystal,
        CrystalForward,
        Sphere,
        Torus,
        Quad,
    }

    /// <summary>
    /// Meshes of the stylized effects built in code (port of the LD57 MageStylizedFx meshes): flat shaded solids with the
    /// light baked into the vertex color (duplicated vertices), a horizontal quad and arcs for the sword and the spin.
    /// </summary>
    public static class VfxMeshes
    {
        private static readonly Vector3 LightDirection = new Vector3(0.4f, 0.8f, 0.45f).normalized;
        private static readonly Dictionary<VfxShape, Mesh> meshes = new Dictionary<VfxShape, Mesh>();
        private static readonly Dictionary<int, Mesh> arcs = new Dictionary<int, Mesh>();

        public static Mesh Get(VfxShape shape)
        {
            if (meshes.TryGetValue(shape, out var cached) && cached)
                return cached;

            var builder = new MeshBuilder();
            switch (shape)
            {
                case VfxShape.Octa:
                    BuildBipyramid(builder, 4, 0.5f, 0f, 0.5f, -0.5f, false);
                    break;
                case VfxShape.Tetra:
                    BuildTetra(builder);
                    break;
                case VfxShape.Cube:
                    BuildCube(builder);
                    break;
                case VfxShape.Crystal:
                    BuildBipyramid(builder, 5, 0.25f, -0.2f, 0.5f, -0.5f, false);
                    break;
                case VfxShape.CrystalForward:
                    BuildBipyramid(builder, 5, 0.25f, -0.2f, 0.5f, -0.5f, true);
                    break;
                case VfxShape.Sphere:
                    BuildIcosphere(builder);
                    break;
                case VfxShape.Torus:
                    BuildTorus(builder, 12, 5, 0.5f, 0.07f);
                    break;
                default:
                    BuildQuad(builder);
                    break;
            }

            var mesh = builder.ToMesh(shape + " (low poly)");
            meshes[shape] = mesh;
            return mesh;
        }

        /// <summary>
        /// Flat sector of a ring in the XZ plane, centred on +Z, of unit outer radius. The leading edge (on the +X side) is opaque,
        /// the tail fades out, so turning it around +Y (positive degrees) looks like a swing.
        /// </summary>
        public static Mesh GetArc(float spanDegrees, float innerRadius)
        {
            int key = Mathf.RoundToInt(spanDegrees) * 1000 + Mathf.RoundToInt(innerRadius * 100f);
            if (arcs.TryGetValue(key, out var cached) && cached)
                return cached;

            int segments = Mathf.Clamp(Mathf.RoundToInt(spanDegrees / 10f), 6, 36);
            float span = spanDegrees * Mathf.Deg2Rad;
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float progress = i / (float)segments;
                float angle = span * 0.5f - progress * span;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float inner = Mathf.Lerp(innerRadius, innerRadius * 0.65f, progress);
                float alpha = Mathf.Pow(1f - progress, 1.6f);
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction;
                colors[i * 2] = new Color(1f, 1f, 1f, alpha * 0.3f);
                colors[i * 2 + 1] = new Color(1f, 1f, 1f, alpha);
                uvs[i * 2] = new Vector2(progress, 0f);
                uvs[i * 2 + 1] = new Vector2(progress, 1f);
                if (i < segments)
                {
                    int vertex = i * 2;
                    int triangle = i * 6;
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 1;
                    triangles[triangle + 2] = vertex + 2;
                    triangles[triangle + 3] = vertex + 1;
                    triangles[triangle + 4] = vertex + 3;
                    triangles[triangle + 5] = vertex + 2;
                }
            }

            var mesh = new Mesh { name = $"Vfx Arc {spanDegrees:0}" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            arcs[key] = mesh;
            return mesh;
        }

        private static void BuildBipyramid(MeshBuilder builder, int sides, float ringRadius, float ringHeight, float top, float bottom, bool isAlongZ)
        {
            var apexTop = Axis(0f, top, 0f, isAlongZ);
            var apexBottom = Axis(0f, bottom, 0f, isAlongZ);
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                ring[i] = Axis(Mathf.Cos(angle) * ringRadius, ringHeight, Mathf.Sin(angle) * ringRadius, isAlongZ);
            }

            var inside = Axis(0f, ringHeight, 0f, isAlongZ);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                builder.Triangle(apexTop, ring[i], ring[next], inside);
                builder.Triangle(apexBottom, ring[i], ring[next], inside);
            }
        }

        private static Vector3 Axis(float x, float y, float z, bool isAlongZ)
        {
            return isAlongZ ? new Vector3(x, z, y) : new Vector3(x, y, z);
        }

        private static void BuildTetra(MeshBuilder builder)
        {
            var a = new Vector3(0.5f, 0.5f, 0.5f);
            var b = new Vector3(-0.5f, -0.5f, 0.5f);
            var c = new Vector3(-0.5f, 0.5f, -0.5f);
            var d = new Vector3(0.5f, -0.5f, -0.5f);
            builder.Triangle(a, b, c, Vector3.zero);
            builder.Triangle(a, b, d, Vector3.zero);
            builder.Triangle(a, c, d, Vector3.zero);
            builder.Triangle(b, c, d, Vector3.zero);
        }

        private static void BuildCube(MeshBuilder builder)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);

            int[] faces = { 0, 1, 3, 2, 4, 5, 7, 6, 0, 1, 5, 4, 2, 3, 7, 6, 0, 2, 6, 4, 1, 3, 7, 5 };
            for (int i = 0; i < faces.Length; i += 4)
            {
                builder.Triangle(corners[faces[i]], corners[faces[i + 1]], corners[faces[i + 2]], Vector3.zero);
                builder.Triangle(corners[faces[i]], corners[faces[i + 2]], corners[faces[i + 3]], Vector3.zero);
            }
        }

        private static void BuildIcosphere(MeshBuilder builder)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new[]
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f),
            };
            for (int i = 0; i < points.Length; i++)
                points[i] = points[i].normalized * 0.5f;

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int i = 0; i < faces.Length; i += 3)
                builder.Triangle(points[faces[i]], points[faces[i + 1]], points[faces[i + 2]], Vector3.zero);
        }

        private static void BuildTorus(MeshBuilder builder, int segments, int tubeSegments, float majorRadius, float minorRadius)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle0 = i / (float)segments * Mathf.PI * 2f;
                float angle1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                var direction0 = new Vector3(Mathf.Cos(angle0), 0f, Mathf.Sin(angle0));
                var direction1 = new Vector3(Mathf.Cos(angle1), 0f, Mathf.Sin(angle1));
                var center = (direction0 + direction1).normalized * majorRadius * Mathf.Cos(Mathf.PI / segments);
                for (int j = 0; j < tubeSegments; j++)
                {
                    float tube0 = j / (float)tubeSegments * Mathf.PI * 2f;
                    float tube1 = (j + 1) / (float)tubeSegments * Mathf.PI * 2f;
                    var p00 = TorusPoint(direction0, tube0, majorRadius, minorRadius);
                    var p01 = TorusPoint(direction0, tube1, majorRadius, minorRadius);
                    var p10 = TorusPoint(direction1, tube0, majorRadius, minorRadius);
                    var p11 = TorusPoint(direction1, tube1, majorRadius, minorRadius);
                    builder.Triangle(p00, p10, p11, center);
                    builder.Triangle(p00, p11, p01, center);
                }
            }
        }

        private static Vector3 TorusPoint(Vector3 direction, float tubeAngle, float majorRadius, float minorRadius)
        {
            return direction * (majorRadius + Mathf.Cos(tubeAngle) * minorRadius) + Vector3.up * Mathf.Sin(tubeAngle) * minorRadius;
        }

        private static void BuildQuad(MeshBuilder builder)
        {
            builder.Quad(
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f));
        }

        private class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            /// <summary>Flat face. <paramref name="inside"/> is a point inside the solid, it decides where the normal points.</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }

                normal.Normalize();
                float shade = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Vector3.Dot(normal, LightDirection) * 0.5f + 0.5f));
                var color = new Color(shade, shade, shade, 1f);
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                for (int i = 0; i < 3; i++)
                {
                    normals.Add(normal);
                    colors.Add(color);
                }

                uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            /// <summary>Horizontal quad with UVs 0-1 and a white color, facing up.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(Vector3.up);
                    colors.Add(Color.white);
                }

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
