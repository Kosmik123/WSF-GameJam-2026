using UnityEditor;
using UnityEngine;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs <see cref="HeroAnimationSetup"/> once, by itself, after the scripts are compiled, when the hero controller does not
    /// exist yet or the player does not use it.
    /// </summary>
    [InitializeOnLoad]
    public static class HeroAnimationAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.HeroAnimationAutoSetup.Done";

        static HeroAnimationAutoSetup()
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

            SessionState.SetBool(SessionKey, true);
            if (HeroAnimationSetup.IsNeeded() == false)
                return;

            Debug.Log("Hero animation auto setup: building Hero.controller and giving every skill its animation.");
            HeroAnimationSetup.Apply();
        }
    }
}
