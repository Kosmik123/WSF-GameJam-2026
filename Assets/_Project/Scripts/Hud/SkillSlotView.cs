using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// One slot of the skill bar: the icon (or the first letter of the name in the color of the skill), the key,
    /// a dark overlay with the seconds left of the cooldown, the tier as a row of pips and the name under the slot.
    /// Every part is optional.
    /// </summary>
    public class SkillSlotView : MonoBehaviour
    {
        private static readonly string[] TierLabels = { "", "I", "II", "III" };

        [SerializeField]
        private Image icon;

        [Tooltip("Image of type Filled, covering the slot while the skill is on cooldown.")]
        [SerializeField]
        private Image cooldownFill;

        [Tooltip("Optional. Seconds left of the cooldown.")]
        [SerializeField]
        private TMP_Text cooldownLabel;

        [Tooltip("Name of the skill, shown in the slot when it has no icon.")]
        [SerializeField]
        private TMP_Text nameLabel;

        [Tooltip("Optional. The first letter of the name in the accent color of the skill, shown when it has no icon.")]
        [SerializeField]
        private TMP_Text glyphLabel;

        [Tooltip("Optional. The name of the skill under the slot.")]
        [SerializeField]
        private TMP_Text captionLabel;

        [SerializeField]
        private TMP_Text keyLabel;

        [SerializeField]
        private TMP_Text tierLabel;

        [Header("Tier pips")]
        [Tooltip("Optional. One pip per tier (base, two upgrades, evolution).")]
        [SerializeField]
        private Image[] tierPips = new Image[0];

        [SerializeField]
        private Color ownedPipColor = new Color(0.93f, 0.9f, 0.84f, 1f);

        [SerializeField]
        private Color evolutionPipColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        [SerializeField]
        private Color emptyPipColor = new Color(0.23f, 0.19f, 0.16f, 1f);

        [Header("Frame")]
        [Tooltip("Optional. Turns gold when the skill reached its evolution.")]
        [SerializeField]
        private Graphic frame;

        [SerializeField]
        private Color frameColor = new Color(0.23f, 0.19f, 0.16f, 1f);

        [SerializeField]
        private Color evolvedFrameColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        /// <summary>Shows the skill, or an empty slot when it is null.</summary>
        public void Show(SkillInstance skill, string key)
        {
            bool hasSkill = skill != null;
            var sprite = hasSkill ? skill.Definition.Icon : null;

            if (icon)
            {
                icon.enabled = sprite != null;
                icon.sprite = sprite;
            }

            string skillName = hasSkill ? skill.Definition.DisplayName : string.Empty;
            SetText(nameLabel, hasSkill && sprite == null && glyphLabel == null ? skillName : string.Empty);
            SetText(glyphLabel, hasSkill && sprite == null && skillName.Length > 0 ? skillName.Substring(0, 1).ToUpperInvariant() : string.Empty);
            if (glyphLabel && hasSkill)
                glyphLabel.color = skill.Definition.AccentColor;

            SetText(captionLabel, skillName);
            SetText(keyLabel, key);
            SetText(tierLabel, hasSkill ? TierLabels[Mathf.Clamp(skill.Tier, 0, TierLabels.Length - 1)] : string.Empty);

            float cooldown = hasSkill ? skill.CooldownNormalized : 0f;
            if (cooldownFill)
                cooldownFill.fillAmount = cooldown;

            if (cooldownLabel)
                SetText(cooldownLabel, cooldown > 0f ? skill.CooldownRemaining.ToString("0.0") : string.Empty);

            int tier = hasSkill ? skill.Tier : -1;
            for (int i = 0; i < tierPips.Length; i++)
                if (tierPips[i])
                    tierPips[i].color = GetPipColor(i, tier);

            if (frame)
                frame.color = hasSkill && skill.IsMaxTier ? evolvedFrameColor : frameColor;
        }

        private Color GetPipColor(int pip, int tier)
        {
            if (pip > tier)
                return emptyPipColor;

            return pip == SkillDefinition.MaxTier ? evolutionPipColor : ownedPipColor;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label && label.text != text)
                label.text = text;
        }
    }
}
