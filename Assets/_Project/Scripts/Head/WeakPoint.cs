using System;
using System.Collections;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// The weak spot of the snake (GDD 6) and the goal of every round: destroying it wins the round.
    /// It stays on the body behind the jaw and moves with the head. It has hit points (<see cref="Health"/>, team of the enemies,
    /// so the attacks of the player hit it), shrinks as it loses them down to <see cref="minScale"/>, and flashes red when hurt
    /// (<see cref="BodyFlash"/>). When it dies it shrinks to nothing and <see cref="OnDestroyed"/> is raised.
    /// Models and animations are meant to be plugged into the visual and the animator.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    [RequireComponent(typeof(Health))]
    public class WeakPoint : MonoBehaviour
    {
        /// <summary>The weak point lost all its hit points. The round is won.</summary>
        public event Action OnDestroyed;

        [SerializeField]
        private HeadMovement head;

        [Tooltip("The weak point stands this far behind the pivot of the head (degrees of theta). More than the reach of the maw zone behind the head.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float behindHeadDegrees = 20f;

        [Tooltip("Where around the tube the weak point sits, in degrees. 90 = top.")]
        [SerializeField]
        [Range(0f, 360f)]
        private float phiDegrees = 90f;

        [Header("Look")]
        [Tooltip("Model of the weak point. It shrinks with the hit points. Empty = this object.")]
        [SerializeField]
        private Transform visual;

        [Tooltip("Scale at the last hit point, relative to the scale at full health (1). The weak point never gets smaller than this while it lives.")]
        [SerializeField]
        [Range(0.05f, 1f)]
        private float minScale = 0.4f;

        [Tooltip("How fast the scale follows the hit points, in full scales per second.")]
        [SerializeField]
        [Min(0.1f)]
        private float scaleSpeed = 3f;

        [Header("Hit and destruction")]
        [Tooltip("Optional animator of the weak point. Gets the triggers below.")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private string damagedTrigger = "Damaged";

        [SerializeField]
        private string destroyedTrigger = "Destroyed";

        [Tooltip("Freeze of the game when hurt (real seconds). 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float damageHitStopSeconds = 0.04f;

        [Tooltip("Freeze of the game when destroyed (real seconds). 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float destroyHitStopSeconds = 0.12f;

        [Tooltip("Time in which the destroyed weak point shrinks to nothing.")]
        [SerializeField]
        [Min(0.05f)]
        private float destroyDuration = 0.5f;

        private TorusObject torusObject;
        private Health health;
        private Vector3 baseScale;
        private float currentScale = 1f;
        private bool isDestroyed;

        /// <summary>Hit points of the weak point, 0-1.</summary>
        public float Normalized => health.Normalized;

        public bool IsDestroyed => isDestroyed;

        private void Reset()
        {
            head = FindAnyObjectByType<HeadMovement>();
        }

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
            health = GetComponent<Health>();
            if (visual == null)
                visual = transform;

            baseScale = visual.localScale;
        }

        private void OnEnable()
        {
            health.OnDamaged += Health_OnDamaged;
            health.OnDied += Health_OnDied;
            ApplyScale(1f);
        }

        /// <summary>Full hit points and full size again, for a new round.</summary>
        public void ResetState()
        {
            StopAllCoroutines();
            isDestroyed = false;
            health.ResetHealth();
            currentScale = 1f;
            ApplyScale(1f);
        }

        private void Update()
        {
            if (isDestroyed)
                return;

            float targetScale = Mathf.Lerp(minScale, 1f, health.Normalized);
            currentScale = Mathf.MoveTowards(currentScale, targetScale, scaleSpeed * Time.deltaTime);
            ApplyScale(currentScale);
        }

        private void LateUpdate()
        {
            if (head == null)
                return;

            float thetaDegrees = head.Theta * Mathf.Rad2Deg - behindHeadDegrees;
            torusObject.SetPosition(TorusAngles.FromDegrees(thetaDegrees, phiDegrees));
        }

        private void ApplyScale(float scale)
        {
            visual.localScale = baseScale * scale;
        }

        private void Health_OnDamaged(Health damagedHealth, DamageInfo damage)
        {
            if (animator && string.IsNullOrEmpty(damagedTrigger) == false)
                animator.SetTrigger(damagedTrigger);

            HitStop.Freeze(damageHitStopSeconds);
        }

        private void Health_OnDied(Health deadHealth)
        {
            if (isDestroyed)
                return;

            isDestroyed = true;
            if (animator && string.IsNullOrEmpty(destroyedTrigger) == false)
                animator.SetTrigger(destroyedTrigger);

            HitStop.Freeze(destroyHitStopSeconds);
            OnDestroyed?.Invoke();
            StartCoroutine(DestroyingCo());
        }

        private IEnumerator DestroyingCo()
        {
            float startScale = currentScale;
            float time = 0f;
            while (time < destroyDuration)
            {
                time += Time.deltaTime;
                currentScale = Mathf.Lerp(startScale, 0f, Mathf.Clamp01(time / destroyDuration));
                ApplyScale(currentScale);
                yield return null;
            }

            currentScale = 0f;
            ApplyScale(0f);
        }

        private void OnDisable()
        {
            health.OnDamaged -= Health_OnDamaged;
            health.OnDied -= Health_OnDied;
        }
    }
}
