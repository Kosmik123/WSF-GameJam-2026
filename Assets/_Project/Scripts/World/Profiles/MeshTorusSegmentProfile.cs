using UnityEngine;
using UnityEngine.Serialization;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Segment terrain taken from the shape of a mesh made by a level designer: a square terrain patch
    /// lying on the XZ plane with heights in Y. Mesh X runs along the segment, mesh Z around the tube.
    /// The mesh is projected from above onto a grid of heights and vertex colors. It needs Read/Write enabled.
    /// The mesh is usually a rectangle, so the grid has separate resolutions along the segment and around the tube.
    /// When they equal the number of the mesh's vertex columns and rows, grid nodes are exactly the mesh vertices.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Mesh Torus Segment Profile")]
    public class MeshTorusSegmentProfile : GridTorusSegmentProfile
    {
        [Header("Mesh")]
        [Tooltip("Needs Read/Write enabled.")]
        [SerializeField]
        private Mesh terrainMesh;
        public Mesh TerrainMesh => terrainMesh;

        [Tooltip("Grid nodes along the segment (mesh X). Set it to the number of vertex columns of the mesh to use its vertices one to one.")]
        [FormerlySerializedAs("gridResolution")]
        [SerializeField]
        [Range(2, 512)]
        private int gridResolutionAlong = 128;
        public int GridResolutionAlong => gridResolutionAlong;

        [Tooltip("Grid nodes around the tube (mesh Z). Set it to the number of vertex rows of the mesh to use its vertices one to one.")]
        [SerializeField]
        [Range(2, 512)]
        private int gridResolutionAround = 128;
        public int GridResolutionAround => gridResolutionAround;

        [Tooltip("Multiplies mesh heights (mesh Y).")]
        [SerializeField]
        private float heightScale = 1f;
        public float HeightScale => heightScale;

        [Tooltip("Added to the height, in world units.")]
        [SerializeField]
        private float heightOffset;
        public float HeightOffset => heightOffset;

        protected override bool TryBuildGrid(out float[] gridHeights, out Color[] gridColors, out int width, out int height)
        {
            gridHeights = null;
            gridColors = null;
            width = gridResolutionAlong;
            height = gridResolutionAround;

            if (terrainMesh == null)
                return false;

            if (terrainMesh.isReadable == false)
            {
                Debug.LogWarning($"Mesh '{terrainMesh.name}' is not readable. Enable Read/Write in its import settings.", this);
                return false;
            }

            if (MeshHeightRasterizer.TryRasterize(terrainMesh, gridResolutionAlong, gridResolutionAround, out gridHeights, out gridColors) == false)
                return false;

            for (int i = 0; i < gridHeights.Length; i++)
                gridHeights[i] = gridHeights[i] * heightScale + heightOffset;

            WarnAboutMeshMismatch();
            return true;
        }

        private void WarnAboutMeshMismatch()
        {
            if (terrainMesh.colors.Length != terrainMesh.vertexCount)
                Debug.LogWarning($"Mesh '{terrainMesh.name}' has no vertex colors, the terrain of '{name}' stays white.", this);

            if (MeshHeightRasterizer.TryGetVertexGridSize(terrainMesh, out int columns, out int rows) == false)
                return;

            if (columns != gridResolutionAlong || rows != gridResolutionAround)
                Debug.LogWarning($"Mesh '{terrainMesh.name}' has {columns} x {rows} vertex columns and rows, but the grid of '{name}' is {gridResolutionAlong} x {gridResolutionAround}. Vertices are resampled instead of used one to one.", this);
        }
    }
}
