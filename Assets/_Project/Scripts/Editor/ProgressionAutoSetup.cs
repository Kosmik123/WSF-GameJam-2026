using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs <see cref="ProgressionSetupMenu"/> once, by itself, after the scripts are compiled: when the open scene has a player
    /// with combat but no <see cref="Experience"/> yet. Then it saves the scene.
    /// </summary>
    [InitializeOnLoad]
    public static class ProgressionAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.ProgressionAutoSetup.Done";

        static ProgressionAutoSetup()
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
            if (playerCombat.GetComponent<Experience>() || TMP_Settings.instance == null)
                return;

            Debug.Log("Progression auto setup: adding XP, levels, XP orbs, the XP bar and the level-up cards.");
            ProgressionSetupMenu.Run(playerCombat);
            EditorSceneManager.SaveScene(playerCombat.gameObject.scene);
        }
    }
}
