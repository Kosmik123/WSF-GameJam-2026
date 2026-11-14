using UnityEngine;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Warrior
{
    /// <summary>
    /// Ground slam (LD57 GroundSlamSkill): a blow into the ground that hits every enemy within Range around the caster and
    /// pushes it away by Knockback. Evolution: the tier's Stun. In LD57 it was a jump on the space key, here it is a normal
    /// slot skill and the blow comes at once.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Ground Slam", fileName = "GroundSlam")]
    public class GroundSlamSkill : SkillDefinition
    {
        [SerializeField]
        private Color color = new Color(1f, 0.8f, 0.45f);

        [SerializeField]
        private Color evolvedColor = new Color(1f, 0.35f, 0.15f);

        public override void Execute(SkillContext context)
        {
            var caster = context.User.TorusObject;
            SkillHits.HitArea(context, caster.Angles, context.Tier.Range, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun);
            PlaySlam(caster, context.Tier.Range, context.IsEvolved ? evolvedColor : color);
        }

        // LD57 slam: a shockwave (rings, cracks, dust, debris, shake), a big flash, a column of cubes and a glow.
        private static void PlaySlam(TorusObject caster, float range, Color tint)
        {
            var ground = VfxGround.Below(caster, out var up);
            StylizedFx.Shockwave(ground, up, range, tint, 0.35f);
            StylizedFx.Flash(ground + up * 0.5f, StylizedFx.HotWhite, 2.2f);
            StylizedFx.CubeBurst(ground + up * 0.2f, up, StylizedFx.Rock, 22, 0.22f, 7f, 0.8f);
            StylizedFx.Column(ground, up, 3.5f, StylizedFx.Gold, 26);
            StylizedFx.GroundGlow(ground, up, range * 1.1f, tint * 0.7f, 1.5f);
        }
    }
}
