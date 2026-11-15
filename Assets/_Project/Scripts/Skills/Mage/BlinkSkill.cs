using UnityEngine;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

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
        private const float ChestHeight = 0.9f;

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
            var startFeet = VfxGround.Below(caster, out var startUp);

            if (context.IsEvolved)
                Blast(context);

            for (int i = 0; i < Steps; i++)
                caster.MoveInDirection(direction, context.Tier.Range / Steps);

            context.User.Health.MakeInvulnerable(context.Tier.Duration);
            Blast(context);
            PlayDeparture(startPosition, startFeet, startUp, caster.transform.position + caster.Up * ChestHeight);
        }

        private void Blast(SkillContext context)
        {
            var caster = context.User.TorusObject;
            SkillHits.HitArea(context, caster.Angles, blastRadius, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun);
            PlayBlast(caster);
        }

        // LD57 start of the blink: the body falls apart, shards implode into the chest, a flash and a line of light to the target.
        private void PlayDeparture(Vector3 startPosition, Vector3 feet, Vector3 up, Vector3 targetChest)
        {
            var chest = startPosition + up * ChestHeight;
            StylizedFx.Dissolve(feet, up, color, 14);
            StylizedFx.Implode(chest, up, color, 20, 1.8f, 0.35f);
            StylizedFx.Flash(chest, color, 1.1f);
            StylizedFx.Line(chest, targetChest, color, 10, 0.3f);
        }

        // LD57 blast: shards, three rings stacked up, a glow on the ground and a flash.
        private void PlayBlast(TorusObject caster)
        {
            var ground = VfxGround.Below(caster, out var up);
            SkillVfx.Ring(caster.transform.position, up, blastRadius, color, 0.4f, 0.2f);
            StylizedFx.Burst(ground + up * 0.6f, up, color, 24, 0.18f, 6f, 0.6f);
            float ringRadius = blastRadius * 1.3f;
            StylizedFx.Repeat(0.1f, 0.05f, progress => StylizedFx.Ring(ground + up * (0.6f * Mathf.RoundToInt(progress * 2f)), up, ringRadius, color, 20));
            StylizedFx.GroundGlow(ground, up, blastRadius, color, 0.8f, 0.6f);
            StylizedFx.Flash(ground + up * 0.8f, color, 1.3f);
        }
    }
}
