using UnityEngine;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Hud
{
    /// <summary>
    /// The round number as a row of dots, the way the Aztecs wrote numbers: round 3 lights three dots.
    /// Rounds above the number of dots light all of them (the digit next to them still says the exact number).
    /// </summary>
    public class RoundDots : MonoBehaviour
    {
        [SerializeField]
        private RoundState roundState;

        [Tooltip("The dots in order. Only the first 'round' of them are shown.")]
        [SerializeField]
        private GameObject[] dots = new GameObject[0];

        private void Reset()
        {
            roundState = FindAnyObjectByType<RoundState>();
        }

        private void OnEnable()
        {
            if (roundState == null)
                return;

            roundState.OnRoundChanged += RoundState_OnRoundChanged;
            Show(roundState.Round);
        }

        private void RoundState_OnRoundChanged(int round)
        {
            Show(round);
        }

        private void Show(int round)
        {
            for (int i = 0; i < dots.Length; i++)
                if (dots[i])
                    dots[i].SetActive(i < round);
        }

        private void OnDisable()
        {
            if (roundState == null)
                return;

            roundState.OnRoundChanged -= RoundState_OnRoundChanged;
        }
    }
}
