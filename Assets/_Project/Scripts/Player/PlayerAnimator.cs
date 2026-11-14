using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Drives the Animator of the character model (port of the LD57 PlayerAnimations): walking forward and back from the
    /// movement input, the 3 swings of the combo, the dodge, casting (mage shots and skills), getting hit and death.
    /// The model comes from <see cref="PlayerClassApplier"/>, so the Animator is taken from it when the model changes.
    /// </summary>
    [RequireComponent(typeof(TorusPlayerMovement), typeof(PlayerCombat), typeof(Health))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int TurnId = Animator.StringToHash("Turn");
        private static readonly int AttackId = Animator.StringToHash("Attack");
        private static readonly int AttackIndexId = Animator.StringToHash("AttackIndex");
        private static readonly int CastId = Animator.StringToHash("Cast");
        private static readonly int DodgeId = Animator.StringToHash("Dodge");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DeadId = Animator.StringToHash("Dead");

        [Tooltip("Controller set on the model (Animations/Player/Player.controller, made by the setup menu).")]
        [SerializeField]
        private RuntimeAnimatorController controller;

        [Tooltip("Used when there is no PlayerClassApplier: the Animator in the children.")]
        [SerializeField]
        private Animator animator;

        private TorusPlayerMovement movement;
        private PlayerCombat combat;
        private Health health;
        private PlayerClassApplier classApplier;
        private RangedAttack rangedAttack;
        private SkillUser skillUser;

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
            if (controller)
                animator.runtimeAnimatorController = controller;
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
            if (IsReady)
                animator.SetTrigger(CastId);
        }

        private void Update()
        {
            if (IsReady == false)
                return;

            var input = movement.IsLocked ? Vector2.zero : movement.SmoothedInput;
            animator.SetFloat(SpeedId, input.y * movement.SpeedMultiplier);
            animator.SetFloat(TurnId, input.x);
            animator.SetBool(DeadId, health.IsDead);
            if (combat.IsDodging == false)
                animator.SetBool(DodgeId, false);
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
