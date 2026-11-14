using System;
using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Skills owned by the player (port of the LD57 SkillSlots): a few slots filled in the order the skills were gained,
    /// the pool that level-up offers are drawn from, and the skills the player starts with.
    /// The pool and the starting skills come from the player class (<see cref="Player.PlayerClassApplier"/>).
    /// </summary>
    public class SkillSlots : MonoBehaviour
    {
        /// <summary>A skill was gained or got a higher tier.</summary>
        public event Action<SkillInstance> OnSkillChanged;

        /// <summary>All skills were removed and the starting skills given again (start of a run, class change).</summary>
        public event Action OnSkillsReset;

        [Tooltip("Number of slots. SkillUser needs one input action per slot.")]
        [SerializeField]
        [Min(1)]
        private int slotCount = 4;
        public int SlotCount => slotCount;

        [Tooltip("All skills that level-up offers can give.")]
        [SerializeField]
        private SkillDefinition[] pool = new SkillDefinition[0];
        public IReadOnlyList<SkillDefinition> Pool => pool;

        [Tooltip("Skills owned from the start of every run, in slot order.")]
        [SerializeField]
        private SkillDefinition[] startingSkills = new SkillDefinition[0];

        private readonly List<SkillInstance> slots = new List<SkillInstance>();

        /// <summary>Owned skills in slot order. Size from 0 to <see cref="SlotCount"/>.</summary>
        public IReadOnlyList<SkillInstance> Slots => slots;

        public bool HasFreeSlot => slots.Count < slotCount;

        private void Awake()
        {
            ResetSkills();
        }

        /// <summary>Replaces the pool and the starting skills (class choice) and resets the skills.</summary>
        public void SetPool(IReadOnlyList<SkillDefinition> newPool, IReadOnlyList<SkillDefinition> newStartingSkills)
        {
            pool = ToArray(newPool);
            startingSkills = ToArray(newStartingSkills);
            ResetSkills();
        }

        /// <summary>Removes all skills and gives the starting ones again. Called when a new run starts (death resets progress).</summary>
        public void ResetSkills()
        {
            slots.Clear();
            for (int i = 0; i < startingSkills.Length; i++)
                if (startingSkills[i])
                    Grant(startingSkills[i]);

            OnSkillsReset?.Invoke();
        }

        public SkillInstance Find(SkillDefinition definition)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Definition == definition)
                    return slots[i];

            return null;
        }

        /// <summary>-1 when the skill is not owned, otherwise its tier (0-3).</summary>
        public int TierOf(SkillDefinition definition)
        {
            var instance = Find(definition);
            return instance != null ? instance.Tier : -1;
        }

        /// <summary>
        /// Gives a skill: a new one takes the first free slot, an owned one gets a higher tier.
        /// Returns the instance, or null when it can not be given (no free slot, last tier).
        /// </summary>
        public SkillInstance Grant(SkillDefinition definition)
        {
            if (definition == null)
                return null;

            var existing = Find(definition);
            if (existing != null)
                return Upgrade(existing);

            if (HasFreeSlot == false)
                return null;

            var instance = new SkillInstance(definition);
            slots.Add(instance);
            OnSkillChanged?.Invoke(instance);
            return instance;
        }

        /// <summary>
        /// Adds to <paramref name="results"/> the skills that can be offered now (for a level-up screen):
        /// skills not owned yet when there is a free slot, and owned ones below the last tier.
        /// </summary>
        public void GetOfferableSkills(List<SkillDefinition> results)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                var definition = pool[i];
                if (definition == null)
                    continue;

                var owned = Find(definition);
                bool canOffer = owned != null ? owned.IsMaxTier == false : HasFreeSlot;
                if (canOffer)
                    results.Add(definition);
            }
        }

        private SkillInstance Upgrade(SkillInstance instance)
        {
            if (instance.Upgrade() == false)
                return null;

            OnSkillChanged?.Invoke(instance);
            return instance;
        }

        private static SkillDefinition[] ToArray(IReadOnlyList<SkillDefinition> list)
        {
            if (list == null)
                return new SkillDefinition[0];

            var array = new SkillDefinition[list.Count];
            for (int i = 0; i < list.Count; i++)
                array[i] = list[i];

            return array;
        }
    }
}
