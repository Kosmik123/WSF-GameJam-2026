using TMPro;
using UnityEngine;
using WSFGameJam2026.Progression;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// Shows the level of the player and the experience bar. Every part is optional, so the same view serves
    /// the HUD badge with its bar and the level number in the pause menu. The bar slides in unscaled time.
    /// </summary>
    public class PlayerLevelView : MonoBehaviour
    {
        [SerializeField]
        private PlayerLevel playerLevel;

        [SerializeField]
        private TMP_Text levelLabel;

        [Tooltip("Optional. Stretches from the left edge of its parent; its anchors are changed at runtime.")]
        [SerializeField]
        private RectTransform experienceFill;

        [SerializeField]
        private TMP_Text experienceLabel;

        [Tooltip("{0} is the experience, {1} the experience needed for the next level.")]
        [SerializeField]
        private string experienceFormat = "XP {0} / {1}";

        [Tooltip("How fast the bar follows the experience. Higher = tighter.")]
        [SerializeField]
        [Min(0f)]
        private float fillSharpness = 10f;

        private float fillValue;

        private void Reset()
        {
            playerLevel = FindAnyObjectByType<PlayerLevel>();
        }

        private void OnEnable()
        {
            if (playerLevel == null)
                return;

            playerLevel.OnExperienceChanged += PlayerLevel_OnExperienceChanged;
            playerLevel.OnLevelUp += PlayerLevel_OnLevelUp;
            fillValue = playerLevel.Normalized;
            Refresh();
        }

        private void PlayerLevel_OnExperienceChanged(int experience, int experienceToNextLevel)
        {
            Refresh();
        }

        private void PlayerLevel_OnLevelUp(int level)
        {
            // The bar starts again from the left for the new level.
            fillValue = 0f;
            Refresh();
        }

        private void Update()
        {
            if (playerLevel == null || experienceFill == null)
                return;

            fillValue = Mathf.Lerp(fillValue, playerLevel.Normalized, 1f - Mathf.Exp(-fillSharpness * Time.unscaledDeltaTime));
            experienceFill.anchorMin = Vector2.zero;
            experienceFill.anchorMax = new Vector2(fillValue, 1f);
            experienceFill.offsetMin = Vector2.zero;
            experienceFill.offsetMax = Vector2.zero;
        }

        private void Refresh()
        {
            if (levelLabel)
                levelLabel.text = playerLevel.Level.ToString();

            if (experienceLabel)
                experienceLabel.text = string.Format(experienceFormat, playerLevel.Experience, playerLevel.ExperienceToNextLevel);
        }

        private void OnDisable()
        {
            if (playerLevel == null)
                return;

            playerLevel.OnExperienceChanged -= PlayerLevel_OnExperienceChanged;
            playerLevel.OnLevelUp -= PlayerLevel_OnLevelUp;
        }
    }
}
