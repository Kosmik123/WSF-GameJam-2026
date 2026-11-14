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
            var world = Object.FindFirstObjectByType<TorusWorld>();
            if (world == null)
            {
                EditorUtility.DisplayDialog("Player setup", "Add a TorusWorld to the scene first.", "OK");
                return;
            }

            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            InputActionReference moveReference = FindActionReference("Player", "Move");

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            Undo.RegisterCreatedObjectUndo(player, "Create Player");

            var movement = player.AddComponent<TorusPlayerMovement>();
            var enabler = player.AddComponent<InputActionMapEnabler>();
            SetReference(movement, "world", world);
            SetReference(movement, "moveAction", moveReference);
            SetReference(enabler, "actions", actionsAsset);

            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Camera");
                camera = cameraObject.GetComponent<Camera>();
            }

            if (camera.TryGetComponent(out TorusFollowCamera followCamera) == false)
                followCamera = Undo.AddComponent<TorusFollowCamera>(camera.gameObject);

            SetReference(followCamera, "target", movement);

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(player.scene);
        }

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(InputAssetPath))
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
