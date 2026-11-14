using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Audio;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Sets up sound effects: <see cref="SoundEffect"/> assets in Data/Sounds, an "Audio" scene object with the
    /// <see cref="SfxPlayer"/> and wired <see cref="SfxHooks"/>, and <see cref="EnemySfx"/> on every enemy prefab.
    /// Safe to run again: existing assets keep their settings, empty references are filled.
    /// </summary>
    public static class AudioSetupMenu
    {
        private const string SoundsFolder = "Assets/_Project/Data/Sounds";
        private const string SwordFolder = "Assets/_Project/Audio/Sword";
        private const string EnemiesFolder = "Assets/_Project/Prefabs/Enemies";

        [MenuItem("WSF GameJam/Setup/Create Audio")]
        private static void CreateAudio()
        {
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat == null)
            {
                EditorUtility.DisplayDialog("Audio setup", "The scene needs a player with PlayerCombat (WSF GameJam/Setup/Create Combat And Enemies).", "OK");
                return;
            }

            Run(playerCombat);
        }

        internal static void Run(PlayerCombat playerCombat)
        {
            EnsureFolder(SoundsFolder);
            var swordClips = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>($"{SwordFolder}/sword_1.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>($"{SwordFolder}/sword_2.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>($"{SwordFolder}/sword_3.wav"),
            };

            var swordSwing = GetSound("Sword Swing", SfxRecipe.Attack, 0.7f, clips: swordClips);
            var dodge = GetSound("Dodge", SfxRecipe.Dash, 0.7f);
            var mageShot = GetSound("Mage Shot", SfxRecipe.Cast, 0.55f);
            var skillCast = GetSound("Skill Cast", SfxRecipe.Cast, 0.8f);
            var playerHurt = GetSound("Player Hurt", SfxRecipe.PlayerHurt, 1f);
            var playerDeath = GetSound("Player Death", SfxRecipe.Death, 1f, spatialBlend: 0f);
            var enemyHit = GetSound("Enemy Hit", SfxRecipe.Hit, 0.8f, minInterval: 0.05f);
            var enemyDie = GetSound("Enemy Die", SfxRecipe.EnemyDie, 0.8f, minInterval: 0.05f);
            var roundCompleted = GetSound("Round Completed", SfxRecipe.Win, 1f, spatialBlend: 0f);
            var newRecord = GetSound("New Record", SfxRecipe.LevelUp, 0.8f, spatialBlend: 0f);
            var enemyWindup = GetSound("Enemy Windup", SfxRecipe.Windup, 0.6f);
            var enemyShot = GetSound("Enemy Shot", SfxRecipe.Shot, 0.6f);
            var enemyLunge = GetSound("Enemy Lunge", SfxRecipe.Dash, 0.5f);
            var heavyWindup = GetSound("Heavy Windup", SfxRecipe.BossTelegraph, 0.9f);
            var heavySlam = GetSound("Heavy Slam", SfxRecipe.GateOpen, 0.8f);

            var audioObject = GameObject.Find("Audio");
            if (audioObject == null)
            {
                audioObject = new GameObject("Audio");
                Undo.RegisterCreatedObjectUndo(audioObject, "Create Audio");
            }

            GetOrAdd<SfxPlayer>(audioObject);
            var hooks = GetOrAdd<SfxHooks>(audioObject);
            var player = playerCombat.gameObject;
            SetIfEmpty(hooks, "playerCombat", playerCombat);
            SetIfEmpty(hooks, "rangedAttack", player.GetComponent<RangedAttack>());
            SetIfEmpty(hooks, "skillUser", player.GetComponent<SkillUser>());
            SetIfEmpty(hooks, "playerHealth", player.GetComponent<Health>());
            SetIfEmpty(hooks, "roundController", Object.FindAnyObjectByType<RoundController>());
            SetIfEmpty(hooks, "swordSwing", swordSwing);
            SetIfEmpty(hooks, "dodge", dodge);
            SetIfEmpty(hooks, "rangedShot", mageShot);
            SetIfEmpty(hooks, "skillCast", skillCast);
            SetIfEmpty(hooks, "playerHurt", playerHurt);
            SetIfEmpty(hooks, "playerDeath", playerDeath);
            SetIfEmpty(hooks, "enemyHit", enemyHit);
            SetIfEmpty(hooks, "enemyDie", enemyDie);
            SetIfEmpty(hooks, "roundCompleted", roundCompleted);
            SetIfEmpty(hooks, "newRecord", newRecord);

            if (Object.FindAnyObjectByType<AudioListener>() == null)
                Debug.LogWarning("Audio setup: the scene has no AudioListener, nothing will be heard. Add one to the camera.");

            int enemyCount = SetupEnemyPrefabs(enemyWindup, enemyShot, enemyLunge, heavyWindup, heavySlam);

            EditorSceneManager.MarkSceneDirty(playerCombat.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Audio setup done: sounds in {SoundsFolder}, Audio object with the SfxPlayer and SfxHooks, EnemySfx on {enemyCount} enemy prefabs.");
        }

        private static int SetupEnemyPrefabs(SoundEffect windup, SoundEffect shot, SoundEffect lunge, SoundEffect heavyWindup, SoundEffect heavySlam)
        {
            if (AssetDatabase.IsValidFolder(EnemiesFolder) == false)
                return 0;

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.TryGetComponent<Enemy>(out _) == false)
                    continue;

                count++;
                if (prefab.TryGetComponent<EnemySfx>(out var existing) && IsWired(existing))
                    continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                var enemySfx = GetOrAdd<EnemySfx>(root);

                // Heavy enemies (the golem) get the boss telegraph and a slam, shooters a shot, the rest a lunge.
                bool isHeavy = Path.GetFileNameWithoutExtension(path).Contains("Golem");
                SetIfEmpty(enemySfx, "windup", isHeavy ? heavyWindup : windup);
                SetIfEmpty(enemySfx, "attack", PickAttackSound(root, isHeavy, heavySlam, shot, lunge));

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            return count;
        }

        private static SoundEffect PickAttackSound(GameObject enemy, bool isHeavy, SoundEffect slam, SoundEffect shot, SoundEffect lunge)
        {
            if (isHeavy)
                return slam;

            return enemy.TryGetComponent<ShooterEnemy>(out _) ? shot : lunge;
        }

        private static bool IsWired(EnemySfx enemySfx)
        {
            var serialized = new SerializedObject(enemySfx);
            return serialized.FindProperty("windup").objectReferenceValue && serialized.FindProperty("attack").objectReferenceValue;
        }

        /// <summary>Loads the sound asset or creates it with these settings. Existing assets are not changed.</summary>
        private static SoundEffect GetSound(string name, SfxRecipe recipe, float volume, float spatialBlend = 0.8f,
            float minInterval = 0.03f, AudioClip[] clips = null)
        {
            string path = $"{SoundsFolder}/{name}.asset";
            var sound = AssetDatabase.LoadAssetAtPath<SoundEffect>(path);
            if (sound)
                return sound;

            sound = ScriptableObject.CreateInstance<SoundEffect>();
            var serialized = new SerializedObject(sound);
            serialized.FindProperty("recipe").intValue = (int)recipe;
            serialized.FindProperty("volume").floatValue = volume;
            serialized.FindProperty("spatialBlend").floatValue = spatialBlend;
            serialized.FindProperty("minInterval").floatValue = minInterval;
            if (clips != null)
            {
                var clipsProperty = serialized.FindProperty("clips");
                clipsProperty.arraySize = 0;
                foreach (var clip in clips)
                {
                    if (clip == null)
                        continue;

                    clipsProperty.arraySize++;
                    clipsProperty.GetArrayElementAtIndex(clipsProperty.arraySize - 1).objectReferenceValue = clip;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(sound, path);
            return sound;
        }

        private static void SetIfEmpty(Object target, string propertyName, Object value)
        {
            if (value == null)
                return;

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property.objectReferenceValue)
                return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
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
