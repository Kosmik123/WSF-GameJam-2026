using TMPro;
using UnityEngine;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>Shows the number of the current round, for example "Round 3".</summary>
    public class RoundLabel : MonoBehaviour
    {
        [SerializeField]
        private RoundState roundState;

        [SerializeField]
        private TMP_Text label;

        [Tooltip("{0} is replaced with the round number.")]
        [SerializeField]
        private string format = "Round {0}";

        private void Reset()
        {
            roundState = FindAnyObjectByType<RoundState>();
            label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (roundState == null)
                return;

            roundState.OnRoundChanged += RoundState_OnRoundChanged;
            Show(roundState.Round);
        }

        private void OnDisable()
        {
            if (roundState == null)
                return;

            roundState.OnRoundChanged -= RoundState_OnRoundChanged;
        }

        private void RoundState_OnRoundChanged(int round)
        {
            Show(round);
        }

        private void Show(int round)
        {
            if (label == null)
                return;

            label.text = string.Format(format, round);
        }
    }
}
