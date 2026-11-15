using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Water surfaces of segments whose profile has water (<see cref="TorusSegmentProfile.TryGetWaterSurface"/>):
    /// a shell around the tube at the water height. The terrain above the water hides it, so it is seen only over the basins.
    /// The shell follows the tail thinning ahead of the head and is pulled inside the tube where the head lies.
    /// </summary>
    public class TorusWaterSurfaces
    {
        private const int MinLengthResolution = 8;

        private class Water
        {
            public Mesh Mesh;
            public Vector3[] BasePoints;
            public Vector3[] Normals;
            public float[] Thetas;
            public float[] Phis;
            public float Height;
            public Vector3[] Vertices;
        }

        private readonly List<Water> waters = new List<Water>();
        private readonly TorusSurface surface;
        private readonly TorusHeadTerrain headTerrain;
        private int appliedVersion = -1;

        public TorusWaterSurfaces(TorusSurface surface, TorusHeadTerrain headTerrain)
        {
            this.surface = surface;
            this.headTerrain = headTerrain;
        }

        /// <summary>Creates the water objects as children of the parent. They are DontSave, like the segments, so the world clears them.</summary>
        public void Build(Transform parent, Material material, int sidesResolution)
        {
            if (material == null)
                return;

            for (int i = 0; i < surface.SegmentCount; i++)
            {
                var profile = surface.GetProfile(i);
                if (profile == null || profile.TryGetWaterSurface(out float height) == false)
                    continue;

                int lengthResolution = Mathf.Max(MinLengthResolution, profile.LengthResolution / 2);
                var mesh = TorusSegmentMeshBuilder.Build(surface, i, lengthResolution, sidesResolution);
                mesh.name = $"TorusWater_{i}";
                mesh.hideFlags = HideFlags.DontSave;
                waters.Add(CreateWater(mesh, height, i));

                var waterObject = new GameObject($"Water_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                waterObject.hideFlags = HideFlags.DontSave;
                waterObject.transform.SetParent(parent, false);
                waterObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = waterObject.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }

            appliedVersion = -1;
            Update();
        }

        /// <summary>Moves the water with the head terrain. Cheap when the head did not change.</summary>
        public void Update()
        {
            if (waters.Count == 0 || appliedVersion == headTerrain.Version)
                return;

            appliedVersion = headTerrain.Version;
            float minorRadius = surface.MinorRadius;
            foreach (var water in waters)
            {
                for (int i = 0; i < water.Vertices.Length; i++)
                {
                    float theta = water.Thetas[i];
                    float height = headTerrain.GetBlendWeight(theta) > 0.001f
                        ? -minorRadius
                        : headTerrain.Apply(water.Height, theta, water.Phis[i], minorRadius);
                    water.Vertices[i] = water.BasePoints[i] + water.Normals[i] * height;
                }

                water.Mesh.vertices = water.Vertices;
                water.Mesh.RecalculateBounds();
            }
        }

        private Water CreateWater(Mesh mesh, float height, int segmentIndex)
        {
            var basePoints = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var water = new Water
            {
                Mesh = mesh,
                BasePoints = basePoints,
                Normals = normals,
                Thetas = new float[basePoints.Length],
                Phis = new float[basePoints.Length],
                Height = height,
                Vertices = new Vector3[basePoints.Length],
            };

            for (int i = 0; i < basePoints.Length; i++)
            {
                water.Thetas[i] = (segmentIndex + uvs[i].x) * surface.SegmentAngle;
                water.Phis[i] = uvs[i].y * TorusGeometry.TwoPi;
                water.Vertices[i] = basePoints[i] + normals[i] * height;
            }

            mesh.vertices = water.Vertices;
            mesh.RecalculateBounds();
            return water;
        }
    }
}
