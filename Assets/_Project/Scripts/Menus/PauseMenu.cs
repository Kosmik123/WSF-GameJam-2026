using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The pause menu, toggled with the Cancel action (Escape, the Start button of a gamepad).
    /// Shows the state of the run and lets the player go back, start the scene again or quit.
    /// Does nothing while the main menu is open.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField]
        private GamePause pause;

        [Tooltip("Optional. The pause menu does not open while the main menu is open.")]
        [SerializeField]
        private MainMenu mainMenu;

        [Tooltip("Opens and closes the menu (UI/Cancel). Its action map is enabled by its owner, not here.")]
        [SerializeField]
        private InputActionReference toggleAction;

        [SerializeField]
        private CanvasGroup screen;

        [Header("Buttons")]
        [SerializeField]
        private Button resumeButton;

        [SerializeField]
        private Button restartButton;

        [SerializeField]
        private Button quitButton;

        [Header("State of the run")]
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
            mainMenu = FindAnyObjectByType<MainMenu>();
            roundState = FindAnyObjectByType<RoundState>();
            lapTracker = FindAnyObjectByType<LapTracker>();
        }

        private void OnEnable()
        {
            if (toggleAction)
                toggleAction.action.performed += ToggleAction_Performed;

            if (resumeButton)
                resumeButton.onClick.AddListener(Close);

            if (restartButton)
                restartButton.onClick.AddListener(RestartScene);

            if (quitButton)
                quitButton.onClick.AddListener(QuitGame);

            if (screen)
                screen.gameObject.SetActive(false);
        }

        /// <summary>Pauses the game and shows the menu.</summary>
        public void Open()
        {
            if (isOpen || (mainMenu && mainMenu.IsOpen))
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
            if (screen)
                screen.gameObject.SetActive(false);

            pause.Release(this);
        }

        private void ToggleAction_Performed(InputAction.CallbackContext context)
        {
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

        private void RestartScene()
        {
            pause.Release(this);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
            if (toggleAction)
                toggleAction.action.performed -= ToggleAction_Performed;

            if (resumeButton)
                resumeButton.onClick.RemoveListener(Close);

            if (restartButton)
                restartButton.onClick.RemoveListener(RestartScene);

            if (quitButton)
                quitButton.onClick.RemoveListener(QuitGame);

            if (isOpen)
                Close();
        }
    }
}
