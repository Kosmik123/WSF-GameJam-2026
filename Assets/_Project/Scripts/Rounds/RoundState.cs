using System;
using UnityEngine;

namespace WSFGameJam2026.Rounds
{
    /// <summary>
    /// Which round of the run is being played (GDD 6). A round is one lap of the torus. Winning one starts the next,
    /// and a death ends the run: the record is the number of completed rounds. This holds only the number,
    /// the rest of the round loop (lap detection, restart) is meant to use it.
    /// </summary>
    public class RoundState : MonoBehaviour
    {
        /// <summary>The number of the current round (1, 2, 3...) after it changed.</summary>
        public event Action<int> OnRoundChanged;

        [SerializeField]
        [Min(1)]
        private int firstRound = 1;

        // 0 until the first change, so the value does not depend on the order of Awake calls of other components.
        private int round;

        /// <summary>The number of the current round. The first round is 1.</summary>
        public int Round => Mathf.Max(round, firstRound);

        /// <summary>Rounds finished in this run: the record of the player when the run ends.</summary>
        public int CompletedRounds => Round - firstRound;

        /// <summary>Starts the next round after a lap was completed.</summary>
        public void StartNextRound()
        {
            round = Round + 1;
            OnRoundChanged?.Invoke(round);
        }

        /// <summary>Back to the first round: a new run after death.</summary>
        public void ResetRounds()
        {
            round = firstRound;
            OnRoundChanged?.Invoke(round);
        }
    }
}
