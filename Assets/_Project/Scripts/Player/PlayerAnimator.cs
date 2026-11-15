using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Drives the Animator of the character model (port of the LD57 PlayerAnimations): locomotion (back, idle, walk, run,
    /// sprint) with damped blending and the step rate following the real speed on the torus, the 3 swings of the combo,
    /// the dodge, the mage shot, one animation per skill (<see cref="SkillDefinition.CastAnimation"/>, played through an
    /// override of the "Skill" state and fitted to the skill's duration), getting hit and death.
    /// The model comes from <see cref="PlayerClassApplier"/>, so the Animator is taken from it when the model changes.
    /// </summary>
    [RequireComponent(typeof(TorusPlayerMovement), typeof(PlayerCombat), typeof(Health))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int TurnId = Animator.StringToHash("Turn");
        private static readonly int MoveRateId = Animator.StringToHash("MoveRate");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int AttackIndexId = Animator.StringToHash("AttackIndex");
        private static readonly int CastId = Animator.StringToHash("Cast");
        private static readonly int SkillSpeedId = Animator.StringToHash("SkillSpeed");
        private static readonly int DodgeId = Animator.StringToHash("Dodge");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int SkillStateId = Animator.StringToHash("Skill");

        [Tooltip("Controller set on the model (Animations/Player/Hero.controller, made by the setup menu).")]
        [SerializeField]
        private RuntimeAnimatorController controller;

        [Tooltip("Clip of the \"Skill\" state in the controller, replaced by the animation of each skill.")]
        [SerializeField]
        private AnimationClip skillPlaceholderClip;

        [Tooltip("Used when there is no PlayerClassApplier: the Animator in the children.")]
        [SerializeField]
        private Animator animator;

        [Header("Locomotion")]
        [Tooltip("Time in which the run blend follows the input. Higher = softer starts and stops.")]
        [SerializeField]
        [Min(0f)]
        private float speedDampTime = 0.12f;

        [Tooltip("Speed on the torus (units per second) at which the run animation plays at its own pace.")]
        [SerializeField]
        [Min(0.1f)]
        private float runAnimationSpeed = 8f;

        [Tooltip("Limits of the step rate, so very slow or very fast movement still looks natural.")]
        [SerializeField]
        private Vector2 moveRateRange = new Vector2(0.75f, 1.6f);

        [Header("Skills")]
        [Tooltip("Cross fade into a skill animation, in seconds.")]
        [SerializeField]
        [Min(0f)]
        private float skillFadeTime = 0.06f;

        private TorusPlayerMovement movement;
        private PlayerCombat combat;
        private Health health;
        private PlayerClassApplier classApplier;
        private RangedAttack rangedAttack;
        private SkillUser skillUser;
        private AnimatorOverrideController overrides;
        private Vector3 lastPosition;
        private float smoothedSpeed;

        private bool IsReady => animator && animator.runtimeAnimatorController;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            movement = GetComponent<TorusPlayerMovement>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
            TryGetComponent(out classApplier);
            TryGetComponent(out rangedAttack);
            TryGetComponent(out skillUser);
            lastPosition = transform.position;

            // The class applier runs earlier, so its model already exists.
            SetAnimator(classApplier && classApplier.ModelAnimator ? classApplier.ModelAnimator : animator);
        }

        private void OnEnable()
        {
            combat.OnAttackStarted += Combat_OnAttackStarted;
            combat.OnDodgeStarted += Combat_OnDodgeStarted;
            combat.OnDodgeEnded += Combat_OnDodgeEnded;
            health.OnDamaged += Health_OnDamaged;
            if (classApplier)
                classApplier.OnModelChanged += SetAnimator;
            if (rangedAttack)
                rangedAttack.OnShot += RangedAttack_OnShot;
            if (skillUser)
                skillUser.OnSkillExecuted += SkillUser_OnSkillExecuted;
        }

        public void SetAnimator(Animator newAnimator)
        {
            animator = newAnimator;
            if (animator == null)
                return;

            // In-place animations: the TorusObject moves the player, not the root motion.
            animator.applyRootMotion = false;
            if (controller == null)
                return;

            // Every model gets its own override controller, so a skill can swap the clip of the "Skill" state.
            overrides = new AnimatorOverrideController(controller);
            animator.runtimeAnimatorController = overrides;
        }

        private void Combat_OnAttackStarted(int comboStep)
        {
            if (IsReady == false)
                return;

            animator.SetInteger(AttackIndexId, comboStep);
            animator.SetTrigger(AttackId);
        }

        private void Combat_OnDodgeStarted()
        {
            if (IsReady)
                animator.SetBool(DodgeId, true);
        }

        private void Combat_OnDodgeEnded()
        {
            if (IsReady)
                animator.SetBool(DodgeId, false);
        }

        private void Health_OnDamaged(Health damagedHealth, DamageInfo damage)
        {
            if (IsReady && health.IsDead == false)
                animator.SetTrigger(HitId);
        }

        private void RangedAttack_OnShot()
        {
            if (IsReady)
                animator.SetTrigger(CastId);
        }

        private void SkillUser_OnSkillExecuted(SkillInstance skill)
        {
            if (IsReady == false || health.IsDead)
                return;

            var clip = skill.Definition.CastAnimation;
            if (clip == null || overrides == null || skillPlaceholderClip == null)
            {
                animator.SetTrigger(CastId);
                return;
            }

            PlaySkill(clip, skill.Definition.CastAnimationDuration);
        }

        // The clip goes into the "Skill" state and is sped up or slowed down to last as long as the skill wants.
        private void PlaySkill(AnimationClip clip, float duration)
        {
            overrides[skillPlaceholderClip] = clip;
            float speed = duration > 0f ? clip.length / duration : 1f;
            animator.SetFloat(SkillSpeedId, Mathf.Clamp(speed, 0.25f, 4f));
            animator.CrossFadeInFixedTime(SkillStateId, skillFadeTime, 0, 0f);
        }

        private void Update()
        {
            if (IsReady == false)
                return;

            float deltaTime = Time.deltaTime;
            var input = movement.IsLocked ? Vector2.zero : movement.SmoothedInput;
            animator.SetFloat(SpeedId, input.y * movement.SpeedMultiplier, speedDampTime, deltaTime);
            animator.SetFloat(TurnId, input.x, speedDampTime, deltaTime);
            animator.SetFloat(MoveRateId, GetMoveRate(deltaTime));
            animator.SetBool(DeadId, health.IsDead);
            if (combat.IsDodging == false)
                animator.SetBool(DodgeId, false);
        }

        // Step rate from the real distance moved, so the feet do not slide when the speed changes (slows, items, levels).
        private float GetMoveRate(float deltaTime)
        {
            if (deltaTime <= 0f)
                return 1f;

            float speed = Vector3.Distance(transform.position, lastPosition) / deltaTime;
            lastPosition = transform.position;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-10f * deltaTime));
            if (smoothedSpeed < 0.2f)
                return 1f;

            return Mathf.Clamp(smoothedSpeed / runAnimationSpeed, moveRateRange.x, moveRateRange.y);
        }

        private void OnDisable()
        {
            combat.OnAttackStarted -= Combat_OnAttackStarted;
            combat.OnDodgeStarted -= Combat_OnDodgeStarted;
            combat.OnDodgeEnded -= Combat_OnDodgeEnded;
            health.OnDamaged -= Health_OnDamaged;
            if (classApplier)
                classApplier.OnModelChanged -= SetAnimator;
            if (rangedAttack)
                rangedAttack.OnShot -= RangedAttack_OnShot;
            if (skillUser)
                skillUser.OnSkillExecuted -= SkillUser_OnSkillExecuted;
        }
    }
}
