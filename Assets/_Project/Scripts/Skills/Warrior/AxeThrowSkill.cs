using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Vfx;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills.Warrior
{
    /// <summary>
    /// Axe throw (LD57 AxeThrowSkill): an axe flies forward along the surface for Range metres and comes back to the caster,
    /// hitting every enemy once on the way out and once on the way back. Evolution: two axes, <see cref="evolvedSpread"/>
    /// degrees to both sides. The axe is a <see cref="TorusObject"/>, so it follows the curve of the torus.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Skills/Axe Throw", fileName = "AxeThrow")]
    public class AxeThrowSkill : SkillDefinition
    {
        [SerializeField]
        [Min(0.1f)]
        private float speed = 20f;

        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.6f;

        [Tooltip("Distance from the caster at which the returning axe is caught.")]
        [SerializeField]
        [Min(0f)]
        private float catchDistance = 0.7f;

        [Tooltip("Safety: the axe is removed after this time even when it did not come back.")]
        [SerializeField]
        [Min(0.5f)]
        private float maxFlightTime = 4f;

        [SerializeField]
        private float evolvedSpread = 15f;

        [SerializeField]
        private Color color = new Color(1f, 0.75f, 0.25f);

        [SerializeField]
        private Color evolvedColor = new Color(1f, 0.25f, 0.15f);

        public override void Execute(SkillContext context)
        {
            var caster = context.User.TorusObject;
            if (context.IsEvolved)
            {
                context.User.StartCoroutine(ThrowingCo(context, Quaternion.AngleAxis(evolvedSpread, caster.Up) * caster.Facing));
                context.User.StartCoroutine(ThrowingCo(context, Quaternion.AngleAxis(-evolvedSpread, caster.Up) * caster.Facing));
                return;
            }

            context.User.StartCoroutine(ThrowingCo(context, caster.Facing));
        }

        private IEnumerator ThrowingCo(SkillContext context, Vector3 direction)
        {
            var caster = context.User.TorusObject;
            var tint = context.IsEvolved ? evolvedColor : color;
            var axe = CreateAxe(caster, direction, tint);
            var outHits = new HashSet<Health>();
            var backHits = new HashSet<Health>();
            var buffer = new List<Health>();
            float range = context.Tier.Range;
            float traveled = 0f;
            float startTime = Time.time;
            bool isReturning = false;

            while (axe && caster && Time.time - startTime < maxFlightTime)
            {
                float step = speed * Time.deltaTime;
                bool wasReturning = isReturning;
                if (wasReturning)
                {
                    var toCaster = axe.GetSurfaceOffsetTo(caster);
                    if (toCaster.magnitude <= catchDistance + step)
                        break;

                    axe.MoveInDirection(toCaster, step);
                }

                if (wasReturning == false)
                {
                    float outStep = Mathf.Min(step, range - traveled);
                    axe.Move(outStep);
                    traveled += outStep;
                    isReturning = traveled >= range;
                    if (isReturning)
                        PlayTurn(axe, tint);
                }

                HitTouching(context, axe, isReturning ? backHits : outHits, buffer);
                yield return null;
            }

            if (axe)
                Destroy(axe.gameObject);
        }

        private TorusObject CreateAxe(TorusObject caster, Vector3 direction, Color tint)
        {
            var axeObject = new GameObject("Thrown Axe");
            var axe = axeObject.AddComponent<TorusObject>();
            axe.Place(caster.World, caster.Angles, direction);
            StylizedFx.AxeVisual(axeObject.transform, tint);
            StylizedFx.AddTrail(axeObject, tint, 0.18f, 0.45f, false);
            // Also removed when the caster stops the coroutine (death, disabled).
            Destroy(axeObject, maxFlightTime + 0.5f);
            return axe;
        }

        private void HitTouching(SkillContext context, TorusObject axe, HashSet<Health> hitTargets, List<Health> buffer)
        {
            buffer.Clear();
            CombatQueries.FindInRadius(axe, context.User.Team, hitRadius, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                var target = buffer[i];
                if (hitTargets.Add(target) == false)
                    continue;

                var direction = axe.GetSurfaceOffsetTo(target.TorusObject);
                if (SkillHits.Hit(context, target, direction, context.Tier.Damage, context.Tier.Knockback, context.Tier.Stun))
                    PlayHit(axe);
            }
        }

        // LD57 axe: a flash and sparks on a hit, a ring of cubes where it turns back.
        private static void PlayHit(TorusObject axe)
        {
            StylizedFx.Flash(axe.transform.position, StylizedFx.Gold, 0.6f);
            StylizedFx.CubeBurst(axe.transform.position, axe.Up, StylizedFx.Orange, 9, 0.12f, 6f, 0.3f);
        }

        private static void PlayTurn(TorusObject axe, Color tint)
        {
            StylizedFx.CubeRing(axe.transform.position, axe.Up, 1.2f, tint, 0.3f, 16);
        }
    }
}
