using UnityEngine;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Shooter (ranged attack), based on the LD57 ShooterController: keeps a distance band from the target,
    /// strafes to the side (changing sides), runs away when the target is too close, and shoots after a telegraphed
    /// windup (flash and an aim line). Projectiles fly along the torus surface.
    /// </summary>
    public class ShooterEnemy : EnemyBehaviour
    {
        [Header("Distance")]
        [Tooltip("Middle of the distance band the shooter keeps.")]
        [SerializeField]
        [Min(0f)]
        private float keepDistance = 9f;

        [Tooltip("Half width of the band.")]
        [SerializeField]
        [Min(0f)]
        private float keepBand = 1.5f;

        [Tooltip("Target closer than this: the shooter runs away.")]
        [SerializeField]
        [Min(0f)]
        private float fleeDistance = 4f;

        [SerializeField]
        [Min(0f)]
        private float strafeSwitchTime = 1.5f;

        [SerializeField]
        [Range(0f, 1f)]
        private float strafeSpeedMultiplier = 0.6f;

        [Header("Shooting")]
        [SerializeField]
        [Min(0f)]
        private float shootRange = 14f;

        [SerializeField]
        [Min(0f)]
        private float cooldown = 2f;

        [SerializeField]
        [Min(0f)]
        private float windup = 0.7f;

        [SerializeField]
        private Color telegraphColor = new Color(1f, 0.85f, 0.15f);

        [SerializeField]
        private EnemyProjectile projectilePrefab;

        [SerializeField]
        [Min(0f)]
        private float projectileSpeed = 12f;

        [SerializeField]
        [Min(0f)]
        private float projectileLifetime = 2.5f;

        [SerializeField]
        [Min(1)]
        private int damage = 6;

        [SerializeField]
        [Min(1)]
        private int burstCount = 1;

        [SerializeField]
        [Min(0f)]
        private float burstInterval = 0.15f;

        [Tooltip("Optional line showing where the shot will go during the windup.")]
        [SerializeField]
        private LineRenderer aimLine;

        [SerializeField]
        [Min(0f)]
        private float aimLineHeight = 0.2f;

        private bool isAware;
        private bool isShooting;
        private float nextShotTime;
        private float nextSideSwitch;
        private float shotTime;
        private int shotsLeft;
        private int strafeSide = 1;
        private Vector3 aimDirection;

        private void Start()
        {
            nextShotTime = Time.time + cooldown;
            strafeSide = Random.value < 0.5f ? -1 : 1;
            SetAimLine(false);
        }

        private void Update()
        {
            if (CanAct == false)
            {
                // A hit during the windup cancels the shot.
                if (isShooting)
                    CancelShot();
                return;
            }

            var offset = OffsetToTarget();
            float distance = offset.magnitude;

            if (isAware == false)
            {
                if (distance > DetectRange)
                    return;
                isAware = true;
            }

            if (isShooting)
            {
                UpdateShooting(offset);
                return;
            }

            if (Time.time >= nextShotTime && distance <= shootRange)
            {
                StartWindup();
                return;
            }

            UpdateMovement(offset, distance);
        }

        private void UpdateMovement(Vector3 offset, float distance)
        {
            var toTarget = distance > 0.01f ? offset / distance : TorusObject.Facing;
            Face(toTarget);

            if (distance < fleeDistance)
            {
                Slide(-toTarget);
                return;
            }

            if (Time.time >= nextSideSwitch)
            {
                nextSideSwitch = Time.time + strafeSwitchTime;
                strafeSide = -strafeSide;
            }

            var side = Vector3.Cross(TorusObject.Up, toTarget) * strafeSide;
            var move = side * strafeSpeedMultiplier;
            move += GetDistanceCorrection(toTarget, distance);

            if (move.sqrMagnitude > 0.0001f)
                Slide(move.normalized, Mathf.Min(1f, move.magnitude));
        }

        /// <summary>Towards the target when too far, away from it when too close, zero inside the band.</summary>
        private Vector3 GetDistanceCorrection(Vector3 toTarget, float distance)
        {
            if (distance > keepDistance + keepBand)
                return toTarget;

            return distance < keepDistance - keepBand ? -toTarget : Vector3.zero;
        }

        private void StartWindup()
        {
            isShooting = true;
            shotsLeft = Mathf.Max(1, burstCount);
            shotTime = Time.time + windup;
            if (BodyFlash)
                BodyFlash.Telegraph(telegraphColor, windup);
            SetAimLine(true);
            RaiseWindupStarted();
        }

        private void UpdateShooting(Vector3 offset)
        {
            Face(offset, 2f);
            aimDirection = offset.sqrMagnitude > 0.0001f ? offset.normalized : TorusObject.Facing;
            UpdateAimLine(offset);

            if (Time.time < shotTime)
                return;

            SetAimLine(false);
            Shoot();
            shotsLeft--;
            if (shotsLeft > 0)
            {
                shotTime = Time.time + burstInterval;
                return;
            }

            isShooting = false;
            nextShotTime = Time.time + cooldown;
            nextSideSwitch = Time.time + strafeSwitchTime;
        }

        private void Shoot()
        {
            if (projectilePrefab == null)
                return;

            RaiseAttackReleased();
            var projectile = Instantiate(projectilePrefab);
            projectile.Launch(TorusObject.World, TorusObject.Angles, aimDirection, projectileSpeed, Enemy.ScaleDamage(damage), projectileLifetime, Health.Team);
        }

        private void CancelShot()
        {
            isShooting = false;
            nextShotTime = Time.time + cooldown * 0.5f;
            SetAimLine(false);
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }

        private void SetAimLine(bool visible)
        {
            if (aimLine)
                aimLine.enabled = visible;
        }

        private void UpdateAimLine(Vector3 offset)
        {
            if (aimLine == null)
                return;

            var start = transform.position + TorusObject.Up * aimLineHeight;
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, start + offset.normalized * Mathf.Min(shootRange, offset.magnitude));
        }

        private void OnDisable()
        {
            isShooting = false;
            SetAimLine(false);
            if (BodyFlash)
                BodyFlash.StopTelegraph();
        }
    }
}
