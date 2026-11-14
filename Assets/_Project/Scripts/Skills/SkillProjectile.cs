using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Projectile of the player flying along the torus surface (port of the LD57 PlayerBolt, without physics):
    /// the mage's basic shot, the fireball, the shots of the arcane orbs. It hits the first living target of another team
    /// within its radius, or ends after its range. It does not deal damage by itself: the one who launched it does it in the
    /// impact callback (single hit, explosion...).
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class SkillProjectile : MonoBehaviour
    {
        /// <summary>Called once at the end of the flight. The target is null when the projectile flew its whole range.</summary>
        public delegate void ImpactHandler(SkillProjectile projectile, Health target);

        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.35f;

        [Tooltip("Distance from the start of the caster, so the projectile does not appear inside the body.")]
        [SerializeField]
        [Min(0f)]
        private float spawnOffset = 0.6f;

        private TorusObject torusObject;
        private ImpactHandler onImpact;
        private CombatTeam team;
        private float speed;
        private float rangeLeft;
        private bool hasEnded;

        public TorusObject TorusObject => torusObject;

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
        }

        public void Launch(TorusWorld world, TorusAngles angles, Vector3 direction, float newSpeed, float range, CombatTeam sourceTeam, ImpactHandler impactHandler)
        {
            torusObject.Place(world, angles, direction);
            torusObject.Move(spawnOffset);
            speed = newSpeed;
            rangeLeft = Mathf.Max(0f, range - spawnOffset);
            team = sourceTeam;
            onImpact = impactHandler;
            hasEnded = false;
        }

        private void Update()
        {
            if (hasEnded)
                return;

            float step = Mathf.Min(speed * Time.deltaTime, rangeLeft);
            torusObject.Move(step);
            rangeLeft -= step;

            var target = FindTarget();
            if (target || rangeLeft <= 0f)
                End(target);
        }

        private Health FindTarget()
        {
            var targets = Health.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Team == team || target.IsDead)
                    continue;

                if (torusObject.GetSurfaceOffsetTo(target.TorusObject).magnitude <= hitRadius + target.BodyRadius)
                    return target;
            }

            return null;
        }

        private void End(Health target)
        {
            hasEnded = true;
            onImpact?.Invoke(this, target);
            Destroy(gameObject);
        }
    }
}
