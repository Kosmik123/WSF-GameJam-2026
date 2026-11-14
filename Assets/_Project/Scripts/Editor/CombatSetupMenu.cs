using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WSFGameJam2026.Cameras;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Head;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Sets up stage 2 in the open scene: combat components on the player prefab, placeholder enemy prefabs
    /// (runner, shooter, projectile) and a "Combat" object with the hit stop, hit feedback and the spawner.
    /// Safe to run again: existing components and prefabs are reused, not duplicated.
    /// </summary>
    public static class CombatSetupMenu
    {
        private const string EnemiesFolder = "Assets/_Project/Prefabs/Enemies";
        private const string MaterialsFolder = "Assets/_Project/Materials/Enemies";

        [MenuItem("WSF GameJam/Setup/Create Combat And Enemies")]
        private static void CreateCombatAndEnemies()
        {
            var world = Object.FindAnyObjectByType<TorusWorld>();
            var playerMovement = Object.FindAnyObjectByType<TorusPlayerMovement>();
            if (world == null || playerMovement == null)
            {
                EditorUtility.DisplayDialog("Combat setup", "The scene needs a TorusWorld and a player (WSF GameJam/Setup/Create Player And Camera).", "OK");
                return;
            }

            SetupPlayer(playerMovement.gameObject);
            var playerHealth = playerMovement.GetComponent<Health>();

            EnsureFolder(EnemiesFolder);
            EnsureFolder(MaterialsFolder);
            var projectile = CreateProjectilePrefab();
            var runner = CreateRunnerPrefab();
            var shooter = CreateShooterPrefab(projectile);

            SetupSceneObject(world, playerHealth, runner, shooter);

            EditorSceneManager.MarkSceneDirty(playerMovement.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Combat setup done: player combat, Runner and Shooter prefabs, Combat object with the spawner.");
        }

        private static void SetupPlayer(GameObject player)
        {
            // Changes go to the prefab, when the player is an instance, so every scene gets them.
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player);
            if (string.IsNullOrEmpty(prefabPath))
            {
                ConfigurePlayer(player);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            ConfigurePlayer(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ConfigurePlayer(GameObject player)
        {
            var health = GetOrAdd<Health>(player);
            SetValues(health, serialized =>
            {
                serialized.FindProperty("team").enumValueIndex = (int)CombatTeam.Player;
                serialized.FindProperty("maxHealth").intValue = 100;
                serialized.FindProperty("bodyRadius").floatValue = 0.5f;
            });

            GetOrAdd<Knockback>(player);

            var flash = GetOrAdd<BodyFlash>(player);
            PlayerSetupMenu.SetReference(flash, "health", health);

            var combat = GetOrAdd<PlayerCombat>(player);
            PlayerSetupMenu.SetReference(combat, "attackAction", PlayerSetupMenu.FindActionReference("Player", "Attack"));
            PlayerSetupMenu.SetReference(combat, "dodgeAction", PlayerSetupMenu.FindActionReference("Player", "Jump"));
        }

        private static EnemyProjectile CreateProjectilePrefab()
        {
            var root = CreatePrimitive("Enemy Projectile", PrimitiveType.Sphere, new Vector3(0.4f, 0.4f, 0.4f), GetMaterial("Projectile", new Color(1f, 0.85f, 0.15f), true));
            var torusObject = root.AddComponent<TorusObject>();
            SetValues(torusObject, serialized => serialized.FindProperty("groundOffset").floatValue = 0.8f);
            root.AddComponent<EnemyProjectile>();
            return SavePrefab(root, "Enemy Projectile").GetComponent<EnemyProjectile>();
        }

        private static Enemy CreateRunnerPrefab()
        {
            var root = new GameObject("Runner");
            CreateBody(root, PrimitiveType.Capsule, new Vector3(0.8f, 0.6f, 0.8f), GetMaterial("Runner", new Color(0.85f, 0.2f, 0.2f), false));
            CreateNose(root, 0.6f);
            AddEnemyComponents(root, groundOffset: 0.6f, maxHealth: 20, bodyRadius: 0.45f, experience: 10, knockbackMultiplier: 1f);
            root.AddComponent<RunnerEnemy>();
            return SavePrefab(root, "Runner").GetComponent<Enemy>();
        }

        private static Enemy CreateShooterPrefab(EnemyProjectile projectile)
        {
            var root = new GameObject("Shooter");
            CreateBody(root, PrimitiveType.Cylinder, new Vector3(0.9f, 0.8f, 0.9f), GetMaterial("Shooter", new Color(0.3f, 0.45f, 0.95f), false));
            CreateNose(root, 0.8f);
            AddEnemyComponents(root, groundOffset: 0.8f, maxHealth: 25, bodyRadius: 0.5f, experience: 20, knockbackMultiplier: 1f);

            var aimLineObject = new GameObject("Aim Line");
            aimLineObject.transform.SetParent(root.transform, false);
            var aimLine = aimLineObject.AddComponent<LineRenderer>();
            aimLine.sharedMaterial = GetMaterial("Aim Line", new Color(1f, 0.85f, 0.15f, 0.8f), true);
            aimLine.widthMultiplier = 0.08f;
            aimLine.shadowCastingMode = ShadowCastingMode.Off;
            aimLine.enabled = false;

            var shooter = root.AddComponent<ShooterEnemy>();
            SetValues(shooter, serialized =>
            {
                serialized.FindProperty("moveSpeed").floatValue = 3f;
                serialized.FindProperty("projectilePrefab").objectReferenceValue = projectile;
                serialized.FindProperty("aimLine").objectReferenceValue = aimLine;
            });

            return SavePrefab(root, "Shooter").GetComponent<Enemy>();
        }

        private static void AddEnemyComponents(GameObject root, float groundOffset, int maxHealth, float bodyRadius, int experience, float knockbackMultiplier)
        {
            var torusObject = root.AddComponent<TorusObject>();
            SetValues(torusObject, serialized => serialized.FindProperty("groundOffset").floatValue = groundOffset);

            var health = root.AddComponent<Health>();
            SetValues(health, serialized =>
            {
                serialized.FindProperty("team").enumValueIndex = (int)CombatTeam.Enemy;
                serialized.FindProperty("maxHealth").intValue = maxHealth;
                serialized.FindProperty("bodyRadius").floatValue = bodyRadius;
            });

            var knockback = root.AddComponent<Knockback>();
            SetValues(knockback, serialized => serialized.FindProperty("distanceMultiplier").floatValue = knockbackMultiplier);

            var flash = root.AddComponent<BodyFlash>();
            PlayerSetupMenu.SetReference(flash, "health", health);

            var enemy = root.AddComponent<Enemy>();
            SetValues(enemy, serialized => serialized.FindProperty("experienceReward").intValue = experience);
        }

        private static void SetupSceneObject(TorusWorld world, Health playerHealth, Enemy runner, Enemy shooter)
        {
            var combatObject = GameObject.Find("Combat");
            if (combatObject == null)
            {
                combatObject = new GameObject("Combat");
                Undo.RegisterCreatedObjectUndo(combatObject, "Create Combat");
            }

            GetOrAdd<HitStop>(combatObject);

            var feedback = GetOrAdd<HitFeedback>(combatObject);
            PlayerSetupMenu.SetReference(feedback, "playerHealth", playerHealth);
            PlayerSetupMenu.SetReference(feedback, "followCamera", Object.FindAnyObjectByType<TorusFollowCamera>());

            var spawner = GetOrAdd<EnemySpawner>(combatObject);
            SetValues(spawner, serialized =>
            {
                serialized.FindProperty("world").objectReferenceValue = world;
                serialized.FindProperty("player").objectReferenceValue = playerHealth;
                serialized.FindProperty("head").objectReferenceValue = Object.FindAnyObjectByType<HeadMovement>();

                var entries = serialized.FindProperty("entries");
                entries.arraySize = 2;
                SetEntry(entries.GetArrayElementAtIndex(0), runner, 3f, 0f, new Vector2Int(2, 4));
                SetEntry(entries.GetArrayElementAtIndex(1), shooter, 1f, 0.15f, new Vector2Int(1, 2));
            });
        }

        private static void SetEntry(SerializedProperty entry, Enemy prefab, float weight, float minDifficulty, Vector2Int groupSize)
        {
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("weight").floatValue = weight;
            entry.FindPropertyRelative("minDifficulty").floatValue = minDifficulty;
            entry.FindPropertyRelative("groupSize").vector2IntValue = groupSize;
        }

        private static void CreateBody(GameObject root, PrimitiveType type, Vector3 scale, Material material)
        {
            var body = CreatePrimitive("Body", type, scale, material);
            body.transform.SetParent(root.transform, false);
        }

        /// <summary>Small box in front, so the facing of a placeholder enemy is visible.</summary>
        private static void CreateNose(GameObject root, float height)
        {
            var nose = CreatePrimitive("Nose", PrimitiveType.Cube, new Vector3(0.25f, 0.25f, 0.4f), GetMaterial("Nose", new Color(0.1f, 0.1f, 0.1f), false));
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, height * 0.3f, 0.45f);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 scale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.localScale = scale;

            // Combat works on surface distances, colliders are not needed.
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static Material GetMaterial(string name, Color color, bool emissive)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = $"{EnemiesFolder}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component))
                return component;

            // Prefab contents loaded for editing live in a preview scene, where Undo is not needed.
            return EditorSceneManager.IsPreviewScene(gameObject.scene)
                ? gameObject.AddComponent<T>()
                : Undo.AddComponent<T>(gameObject);
        }

        private static void SetValues(Object target, System.Action<SerializedObject> change)
        {
            var serialized = new SerializedObject(target);
            change(serialized);
            serialized.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
