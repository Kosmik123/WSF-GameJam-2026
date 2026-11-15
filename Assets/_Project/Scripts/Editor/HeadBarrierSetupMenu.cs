using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Head;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the head barrier to the open scene: <see cref="HeadBarrier"/> on the head, and the player gets "Is Blocked By Head Barrier".
    /// Safe to run again.
    /// </summary>
    public static class HeadBarrierSetupMenu
    {
        [MenuItem("WSF GameJam/Setup/Create Head Barrier")]
        private static void CreateHeadBarrier()
        {
            var head = Object.FindAnyObjectByType<HeadMovement>();
            var world = Object.FindAnyObjectByType<TorusWorld>();
            var player = Object.FindAnyObjectByType<TorusPlayerMovement>();
            if (head == null || world == null || player == null)
            {
                EditorUtility.DisplayDialog("Head barrier setup", "The scene needs the world, the head and the player.", "OK");
                return;
            }

            if (head.TryGetComponent<HeadBarrier>(out var barrier) == false)
                barrier = Undo.AddComponent<HeadBarrier>(head.gameObject);

            var barrierSerialized = new SerializedObject(barrier);
            barrierSerialized.FindProperty("world").objectReferenceValue = world;
            barrierSerialized.ApplyModifiedPropertiesWithoutUndo();

            var playerObject = player.GetComponent<TorusObject>();
            var playerSerialized = new SerializedObject(playerObject);
            playerSerialized.FindProperty("isBlockedByHeadBarrier").boolValue = true;
            playerSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(head.gameObject.scene);
            Selection.activeObject = head.gameObject;
            Debug.Log("Head barrier setup done: the player can not cross the theta of the head. Save the scene to keep it.");
        }
    }
}
