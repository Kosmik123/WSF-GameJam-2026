using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// The level system (<see cref="PlayerLevel"/>, the XP bar and the level-up cards) is put into the scene by the menu
    /// WSF GameJam/Setup/Create Aztec UI. This runs that menu once, by itself, after the scripts are compiled, when the open
    /// scene has the player, the health bar and the round loop but no <see cref="PlayerLevel"/> yet. Then it saves the scene.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelingAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.LevelingAutoSetup.Done";
        private const string MenuPath = "WSF GameJam/Setup/Create Aztec UI";

        static LevelingAutoSetup()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (SessionState.GetBool(SessionKey, false) || Application.isBatchMode)
                return;

            // The editor is busy: try again a moment later.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat == null)
                return;

            SessionState.SetBool(SessionKey, true);
            bool hasLevels = Object.FindAnyObjectByType<PlayerLevel>(FindObjectsInactive.Include);
            bool canSetUp = Object.FindAnyObjectByType<HealthBar>() && Object.FindAnyObjectByType<RoundController>();
            if (hasLevels || canSetUp == false)
                return;

            Debug.Log("Leveling auto setup: the scene has no PlayerLevel, running " + MenuPath + " (levels, XP bar, level-up cards).");
            if (EditorApplication.ExecuteMenuItem(MenuPath))
                EditorSceneManager.SaveScene(playerCombat.gameObject.scene);
        }
    }
}
