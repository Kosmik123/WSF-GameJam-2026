using System;
using UnityEngine;

namespace WSFGameJam2026.Leaderboard
{
    /// <summary>
    /// Generic collector of the points of one run. Other systems add points with <see cref="Add"/> (or set the total with
    /// <see cref="Set"/>); the leaderboard screen sends <see cref="Score"/> to the server when the player confirms his name.
    /// </summary>
    public class ScoreCollector : MonoBehaviour
    {
        /// <summary>The total changed; the argument is the new total.</summary>
        public event Action<int> OnScoreChanged;

        /// <summary>The score of the run was sent to the leaderboard.</summary>
        public event Action OnSubmitted;

        [Header("States")]
        [SerializeField]
        private int score;
        public int Score => score;

        [SerializeField]
        private bool isSubmitted;
        public bool IsSubmitted => isSubmitted;

        /// <summary>True when there is a score of the run that was not sent yet.</summary>
        public bool CanSubmit => score > 0 && isSubmitted == false;

        /// <summary>Adds points (a negative amount takes them away; the total never goes below zero).</summary>
        public void Add(int points)
        {
            Set(score + points);
        }

        public void Set(int total)
        {
            total = Mathf.Max(0, total);
            if (total == score)
                return;

            score = total;
            isSubmitted = false;
            OnScoreChanged?.Invoke(score);
        }

        /// <summary>Starts a new run: zero points, nothing waits to be sent.</summary>
        public void ResetScore()
        {
            score = 0;
            isSubmitted = false;
            OnScoreChanged?.Invoke(score);
        }

        /// <summary>Called by the leaderboard screen after the score was sent, so it is not sent twice.</summary>
        public void MarkSubmitted()
        {
            isSubmitted = true;
            OnSubmitted?.Invoke();
        }
    }
}
