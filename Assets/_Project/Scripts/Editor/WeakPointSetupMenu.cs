using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Head;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the weak point of the snake to the open scene: an object with TorusObject, Health (team of the enemies, immune to the jaw),
    /// BodyFlash (red) and WeakPoint, with a placeholder sphere as its model, connected to the head. The round controller
    /// is connected to it too. Safe to run again: it adds what is missing and connects the existing object.
    /// Replace the "Model" child with the real model.
    /// </summary>
    public static class WeakPointSetupMenu
    {
        private const int WeakPointHealth = 60;

        [MenuItem("WSF GameJam/Setup/Create Weak Point")]
        private static void CreateWeakPoint()
        {
            var head = Object.FindAnyObjectByType<HeadMovement>();
            var world = Object.FindAnyObjectByType<TorusWorld>();
            if (head == null || world == null)
            {
                EditorUtility.DisplayDialog("Weak point setup", "The scene needs the world and the head.", "OK");
                return;
            }

            var weakPoint = Object.FindAnyObjectByType<WeakPoint>();
            var weakPointObject = weakPoint ? weakPoint.gameObject : CreateWeakPointObject();
            weakPoint = GetOrAdd<WeakPoint>(weakPointObject);
            var health = GetOrAdd<Health>(weakPointObject);
            var flash = GetOrAdd<BodyFlash>(weakPointObject);

            Connect(weakPointObject.GetComponent<TorusObject>(), "world", world);
            Connect(weakPoint, "head", head);

            // The attacks of the player hit it (enemy team), the jaw ignores it.
            var healthSerialized = new SerializedObject(health);
            healthSerialized.FindProperty("team").enumValueIndex = (int)CombatTeam.Enemy;
            healthSerialized.FindProperty("maxHealth").intValue = WeakPointHealth;
            healthSerialized.FindProperty("mawDamageMode").enumValueIndex = (int)MawDamageMode.Immune;
            healthSerialized.FindProperty("bodyRadius").floatValue = 1.5f;
            healthSerialized.ApplyModifiedPropertiesWithoutUndo();

            Connect(flash, "health", health);
            var flashSerialized = new SerializedObject(flash);
            flashSerialized.FindProperty("damageFlashColor").colorValue = Color.red;
            flashSerialized.FindProperty("damageFlashDuration").floatValue = 0.15f;
            flashSerialized.ApplyModifiedPropertiesWithoutUndo();

            var model = weakPointObject.transform.Find("Model");
            if (model)
                Connect(weakPoint, "visual", model);

            var roundController = Object.FindAnyObjectByType<RoundController>();
            if (roundController)
                Connect(roundController, "weakPoint", weakPoint);

            EditorSceneManager.MarkSceneDirty(weakPointObject.scene);
            Selection.activeObject = weakPointObject;
            Debug.Log("Weak point setup done: destroying it wins the round. Replace the Model child with the real model.");
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
