using System;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Base of enemy AI on the torus. Gives access to the enemy, its target and movement helpers.
    /// <see cref="Enemy"/> disables all behaviours on death.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public abstract class EnemyBehaviour : MonoBehaviour
    {
        /// <summary>The telegraph of an attack started (windup). For animations.</summary>
        public event Action OnWindupStarted;

        /// <summary>The attack was released (lunge, shot). For animations.</summary>
        public event Action OnAttackReleased;

        [Header("Movement")]
        [SerializeField]
        [Min(0f)]
        private float moveSpeed = 4f;

        [Tooltip("Turning speed in degrees per second.")]
        [SerializeField]
        [Min(0f)]
        private float turnSpeedDegrees = 360f;

        [Tooltip("Target further than this (along the surface) is not noticed.")]
        [SerializeField]
        [Min(0f)]
        private float detectRange = 18f;
        protected float DetectRange => detectRange;

        private Knockback knockback;
        private StatusEffects statusEffects;
        private BodyFlash bodyFlash;
        protected BodyFlash BodyFlash => bodyFlash;

        protected Enemy Enemy { get; private set; }
        protected TorusObject TorusObject { get; private set; }
        protected Health Health { get; private set; }

        protected Health Target => Enemy.Target;

        /// <summary>The enemy can act: alive, after the spawn delay, not being pushed, not stunned, with a living target.</summary>
        protected bool CanAct => Enemy.IsActive
            && (knockback == null || knockback.IsActive == false)
            && (statusEffects == null || statusEffects.IsStunned == false)
            && Target && Target.IsDead == false;

        /// <summary>Slow from skills (frost nova). 1 = full speed.</summary>
        private float StatusSpeedMultiplier => statusEffects ? statusEffects.SpeedMultiplier : 1f;

        protected virtual void Awake()
        {
            Enemy = GetComponent<Enemy>();
            TorusObject = GetComponent<TorusObject>();
            Health = GetComponent<Health>();
            TryGetComponent(out bodyFlash);
            TryGetComponent(out knockback);
            TryGetComponent(out statusEffects);
        }

        protected void RaiseWindupStarted()
        {
            OnWindupStarted?.Invoke();
        }

        protected void RaiseAttackReleased()
        {
            OnAttackReleased?.Invoke();
        }

        /// <summary>Surface offset from this enemy to the target (see <see cref="TorusObject.GetSurfaceOffsetTo(TorusObject)"/>).</summary>
        protected Vector3 OffsetToTarget()
        {
            return TorusObject.GetSurfaceOffsetTo(Target.TorusObject);
        }

        /// <summary>Turns towards the direction and moves forward with the given speed multiplier.</summary>
        protected void MoveTowards(Vector3 worldDirection, float speedMultiplier = 1f)
        {
            TorusObject.TurnTowards(worldDirection, turnSpeedDegrees * Mathf.Deg2Rad * Time.deltaTime);

            // Slower while still turning, so enemies do not run in circles around the target.
            float alignment = Mathf.Clamp01(Vector3.Dot(TorusObject.Facing, worldDirection.normalized));
            TorusObject.Move(moveSpeed * speedMultiplier * StatusSpeedMultiplier * alignment * Time.deltaTime);
        }

        /// <summary>Moves along a direction without turning (strafe, retreat).</summary>
        protected void Slide(Vector3 worldDirection, float speedMultiplier = 1f)
        {
            TorusObject.MoveInDirection(worldDirection, moveSpeed * speedMultiplier * StatusSpeedMultiplier * Time.deltaTime);
        }

        protected void Face(Vector3 worldDirection, float turnMultiplier = 1f)
        {
            TorusObject.TurnTowards(worldDirection, turnSpeedDegrees * turnMultiplier * Mathf.Deg2Rad * Time.deltaTime);
        }

        /// <summary>Deals damage to the target when its body is within the radius. Returns true on a hit.</summary>
        protected bool TryHitTarget(float radius, int baseDamage, float knockbackDistance)
        {
            var offset = OffsetToTarget();
            if (offset.magnitude > radius + Target.BodyRadius)
                return false;

            var direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : TorusObject.Facing;
            return Target.TakeDamage(new DamageInfo(Enemy.ScaleDamage(baseDamage), direction, knockbackDistance, Health.Team));
        }
    }
}
