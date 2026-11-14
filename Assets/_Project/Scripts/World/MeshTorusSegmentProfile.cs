using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Segment terrain taken from the shape of a mesh made by a level designer: a square terrain patch
    /// lying on the XZ plane with heights in Y. Mesh X runs along the segment, mesh Z around the tube.
    /// The mesh is projected from above onto a grid of heights. It needs Read/Write enabled.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Mesh Torus Segment Profile")]
    public class MeshTorusSegmentProfile : GridTorusSegmentProfile
    {
        [Header("Mesh")]
        [Tooltip("Needs Read/Write enabled.")]
        [SerializeField]
        private Mesh terrainMesh;
        public Mesh TerrainMesh => terrainMesh;

        [Tooltip("Size of the grid the mesh is projected onto (per side). Higher means more detail and a slower rebuild.")]
        [SerializeField]
        [Range(16, 512)]
        private int gridResolution = 128;
        public int GridResolution => gridResolution;

        [Tooltip("Multiplies mesh heights (mesh Y).")]
        [SerializeField]
        private float heightScale = 1f;
        public float HeightScale => heightScale;

        [Tooltip("Added to the height, in world units.")]
        [SerializeField]
        private float heightOffset;
        public float HeightOffset => heightOffset;

        protected override bool TryBuildGrid(out float[] gridHeights, out int width, out int height)
        {
            gridHeights = null;
            width = gridResolution;
            height = gridResolution;

            if (terrainMesh == null)
                return false;

            if (terrainMesh.isReadable == false)
            {
                Debug.LogWarning($"Mesh '{terrainMesh.name}' is not readable. Enable Read/Write in its import settings.", this);
                return false;
            }

            if (MeshHeightRasterizer.TryRasterize(terrainMesh, gridResolution, out gridHeights) == false)
                return false;

            for (int i = 0; i < gridHeights.Length; i++)
                gridHeights[i] = gridHeights[i] * heightScale + heightOffset;

            return true;
        }
    }
}
