using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Skills;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Basic attack of the mage (port of the LD57 RangedAttack): while the attack button is held, shoots a projectile
    /// forward along the surface every cooldown. Turned on by <see cref="PlayerClassApplier"/> for ranged classes,
    /// which also turns off the melee swing of <see cref="PlayerCombat"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class RangedAttack : MonoBehaviour
    {
        /// <summary>A shot was fired. For animations and sounds.</summary>
        public event Action OnShot;

        [SerializeField]
        private InputActionReference attackAction;

        [SerializeField]
        private SkillProjectile projectilePrefab;

        [SerializeField]
        [Min(1)]
        private int damage = 8;

        [Tooltip("Time between shots in seconds.")]
        [SerializeField]
        [Min(0.05f)]
        private float cooldown = 0.35f;

        [SerializeField]
        [Min(0f)]
        private float speed = 22f;

        [SerializeField]
        [Min(0f)]
        private float range = 26f;

        [SerializeField]
        [Min(0f)]
        private float knockback = 0.5f;

        private PlayerCombat combat;
        private TorusObject torusObject;
        private Health health;
        private float nextShotTime;

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            torusObject = GetComponent<TorusObject>();
            health = GetComponent<Health>();
        }

        /// <summary>Sets the numbers of the class (damage per shot, time between shots).</summary>
        public void Configure(int newDamage, float newCooldown)
        {
            damage = Mathf.Max(1, newDamage);
            cooldown = Mathf.Max(0.05f, newCooldown);
        }

        private void Update()
        {
            if (attackAction == null || projectilePrefab == null)
                return;

            // Paused (menu, level-up cards): no shots.
            if (Time.timeScale <= 0f || health.IsDead || combat.IsDodging || Time.time < nextShotTime)
                return;

            if (attackAction.action.IsPressed() == false)
                return;

            Shoot();
        }

        private void Shoot()
        {
            nextShotTime = Time.time + cooldown / Mathf.Max(0.1f, combat.AttackSpeedMultiplier);
            int amount = Mathf.Max(1, Mathf.RoundToInt(damage * combat.DamageMultiplier));

            var projectile = Instantiate(projectilePrefab);
            projectile.Launch(torusObject.World, torusObject.Angles, torusObject.Facing, speed, range, health.Team,
                (endedProjectile, target) => HitTarget(endedProjectile, target, amount));
            OnShot?.Invoke();
        }

        private void HitTarget(SkillProjectile projectile, Health target, int amount)
        {
            if (target)
                target.TakeDamage(new DamageInfo(amount, projectile.TorusObject.Facing, knockback, health.Team));
        }
    }
}
