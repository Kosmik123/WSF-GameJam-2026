using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;

namespace WSFGameJam2026.Rounds
{
    /// <summary>
    /// Numbers of the current round and run for the round screens: lap time, enemies killed by the player,
    /// the closest the jaw got and what killed the player (the jaw or a fight).
    /// </summary>
    public class RunStats : MonoBehaviour
    {
        [SerializeField]
        private RoundController roundController;

        [Tooltip("Optional. Measures how close the jaw got.")]
        [SerializeField]
        private LapTracker lapTracker;

        [SerializeField]
        private Health playerHealth;

        private float roundStartTime;
        private bool isRoundRunning;
        private bool isNewRun = true;
        private int lastCombatHitFrame = -1;

        [Header("States")]
        [SerializeField]
        private float lastLapSeconds;

        [SerializeField]
        private int roundKills;
        public int RoundKills => roundKills;

        [SerializeField]
        private int runKills;
        public int RunKills => runKills;

        [SerializeField]
        private float closestJawMeters;

        [SerializeField]
        private bool wasLastLossFromCombat;

        /// <summary>Time of the current round, or of the last won round between rounds.</summary>
        public float LapSeconds => isRoundRunning ? Time.time - roundStartTime : lastLapSeconds;

        /// <summary>The smallest lead the player had over the jaw in this round, in metres.</summary>
        public float ClosestJawMeters => closestJawMeters < float.MaxValue ? closestJawMeters : 0f;

        /// <summary>True when the last health the player lost was taken by the jaw (or anything but a hit).</summary>
        public bool WasKilledByJaw => wasLastLossFromCombat == false;

        private void Reset()
        {
            roundController = FindAnyObjectByType<RoundController>();
            lapTracker = FindAnyObjectByType<LapTracker>();
            foreach (var health in FindObjectsByType<Health>())
            {
                if (health.Team == CombatTeam.Player)
                    playerHealth = health;
            }
        }

        private void OnEnable()
        {
            closestJawMeters = float.MaxValue;
            Enemy.OnAnyEnemyDamaged += Enemy_OnAnyEnemyDamaged;
            if (roundController)
            {
                roundController.OnRoundStarted += RoundController_OnRoundStarted;
                roundController.OnRoundCompleted += RoundController_OnRoundCompleted;
                roundController.OnRunEnded += RoundController_OnRunEnded;
            }

            if (playerHealth)
            {
                playerHealth.OnDamaged += PlayerHealth_OnDamaged;
                playerHealth.OnHealthChanged += PlayerHealth_OnHealthChanged;
            }
        }

        private void Update()
        {
            if (isRoundRunning && lapTracker && lapTracker.IsRunning)
                closestJawMeters = Mathf.Min(closestJawMeters, Mathf.Max(0f, lapTracker.MetersAheadOfHead));
        }

        private void RoundController_OnRoundStarted(int round)
        {
            if (isNewRun)
                runKills = 0;

            isNewRun = false;
            isRoundRunning = true;
            roundStartTime = Time.time;
            roundKills = 0;
            closestJawMeters = float.MaxValue;
        }

        private void RoundController_OnRoundCompleted(int round)
        {
            lastLapSeconds = Time.time - roundStartTime;
            isRoundRunning = false;
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            isRoundRunning = false;
            isNewRun = true;
        }

        private void Enemy_OnAnyEnemyDamaged(Enemy enemy, DamageInfo damage)
        {
            if (damage.SourceTeam != CombatTeam.Player || enemy.Health.IsDead == false)
                return;

            roundKills++;
            runKills++;
        }

        // A hit raises OnDamaged right before OnHealthChanged; the jaw only changes the health.
        private void PlayerHealth_OnDamaged(Health health, DamageInfo damage)
        {
            lastCombatHitFrame = Time.frameCount;
        }

        private void PlayerHealth_OnHealthChanged(int change)
        {
            if (change < 0)
                wasLastLossFromCombat = lastCombatHitFrame == Time.frameCount;
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyDamaged -= Enemy_OnAnyEnemyDamaged;
            if (roundController)
            {
                roundController.OnRoundStarted -= RoundController_OnRoundStarted;
                roundController.OnRoundCompleted -= RoundController_OnRoundCompleted;
                roundController.OnRunEnded -= RoundController_OnRunEnded;
            }

            if (playerHealth)
            {
                playerHealth.OnDamaged -= PlayerHealth_OnDamaged;
                playerHealth.OnHealthChanged -= PlayerHealth_OnHealthChanged;
            }
        }
    }
}
