using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WSFGameJam2026.Hud
{
    /// <summary>One card of the <see cref="LevelUpScreen"/>: key, icon, title and description, clickable. Pops in when shown.</summary>
    [RequireComponent(typeof(Button))]
    public class LevelUpCard : MonoBehaviour
    {
        /// <summary>The card was clicked.</summary>
        public event Action<LevelUpCard> OnChosen;

        [SerializeField]
        private TMP_Text keyLabel;

        [SerializeField]
        private TMP_Text title;

        [SerializeField]
        private TMP_Text description;

        [SerializeField]
        private Image icon;

        [SerializeField]
        [Min(0.01f)]
        private float popSeconds = 0.25f;

        private Button button;
        private float shownAt;
        private float delay;

        // The card can be used before its Awake (it lives under an inactive screen), so the button is taken when needed.
        private Button Button
        {
            get
            {
                if (button == null)
                    button = GetComponent<Button>();

                return button;
            }
        }

        public bool IsInteractable
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }

        private void Awake()
        {
            Button.onClick.AddListener(Button_OnClick);
        }

        public void Show(string key, string newTitle, string newDescription, Sprite newIcon, float popDelay)
        {
            gameObject.SetActive(true);
            SetText(keyLabel, key);
            SetText(title, newTitle);
            SetText(description, newDescription);
            if (icon)
            {
                icon.sprite = newIcon;
                icon.enabled = newIcon != null;
            }

            shownAt = Time.unscaledTime;
            delay = popDelay;
            transform.localScale = Vector3.one * 0.6f;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Button_OnClick()
        {
            OnChosen?.Invoke(this);
        }

        // Unscaled, because the game is paused while the cards are shown.
        private void Update()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - shownAt - delay) / popSeconds);
            float back = 1.70158f;
            float eased = 1f + (back + 1f) * Mathf.Pow(progress - 1f, 3f) + back * Mathf.Pow(progress - 1f, 2f);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label)
                label.text = text;
        }

        private void OnDestroy()
        {
            if (button)
                button.onClick.RemoveListener(Button_OnClick);
        }
    }
}
