using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Skills.Mage
{
    /// <summary>
    /// Arcane orbs (LD57 ArcaneOrbsSkill): 3/4/5 orbs (tiers 0/1/2+) circle the caster for Duration seconds at the distance
    /// Range and hurt enemies they touch (each enemy at most once per <see cref="perEnemyCooldown"/>).
    /// Evolution: every <see cref="shotInterval"/> seconds an orb shoots the nearest enemy.
    /// The orbs lie in the tangent plane of the caster, and a touch is measured as a surface offset, like every other hit.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Arcane Orbs", fileName = "ArcaneOrbs")]
    public class ArcaneOrbsSkill : SkillDefinition
    {
        [Tooltip("Orbs at tier 0. Every tier adds one, up to tier 2.")]
        [SerializeField]
        [Min(1)]
        private int baseOrbCount = 3;

        [Tooltip("Degrees per second.")]
        [SerializeField]
        private float angularSpeed = 180f;

        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.4f;

        [SerializeField]
        [Min(0f)]
        private float perEnemyCooldown = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float orbSize = 0.36f;

        [Header("Evolution")]
        [SerializeField]
        [Min(0.05f)]
        private float shotInterval = 1f;

        [SerializeField]
        [Min(0f)]
        private float shotRange = 10f;

        [SerializeField]
        [Min(0f)]
        private float shotSpeed = 16f;

        [SerializeField]
        private SkillProjectile shotPrefab;

        [SerializeField]
        private Color color = new Color(0.7f, 0.4f, 1f);

        public override void Execute(SkillContext context)
        {
            context.User.StartCoroutine(OrbitingCo(context));
        }

        private IEnumerator OrbitingCo(SkillContext context)
        {
            var user = context.User;
            var caster = user.TorusObject;
            int count = baseOrbCount + Mathf.Min(context.TierIndex, 2);
            float duration = Mathf.Max(0.5f, context.Tier.Duration);
            float radius = context.Tier.Range;

            // Orbs remove themselves after the duration, also when the coroutine is stopped (death, disabled caster).
            var orbs = new Transform[count];
            for (int i = 0; i < count; i++)
                orbs[i] = SkillVfx.Orb(color, orbSize, duration);

            var nextHitTimes = new Dictionary<Health, float>();
            var touching = new List<Health>();
            float angle = 0f;
            float elapsed = 0f;
            float nextShotTime = shotInterval;
            while (elapsed < duration && user.Health.IsDead == false)
            {
                angle += angularSpeed * Time.deltaTime;
                elapsed += Time.deltaTime;

                // A frame that does not turn with the caster, so the orbs keep circling evenly.
                var forward = caster.RunDirection;
                var right = Vector3.Cross(caster.Up, forward);
                for (int i = 0; i < count; i++)
                {
                    float orbAngle = (angle + 360f * i / count) * Mathf.Deg2Rad;
                    var offset = (right * Mathf.Cos(orbAngle) + forward * Mathf.Sin(orbAngle)) * radius;
                    if (orbs[i])
                        orbs[i].position = caster.transform.position + offset;

                    HitTouching(context, offset, nextHitTimes, touching);
                }

                if (context.IsEvolved && elapsed >= nextShotTime)
                {
                    nextShotTime += shotInterval;
                    Shoot(context);
                }

                yield return null;
            }

            for (int i = 0; i < count; i++)
                if (orbs[i])
                    Destroy(orbs[i].gameObject);
        }

        private void HitTouching(SkillContext context, Vector3 orbOffset, Dictionary<Health, float> nextHitTimes, List<Health> touching)
        {
            var caster = context.User.TorusObject;
            touching.Clear();
            var targets = Health.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Team == context.User.Team || target.IsDead)
                    continue;

                var fromOrb = caster.GetSurfaceOffsetTo(target.TorusObject) - orbOffset;
                if (fromOrb.magnitude <= hitRadius + target.BodyRadius)
                    touching.Add(target);
            }

            for (int i = 0; i < touching.Count; i++)
            {
                var target = touching[i];
                if (nextHitTimes.TryGetValue(target, out float nextHitTime) && Time.time < nextHitTime)
                    continue;

                nextHitTimes[target] = Time.time + perEnemyCooldown;
                var direction = caster.GetSurfaceOffsetTo(target.TorusObject);
                SkillHits.Hit(context, target, direction, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun);
            }
        }

        private void Shoot(SkillContext context)
        {
            var caster = context.User.TorusObject;
            if (shotPrefab == null)
                return;

            var target = CombatQueries.FindNearest(caster, context.User.Team, shotRange);
            if (target == null)
                return;

            var direction = caster.GetSurfaceOffsetTo(target.TorusObject);
            var shot = Instantiate(shotPrefab);
            shot.Launch(caster.World, caster.Angles, direction, shotSpeed, shotRange + 3f, context.User.Team,
                (endedShot, hitTarget) => SkillHits.Hit(context, hitTarget, endedShot.TorusObject.Facing, context.Tier.Damage, 0f, 0f));
        }
    }
}
