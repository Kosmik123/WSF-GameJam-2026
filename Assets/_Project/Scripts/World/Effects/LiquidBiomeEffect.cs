using UnityEngine;
using UnityEngine.Serialization;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Liquid of a biome (water of the coast and the swamp, lava of the volcano): where the terrain is lower than the liquid level,
    /// walking is slower and the dodge does not work. The slowdown grows with the depth, from none at the liquid level to the full one
    /// at 'Depth Of Full Slowdown' below it. Every liquid is its own asset of this type.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Liquid Biome Effect")]
    public class LiquidBiomeEffect : BiomeEffect
    {
        [Tooltip("Terrain height (the same units as the height of the terrain, above the base tube) below which the place is under the liquid.")]
        [FormerlySerializedAs("liquidLevel")]
        [SerializeField]
        private float liquidLevel;
        public float LiquidLevel => liquidLevel;

        [Tooltip("Multiplier of the speed in the deep liquid. 1 = no slowdown, 0.5 = half the speed.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float speedMultiplier = 0.5f;

        [Tooltip("How far below the liquid level the full slowdown is reached. 0 = the full slowdown at once at the edge of the liquid.")]
        [SerializeField]
        [Min(0f)]
        private float depthOfFullSlowdown = 0.5f;

        [Tooltip("The dodge (the dash) does not work in the liquid, that is where the terrain is lower than the liquid level.")]
        [SerializeField]
        private bool blocksDodge = true;

        public override bool CanDodge(float terrainHeight)
        {
            return blocksDodge == false || terrainHeight >= liquidLevel;
        }

        public override float GetSpeedMultiplier(float terrainHeight)
        {
            float depth = liquidLevel - terrainHeight;
            if (depth <= 0f)
                return 1f;

            float fullness = depthOfFullSlowdown <= 0.0001f ? 1f : Mathf.Clamp01(depth / depthOfFullSlowdown);
            return Mathf.Lerp(1f, speedMultiplier, fullness);
        }
    }
}
