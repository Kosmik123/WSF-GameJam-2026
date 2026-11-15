using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// A screen opened from a menu (settings, controls) and closed with its back button or the Cancel action.
    /// When it closes, the button that opened it is selected again. Menus that also react to Cancel ask
    /// <see cref="IsAnyOpenOrJustClosed"/>, so one press of Escape closes only the top screen.
    /// </summary>
    public class SubScreen : MonoBehaviour
    {
        /// <summary>The screen was closed.</summary>
        public event Action OnClosed;

        private static int openCount;
        private static int lastClosedFrame = -1;

        [SerializeField]
        private CanvasGroup screen;

        [SerializeField]
        private Button backButton;

        [Tooltip("Optional. Closes the screen (UI/Cancel). Its action map is enabled by its owner, not here.")]
        [SerializeField]
        private InputActionReference cancelAction;

        [Tooltip("Optional. Selected when the screen opens, for keyboard and gamepad.")]
        [SerializeField]
        private Selectable firstSelected;

        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.15f;

        private Selectable returnFocus;

        public bool IsOpen { get; private set; }

        /// <summary>True while any sub screen is open, and in the frame one was closed.</summary>
        public static bool IsAnyOpenOrJustClosed => openCount > 0 || lastClosedFrame == Time.frameCount;

        private void OnEnable()
        {
            if (backButton)
                backButton.onClick.AddListener(Close);

            if (cancelAction)
                cancelAction.action.performed += CancelAction_Performed;

            if (screen && IsOpen == false)
                screen.gameObject.SetActive(false);
        }

        /// <summary>Opens the screen; <paramref name="focusAfterClose"/> is selected again when it closes.</summary>
        public void Open(Selectable focusAfterClose)
        {
            if (IsOpen)
                return;

            IsOpen = true;
            openCount++;
            returnFocus = focusAfterClose;
            if (screen)
            {
                screen.gameObject.SetActive(true);
                screen.alpha = 0f;
                StartCoroutine(UiFade.FadingCo(screen, 1f, fadeSeconds));
            }

            Select(firstSelected ? firstSelected : backButton);
        }

        public void Close()
        {
            if (IsOpen == false)
                return;

            IsOpen = false;
            openCount--;
            lastClosedFrame = Time.frameCount;
            StopAllCoroutines();
            if (screen)
                screen.gameObject.SetActive(false);

            Select(returnFocus);
            OnClosed?.Invoke();
        }

        private void CancelAction_Performed(InputAction.CallbackContext context)
        {
            Close();
        }

        private static void Select(Selectable selectable)
        {
            if (selectable && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private void OnDisable()
        {
            if (backButton)
                backButton.onClick.RemoveListener(Close);

            if (cancelAction)
                cancelAction.action.performed -= CancelAction_Performed;

            Close();
        }
    }
}
