using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Projectile flying along the torus surface (port of the LD57 shooter projectile). Flies straight ahead,
    /// hits the first living target of the other team within its radius and disappears after its lifetime.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float hitRadius = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float knockback = 0.8f;

        private TorusObject torusObject;
        private CombatTeam team;
        private float speed;
        private int damage;
        private float lifeLeft;

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
        }

        public void Launch(TorusWorld world, TorusAngles angles, Vector3 direction, float newSpeed, int newDamage, float lifetime, CombatTeam sourceTeam)
        {
            torusObject.Place(world, angles, direction);
            speed = newSpeed;
            damage = newDamage;
            lifeLeft = lifetime;
            team = sourceTeam;
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;
            torusObject.Move(step);
            lifeLeft -= Time.deltaTime;

            if (TryHit() || lifeLeft <= 0f)
                Destroy(gameObject);
        }

        private bool TryHit()
        {
            var targets = Health.Active;
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Team == team || target.IsDead)
                    continue;

                if (torusObject.GetSurfaceOffsetTo(target.TorusObject).magnitude > hitRadius + target.BodyRadius)
                    continue;

                // A dodging target lets the projectile pass through.
                return target.TakeDamage(new DamageInfo(damage, torusObject.Facing, knockback, team));
            }

            return false;
        }
    }
}
