using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Warrior
{
    /// <summary>
    /// War cry (LD57 WarCrySkill): at once, every enemy within Range around the caster is pushed away by Knockback and stunned
    /// for Stun seconds, and takes Damage (0 until the evolution). Without damage <see cref="Health.TakeDamage"/> ignores the
    /// hit, so the push and the stun are then applied directly.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/War Cry", fileName = "WarCry")]
    public class WarCrySkill : SkillDefinition
    {
        [SerializeField]
        private Color color = new Color(1f, 0.75f, 0.25f);

        [SerializeField]
        private Color evolvedColor = new Color(1f, 0.25f, 0.15f);

        private readonly List<Health> targets = new List<Health>();

        public override void Execute(SkillContext context)
        {
            var caster = context.User.TorusObject;
            var tint = context.IsEvolved ? evolvedColor : color;
            targets.Clear();
            CombatQueries.FindInRadius(caster.Angles, context.User.Team, context.Tier.Range, targets);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var direction = -target.TorusObject.GetSurfaceOffsetTo(caster.Angles);
                if (context.Tier.Damage > 0)
                    SkillHits.Hit(context, target, direction, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun);

                if (context.Tier.Damage <= 0)
                    Shove(target, direction, context.Tier.Knockback, context.Tier.Stun);

                if (context.Tier.Stun > 0f)
                    StylizedFx.Electrify(target.transform, tint, context.Tier.Stun);
            }

            PlayCry(context, caster, tint);
        }

        private static void Shove(Health target, Vector3 direction, float distance, float stun)
        {
            if (target.IsDead || target.IsInvulnerable)
                return;

            if (distance > 0f && target.TryGetComponent<Knockback>(out var knockback))
                knockback.Apply(direction, distance);

            if (stun > 0f && target.TryGetComponent<StatusEffects>(out var statusEffects))
                statusEffects.ApplyStun(stun);
        }

        // LD57 cry: three rings of cubes, a column, a flash, a growing ring and a glow around the caster.
        private static void PlayCry(SkillContext context, TorusObject caster, Color tint)
        {
            float range = context.Tier.Range;
            var ground = VfxGround.Below(caster, out var up);
            StylizedFx.Repeat(0.2f, 0.1f, progress => StylizedFx.CubeRing(ground, up, range * (1f - progress * 0.2f), tint, 0.5f));
            StylizedFx.GrowingRing(ground + up * 0.06f, up, range, tint, 0.6f, 0.35f);
            StylizedFx.Column(ground, up, 3f, StylizedFx.Orange, 26);
            StylizedFx.Flash(ground + up, StylizedFx.HotWhite, 1.3f);
            StylizedFx.GroundGlow(ground, up, range * 0.8f, tint * 0.6f, Mathf.Max(context.Tier.Duration, 0.8f));
            StylizedFx.Shake(0.2f, 0.15f);
        }
    }
}
