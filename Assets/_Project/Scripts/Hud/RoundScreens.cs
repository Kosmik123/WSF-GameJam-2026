using System.Collections;
using TMPro;
using UnityEngine;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// The screens of the round loop: "round complete" after a lap and the death screen with the result and the record.
    /// They listen to <see cref="RoundController"/> and fade in with unscaled time. Keep this component on an object that
    /// is always active (not on a screen), because it also switches the screens on and off.
    /// </summary>
    public class RoundScreens : MonoBehaviour
    {
        [SerializeField]
        private RoundController controller;

        [Header("Round complete")]
        [SerializeField]
        private CanvasGroup roundCompleteScreen;

        [SerializeField]
        private TMP_Text roundCompleteTitle;

        [Tooltip("{0} is replaced with the number of the round that was won.")]
        [SerializeField]
        private string roundCompleteFormat = "Round {0} complete";

        [Header("Death")]
        [SerializeField]
        private CanvasGroup deathScreen;

        [SerializeField]
        private TMP_Text deathResult;

        [Tooltip("{0} is replaced with the number of rounds completed in this run.")]
        [SerializeField]
        private string deathResultFormat = "Rounds completed: {0}";

        [SerializeField]
        private TMP_Text deathBest;

        [Tooltip("{0} is replaced with the best result.")]
        [SerializeField]
        private string deathBestFormat = "Best: {0}";

        [Tooltip("Shown only when this run set a new record.")]
        [SerializeField]
        private GameObject newRecordLabel;

        [Header("Animation")]
        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.35f;

        private void Reset()
        {
            controller = FindAnyObjectByType<RoundController>();
        }

        private void OnEnable()
        {
            HideAll();
            if (controller == null)
                return;

            controller.OnRoundCompleted += RoundController_OnRoundCompleted;
            controller.OnRoundStarted += RoundController_OnRoundStarted;
            controller.OnRunEnded += RoundController_OnRunEnded;
        }

        private void OnDisable()
        {
            if (controller == null)
                return;

            controller.OnRoundCompleted -= RoundController_OnRoundCompleted;
            controller.OnRoundStarted -= RoundController_OnRoundStarted;
            controller.OnRunEnded -= RoundController_OnRunEnded;
        }

        private void RoundController_OnRoundCompleted(int round)
        {
            if (roundCompleteTitle != null)
                roundCompleteTitle.text = string.Format(roundCompleteFormat, round);

            Show(roundCompleteScreen);
        }

        private void RoundController_OnRoundStarted(int round)
        {
            HideAll();
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            if (deathResult != null)
                deathResult.text = string.Format(deathResultFormat, completedRounds);

            if (deathBest != null)
                deathBest.text = string.Format(deathBestFormat, bestRounds);

            if (newRecordLabel != null)
                newRecordLabel.SetActive(isNewBest);

            Show(deathScreen);
        }

        private void HideAll()
        {
            StopAllCoroutines();
            Hide(roundCompleteScreen);
            Hide(deathScreen);
        }

        private static void Hide(CanvasGroup screen)
        {
            if (screen != null)
                screen.gameObject.SetActive(false);
        }

        private void Show(CanvasGroup screen)
        {
            if (screen == null)
                return;

            screen.gameObject.SetActive(true);
            StartCoroutine(FadeInCo(screen));
        }

        private IEnumerator FadeInCo(CanvasGroup screen)
        {
            var elapsed = 0f;
            screen.alpha = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                screen.alpha = Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }

            screen.alpha = 1f;
        }
    }
}
