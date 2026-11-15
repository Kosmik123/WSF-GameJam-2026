using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The pause menu, toggled with the Cancel action (Escape, the Start button of a gamepad).
    /// Shows the state of the run (round, level, lap, skills) and offers: back to the game, settings, controls
    /// and giving up the run. Does nothing while the game is already paused by something else (main menu, level up).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField]
        private GamePause pause;

        [Tooltip("Opens and closes the menu (UI/Cancel). Its action map is enabled by its owner, not here.")]
        [SerializeField]
        private InputActionReference toggleAction;

        [SerializeField]
        private CanvasGroup screen;

        [Header("Buttons")]
        [SerializeField]
        private Button resumeButton;

        [SerializeField]
        private Button settingsButton;

        [SerializeField]
        private Button controlsButton;

        [Tooltip("Ends the run at once, as if the player died.")]
        [SerializeField]
        private Button surrenderButton;

        [Header("Screens")]
        [SerializeField]
        private SubScreen settingsScreen;

        [SerializeField]
        private SubScreen controlsScreen;

        [Header("State of the run")]
        [SerializeField]
        private Health playerHealth;

        [SerializeField]
        private RoundState roundState;

        [SerializeField]
        private LapTracker lapTracker;

        [SerializeField]
        private TMP_Text roundLabel;

        [SerializeField]
        private TMP_Text lapLabel;

        [Tooltip("{0} is replaced with the lap progress in percent.")]
        [SerializeField]
        private string lapFormat = "{0:0}%";

        [Header("Animation")]
        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.2f;

        private bool isOpen;

        private void Reset()
        {
            pause = FindAnyObjectByType<GamePause>();
            roundState = FindAnyObjectByType<RoundState>();
            lapTracker = FindAnyObjectByType<LapTracker>();
        }

        private void OnEnable()
        {
            if (toggleAction)
                toggleAction.action.performed += ToggleAction_Performed;

            if (resumeButton)
                resumeButton.onClick.AddListener(Close);

            if (settingsButton)
                settingsButton.onClick.AddListener(SettingsButton_OnClick);

            if (controlsButton)
                controlsButton.onClick.AddListener(ControlsButton_OnClick);

            if (surrenderButton)
                surrenderButton.onClick.AddListener(Surrender);

            if (screen)
                screen.gameObject.SetActive(false);
        }

        /// <summary>Pauses the game and shows the menu.</summary>
        public void Open()
        {
            if (isOpen || pause.IsPaused)
                return;

            isOpen = true;
            pause.Request(this);
            ShowRunState();

            if (screen == null)
                return;

            screen.gameObject.SetActive(true);
            screen.alpha = 0f;
            StopAllCoroutines();
            StartCoroutine(UiFade.FadingCo(screen, 1f, fadeSeconds));

            if (resumeButton && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        /// <summary>Hides the menu and lets the game run again.</summary>
        public void Close()
        {
            if (isOpen == false)
                return;

            isOpen = false;
            StopAllCoroutines();
            CloseScreen(settingsScreen);
            CloseScreen(controlsScreen);
            if (screen)
                screen.gameObject.SetActive(false);

            pause.Release(this);
        }

        private void ToggleAction_Performed(InputAction.CallbackContext context)
        {
            // Escape first closes the settings or the controls, and only then the pause.
            if (SubScreen.IsAnyOpenOrJustClosed)
                return;

            if (isOpen)
            {
                Close();
                return;
            }

            Open();
        }

        private void ShowRunState()
        {
            if (roundLabel && roundState)
                roundLabel.text = roundState.Round.ToString();

            if (lapLabel && lapTracker)
                lapLabel.text = string.Format(lapFormat, Mathf.Clamp01(lapTracker.Progress) * 100f);
        }

        private void SettingsButton_OnClick()
        {
            if (settingsScreen)
                settingsScreen.Open(settingsButton);
        }

        private void ControlsButton_OnClick()
        {
            if (controlsScreen)
                controlsScreen.Open(controlsButton);
        }

        private void Surrender()
        {
            Close();
            if (playerHealth)
                playerHealth.Kill();
        }

        private static void CloseScreen(SubScreen subScreen)
        {
            if (subScreen)
                subScreen.Close();
        }

        private void OnDisable()
        {
            if (toggleAction)
                toggleAction.action.performed -= ToggleAction_Performed;

            if (resumeButton)
                resumeButton.onClick.RemoveListener(Close);

            if (settingsButton)
                settingsButton.onClick.RemoveListener(SettingsButton_OnClick);

            if (controlsButton)
                controlsButton.onClick.RemoveListener(ControlsButton_OnClick);

            if (surrenderButton)
                surrenderButton.onClick.RemoveListener(Surrender);

            if (isOpen)
                Close();
        }
    }
}
