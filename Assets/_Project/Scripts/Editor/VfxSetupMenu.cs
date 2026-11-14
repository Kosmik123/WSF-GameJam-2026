using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Player;
using WSFGameJam2026.Vfx;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Sets up the stylized effects: a <see cref="ProjectileVisual"/> on the projectile prefabs of the skills (crystal bolts,
    /// fire core) and the <see cref="CombatVfx"/> object in the scene, connected to the player. Safe to run again.
    /// </summary>
    public static class VfxSetupMenu
    {
        private const string PrefabsFolder = "Assets/_Project/Prefabs/Skills";

        [MenuItem("WSF GameJam/Setup/Add Combat Vfx")]
        private static void AddCombatVfx()
        {
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat == null)
            {
                EditorUtility.DisplayDialog("Vfx setup", "The scene needs a player with combat (WSF GameJam/Setup/Create Combat And Enemies).", "OK");
                return;
            }

            AddProjectileVisuals();
            EnsureCombatVfx(playerCombat);
            EditorSceneManager.MarkSceneDirty(playerCombat.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Vfx setup done: projectile visuals and combat effects.");
        }

        /// <summary>Adds the look to the projectile prefabs that exist and do not have it yet.</summary>
        internal static void AddProjectileVisuals()
        {
            AddProjectileVisual("Mage Bolt", new Color(0.45f, 0.65f, 1f), false, 0.1f);
            AddProjectileVisual("Fireball", new Color(1f, 0.45f, 0.12f), true, 0.12f);
            AddProjectileVisual("Arcane Shot", new Color(0.7f, 0.4f, 1f), false, 0.08f);
        }

        /// <summary>The <see cref="CombatVfx"/> of the scene, created and connected to the player when missing.</summary>
        internal static CombatVfx EnsureCombatVfx(PlayerCombat playerCombat)
        {
            var combatVfx = Object.FindAnyObjectByType<CombatVfx>();
            if (combatVfx == null)
            {
                var vfxObject = new GameObject("Combat Vfx");
                MoveToSceneOf(vfxObject, playerCombat);
                Undo.RegisterCreatedObjectUndo(vfxObject, "Create Combat Vfx");
                combatVfx = vfxObject.AddComponent<CombatVfx>();
            }

            PlayerSetupMenu.SetReference(combatVfx, "playerCombat", playerCombat);
            return combatVfx;
        }

        private static void MoveToSceneOf(GameObject gameObject, PlayerCombat playerCombat)
        {
            if (gameObject.scene != playerCombat.gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, playerCombat.gameObject.scene);
        }

        private static void AddProjectileVisual(string prefabName, Color color, bool isFiery, float trailSize)
        {
            string path = $"{PrefabsFolder}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponent<ProjectileVisual>() == null)
            {
                var visual = root.AddComponent<ProjectileVisual>();
                var serialized = new SerializedObject(visual);
                serialized.FindProperty("color").colorValue = color;
                serialized.FindProperty("isFiery").boolValue = isFiery;
                serialized.FindProperty("trailSize").floatValue = trailSize;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
