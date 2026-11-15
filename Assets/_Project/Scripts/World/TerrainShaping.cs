using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Changes the heights of a designer-made terrain to make it harder to cross: adds plateaus with steep cliffs
    /// (that must be walked around) and sinks the water areas into basins under a water surface.
    /// Plateaus come from 3D noise at the base torus point, so they continue across segment borders and the tube seam.
    /// </summary>
    [System.Serializable]
    public class TerrainShaping
    {
        private const int NoiseOctaves = 4;

        [Header("Cliffs")]
        [Tooltip("Height of one plateau tier, in meters. 0 = no plateaus.")]
        [SerializeField]
        [Min(0f)]
        private float tierHeight;

        [Tooltip("Number of plateau tiers stacked on each other.")]
        [SerializeField]
        [Range(1, 4)]
        private int tierCount = 1;

        [Tooltip("Typical size of a plateau, in meters.")]
        [SerializeField]
        [Min(5f)]
        private float plateauSize = 70f;

        [Tooltip("Part of the land covered by the lowest tier.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float coverage = 0.35f;

        [Tooltip("How soft the cliff faces are. Smaller = steeper.")]
        [SerializeField]
        [Range(0.005f, 0.3f)]
        private float cliffSoftness = 0.03f;

        [Tooltip("Moves the noise, so biomes get different plateaus.")]
        [SerializeField]
        private float seed;

        [Header("Basins (water, lava)")]
        [Tooltip("Low areas (water, lava) are sunk into basins and get no plateaus.")]
        [SerializeField]
        private bool hasBasins;

        [Tooltip("Terrain at or below this height (before shaping) is the bottom of a basin.")]
        [SerializeField]
        private float basinLevel;

        [Tooltip("Height range (before shaping) above the basin level where the ground slopes down into the basin.")]
        [SerializeField]
        [Min(0.01f)]
        private float shoreWidth = 0.3f;

        [Tooltip("Depth of the basins, in meters.")]
        [SerializeField]
        [Min(0f)]
        private float basinDepth = 3f;

        [Tooltip("The basins are filled with water (a semi-transparent surface).")]
        [SerializeField]
        private bool hasWater;
        public bool HasWater => hasBasins && hasWater;

        [Tooltip("Water surface height above the basin level.")]
        [SerializeField]
        private float surfaceOffset = 0.3f;

        /// <summary>Height of the water surface above the base tube.</summary>
        public float WaterSurfaceHeight => basinLevel + surfaceOffset;

        public bool IsActive => tierHeight > 0f || hasBasins;

        /// <param name="height">Height of the terrain before shaping.</param>
        /// <param name="basePoint">The same place on the base torus, in world units.</param>
        public float Apply(float height, Vector3 basePoint)
        {
            float shaped = height;
            float dryness = 1f;

            if (hasBasins)
            {
                float sink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(basinLevel, basinLevel + shoreWidth, height));
                float depthNoise = TorusNoise.Fractal(basePoint / 25f + new Vector3(seed, 3.1f, -seed), 2);
                shaped -= basinDepth * sink * (0.6f + 0.8f * depthNoise);
                dryness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(basinLevel + shoreWidth, basinLevel + shoreWidth * 4f, height));
            }

            if (tierHeight > 0f && dryness > 0f)
                shaped += tierHeight * GetTiers(basePoint) * dryness;

            return shaped;
        }

        /// <summary>Number of tiers at the point, with smooth (but steep) steps between them: 0 to tierCount.</summary>
        private float GetTiers(Vector3 basePoint)
        {
            float noise = TorusNoise.Spread(basePoint / plateauSize + new Vector3(seed * 1.7f, -seed, seed * 0.3f), NoiseOctaves);
            float tiers = 0f;
            for (int i = 0; i < tierCount; i++)
            {
                float threshold = 1f - coverage * (tierCount - i) / tierCount;
                tiers += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold, threshold + cliffSoftness, noise));
            }

            return tiers;
        }
    }
}
