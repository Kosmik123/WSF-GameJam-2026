using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// The two slots in front of the skill bar: the basic attack (a shot for the mage, a sword for the warrior)
    /// and the dodge with its cooldown.
    /// </summary>
    public class BasicActionsView : MonoBehaviour
    {
        [SerializeField]
        private PlayerClassApplier playerClass;

        [SerializeField]
        private PlayerCombat playerCombat;

        [Header("Attack")]
        [SerializeField]
        private Image attackIcon;

        [SerializeField]
        private Sprite rangedIcon;

        [SerializeField]
        private Sprite meleeIcon;

        [SerializeField]
        private TMP_Text attackLabel;

        [SerializeField]
        private string rangedText = "LPM · strzał";

        [SerializeField]
        private string meleeText = "LPM · cios";

        [Header("Dodge")]
        [Tooltip("Optional. Shown over the dodge slot while the dodge is on cooldown.")]
        [SerializeField]
        private GameObject dodgeCooldown;

        [SerializeField]
        private TMP_Text dodgeCooldownLabel;

        private PlayerClass shownClass;

        private void Reset()
        {
            playerClass = FindAnyObjectByType<PlayerClassApplier>();
            playerCombat = FindAnyObjectByType<PlayerCombat>();
        }

        private void LateUpdate()
        {
            if (playerClass && playerClass.Current != shownClass)
                ShowClass(playerClass.Current);

            if (playerCombat == null || dodgeCooldown == null)
                return;

            float secondsLeft = playerCombat.DodgeCooldownLeft;
            dodgeCooldown.SetActive(secondsLeft > 0.05f);
            if (dodgeCooldownLabel && secondsLeft > 0.05f)
                dodgeCooldownLabel.text = secondsLeft.ToString("0.0");
        }

        private void ShowClass(PlayerClass newClass)
        {
            shownClass = newClass;
            bool isRanged = newClass && newClass.IsRanged;
            if (attackIcon)
                attackIcon.sprite = isRanged ? rangedIcon : meleeIcon;

            if (attackLabel)
                attackLabel.text = isRanged ? rangedText : meleeText;
        }
    }
}
