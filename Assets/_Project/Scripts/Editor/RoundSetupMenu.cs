using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Head;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the round loop to the open scene: RoundState, LapTracker and RoundController on a "Rounds" object,
    /// connected to the head, the spawner and the player. Safe to run again: it only adds what is missing and connects it again.
    /// </summary>
    public static class RoundSetupMenu
    {
        [MenuItem("WSF GameJam/Setup/Create Round Loop")]
        private static void CreateRoundLoop()
        {
            var head = Object.FindAnyObjectByType<HeadMovement>();
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            var player = playerCombat != null ? playerCombat.GetComponent<TorusObject>() : null;
            if (head == null || spawner == null || player == null)
            {
                EditorUtility.DisplayDialog("Round loop setup", "The scene needs the head (HeadMovement), the enemy spawner and the player with combat (WSF GameJam/Setup/Create Combat And Enemies).", "OK");
                return;
            }

            var roundState = Object.FindAnyObjectByType<RoundState>();
            var roundsObject = roundState != null ? roundState.gameObject : CreateRoundsObject();
            roundState = GetOrAdd<RoundState>(roundsObject);
            var lapTracker = GetOrAdd<LapTracker>(roundsObject);
            var controller = GetOrAdd<RoundController>(roundsObject);

            Connect(lapTracker, "head", head);
            Connect(lapTracker, "player", player);
            Connect(controller, "roundState", roundState);
            Connect(controller, "lapTracker", lapTracker);
            Connect(controller, "head", head);
            Connect(controller, "spawner", spawner);
            Connect(controller, "playerCombat", playerCombat);

            EditorSceneManager.MarkSceneDirty(roundsObject.scene);
            Selection.activeObject = roundsObject;
            Debug.Log("Round loop setup done: RoundState, LapTracker and RoundController on the Rounds object.");
        }

        private static GameObject CreateRoundsObject()
        {
            var roundsObject = new GameObject("Rounds");
            Undo.RegisterCreatedObjectUndo(roundsObject, "Create Round Loop");
            return roundsObject;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            if (target.TryGetComponent<T>(out var existing))
                return existing;

            return Undo.AddComponent<T>(target);
        }

        private static void Connect(Component component, string propertyName, Object target)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
