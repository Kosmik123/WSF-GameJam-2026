using System.Collections;
using TMPro;
using UnityEngine;
using WSFGameJam2026.Menus;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// A large "Round N" banner in the middle of the screen at the start of every round. It fades in, stays for a moment
    /// and fades out, in unscaled time. Skipped while the game is paused (the first round starts under the main menu,
    /// which shows the banner itself after the intro).
    /// </summary>
    public class RoundBanner : MonoBehaviour
    {
        [SerializeField]
        private RoundController controller;

        [SerializeField]
        private RoundState roundState;

        [Tooltip("Optional. While the game is paused the banner is not shown.")]
        [SerializeField]
        private GamePause pause;

        [SerializeField]
        private CanvasGroup group;

        [SerializeField]
        private TMP_Text roundLabel;

        [Header("Animation")]
        [SerializeField]
        [Min(0f)]
        private float fadeInSeconds = 0.3f;

        [SerializeField]
        [Min(0f)]
        private float holdSeconds = 1.4f;

        [SerializeField]
        [Min(0f)]
        private float fadeOutSeconds = 0.6f;

        private void Reset()
        {
            controller = FindAnyObjectByType<RoundController>();
            roundState = FindAnyObjectByType<RoundState>();
            pause = FindAnyObjectByType<GamePause>();
            group = GetComponentInChildren<CanvasGroup>();
            roundLabel = GetComponentInChildren<TMP_Text>();
        }

        private void OnEnable()
        {
            if (group)
                group.alpha = 0f;

            if (controller)
                controller.OnRoundStarted += RoundController_OnRoundStarted;
        }

        /// <summary>Shows the banner with the number of the current round.</summary>
        public void ShowCurrentRound()
        {
            if (roundState)
                Show(roundState.Round);
        }

        private void RoundController_OnRoundStarted(int round)
        {
            if (pause && pause.IsPaused)
                return;

            Show(round);
        }

        private void Show(int round)
        {
            if (roundLabel)
                roundLabel.text = round.ToString();

            StopAllCoroutines();
            StartCoroutine(BannerShowingCo());
        }

        private IEnumerator BannerShowingCo()
        {
            yield return UiFade.FadingCo(group, 1f, fadeInSeconds);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return UiFade.FadingCo(group, 0f, fadeOutSeconds);
        }

        private void OnDisable()
        {
            if (controller)
                controller.OnRoundStarted -= RoundController_OnRoundStarted;
        }
    }
}
