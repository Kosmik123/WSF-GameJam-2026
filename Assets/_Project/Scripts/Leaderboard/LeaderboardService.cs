using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace WSFGameJam2026.Leaderboard
{
    /// <summary>
    /// The connection to the Unity Services Leaderboards: signs the player in anonymously, sends scores and downloads the top list.
    /// Every call is safe without a connection: a failed download falls back to the <see cref="LeaderboardCache"/>, a failed send returns false.
    /// Set the leaderboard id (from the Unity Cloud dashboard) in the Inspector; the project must be linked to Unity Services.
    /// For the best result per player set the update strategy of the leaderboard to "Keep best".
    /// </summary>
    public class LeaderboardService : MonoBehaviour
    {
        private const int MinNameLength = 3;

        [Tooltip("Id of the leaderboard created in the Unity Cloud dashboard (Leaderboards).")]
        [SerializeField]
        private string leaderboardId;

        [Tooltip("How many places are downloaded.")]
        [SerializeField]
        [Min(1)]
        private int topCount = 10;

        private Task signInTask;

        public bool HasLeaderboardId => string.IsNullOrWhiteSpace(leaderboardId) == false;

        /// <summary>Removes spaces and cuts the name to <paramref name="maxLength"/> characters. Unity Authentication does not accept spaces.</summary>
        public static string CleanName(string rawName, int maxLength)
        {
            var name = (rawName ?? string.Empty).Replace(" ", string.Empty).Trim();
            return name.Length > maxLength ? name.Substring(0, maxLength) : name;
        }

        public static bool IsNameValid(string name)
        {
            return string.IsNullOrEmpty(name) == false && name.Length >= MinNameLength;
        }

        /// <summary>Downloads the top list. Returns the cached one when the server can not be reached, or nothing when there is no cache either.</summary>
        public async Task<LeaderboardFetchResult> FetchTopAsync()
        {
            if (HasLeaderboardId == false)
                return LoadCache();

            try
            {
                await EnsureSignedInAsync();
                var page = await LeaderboardsService.Instance.GetScoresAsync(leaderboardId, new GetScoresOptions { Limit = topCount });
                var playerId = AuthenticationService.Instance.PlayerId;
                var rows = new List<LeaderboardRow>();
                foreach (var entry in page.Results)
                    rows.Add(new LeaderboardRow(entry.Rank + 1, RemoveTag(entry.PlayerName), (int)entry.Score, entry.PlayerId == playerId));

                LeaderboardCache.Save(rows);
                return new LeaderboardFetchResult(LeaderboardSource.Server, rows);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Leaderboard: the top list could not be downloaded ({exception.Message}).");
                return LoadCache();
            }
        }

        /// <summary>Sets the name of the player and sends the score. Returns false when it did not work.</summary>
        public async Task<bool> SubmitScoreAsync(string playerName, int score)
        {
            if (HasLeaderboardId == false)
                return false;

            try
            {
                await EnsureSignedInAsync();
                await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Leaderboard: the score could not be sent ({exception.Message}).");
                return false;
            }
        }

        private Task EnsureSignedInAsync()
        {
            signInTask ??= SignInAsync();
            return signInTask;
        }

        private async Task SignInAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                    await UnityServices.InitializeAsync();

                if (AuthenticationService.Instance.IsSignedIn == false)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch
            {
                signInTask = null;
                throw;
            }
        }

        private static LeaderboardFetchResult LoadCache()
        {
            var rows = LeaderboardCache.Load();
            return new LeaderboardFetchResult(rows.Count > 0 ? LeaderboardSource.Cache : LeaderboardSource.None, rows);
        }

        // The server adds "#1234" to every name.
        private static string RemoveTag(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return string.Empty;

            int tag = playerName.LastIndexOf('#');
            return tag > 0 ? playerName.Substring(0, tag) : playerName;
        }
    }
}
