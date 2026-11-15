using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The screen after a won round: lap time, enemies killed, the closest the jaw got, what changes in the next round
    /// and a button that counts down to it (pressing it starts the next round at once).
    /// </summary>
    public class RoundWonScreen : MonoBehaviour
    {
        [SerializeField]
        private RoundController controller;

        [SerializeField]
        private RunStats stats;

        [SerializeField]
        private CanvasGroup screen;

        [SerializeField]
        private TMP_Text titleLabel;

        [Tooltip("{0} is replaced with the number of the won round.")]
        [SerializeField]
        private string titleFormat = "Runda {0} ukończona";

        [Header("Numbers")]
        [SerializeField]
        private TMP_Text lapTimeLabel;

        [SerializeField]
        private TMP_Text killsLabel;

        [SerializeField]
        private TMP_Text closestJawLabel;

        [Tooltip("{0} is replaced with the closest lead over the jaw in metres.")]
        [SerializeField]
        private string closestJawFormat = "{0:0.0} m";

        [Header("Next round")]
        [SerializeField]
        private TMP_Text nextRoundLabel;

        [Tooltip("Rich text. {0}: next round, {1}: how much faster the jaw is in percent, {2}: enemy multiplier.")]
        [SerializeField]
        [TextArea]
        private string nextRoundFormat = "Runda {0}:      paszcza <color=#FF8A7F><b>+{1:0}% szybciej</b></color>      wrogowie <color=#FF8A7F><b>×{2:0.0#}</b></color>      poziom i skille <color=#36B3A0><b>zostają</b></color>";

        [SerializeField]
        private Button continueButton;

        [SerializeField]
        private TMP_Text continueLabel;

        [Tooltip("{0}: next round, {1}: seconds left.")]
        [SerializeField]
        private string continueFormat = "DALEJ · RUNDA {0} ZA {1} s";

        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.35f;

        private int nextRound;

        private void Reset()
        {
            controller = FindAnyObjectByType<RoundController>();
            stats = FindAnyObjectByType<RunStats>();
        }

        private void OnEnable()
        {
            if (screen)
                screen.gameObject.SetActive(false);

            if (continueButton)
                continueButton.onClick.AddListener(ContinueButton_OnClick);

            if (controller == null)
                return;

            controller.OnRoundCompleted += RoundController_OnRoundCompleted;
            controller.OnRoundStarted += RoundController_OnRoundStarted;
            controller.OnRunEnded += RoundController_OnRunEnded;
        }

        private void RoundController_OnRoundCompleted(int round)
        {
            nextRound = round + 1;
            if (titleLabel)
                titleLabel.text = string.Format(titleFormat, round);

            ShowNumbers();
            if (nextRoundLabel)
            {
                float jawFaster = controller.GetJawSpeedMultiplier(nextRound) / controller.GetJawSpeedMultiplier(round) - 1f;
                nextRoundLabel.text = string.Format(nextRoundFormat, nextRound, jawFaster * 100f, controller.GetEnemyMultiplier(nextRound));
            }

            if (screen == null)
                return;

            screen.gameObject.SetActive(true);
            screen.alpha = 0f;
            StopAllCoroutines();
            StartCoroutine(UiFade.FadingCo(screen, 1f, fadeSeconds));
            if (continueButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        private void ShowNumbers()
        {
            if (stats == null)
                return;

            if (lapTimeLabel)
            {
                int seconds = Mathf.FloorToInt(stats.LapSeconds);
                lapTimeLabel.text = $"{seconds / 60}:{seconds % 60:00}";
            }

            if (killsLabel)
                killsLabel.text = stats.RoundKills.ToString();

            if (closestJawLabel)
                closestJawLabel.text = string.Format(closestJawFormat, stats.ClosestJawMeters);
        }

        private void Update()
        {
            if (continueLabel && screen && screen.gameObject.activeSelf)
                continueLabel.text = string.Format(continueFormat, nextRound, Mathf.CeilToInt(controller.BreakTimeLeft));
        }

        private void RoundController_OnRoundStarted(int round)
        {
            Hide();
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            Hide();
        }

        private void ContinueButton_OnClick()
        {
            controller.ContinueNow();
        }

        private void Hide()
        {
            StopAllCoroutines();
            if (screen)
                screen.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (continueButton)
                continueButton.onClick.RemoveListener(ContinueButton_OnClick);

            if (controller == null)
                return;

            controller.OnRoundCompleted -= RoundController_OnRoundCompleted;
            controller.OnRoundStarted -= RoundController_OnRoundStarted;
            controller.OnRunEnded -= RoundController_OnRunEnded;
        }
    }
}
