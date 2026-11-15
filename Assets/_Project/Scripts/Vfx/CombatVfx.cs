using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Effects of the fight (port of the LD57 HitSplash, DeathEffects and SwordTrail ideas): a splash of cubes in the color of
    /// an enemy when it is hit, a burst when it dies, a red splash when the player is hurt, a puff and a trail when the player
    /// dodges and an arc of the sword for every swing. Only listens to events, the fight knows nothing about it. One per scene.
    /// </summary>
    public class CombatVfx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        private PlayerCombat playerCombat;

        [Header("Enemies")]
        [Tooltip("Color of enemies whose material has no color (textured models).")]
        [SerializeField]
        private Color fallbackEnemyColor = new Color(0.4f, 0.9f, 0.3f);

        [SerializeField]
        [Min(0)]
        private int hitCubes = 10;

        [SerializeField]
        [Min(0)]
        private int deathCubes = 18;

        [Header("Player")]
        [SerializeField]
        private Color playerHurtColor = new Color(1f, 0.15f, 0.12f);

        [Tooltip("Height of the chest of the player above its pivot.")]
        [SerializeField]
        private float playerChestHeight = 0.9f;

        [Header("Sword")]
        [Tooltip("Radius of the arc. Match the range of the attack in PlayerCombat.")]
        [SerializeField]
        [Min(0.1f)]
        private float swordRadius = 2.2f;

        [Tooltip("Full angle of the arc. Match the arc of the attack in PlayerCombat.")]
        [SerializeField]
        [Range(10f, 360f)]
        private float swordArcDegrees = 140f;

        [Tooltip("Combo length of PlayerCombat: the last step gets the finisher arc.")]
        [SerializeField]
        [Min(1)]
        private int comboLength = 3;

        [SerializeField]
        [Min(0.05f)]
        private float swingDuration = 0.18f;

        [SerializeField]
        private Color swordColor = new Color(1f, 0.8f, 0.45f);

        [SerializeField]
        private Color finisherColor = new Color(1f, 0.45f, 0.15f);

        [Header("Dodge")]
        [SerializeField]
        private Color dodgeColor = new Color(0.7f, 0.85f, 1f);

        [SerializeField]
        [Min(0f)]
        private float dodgeTrailTime = 0.25f;

        private readonly Dictionary<Enemy, Color> enemyColors = new Dictionary<Enemy, Color>();
        private Health playerHealth;
        private TorusObject playerTorusObject;

        private void Reset()
        {
            playerCombat = FindAnyObjectByType<PlayerCombat>();
        }

        private void Awake()
        {
            if (playerCombat == null)
                return;

            playerCombat.TryGetComponent(out playerHealth);
            playerCombat.TryGetComponent(out playerTorusObject);
        }

        private void OnEnable()
        {
            Enemy.OnAnyEnemyDamaged += Enemy_OnAnyEnemyDamaged;
            Enemy.OnAnyEnemyDied += Enemy_OnAnyEnemyDied;
            if (playerCombat)
            {
                playerCombat.OnAttackStarted += PlayerCombat_OnAttackStarted;
                playerCombat.OnDodgeStarted += PlayerCombat_OnDodgeStarted;
            }

            if (playerHealth)
                playerHealth.OnDamaged += PlayerHealth_OnDamaged;
        }

        private void Enemy_OnAnyEnemyDamaged(Enemy enemy, DamageInfo damage)
        {
            var bodyColor = GetColor(enemy);
            var up = enemy.transform.up;
            var position = enemy.transform.position + up * 0.1f;
            var glow = new Color(bodyColor.r * 0.6f + 0.4f, bodyColor.g * 0.6f + 0.4f, bodyColor.b * 0.6f + 0.4f);
            StylizedFx.CubeBurst(position, up, bodyColor, hitCubes, 0.16f, 4f, 0.45f, damage.Direction);
            StylizedFx.Burst(position, up, glow, 3, 0.09f, 5f, 0.3f);
            StylizedFx.Flash(position, Color.white, 0.45f, 0.1f);
        }

        private void Enemy_OnAnyEnemyDied(Enemy enemy)
        {
            var bodyColor = GetColor(enemy);
            enemyColors.Remove(enemy);

            var up = enemy.transform.up;
            var center = enemy.transform.position + up * 0.2f;
            var ground = enemy.TorusObject ? VfxGround.Below(enemy.TorusObject, out up) : enemy.transform.position;
            StylizedFx.CubeBurst(center, up, bodyColor, deathCubes, 0.24f, 5f, 0.6f);
            StylizedFx.Burst(center, up, Color.Lerp(bodyColor, Color.white, 0.5f), 6, 0.12f, 6f, 0.4f);
            StylizedFx.Flash(center, Color.Lerp(bodyColor, Color.white, 0.4f), 1f, 0.16f);
            StylizedFx.Ring(ground, up, 1.2f, bodyColor, 16, 0.4f);
            StylizedFx.Puddle(ground, up, 0.7f, bodyColor * 0.7f, 2.5f);
            for (int i = 0; i < 3; i++)
                StylizedFx.SmokePuff(center, up, 0.8f, 0.4f);
        }

        private void PlayerHealth_OnDamaged(Health health, DamageInfo damage)
        {
            var player = health.transform;
            var chest = player.position + player.up * playerChestHeight;
            StylizedFx.CubeBurst(chest, player.up, playerHurtColor, 8, 0.14f, 4f, 0.45f, damage.Direction);
            StylizedFx.Flash(chest, playerHurtColor, 0.6f, 0.12f);
        }

        private void PlayerCombat_OnAttackStarted(int comboStep)
        {
            bool isFinisher = comboLength > 1 && comboStep == comboLength - 1;
            var player = playerCombat.transform;
            float radius = isFinisher ? swordRadius * 1.15f : swordRadius;
            var color = isFinisher ? finisherColor : swordColor;
            StylizedFx.SwordArc(player, radius, swordArcDegrees, comboStep % 2 == 1, color, swingDuration, playerChestHeight * 0.85f);
            if (isFinisher == false)
                return;

            var front = player.position + player.up * playerChestHeight + player.forward * radius * 0.6f;
            StylizedFx.Burst(front, player.up, finisherColor, 8, 0.12f, 6f, 0.35f);
        }

        private void PlayerCombat_OnDodgeStarted()
        {
            var player = playerCombat.transform;
            var up = player.up;
            var ground = playerTorusObject ? VfxGround.Below(playerTorusObject, out up) : player.position;
            for (int i = 0; i < 4; i++)
                StylizedFx.SmokePuff(ground + up * 0.2f, up, 0.5f, 0.35f);

            StylizedFx.Ring(ground, up, 0.9f, dodgeColor, 12, 0.3f);
            StylizedFx.Dissolve(ground, up, dodgeColor, 8, 1.4f);
            StylizedFx.AddTrail(playerCombat.gameObject, dodgeColor, 0.12f, 0.35f, false, playerChestHeight * 0.7f, dodgeTrailTime);
        }

        private Color GetColor(Enemy enemy)
        {
            if (enemyColors.TryGetValue(enemy, out var cached))
                return cached;

            var color = FindBodyColor(enemy.gameObject);
            enemyColors[enemy] = color;
            return color;
        }

        private Color FindBodyColor(GameObject body)
        {
            foreach (var bodyRenderer in body.GetComponentsInChildren<Renderer>())
            {
                if (bodyRenderer is ParticleSystemRenderer || bodyRenderer is TrailRenderer || bodyRenderer is LineRenderer)
                    continue;

                var material = bodyRenderer.sharedMaterial;
                if (material == null)
                    continue;

                if (material.HasProperty(BaseColorId))
                    return Usable(material.GetColor(BaseColorId));

                if (material.HasProperty(ColorId))
                    return Usable(material.GetColor(ColorId));
            }

            return fallbackEnemyColor;
        }

        // A white base color means a textured model: the texture decides the look, so a fixed color fits better.
        private Color Usable(Color color)
        {
            return color.r + color.g + color.b > 2.7f ? fallbackEnemyColor : color;
        }

        private void OnDisable()
        {
            Enemy.OnAnyEnemyDamaged -= Enemy_OnAnyEnemyDamaged;
            Enemy.OnAnyEnemyDied -= Enemy_OnAnyEnemyDied;
            if (playerCombat)
            {
                playerCombat.OnAttackStarted -= PlayerCombat_OnAttackStarted;
                playerCombat.OnDodgeStarted -= PlayerCombat_OnDodgeStarted;
            }

            if (playerHealth)
                playerHealth.OnDamaged -= PlayerHealth_OnDamaged;
        }
    }
}
