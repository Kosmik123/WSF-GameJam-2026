using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// The level up screen (GDD 9): the game pauses and the player picks one of up to three cards, each a new skill,
    /// an upgrade or the evolution of an owned one. Several levels gained at once show the screen several times.
    /// Cards are picked with the mouse, the keyboard or a gamepad, or directly with the keys 1, 2 and 3.
    /// </summary>
    public class LevelUpScreen : MonoBehaviour
    {
        [SerializeField]
        private PlayerLevel playerLevel;

        [SerializeField]
        private SkillSlots skills;

        [SerializeField]
        private GamePause pause;

        [SerializeField]
        private CanvasGroup screen;

        [SerializeField]
        private TMP_Text titleLabel;

        [Tooltip("{0} is replaced with the new level.")]
        [SerializeField]
        private string titleFormat = "Poziom {0}";

        [SerializeField]
        private TMP_Text bonusLabel;

        [Tooltip("{0}: health per level, {1}: damage per level in percent.")]
        [SerializeField]
        private string bonusFormat = "+{0} HP · +{1:0}% obrażeń · wybierz jedną kartę (1 / 2 / 3)";

        [SerializeField]
        private LevelUpCardView[] cards = new LevelUpCardView[0];

        [Tooltip("Optional. One action per card (UI/Choose1, Choose2, Choose3). Their action map is enabled by its owner.")]
        [SerializeField]
        private InputActionReference[] chooseActions = new InputActionReference[0];

        [SerializeField]
        [Min(0f)]
        private float fadeSeconds = 0.25f;

        private readonly List<SkillDefinition> offers = new List<SkillDefinition>();
        private int pendingLevels;
        private bool isOpen;

        private void Reset()
        {
            playerLevel = FindAnyObjectByType<PlayerLevel>();
            skills = FindAnyObjectByType<SkillSlots>();
            pause = FindAnyObjectByType<GamePause>();
            cards = GetComponentsInChildren<LevelUpCardView>(true);
        }

        private void OnEnable()
        {
            if (screen)
                screen.gameObject.SetActive(false);

            if (playerLevel)
            {
                playerLevel.OnLevelUp += PlayerLevel_OnLevelUp;
                playerLevel.OnLevelReset += PlayerLevel_OnLevelReset;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i])
                {
                    cards[i].Button.onClick.AddListener(() => Choose(index));
                }
            }

            foreach (var action in chooseActions)
                if (action)
                    action.action.performed += ChooseAction_Performed;
        }

        private void PlayerLevel_OnLevelUp(int level)
        {
            pendingLevels++;
            if (isOpen == false)
                ShowNextOffer();
        }

        private void PlayerLevel_OnLevelReset()
        {
            pendingLevels = 0;
            Close();
        }

        private void ShowNextOffer()
        {
            if (pendingLevels <= 0 || skills == null)
                return;

            offers.Clear();
            skills.GetOfferableSkills(offers);
            if (offers.Count == 0)
            {
                // Every skill is at its evolution: the level still gives its health and damage.
                pendingLevels = 0;
                return;
            }

            pendingLevels--;
            Shuffle(offers);
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    continue;

                bool hasOffer = i < offers.Count;
                cards[i].gameObject.SetActive(hasOffer);
                if (hasOffer)
                {
                    cards[i].Show(offers[i], skills.TierOf(offers[i]), i + 1);
                }
            }

            if (titleLabel)
                titleLabel.text = string.Format(titleFormat, playerLevel.Level - pendingLevels);

            if (bonusLabel)
                bonusLabel.text = string.Format(bonusFormat, playerLevel.HealthPerLevel, playerLevel.DamagePerLevel * 100f);

            Open();
        }

        private void Open()
        {
            isOpen = true;
            pause.Request(this);
            if (screen == null)
                return;

            screen.gameObject.SetActive(true);
            screen.alpha = 0f;
            StopAllCoroutines();
            StartCoroutine(UiFade.FadingCo(screen, 1f, fadeSeconds));
            if (cards.Length > 0 && cards[0] && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(cards[0].gameObject);
        }

        private void Choose(int index)
        {
            if (isOpen == false || index < 0 || index >= cards.Length || cards[index] == null || cards[index].gameObject.activeSelf == false)
                return;

            skills.Grant(cards[index].Skill);
            Close();
            ShowNextOffer();
        }

        private void Close()
        {
            if (isOpen == false)
                return;

            isOpen = false;
            StopAllCoroutines();
            if (screen)
                screen.gameObject.SetActive(false);

            pause.Release(this);
        }

        private void ChooseAction_Performed(InputAction.CallbackContext context)
        {
            for (int i = 0; i < chooseActions.Length; i++)
                if (chooseActions[i] && chooseActions[i].action == context.action)
                    Choose(i);
        }

        private static void Shuffle(List<SkillDefinition> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                (list[i], list[other]) = (list[other], list[i]);
            }
        }

        private void OnDisable()
        {
            if (playerLevel)
            {
                playerLevel.OnLevelUp -= PlayerLevel_OnLevelUp;
                playerLevel.OnLevelReset -= PlayerLevel_OnLevelReset;
            }

            foreach (var card in cards)
                if (card)
                    card.Button.onClick.RemoveAllListeners();

            foreach (var action in chooseActions)
                if (action)
                    action.action.performed -= ChooseAction_Performed;

            Close();
        }
    }
}
