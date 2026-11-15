using TMPro;
using UnityEngine;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// A list of the skills of the player with their keys and tiers ("Baza", "Ulepszenie I", ..., "Ewolucja"),
    /// for the pause menu. Refreshed every time it is shown.
    /// </summary>
    public class SkillListView : MonoBehaviour
    {
        [SerializeField]
        private SkillUser skillUser;

        [Tooltip("One row per slot. Rows of empty slots are hidden.")]
        [SerializeField]
        private GameObject[] rows = new GameObject[0];

        [SerializeField]
        private TMP_Text[] keyLabels = new TMP_Text[0];

        [SerializeField]
        private TMP_Text[] nameLabels = new TMP_Text[0];

        [SerializeField]
        private TMP_Text[] tierLabels = new TMP_Text[0];

        [Tooltip("Names of the tiers: base, two upgrades, evolution.")]
        [SerializeField]
        private string[] tierNames = { "Baza", "Ulepszenie I", "Ulepszenie II", "Ewolucja" };

        [SerializeField]
        private Color tierColor = new Color(0.66f, 0.64f, 0.58f, 1f);

        [SerializeField]
        private Color evolutionColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        private void Reset()
        {
            skillUser = FindAnyObjectByType<SkillUser>();
        }

        private void OnEnable()
        {
            if (skillUser == null)
                return;

            var owned = skillUser.Slots.Slots;
            for (int i = 0; i < rows.Length; i++)
            {
                bool hasSkill = i < owned.Count;
                if (rows[i])
                    rows[i].SetActive(hasSkill);

                if (hasSkill == false)
                    continue;

                var skill = owned[i];
                SetText(keyLabels, i, skillUser.GetSlotKeyLabel(i), null);
                SetText(nameLabels, i, skill.Definition.DisplayName, null);
                SetText(tierLabels, i, tierNames[Mathf.Clamp(skill.Tier, 0, tierNames.Length - 1)], skill.IsMaxTier ? evolutionColor : tierColor);
            }
        }

        private static void SetText(TMP_Text[] labels, int index, string text, Color? color)
        {
            if (index >= labels.Length || labels[index] == null)
                return;

            labels[index].text = text;
            if (color.HasValue)
                labels[index].color = color.Value;
        }
    }
}
