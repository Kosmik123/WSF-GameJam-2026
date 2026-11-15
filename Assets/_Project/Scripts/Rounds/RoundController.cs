using System;
using System.Collections;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Head;
using WSFGameJam2026.Player;
using WSFGameJam2026.Skills;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Rounds
{
    /// <summary>
    /// The round loop (GDD 6). A round is one lap: the player starts in front of the jaw and runs to the place behind it.
    /// Winning starts the next round with a faster jaw and more, stronger enemies. A death ends the whole run:
    /// the record (completed rounds) is saved, and after a short delay the run starts again from round 1.
    /// The screens (round won, death) listen to the events of this class.
    /// </summary>
    public class RoundController : MonoBehaviour
    {
        private const string BestRoundsKey = "WSFGameJam2026.BestRounds";

        /// <summary>A round was won. The argument is its number.</summary>
        public event Action<int> OnRoundCompleted;

        /// <summary>A round started (also the first one, after a restart). The argument is its number.</summary>
        public event Action<int> OnRoundStarted;

        /// <summary>The player died. Arguments: rounds completed in this run, the best result, whether it is a new record.</summary>
        public event Action<int, int, bool> OnRunEnded;

        [SerializeField]
        private RoundState roundState;

        [SerializeField]
        private WeakPoint weakPoint;

        [SerializeField]
        private HeadMovement head;

        [SerializeField]
        private EnemySpawner spawner;

        [SerializeField]
        private PlayerCombat playerCombat;

        [Tooltip("Optional. Skills of the player, reset to the starting ones when a new run starts.")]
        [SerializeField]
        private SkillSlots playerSkills;

        [Header("Start of a round")]
        [Tooltip("The player starts this far in front of the head, in degrees of theta.")]
        [SerializeField]
        [Range(5f, 180f)]
        private float startGapDegrees = 60f;

        [Tooltip("Where around the tube the player starts. 90 = top.")]
        [SerializeField]
        [Range(0f, 360f)]
        private float startPhiDegrees = 90f;

        [Header("Difficulty")]
        [Tooltip("Speed of the jaw grows by this fraction of the base speed with every round (0.2 = +20%).")]
        [SerializeField]
        [Min(0f)]
        private float jawSpeedPerRound = 0.2f;

        [Tooltip("The enemy multiplier (level and number) grows by this much with every round.")]
        [SerializeField]
        [Min(0f)]
        private float enemiesPerRound = 0.25f;

        [Header("Pauses")]
        [Tooltip("Time between winning a round and the start of the next one.")]
        [SerializeField]
        [Min(0f)]
        private float roundBreakSeconds = 2.5f;

        [Tooltip("Part of the maximum health restored when a round is won (0-1).")]
        [SerializeField]
        [Range(0f, 1f)]
        private float healOnRoundCompleted = 0.5f;

        [Tooltip("Time between the death and the restart of the run (the death screen shows here).")]
        [SerializeField]
        [Min(0f)]
        private float deathDelaySeconds = 3f;

        private Health playerHealth;
        private TorusObject playerObject;

        /// <summary>Best number of completed rounds, saved between sessions.</summary>
        public int BestRounds => PlayerPrefs.GetInt(BestRoundsKey, 0);

        private void Reset()
        {
            roundState = FindAnyObjectByType<RoundState>();
            weakPoint = FindAnyObjectByType<WeakPoint>();
            head = FindAnyObjectByType<HeadMovement>();
            spawner = FindAnyObjectByType<EnemySpawner>();
            playerCombat = FindAnyObjectByType<PlayerCombat>();
            playerSkills = FindAnyObjectByType<SkillSlots>();
        }

        private void Awake()
        {
            playerHealth = playerCombat.GetComponent<Health>();
            playerObject = playerCombat.GetComponent<TorusObject>();
        }

        private void OnEnable()
        {
            weakPoint.OnDestroyed += WeakPoint_OnDestroyed;
            playerHealth.OnDied += PlayerHealth_OnDied;
        }

        private void Start()
        {
            BeginRound();
        }

        private void OnDisable()
        {
            weakPoint.OnDestroyed -= WeakPoint_OnDestroyed;
            playerHealth.OnDied -= PlayerHealth_OnDied;
        }

        private void WeakPoint_OnDestroyed()
        {
            StartCoroutine(RoundBreakCo());
        }

        private void PlayerHealth_OnDied(Health deadHealth)
        {
            // Dying during the break between rounds cancels the next round.
            StopAllCoroutines();
            StartCoroutine(RunEndedCo());
        }

        private IEnumerator RoundBreakCo()
        {
            OnRoundCompleted?.Invoke(roundState.Round);

            // The jaw waits and the player can not be hurt, so the break is safe.
            head.IsMoving = false;
            playerHealth.MakeInvulnerable(roundBreakSeconds);
            playerHealth.Heal(Mathf.CeilToInt(playerHealth.MaxHealth * healOnRoundCompleted));

            yield return new WaitForSeconds(roundBreakSeconds);

            roundState.StartNextRound();
            BeginRound();
        }

        private IEnumerator RunEndedCo()
        {
            head.IsMoving = false;

            var completed = roundState.CompletedRounds;
            var best = BestRounds;
            var isNewBest = completed > best;
            if (isNewBest)
            {
                best = completed;
                PlayerPrefs.SetInt(BestRoundsKey, best);
                PlayerPrefs.Save();
            }

            OnRunEnded?.Invoke(completed, best, isNewBest);

            yield return new WaitForSeconds(deathDelaySeconds);

            roundState.ResetRounds();
            playerCombat.Revive();
            if (playerSkills)
                playerSkills.ResetSkills();

            BeginRound();
        }

        // Puts the jaw and the player at the start, clears the enemies and sets the difficulty of the current round.
        private void BeginRound()
        {
            var roundsPlayed = roundState.Round - 1;

            head.SpeedMultiplier = 1f + jawSpeedPerRound * roundsPlayed;
            head.ResetPosition();
            head.IsMoving = true;

            spawner.RoundMultiplier = 1f + enemiesPerRound * roundsPlayed;
            spawner.Clear();
             
            var startTheta = head.Theta + startGapDegrees * Mathf.Deg2Rad;
            var startAngles = new TorusAngles(startTheta, startPhiDegrees * Mathf.Deg2Rad);
            var world = playerObject.World;
            var facing = world.transform.TransformDirection(TorusGeometry.GetThetaDirection(startAngles.Theta));
            playerObject.Place(world, startAngles, facing);

            weakPoint.ResetState();
            OnRoundStarted?.Invoke(roundState.Round);
        }
    }
}
