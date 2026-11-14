using System.Collections;
using UnityEngine;
using WSFGameJam2026.Combat;
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
            SkillVfx.Ring(place.transform.position, place.Up, context.Tier.Range, color, 0.5f, 0.25f);

            if (context.IsEvolved && context.Tier.Duration > 0f && context.User)
                context.User.StartCoroutine(BurningCo(context, center, place.transform.position, place.Up));
        }

        private IEnumerator BurningCo(SkillContext context, TorusAngles center, Vector3 position, Vector3 up)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(context.Tier.Duration / burnTickInterval));
            int damage = Mathf.Max(1, context.Tier.Damage / 2);
            for (int i = 0; i < ticks; i++)
            {
                yield return new WaitForSeconds(burnTickInterval);
                SkillHits.HitArea(context, center, context.Tier.Range, damage, 0f, 0f);
                SkillVfx.Ring(position, up, context.Tier.Range * 0.8f, color, burnTickInterval, 0.12f);
            }
        }
    }
}
