using System.Collections;
using UnityEngine;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Mage
{
    /// <summary>
    /// Fireball (LD57 FireballSkill): flies straight along the surface and explodes on the first enemy or at the end
    /// of its flight, damaging everything within Range. Evolution: the ground keeps burning for Duration seconds
    /// and hurts enemies in the same spot every tick, with half of the damage.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Fireball", fileName = "Fireball")]
    public class FireballSkill : SkillDefinition
    {
        [SerializeField]
        private SkillProjectile projectilePrefab;

        [SerializeField]
        [Min(0f)]
        private float speed = 16f;

        [SerializeField]
        [Min(0f)]
        private float flightRange = 20f;

        [Tooltip("Time between the ticks of the burning ground (evolution).")]
        [SerializeField]
        [Min(0.05f)]
        private float burnTickInterval = 0.5f;

        [SerializeField]
        private Color color = new Color(1f, 0.35f, 0.1f);

        [SerializeField]
        private Color emberColor = new Color(1f, 0.75f, 0.3f);

        public override void Execute(SkillContext context)
        {
            if (projectilePrefab == null)
                return;

            var caster = context.User.TorusObject;
            var projectile = Instantiate(projectilePrefab);
            projectile.Launch(caster.World, caster.Angles, caster.Facing, speed, flightRange, context.User.Team,
                (endedProjectile, target) => Explode(context, endedProjectile.TorusObject));
        }

        private void Explode(SkillContext context, TorusObject place)
        {
            var center = place.Angles;
            SkillHits.HitArea(context, center, context.Tier.Range, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun);
            PlayExplosion(place.transform.position, VfxGround.Below(place, out var up), up, context.Tier.Range);

            if (context.IsEvolved && context.Tier.Duration > 0f && context.User)
                context.User.StartCoroutine(BurningCo(context, center, place.transform.position, place.Up));
        }

        // LD57 explosion: big flashes, the fireball breaks into shards, two rings, a column of smoke, a glow and a burnt spot.
        private void PlayExplosion(Vector3 position, Vector3 ground, Vector3 up, float radius)
        {
            StylizedFx.Shockwave(ground, up, radius, color, 0.25f);
            StylizedFx.Flash(position, emberColor, 2.2f);
            StylizedFx.Flash(position, Color.white, 3.2f);
            StylizedFx.Ring(ground, up, radius, new Color(1f, 0.85f, 0.5f), 28);
            StylizedFx.Ring(ground, up, radius * 0.9f, new Color(0.85f, 0.12f, 0.05f), 20, 1.1f);
            StylizedFx.Burst(position, up, color, 12, 0.28f, 7f, 0.75f);
            StylizedFx.Burst(position, up, emberColor, 12, 0.22f, 8f, 0.65f);
            StylizedFx.Burst(position, up, StylizedFx.SmokeGray, 6, 0.3f, 4f, 1.1f);
            StylizedFx.Repeat(1.5f, 0.125f, progress => StylizedFx.SmokePuff(ground + up * (0.3f + progress * 0.8f), up, 1.6f, 0.5f));
            StylizedFx.GroundGlow(ground, up, radius, color, 3f, 1f);
            StylizedFx.Scorch(ground, up, radius * 0.9f, 6f);
        }

        private IEnumerator BurningCo(SkillContext context, TorusAngles center, Vector3 position, Vector3 up)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(context.Tier.Duration / burnTickInterval));
            int damage = Mathf.Max(1, context.Tier.Damage / 2);
            var ground = VfxGround.Below(position, out var groundUp);
            StylizedFx.GroundGlow(ground, groundUp, context.Tier.Range, color, context.Tier.Duration + burnTickInterval, 1f);
            StylizedFx.Repeat(context.Tier.Duration, 0.3f, progress => PlayEmbers(ground, groundUp, context.Tier.Range));
            for (int i = 0; i < ticks; i++)
            {
                yield return new WaitForSeconds(burnTickInterval);
                SkillHits.HitArea(context, center, context.Tier.Range, damage, 0f, 0f);
                PlayFlames(ground, groundUp, context.Tier.Range);
                SkillVfx.Ring(position, up, context.Tier.Range * 0.8f, color, burnTickInterval, 0.12f);
            }
        }

        private void PlayFlames(Vector3 ground, Vector3 up, float radius)
        {
            for (int i = 0; i < 3; i++)
                StylizedFx.Burst(RandomPointOnGround(ground, up, radius * 0.6f), up, Random.value < 0.5f ? color : emberColor, 4, 0.12f, 2.5f, 0.6f);
        }

        private static void PlayEmbers(Vector3 ground, Vector3 up, float radius)
        {
            StylizedFx.FireSpark(RandomPointOnGround(ground, up, radius * 0.6f), Vector3.zero, up);
        }

        private static Vector3 RandomPointOnGround(Vector3 ground, Vector3 up, float radius)
        {
            VfxGround.GetTangents(up, out var right, out var forward);
            var disc = Random.insideUnitCircle * radius;
            return ground + right * disc.x + forward * disc.y + up * 0.1f;
        }
    }
}
