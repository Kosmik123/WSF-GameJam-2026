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
    }
}
