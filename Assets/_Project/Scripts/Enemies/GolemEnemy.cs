using UnityEngine;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Golem (the armored enemy of GDD 8: slow, tough): walks straight at the target, and when it is close it raises its
    /// fists (a ring on the ground shows the reach) and slams the ground, hitting everything within the radius with a big push.
    /// After the slam it stays still for a moment, which is the time to hit it. Barely moved by knockback (set on its Knockback).
    /// </summary>
    public class GolemEnemy : EnemyBehaviour
    {
        private enum State
        {
            Chase,
            Windup,
            Recover,
        }

        [Header("Slam")]
        [Tooltip("Distance from which the golem starts its slam.")]
        [SerializeField]
        [Min(0f)]
        private float attackRange = 2.6f;

        [SerializeField]
        [Min(0f)]
        private float slamRadius = 3.5f;

        [SerializeField]
        [Min(0.05f)]
        private float windup = 1f;

        [SerializeField]
        [Min(1)]
        private int damage = 18;

        [SerializeField]
        [Min(0f)]
        private float knockbackValue = 3f;

        [Tooltip("Time after the slam in which the golem does not move.")]
        [SerializeField]
        [Min(0f)]
        private float recoverTime = 1.2f;

        [SerializeField]
        [Min(0f)]
        private float attackCooldown = 1.5f;

        [SerializeField]
        private Color telegraphColor = new Color(1f, 0.45f, 0.15f);

        private State state = State.Chase;
        private float stateEndTime;
        private float nextAttackTime;

        private void Update()
        {
            if (CanAct == false)
            {
                // Stunned or pushed during the windup: the slam is lost.
                if (state == State.Windup)
                    EnterRecover(recoverTime * 0.5f);
                return;
            }

            switch (state)
            {
                case State.Chase:
                    UpdateChase();
                    break;

                case State.Windup:
                    Face(OffsetToTarget(), 0.3f);
                    if (Time.time >= stateEndTime)
                        Slam();
                    break;

                case State.Recover:
                    if (Time.time >= stateEndTime)
                        state = State.Chase;
                    break;
            }
        }

        private void UpdateChase()
        {
            var offset = OffsetToTarget();
            float distance = offset.magnitude;
            if (distance > DetectRange)
                return;

            if (distance <= attackRange && Time.time >= nextAttackTime)
            {
                EnterWindup();
                return;
            }

            // Close enough: only turns, so it does not push into the target.
            if (distance <= attackRange * 0.7f)
            {
                Face(offset);
                return;
            }

            MoveTowards(offset);
        }

        private void EnterWindup()
        {
            state = State.Windup;
            stateEndTime = Time.time + windup;
            if (BodyFlash)
                BodyFlash.Telegraph(telegraphColor, windup);

            SkillVfx.Ring(transform.position, TorusObject.Up, slamRadius, telegraphColor, windup, 0.12f);
            RaiseWindupStarted();
        }

        private void Slam()
        {
            TryHitTarget(slamRadius, damage, knockbackValue);
            SkillVfx.Ring(transform.position, TorusObject.Up, slamRadius, new Color(0.55f, 0.45f, 0.35f), 0.5f, 0.35f);
            RaiseAttackReleased();
            EnterRecover(recoverTime);
        }

        private void EnterRecover(float seconds)
        {
            if (BodyFlash)
                BodyFlash.StopTelegraph();

            state = State.Recover;
            stateEndTime = Time.time + seconds;
            nextAttackTime = stateEndTime + attackCooldown;
        }

        private void OnDisable()
        {
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }
    }
}
