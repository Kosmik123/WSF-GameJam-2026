using System;
using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.Leaderboard
{
    /// <summary>The last top list received from the server, kept in PlayerPrefs to be shown when the server can not be reached.</summary>
    public static class LeaderboardCache
    {
        private const string Key = "WSFGameJam2026.Leaderboard.Cache";

        [Serializable]
        private class Stored
        {
            [SerializeField]
            private List<LeaderboardRow> rows = new List<LeaderboardRow>();
            public List<LeaderboardRow> Rows => rows;
        }

        public static void Save(IReadOnlyList<LeaderboardRow> rows)
        {
            var stored = new Stored();
            stored.Rows.AddRange(rows);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(stored));
            PlayerPrefs.Save();
        }

        /// <summary>The saved rows, or an empty list when nothing was saved.</summary>
        public static IReadOnlyList<LeaderboardRow> Load()
        {
            var json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return Array.Empty<LeaderboardRow>();

            try
            {
                return JsonUtility.FromJson<Stored>(json).Rows;
            }
            catch (ArgumentException)
            {
                return Array.Empty<LeaderboardRow>();
            }
        }
    }
}
