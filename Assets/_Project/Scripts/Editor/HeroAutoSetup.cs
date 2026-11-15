using UnityEditor;
using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs <see cref="HeroSetupMenu"/> once, by itself, after the scripts are compiled: when the priest FBX exists and the Mage
    /// class still uses the old RPG Tiny Hero Duo model (or none). A model chosen by hand is never replaced.
    /// </summary>
    [InitializeOnLoad]
    public static class HeroAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.HeroAutoSetup.Done";
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
            var model = mage.ModelPrefab;
            bool usesOldHero = model == null || AssetDatabase.GetAssetPath(model).StartsWith(OldHeroFolder);
            if (usesOldHero == false)
                return;

            Debug.Log("Hero auto setup: replacing the RPG Tiny Hero Duo model with the quetzal priest.");
            HeroSetupMenu.ApplyPriest();
        }
    }
}
