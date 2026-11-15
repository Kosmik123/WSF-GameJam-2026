using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// Card choice after a level up (port of the LD57 LevelUpScreen, without DOTween): the game pauses and shows up to 3 random
    /// skills that can be offered now (a new one for a free slot or the next tier of an owned one). Several level ups at once
    /// are shown one after another. When there is nothing to offer, one card heals the player. Mouse or keys 1-3.
    /// </summary>
    public class LevelUpScreen : MonoBehaviour
    {
        private static readonly string[] TierNames = { "Base", "Upgrade I", "Upgrade II", "Evolution" };

        [SerializeField]
        private Experience experience;

        [SerializeField]
        private SkillSlots skillSlots;

        [SerializeField]
        private Health playerHealth;

        [Tooltip("Object shown while choosing (dark background, header, cards).")]
        [SerializeField]
        private GameObject root;

        [SerializeField]
        private LevelUpCard[] cards = new LevelUpCard[3];

        [Tooltip("One action per card (Card1, Card2, Card3).")]
        [SerializeField]
        private InputActionReference[] cardActions = new InputActionReference[3];

        [Tooltip("Cards do not take a choice for this long after they appear, so a click from the fight does not pick one.")]
        [SerializeField]
        [Min(0f)]
        private float inputGrace = 0.4f;

        [SerializeField]
        [Min(1)]
        private int healCardAmount = 20;

        private readonly List<SkillDefinition> offer = new List<SkillDefinition>();

        [Header("States")]
        [SerializeField]
        private int pendingLevelUps;

        [SerializeField]
        private bool isOpen;

        private bool isHealCard;
        private float acceptAfter;

        public bool IsOpen => isOpen;

        private void Reset()
        {
            experience = FindAnyObjectByType<Experience>();
            skillSlots = FindAnyObjectByType<SkillSlots>();
            if (experience)
                playerHealth = experience.GetComponent<Health>();
        }

        private void Awake()
        {
            if (root)
                root.SetActive(false);
        }

        private void OnEnable()
        {
            experience.OnLevelUp += Experience_OnLevelUp;
            experience.OnProgressReset += Experience_OnProgressReset;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i])
                    cards[i].OnChosen += Card_OnChosen;

            for (int i = 0; i < cardActions.Length; i++)
                if (cardActions[i])
                    cardActions[i].action.performed += CardAction_Performed;
        }

        private void Experience_OnLevelUp(int level)
        {
            if (playerHealth && playerHealth.IsDead)
                return;

            pendingLevelUps++;
            if (isOpen == false)
                Open();
        }

        private void Experience_OnProgressReset()
        {
            pendingLevelUps = 0;
            if (isOpen)
                Close();
        }

        private void Card_OnChosen(LevelUpCard card)
        {
            Choose(System.Array.IndexOf(cards, card));
        }

        private void CardAction_Performed(InputAction.CallbackContext context)
        {
            for (int i = 0; i < cardActions.Length; i++)
            {
                if (cardActions[i] && cardActions[i].action == context.action)
                {
                    Choose(i);
                    return;
                }
            }
        }

        private void Update()
        {
            // Another pause (the menu) may have resumed the time: the cards keep the game stopped.
            if (isOpen && Time.timeScale > 0f)
                Time.timeScale = 0f;

            if (isOpen && Time.unscaledTime >= acceptAfter)
                SetCardsInteractable(true);
        }

        private void Open()
        {
            isOpen = true;
            Time.timeScale = 0f;
            if (root)
                root.SetActive(true);

            ShowOffer();
        }

        private void Close()
        {
            isOpen = false;
            Time.timeScale = 1f;
            if (root)
                root.SetActive(false);
        }

        private void ShowOffer()
        {
            offer.Clear();
            var offerable = new List<SkillDefinition>();
            skillSlots.GetOfferableSkills(offerable);
            for (int i = offerable.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (offerable[i], offerable[j]) = (offerable[j], offerable[i]);
            }

            for (int i = 0; i < offerable.Count && offer.Count < cards.Length; i++)
                offer.Add(offerable[i]);

            isHealCard = offer.Count == 0;
            for (int i = 0; i < cards.Length; i++)
                ShowCard(i);

            acceptAfter = Time.unscaledTime + inputGrace;
            SetCardsInteractable(false);
        }

        private void ShowCard(int index)
        {
            var card = cards[index];
            if (card == null)
                return;

            string key = (index + 1).ToString();
            if (isHealCard && index == 0)
            {
                card.Show(key, $"+{healCardAmount} HP", $"Heals {healCardAmount} hit points. There is no skill left to choose.", null, 0f);
                return;
            }

            if (isHealCard || index >= offer.Count)
            {
                card.Hide();
                return;
            }

            var skill = offer[index];
            int tier = Mathf.Min(skillSlots.TierOf(skill) + 1, SkillDefinition.MaxTier);
            card.Show(key, $"{skill.DisplayName} · {TierNames[tier]}", GetTierText(skill, tier), skill.Icon, 0.08f * index);
        }

        /// <summary>Description of the tier and its numbers; for an upgrade "before → after".</summary>
        private static string GetTierText(SkillDefinition skill, int tier)
        {
            var next = skill.GetTier(tier);
            var previous = tier > 0 ? skill.GetTier(tier - 1) : null;
            string cooldown = Compare(previous != null ? previous.Cooldown : next.Cooldown, next.Cooldown, "0.##");
            string stats = $"Cooldown {cooldown} s";
            if (next.Damage > 0)
                stats += $"  ·  Damage {Compare(previous != null ? previous.Damage : next.Damage, next.Damage, "0")}";

            return $"{next.Description}\n\n<size=80%><color=#FFD966>{stats}</color></size>";
        }

        private static string Compare(float before, float after, string format)
        {
            return Mathf.Approximately(before, after) ? after.ToString(format) : $"{before.ToString(format)} → {after.ToString(format)}";
        }

        private void Choose(int index)
        {
            if (isOpen == false || index < 0 || Time.unscaledTime < acceptAfter)
                return;

            if (isHealCard)
            {
                if (index != 0)
                    return;

                playerHealth.Heal(healCardAmount);
            }

            if (isHealCard == false)
            {
                if (index >= offer.Count)
                    return;

                skillSlots.Grant(offer[index]);
            }

            pendingLevelUps--;
            if (pendingLevelUps > 0)
            {
                ShowOffer();
                return;
            }

            Close();
        }

        private void SetCardsInteractable(bool isInteractable)
        {
            for (int i = 0; i < cards.Length; i++)
                if (cards[i])
                    cards[i].IsInteractable = isInteractable;
        }

        private void OnDisable()
        {
            experience.OnLevelUp -= Experience_OnLevelUp;
            experience.OnProgressReset -= Experience_OnProgressReset;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i])
                    cards[i].OnChosen -= Card_OnChosen;

            for (int i = 0; i < cardActions.Length; i++)
                if (cardActions[i])
                    cardActions[i].action.performed -= CardAction_Performed;

            if (isOpen)
                Close();
        }
    }
}
