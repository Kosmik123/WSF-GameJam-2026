using System;
using UnityEngine;

namespace WSFGameJam2026.Progression
{
    /// <summary>
    /// Experience and level of the player (port of the LD57 Experience). XP comes from <see cref="ExperienceOrb"/>s dropped
    /// by enemies. Each level needs more XP (LD57 curve: base + step × level). A death ends the run and
    /// <see cref="ResetProgress"/> takes the level back to 1 (GDD 9).
    /// </summary>
    public class Experience : MonoBehaviour
    {
        public delegate void ExperienceChangedHandler(int currentXp, int xpToNextLevel);

        /// <summary>XP was gained or reset. For the XP bar.</summary>
        public event ExperienceChangedHandler OnExperienceChanged;

        /// <summary>A new level was reached. The argument is the new level. Raised once per level, also when several come at once.</summary>
        public event Action<int> OnLevelUp;

        /// <summary>The level went back to 1 (new run).</summary>
        public event Action OnProgressReset;

        [Tooltip("XP needed for level 2.")]
        [SerializeField]
        [Min(1)]
        private int baseXpToLevel = 40;

        [Tooltip("Extra XP needed for every next level.")]
        [SerializeField]
        [Min(0)]
        private int xpPerLevel = 13;

        [Header("States")]
        [SerializeField]
        private int level = 1;
        public int Level => level;

        [SerializeField]
        private int currentXp;
        public int CurrentXp => currentXp;

        public int XpToNextLevel => baseXpToLevel + xpPerLevel * (level - 1);
        public float Progress => Mathf.Clamp01((float)currentXp / XpToNextLevel);

        public void AddXp(int amount)
        {
            if (amount <= 0)
                return;

            currentXp += amount;
            while (currentXp >= XpToNextLevel)
            {
                currentXp -= XpToNextLevel;
                level++;
                OnLevelUp?.Invoke(level);
            }

            OnExperienceChanged?.Invoke(currentXp, XpToNextLevel);
        }

        public void ResetProgress()
        {
            level = 1;
            currentXp = 0;
            OnProgressReset?.Invoke();
            OnExperienceChanged?.Invoke(currentXp, XpToNextLevel);
        }
    }
}
