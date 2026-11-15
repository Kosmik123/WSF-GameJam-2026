using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Progression
{
    /// <summary>
    /// Stat growth with the player level (GDD 9: health, damage, attack speed). Damage and attack speed go through
    /// <see cref="PlayerCombat"/>, so melee, the mage shot and skills all grow. A level up also heals the gained health.
    /// The base health is the one set by the class when the game starts.
    /// </summary>
    [RequireComponent(typeof(Experience), typeof(PlayerCombat), typeof(Health))]
    public class LevelStats : MonoBehaviour
    {
        [Tooltip("Extra maximum health per level above 1.")]
        [SerializeField]
        [Min(0)]
        private int healthPerLevel = 8;

        [Tooltip("Extra damage per level above 1, as a fraction (0.08 = +8%).")]
        [SerializeField]
        [Min(0f)]
        private float damagePerLevel = 0.08f;

        [Tooltip("Extra attack speed per level above 1, as a fraction.")]
        [SerializeField]
        [Min(0f)]
        private float attackSpeedPerLevel = 0.04f;

        private Experience experience;
        private PlayerCombat combat;
        private Health health;
        private int baseMaxHealth;

        private void Awake()
        {
            experience = GetComponent<Experience>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            experience.OnLevelUp += Experience_OnLevelUp;
            experience.OnProgressReset += Experience_OnProgressReset;
        }

        private void Start()
        {
            // After the class applier set the health of the class.
            baseMaxHealth = health.MaxHealth;
            Apply(false);
        }

        private void Experience_OnLevelUp(int level)
        {
            Apply(true);
        }

        private void Experience_OnProgressReset()
        {
            Apply(false);
            health.ResetHealth();
        }

        private void Apply(bool healGain)
        {
            int levelsGained = experience.Level - 1;
            combat.DamageMultiplier = 1f + damagePerLevel * levelsGained;
            combat.AttackSpeedMultiplier = 1f + attackSpeedPerLevel * levelsGained;

            int newMaxHealth = baseMaxHealth + healthPerLevel * levelsGained;
            int gained = newMaxHealth - health.MaxHealth;
            health.SetMaxHealth(newMaxHealth, false);
            if (healGain && gained > 0)
                health.Heal(gained);
        }

        private void OnDisable()
        {
            experience.OnLevelUp -= Experience_OnLevelUp;
            experience.OnProgressReset -= Experience_OnProgressReset;
        }
    }
}
