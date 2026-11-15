using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Water of a biome (the coast, the swamp): where the terrain is lower than the water level, walking is slower.
    /// The slowdown grows with the depth, from none at the water level to the full one at 'Depth Of Full Slowdown' below it.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Water Biome Effect")]
    public class WaterBiomeEffect : BiomeEffect
    {
        [Tooltip("Terrain height (the same units as the height of the terrain, above the base tube) below which the place is under water.")]
        [SerializeField]
        private float waterLevel;
        public float WaterLevel => waterLevel;

        [Tooltip("Multiplier of the speed in deep water. 1 = no slowdown, 0.5 = half the speed.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float speedMultiplier = 0.5f;

        [Tooltip("How far below the water level the full slowdown is reached. 0 = the full slowdown at once at the edge of the water.")]
        [SerializeField]
        [Min(0f)]
        private float depthOfFullSlowdown = 0.5f;

        public override float GetSpeedMultiplier(float terrainHeight)
        {
            float depth = waterLevel - terrainHeight;
            if (depth <= 0f)
                return 1f;

            float fullness = depthOfFullSlowdown <= 0.0001f ? 1f : Mathf.Clamp01(depth / depthOfFullSlowdown);
            return Mathf.Lerp(1f, speedMultiplier, fullness);
        }
    }
}
