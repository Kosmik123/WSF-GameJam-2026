using System;
using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.Leaderboard
{
    /// <summary>Where the rows of a <see cref="LeaderboardFetchResult"/> came from.</summary>
    public enum LeaderboardSource
    {
        None,
        Server,
        Cache,
    }

    /// <summary>One place of the leaderboard.</summary>
    [Serializable]
    public struct LeaderboardRow
    {
        [SerializeField]
        private int rank;
        public int Rank => rank;

        [SerializeField]
        private string playerName;
        public string PlayerName => playerName;

        [SerializeField]
        private int score;
        public int Score => score;

        [SerializeField]
        private bool isPlayer;
        public bool IsPlayer => isPlayer;

        public LeaderboardRow(int rank, string playerName, int score, bool isPlayer)
        {
            this.rank = rank;
            this.playerName = playerName;
            this.score = score;
            this.isPlayer = isPlayer;
        }
    }

    /// <summary>The rows shown by the leaderboard and where they came from. No rows and <see cref="LeaderboardSource.None"/> when there is nothing to show.</summary>
    public readonly struct LeaderboardFetchResult
    {
        public LeaderboardSource Source { get; }
        public IReadOnlyList<LeaderboardRow> Rows { get; }

        public LeaderboardFetchResult(LeaderboardSource source, IReadOnlyList<LeaderboardRow> rows)
        {
            Source = source;
            Rows = rows;
        }
    }
}
