using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Player;
using WSFGameJam2026.Vfx;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs once per editor session, by itself, after the scripts are compiled, when the open scene has a player with combat:
    /// creates the missing warrior skills (and gives them to a Warrior class without skills), adds the look to the projectile
    /// prefabs and, when the scene has no <see cref="CombatVfx"/> yet, creates it and saves the scene. Every step is idempotent,
    /// so after the first run (and a commit) it changes nothing.
    /// </summary>
    [InitializeOnLoad]
    public static class VfxAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.VfxAutoSetup.Done";

        static VfxAutoSetup()
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
            SkillsSetupMenu.SetupWarrior();
            VfxSetupMenu.AddProjectileVisuals();
            AssetDatabase.SaveAssets();

            if (Object.FindAnyObjectByType<CombatVfx>())
                return;

            Debug.Log("Vfx auto setup: the scene has no combat effects yet, adding them.");
            VfxSetupMenu.EnsureCombatVfx(playerCombat);
            EditorSceneManager.SaveScene(playerCombat.gameObject.scene);
        }
    }
}
