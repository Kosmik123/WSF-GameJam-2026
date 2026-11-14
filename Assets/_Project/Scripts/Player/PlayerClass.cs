using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Class of the player character (port of the LD57 PlayerClass: warrior, mage): attack mode, health, skill pool
    /// and starting skills. Applied by <see cref="PlayerClassApplier"/>. The model of the class is not swapped yet.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Player Class", fileName = "PlayerClass")]
    public class PlayerClass : ScriptableObject
    {
        [SerializeField]
        private string displayName = "Class";
        public string DisplayName => displayName;

        [TextArea]
        [SerializeField]
        private string description;
        public string Description => description;

        [Header("Attack")]
        [Tooltip("True: the attack button shoots projectiles (RangedAttack) instead of the melee swing.")]
        [SerializeField]
        private bool isRanged;
        public bool IsRanged => isRanged;

        [Tooltip("Damage of one ranged shot.")]
        [SerializeField]
        [Min(1)]
        private int rangedDamage = 8;
        public int RangedDamage => rangedDamage;

        [Tooltip("Time between ranged shots in seconds.")]
        [SerializeField]
        [Min(0.05f)]
        private float rangedCooldown = 0.35f;
        public float RangedCooldown => rangedCooldown;

        [Header("Body")]
        [SerializeField]
        [Min(1)]
        private int maxHealth = 100;
        public int MaxHealth => maxHealth;

        [Header("Skills")]
        [Tooltip("Skills that level-up offers can give.")]
        [SerializeField]
        private SkillDefinition[] skillPool = new SkillDefinition[0];
        public IReadOnlyList<SkillDefinition> SkillPool => skillPool;

        [Tooltip("Skills owned from the start of every run, in slot order.")]
        [SerializeField]
        private SkillDefinition[] startingSkills = new SkillDefinition[0];
        public IReadOnlyList<SkillDefinition> StartingSkills => startingSkills;
    }
}
