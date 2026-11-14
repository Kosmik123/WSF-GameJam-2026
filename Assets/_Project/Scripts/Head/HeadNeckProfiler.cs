using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Measures the outline of the head at its rear end (the neck) from its meshes. The result is the height of
    /// the outline above the base tube for evenly spaced angles phi, which the terrain behind the head blends into.
    /// </summary>
    public static class HeadNeckProfiler
    {
        /// <summary>
        /// Measures the head as it is placed at the given theta.
        /// </summary>
        /// <param name="sliceDepth">Thickness of the slice, measured from the rear end of the head, whose widest points make the outline.</param>
        /// <param name="heights">Filled with outline heights above the base tube, evenly spaced from phi = 0.</param>
        /// <param name="neckOffset">How far (radians of theta) the rear end lies behind the head's pivot.</param>
        /// <param name="headLength">Length of the head along the body, in radians of theta.</param>
        public static bool TryMeasure(TorusWorld world, float theta, IReadOnlyList<Renderer> renderers, float sliceDepth, float[] heights, out float neckOffset, out float headLength)
        {
            neckOffset = 0f;
            headLength = 0f;

            var points = CollectPoints(world, theta, renderers);
            if (points.Count == 0)
                return false;

            float minForward = float.MaxValue;
            float maxForward = float.MinValue;
            foreach (var point in points)
            {
                minForward = Mathf.Min(minForward, point.z);
                maxForward = Mathf.Max(maxForward, point.z);
            }

            int bins = heights.Length;
            var radii = new float[bins];
            foreach (var point in points)
            {
                if (point.z > minForward + sliceDepth)
                    continue;

                float phi = Mathf.Atan2(point.y, point.x);
                int bin = Mathf.RoundToInt(Mathf.Repeat(phi, TorusGeometry.TwoPi) / TorusGeometry.TwoPi * bins) % bins;
                float radius = Mathf.Sqrt(point.x * point.x + point.y * point.y);
                radii[bin] = Mathf.Max(radii[bin], radius);
            }

            if (FillEmptyBins(radii) == false)
                return false;

            for (int i = 0; i < bins; i++)
                heights[i] = radii[i] - world.MinorRadius;

            neckOffset = -minForward / world.MajorRadius;
            headLength = (maxForward - minForward) / world.MajorRadius;
            return true;
        }

        /// <summary>Vertices in the frame of the tube cross-section at theta: x outward, y up, z along the body.</summary>
        private static List<Vector3> CollectPoints(TorusWorld world, float theta, IReadOnlyList<Renderer> renderers)
        {
            var center = TorusGeometry.GetCenterPoint(world.MajorRadius, theta);
            var forward = TorusGeometry.GetThetaDirection(theta);
            var outward = TorusGeometry.GetNormal(theta, 0f);
            var up = TorusGeometry.GetNormal(theta, Mathf.PI * 0.5f);

            var points = new List<Vector3>();
            var vertices = new List<Vector3>();
            foreach (var renderer in renderers)
            {
                var mesh = GetMesh(renderer);
                if (mesh == null)
                    continue;

                vertices.Clear();
                mesh.GetVertices(vertices);
                foreach (var vertex in vertices)
                {
                    var local = world.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex)) - center;
                    points.Add(new Vector3(Vector3.Dot(local, outward), Vector3.Dot(local, up), Vector3.Dot(local, forward)));
                }
            }

            return points;
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;

            return renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
        }

        /// <summary>Fills bins without any vertex by interpolating between the nearest filled bins around the circle.</summary>
        private static bool FillEmptyBins(float[] radii)
        {
            int count = radii.Length;
            bool hasAny = false;
            foreach (float radius in radii)
                hasAny |= radius > 0f;

            if (hasAny == false)
                return false;

            var filled = (float[])radii.Clone();
            for (int i = 0; i < count; i++)
            {
                if (radii[i] > 0f)
                    continue;

                int before = 1;
                while (radii[(i - before + count) % count] <= 0f)
                    before++;

                int after = 1;
                while (radii[(i + after) % count] <= 0f)
                    after++;

                float t = (float)before / (before + after);
                filled[i] = Mathf.Lerp(radii[(i - before + count) % count], radii[(i + after) % count], t);
            }

            System.Array.Copy(filled, radii, count);
            return true;
        }
    }
}
