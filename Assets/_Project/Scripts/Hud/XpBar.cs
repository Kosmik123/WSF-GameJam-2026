using TMPro;
using UnityEngine;
using WSFGameJam2026.Progression;

namespace WSFGameJam2026.Hud
{
    /// <summary>XP bar with the level of the player (port of the LD57 XpBarView). The fill is sized by anchors and follows smoothly.</summary>
    public class XpBar : MonoBehaviour
    {
        [SerializeField]
        private Experience experience;

        [Tooltip("Stretches from the left edge of its parent. Its anchors are changed at runtime.")]
        [SerializeField]
        private RectTransform fill;

        [Tooltip("Optional. Shows the level.")]
        [SerializeField]
        private TMP_Text levelLabel;

        [SerializeField]
        [Min(0f)]
        private float fillSharpness = 12f;

        private float fillValue;
        private int shownLevel = -1;

        private void Reset()
        {
            experience = FindAnyObjectByType<Experience>();
        }

        private void Update()
        {
            if (experience == null)
                return;

            // A level up empties the bar at once instead of draining it backwards.
            if (experience.Level != shownLevel)
            {
                shownLevel = experience.Level;
                fillValue = 0f;
                if (levelLabel)
                    levelLabel.text = $"Lv {shownLevel}";
            }

            fillValue = Mathf.Lerp(fillValue, experience.Progress, 1f - Mathf.Exp(-fillSharpness * Time.unscaledDeltaTime));
            if (fill)
                fill.anchorMax = new Vector2(fillValue, fill.anchorMax.y);
        }
    }
}
