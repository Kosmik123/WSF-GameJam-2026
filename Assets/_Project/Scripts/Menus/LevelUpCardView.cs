using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// One card of the level up screen: what kind of offer it is (new skill, upgrade, evolution), its number key,
    /// the letter or icon of the skill, the name, what the next tier does and the tiers as pips (the new one in gold).
    /// The selected card (mouse over, keyboard, gamepad) rises and gets a gold frame.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LevelUpCardView : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        [SerializeField]
        private TMP_Text kindLabel;

        [SerializeField]
        private Image kindBackground;

        [SerializeField]
        private TMP_Text numberLabel;

        [SerializeField]
        private TMP_Text glyphLabel;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TMP_Text nameLabel;

        [SerializeField]
        private TMP_Text descriptionLabel;

        [SerializeField]
        private Image[] tierPips = new Image[0];

        [Header("Selection")]
        [Tooltip("Moved up while the card is selected.")]
        [SerializeField]
        private RectTransform content;

        [SerializeField]
        private Graphic frame;

        [SerializeField]
        private float selectedLift = 15f;

        [SerializeField]
        private Color frameColor = new Color(0.23f, 0.19f, 0.16f, 1f);

        [SerializeField]
        private Color selectedFrameColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        [Header("Kinds")]
        [SerializeField]
        private string newSkillText = "NOWY SKILL";

        [SerializeField]
        private Color newSkillBackground = new Color(0.16f, 0.14f, 0.22f, 1f);

        [SerializeField]
        private Color newSkillColor = new Color(0.79f, 0.72f, 0.94f, 1f);

        [SerializeField]
        private string upgradeText = "ULEPSZENIE";

        [SerializeField]
        private Color upgradeBackground = new Color(0.12f, 0.17f, 0.2f, 1f);

        [SerializeField]
        private Color upgradeColor = new Color(0.56f, 0.76f, 0.92f, 1f);

        [SerializeField]
        private string evolutionText = "EWOLUCJA";

        [SerializeField]
        private Color evolutionBackground = new Color(0.89f, 0.64f, 0.23f, 1f);

        [SerializeField]
        private Color evolutionColor = new Color(0.1f, 0.07f, 0.02f, 1f);

        [Header("Pips")]
        [SerializeField]
        private Color ownedPipColor = new Color(0.93f, 0.9f, 0.84f, 1f);

        [SerializeField]
        private Color newPipColor = new Color(0.89f, 0.64f, 0.23f, 1f);

        [SerializeField]
        private Color emptyPipColor = new Color(0.23f, 0.19f, 0.16f, 1f);

        private Button button;
        private bool isSelected;
        private float lift;

        public Button Button
        {
            get
            {
                if (button == null)
                    button = GetComponent<Button>();

                return button;
            }
        }

        /// <summary>The skill this card offers.</summary>
        public SkillDefinition Skill { get; private set; }

        /// <summary>Shows an offer. <paramref name="currentTier"/> is -1 when the player does not have the skill yet.</summary>
        public void Show(SkillDefinition skill, int currentTier, int number)
        {
            Skill = skill;
            int nextTier = currentTier + 1;
            ShowKind(currentTier, nextTier);

            if (numberLabel)
                numberLabel.text = number.ToString();

            string skillName = skill.DisplayName;
            if (nameLabel)
                nameLabel.text = skillName;

            if (descriptionLabel)
                descriptionLabel.text = skill.GetTier(nextTier).Description;

            if (iconImage)
            {
                iconImage.enabled = skill.Icon != null;
                iconImage.sprite = skill.Icon;
            }

            if (glyphLabel)
            {
                glyphLabel.text = skill.Icon == null && skillName.Length > 0 ? skillName.Substring(0, 1).ToUpperInvariant() : string.Empty;
                glyphLabel.color = skill.AccentColor;
            }

            for (int i = 0; i < tierPips.Length; i++)
                if (tierPips[i])
                    tierPips[i].color = GetPipColor(i, currentTier, nextTier);
        }

        public void OnSelect(BaseEventData eventData)
        {
            isSelected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isSelected = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        private void ShowKind(int currentTier, int nextTier)
        {
            string kindText = upgradeText;
            var background = upgradeBackground;
            var color = upgradeColor;
            if (currentTier < 0)
            {
                kindText = newSkillText;
                background = newSkillBackground;
                color = newSkillColor;
            }

            if (nextTier >= SkillDefinition.MaxTier)
            {
                kindText = evolutionText;
                background = evolutionBackground;
                color = evolutionColor;
            }

            if (kindLabel)
            {
                kindLabel.text = kindText;
                kindLabel.color = color;
            }

            if (kindBackground)
                kindBackground.color = background;
        }

        private Color GetPipColor(int pip, int currentTier, int nextTier)
        {
            if (pip <= currentTier)
                return ownedPipColor;

            return pip == nextTier ? newPipColor : emptyPipColor;
        }

        private void Update()
        {
            float target = isSelected ? 1f : 0f;
            lift = Mathf.MoveTowards(lift, target, 6f * Time.unscaledDeltaTime);
            float eased = Mathf.SmoothStep(0f, 1f, lift);

            if (content)
                content.anchoredPosition = new Vector2(0f, selectedLift * eased);

            if (frame)
                frame.color = Color.Lerp(frameColor, selectedFrameColor, eased);
        }

        private void OnDisable()
        {
            isSelected = false;
            lift = 0f;
        }
    }
}
