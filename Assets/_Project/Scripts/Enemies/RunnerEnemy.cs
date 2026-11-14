using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Runner (fast, weak), based on the LD57 slime: Idle (wander) → Chase (runners near each other surround
    /// the target, each in its own sector) → Windup (stops and flashes) → Lunge (fast jump forward, hits once)
    /// → Retreat (jumps back) → Chase.
    /// </summary>
    public class RunnerEnemy : EnemyBehaviour
    {
        private enum State
        {
            Idle,
            Chase,
            Windup,
            Lunge,
            Retreat,
        }

        [Header("Idle")]
        [SerializeField]
        private Vector2 wanderInterval = new Vector2(1.5f, 3f);

        [SerializeField]
        [Range(0f, 1f)]
        private float wanderSpeedMultiplier = 0.4f;

        [Header("Chase")]
        [Tooltip("Target further than this is lost and the runner goes back to wandering.")]
        [SerializeField]
        [Min(0f)]
        private float loseRange = 26f;

        [Tooltip("Distance from the target on which runners wait for their attack.")]
        [SerializeField]
        [Min(0f)]
        private float surroundRadius = 2.5f;

        [Tooltip("Runners closer to each other than this share the circle around the target.")]
        [SerializeField]
        [Min(0f)]
        private float groupRadius = 8f;

        [Header("Attack")]
        [Tooltip("Distance from which the runner starts its attack.")]
        [SerializeField]
        [Min(0f)]
        private float attackRange = 3.2f;

        [SerializeField]
        [Min(0f)]
        private float windup = 0.5f;

        [SerializeField]
        private Color telegraphColor = new Color(1f, 0.35f, 0.1f);

        [SerializeField]
        [Min(0f)]
        private float lungeDistance = 4f;

        [SerializeField]
        [Min(0.01f)]
        private float lungeDuration = 0.25f;

        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.9f;

        [SerializeField]
        [Min(1)]
        private int damage = 8;

        [SerializeField]
        [Min(0f)]
        private float knockback = 1.5f;

        [Header("Retreat")]
        [SerializeField]
        [Min(0f)]
        private float retreatTime = 0.7f;

        [SerializeField]
        [Min(0f)]
        private float retreatSpeedMultiplier = 0.8f;

        [Tooltip("Time after the retreat before the next attack.")]
        [SerializeField]
        [Min(0f)]
        private float attackCooldown = 1.2f;

        private static readonly List<RunnerEnemy> chasers = new List<RunnerEnemy>();

        private State state = State.Idle;
        private float stateEndTime;
        private float nextAttackTime;
        private Vector3 wanderDirection;
        private Vector3 lungeDirection;
        private float sectorDegrees;
        private bool lungeHit;

        protected override void Awake()
        {
            base.Awake();
            sectorDegrees = Random.value * 360f;
        }

        private void Update()
        {
            if (CanAct == false)
            {
                // Pushed or stunned mid-attack: the attack is lost.
                if (state == State.Windup || state == State.Lunge)
                    EnterRetreat();
                return;
            }

            var offset = OffsetToTarget();
            float distance = offset.magnitude;

            switch (state)
            {
                case State.Idle:
                    UpdateIdle(distance);
                    break;

                case State.Chase:
                    UpdateChase(offset, distance);
                    break;

                case State.Windup:
                    // Keeps tracking the target a little, so the attack is fair but not trivial to avoid.
                    Face(offset, 0.5f);
                    if (Time.time >= stateEndTime)
                        EnterLunge();
                    break;

                case State.Lunge:
                    UpdateLunge();
                    break;

                case State.Retreat:
                    Slide(-offset, retreatSpeedMultiplier);
                    Face(offset);
                    if (Time.time >= stateEndTime)
                        SetState(State.Chase, 0f);
                    break;
            }
        }

        private void UpdateIdle(float distance)
        {
            if (distance <= DetectRange)
            {
                SetState(State.Chase, 0f);
                return;
            }

            if (Time.time >= stateEndTime)
            {
                float randomAngle = Random.Range(0f, 360f);
                wanderDirection = Quaternion.AngleAxis(randomAngle, TorusObject.Up) * TorusObject.Facing;
                stateEndTime = Time.time + Random.Range(wanderInterval.x, wanderInterval.y);
            }

            MoveTowards(wanderDirection, wanderSpeedMultiplier);
        }

        private void UpdateChase(Vector3 offset, float distance)
        {
            if (distance > loseRange)
            {
                SetState(State.Idle, 0f);
                return;
            }

            if (distance <= attackRange && Time.time >= nextAttackTime)
            {
                EnterWindup();
                return;
            }

            // Ready to attack: straight at the target. Otherwise to its own place on the circle around it.
            var destinationOffset = Time.time >= nextAttackTime ? offset : GetSurroundOffset(offset);
            if (destinationOffset.magnitude > 0.3f)
            {
                MoveTowards(destinationOffset);
                return;
            }

            Face(offset);
        }

        /// <summary>Offset to the runner's own place on the circle around the target.</summary>
        private Vector3 GetSurroundOffset(Vector3 offset)
        {
            var target = Target.TorusObject;
            var sectorDirection = Quaternion.AngleAxis(GetSectorDegrees(), target.Up) * target.RunDirection;
            return offset + sectorDirection * surroundRadius;
        }

        private void EnterWindup()
        {
            SetState(State.Windup, windup);
            if (BodyFlash)
                BodyFlash.Telegraph(telegraphColor, windup);
        }

        private void EnterLunge()
        {
            lungeDirection = TorusObject.Facing;
            lungeHit = false;
            SetState(State.Lunge, lungeDuration);
        }

        private void UpdateLunge()
        {
            TorusObject.MoveInDirection(lungeDirection, lungeDistance / lungeDuration * Time.deltaTime);
            if (lungeHit == false && TryHitTarget(hitRadius, damage, knockback))
                lungeHit = true;

            if (Time.time >= stateEndTime)
                EnterRetreat();
        }

        private void EnterRetreat()
        {
            if (BodyFlash)
                BodyFlash.StopTelegraph();

            SetState(State.Retreat, retreatTime);
            nextAttackTime = Time.time + retreatTime + attackCooldown;
        }

        private void SetState(State newState, float duration)
        {
            state = newState;
            stateEndTime = Time.time + duration;
            if (newState == State.Idle)
                chasers.Remove(this);

            if ((newState == State.Chase || newState == State.Retreat) && chasers.Contains(this) == false)
                chasers.Add(this);
        }

        /// <summary>Runners within groupRadius of this one split the circle around the target into equal sectors.</summary>
        private float GetSectorDegrees()
        {
            int index = 0;
            int count = 0;
            float groupRadiusSquared = groupRadius * groupRadius;
            for (int i = 0; i < chasers.Count; i++)
            {
                var other = chasers[i];
                if (other != this && TorusObject.GetSurfaceOffsetTo(other.TorusObject).sqrMagnitude > groupRadiusSquared)
                    continue;

                if (other == this)
                    index = count;

                count++;
            }

            // Alone: keeps its own random place on the circle.
            return count <= 1 ? sectorDegrees : index * 360f / count;
        }

        private void OnDisable()
        {
            chasers.Remove(this);
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }
    }
}
