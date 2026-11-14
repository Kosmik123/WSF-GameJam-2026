using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Segment terrain read from a heightmap texture (red channel). Texture x runs along the segment,
    /// texture y around the tube. Import the texture with Read/Write enabled and sRGB turned off.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Heightmap Torus Segment Profile")]
    public class HeightmapTorusSegmentProfile : GridTorusSegmentProfile
    {
        [Header("Heightmap")]
        [Tooltip("Needs Read/Write enabled. Use a 16-bit format (R16) to avoid terracing.")]
        [SerializeField]
        private Texture2D heightmap;
        public Texture2D Heightmap => heightmap;

        [Tooltip("Height for a black pixel, in world units.")]
        [SerializeField]
        private float minHeight = -2f;
        public float MinHeight => minHeight;

        [Tooltip("Height for a white pixel, in world units.")]
        [SerializeField]
        private float maxHeight = 2f;
        public float MaxHeight => maxHeight;

        protected override bool TryBuildGrid(out float[] gridHeights, out int width, out int height)
        {
            gridHeights = null;
            width = 0;
            height = 0;

            if (heightmap == null)
                return false;

            if (heightmap.isReadable == false)
            {
                Debug.LogWarning($"Heightmap '{heightmap.name}' is not readable. Enable Read/Write in its import settings.", this);
                return false;
            }

            var pixels = heightmap.GetPixels();
            width = heightmap.width;
            height = heightmap.height;

            gridHeights = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                gridHeights[i] = Mathf.Lerp(minHeight, maxHeight, pixels[i].r);

            return true;
        }
    }
}
