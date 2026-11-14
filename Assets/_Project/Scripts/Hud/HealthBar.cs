using TMPro;
using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// Hit point bar of one <see cref="Health"/> (usually the player). The bars are sized by anchors, so no sprites are needed.
    /// The front bar follows the hit points quickly, the trail bar stays behind for a moment and then catches up,
    /// so a loss of health is easy to see. Uses unscaled time, so the hit stop does not freeze the bar.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField]
        private Health health;

        [Header("Bars")]
        [Tooltip("Stretches from the left edge of its parent. Its anchors are changed at runtime.")]
        [SerializeField]
        private RectTransform fill;

        [Tooltip("Optional. The pale bar showing the health that was just lost.")]
        [SerializeField]
        private RectTransform trail;

        [Tooltip("Optional. Shows 'current / max'.")]
        [SerializeField]
        private TMP_Text label;

        [Header("Animation")]
        [Tooltip("How fast the front bar follows the hit points. Higher = tighter.")]
        [SerializeField]
        [Min(0f)]
        private float fillSharpness = 20f;

        [Tooltip("Time after a loss of health in which the trail bar does not move.")]
        [SerializeField]
        [Min(0f)]
        private float trailDelay = 0.4f;

        [Tooltip("Speed of the trail bar, in the part of the whole bar per second.")]
        [SerializeField]
        [Min(0f)]
        private float trailSpeed = 0.6f;

        private float fillValue;
        private float trailValue;
        private float trailHoldUntil;

        private void Reset()
        {
            var healths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (var candidate in healths)
            {
                if (candidate.Team == CombatTeam.Player)
                    health = candidate;
            }
        }

        private void OnEnable()
        {
            if (health == null)
                return;

            health.OnHealthChanged += Health_OnHealthChanged;
            fillValue = health.Normalized;
            trailValue = fillValue;
            Apply();
        }

        private void OnDisable()
        {
            if (health == null)
                return;

            health.OnHealthChanged -= Health_OnHealthChanged;
        }

        /// <summary>Shows the hit points of another body.</summary>
        public void SetHealth(Health newHealth)
        {
            if (isActiveAndEnabled && health != null)
                health.OnHealthChanged -= Health_OnHealthChanged;

            health = newHealth;
            if (isActiveAndEnabled)
                OnEnable();
        }

        private void Health_OnHealthChanged(int change)
        {
            if (change < 0)
                trailHoldUntil = Time.unscaledTime + trailDelay;

            UpdateLabel();
        }

        private void Update()
        {
            if (health == null)
                return;

            var target = health.Normalized;
            var blend = 1f - Mathf.Exp(-fillSharpness * Time.unscaledDeltaTime);
            fillValue = Mathf.Lerp(fillValue, target, blend);

            // Healing pulls the trail up at once. After a loss it waits, then slides down to the front bar.
            if (trailValue < fillValue)
                trailValue = fillValue;
            else if (Time.unscaledTime >= trailHoldUntil)
                trailValue = Mathf.MoveTowards(trailValue, fillValue, trailSpeed * Time.unscaledDeltaTime);

            Apply();
        }

        private void Apply()
        {
            SetBar(fill, fillValue);
            SetBar(trail, trailValue);
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (label == null || health == null)
                return;

            label.text = $"{health.CurrentHealth} / {health.MaxHealth}";
        }

        private static void SetBar(RectTransform bar, float value)
        {
            if (bar == null)
                return;

            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;
        }
    }
}
