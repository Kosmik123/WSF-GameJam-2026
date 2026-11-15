using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WSFGameJam2026.Menu;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Leaderboard
{
    /// <summary>
    /// The leaderboard window: the top places downloaded from the server (or the cached ones when it can not be reached,
    /// or nothing), and, when the <see cref="ScoreCollector"/> holds a score that was not sent yet, a field for the name
    /// of the player with a confirm button that sends the score.
    /// The first confirmed name becomes the account of the player: it is remembered, the field is replaced by "playing as" and every
    /// next score goes to the same account. The reset button signs the player out and forgets the name, so the next score starts a new account.
    /// Opened by the menu button, or from code with <see cref="Open"/>. After a death it opens by itself over the death screen;
    /// closing it then returns to the main menu (the scene is loaded again), so a new run never starts without the menu.
    /// </summary>
    public class LeaderboardScreen : MonoBehaviour
    {
        private const string NameKey = "WSFGameJam2026.Leaderboard.Name";
        private const int MaxNameLength = 16;

        [SerializeField]
        private LeaderboardService service;

        [SerializeField]
        private ScoreCollector scoreCollector;

        [Header("Opening")]
        [Tooltip("Button of the menu that opens the window.")]
        [SerializeField]
        private Button openButton;

        [Tooltip("Optional. Group of the open button: it is shown only while the main menu is open.")]
        [SerializeField]
        private CanvasGroup openButtonGroup;

        [Tooltip("Optional. When set, the open button is visible only while the game is in this menu. Connect it in the scene.")]
        [SerializeField]
        private MainMenu mainMenu;

        [Tooltip("The window with the table. Switched off until it is opened.")]
        [SerializeField]
        private GameObject window;

        [SerializeField]
        private Button backButton;

        [Tooltip("Optional. Closes the window (UI/Cancel). Its action map is enabled by its owner, not here.")]
        [SerializeField]
        private InputActionReference cancelAction;

        [Header("Death")]
        [Tooltip("Optional. When set, the window opens after a death and closing it returns to the main menu. Connect it in the scene; "
            + "the round loop must not restart the run by itself (Restart Automatically off).")]
        [SerializeField]
        private RoundController roundController;

        [Tooltip("Time after the death before the window opens, so the death screen is seen first.")]
        [SerializeField]
        [Min(0f)]
        private float openAfterDeathSeconds = 1.5f;

        [Header("Window")]
        [SerializeField]
        private RowView[] rows = new RowView[0];

        [SerializeField]
        private TMP_Text statusLabel;

        [SerializeField]
        private Color playerColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        [SerializeField]
        private Color rowColor = new Color(0.93f, 0.9f, 0.84f, 1f);

        [Header("Name entry")]
        [Tooltip("Shown only when there is a score that was not sent yet.")]
        [SerializeField]
        private GameObject entryPanel;

        [SerializeField]
        private TMP_Text scoreLabel;

        [Tooltip("{0} is replaced with the score of the run.")]
        [SerializeField]
        private string scoreFormat = "Twój wynik: {0}";

        [SerializeField]
        private TMP_InputField nameInput;

        [SerializeField]
        private Button submitButton;

        [Header("Account")]
        [Tooltip("Shown only when the player already has an account (a name was confirmed before).")]
        [SerializeField]
        private GameObject accountRow;

        [SerializeField]
        private TMP_Text accountLabel;

        [Tooltip("{0} is replaced with the name of the account.")]
        [SerializeField]
        private string accountFormat = "Grasz jako: {0}";

        [Tooltip("Signs out and forgets the name.")]
        [SerializeField]
        private Button resetButton;

        [Header("Texts")]
        [SerializeField]
        private string loadingText = "Ładowanie…";

        [SerializeField]
        private string cacheText = "Brak połączenia. Pokazuję ostatnio zapisaną listę.";

        [SerializeField]
        private string emptyText = "Brak wyników do wyświetlenia.";

        [SerializeField]
        private string noIdText = "Nie ustawiono ID tabeli wyników.";

        [SerializeField]
        private string sendingText = "Wysyłanie wyniku…";

        [SerializeField]
        private string sendFailedText = "Nie udało się wysłać wyniku. Spróbuj ponownie.";

        [SerializeField]
        private string resetFailedText = "Nie udało się zresetować konta.";

        [SerializeField]
        private string nameTooShortText = "Imię musi mieć co najmniej 3 znaki.";

        private bool isRefreshing;
        private bool isSubmitting;
        private bool returnsToMenu;

        public bool IsOpen => window && window.activeSelf;

        private string SavedName => PlayerPrefs.GetString(NameKey, string.Empty);

        private bool HasAccount => string.IsNullOrEmpty(SavedName) == false;

        private void Reset()
        {
            service = GetComponentInChildren<LeaderboardService>();
            scoreCollector = GetComponentInChildren<ScoreCollector>();
            roundController = FindAnyObjectByType<RoundController>();
            mainMenu = FindAnyObjectByType<MainMenu>();
        }

        private void OnEnable()
        {
            if (openButton)
                openButton.onClick.AddListener(OpenButton_OnClick);

            if (submitButton)
                submitButton.onClick.AddListener(SubmitButton_OnClick);

            if (resetButton)
                resetButton.onClick.AddListener(ResetButton_OnClick);

            if (nameInput)
                nameInput.characterLimit = MaxNameLength;

            if (scoreCollector)
                scoreCollector.OnScoreChanged += ScoreCollector_OnScoreChanged;

            if (backButton)
                backButton.onClick.AddListener(BackButton_OnClick);

            if (cancelAction)
                cancelAction.action.performed += CancelAction_Performed;

            if (window && IsOpen == false)
                window.SetActive(false);

            if (roundController)
                roundController.OnRunEnded += RoundController_OnRunEnded;
        }

        private void Update()
        {
            if (mainMenu && openButtonGroup)
                SetOpenButtonVisible(mainMenu.IsInMenu && IsOpen == false);
        }

        /// <summary>Opens the window and downloads the top list.</summary>
        public void Open()
        {
            if (window == null || IsOpen)
                return;

            window.SetActive(true);
            Select(backButton);
            ShowEntry();
            RefreshAsync();
        }

        /// <summary>Closes the window. After a death it also returns to the main menu.</summary>
        public void Close()
        {
            if (IsOpen == false)
                return;

            window.SetActive(false);
            if (returnsToMenu)
            {
                returnsToMenu = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            Select(openButton);
        }

        private static void Select(Selectable selectable)
        {
            if (selectable && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private async void RefreshAsync()
        {
            if (isRefreshing)
                return;

            isRefreshing = true;
            ShowRows(Array.Empty<LeaderboardRow>());
            SetStatus(service.HasLeaderboardId ? loadingText : noIdText);

            var result = await service.FetchTopAsync();
            isRefreshing = false;
            if (this == null)
                return;

            ShowRows(result.Rows);
            SetStatus(result.Source switch
            {
                LeaderboardSource.Server => string.Empty,
                LeaderboardSource.Cache => cacheText,
                _ => service.HasLeaderboardId ? emptyText : noIdText,
            });
        }

        private async void SubmitAsync()
        {
            if (isSubmitting || scoreCollector == null || scoreCollector.CanSubmit == false)
                return;

            var playerName = HasAccount ? SavedName : LeaderboardService.CleanName(nameInput.text, MaxNameLength);
            if (LeaderboardService.IsNameValid(playerName) == false)
            {
                SetStatus(nameTooShortText);
                return;
            }

            isSubmitting = true;
            submitButton.interactable = false;
            SetStatus(sendingText);

            bool isSent = await service.SubmitScoreAsync(playerName, scoreCollector.Score);
            isSubmitting = false;
            if (this == null)
                return;

            submitButton.interactable = true;
            if (isSent == false)
            {
                SetStatus(sendFailedText);
                return;
            }

            PlayerPrefs.SetString(NameKey, playerName);
            scoreCollector.MarkSubmitted();
            ShowEntry();
            RefreshAsync();
        }

        private async void ResetAccountAsync()
        {
            bool isReset = await service.ResetAccountAsync();
            if (this == null)
                return;

            if (isReset == false)
            {
                SetStatus(resetFailedText);
                return;
            }

            PlayerPrefs.DeleteKey(NameKey);
            if (nameInput)
                nameInput.text = string.Empty;

            ShowEntry();
            RefreshAsync();
        }

        private void ShowEntry()
        {
            if (accountRow)
                accountRow.SetActive(HasAccount);

            if (HasAccount && accountLabel)
                accountLabel.text = string.Format(accountFormat, SavedName);

            if (nameInput)
                nameInput.gameObject.SetActive(HasAccount == false);

            bool canSubmit = scoreCollector && scoreCollector.CanSubmit;
            if (entryPanel)
                entryPanel.SetActive(canSubmit);

            if (canSubmit && scoreLabel)
                scoreLabel.text = string.Format(scoreFormat, scoreCollector.Score);
        }

        private void ShowRows(IReadOnlyList<LeaderboardRow> data)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < data.Count)
                    rows[i].Show(data[i], playerColor, rowColor);
                else
                    rows[i].Hide();
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = text;
            statusLabel.gameObject.SetActive(string.IsNullOrEmpty(text) == false);
        }

        private void SetOpenButtonVisible(bool isVisible)
        {
            openButtonGroup.alpha = isVisible ? 1f : 0f;
            openButtonGroup.interactable = isVisible;
            openButtonGroup.blocksRaycasts = isVisible;
        }

        private IEnumerator DeathOpeningCo()
        {
            yield return new WaitForSecondsRealtime(openAfterDeathSeconds);
            returnsToMenu = true;
            Open();
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            StartCoroutine(DeathOpeningCo());
        }

        private void BackButton_OnClick()
        {
            Close();
        }

        private void CancelAction_Performed(InputAction.CallbackContext context)
        {
            Close();
        }

        private void OpenButton_OnClick()
        {
            Open();
        }

        private void ResetButton_OnClick()
        {
            ResetAccountAsync();
        }

        private void SubmitButton_OnClick()
        {
            SubmitAsync();
        }

        private void ScoreCollector_OnScoreChanged(int score)
        {
            if (IsOpen)
                ShowEntry();
        }

        private void OnDisable()
        {
            if (openButton)
                openButton.onClick.RemoveListener(OpenButton_OnClick);

            if (submitButton)
                submitButton.onClick.RemoveListener(SubmitButton_OnClick);

            if (resetButton)
                resetButton.onClick.RemoveListener(ResetButton_OnClick);

            if (scoreCollector)
                scoreCollector.OnScoreChanged -= ScoreCollector_OnScoreChanged;

            if (backButton)
                backButton.onClick.RemoveListener(BackButton_OnClick);

            if (cancelAction)
                cancelAction.action.performed -= CancelAction_Performed;

            if (roundController)
                roundController.OnRunEnded -= RoundController_OnRunEnded;
        }

        /// <summary>One line of the table.</summary>
        [Serializable]
        public class RowView
        {
            [SerializeField]
            private GameObject root;

            [SerializeField]
            private TMP_Text rankLabel;

            [SerializeField]
            private TMP_Text nameLabel;

            [SerializeField]
            private TMP_Text scoreLabel;

            public void Show(LeaderboardRow row, Color playerColor, Color rowColor)
            {
                var color = row.IsPlayer ? playerColor : rowColor;
                rankLabel.text = row.Rank.ToString();
                nameLabel.text = row.PlayerName;
                scoreLabel.text = row.Score.ToString();
                rankLabel.color = color;
                nameLabel.color = color;
                scoreLabel.color = color;
                root.SetActive(true);
            }

            public void Hide()
            {
                root.SetActive(false);
            }
        }
    }
}
