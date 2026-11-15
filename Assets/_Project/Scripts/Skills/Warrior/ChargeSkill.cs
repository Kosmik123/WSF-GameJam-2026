using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Warrior
{
    /// <summary>
    /// Charge (LD57 ChargeSkill): a dash by Range forward in <see cref="chargeTime"/> seconds, invulnerable on the way, hitting
    /// every enemy it passes once. Evolution: leaves a trail of fire for Duration seconds that hurts enemies standing in it
    /// (<see cref="fireDamagePerSecond"/>, once a second). Movement goes along the surface, hits by surface distance.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Charge", fileName = "Charge")]
    public class ChargeSkill : SkillDefinition
    {
        [SerializeField]
        [Min(0.05f)]
        private float chargeTime = 0.2f;

        [Tooltip("Radius around the caster in which enemies are hit on the way.")]
        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.7f;

        [Header("Evolution")]
        [Tooltip("Distance between the burning spots of the trail.")]
        [SerializeField]
        [Min(0.2f)]
        private float fireSpacing = 1f;

        [SerializeField]
        [Min(0f)]
        private float fireRadius = 0.8f;

        [Tooltip("Damage of the fire trail, dealt once a second to every enemy in it (LD57: 2, x4 for the WSF health scale).")]
        [SerializeField]
        [Min(0)]
        private int fireDamagePerSecond = 8;

        [SerializeField]
        private Color color = new Color(1f, 0.5f, 0.15f);

        [SerializeField]
        private Color evolvedColor = new Color(1f, 0.2f, 0.1f);

        public override void Execute(SkillContext context)
        {
            context.User.StartCoroutine(ChargingCo(context));
        }

        private IEnumerator ChargingCo(SkillContext context)
        {
            var user = context.User;
            var caster = user.TorusObject;
            var direction = caster.Facing;
            float range = context.Tier.Range;
            float speed = range / chargeTime;
            var hitTargets = new HashSet<Health>();
            var buffer = new List<Health>();
            var fireSpots = new List<TorusAngles>();
            var tint = context.IsEvolved ? evolvedColor : color;

            user.Health.MakeInvulnerable(chargeTime + 0.05f);
            PlayStart(caster, direction, tint);

            float traveled = 0f;
            float nextFireDistance = 0f;
            while (traveled < range && user.Health.IsDead == false)
            {
                float step = Mathf.Min(speed * Time.deltaTime, range - traveled);
                // Short steps, so a fast charge does not jump over an enemy.
                int substeps = Mathf.Max(1, Mathf.CeilToInt(step / Mathf.Max(0.1f, hitRadius * 0.5f)));
                for (int i = 0; i < substeps; i++)
                {
                    direction = Vector3.ProjectOnPlane(direction, caster.Up).normalized;
                    caster.MoveInDirection(direction, step / substeps);
                    HitTouching(context, caster, direction, hitTargets, buffer);
                }

                traveled += step;
                while (context.IsEvolved && nextFireDistance <= traveled)
                {
                    nextFireDistance += fireSpacing;
                    fireSpots.Add(caster.Angles);
                    PlayFireSpot(caster.World, caster.Angles, context.Tier.Duration);
                }

                yield return null;
            }

            PlayEnd(caster, tint);
            if (fireSpots.Count > 0 && context.Tier.Duration > 0f)
                user.StartCoroutine(BurningTrailCo(context, fireSpots));
        }

        private void HitTouching(SkillContext context, TorusObject caster, Vector3 direction, HashSet<Health> hitTargets, List<Health> buffer)
        {
            buffer.Clear();
            CombatQueries.FindInRadius(caster, context.User.Team, hitRadius, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                var target = buffer[i];
                if (hitTargets.Add(target) == false)
                    continue;

                if (SkillHits.Hit(context, target, direction, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun))
                    StylizedFx.CubeBurst(target.transform.position, caster.Up, StylizedFx.Gold, 8, 0.12f, 5f, 0.35f, direction);
            }
        }

        private IEnumerator BurningTrailCo(SkillContext context, List<TorusAngles> spots)
        {
            var nextHitTimes = new Dictionary<Health, float>();
            var buffer = new List<Health>();
            float endTime = Time.time + context.Tier.Duration;
            while (Time.time < endTime)
            {
                for (int i = 0; i < spots.Count; i++)
                {
                    buffer.Clear();
                    CombatQueries.FindInRadius(spots[i], context.User.Team, fireRadius, buffer);
                    for (int j = 0; j < buffer.Count; j++)
                    {
                        var target = buffer[j];
                        if (nextHitTimes.TryGetValue(target, out float nextHitTime) && Time.time < nextHitTime)
                            continue;

                        nextHitTimes[target] = Time.time + 1f;
                        SkillHits.Hit(context, target, Vector3.zero, fireDamagePerSecond, 0f, 0f);
                    }
                }

                yield return null;
            }
        }

        // LD57 charge: shards thrown back and a flash at the start, a trail of shards, a ring of cubes at the end.
        private static void PlayStart(TorusObject caster, Vector3 direction, Color tint)
        {
            var ground = VfxGround.Below(caster, out var up);
            var chest = ground + up * 0.8f;
            for (int i = 0; i < 14; i++)
                StylizedFx.Spark(chest, -direction * 1.5f, up, tint);

            StylizedFx.Flash(chest, StylizedFx.HotWhite, 1f);
            StylizedFx.AddTrail(caster.gameObject, tint, 0.26f, 0.5f, false, 0.7f, 0.3f);
            StylizedFx.Shake(0.12f, 0.1f);
        }

        private static void PlayEnd(TorusObject caster, Color tint)
        {
            var ground = VfxGround.Below(caster, out var up);
            StylizedFx.CubeRing(ground, up, 2.2f, tint, 0.4f);
            StylizedFx.CubeBurst(ground + up * 0.3f, up, StylizedFx.Gold, 8, 0.12f, 5f, 0.35f);
        }

        private void PlayFireSpot(TorusWorld world, TorusAngles angles, float seconds)
        {
            if (world == null)
                return;

            var ground = VfxGround.At(world, angles, out var up);
            StylizedFx.Column(ground, up, 1.6f, StylizedFx.Red, 8);
            StylizedFx.GroundGlow(ground, up, fireRadius, new Color(1f, 0.25f, 0.08f), seconds, 0.8f);
            StylizedFx.Repeat(seconds, 0.25f, progress => StylizedFx.FireSpark(ground + up * 0.1f, Vector3.zero, up));
        }
    }
}
