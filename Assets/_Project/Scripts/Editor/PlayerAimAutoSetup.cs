using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds <see cref="PlayerAim"/> (mouse aiming) to the player and wires its input actions (Player/AimPoint and Player/Look).
    /// Runs once per editor session, by itself, after the scripts are compiled: on Player.prefab when its player has no wired
    /// PlayerAim, and on the player in the open scene when it is not an instance of a prefab (an instance gets it from the prefab).
    /// Saves what it changed. Can be run again from WSF GameJam/Setup/Wire Player Aim.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerAimAutoSetup
    {
        private const string SessionKey = "WSFGameJam2026.PlayerAimAutoSetup.Done";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        static PlayerAimAutoSetup()
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
            Run();
        }

        [MenuItem("WSF GameJam/Setup/Wire Player Aim")]
        private static void Run()
        {
            if (PlayerSetupMenu.FindActionReference("Player", "AimPoint") == null)
            {
                Debug.LogWarning("Player aim setup: the input asset has no Player/AimPoint action yet. Reimport InputSystem_Actions and run WSF GameJam/Setup/Wire Player Aim.");
                return;
            }

            SetUpPrefab();
            SetUpScene();
        }

        /// <summary>Adds a PlayerAim to the player (when it has none) and wires its actions.</summary>
        internal static PlayerAim AddAndWire(GameObject player)
        {
            if (player.TryGetComponent<PlayerAim>(out var aim) == false)
                aim = player.AddComponent<PlayerAim>();

            PlayerSetupMenu.SetReference(aim, "pointerAction", PlayerSetupMenu.FindActionReference("Player", "AimPoint"));
            PlayerSetupMenu.SetReference(aim, "stickAction", PlayerSetupMenu.FindActionReference("Player", "Look"));
            return aim;
        }

        private static void SetUpPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
                return;

            var prefabMovement = prefab.GetComponentInChildren<TorusPlayerMovement>(true);
            if (prefabMovement == null || IsSetUp(prefabMovement))
                return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                AddAndWire(root.GetComponentInChildren<TorusPlayerMovement>(true).gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("Player aim setup: added PlayerAim (mouse aiming) to " + PlayerPrefabPath + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetUpScene()
        {
            var movement = Object.FindAnyObjectByType<TorusPlayerMovement>(FindObjectsInactive.Include);
            if (movement == null || IsSetUp(movement) || PrefabUtility.IsPartOfPrefabInstance(movement))
                return;

            AddAndWire(movement.gameObject);
            EditorSceneManager.SaveScene(movement.gameObject.scene);
            Debug.Log("Player aim setup: added PlayerAim (mouse aiming) to the player in the scene " + movement.gameObject.scene.name + ".");
        }

        private static bool IsSetUp(TorusPlayerMovement movement)
        {
            return movement.TryGetComponent<PlayerAim>(out var aim) && new SerializedObject(aim).FindProperty("pointerAction").objectReferenceValue;
        }
    }
}
