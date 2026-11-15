using UnityEditor;
using UnityEngine;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Mouse aiming (PlayerAim) was taken out for now, but its auto setup had already added the component to the player prefab,
    /// which then shows a "Missing Script". This removes components with missing scripts from the player prefab, once per
    /// editor session, only when there are any.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerMissingScriptCleanup
    {
        private const string SessionKey = "WSFGameJam2026.PlayerMissingScriptCleanup.Done";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        static PlayerMissingScriptCleanup()
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
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int removed = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

            if (removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log($"Player cleanup: removed {removed} component(s) with a missing script (mouse aiming taken out) from the player prefab.");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
