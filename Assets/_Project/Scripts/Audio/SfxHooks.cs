using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// Bridge from game events to sound effects (port of the LD57 SfxHooks): player attacks, dodge, casts, damage and death,
    /// enemy hits and deaths, round results. Scene component (the "Audio" object). Enemy windups are in <see cref="EnemySfx"/>.
    /// </summary>
    public class SfxHooks : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField]
        private PlayerCombat playerCombat;

        [SerializeField]
        private RangedAttack rangedAttack;

        [SerializeField]
        private SkillUser skillUser;

        [SerializeField]
        private Health playerHealth;

        [SerializeField]
        private RoundController roundController;

        [SerializeField]
        private PlayerLevel playerLevel;

        [Header("Player")]
        [SerializeField]
        private SoundEffect swordSwing;

        [SerializeField]
        private SoundEffect dodge;

        [SerializeField]
        private SoundEffect rangedShot;

        [SerializeField]
        private SoundEffect skillCast;

        [SerializeField]
        private SoundEffect playerHurt;

        [SerializeField]
        private SoundEffect playerDeath;

        [SerializeField]
        private SoundEffect levelUp;

        [Header("Enemies")]
        [SerializeField]
        private SoundEffect enemyHit;

        [SerializeField]
        private SoundEffect enemyDie;

        [Header("Rounds")]
        [SerializeField]
        private SoundEffect roundCompleted;

        [Tooltip("Played when the run ends with a new best result.")]
        [SerializeField]
        private SoundEffect newRecord;

        private Vector3 PlayerPosition => playerCombat ? playerCombat.transform.position : Vector3.zero;

        private void Reset()
        {
            playerCombat = FindAnyObjectByType<PlayerCombat>();
            roundController = FindAnyObjectByType<RoundController>();
            if (playerCombat == null)
                return;

            rangedAttack = playerCombat.GetComponent<RangedAttack>();
            skillUser = playerCombat.GetComponent<SkillUser>();
            playerHealth = playerCombat.GetComponent<Health>();
            playerLevel = playerCombat.GetComponent<PlayerLevel>();
        }

        private void OnEnable()
        {
            if (playerCombat)
            {
                playerCombat.OnAttackStarted += PlayerCombat_OnAttackStarted;
                playerCombat.OnDodgeStarted += PlayerCombat_OnDodgeStarted;
            }

            if (rangedAttack)
                rangedAttack.OnShot += RangedAttack_OnShot;

            if (skillUser)
                skillUser.OnSkillExecuted += SkillUser_OnSkillExecuted;

            if (playerHealth)
            {
                playerHealth.OnHealthChanged += PlayerHealth_OnHealthChanged;
                playerHealth.OnDied += PlayerHealth_OnDied;
            }

            if (roundController)
            {
                roundController.OnRoundCompleted += RoundController_OnRoundCompleted;
                roundController.OnRunEnded += RoundController_OnRunEnded;
            }

            if (playerLevel)
                playerLevel.OnLevelUp += PlayerLevel_OnLevelUp;

            Enemy.OnAnyEnemyDamaged += Enemy_OnAnyEnemyDamaged;
            Enemy.OnAnyEnemyDied += Enemy_OnAnyEnemyDied;
        }

        private void PlayerCombat_OnAttackStarted(int comboStep)
        {
            Sfx.Play(swordSwing, PlayerPosition);
        }

        private void PlayerCombat_OnDodgeStarted()
        {
            Sfx.Play(dodge, PlayerPosition);
        }

        private void RangedAttack_OnShot()
        {
            Sfx.Play(rangedShot, PlayerPosition);
        }

        private void SkillUser_OnSkillExecuted(SkillInstance skill)
        {
            Sfx.Play(skillCast, PlayerPosition);
        }

        // Health changes cover every kind of damage (hits, the maw, direct losses), not only TakeDamage.
        private void PlayerHealth_OnHealthChanged(int change)
        {
            if (change < 0)
                Sfx.Play(playerHurt, PlayerPosition);
        }

        private void PlayerHealth_OnDied(Health health)
        {
            Sfx.Play(playerDeath, PlayerPosition);
        }

        private void PlayerLevel_OnLevelUp(int level)
        {
            Sfx.Play(levelUp);
        }

        private void Enemy_OnAnyEnemyDamaged(Enemy enemy, DamageInfo damage)
        {
            if (enemy)
                Sfx.Play(enemyHit, enemy.transform.position);
        }

        private void Enemy_OnAnyEnemyDied(Enemy enemy)
        {
            if (enemy)
                Sfx.Play(enemyDie, enemy.transform.position);
        }

        private void RoundController_OnRoundCompleted(int round)
        {
            Sfx.Play(roundCompleted);
        }

        private void RoundController_OnRunEnded(int roundsCompleted, int bestRounds, bool isNewRecord)
        {
            if (isNewRecord)
                Sfx.Play(newRecord);
        }

        private void OnDisable()
        {
            if (playerCombat)
            {
                playerCombat.OnAttackStarted -= PlayerCombat_OnAttackStarted;
                playerCombat.OnDodgeStarted -= PlayerCombat_OnDodgeStarted;
            }

            if (rangedAttack)
                rangedAttack.OnShot -= RangedAttack_OnShot;

            if (skillUser)
                skillUser.OnSkillExecuted -= SkillUser_OnSkillExecuted;

            if (playerHealth)
            {
                playerHealth.OnHealthChanged -= PlayerHealth_OnHealthChanged;
                playerHealth.OnDied -= PlayerHealth_OnDied;
            }

            if (roundController)
            {
                roundController.OnRoundCompleted -= RoundController_OnRoundCompleted;
                roundController.OnRunEnded -= RoundController_OnRunEnded;
            }

            if (playerLevel)
                playerLevel.OnLevelUp -= PlayerLevel_OnLevelUp;

            Enemy.OnAnyEnemyDamaged -= Enemy_OnAnyEnemyDamaged;
            Enemy.OnAnyEnemyDied -= Enemy_OnAnyEnemyDied;
        }
    }
}
