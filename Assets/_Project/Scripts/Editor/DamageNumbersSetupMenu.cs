using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Vfx;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the damage numbers to the open scene: one "Damage Numbers" object with <see cref="DamageNumbers"/>.
    /// Safe to run again: it only adds what is missing.
    /// </summary>
    public static class DamageNumbersSetupMenu
    {
        [MenuItem("WSF GameJam/Setup/Create Damage Numbers")]
        private static void CreateDamageNumbers()
        {
            var existing = Object.FindAnyObjectByType<DamageNumbers>();
            if (existing)
            {
                Selection.activeObject = existing.gameObject;
                Debug.Log("Damage numbers are already in the scene.");
                return;
            }

            var numbersObject = new GameObject("Damage Numbers");
            Undo.RegisterCreatedObjectUndo(numbersObject, "Create Damage Numbers");
            Undo.AddComponent<DamageNumbers>(numbersObject);

            EditorSceneManager.MarkSceneDirty(numbersObject.scene);
            Selection.activeObject = numbersObject;
            Debug.Log("Damage numbers added to the scene. Save the scene to keep them.");
        }
    }
}
