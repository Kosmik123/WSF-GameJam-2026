using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Changes the heights of a designer-made terrain to make it harder to cross: adds plateaus with steep cliffs
    /// (that must be walked around) and sinks the water areas into basins under a water surface.
    /// A winding path along the top of the tube stays free of plateaus, so the biome can always be crossed.
    /// Plateaus come from 3D noise at the base torus point, so they continue across segment borders and the tube seam.
    /// It also paints the terrain by the rules of the biome assets (docs/WSF_Biom.md): one color per face with a random
    /// brightness spread, dark bottoms and light tops of cliffs, rock layers, a cover (moss, snow) on plateau tops, lava cracks.
    /// Colors are sRGB, like the vertex colors of the terrain meshes.
    /// </summary>
    [System.Serializable]
    public class TerrainShaping
    {
        private const int NoiseOctaves = 4;
        private const int MaxTiers = 4;
        private const float CausewayHeight = 0.3f;

        [Header("Cliffs")]
        [Tooltip("Height of one plateau tier, in meters. 0 = no plateaus.")]
        [SerializeField]
        [Min(0f)]
        private float tierHeight;

        [Tooltip("Number of plateau tiers stacked on each other.")]
        [SerializeField]
        [Range(1, MaxTiers)]
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

        [Header("Path")]
        [Tooltip("Half width of the path kept free of plateaus, as a part of the way around the tube (0.05 = about 16 m on a 50 m tube). 0 = no path.")]
        [SerializeField]
        [Range(0f, 0.25f)]
        private float pathHalfWidth = 0.05f;

        [Tooltip("How far the path winds to the sides of the top of the tube, as a part of the way around the tube.")]
        [SerializeField]
        [Range(0f, 0.2f)]
        private float pathWander = 0.07f;

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

        [Header("Painting")]
        [Tooltip("Repaints the terrain by the rules of the biome assets.")]
        [SerializeField]
        private bool isPainted;

        [Tooltip("Random brightness change of every face (min and max, both directions).")]
        [SerializeField]
        private Vector2 brightnessSpread = new Vector2(0.05f, 0.12f);

        [Tooltip("Rock of the cliff faces: color at the bottom of a cliff.")]
        [SerializeField]
        private Color cliffDark = new Color(0.30f, 0.32f, 0.36f);

        [Tooltip("Rock of the cliff faces: color at the top of a cliff.")]
        [SerializeField]
        private Color cliffLight = new Color(0.62f, 0.64f, 0.68f);

        [Tooltip("Optional rock layers on the cliffs (light colors, shaded by the bottom-top gradient). Empty = plain rock.")]
        [SerializeField]
        private Color[] strataColors;

        [Tooltip("Height of one rock layer, in meters.")]
        [SerializeField]
        [Min(0.1f)]
        private float strataBand = 2f;

        [Tooltip("Cover of the plateau tops (moss, snow). Alpha = strength.")]
        [SerializeField]
        private Color topCover = new Color(1f, 1f, 1f, 0f);

        [Tooltip("How much lighter every higher tier is.")]
        [SerializeField]
        [Range(0f, 0.5f)]
        private float tierLighten = 0.08f;

        [Tooltip("Part of the cliff faces that are lava cracks.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float lavaCrackChance;

        [SerializeField]
        private Color lavaColor = new Color(1f, 0.45f, 0.1f);

        [Tooltip("Color of the basin bottoms (wet ground, mud). Alpha = strength.")]
        [SerializeField]
        private Color basinColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>Height of the water surface above the base tube.</summary>
        public float WaterSurfaceHeight => basinLevel + surfaceOffset;

        public bool IsActive => tierHeight > 0f || hasBasins;

        public bool IsPainted => isPainted;

        /// <param name="height">Height of the terrain before shaping.</param>
        /// <param name="around">Position around the tube: 0-1 (0.25 = top).</param>
        /// <param name="basePoint">The same place on the base torus, in world units.</param>
        public float Apply(float height, float around, Vector3 basePoint)
        {
            float sink = GetSink(height);
            float shaped = height;
            if (sink > 0f)
            {
                float depthNoise = TorusNoise.Fractal(basePoint / 25f + new Vector3(seed, 3.1f, -seed), 2);
                shaped -= basinDepth * sink * (0.6f + 0.8f * depthNoise);
            }

            if (tierHeight > 0f)
                shaped += tierHeight * EvaluateTiers(height, around, basePoint, out _, out _);

            // A causeway: the path crosses the water just above its surface.
            if (HasWater && pathHalfWidth > 0f)
            {
                float onPath = 1f - GetPathClearance(around, basePoint);
                shaped = Mathf.Lerp(shaped, Mathf.Max(shaped, WaterSurfaceHeight + CausewayHeight), onPath);
            }

            return shaped;
        }

        /// <summary>Paints the terrain color of the place. Takes and returns sRGB.</summary>
        /// <param name="color">Color of the terrain before painting.</param>
        /// <param name="height">Height of the terrain before shaping.</param>
        public Color Paint(Color color, float height, float around, Vector3 basePoint)
        {
            if (isPainted == false)
                return color;

            float alpha = color.a;
            float tiers = 0f;
            float cliff = 0f;
            float cliffPosition = 0f;
            if (tierHeight > 0f)
                tiers = EvaluateTiers(height, around, basePoint, out cliff, out cliffPosition);

            // Plateau tops: lighter with every tier, with the cover (moss, snow).
            if (tiers > 0f)
            {
                color *= 1f + tierLighten * tiers;
                color = Color.Lerp(color, topCover, topCover.a * Mathf.Clamp01(tiers));
            }

            // Cliff faces: rock (or rock layers), dark at the bottom, light at the top, some faces are lava cracks.
            if (cliff > 0f)
                color = Color.Lerp(color, GetCliffColor(tiers, cliffPosition, basePoint), cliff);

            float sink = GetSink(height);
            if (HasWater && pathHalfWidth > 0f)
                sink *= GetPathClearance(around, basePoint);

            if (sink > 0f)
                color = Color.Lerp(color, basinColor, basinColor.a * sink);

            float spread = Mathf.Lerp(brightnessSpread.x, brightnessSpread.y, TorusNoise.Random01(basePoint, 1));
            float sign = TorusNoise.Random01(basePoint, 2) < 0.5f ? -1f : 1f;
            color *= 1f + spread * sign;
            color.a = alpha;
            return color;
        }

        private Color GetCliffColor(float tiers, float cliffPosition, Vector3 basePoint)
        {
            if (lavaCrackChance > 0f && TorusNoise.Random01(basePoint, 3) < lavaCrackChance)
                return lavaColor;

            var dark = cliffDark;
            var light = cliffLight;
            if (strataColors != null && strataColors.Length > 0)
            {
                float rockHeight = (Mathf.Floor(tiers) + cliffPosition) * tierHeight;
                int layer = Mathf.FloorToInt(rockHeight / strataBand + seed);
                var layerColor = strataColors[((layer % strataColors.Length) + strataColors.Length) % strataColors.Length];
                dark = layerColor * 0.7f;
                light = layerColor * 1.05f;
            }

            return Color.Lerp(dark, light, 0.25f + 0.6f * cliffPosition);
        }

        /// <summary>0 above the shore, 1 at the bottom of a basin.</summary>
        private float GetSink(float height)
        {
            if (hasBasins == false)
                return 0f;

            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(basinLevel, basinLevel + shoreWidth, height));
        }

        /// <summary>
        /// Number of tiers at the point, with smooth (but steep) steps between them: 0 to tierCount.
        /// Cliff is 1 on a cliff face and 0 on flat ground; cliffPosition is the place on the face (0 bottom, 1 top).
        /// </summary>
        private float EvaluateTiers(float height, float around, Vector3 basePoint, out float cliff, out float cliffPosition)
        {
            cliff = 0f;
            cliffPosition = 0f;
            float allowed = GetDryness(height) * GetPathClearance(around, basePoint);
            if (allowed <= 0f)
                return 0f;

            float noise = TorusNoise.Spread(basePoint / plateauSize + new Vector3(seed * 1.7f, -seed, seed * 0.3f), NoiseOctaves);
            noise = Mathf.Lerp(-cliffSoftness, noise, allowed);
            float tiers = 0f;
            for (int i = 0; i < tierCount; i++)
            {
                float threshold = 1f - coverage * (tierCount - i) / tierCount;
                float step = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold, threshold + cliffSoftness, noise));
                tiers += step;

                float face = Mathf.Clamp01(Mathf.Min(step, 1f - step) * 10f);
                if (face > cliff)
                {
                    cliff = face;
                    cliffPosition = step;
                }
            }

            return tiers;
        }

        /// <summary>No plateaus in and right next to the basins.</summary>
        private float GetDryness(float height)
        {
            if (hasBasins == false)
                return 1f;

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(basinLevel + shoreWidth, basinLevel + shoreWidth * 4f, height));
        }

        /// <summary>0 on the path along the top of the tube, 1 away from it.</summary>
        private float GetPathClearance(float around, Vector3 basePoint)
        {
            if (pathHalfWidth <= 0f)
                return 1f;

            float theta = Mathf.Atan2(basePoint.x, basePoint.z);
            var direction = new Vector3(Mathf.Sin(theta), Mathf.Cos(theta), seed * 0.37f);
            float centre = 0.25f + pathWander * (TorusNoise.Fractal(direction * 2f, 2) * 2f - 1f) * 2f;
            float distance = Mathf.Abs(Mathf.Repeat(around - centre + 0.5f, 1f) - 0.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pathHalfWidth, pathHalfWidth * 1.6f, distance));
        }
    }
}
