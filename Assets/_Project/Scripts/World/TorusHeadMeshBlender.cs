using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Deforms the segment meshes near the head to match <see cref="TorusHeadTerrain"/>. Only segments
    /// the head currently touches are recalculated, all others keep their original vertices and normals.
    /// Normals along the borders of the changed segments are averaged, like in <see cref="TorusNormalStitcher"/>.
    /// </summary>
    public class TorusHeadMeshBlender
    {
        private const float TwoPi = TorusGeometry.TwoPi;

        private class SegmentData
        {
            public Mesh Mesh;
            public int Rows;
            public int Columns;
            public float StartTheta;

            public Vector3[] OriginalVertices;
            public Vector3[] OriginalNormals;
            public Vector3[] BaseNormals;
            public float[] Heights;

            public List<Vector3> Vertices = new List<Vector3>();
            public List<Vector3> Normals = new List<Vector3>();

            public bool HasDeformedVertices;
            public bool HasModifiedNormals;
            public bool IsNormalsDirty;
        }

        private readonly SegmentData[] segments;
        private readonly TorusSurface surface;
        private readonly TorusHeadTerrain terrain;
        private readonly int sidesResolution;
        private readonly float[] columnNeckHeights;
        private int appliedVersion = -1;

        public TorusHeadMeshBlender(TorusSurface surface, IReadOnlyList<Mesh> meshes, int sidesResolution, TorusHeadTerrain terrain)
        {
            this.surface = surface;
            this.terrain = terrain;
            this.sidesResolution = sidesResolution;
            columnNeckHeights = new float[sidesResolution + 1];

            segments = new SegmentData[meshes.Count];
            for (int i = 0; i < segments.Length; i++)
                segments[i] = CreateData(i, meshes[i]);
        }

        /// <summary>Applies the current state of the head to the meshes. Does nothing if nothing changed.</summary>
        public void Update()
        {
            if (terrain.Version == appliedVersion)
                return;

            appliedVersion = terrain.Version;
            var affected = GetAffectedSegments();

            for (int i = 0; i < segments.Length; i++)
            {
                if (affected[i])
                    Deform(segments[i], i);
                else
                    Restore(segments[i]);
            }

            for (int i = 0; i < segments.Length; i++)
            {
                int next = (i + 1) % segments.Length;
                if (next != i && (affected[i] || affected[next]))
                    StitchBorder(segments[i], segments[next]);
            }

            foreach (var segment in segments)
            {
                if (segment.IsNormalsDirty == false)
                    continue;

                segment.Mesh.SetNormals(segment.Normals);
                segment.IsNormalsDirty = false;
            }
        }

        private SegmentData CreateData(int segmentIndex, Mesh mesh)
        {
            var data = new SegmentData
            {
                Mesh = mesh,
                Columns = sidesResolution + 1,
                StartTheta = segmentIndex * surface.SegmentAngle,
                OriginalVertices = mesh.vertices,
                OriginalNormals = mesh.normals,
            };

            data.Rows = data.OriginalVertices.Length / data.Columns;
            data.BaseNormals = new Vector3[data.OriginalVertices.Length];
            data.Heights = new float[data.OriginalVertices.Length];
            data.Normals.AddRange(data.OriginalNormals);

            for (int row = 0; row < data.Rows; row++)
            {
                float theta = GetRowTheta(data, row);
                for (int column = 0; column < data.Columns; column++)
                {
                    float phi = GetColumnPhi(column);
                    int index = row * data.Columns + column;

                    var baseNormal = surface.GetBaseNormal(theta, phi);
                    data.BaseNormals[index] = baseNormal;
                    data.Heights[index] = Vector3.Dot(data.OriginalVertices[index] - surface.GetBasePoint(theta, phi), baseNormal);
                }
            }

            return data;
        }

        private float GetRowTheta(SegmentData data, int row)
        {
            return data.StartTheta + (float)row / (data.Rows - 1) * surface.SegmentAngle;
        }

        private float GetColumnPhi(int column)
        {
            return (float)column / sidesResolution * TwoPi;
        }

        private bool[] GetAffectedSegments()
        {
            var affected = new bool[segments.Length];
            if (terrain.IsActive == false)
                return affected;

            terrain.GetAffectedRange(out float start, out float length);
            if (length >= TwoPi)
            {
                for (int i = 0; i < affected.Length; i++)
                    affected[i] = true;

                return affected;
            }

            float wrappedStart = Mathf.Repeat(start, TwoPi);
            int first = Mathf.FloorToInt(wrappedStart / surface.SegmentAngle);
            int last = Mathf.FloorToInt((wrappedStart + length) / surface.SegmentAngle);
            for (int i = first; i <= last; i++)
                affected[i % affected.Length] = true;

            return affected;
        }

        private void Deform(SegmentData data, int segmentIndex)
        {
            for (int column = 0; column < data.Columns; column++)
                columnNeckHeights[column] = terrain.GetNeckHeight(GetColumnPhi(column));

            data.Vertices.Clear();
            for (int row = 0; row < data.Rows; row++)
            {
                float weight = terrain.GetWeight(GetRowTheta(data, row));
                for (int column = 0; column < data.Columns; column++)
                {
                    int index = row * data.Columns + column;
                    var vertex = data.OriginalVertices[index];
                    if (weight > 0f)
                        vertex += data.BaseNormals[index] * ((columnNeckHeights[column] - data.Heights[index]) * weight);

                    data.Vertices.Add(vertex);
                }
            }

            data.Mesh.SetVertices(data.Vertices);
            data.Mesh.RecalculateNormals();
            data.Mesh.RecalculateBounds();
            data.Mesh.GetNormals(data.Normals);

            // The seam around the tube is a border of the mesh as well.
            for (int row = 0; row < data.Rows; row++)
            {
                int first = row * data.Columns;
                int last = first + sidesResolution;
                var average = (data.Normals[first] + data.Normals[last]).normalized;
                data.Normals[first] = average;
                data.Normals[last] = average;
            }

            data.HasDeformedVertices = true;
            data.HasModifiedNormals = true;
            data.IsNormalsDirty = true;
        }

        private void Restore(SegmentData data)
        {
            if (data.HasDeformedVertices)
            {
                data.Mesh.SetVertices(data.OriginalVertices);
                data.Mesh.RecalculateBounds();
                data.HasDeformedVertices = false;
            }

            if (data.HasModifiedNormals == false)
                return;

            for (int i = 0; i < data.OriginalNormals.Length; i++)
                data.Normals[i] = data.OriginalNormals[i];

            data.HasModifiedNormals = false;
            data.IsNormalsDirty = true;
        }

        /// <summary>Averages normals along the border between two neighbouring segments (the last row of one and the first row of the next).</summary>
        private void StitchBorder(SegmentData current, SegmentData next)
        {
            int lastRowStart = (current.Rows - 1) * current.Columns;

            for (int column = 0; column < sidesResolution; column++)
            {
                int a = lastRowStart + column;
                int b = column;

                var sum = current.Normals[a] + next.Normals[b];

                // The corner of the seam around the tube is the same point as the last column.
                bool isSeam = column == 0;
                if (isSeam)
                    sum += current.Normals[a + sidesResolution] + next.Normals[b + sidesResolution];

                var average = sum.normalized;
                current.Normals[a] = average;
                next.Normals[b] = average;
                if (isSeam)
                {
                    current.Normals[a + sidesResolution] = average;
                    next.Normals[b + sidesResolution] = average;
                }
            }

            current.HasModifiedNormals = true;
            current.IsNormalsDirty = true;
            next.HasModifiedNormals = true;
            next.IsNormalsDirty = true;
        }
    }
}
