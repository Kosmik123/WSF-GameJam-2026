using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Casts the skills in <see cref="SkillSlots"/> from input actions (one per slot) and counts their cooldowns
    /// (port of the LD57 SkillUser, Input System instead of KeyCodes). Also gives skills access to the caster.
    /// </summary>
    [RequireComponent(typeof(SkillSlots), typeof(PlayerCombat))]
    public class SkillUser : MonoBehaviour
    {
        /// <summary>A skill was cast (after it ran). For animations and sounds.</summary>
        public event Action<SkillInstance> OnSkillExecuted;

        [Tooltip("One action per slot, in slot order (Skill1, Skill2...).")]
        [SerializeField]
        private InputActionReference[] slotActions = new InputActionReference[0];

        private SkillSlots slots;
        private PlayerCombat combat;
        private TorusPlayerMovement movement;

        public SkillSlots Slots => slots;
        public TorusObject TorusObject { get; private set; }
        public Health Health { get; private set; }
        public CombatTeam Team => Health.Team;

        /// <summary>Damage multiplier for progression. Shared with the melee attack.</summary>
        public float DamageMultiplier => combat.DamageMultiplier;

        private bool CanCast => Time.timeScale > 0f && Health.IsDead == false && combat.IsDodging == false;

        private void Awake()
        {
            slots = GetComponent<SkillSlots>();
            combat = GetComponent<PlayerCombat>();
            movement = GetComponent<TorusPlayerMovement>();
            TorusObject = GetComponent<TorusObject>();
            Health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            for (int i = 0; i < slotActions.Length; i++)
                if (slotActions[i])
                    slotActions[i].action.performed += SlotAction_Performed;
        }

        /// <summary>Damage of a skill after the progression multiplier.</summary>
        public int ScaleDamage(int baseDamage)
        {
            return baseDamage > 0 ? Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMultiplier)) : 0;
        }

        /// <summary>Direction of the player's movement, or the facing when standing still. Same as the dodge.</summary>
        public Vector3 GetMoveDirection()
        {
            return movement ? movement.GetMoveDirectionOrFacing() : TorusObject.Facing;
        }

        /// <summary>Short name of the key bound to the slot, for the HUD. Empty when the slot has no action.</summary>
        public string GetSlotKeyLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slotActions.Length || slotActions[slotIndex] == null)
                return string.Empty;

            return slotActions[slotIndex].action.GetBindingDisplayString();
        }

        /// <summary>Casts the skill in the slot when it is ready. Returns true when it was cast.</summary>
        public bool TryCast(int slotIndex)
        {
            if (CanCast == false || slotIndex < 0 || slotIndex >= slots.Slots.Count)
                return false;

            var skill = slots.Slots[slotIndex];
            if (skill.IsReady == false)
                return false;

            skill.Definition.Execute(new SkillContext(this, skill.Current, skill.Tier));
            skill.StartCooldown();
            OnSkillExecuted?.Invoke(skill);
            return true;
        }

        private void SlotAction_Performed(InputAction.CallbackContext context)
        {
            for (int i = 0; i < slotActions.Length; i++)
            {
                if (slotActions[i] && slotActions[i].action == context.action)
                {
                    TryCast(i);
                    return;
                }
            }
        }

        private void Update()
        {
            var owned = slots.Slots;
            for (int i = 0; i < owned.Count; i++)
                owned[i].Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            for (int i = 0; i < slotActions.Length; i++)
                if (slotActions[i])
                    slotActions[i].action.performed -= SlotAction_Performed;
        }
    }
}
