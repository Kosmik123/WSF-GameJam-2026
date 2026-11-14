using System;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Combat
{
    /// <summary>
    /// Hit points of the player or an enemy. Every enabled Health registers itself, so attacks find their targets
    /// by distance on the torus surface (<see cref="CombatQueries"/>) instead of colliders and physics.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class Health : MonoBehaviour
    {
        private static readonly List<Health> active = new List<Health>();

        /// <summary>Damage taken (after invulnerability check). Not raised for blocked hits.</summary>
        public event Action<Health, DamageInfo> OnDamaged;

        /// <summary>Hit points changed by damage or healing. The argument is the change (negative = damage).</summary>
        public event Action<int> OnHealthChanged;

        public event Action<Health> OnDied;

        [SerializeField]
        private CombatTeam team = CombatTeam.Enemy;

        [SerializeField]
        [Min(1)]
        private int maxHealth = 30;

        [Tooltip("Radius of the body on the surface. Attacks reach the edge of the body, not only its centre.")]
        [SerializeField]
        [Min(0f)]
        private float bodyRadius = 0.5f;

        private int currentHealth;
        private TorusObject torusObject;
        private Knockback knockback;
        private float invulnerableUntil = -1f;

        /// <summary>All enabled, living and dead Health components. Do not modify.</summary>
        public static IReadOnlyList<Health> Active => active;

        public CombatTeam Team => team;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public float BodyRadius => bodyRadius;
        public TorusObject TorusObject => torusObject;
        public bool IsDead => currentHealth <= 0;
        public float Normalized => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        /// <summary>Set by code that needs i-frames for an open-ended time (for example the dodge). Healing always works.</summary>
        public bool IsInvulnerable { get; set; }

        private bool IsProtected => IsInvulnerable || Time.time < invulnerableUntil;

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
            knockback = GetComponent<Knockback>();
            currentHealth = maxHealth;
        }

        private void OnEnable()
        {
            active.Add(this);
        }

        private void OnDisable()
        {
            active.Remove(this);
        }

        /// <summary>Changes the maximum (enemy level, player level). Optionally fills the hit points to the new maximum.</summary>
        public void SetMaxHealth(int newMaxHealth, bool fill)
        {
            maxHealth = Mathf.Max(1, newMaxHealth);
            currentHealth = fill ? maxHealth : Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(0);
        }

        /// <summary>Grants i-frames for the given time, for example after the player was hit.</summary>
        public void MakeInvulnerable(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        }

        /// <summary>Applies a hit. Returns false when the hit was blocked (dead, invulnerable, same team).</summary>
        public bool TakeDamage(DamageInfo damage)
        {
            if (IsDead || IsProtected || damage.SourceTeam == team || damage.Amount <= 0)
                return false;

            currentHealth = Mathf.Max(0, currentHealth - damage.Amount);
            if (knockback != null && damage.Knockback > 0f)
                knockback.Apply(damage.Direction, damage.Knockback);

            OnDamaged?.Invoke(this, damage);
            OnHealthChanged?.Invoke(-damage.Amount);
            if (currentHealth <= 0)
                OnDied?.Invoke(this);

            return true;
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0)
                return;

            int before = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth - before);
        }

        /// <summary>Back to full hit points, also after death (restart of the run).</summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            invulnerableUntil = -1f;
            IsInvulnerable = false;
            OnHealthChanged?.Invoke(0);
        }
    }
}
