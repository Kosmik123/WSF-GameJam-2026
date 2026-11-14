using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Skills.Mage
{
    /// <summary>
    /// Frost nova (LD57 FrostNovaSkill): ring of ice around the caster, damages enemies within Range and slows them
    /// for Duration seconds. Evolution: the tier's Stun also freezes them in place.
    /// The slow goes through <see cref="StatusEffects"/> instead of the NavMesh agent speed.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Frost Nova", fileName = "FrostNova")]
    public class FrostNovaSkill : SkillDefinition
    {
        [Tooltip("Part of the speed left to slowed enemies.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float slowMultiplier = 0.4f;

        [SerializeField]
        private Color color = new Color(0.6f, 0.9f, 1f);

        private readonly List<Health> hits = new List<Health>();

        public override void Execute(SkillContext context)
        {
            var caster = context.User.TorusObject;
            hits.Clear();
            SkillHits.HitArea(context, caster.Angles, context.Tier.Range, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun, null, hits);

            for (int i = 0; i < hits.Count; i++)
                if (hits[i].TryGetComponent<StatusEffects>(out var statusEffects))
                    statusEffects.ApplySlow(slowMultiplier, context.Tier.Duration);

            SkillVfx.Ring(caster.transform.position, caster.Up, context.Tier.Range, color, 0.6f, 0.3f);
            SkillVfx.Ring(caster.transform.position, caster.Up, context.Tier.Range * 0.55f, Color.white, 0.4f, 0.15f);
        }
    }
}
