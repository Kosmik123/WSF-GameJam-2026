using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Mage
{
    /// <summary>
    /// Chain lightning (LD57 ChainLightningSkill): hits the nearest enemy within Range of the caster, then jumps to the
    /// nearest not yet hit enemy within the jump range, 3/4/5 times (tiers 0/1/2+). Every jump deals less damage.
    /// Evolution: the tier's Stun on every hit enemy. Distances are measured along the surface.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Chain Lightning", fileName = "ChainLightning")]
    public class ChainLightningSkill : SkillDefinition
    {
        [SerializeField]
        [Min(0f)]
        private float jumpRange = 5f;

        [Tooltip("Jumps after the first hit at tier 0. Every tier adds one, up to tier 2.")]
        [SerializeField]
        [Min(0)]
        private int baseJumps = 3;

        [Tooltip("Damage lost with every jump. A hit always deals at least 1.")]
        [SerializeField]
        [Min(0)]
        private int damageLossPerJump = 4;

        [Tooltip("Length of the lightning shown when nothing was in range.")]
        [SerializeField]
        [Min(0f)]
        private float missLength = 2.5f;

        [SerializeField]
        private Color color = new Color(0.6f, 0.8f, 1f);

        [Tooltip("Height of the hand of the caster above its pivot, where the lightning starts (only the look).")]
        [SerializeField]
        private float castHeight = 1f;

        private readonly HashSet<Health> hit = new HashSet<Health>();
        private readonly List<Vector3> points = new List<Vector3>();

        public override void Execute(SkillContext context)
        {
            var caster = context.User.TorusObject;
            int jumps = baseJumps + Mathf.Min(context.TierIndex, 2);
            int damage = context.Tier.Damage;
            float range = context.Tier.Range;
            var from = caster;

            hit.Clear();
            points.Clear();
            points.Add(caster.transform.position);

            for (int i = 0; i <= jumps; i++)
            {
                var target = CombatQueries.FindNearest(from, context.User.Team, range, hit);
                if (target == null)
                    break;

                hit.Add(target);
                points.Add(target.transform.position);
                var direction = from.GetSurfaceOffsetTo(target.TorusObject);
                SkillHits.Hit(context, target, direction, Mathf.Max(1, damage), context.Tier.Knockback, context.Tier.Stun);

                damage -= damageLossPerJump;
                from = target.TorusObject;
                range = jumpRange;
            }

            // A miss: a short crackle in front of the caster.
            if (points.Count == 1)
                points.Add(caster.transform.position + caster.Facing * missLength);

            PlayLightning(caster);
        }

        // LD57 lightning: a bolt of crystals on every jump (from the hand of the caster), a flash, sparks and electricity on every hit.
        private void PlayLightning(TorusObject caster)
        {
            points[0] = caster.transform.position + caster.Up * castHeight + caster.Facing * 0.4f;
            if (hit.Count == 0)
                points[1] += caster.Up * castHeight;

            for (int i = 0; i < points.Count - 1; i++)
                StylizedFx.Bolt(points[i], points[i + 1], caster.Up, color, 0.36f, 0.22f);

            StylizedFx.Flash(points[0], color, 0.5f, 0.1f);
            foreach (var target in hit)
            {
                var position = target.transform.position;
                StylizedFx.Flash(position, color, 0.9f);
                StylizedFx.Burst(position, target.transform.up, color, 8, 0.1f, 6f, 0.4f);
                StylizedFx.Electrify(target.transform, color, 0.4f);
            }
        }
    }
}
