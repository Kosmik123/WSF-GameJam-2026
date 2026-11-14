using UnityEngine;

namespace WSFGameJam2026.Skills.Mage
{
    /// <summary>
    /// Blink (LD57 BlinkSkill): teleport by Range along the surface in the direction of movement (back when holding back,
    /// otherwise forward), with Duration seconds of invulnerability and a blast at the end (Damage, small radius).
    /// Evolution: a blast also at the start. On the torus there are no walls and no NavMesh, so the target is always valid.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Blink", fileName = "Blink")]
    public class BlinkSkill : SkillDefinition
    {
        // The teleport is done in steps, so the direction follows the curve of the torus.
        private const int Steps = 4;

        [SerializeField]
        [Min(0f)]
        private float blastRadius = 1.5f;

        [SerializeField]
        private Color color = new Color(0.6f, 0.45f, 1f);

        public override void Execute(SkillContext context)
        {
            var caster = context.User.TorusObject;
            var direction = context.User.GetMoveDirection();
            var startPosition = caster.transform.position;

            if (context.IsEvolved)
                Blast(context);

            for (int i = 0; i < Steps; i++)
                caster.MoveInDirection(direction, context.Tier.Range / Steps);

            context.User.Health.MakeInvulnerable(context.Tier.Duration);
            Blast(context);
            SkillVfx.Bolt(new[] { startPosition, caster.transform.position }, caster.Up, color, 0.3f, 0.25f);
        }

        private void Blast(SkillContext context)
        {
            var caster = context.User.TorusObject;
            SkillHits.HitArea(context, caster.Angles, blastRadius, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun);
            SkillVfx.Ring(caster.transform.position, caster.Up, blastRadius, color, 0.4f, 0.2f);
        }
    }
}
