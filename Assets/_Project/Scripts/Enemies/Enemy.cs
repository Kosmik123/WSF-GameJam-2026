using System;
using System.Collections;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Common part of every enemy (LD57 EnemyHealth + EnemyDeath): level given once at spawn, scaling of health and damage,
    /// target, global events for feedback and XP, and death (AI off, shrink, destroy).
    /// The behaviour (runner, shooter...) lives in a separate <see cref="EnemyBehaviour"/>.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour
    {
        public delegate void EnemyDamagedHandler(Enemy enemy, DamageInfo damage);

        /// <summary>Any enemy took damage. Used by hit feedback and damage numbers.</summary>
        public static event EnemyDamagedHandler OnAnyEnemyDamaged;

        /// <summary>Any enemy died. Raised before the object is destroyed, so its position and reward can still be read.</summary>
        public static event Action<Enemy> OnAnyEnemyDied;

        [SerializeField]
        [Min(0)]
        private int experienceReward = 10;

        [Header("Level scaling")]
        [Tooltip("Extra health per level above 1, as a fraction of the base (0.25 = +25% per level).")]
        [SerializeField]
        [Min(0f)]
        private float healthPerLevel = 0.25f;

        [Tooltip("Extra damage per level above 1, as a fraction of the base.")]
        [SerializeField]
        [Min(0f)]
        private float damagePerLevel = 0.15f;

        [Tooltip("Extra XP per level above 1, as a fraction of the base.")]
        [SerializeField]
        [Min(0f)]
        private float experiencePerLevel = 0.5f;

        [Header("Spawn and death")]
        [Tooltip("Time after the spawn in which the enemy does not act (it shows up first).")]
        [SerializeField]
        [Min(0f)]
        private float activationDelay = 0.6f;

        [SerializeField]
        [Min(0f)]
        private float deathDuration = 0.35f;

        private Health health;
        public Health Health => health;

        private TorusObject torusObject;
        public TorusObject TorusObject => torusObject;

        private int baseMaxHealth;
        private float activeFrom;
        private Vector3 baseScale;

        /// <summary>Level set once at spawn. It never changes later (GDD 9).</summary>
        public int Level { get; private set; } = 1;

        public Health Target { get; private set; }

        public int ExperienceReward => Mathf.RoundToInt(experienceReward * (1f + experiencePerLevel * (Level - 1)));

        /// <summary>True when the enemy is alive and its spawn delay has passed.</summary>
        public bool IsActive => health.IsDead == false && Time.time >= activeFrom;

        private void Awake()
        {
            health = GetComponent<Health>();
            torusObject = GetComponent<TorusObject>();
            baseMaxHealth = health.MaxHealth;
            baseScale = transform.localScale;
            activeFrom = Time.time + activationDelay;
        }

        private void OnEnable()
        {
            health.OnDamaged += Health_OnDamaged;
            health.OnDied += Health_OnDied;
        }

        /// <summary>Places a freshly spawned enemy and gives it its level and target. Call right after Instantiate.</summary>
        public void Initialize(TorusWorld world, TorusAngles angles, Vector3 facing, Health target, int level)
        {
            Level = Mathf.Max(1, level);
            Target = target;
            torusObject.Place(world, angles, facing);
            health.SetMaxHealth(Mathf.RoundToInt(baseMaxHealth * (1f + healthPerLevel * (Level - 1))), true);
            activeFrom = Time.time + activationDelay;
        }

        /// <summary>Damage of an attack of this enemy after the level scaling.</summary>
        public int ScaleDamage(int baseDamage)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * (1f + damagePerLevel * (Level - 1))));
        }

        /// <summary>Removes the enemy without death effects and without XP (too far behind the player, end of the run).</summary>
        public void Despawn()
        {
            Destroy(gameObject);
        }

        private void Health_OnDamaged(Health damagedHealth, DamageInfo damage)
        {
            OnAnyEnemyDamaged?.Invoke(this, damage);
        }

        private void Health_OnDied(Health deadHealth)
        {
            foreach (var behaviour in GetComponents<EnemyBehaviour>())
                behaviour.enabled = false;

            OnAnyEnemyDied?.Invoke(this);
            StartCoroutine(DyingCo());
        }

        private IEnumerator DyingCo()
        {
            // Short pop, then shrink into the ground. Placeholder until there are death animations.
            float time = 0f;
            while (time < deathDuration)
            {
                time += Time.deltaTime;
                float progress = Mathf.Clamp01(time / deathDuration);
                float scale = progress < 0.2f ? Mathf.Lerp(1f, 1.25f, progress / 0.2f) : Mathf.Lerp(1.25f, 0f, (progress - 0.2f) / 0.8f);
                transform.localScale = baseScale * scale;
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDisable()
        {
            health.OnDamaged -= Health_OnDamaged;
            health.OnDied -= Health_OnDied;
        }
    }
}
