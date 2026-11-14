using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Head;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the weak point of the snake to the open scene: an object with TorusObject and WeakPoint and a placeholder sphere
    /// as its model, connected to the head, the lap tracker and the player. Safe to run again: it connects the existing object.
    /// Replace the "Model" child with the real model.
    /// </summary>
    public static class WeakPointSetupMenu
    {
        [MenuItem("WSF GameJam/Setup/Create Weak Point")]
        private static void CreateWeakPoint()
        {
            var head = Object.FindAnyObjectByType<HeadMovement>();
            var lapTracker = Object.FindAnyObjectByType<LapTracker>();
            var world = Object.FindAnyObjectByType<TorusWorld>();
            var movement = Object.FindAnyObjectByType<TorusPlayerMovement>();
            if (head == null || lapTracker == null || world == null || movement == null)
            {
                EditorUtility.DisplayDialog("Weak point setup", "The scene needs the world, the head, the player and the round loop (WSF GameJam/Setup/Create Round Loop).", "OK");
                return;
            }

            var weakPoint = Object.FindAnyObjectByType<WeakPoint>();
            var weakPointObject = weakPoint ? weakPoint.gameObject : CreateWeakPointObject();
            weakPoint = GetOrAdd<WeakPoint>(weakPointObject);

            Connect(weakPointObject.GetComponent<TorusObject>(), "world", world);
            Connect(weakPoint, "head", head);
            Connect(weakPoint, "lapTracker", lapTracker);
            Connect(weakPoint, "player", movement.GetComponent<TorusObject>());

            EditorSceneManager.MarkSceneDirty(weakPointObject.scene);
            Selection.activeObject = weakPointObject;
            Debug.Log("Weak point setup done: replace the Model child with the real model.");
        }

        private static GameObject CreateWeakPointObject()
        {
            var weakPointObject = new GameObject("Weak Point");
            Undo.RegisterCreatedObjectUndo(weakPointObject, "Create Weak Point");
            Undo.AddComponent<TorusObject>(weakPointObject);

            var model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.name = "Model";
            Object.DestroyImmediate(model.GetComponent<Collider>());
            model.transform.SetParent(weakPointObject.transform, false);
            model.transform.localScale = Vector3.one * 1.5f;
            return weakPointObject;
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
