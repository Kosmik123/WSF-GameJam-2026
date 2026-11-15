using System;
using UnityEngine;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// A skill as an asset (port of the LD57 SkillDefinition): name, icon and 4 tiers. Every kind of skill is a subclass
    /// that implements <see cref="Execute"/>, so a new skill is a new class and asset, not a new enum value.
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        /// <summary>Index of the last tier (the evolution). Tiers: 0 base, 1 and 2 upgrades, 3 evolution.</summary>
        public const int MaxTier = 3;

        [SerializeField]
        private string displayName = "Skill";
        public string DisplayName => displayName;

        [SerializeField]
        private Sprite icon;
        public Sprite Icon => icon;

        [Tooltip("Color of the skill in the UI: the letter in its slot when it has no icon, the card on level up.")]
        [SerializeField]
        private Color accentColor = new Color(0.93f, 0.9f, 0.84f, 1f);
        public Color AccentColor => accentColor;

        [Header("Animation")]
        [Tooltip("Body animation of the hero when the skill is cast (humanoid clip). Empty = the common cast animation.")]
        [SerializeField]
        private AnimationClip castAnimation;
        public AnimationClip CastAnimation => castAnimation;

        [Tooltip("How long the cast animation lasts, in seconds; the clip is sped up or slowed down to fit. 0 = its own length.")]
        [SerializeField]
        [Min(0f)]
        private float castAnimationDuration = 0.5f;
        public float CastAnimationDuration => castAnimationDuration;

        [Tooltip("Exactly 4 elements: base, upgrade 1, upgrade 2, evolution.")]
        [SerializeField]
        private SkillTier[] tiers = new SkillTier[MaxTier + 1];

        public SkillTier GetTier(int tier)
        {
            return tiers[Mathf.Clamp(tier, 0, MaxTier)];
        }

        /// <summary>Runs the skill. The cooldown is started by <see cref="SkillUser"/>.</summary>
        public abstract void Execute(SkillContext context);

        private void OnValidate()
        {
            if (tiers == null || tiers.Length != MaxTier + 1)
                Array.Resize(ref tiers, MaxTier + 1);
        }
    }
}
