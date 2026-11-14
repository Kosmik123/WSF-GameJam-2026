using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

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

            PlayNova(caster, context.Tier.Range);
        }

        // LD57 nova: a flash, two rings, ice crystals growing out of the ground with a frosty mist, a cold glow, frost on the hit enemies.
        private void PlayNova(TorusObject caster, float radius)
        {
            var ground = VfxGround.Below(caster, out var up);
            StylizedFx.Flash(ground + up * 0.5f, color, 1.5f);
            StylizedFx.Ring(ground, up, radius, color, 36);
            StylizedFx.Ring(ground, up, radius * 0.55f, Color.white, 20);
            StylizedFx.IceGrowth(ground, up, radius, color, 16, 8);
            StylizedFx.GroundGlow(ground, up, radius * 0.8f, color, 1.5f, 0.5f);
            SkillVfx.Ring(caster.transform.position, caster.Up, radius, color, 0.6f, 0.3f);
            for (int i = 0; i < hits.Count; i++)
                StylizedFx.Burst(hits[i].transform.position + hits[i].transform.up * 0.3f, hits[i].transform.up, color, 4, 0.1f, 3f, 0.4f);
        }
    }
}
