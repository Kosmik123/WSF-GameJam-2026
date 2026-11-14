using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.Cameras;
using WSFGameJam2026.Controls;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    public static class PlayerSetupMenu
    {
        private const string InputAssetPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";

        [MenuItem("WSF GameJam/Setup/Create Player And Camera")]
        private static void CreatePlayerAndCamera()
        {
            var world = Object.FindAnyObjectByType<TorusWorld>();
            if (world == null)
            {
                EditorUtility.DisplayDialog("Player setup", "Add a TorusWorld to the scene first.", "OK");
                return;
            }

            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            var moveReference = FindActionReference("Player", "Move");

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            Undo.RegisterCreatedObjectUndo(player, "Create Player");

            // TorusPlayerMovement requires TorusObject, so Unity adds it automatically.
            var movement = player.AddComponent<TorusPlayerMovement>();
            var torusObject = player.GetComponent<TorusObject>();
            var enabler = player.AddComponent<InputActionMapEnabler>();
            SetReference(torusObject, "world", world);
            SetReference(movement, "moveAction", moveReference);
            SetReference(enabler, "actions", actionsAsset);

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Camera");
                camera = cameraObject.GetComponent<Camera>();
            }

            if (camera.TryGetComponent<TorusFollowCamera>(out var followCamera) == false)
                followCamera = Undo.AddComponent<TorusFollowCamera>(camera.gameObject);

            SetReference(followCamera, "target", torusObject);

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(player.scene);
        }

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InputAssetPath))
                if (asset is InputActionReference reference && reference.action != null)
                    if (reference.action.actionMap.name == mapName && reference.action.name == actionName)
                        return reference;

            return null;
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
