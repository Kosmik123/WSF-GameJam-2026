using UnityEngine;
using WSFGameJam2026.Enemies;

namespace WSFGameJam2026.Progression
{
    /// <summary>
    /// Drops <see cref="ExperienceOrb"/>s when an enemy dies (LD57: essence crystals from every enemy). The XP reward of the
    /// enemy (which grows with its level) is split into a few orbs.
    /// </summary>
    public class ExperienceDropper : MonoBehaviour
    {
        [SerializeField]
        private Experience player;

        [SerializeField]
        private ExperienceOrb orbPrefab;

        [Tooltip("XP carried by one orb at most; bigger rewards are split into more orbs.")]
        [SerializeField]
        [Min(1)]
        private int xpPerOrb = 5;

        [SerializeField]
        [Min(1)]
        private int maxOrbsPerEnemy = 8;

        private void Reset()
        {
            player = FindAnyObjectByType<Experience>();
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyDied += Enemy_OnAnyEnemyDied;
        }

        private void Enemy_OnAnyEnemyDied(Enemy enemy)
        {
            if (player == null || orbPrefab == null || enemy.ExperienceReward <= 0)
                return;

            int reward = enemy.ExperienceReward;
            int count = Mathf.Clamp(Mathf.CeilToInt((float)reward / xpPerOrb), 1, maxOrbsPerEnemy);
            var source = enemy.TorusObject;
            for (int i = 0; i < count; i++)
            {
                // Spread the reward evenly; the first orbs take the remainder.
                int value = reward / count + (i < reward % count ? 1 : 0);
                var direction = Quaternion.AngleAxis(360f * (i + Random.value * 0.5f) / count, source.Up) * source.Facing;
                var orb = Instantiate(orbPrefab);
                orb.Launch(source.World, source.Angles, direction, value, player);
            }
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyDied -= Enemy_OnAnyEnemyDied;
        }
    }
}
