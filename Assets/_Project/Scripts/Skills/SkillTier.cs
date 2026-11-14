using System;
using UnityEngine;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Numbers of one level of a skill (port of the LD57 SkillTier). Tier 0 is the base, 1 and 2 are upgrades, 3 is the evolution.
    /// What Range and Duration mean depends on the skill (see its tooltip in the skill asset).
    /// </summary>
    [Serializable]
    public class SkillTier
    {
        [SerializeField]
        private string tierName = "Base";
        public string TierName => tierName;

        [TextArea]
        [SerializeField]
        private string description;
        public string Description => description;

        [SerializeField]
        [Min(0f)]
        private float cooldown = 5f;
        public float Cooldown => cooldown;

        [SerializeField]
        [Min(0)]
        private int damage = 10;
        public int Damage => damage;

        [Tooltip("Radius or distance in world units. The skill decides which one.")]
        [SerializeField]
        [Min(0f)]
        private float range = 3f;
        public float Range => range;

        [Tooltip("Time of the effect in seconds (slow, orbit, burning, invulnerability...).")]
        [SerializeField]
        [Min(0f)]
        private float duration = 1f;
        public float Duration => duration;

        [Tooltip("Knockback distance in world units, 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float knockback;
        public float Knockback => knockback;

        [Tooltip("Stun time in seconds, 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float stun;
        public float Stun => stun;
    }
}
