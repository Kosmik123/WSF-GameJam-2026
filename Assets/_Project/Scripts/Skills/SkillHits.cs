using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Hitting targets with skills (port of the LD57 SkillHit). Uses the same model as the melee attack:
    /// <see cref="Health.TakeDamage"/> with the caster's team (so i-frames and teams work the same way),
    /// targets found by distance on the torus surface, stun through <see cref="StatusEffects"/>.
    /// </summary>
    public static class SkillHits
    {
        private static readonly List<Health> areaBuffer = new List<Health>();

        /// <summary>
        /// Damages one target. <paramref name="direction"/> is the world direction of the push.
        /// The damage is scaled by the caster's progression. Returns false when the hit was blocked.
        /// </summary>
        public static bool Hit(SkillContext context, Health target, Vector3 direction, int baseDamage, float knockback, float stun)
        {
            if (target == null || target.IsDead)
                return false;

            var push = direction.sqrMagnitude > 0.0001f ? direction.normalized : context.User.TorusObject.Facing;
            var damage = new DamageInfo(context.User.ScaleDamage(baseDamage), push, knockback, context.User.Team);
            if (target.TakeDamage(damage) == false)
                return false;

            if (stun > 0f && target.TryGetComponent<StatusEffects>(out var statusEffects))
                statusEffects.ApplyStun(stun);

            return true;
        }

        /// <summary>
        /// Damages every target of the other team within the radius around a point on the surface, pushing them away from it.
        /// Targets in <paramref name="alreadyHit"/> are skipped, and the hit ones are added to it and to <paramref name="hits"/>.
        /// Returns the number of hit targets.
        /// </summary>
        public static int HitArea(SkillContext context, TorusAngles center, float radius, int baseDamage, float knockback, float stun,
            HashSet<Health> alreadyHit = null, List<Health> hits = null)
        {
            areaBuffer.Clear();
            CombatQueries.FindInRadius(center, context.User.Team, radius, areaBuffer);

            int count = 0;
            for (int i = 0; i < areaBuffer.Count; i++)
            {
                var target = areaBuffer[i];
                if (alreadyHit != null && alreadyHit.Add(target) == false)
                    continue;

                var direction = -target.TorusObject.GetSurfaceOffsetTo(center);
                if (Hit(context, target, direction, baseDamage, knockback, stun) == false)
                    continue;

                hits?.Add(target);
                count++;
            }

            return count;
        }
    }
}
