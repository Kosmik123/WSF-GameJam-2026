using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Special effect of a biome (a <see cref="TorusSegmentProfile"/> has at most one, none = no effect). Derive from it to add a new one,
    /// for example slowing in water or damage in lava. The torus objects ask the surface what the effect of the place they stand on does.
    /// </summary>
    public abstract class BiomeEffect : ScriptableObject
    {
        /// <summary>Multiplier of the walking speed of an object that stands on the terrain of the given height. 1 = no change.</summary>
        public virtual float GetSpeedMultiplier(float terrainHeight)
        {
            return 1f;
        }

        /// <summary>False when the dodge (the dash) does not work on the terrain of the given height.</summary>
        public virtual bool CanDodge(float terrainHeight)
        {
            return true;
        }
    }
}
