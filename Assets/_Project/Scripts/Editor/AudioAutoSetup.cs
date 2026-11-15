using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Audio;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs the audio setup once, by itself, after the scripts are compiled: when the open scene has a player with combat
    /// but no <see cref="SfxHooks"/>. Then it saves the scene. Once the scene has the hooks it never runs again.
    /// </summary>
    [InitializeOnLoad]
    public static class AudioAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.AudioAutoSetup.Done";

        static AudioAutoSetup()
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
            if (Object.FindAnyObjectByType<SfxHooks>())
                return;

            Debug.Log("Audio auto setup: the scene has a player but no SfxHooks, setting up the sound effects.");
            AudioSetupMenu.Run(playerCombat);
            EditorSceneManager.SaveScene(playerCombat.gameObject.scene);
        }
    }
}
