using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Hud
{
    /// <summary>One slot of the skill bar: icon or name, key, tier and a dark overlay showing the cooldown.</summary>
    public class SkillSlotView : MonoBehaviour
    {
        private static readonly string[] TierLabels = { "", "I", "II", "III" };

        [SerializeField]
        private Image icon;

        [Tooltip("Image of type Filled, covering the slot while the skill is on cooldown.")]
        [SerializeField]
        private Image cooldownFill;

        [Tooltip("Name of the skill, shown when it has no icon.")]
        [SerializeField]
        private TMP_Text nameLabel;

        [SerializeField]
        private TMP_Text keyLabel;

        [SerializeField]
        private TMP_Text tierLabel;

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

            SetText(nameLabel, hasSkill && sprite == null ? skill.Definition.DisplayName : string.Empty);
            SetText(keyLabel, key);
            SetText(tierLabel, hasSkill ? TierLabels[Mathf.Clamp(skill.Tier, 0, TierLabels.Length - 1)] : string.Empty);

            if (cooldownFill)
                cooldownFill.fillAmount = hasSkill ? skill.CooldownNormalized : 0f;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label && label.text != text)
                label.text = text;
        }
    }
}
