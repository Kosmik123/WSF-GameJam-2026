using System;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Head;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Spawns groups of enemies on the torus a few seconds ahead of the player (GDD 8 and 9).
    /// The level of every enemy is set once, at spawn, from the difficulty of the place where it appears
    /// (distance from the jaw along the run direction) and the round multiplier. Enemies far behind the player are removed.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Serializable]
        private class SpawnEntry
        {
            [SerializeField]
            private Enemy prefab;
            public Enemy Prefab => prefab;

            [SerializeField]
            [Min(0f)]
            private float weight = 1f;
            public float Weight => weight;

            [Tooltip("The enemy appears only where the difficulty (0-1) is at least this.")]
            [SerializeField]
            [Range(0f, 1f)]
            private float minDifficulty;
            public float MinDifficulty => minDifficulty;

            [Tooltip("Size of the group (min, max).")]
            [SerializeField]
            private Vector2Int groupSize = new Vector2Int(1, 1);
            public Vector2Int GroupSize => groupSize;
        }

        [SerializeField]
        private TorusWorld world;

        [SerializeField]
        private Health player;

        [Tooltip("Optional. Without it the difficulty depends only on the round.")]
        [SerializeField]
        private HeadMovement head;

        [SerializeField]
        private List<SpawnEntry> entries = new List<SpawnEntry>();

        [Header("Timing")]
        [SerializeField]
        [Min(0.1f)]
        private float spawnInterval = 4f;

        [SerializeField]
        [Min(0f)]
        private float firstSpawnDelay = 2f;

        [Tooltip("Alive enemies at once, at round multiplier 1.")]
        [SerializeField]
        [Min(1)]
        private int maxAlive = 8;

        [Header("Placement")]
        [Tooltip("+1 when the player runs towards growing theta (the same way as the head), -1 otherwise.")]
        [SerializeField]
        private int runDirection = 1;

        [Tooltip("Distance ahead of the player along the body (min, max). About the player's speed × a few seconds.")]
        [SerializeField]
        private Vector2 spawnAheadDistance = new Vector2(20f, 30f);

        [Tooltip("Random spread around the tube relative to the player, in degrees.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float phiSpreadDegrees = 50f;

        [Tooltip("Distance between enemies of one group.")]
        [SerializeField]
        [Min(0f)]
        private float groupSpread = 2.5f;

        [Tooltip("Enemies behind the player further than this are removed.")]
        [SerializeField]
        [Min(1f)]
        private float despawnDistanceBehind = 30f;

        [Header("Difficulty")]
        [Tooltip("Level at difficulty 0 (just ahead of the jaw) and 1 (right behind it, at the top of the head).")]
        [SerializeField]
        private Vector2Int levelRange = new Vector2Int(1, 10);

        [Tooltip("How the difficulty grows with the distance from the jaw (x = 0..1 distance, y = 0..1 difficulty).")]
        [SerializeField]
        private AnimationCurve difficultyCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private readonly List<Enemy> alive = new List<Enemy>();
        public IReadOnlyList<Enemy> Alive => alive;

        private float nextSpawnTime;

        /// <summary>Set by the round loop. Scales the enemy level and the number of enemies at once. 1 = first round.</summary>
        public float RoundMultiplier { get; set; } = 1f;

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
            head = FindAnyObjectByType<HeadMovement>();
        }

        private void OnEnable()
        {
            nextSpawnTime = Time.time + firstSpawnDelay;
            Enemy.OnAnyEnemyDied += Enemy_OnAnyEnemyDied;
        }

        /// <summary>Difficulty 0-1 of a place along the body: 0 just ahead of the jaw, 1 right behind it (GDD 9).</summary>
        public float GetDifficulty(float theta)
        {
            if (head == null)
                return 0f;

            float distanceFromJaw = Mathf.Repeat((theta - head.Theta) * Mathf.Sign(runDirection), TorusGeometry.TwoPi) / TorusGeometry.TwoPi;
            return Mathf.Clamp01(difficultyCurve.Evaluate(distanceFromJaw));
        }

        /// <summary>Level given to an enemy spawned at this theta. It never changes later.</summary>
        public int GetLevel(float difficulty)
        {
            float baseLevel = Mathf.Lerp(levelRange.x, levelRange.y, difficulty);
            return Mathf.Max(1, Mathf.RoundToInt(baseLevel * RoundMultiplier));
        }

        /// <summary>Removes all enemies (restart of the run or a new round).</summary>
        public void Clear()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
                if (alive[i])
                    alive[i].Despawn();

            alive.Clear();
            nextSpawnTime = Time.time + firstSpawnDelay;
        }

        private void Enemy_OnAnyEnemyDied(Enemy enemy)
        {
            alive.Remove(enemy);
        }

        private void Update()
        {
            if (world == null || player == null || player.IsDead)
                return;

            alive.RemoveAll(enemy => enemy == null);
            DespawnFarBehind();

            if (Time.time < nextSpawnTime)
                return;

            nextSpawnTime = Time.time + spawnInterval / Mathf.Max(0.1f, RoundMultiplier);
            int capacity = Mathf.RoundToInt(maxAlive * RoundMultiplier) - alive.Count;
            if (capacity > 0)
                SpawnGroup(capacity);
        }

        private void SpawnGroup(int capacity)
        {
            var playerObject = player.TorusObject;
            var surface = world.Surface;
            float sign = Mathf.Sign(runDirection);

            // Centre of the group: ahead of the player along the body, spread around the tube.
            float aheadDistance = UnityEngine.Random.Range(spawnAheadDistance.x, spawnAheadDistance.y);
            float phi = playerObject.Angles.Phi + UnityEngine.Random.Range(-phiSpreadDegrees, phiSpreadDegrees) * Mathf.Deg2Rad;
            float ringRadius = Mathf.Max(0.1f, TorusGeometry.GetRingRadius(surface.MajorRadius, surface.MinorRadius, phi));
            float theta = playerObject.Angles.Theta + sign * aheadDistance / ringRadius;

            float difficulty = GetDifficulty(theta);
            var entry = PickEntry(difficulty);
            if (entry == null || entry.Prefab == null)
                return;

            int level = GetLevel(difficulty);
            int count = Mathf.Min(capacity, UnityEngine.Random.Range(entry.GroupSize.x, entry.GroupSize.y + 1));
            for (int i = 0; i < count; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * groupSpread;
                float memberTheta = theta + offset.x / ringRadius;
                float memberPhi = phi + offset.y / surface.MinorRadius;
                var angles = new TorusAngles(memberTheta, memberPhi);

                // Facing back, towards the incoming player.
                var facing = world.transform.TransformDirection(TorusGeometry.GetThetaDirection(memberTheta)) * -sign;

                var enemy = Instantiate(entry.Prefab, transform);
                enemy.Initialize(world, angles, facing, player, level);
                alive.Add(enemy);
            }
        }

        private SpawnEntry PickEntry(float difficulty)
        {
            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].MinDifficulty <= difficulty)
                    totalWeight += entries[i].Weight;

            if (totalWeight <= 0f)
                return null;

            float pick = UnityEngine.Random.value * totalWeight;
            SpawnEntry lastAllowed = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].MinDifficulty > difficulty)
                    continue;

                lastAllowed = entries[i];
                pick -= entries[i].Weight;
                if (pick <= 0f)
                    return entries[i];
            }

            return lastAllowed;
        }

        private void DespawnFarBehind()
        {
            var playerObject = player.TorusObject;
            float sign = Mathf.Sign(runDirection);
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var enemy = alive[i];
                if (enemy.Health.IsDead)
                    continue;

                float thetaDelta = playerObject.Angles.DeltaTo(enemy.TorusObject.Angles).x * sign;
                if (thetaDelta >= 0f)
                    continue;

                if (playerObject.GetSurfaceOffsetTo(enemy.TorusObject).magnitude > despawnDistanceBehind)
                {
                    enemy.Despawn();
                    alive.RemoveAt(i);
                }
            }
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyDied -= Enemy_OnAnyEnemyDied;
        }
    }
}
