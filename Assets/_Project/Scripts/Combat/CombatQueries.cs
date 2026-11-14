using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Combat
{
    /// <summary>Finding targets on the torus surface. Distances are measured along the surface (see <see cref="TorusObject.GetSurfaceOffsetTo(TorusObject)"/>).</summary>
    public static class CombatQueries
    {
        /// <summary>
        /// Adds to <paramref name="results"/> every living target of another team whose body touches a circular sector
        /// in front of the attacker: radius <paramref name="range"/>, full angle <paramref name="arcDegrees"/>.
        /// </summary>
        public static void FindInArc(TorusObject attacker, CombatTeam attackerTeam, float range, float arcDegrees, List<Health> results)
        {
            float halfArc = arcDegrees * 0.5f;
            var targets = Health.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Team == attackerTeam || target.IsDead || target.TorusObject == attacker)
                    continue;

                var offset = attacker.GetSurfaceOffsetTo(target.TorusObject);
                float distance = offset.magnitude;
                if (distance > range + target.BodyRadius)
                    continue;

                // Targets touching the attacker are always hit, even when they are slightly behind.
                if (distance > target.BodyRadius && Vector3.Angle(attacker.Facing, offset) > halfArc)
                    continue;

                results.Add(target);
            }
        }

        /// <summary>Like <see cref="FindInArc"/>, but in a full circle around a point on the surface.</summary>
        public static void FindInRadius(TorusObject center, CombatTeam attackerTeam, float radius, List<Health> results)
        {
            FindInArc(center, attackerTeam, radius, 360f, results);
        }

        /// <summary>
        /// Like <see cref="FindInRadius(TorusObject, CombatTeam, float, List{Health})"/>, but around any point of the surface
        /// (an explosion, a burning spot), not around an object. The distance is measured from each target to the point.
        /// </summary>
        public static void FindInRadius(TorusAngles center, CombatTeam attackerTeam, float radius, List<Health> results)
        {
            var targets = Health.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Team == attackerTeam || target.IsDead)
                    continue;

                if (target.TorusObject.GetSurfaceOffsetTo(center).magnitude > radius + target.BodyRadius)
                    continue;

                results.Add(target);
            }
        }

        /// <summary>
        /// The living target of another team closest to the given object (distance along the surface), within the range.
        /// Targets in <paramref name="exclude"/> are skipped. Returns null when there is none.
        /// </summary>
        public static Health FindNearest(TorusObject from, CombatTeam attackerTeam, float range, ICollection<Health> exclude = null)
        {
            Health nearest = null;
            float nearestDistance = float.MaxValue;
            var targets = Health.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Team == attackerTeam || target.IsDead || target.TorusObject == from)
                    continue;

                if (exclude != null && exclude.Contains(target))
                    continue;

                float distance = from.GetSurfaceOffsetTo(target.TorusObject).magnitude - target.BodyRadius;
                if (distance > range || distance >= nearestDistance)
                    continue;

                nearest = target;
                nearestDistance = distance;
            }

            return nearest;
        }
    }
}
