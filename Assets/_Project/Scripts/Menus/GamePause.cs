using System;
using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Controls;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The only owner of the pause. Menus ask for it with <see cref="Request"/> and give it back with <see cref="Release"/>;
    /// the game stays paused while anyone holds it (the main menu and the pause menu can overlap).
    /// While paused the time scale is exactly 0 (the hit stop tells it apart from its own freeze)
    /// and the owner of the player's action map is off, so attacks and skills do not fire.
    /// </summary>
    public class GamePause : MonoBehaviour
    {
        /// <summary>The pause started (true) or ended (false).</summary>
        public event Action<bool> OnPausedChanged;

        [Tooltip("Owner of the player's action map. Disabled while the game is paused.")]
        [SerializeField]
        private InputActionMapEnabler playerInput;

        private readonly HashSet<UnityEngine.Object> holders = new HashSet<UnityEngine.Object>();

        public bool IsPaused => holders.Count > 0;

        private void Reset()
        {
            playerInput = FindAnyObjectByType<InputActionMapEnabler>();
        }

        /// <summary>Pauses the game on behalf of the holder. Asking twice with the same holder changes nothing.</summary>
        public void Request(UnityEngine.Object holder)
        {
            if (holders.Add(holder) && holders.Count == 1)
                Apply();
        }

        /// <summary>Gives the pause back. The game runs again when nobody holds it.</summary>
        public void Release(UnityEngine.Object holder)
        {
            if (holders.Remove(holder) && holders.Count == 0)
                Apply();
        }

        private void Apply()
        {
            bool isPaused = IsPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            if (playerInput)
                playerInput.enabled = isPaused == false;

            OnPausedChanged?.Invoke(isPaused);
        }

        private void OnDestroy()
        {
            // Loading the scene again must not start it frozen.
            if (IsPaused)
                Time.timeScale = 1f;
        }
    }
}
