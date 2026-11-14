using UnityEngine;
using WSFGameJam2026.Cameras;
using WSFGameJam2026.Enemies;

namespace WSFGameJam2026.Combat
{
    /// <summary>
    /// Feel of hits (port of the LD57 HitFeedback): hit stop and a small shake when the player hits an enemy,
    /// a bigger shake when the player takes damage. One component in the scene.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [SerializeField]
        private Health playerHealth;

        [SerializeField]
        private TorusFollowCamera followCamera;

        [Header("Player hits an enemy")]
        [SerializeField]
        [Min(0f)]
        private float enemyHitStop = 0.06f;

        [SerializeField]
        [Min(0f)]
        private float enemyKillHitStop = 0.1f;

        [SerializeField]
        private Vector2 enemyHitShake = new Vector2(0.15f, 0.1f);

        [Header("Player is hit")]
        [SerializeField]
        [Min(0f)]
        private float playerHitStop = 0.04f;

        [SerializeField]
        private Vector2 playerHitShake = new Vector2(0.35f, 0.2f);

        private void Reset()
        {
            followCamera = FindAnyObjectByType<TorusFollowCamera>();
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyDamaged += Enemy_OnAnyEnemyDamaged;
            if (playerHealth != null)
                playerHealth.OnDamaged += PlayerHealth_OnDamaged;
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyDamaged -= Enemy_OnAnyEnemyDamaged;
            if (playerHealth != null)
                playerHealth.OnDamaged -= PlayerHealth_OnDamaged;
        }

        private void Enemy_OnAnyEnemyDamaged(Enemy enemy, DamageInfo damage)
        {
            HitStop.Freeze(enemy.Health.IsDead ? enemyKillHitStop : enemyHitStop);
            Shake(enemyHitShake);
        }

        private void PlayerHealth_OnDamaged(Health health, DamageInfo damage)
        {
            HitStop.Freeze(playerHitStop);
            Shake(playerHitShake);
        }

        private void Shake(Vector2 amplitudeAndDuration)
        {
            if (followCamera != null)
                followCamera.Shake(amplitudeAndDuration.x, amplitudeAndDuration.y);
        }
    }
}
