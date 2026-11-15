using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The death screen (GDD 6): what killed the player, rounds survived, the record (with a badge when it is new),
    /// the level, the enemies killed and the class of the run, and buttons to start again or go to the main menu.
    /// </summary>
    public class RunEndScreen : MonoBehaviour
    {
        [SerializeField]
        private RoundController controller;

        [SerializeField]
        private RunStats stats;

        [SerializeField]
        private PlayerLevel playerLevel;

        [SerializeField]
        private PlayerClassApplier playerClass;

        [SerializeField]
        private CanvasGroup screen;

        [Header("Texts")]
        [SerializeField]
        private TMP_Text causeLabel;

        [SerializeField]
        private string killedByJawText = "DOPADŁA CIĘ PASZCZA";

        [SerializeField]
        private string killedInFightText = "ŚMIERĆ W WALCE";

        [SerializeField]
        private TMP_Text roundsLabel;

        [SerializeField]
        private TMP_Text bestLabel;

        [Tooltip("Shown only when this run set a new record.")]
        [SerializeField]
        private GameObject newRecordBadge;

        [Tooltip("Optional. Gold when this run set a new record.")]
        [SerializeField]
        private Graphic bestFrame;

        [SerializeField]
        private Color bestFrameColor = new Color(0.93f, 0.9f, 0.84f, 0.16f);

        [SerializeField]
        private Color newRecordFrameColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        [SerializeField]
        private TMP_Text summaryLabel;

        [Tooltip("Rich text. {0}: level, {1}: enemies killed in the run, {2}: class.")]
        [SerializeField]
        [TextArea]
        private string summaryFormat = "Poziom <color=#EDE6D6><b>{0}</b></color>          Wrogowie <color=#EDE6D6><b>{1}</b></color>          Klasa <color=#EDE6D6><b>{2}</b></color>";

        [Header("Buttons")]
        [SerializeField]
        private Button restartButton;

        [SerializeField]
        private Button mainMenuButton;

        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.5f;

        private void Reset()
        {
            controller = FindAnyObjectByType<RoundController>();
            stats = FindAnyObjectByType<RunStats>();
            playerLevel = FindAnyObjectByType<PlayerLevel>();
            playerClass = FindAnyObjectByType<PlayerClassApplier>();
        }

        private void OnEnable()
        {
            if (screen)
                screen.gameObject.SetActive(false);

            if (restartButton)
                restartButton.onClick.AddListener(RestartButton_OnClick);

            if (mainMenuButton)
                mainMenuButton.onClick.AddListener(MainMenuButton_OnClick);

            if (controller == null)
                return;

            controller.OnRunEnded += RoundController_OnRunEnded;
            controller.OnRoundStarted += RoundController_OnRoundStarted;
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            if (causeLabel)
                causeLabel.text = stats && stats.WasKilledByJaw == false ? killedInFightText : killedByJawText;

            if (roundsLabel)
                roundsLabel.text = completedRounds.ToString();

            if (bestLabel)
                bestLabel.text = bestRounds.ToString();

            if (newRecordBadge)
                newRecordBadge.SetActive(isNewBest);

            if (bestFrame)
                bestFrame.color = isNewBest ? newRecordFrameColor : bestFrameColor;

            if (summaryLabel)
            {
                int level = playerLevel ? playerLevel.Level : 1;
                int kills = stats ? stats.RunKills : 0;
                string className = playerClass && playerClass.Current ? playerClass.Current.DisplayName : "-";
                summaryLabel.text = string.Format(summaryFormat, level, kills, className);
            }

            if (screen == null)
                return;

            screen.gameObject.SetActive(true);
            screen.alpha = 0f;
            StopAllCoroutines();
            StartCoroutine(UiFade.FadingCo(screen, 1f, fadeSeconds));
            if (restartButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
        }

        private void RoundController_OnRoundStarted(int round)
        {
            StopAllCoroutines();
            if (screen)
                screen.gameObject.SetActive(false);
        }

        private void RestartButton_OnClick()
        {
            controller.RestartRun();
        }

        private void MainMenuButton_OnClick()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDisable()
        {
            if (restartButton)
                restartButton.onClick.RemoveListener(RestartButton_OnClick);

            if (mainMenuButton)
                mainMenuButton.onClick.RemoveListener(MainMenuButton_OnClick);

            if (controller == null)
                return;

            controller.OnRunEnded -= RoundController_OnRunEnded;
            controller.OnRoundStarted -= RoundController_OnRoundStarted;
        }
    }
}
