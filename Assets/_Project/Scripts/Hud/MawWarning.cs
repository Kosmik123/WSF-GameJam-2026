using UnityEngine;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// Red, pulsing edges of the screen and a warning when the jaw is close behind the player (GDD 10).
    /// The strength follows the lead of the player over the jaw. Hidden between laps.
    /// </summary>
    public class MawWarning : MonoBehaviour
    {
        [SerializeField]
        private LapTracker lapTracker;

        [Tooltip("The vignette and the warning text. Its alpha is the strength of the warning.")]
        [SerializeField]
        private CanvasGroup group;

        [Tooltip("Below this lead (metres) the warning starts to show.")]
        [SerializeField]
        [Min(0f)]
        private float warnMeters = 12f;

        [Tooltip("At this lead (metres) the warning is at full strength.")]
        [SerializeField]
        [Min(0f)]
        private float fullMeters = 3f;

        [Tooltip("Pulses per second at full strength.")]
        [SerializeField]
        [Min(0f)]
        private float pulseFrequency = 2.5f;

        [Tooltip("How much the pulse dims the warning (0 = no pulse).")]
        [SerializeField]
        [Range(0f, 1f)]
        private float pulseDepth = 0.35f;

        [Tooltip("How fast the warning follows the distance. Higher = sharper.")]
        [SerializeField]
        [Min(0f)]
        private float sharpness = 6f;

        private float strength;

        private void Reset()
        {
            lapTracker = FindAnyObjectByType<LapTracker>();
            group = GetComponentInChildren<CanvasGroup>();
        }

        private void OnEnable()
        {
            strength = 0f;
            if (group)
                group.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (lapTracker == null || group == null)
                return;

            float target = lapTracker.IsRunning ? Mathf.InverseLerp(warnMeters, fullMeters, lapTracker.MetersAheadOfHead) : 0f;
            strength = Mathf.Lerp(strength, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));

            float pulse = 1f - pulseDepth * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * pulseFrequency * 2f * Mathf.PI));
            group.alpha = strength * pulse;
        }
    }
}
