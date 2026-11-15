using UnityEditor;
using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs <see cref="HeroSetupMenu"/> once, by itself, after the scripts are compiled: when the priest FBX exists and the Mage
    /// class still uses the old RPG Tiny Hero Duo model (or none), or the player prefab has no hero to show in the editor.
    /// A model chosen by hand is never replaced.
    /// </summary>
    [InitializeOnLoad]
    public static class HeroAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.HeroAutoSetup.Done.v3";
        private const string MageClassPath = "Assets/_Project/Data/Classes/Mage.asset";
        private const string OldHeroFolder = "Assets/RPG Tiny Hero Duo/";

        static HeroAutoSetup()
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

            var mage = AssetDatabase.LoadAssetAtPath<PlayerClass>(MageClassPath);
            if (mage == null)
                return;

            SessionState.SetBool(SessionKey, true);

            // Always checked: the main menu must not turn the placeholder capsule back on over the hero.
            HeroSetupMenu.FixMainMenuHiddenObjects();

            var model = mage.ModelPrefab;
            bool usesOldHero = model == null || AssetDatabase.GetAssetPath(model).StartsWith(OldHeroFolder);
            bool usesPriest = HeroSetupMenu.IsPriestPrefab(model);
            if (usesOldHero == false && (usesPriest == false || HeroSetupMenu.NeedsPreview() == false))
                return;

            Debug.Log("Hero auto setup: setting the quetzal priest as the hero and placing it in the player prefab.");
            HeroSetupMenu.ApplyPriest();
        }
    }
}
