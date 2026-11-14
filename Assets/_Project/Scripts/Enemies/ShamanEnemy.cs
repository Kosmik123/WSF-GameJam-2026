using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Skills;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Shaman (support caster): keeps its distance from the target like the shooter, heals hurt enemies around it from time
    /// to time, and curses the ground under the target: a ring appears where the target stands and after a moment
    /// it bursts, so the target has to walk out of it. A hit during the casting cancels the curse.
    /// </summary>
    public class ShamanEnemy : EnemyBehaviour
    {
        [Header("Distance")]
        [SerializeField]
        [Min(0f)]
        private float keepDistance = 10f;

        [SerializeField]
        [Min(0f)]
        private float keepBand = 2f;

        [SerializeField]
        [Range(0f, 1f)]
        private float strafeSpeedMultiplier = 0.4f;

        [SerializeField]
        [Min(0f)]
        private float strafeSwitchTime = 2f;

        [Header("Curse")]
        [SerializeField]
        [Min(0f)]
        private float curseRange = 15f;

        [SerializeField]
        [Min(0f)]
        private float curseRadius = 2.2f;

        [Tooltip("Time between the ring appearing and the burst.")]
        [SerializeField]
        [Min(0.1f)]
        private float curseDelay = 1.1f;

        [SerializeField]
        [Min(0f)]
        private float curseCooldown = 3.5f;

        [SerializeField]
        [Min(1)]
        private int curseDamage = 12;

        [SerializeField]
        private Color curseColor = new Color(0.75f, 0.2f, 1f);

        [Header("Healing")]
        [SerializeField]
        [Min(0f)]
        private float healRadius = 8f;

        [SerializeField]
        [Min(1)]
        private int healAmount = 8;

        [SerializeField]
        [Min(0.5f)]
        private float healInterval = 4f;

        [SerializeField]
        private Color healColor = new Color(0.3f, 1f, 0.4f);

        private readonly List<Health> healTargets = new List<Health>();

        private bool isAware;
        private bool isCasting;
        private float curseTime;
        private float nextCurseTime;
        private float nextHealTime;
        private float nextSideSwitch;
        private int strafeSide = 1;
        private TorusAngles curseCenter;
        private Vector3 curseRingPosition;
        private Vector3 curseRingUp;

        private void Start()
        {
            nextCurseTime = Time.time + curseCooldown * 0.5f;
            nextHealTime = Time.time + healInterval;
            strafeSide = Random.value < 0.5f ? -1 : 1;
        }

        private void Update()
        {
            if (CanAct == false)
            {
                if (isCasting)
                    CancelCurse();
                return;
            }

            var offset = OffsetToTarget();
            float distance = offset.magnitude;
            if (isAware == false)
            {
                if (distance > DetectRange)
                    return;
                isAware = true;
            }

            if (Time.time >= nextHealTime)
                HealAllies();

            if (isCasting)
            {
                Face(offset);
                if (Time.time >= curseTime)
                    BurstCurse();
                return;
            }

            if (Time.time >= nextCurseTime && distance <= curseRange)
            {
                StartCurse();
                return;
            }

            UpdateMovement(offset, distance);
        }

        private void UpdateMovement(Vector3 offset, float distance)
        {
            var toTarget = distance > 0.01f ? offset / distance : TorusObject.Facing;
            Face(toTarget);

            if (Time.time >= nextSideSwitch)
            {
                nextSideSwitch = Time.time + strafeSwitchTime;
                strafeSide = -strafeSide;
            }

            var move = Vector3.Cross(TorusObject.Up, toTarget) * (strafeSide * strafeSpeedMultiplier);
            if (distance > keepDistance + keepBand)
                move += toTarget;

            if (distance < keepDistance - keepBand)
                move -= toTarget;

            if (move.sqrMagnitude > 0.0001f)
                Slide(move.normalized, Mathf.Min(1f, move.magnitude));
        }

        private void StartCurse()
        {
            isCasting = true;
            curseTime = Time.time + curseDelay;
            curseCenter = Target.TorusObject.Angles;
            if (BodyFlash)
                BodyFlash.Telegraph(curseColor, curseDelay);

            SkillVfx.Ring(Target.transform.position, Target.TorusObject.Up, curseRadius, curseColor, curseDelay, 0.12f);
            curseRingPosition = Target.transform.position;
            curseRingUp = Target.TorusObject.Up;
            RaiseWindupStarted();
        }

        private void BurstCurse()
        {
            isCasting = false;
            nextCurseTime = Time.time + curseCooldown;
            RaiseAttackReleased();
            SkillVfx.Ring(curseRingPosition, curseRingUp, curseRadius, Color.white, 0.35f, 0.3f);

            // The target is hit only if it is still in the ring.
            if (Target.TorusObject.GetSurfaceOffsetTo(curseCenter).magnitude > curseRadius + Target.BodyRadius)
                return;

            var push = Target.TorusObject.GetSurfaceOffsetTo(curseCenter) * -1f;
            Target.TakeDamage(new DamageInfo(Enemy.ScaleDamage(curseDamage), push.normalized, 1f, Health.Team));
        }

        private void CancelCurse()
        {
            isCasting = false;
            nextCurseTime = Time.time + curseCooldown * 0.5f;
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }

        private void HealAllies()
        {
            nextHealTime = Time.time + healInterval;
            healTargets.Clear();
            CombatQueries.FindInRadius(TorusObject.Angles, Target.Team, healRadius, healTargets);

            bool hasHealed = false;
            for (int i = 0; i < healTargets.Count; i++)
            {
                var ally = healTargets[i];
                if (ally.CurrentHealth >= ally.MaxHealth)
                    continue;

                ally.Heal(healAmount);
                SkillVfx.Ring(ally.transform.position, ally.TorusObject.Up, ally.BodyRadius + 0.4f, healColor, 0.5f, 0.1f);
                hasHealed = true;
            }

            if (hasHealed)
                SkillVfx.Ring(transform.position, TorusObject.Up, healRadius, healColor, 0.6f, 0.08f);
        }

        private void OnDisable()
        {
            isCasting = false;
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }
    }
}
