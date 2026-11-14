using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// The lap on a ring (the snake biting its tail): the top of the ring is the head, the fill grows clockwise
    /// from the jaw to the player, and the goal is at the end of the ring, right behind the head.
    /// Also shows the lap in percent and the lead over the jaw in metres. The fill turns red when the jaw is close.
    /// </summary>
    public class LapRing : MonoBehaviour
    {
        [SerializeField]
        private LapTracker lapTracker;

        [Tooltip("Image of type Filled (Radial 360, origin Top, clockwise): the lead of the player over the jaw.")]
        [SerializeField]
        private Image progressFill;

        [Tooltip("Optional. Rotated around the centre of the ring, so its child marker sits at the player.")]
        [SerializeField]
        private RectTransform playerMarkerPivot;

        [SerializeField]
        private TMP_Text progressLabel;

        [Tooltip("{0} is replaced with the lap progress in percent.")]
        [SerializeField]
        private string progressFormat = "{0:0}%";

        [SerializeField]
        private TMP_Text distanceLabel;

        [Tooltip("{0} is replaced with the lead over the jaw in metres.")]
        [SerializeField]
        private string distanceFormat = "{0:0} m";

        [Header("Colors")]
        [SerializeField]
        private Color safeColor = new Color(0.21f, 0.70f, 0.63f, 1f);

        [SerializeField]
        private Color dangerColor = new Color(0.88f, 0.28f, 0.24f, 1f);

        [Tooltip("Below this lead (metres) the ring starts to turn red.")]
        [SerializeField]
        [Min(0f)]
        private float warnMeters = 15f;

        [Tooltip("At this lead (metres) the ring is fully red.")]
        [SerializeField]
        [Min(0f)]
        private float dangerMeters = 4f;

        private void Reset()
        {
            lapTracker = FindAnyObjectByType<LapTracker>();
        }

        private void LateUpdate()
        {
            if (lapTracker == null)
                return;

            float progress = Mathf.Clamp01(lapTracker.Progress);
            float meters = Mathf.Max(0f, lapTracker.MetersAheadOfHead);
            float danger = Mathf.InverseLerp(warnMeters, dangerMeters, meters);
            var color = Color.Lerp(safeColor, dangerColor, danger);

            if (progressFill)
            {
                progressFill.fillAmount = progress;
                progressFill.color = color;
            }

            if (playerMarkerPivot)
                playerMarkerPivot.localRotation = Quaternion.Euler(0f, 0f, -360f * progress);

            if (progressLabel)
                progressLabel.text = string.Format(progressFormat, progress * 100f);

            if (distanceLabel)
            {
                distanceLabel.text = string.Format(distanceFormat, meters);
                distanceLabel.color = Color.Lerp(Color.white, dangerColor, danger);
            }
        }
    }
}
