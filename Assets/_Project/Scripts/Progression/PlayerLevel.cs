using System;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Progression
{
    /// <summary>
    /// Experience and level of the player (GDD 9). Enemies killed by the player give experience; every level adds
    /// health and damage. The level stays between rounds and is lost with the run: it resets when the next run starts
    /// (not at the death, so the death screen can still show it).
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerCombat))]
    public class PlayerLevel : MonoBehaviour
    {
        public delegate void ExperienceChangedHandler(int experience, int experienceToNextLevel);

        /// <summary>Experience changed (also after a level up or a reset).</summary>
        public event ExperienceChangedHandler OnExperienceChanged;

        /// <summary>The player reached a new level. The argument is the new level.</summary>
        public event Action<int> OnLevelUp;

        /// <summary>The level went back to 1 at the start of a new run.</summary>
        public event Action OnLevelReset;

        [Tooltip("Optional. Without it the level is never reset.")]
        [SerializeField]
        private RoundController roundController;

        [Header("Experience")]
        [Tooltip("Experience needed for level 2.")]
        [SerializeField]
        [Min(1)]
        private int firstLevelExperience = 60;

        [Tooltip("Every next level needs this many times more experience than the one before.")]
        [SerializeField]
        [Min(1f)]
        private float experienceGrowth = 1.35f;

        [Header("Bonus per level")]
        [SerializeField]
        [Min(0)]
        private int healthPerLevel = 12;
        public int HealthPerLevel => healthPerLevel;

        [Tooltip("Added to the damage multiplier of the player (0.06 = +6% damage).")]
        [SerializeField]
        [Min(0f)]
        private float damagePerLevel = 0.06f;
        public float DamagePerLevel => damagePerLevel;

        private Health health;
        private PlayerCombat playerCombat;
        private int bonusHealth;
        private float bonusDamage;
        private bool shouldResetOnNextRound;

        [Header("States")]
        [SerializeField]
        private int level = 1;
        public int Level => level;

        [SerializeField]
        private int experience;
        public int Experience => experience;

        public int ExperienceToNextLevel => Mathf.Max(1, Mathf.RoundToInt(firstLevelExperience * Mathf.Pow(experienceGrowth, level - 1)));

        /// <summary>Progress to the next level, 0-1.</summary>
        public float Normalized => Mathf.Clamp01((float)experience / ExperienceToNextLevel);

        private void Reset()
        {
            roundController = FindAnyObjectByType<RoundController>();
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            playerCombat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyDamaged += Enemy_OnAnyEnemyDamaged;
            if (roundController == null)
                return;

            roundController.OnRunEnded += RoundController_OnRunEnded;
            roundController.OnRoundStarted += RoundController_OnRoundStarted;
        }

        /// <summary>Adds experience; every full threshold gives a level.</summary>
        public void AddExperience(int amount)
        {
            if (amount <= 0 || health.IsDead)
                return;

            experience += amount;
            while (experience >= ExperienceToNextLevel)
            {
                experience -= ExperienceToNextLevel;
                GainLevel();
            }

            OnExperienceChanged?.Invoke(experience, ExperienceToNextLevel);
        }

        private void GainLevel()
        {
            level++;
            bonusHealth += healthPerLevel;
            health.SetMaxHealth(health.MaxHealth + healthPerLevel, false);
            health.Heal(healthPerLevel);
            bonusDamage += damagePerLevel;
            playerCombat.DamageMultiplier += damagePerLevel;
            OnLevelUp?.Invoke(level);
        }

        private void ResetLevel()
        {
            shouldResetOnNextRound = false;
            if (bonusHealth > 0)
                health.SetMaxHealth(health.MaxHealth - bonusHealth, true);

            playerCombat.DamageMultiplier -= bonusDamage;
            bonusHealth = 0;
            bonusDamage = 0f;
            level = 1;
            experience = 0;
            OnLevelReset?.Invoke();
            OnExperienceChanged?.Invoke(experience, ExperienceToNextLevel);
        }

        private void Enemy_OnAnyEnemyDamaged(Enemy enemy, DamageInfo damage)
        {
            // The jaw kills enemies too; only kills by the player count.
            if (damage.SourceTeam == CombatTeam.Player && enemy.Health.IsDead)
                AddExperience(enemy.ExperienceReward);
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            shouldResetOnNextRound = true;
        }

        private void RoundController_OnRoundStarted(int round)
        {
            if (shouldResetOnNextRound)
                ResetLevel();
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyDamaged -= Enemy_OnAnyEnemyDamaged;
            if (roundController == null)
                return;

            roundController.OnRunEnded -= RoundController_OnRunEnded;
            roundController.OnRoundStarted -= RoundController_OnRoundStarted;
        }
    }
}
