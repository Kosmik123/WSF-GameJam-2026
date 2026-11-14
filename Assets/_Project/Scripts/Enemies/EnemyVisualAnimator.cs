using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Enemies
{
    /// <summary>
    /// Procedural animation of the LD57 enemy models (port of SlimeAnimator, ShooterAnimator and HitReaction, without DOTween).
    /// Animates the "Visual" child: breathing when standing, hops when moving (slime) or a sway (shooter),
    /// inflating during the attack windup, a kick when the attack is released, a squash and a push on hits.
    /// Mechanics do not know about it: it only listens to <see cref="EnemyBehaviour"/> and <see cref="Health"/> events.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyVisualAnimator : MonoBehaviour
    {
        private static readonly float[] HopKeys = { 1f, 0.8f, 1.25f, 0.85f, 1f };

        [SerializeField]
        private Transform visual;

        [Tooltip("Optional part that grows and glows during the windup (the core of the shooter).")]
        [SerializeField]
        private Transform core;

        [Header("Idle")]
        [SerializeField]
        [Min(0f)]
        private float breathAmount = 0.03f;

        [SerializeField]
        [Min(0.05f)]
        private float breathPeriod = 1.2f;

        [Tooltip("Side sway in degrees (shooter). 0 = none.")]
        [SerializeField]
        [Min(0f)]
        private float swayDegrees;

        [SerializeField]
        [Min(0f)]
        private float swayHz = 1.5f;

        [Header("Moving")]
        [Tooltip("Hops while moving (slime).")]
        [SerializeField]
        private bool hopsWhenMoving = true;

        [SerializeField]
        [Min(0f)]
        private float moveThreshold = 0.3f;

        [SerializeField]
        [Min(0.05f)]
        private float hopPeriod = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float hopHeight = 0.25f;

        [Header("Attack")]
        [SerializeField]
        [Min(0.1f)]
        private float windupScale = 1.3f;

        [SerializeField]
        [Min(1f)]
        private float coreWindupScale = 1.8f;

        [SerializeField]
        [Min(0.01f)]
        private float windupSeconds = 0.5f;

        [Tooltip("Forward kick of the model when the attack is released. Negative = recoil (shooter).")]
        [SerializeField]
        private float releaseKick = 0.5f;

        [Header("Hits")]
        [SerializeField]
        [Range(0.1f, 1f)]
        private float hitSquash = 0.85f;

        [SerializeField]
        [Min(0f)]
        private float hitPunch = 0.15f;

        [Tooltip("How fast the squash and the kicks go back (higher = faster).")]
        [SerializeField]
        [Min(0.1f)]
        private float recoverSharpness = 12f;

        private Health health;
        private EnemyBehaviour behaviour;
        private Vector3 visualBasePosition;
        private Vector3 visualBaseScale;
        private Quaternion visualBaseRotation;
        private Vector3 coreBaseScale;
        private Vector3 lastPosition;
        private float clock;
        private float hopPhase;
        private float windup;
        private float windupTarget;
        private float squash = 1f;
        private Vector3 kick;

        private void Reset()
        {
            visual = transform.Find("Visual");
            if (visual)
                core = visual.Find("Core");
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            TryGetComponent(out behaviour);
            if (visual)
            {
                visualBasePosition = visual.localPosition;
                visualBaseScale = visual.localScale;
                visualBaseRotation = visual.localRotation;
            }

            if (core)
                coreBaseScale = core.localScale;

            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            health.OnDamaged += Health_OnDamaged;
            if (behaviour)
            {
                behaviour.OnWindupStarted += Behaviour_OnWindupStarted;
                behaviour.OnAttackReleased += Behaviour_OnAttackReleased;
            }
        }

        private void Behaviour_OnWindupStarted()
        {
            windupTarget = 1f;
        }

        private void Behaviour_OnAttackReleased()
        {
            windupTarget = 0f;
            kick += Vector3.forward * releaseKick;
        }

        private void Health_OnDamaged(Health damagedHealth, DamageInfo damage)
        {
            // A hit also cancels the windup of the enemy.
            windupTarget = 0f;
            squash = hitSquash;
            if (damage.Direction.sqrMagnitude > 0.0001f)
                kick += transform.InverseTransformDirection(damage.Direction).normalized * hitPunch;
        }

        private void LateUpdate()
        {
            if (visual == null || health.IsDead)
                return;

            float deltaTime = Time.deltaTime;
            clock += deltaTime;
            float speed = deltaTime > 0f ? Vector3.Distance(transform.position, lastPosition) / deltaTime : 0f;
            lastPosition = transform.position;

            float recover = 1f - Mathf.Exp(-recoverSharpness * deltaTime);
            squash = Mathf.Lerp(squash, 1f, recover);
            kick = Vector3.Lerp(kick, Vector3.zero, recover);
            windup = Mathf.MoveTowards(windup, windupTarget, deltaTime / windupSeconds);

            GetIdlePose(speed, deltaTime, out float stretchY, out float lift);
            float inflate = Mathf.Lerp(1f, windupScale, windup);
            float squashWidth = 1f / Mathf.Sqrt(Mathf.Max(0.1f, squash * stretchY));

            visual.localScale = Vector3.Scale(visualBaseScale, new Vector3(squashWidth, squash * stretchY, squashWidth)) * inflate;
            visual.localPosition = visualBasePosition + Vector3.up * lift + kick;
            float sway = swayDegrees * Mathf.Sin(clock * Mathf.PI * 2f * swayHz);
            visual.localRotation = visualBaseRotation * Quaternion.Euler(0f, 0f, sway);

            if (core)
                core.localScale = coreBaseScale * Mathf.Lerp(1f, coreWindupScale, windup);
        }

        // Hop cycle while moving (squash keys and a jump), otherwise breathing.
        private void GetIdlePose(float speed, float deltaTime, out float stretchY, out float lift)
        {
            bool isMoving = hopsWhenMoving && speed > moveThreshold;
            if (isMoving || hopPhase > 0f)
            {
                hopPhase += deltaTime / hopPeriod;
                if (hopPhase >= 1f)
                    hopPhase = isMoving ? hopPhase - 1f : 0f;
            }

            if (hopPhase > 0f)
            {
                float keyPosition = hopPhase * 4f;
                int segment = Mathf.Min(3, (int)keyPosition);
                float blend = Mathf.SmoothStep(0f, 1f, keyPosition - segment);
                stretchY = Mathf.Lerp(HopKeys[segment], HopKeys[segment + 1], blend);
                lift = hopHeight * Mathf.Sin(hopPhase * Mathf.PI);
                return;
            }

            stretchY = 1f + breathAmount * Mathf.Sin(clock * Mathf.PI * 2f / breathPeriod);
            lift = 0f;
        }

        private void OnDisable()
        {
            health.OnDamaged -= Health_OnDamaged;
            if (behaviour)
            {
                behaviour.OnWindupStarted -= Behaviour_OnWindupStarted;
                behaviour.OnAttackReleased -= Behaviour_OnAttackReleased;
            }
        }
    }
}
