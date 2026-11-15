using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WSFGameJam2026.Cameras;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The start of the game: a short loading screen, then the menu (class choice, record, play, quit) over the torus
    /// seen from above. "Play" hides the menu, flies the camera down behind the player and only then unpauses the game.
    /// The round loop already placed the player and the jaw at the start; the pause keeps them waiting.
    /// Starts before the other components, so the game is paused before the first round begins.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MainMenu : MonoBehaviour
    {
        /// <summary>The intro ended and the game runs.</summary>
        public event Action OnGameStarted;

        [SerializeField]
        private GamePause pause;

        [SerializeField]
        private CameraIntro cameraIntro;

        [SerializeField]
        private RoundController roundController;

        [SerializeField]
        private PlayerClassApplier playerClass;

        [Tooltip("Optional. Shows the round number when the game starts.")]
        [SerializeField]
        private RoundBanner roundBanner;

        [Header("Screens")]
        [Tooltip("The whole menu screen. Fades out when the game starts.")]
        [SerializeField]
        private CanvasGroup menuScreen;

        [Tooltip("Optional. The HUD, hidden while the menu is open.")]
        [SerializeField]
        private CanvasGroup hud;

        [Header("Loading")]
        [SerializeField]
        private GameObject loadingPanel;

        [Tooltip("Image of type Filled.")]
        [SerializeField]
        private Image loadingFill;

        [SerializeField]
        private TMP_Text loadingTip;

        [SerializeField]
        private string[] loadingTips =
        {
            "Budzenie węża…",
            "Splatanie segmentów ciała…",
            "Ostrzenie łusek…",
            "Paszcza otwiera się…",
        };

        [SerializeField]
        [Min(0f)]
        private float loadingSeconds = 1.6f;

        [Header("Menu")]
        [SerializeField]
        private CanvasGroup menuPanel;

        [SerializeField]
        private Button playButton;

        [SerializeField]
        private Button quitButton;

        [SerializeField]
        private Button settingsButton;

        [SerializeField]
        private SubScreen settingsScreen;

        [SerializeField]
        private TMP_Text recordLabel;

        [Tooltip("{0} is replaced with the best number of completed rounds.")]
        [SerializeField]
        private string recordFormat = "Rekord: {0}";

        [SerializeField]
        private ClassOption[] classOptions = new ClassOption[0];

        [SerializeField]
        private Color selectedFrameColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        [SerializeField]
        private Color idleFrameColor = new Color(0.23f, 0.19f, 0.16f, 1f);

        [Header("Animation")]
        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.5f;

        private bool isStarting;

        /// <summary>True from the start of the scene until the camera reached the player.</summary>
        public bool IsOpen { get; private set; }

        private void Reset()
        {
            pause = FindAnyObjectByType<GamePause>();
            cameraIntro = FindAnyObjectByType<CameraIntro>();
            roundController = FindAnyObjectByType<RoundController>();
            playerClass = FindAnyObjectByType<PlayerClassApplier>();
            roundBanner = FindAnyObjectByType<RoundBanner>();
        }

        private void OnEnable()
        {
            if (playButton)
                playButton.onClick.AddListener(StartGame);

            if (quitButton)
                quitButton.onClick.AddListener(QuitGame);

            if (settingsButton)
                settingsButton.onClick.AddListener(SettingsButton_OnClick);

            foreach (var option in classOptions)
                option.Bind(this);
        }

        private void Start()
        {
            IsOpen = true;
            pause.Request(this);
            if (cameraIntro)
                cameraIntro.ShowOverview();

            if (hud)
                hud.alpha = 0f;

            if (recordLabel && roundController)
                recordLabel.text = string.Format(recordFormat, roundController.BestRounds);

            RefreshClassFrames();
            StartCoroutine(LoadingCo());
        }

        /// <summary>Hides the menu and flies the camera to the player. The game runs when the camera arrives.</summary>
        public void StartGame()
        {
            if (isStarting || IsOpen == false)
                return;

            isStarting = true;
            StartCoroutine(GameStartCo());
        }

        /// <summary>Chooses the class of the player. Called by the class buttons.</summary>
        public void ChooseClass(PlayerClass chosen)
        {
            if (playerClass == null || chosen == null || isStarting)
                return;

            playerClass.Apply(chosen);
            RefreshClassFrames();
        }

        private IEnumerator LoadingCo()
        {
            SetVisible(menuPanel, false);
            if (loadingPanel)
                loadingPanel.SetActive(true);

            float elapsed = 0f;
            while (elapsed < loadingSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                ShowLoading(elapsed / loadingSeconds);
                yield return null;
            }

            ShowLoading(1f);
            if (loadingPanel)
                loadingPanel.SetActive(false);

            SetVisible(menuPanel, true);
            if (menuPanel)
            {
                menuPanel.alpha = 0f;
                yield return UiFade.FadingCo(menuPanel, 1f, fadeSeconds);
            }

            if (playButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        private void ShowLoading(float progress)
        {
            if (loadingFill)
                loadingFill.fillAmount = progress;

            if (loadingTip == null || loadingTips.Length == 0)
                return;

            int tipIndex = Mathf.Min(loadingTips.Length - 1, Mathf.FloorToInt(progress * loadingTips.Length));
            loadingTip.text = loadingTips[tipIndex];
        }

        private IEnumerator GameStartCo()
        {
            if (menuScreen)
                menuScreen.interactable = false;

            yield return UiFade.FadingCo(menuScreen, 0f, fadeSeconds);

            if (cameraIntro)
            {
                cameraIntro.PlayDescent();
                while (cameraIntro.IsPlaying)
                    yield return null;
            }

            if (menuScreen)
                menuScreen.gameObject.SetActive(false);

            IsOpen = false;
            pause.Release(this);
            if (roundBanner)
                roundBanner.ShowCurrentRound();

            OnGameStarted?.Invoke();
            yield return UiFade.FadingCo(hud, 1f, fadeSeconds);
        }

        private void RefreshClassFrames()
        {
            var current = playerClass ? playerClass.Current : null;
            foreach (var option in classOptions)
                option.ShowSelected(option.PlayerClass == current, selectedFrameColor, idleFrameColor);
        }

        private static void SetVisible(CanvasGroup group, bool isVisible)
        {
            if (group)
                group.gameObject.SetActive(isVisible);
        }

        private void SettingsButton_OnClick()
        {
            if (settingsScreen && isStarting == false)
                settingsScreen.Open(settingsButton);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable()
        {
            if (playButton)
                playButton.onClick.RemoveListener(StartGame);

            if (quitButton)
                quitButton.onClick.RemoveListener(QuitGame);

            if (settingsButton)
                settingsButton.onClick.RemoveListener(SettingsButton_OnClick);

            foreach (var option in classOptions)
                option.Unbind();
        }

        /// <summary>One button of the class choice.</summary>
        [Serializable]
        public class ClassOption
        {
            [SerializeField]
            private PlayerClass playerClass;
            public PlayerClass PlayerClass => playerClass;

            [SerializeField]
            private Button button;

            [Tooltip("Optional. Tinted with the selection color when this class is chosen.")]
            [SerializeField]
            private Graphic frame;

            private MainMenu menu;

            public void Bind(MainMenu owner)
            {
                menu = owner;
                if (button)
                    button.onClick.AddListener(Button_OnClick);
            }

            public void Unbind()
            {
                if (button)
                    button.onClick.RemoveListener(Button_OnClick);
            }

            public void ShowSelected(bool isSelected, Color selectedColor, Color idleColor)
            {
                if (frame)
                    frame.color = isSelected ? selectedColor : idleColor;
            }

            private void Button_OnClick()
            {
                menu.ChooseClass(playerClass);
            }
        }
    }
}
