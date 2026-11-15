using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// The arrow under the player pointing back at the jaw, with the distance: "paszcza · 14 m" (GDD 10).
    /// It turns red when the jaw is close and hides between laps.
    /// </summary>
    public class MawDistanceLabel : MonoBehaviour
    {
        [SerializeField]
        private LapTracker lapTracker;

        [SerializeField]
        private TMP_Text label;

        [Tooltip("Optional. The arrow; tinted like the text.")]
        [SerializeField]
        private Graphic arrow;

        [Tooltip("Optional. Hidden between laps.")]
        [SerializeField]
        private CanvasGroup group;

        [Tooltip("{0} is replaced with the lead over the jaw in metres.")]
        [SerializeField]
        private string format = "paszcza · {0:0} m";

        [SerializeField]
        private Color safeColor = new Color(0.93f, 0.9f, 0.84f, 1f);

        [SerializeField]
        private Color dangerColor = new Color(1f, 0.42f, 0.37f, 1f);

        [Tooltip("Below this lead (metres) the label is red.")]
        [SerializeField]
        [Min(0f)]
        private float dangerMeters = 8f;

        private void Reset()
        {
            lapTracker = FindAnyObjectByType<LapTracker>();
            label = GetComponentInChildren<TMP_Text>();
            group = GetComponent<CanvasGroup>();
        }

        private void LateUpdate()
        {
            if (lapTracker == null)
                return;

            if (group)
                group.alpha = Mathf.MoveTowards(group.alpha, lapTracker.IsRunning ? 1f : 0f, 4f * Time.unscaledDeltaTime);

            float meters = Mathf.Max(0f, lapTracker.MetersAheadOfHead);
            var color = meters < dangerMeters ? dangerColor : safeColor;
            if (label)
            {
                label.text = string.Format(format, meters);
                label.color = color;
            }

            if (arrow)
                arrow.color = color;
        }
    }
}
