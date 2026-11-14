using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Applies a <see cref="PlayerClass"/> to the player (port of the LD57 PlayerClassApplier, without reflection):
    /// melee or ranged attack, maximum health, skill pool and starting skills.
    /// Runs before the other components, so they start with the class already set.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PlayerCombat), typeof(Health))]
    public class PlayerClassApplier : MonoBehaviour
    {
        [SerializeField]
        private PlayerClass playerClass;

        public PlayerClass Current { get; private set; }

        private void Awake()
        {
            Apply(playerClass);
        }

        /// <summary>Applies the class. Resets the skills and fills the health. Safe to call during the game (class choice).</summary>
        public void Apply(PlayerClass newClass)
        {
            if (newClass == null)
                return;

            Current = newClass;
            GetComponent<PlayerCombat>().MeleeEnabled = newClass.IsRanged == false;

            if (TryGetComponent<RangedAttack>(out var rangedAttack))
            {
                rangedAttack.Configure(newClass.RangedDamage, newClass.RangedCooldown);
                rangedAttack.enabled = newClass.IsRanged;
            }

            if (TryGetComponent<SkillSlots>(out var skillSlots))
                skillSlots.SetPool(newClass.SkillPool, newClass.StartingSkills);

            GetComponent<Health>().SetMaxHealth(newClass.MaxHealth, true);
        }
    }
}
