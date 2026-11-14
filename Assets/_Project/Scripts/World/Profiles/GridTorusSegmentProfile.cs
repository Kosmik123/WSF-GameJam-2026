using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Base for profiles whose terrain comes from a regular grid of heights made by a level designer:
    /// grid x runs along the segment, grid y runs around the tube.
    /// </summary>
    public abstract class GridTorusSegmentProfile : TorusSegmentProfile
    {
        [Header("Mapping")]
        [Tooltip("How many times the grid is repeated around the tube.")]
        [SerializeField]
        [Min(1)]
        private int aroundTiling = 1;
        public int AroundTiling => aroundTiling;

        [Tooltip("The grid is a square patch, so its opposite edges around the tube do not match. Heights this close to those edges (part of the grid) are blended to meet.")]
        [SerializeField]
        [Range(0f, 0.5f)]
        private float seamBlendWidth = 0.15f;
        public float SeamBlendWidth => seamBlendWidth;

        private float[] heights;
        private int gridWidth;
        private int gridHeight;
        private bool isGridBuilt;

        /// <summary>Fills a grid of final heights (world units). Row-major: index = y * width + x. At least 2x2.</summary>
        protected abstract bool TryBuildGrid(out float[] gridHeights, out int width, out int height);

        public override void Invalidate()
        {
            isGridBuilt = false;
            heights = null;
        }

        public override float SampleHeight(float along, float around, Vector3 basePoint)
        {
            if (EnsureGrid() == false)
                return 0f;

            float u = Mathf.Clamp01(along);
            float v = Mathf.Repeat(around * aroundTiling, 1f);
            float height = SampleGrid(u, v);

            if (seamBlendWidth <= 0f)
                return height;

            // Both edges are pulled to their average, with the correction fading out inside the grid.
            float start = SampleGrid(u, 0f);
            float end = SampleGrid(u, 1f);
            float middle = (start + end) * 0.5f;

            if (v < seamBlendWidth)
                height += (middle - start) * SeamFalloff(v / seamBlendWidth);

            if (v > 1f - seamBlendWidth)
                height += (middle - end) * SeamFalloff((1f - v) / seamBlendWidth);

            return height;
        }

        private bool EnsureGrid()
        {
            if (isGridBuilt)
                return heights != null;

            isGridBuilt = true;
            if (TryBuildGrid(out heights, out gridWidth, out gridHeight) == false || IsGridValid() == false)
                heights = null;

            return heights != null;
        }

        private bool IsGridValid()
        {
            return heights != null && gridWidth >= 2 && gridHeight >= 2 && heights.Length == gridWidth * gridHeight;
        }

        /// <summary>Bilinear sample. u and v are 0-1 and clamped to the grid.</summary>
        private float SampleGrid(float u, float v)
        {
            float x = u * (gridWidth - 1);
            float y = v * (gridHeight - 1);
            int x0 = Mathf.Min(Mathf.FloorToInt(x), gridWidth - 2);
            int y0 = Mathf.Min(Mathf.FloorToInt(y), gridHeight - 2);
            float tx = x - x0;
            float ty = y - y0;

            float h00 = heights[y0 * gridWidth + x0];
            float h10 = heights[y0 * gridWidth + x0 + 1];
            float h01 = heights[(y0 + 1) * gridWidth + x0];
            float h11 = heights[(y0 + 1) * gridWidth + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        private float SeamFalloff(float distance)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, distance);
        }

        private void OnValidate()
        {
            Invalidate();
        }
    }
}
