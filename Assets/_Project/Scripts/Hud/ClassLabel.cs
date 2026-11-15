using TMPro;
using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Hud
{
    /// <summary>Shows the name of the current class of the player, for example "MAG".</summary>
    public class ClassLabel : MonoBehaviour
    {
        [SerializeField]
        private PlayerClassApplier playerClass;

        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        private bool upperCase = true;

        private PlayerClass shownClass;

        private void Reset()
        {
            playerClass = FindAnyObjectByType<PlayerClassApplier>();
            label = GetComponent<TMP_Text>();
        }

        // The class can change in the main menu; checking a reference every frame is cheaper than an event here.
        private void LateUpdate()
        {
            if (playerClass == null || label == null || playerClass.Current == shownClass)
                return;

            shownClass = playerClass.Current;
            string className = shownClass ? shownClass.DisplayName : string.Empty;
            label.text = upperCase ? className.ToUpperInvariant() : className;
        }
    }
}
