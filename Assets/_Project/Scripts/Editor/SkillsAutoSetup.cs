using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs the skills, mage and LD57 models setup once, by itself, after the scripts are compiled: when the open scene has
    /// a player with combat that has no class yet. Then it saves the scene. When the player already has a class it does nothing,
    /// so after the first run (and a commit of the scene) it never runs again.
    /// </summary>
    [InitializeOnLoad]
    public static class SkillsAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.SkillsAutoSetup.Done";

        static SkillsAutoSetup()
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
            if (playerCombat.TryGetComponent<PlayerClassApplier>(out var applier) && new SerializedObject(applier).FindProperty("playerClass").objectReferenceValue)
                return;

            Debug.Log("Skills auto setup: the player has no class yet, setting up the mage, skills and LD57 models.");
            SkillsSetupMenu.Run(playerCombat, true);
            EditorSceneManager.SaveScene(playerCombat.gameObject.scene);
        }
    }
}
