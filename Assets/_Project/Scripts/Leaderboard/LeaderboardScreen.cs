using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Menus;

namespace WSFGameJam2026.Leaderboard
{
    /// <summary>
    /// The leaderboard window: the top places downloaded from the server (or the cached ones when it can not be reached,
    /// or nothing), and, when the <see cref="ScoreCollector"/> holds a score that was not sent yet, a field for the name
    /// of the player with a confirm button that sends the score.
    /// Opened by the menu button, or from code with <see cref="Open"/> (for example by the death screen).
    /// </summary>
    public class LeaderboardScreen : MonoBehaviour
    {
        private const string NameKey = "WSFGameJam2026.Leaderboard.Name";
        private const int MaxNameLength = 16;

        [SerializeField]
        private LeaderboardService service;

        [SerializeField]
        private ScoreCollector scoreCollector;

        [SerializeField]
        private SubScreen subScreen;

        [Header("Opening")]
        [Tooltip("Button of the menu that opens the window.")]
        [SerializeField]
        private Button openButton;

        [Tooltip("Optional. Group of the open button: it is shown only while the main menu is open.")]
        [SerializeField]
        private CanvasGroup openButtonGroup;

        [Tooltip("Optional. When set, the open button is visible only while this menu is on the screen. Connect it in the scene.")]
        [SerializeField]
        private MainMenu mainMenu;

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
        private string nameTooShortText = "Imię musi mieć co najmniej 3 znaki.";

        private bool isRefreshing;
        private bool isSubmitting;

        private void OnEnable()
        {
            if (openButton)
                openButton.onClick.AddListener(OpenButton_OnClick);

            if (submitButton)
                submitButton.onClick.AddListener(SubmitButton_OnClick);

            if (nameInput)
            {
                nameInput.characterLimit = MaxNameLength;
                nameInput.text = PlayerPrefs.GetString(NameKey, string.Empty);
            }

            if (scoreCollector)
                scoreCollector.OnScoreChanged += ScoreCollector_OnScoreChanged;
        }

        private void Update()
        {
            if (mainMenu && openButtonGroup)
                SetOpenButtonVisible(mainMenu.IsOpen && subScreen.IsOpen == false);
        }

        /// <summary>Opens the window and downloads the top list.</summary>
        public void Open()
        {
            if (subScreen == null || subScreen.IsOpen)
                return;

            subScreen.Open(openButton);
            ShowEntry();
            RefreshAsync();
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

            var playerName = LeaderboardService.CleanName(nameInput.text, MaxNameLength);
            if (LeaderboardService.IsNameValid(playerName) == false)
            {
                SetStatus(nameTooShortText);
                return;
            }

            isSubmitting = true;
            submitButton.interactable = false;
            SetStatus(sendingText);
            PlayerPrefs.SetString(NameKey, playerName);

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

            scoreCollector.MarkSubmitted();
            ShowEntry();
            RefreshAsync();
        }

        private void ShowEntry()
        {
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

        private void OpenButton_OnClick()
        {
            Open();
        }

        private void SubmitButton_OnClick()
        {
            SubmitAsync();
        }

        private void ScoreCollector_OnScoreChanged(int score)
        {
            if (subScreen && subScreen.IsOpen)
                ShowEntry();
        }

        private void OnDisable()
        {
            if (openButton)
                openButton.onClick.RemoveListener(OpenButton_OnClick);

            if (submitButton)
                submitButton.onClick.RemoveListener(SubmitButton_OnClick);

            if (scoreCollector)
                scoreCollector.OnScoreChanged -= ScoreCollector_OnScoreChanged;
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
