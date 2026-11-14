using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Warrior
{
    /// <summary>
    /// Spin (LD57 SpinSkill): for Duration seconds, every <see cref="tickInterval"/> seconds, hits every enemy within Range
    /// around the caster. Evolution (longer in the asset): every tick also pulls the hit enemies towards the caster.
    /// Distances and the pull go along the torus surface.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Spin", fileName = "Spin")]
    public class SpinSkill : SkillDefinition
    {
        [SerializeField]
        [Min(0.05f)]
        private float tickInterval = 0.25f;

        [Tooltip("Distance the hit enemies are pulled towards the caster on every tick (evolution).")]
        [SerializeField]
        [Min(0f)]
        private float pullDistance = 0.5f;

        [SerializeField]
        private Color color = new Color(1f, 0.75f, 0.3f);

        [SerializeField]
        private Color evolvedColor = new Color(1f, 0.25f, 0.15f);

        public override void Execute(SkillContext context)
        {
            context.User.StartCoroutine(SpinningCo(context));
        }

        private IEnumerator SpinningCo(SkillContext context)
        {
            var user = context.User;
            var caster = user.TorusObject;
            var tint = context.IsEvolved ? evolvedColor : color;
            var hits = new List<Health>();
            PlayStart(caster, context.Tier.Range, context.Tier.Duration, tint, context.IsEvolved);

            float elapsed = 0f;
            while (elapsed < context.Tier.Duration && user.Health.IsDead == false)
            {
                hits.Clear();
                SkillHits.HitArea(context, caster.Angles, context.Tier.Range, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun, null, hits);
                if (context.IsEvolved)
                    Pull(caster, hits);

                PlayTick(caster, context.Tier.Range, tint);
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }
        }

        private void Pull(TorusObject caster, List<Health> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.IsDead)
                    continue;

                var toCaster = target.TorusObject.GetSurfaceOffsetTo(caster);
                float distance = toCaster.magnitude;
                if (distance <= pullDistance)
                    continue;

                target.TorusObject.MoveInDirection(toCaster, Mathf.Min(pullDistance, distance - pullDistance));
            }
        }

        // LD57 spin: a fast blade arc around the caster, a glow, a flash and a ring of cubes on every tick.
        private static void PlayStart(TorusObject caster, float range, float duration, Color tint, bool isEvolved)
        {
            var ground = VfxGround.Below(caster, out var up);
            StylizedFx.SpinArc(caster.transform, ground, range, duration, tint);
            StylizedFx.GroundGlow(ground, up, Mathf.Max(1.5f, range), tint * 0.6f, duration);
            StylizedFx.Flash(ground + up * 0.9f, StylizedFx.HotWhite, 0.8f);
            StylizedFx.Shake(0.15f, 0.08f);
            if (isEvolved)
                StylizedFx.Column(ground, up, 4f, StylizedFx.Red, 20);
        }

        private static void PlayTick(TorusObject caster, float range, Color tint)
        {
            var ground = VfxGround.Below(caster, out var up);
            StylizedFx.CubeRing(ground, up, Mathf.Max(1.5f, range), tint, 0.4f);
        }
    }
}
