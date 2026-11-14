using System;
using System.Collections;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// The weak spot of the snake (GDD 6). It stays on the body right behind the jaw, exactly at the place where the lap is won
    /// (<see cref="LapTracker"/>), and moves with the head. When the player reaches it, the player attacks it:
    /// the player turns towards it, the game freezes for a moment and the spot pulses. Models and animations are meant to be plugged
    /// into the animators and the visual; until they exist the placeholder pulse is used.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class WeakPoint : MonoBehaviour
    {
        /// <summary>The player struck the weak point. Raised when the strike starts.</summary>
        public event Action OnStruck;

        [SerializeField]
        private HeadMovement head;

        [SerializeField]
        private LapTracker lapTracker;

        [SerializeField]
        private TorusObject player;

        [Tooltip("Where around the tube the weak point sits, in degrees. 90 = top.")]
        [SerializeField]
        [Range(0f, 360f)]
        private float phiDegrees = 90f;

        [Header("Strike")]
        [Tooltip("Model of the weak point. The placeholder pulse scales it. Empty = this object.")]
        [SerializeField]
        private Transform visual;

        [Tooltip("Optional animator of the weak point. Gets the trigger below at the strike.")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private string struckTrigger = "Struck";

        [Tooltip("Optional animator of the player. Gets the trigger below at the strike (the attack animation).")]
        [SerializeField]
        private Animator playerAnimator;

        [SerializeField]
        private string playerAttackTrigger = "AttackWeakPoint";

        [Tooltip("Length of the strike: the player turns and the weak point pulses.")]
        [SerializeField]
        [Min(0.05f)]
        private float strikeDuration = 0.5f;

        [Tooltip("Scale of the weak point at the peak of the pulse.")]
        [SerializeField]
        [Min(1f)]
        private float pulseScale = 1.6f;

        [Tooltip("Freeze of the game at the strike (real seconds). 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float hitStopSeconds = 0.08f;

        [Tooltip("Turning speed of the player towards the weak point, degrees per second.")]
        [SerializeField]
        [Min(0f)]
        private float playerTurnSpeedDegrees = 720f;

        private TorusObject torusObject;
        private Vector3 baseScale;
        private bool isStruck;

        private void Reset()
        {
            head = FindAnyObjectByType<HeadMovement>();
            lapTracker = FindAnyObjectByType<LapTracker>();

            var movement = FindAnyObjectByType<TorusPlayerMovement>();
            if (movement)
                player = movement.GetComponent<TorusObject>();
        }

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
            if (visual == null)
                visual = transform;

            baseScale = visual.localScale;
        }

        private void OnEnable()
        {
            lapTracker.OnLapCompleted += LapTracker_OnLapCompleted;
            isStruck = false;
            visual.localScale = baseScale;
        }

        private void LateUpdate()
        {
            if (head == null || lapTracker == null)
                return;

            var thetaDegrees = head.Theta * Mathf.Rad2Deg - lapTracker.GoalBehindHeadDegrees;
            torusObject.SetPosition(TorusAngles.FromDegrees(thetaDegrees, phiDegrees));
        }

        private void LapTracker_OnLapCompleted()
        {
            if (isStruck)
                return;

            isStruck = true;
            OnStruck?.Invoke();
            StartCoroutine(StrikingCo());
        }

        private IEnumerator StrikingCo()
        {
            if (animator && string.IsNullOrEmpty(struckTrigger) == false)
                animator.SetTrigger(struckTrigger);

            if (playerAnimator && string.IsNullOrEmpty(playerAttackTrigger) == false)
                playerAnimator.SetTrigger(playerAttackTrigger);

            HitStop.Freeze(hitStopSeconds);

            var time = 0f;
            while (time < strikeDuration)
            {
                time += Time.deltaTime;
                TurnPlayerTowardsSelf();

                var progress = Mathf.Clamp01(time / strikeDuration);
                var pulse = Mathf.Sin(progress * Mathf.PI);
                visual.localScale = baseScale * Mathf.Lerp(1f, pulseScale, pulse);
                yield return null;
            }

            visual.localScale = baseScale;
        }

        private void TurnPlayerTowardsSelf()
        {
            if (player == null)
                return;

            var offset = player.GetSurfaceOffsetTo(torusObject);
            if (offset.sqrMagnitude < 0.0001f)
                return;

            player.TurnTowards(offset, playerTurnSpeedDegrees * Mathf.Deg2Rad * Time.deltaTime);
        }

        private void OnDisable()
        {
            lapTracker.OnLapCompleted -= LapTracker_OnLapCompleted;
        }
    }
}
