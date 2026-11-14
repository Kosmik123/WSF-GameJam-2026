using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Player combat based on the LD57 PlayerController: a melee swing in an arc in front of the player
    /// (short windup, active window, recovery, then a combo step) and a dodge (dash with invulnerability).
    /// Hits are found by distance on the torus surface, not by colliders.
    /// </summary>
    [RequireComponent(typeof(TorusPlayerMovement), typeof(Health))]
    public class PlayerCombat : MonoBehaviour
    {
        private enum State
        {
            Ready,
            Windup,
            Active,
            Recovery,
            Dodge,
            Dead,
        }

        /// <summary>A swing started. The argument is the combo step (0, 1, 2...). For animations and sounds.</summary>
        public event Action<int> OnAttackStarted;

        /// <summary>The swing hit at least one target.</summary>
        public event Action<int> OnAttackHit;

        public event Action OnDodgeStarted;
        public event Action OnDodgeEnded;

        [SerializeField]
        private InputActionReference attackAction;

        [SerializeField]
        private InputActionReference dodgeAction;

        [Header("Attack")]
        [SerializeField]
        [Min(1)]
        private int damage = 10;

        [SerializeField]
        [Min(0f)]
        private float range = 2.2f;

        [Tooltip("Full angle of the swing, in degrees.")]
        [SerializeField]
        [Range(10f, 360f)]
        private float arcDegrees = 140f;

        [SerializeField]
        [Min(0f)]
        private float windup = 0.06f;

        [Tooltip("Time in which the swing deals damage.")]
        [SerializeField]
        [Min(0.01f)]
        private float activeTime = 0.12f;

        [SerializeField]
        [Min(0f)]
        private float recovery = 0.18f;

        [SerializeField]
        [Min(0f)]
        private float knockback = 1.5f;

        [Tooltip("Move speed multiplier from the start of the swing to the end of the recovery.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float moveSpeedWhileAttacking = 0.35f;

        [Tooltip("Small step forward at the start of every swing, in units.")]
        [SerializeField]
        [Min(0f)]
        private float attackLunge = 0.4f;

        [Header("Combo")]
        [Tooltip("Number of swings in a combo. The last one is stronger.")]
        [SerializeField]
        [Min(1)]
        private int comboLength = 3;

        [SerializeField]
        [Min(1f)]
        private float comboFinisherMultiplier = 1.5f;

        [Tooltip("Time after the recovery in which the next press continues the combo.")]
        [SerializeField]
        [Min(0f)]
        private float comboWindow = 0.35f;

        [Tooltip("Presses during a swing are remembered for this long.")]
        [SerializeField]
        [Min(0f)]
        private float inputBuffer = 0.2f;

        [Header("Dodge")]
        [SerializeField]
        [Min(0f)]
        private float dodgeDistance = 5f;

        [SerializeField]
        [Min(0.01f)]
        private float dodgeDuration = 0.22f;

        [SerializeField]
        [Min(0f)]
        private float dodgeCooldown = 0.6f;

        [Tooltip("Extra invulnerability after the dodge ends.")]
        [SerializeField]
        [Min(0f)]
        private float dodgeInvulnerabilityTail = 0.08f;

        [Header("Taking damage")]
        [Tooltip("Invulnerability after a hit, so many enemies cannot hit the player in the same moment.")]
        [SerializeField]
        [Min(0f)]
        private float invulnerabilityAfterHit = 0.5f;

        private readonly List<Health> targetsBuffer = new List<Health>();
        private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

        private TorusPlayerMovement movement;
        private TorusObject torusObject;
        private Health health;
        private Knockback knockbackComponent;

        private State state = State.Ready;
        private float stateEndTime;
        private int comboStep;
        private float comboResetTime;
        private float attackBufferedUntil = -1f;
        private float dodgeBufferedUntil = -1f;
        private float nextDodgeTime;
        private Vector3 dodgeDirection;
        private bool swingHit;

        /// <summary>Damage multiplier for progression (levels, items). 1 = base damage.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Attack speed multiplier for progression. 2 = swings twice as fast.</summary>
        public float AttackSpeedMultiplier { get; set; } = 1f;

        public bool IsAttacking => state == State.Windup || state == State.Active || state == State.Recovery;
        public bool IsDodging => state == State.Dodge;
        public float DodgeCooldownLeft => Mathf.Max(0f, nextDodgeTime - Time.time);

        private void Awake()
        {
            movement = GetComponent<TorusPlayerMovement>();
            torusObject = GetComponent<TorusObject>();
            health = GetComponent<Health>();
            knockbackComponent = GetComponent<Knockback>();
        }

        private void OnEnable()
        {
            if (attackAction != null)
                attackAction.action.performed += AttackAction_Performed;
            if (dodgeAction != null)
                dodgeAction.action.performed += DodgeAction_Performed;

            health.OnDamaged += Health_OnDamaged;
            health.OnDied += Health_OnDied;
        }

        private void OnDisable()
        {
            if (attackAction != null)
                attackAction.action.performed -= AttackAction_Performed;
            if (dodgeAction != null)
                dodgeAction.action.performed -= DodgeAction_Performed;

            health.OnDamaged -= Health_OnDamaged;
            health.OnDied -= Health_OnDied;
            EnterReady();
        }

        /// <summary>Restores control after the death (restart of the run).</summary>
        public void Revive()
        {
            health.ResetHealth();
            comboStep = 0;
            EnterReady();
        }

        private void AttackAction_Performed(InputAction.CallbackContext context)
        {
            attackBufferedUntil = Time.time + inputBuffer;
        }

        private void DodgeAction_Performed(InputAction.CallbackContext context)
        {
            dodgeBufferedUntil = Time.time + inputBuffer;
        }

        private void Health_OnDamaged(Health damagedHealth, DamageInfo damage)
        {
            health.MakeInvulnerable(invulnerabilityAfterHit);
        }

        private void Health_OnDied(Health deadHealth)
        {
            state = State.Dead;
            movement.IsLocked = true;
            health.IsInvulnerable = false;
        }

        private void Update()
        {
            if (state == State.Dead)
                return;

            // Being pushed by a hit takes over the movement.
            bool isKnockedBack = knockbackComponent != null && knockbackComponent.IsActive;
            movement.IsLocked = state == State.Dodge || isKnockedBack;

            switch (state)
            {
                case State.Ready:
                    UpdateReady();
                    break;

                case State.Windup:
                    if (Time.time >= stateEndTime)
                        EnterActive();
                    break;

                case State.Active:
                    DealDamage();
                    if (Time.time >= stateEndTime)
                        EnterRecovery();
                    break;

                case State.Recovery:
                    // A dodge cancels the recovery, so the player can always react.
                    if (TryDodge())
                        break;
                    if (Time.time >= stateEndTime)
                        EnterReady();
                    break;

                case State.Dodge:
                    UpdateDodge();
                    break;
            }
        }

        private void UpdateReady()
        {
            if (TryDodge())
                return;

            if (Time.time < attackBufferedUntil)
            {
                attackBufferedUntil = -1f;
                StartSwing();
            }
        }

        private void StartSwing()
        {
            if (Time.time > comboResetTime)
                comboStep = 0;

            swingHit = false;
            hitThisSwing.Clear();
            movement.SpeedMultiplier = moveSpeedWhileAttacking;
            if (attackLunge > 0f)
                torusObject.Move(attackLunge);

            OnAttackStarted?.Invoke(comboStep);
            SetState(State.Windup, windup);
        }

        private void EnterActive()
        {
            SetState(State.Active, activeTime);
            DealDamage();
        }

        private void EnterRecovery()
        {
            if (swingHit)
                OnAttackHit?.Invoke(comboStep);

            comboStep = (comboStep + 1) % comboLength;
            comboResetTime = Time.time + recovery / AttackSpeedMultiplier + comboWindow;
            SetState(State.Recovery, recovery);
        }

        private void EnterReady()
        {
            state = State.Ready;
            movement.SpeedMultiplier = 1f;
            movement.IsLocked = false;
            health.IsInvulnerable = false;
        }

        private void SetState(State newState, float duration)
        {
            state = newState;
            float speed = newState == State.Dodge ? 1f : Mathf.Max(0.1f, AttackSpeedMultiplier);
            stateEndTime = Time.time + duration / speed;
        }

        private void DealDamage()
        {
            targetsBuffer.Clear();
            CombatQueries.FindInArc(torusObject, health.Team, range, arcDegrees, targetsBuffer);

            bool isFinisher = comboStep == comboLength - 1 && comboLength > 1;
            float multiplier = DamageMultiplier * (isFinisher ? comboFinisherMultiplier : 1f);
            int amount = Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
            float push = knockback * (isFinisher ? 2f : 1f);

            for (int i = 0; i < targetsBuffer.Count; i++)
            {
                var target = targetsBuffer[i];
                if (hitThisSwing.Add(target) == false)
                    continue;

                var direction = torusObject.GetSurfaceOffsetTo(target.TorusObject);
                if (direction.sqrMagnitude < 0.0001f)
                    direction = torusObject.Facing;

                if (target.TakeDamage(new DamageInfo(amount, direction.normalized, push, health.Team)))
                    swingHit = true;
            }
        }

        private bool TryDodge()
        {
            if (Time.time >= dodgeBufferedUntil || Time.time < nextDodgeTime)
                return false;

            dodgeBufferedUntil = -1f;
            attackBufferedUntil = -1f;

            // Tank controls: the dodge goes back when the player holds back, otherwise forward.
            float sign = movement.SmoothedInput.y < -0.1f ? -1f : 1f;
            dodgeDirection = torusObject.Facing * sign;

            nextDodgeTime = Time.time + dodgeDuration + dodgeCooldown;
            health.IsInvulnerable = true;
            movement.SpeedMultiplier = 1f;
            movement.IsLocked = true;
            SetState(State.Dodge, dodgeDuration);
            OnDodgeStarted?.Invoke();
            return true;
        }

        private void UpdateDodge()
        {
            torusObject.MoveInDirection(dodgeDirection, dodgeDistance / dodgeDuration * Time.deltaTime);
            if (Time.time < stateEndTime)
                return;

            EnterReady();
            health.MakeInvulnerable(dodgeInvulnerabilityTail);
            OnDodgeEnded?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            var up = transform.up;
            var forward = transform.forward;
            var left = Quaternion.AngleAxis(-arcDegrees * 0.5f, up) * forward;
            var right = Quaternion.AngleAxis(arcDegrees * 0.5f, up) * forward;
            Gizmos.DrawLine(transform.position, transform.position + left * range);
            Gizmos.DrawLine(transform.position, transform.position + right * range);
            Gizmos.DrawLine(transform.position + left * range, transform.position + forward * range);
            Gizmos.DrawLine(transform.position + forward * range, transform.position + right * range);
        }
    }
}
