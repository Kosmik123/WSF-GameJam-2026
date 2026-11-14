using UnityEngine;

namespace WSFGameJam2026.Combat
{
    /// <summary>One hit: how much damage, where it came from and how far it pushes the target.</summary>
    public readonly struct DamageInfo
    {
        public readonly int Amount;

        /// <summary>World space direction of the push (along the surface). Zero = no direction.</summary>
        public readonly Vector3 Direction;

        /// <summary>Knockback distance in world units. 0 = no knockback.</summary>
        public readonly float Knockback;

        public readonly CombatTeam SourceTeam;

        public DamageInfo(int amount, Vector3 direction, float knockback, CombatTeam sourceTeam)
        {
            Amount = amount;
            Direction = direction;
            Knockback = knockback;
            SourceTeam = sourceTeam;
        }
    }
}
