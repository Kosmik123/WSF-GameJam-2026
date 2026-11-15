using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using WSFGameJam2026.Head;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Menu
{
    /// <summary>
    /// The intro of the game: the menu and the game are one scene. In the menu a camera looks at the whole torus from above,
    /// the head eats the tail, and the player is parked: his components are off (so he can not be hit or controlled),
    /// and the gameplay objects (HUD, enemies) are hidden. "Play" brings the player in, blends to the camera of the player
    /// and starts the first round.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [SerializeField]
        private Button playButton;

        [Tooltip("The menu screen with the Play button. Hidden when the game starts.")]
        [SerializeField]
        private GameObject menuScreen;

        [Header("Cameras")]
        [Tooltip("Looks at the whole torus from above.")]
        [SerializeField]
        private CinemachineCamera menuCamera;

        [Tooltip("The camera that follows the player.")]
        [SerializeField]
        private CinemachineCamera gameCamera;

        [Tooltip("Priority of the camera that is in use. The other one gets 0.")]
        [SerializeField]
        private int activePriority = 20;

        [Header("Fog")]
        [Tooltip("Density of the fog (Exponential Squared) in the game. There is no fog in the menu.")]
        [SerializeField]
        [Min(0f)]
        private float gameFogDensity = 0.08f;

        [Tooltip("Time in which the fog grows linearly from nothing to the density of the game after Play.")]
        [SerializeField]
        [Min(0f)]
        private float fogTransitionSeconds = 2f;

        [Header("Game")]
        [SerializeField]
        private RoundController controller;

        [SerializeField]
        private HeadMovement head;

        [Tooltip("Speed of the head in the menu, relative to its base speed.")]
        [SerializeField]
        [Min(0f)]
        private float menuHeadSpeedMultiplier = 1.5f;

        [Header("Hidden In The Menu")]
        [Tooltip("The player and everything that is only for the game: the model of the player, the HUD, the enemies. They are switched off in the menu and on when the game starts.")]
        [SerializeField]
        private GameObject[] hiddenInMenu;

        [Tooltip("The components that make the player alive (movement, combat, health...). Off in the menu: the parked player can not be controlled or hurt.")]
        [SerializeField]
        private Behaviour[] pausedInMenu;

        private bool isInMenu;

        /// <summary>True from the start until Play is pressed.</summary>
        public bool IsInMenu => isInMenu;

        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();

        private void Reset()
        {
            controller = FindAnyObjectByType<RoundController>();
            head = FindAnyObjectByType<HeadMovement>();
        }

        private void Start()
        {
            isInMenu = true;
            HideHud();
            SetGameplay(false);
            SetCameraPriorities(true);
            SetFog(0f);
            head.SpeedMultiplier = menuHeadSpeedMultiplier;
            head.IsMoving = true;
            if (menuScreen)
                menuScreen.SetActive(true);

            playButton.onClick.AddListener(PlayButton_OnClick);
        }

        // The parked player keeps his distance from the jaw, so Play can start the game wherever the jaw is.
        private void Update()
        {
            if (isInMenu)
                controller.PlacePlayerAheadOfHead();
        }

        private void OnDestroy()
        {
            if (playButton)
                playButton.onClick.RemoveListener(PlayButton_OnClick);
        }

        private void PlayButton_OnClick()
        {
            playButton.onClick.RemoveListener(PlayButton_OnClick);
            isInMenu = false;
            if (menuScreen)
                menuScreen.SetActive(false);

            SetGameplay(true);
            ShowHud();
            gameCamera.gameObject.SetActive(true);
            gameCamera.enabled = true;
            SetCameraPriorities(false);
            StartCoroutine(FogCo());

            // Puts the player and the head at the start of the first round (also resets the speed of the head).
            controller.StartRun();
        }

        // Every canvas except the menu is HUD (health, skills, round, screens), whatever it is made of: it is only switched off, not deactivated.
        private void HideHud()
        {
            var menuCanvas = menuScreen ? menuScreen.GetComponentInParent<Canvas>() : null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.enabled && canvas != menuCanvas && canvas.isRootCanvas && canvas.TryGetComponent<KeepVisibleInMenu>(out _) == false)
                {
                    canvas.enabled = false;
                    hiddenCanvases.Add(canvas);
                }
            }
        }

        private void ShowHud()
        {
            foreach (var canvas in hiddenCanvases)
            {
                if (canvas)
                    canvas.enabled = true;
            }

            hiddenCanvases.Clear();
        }

        private IEnumerator FogCo()
        {
            for (float time = 0f; time < fogTransitionSeconds; time += Time.deltaTime)
            {
                SetFog(gameFogDensity * time / fogTransitionSeconds);
                yield return null;
            }

            SetFog(gameFogDensity);
        }

        private static void SetFog(float density)
        {
            RenderSettings.fog = density > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = density;
        }

        private void SetGameplay(bool isOn)
        {
            foreach (var behaviour in pausedInMenu)
            {
                if (behaviour)
                    behaviour.enabled = isOn;
            }

            foreach (var hidden in hiddenInMenu)
            {
                if (hidden)
                    hidden.SetActive(isOn);
            }
        }

        // The Cinemachine brain blends to the camera with the higher priority.
        private void SetCameraPriorities(bool isMenu)
        {
            SetPriority(menuCamera, isMenu ? activePriority : 0);
            SetPriority(gameCamera, isMenu ? 0 : activePriority);
        }

        private static void SetPriority(CinemachineCamera target, int value)
        {
            target.Priority.Enabled = true;
            target.Priority.Value = value;
        }
    }
}
