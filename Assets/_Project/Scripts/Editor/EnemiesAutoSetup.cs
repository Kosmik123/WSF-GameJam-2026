using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Enemies;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Runs <see cref="EnemiesSetupMenu"/> once, by itself, after the scripts are compiled: when the open scene has an
    /// <see cref="EnemySpawner"/> and the golem prefab does not exist yet. Then it saves the scene.
    /// </summary>
    [InitializeOnLoad]
    public static class EnemiesAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.EnemiesAutoSetup.Done";
        private const string GolemPath = "Assets/_Project/Prefabs/Enemies/Golem.prefab";

        static EnemiesAutoSetup()
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

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null)
                return;

            SessionState.SetBool(SessionKey, true);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GolemPath))
                return;

            Debug.Log("Enemies auto setup: creating the golem, bat and shaman.");
            EnemiesSetupMenu.CreateEnemies();
            EditorSceneManager.SaveScene(spawner.gameObject.scene);
        }
    }
}
