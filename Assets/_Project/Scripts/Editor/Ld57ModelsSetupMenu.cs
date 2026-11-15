using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Enemies;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Replaces the placeholders with the LD57 models: the slime (runner) and the shooter built from primitives with the LD57
    /// materials and the procedural <see cref="EnemyVisualAnimator"/>, and the player character from RPG Tiny Hero Duo
    /// (warrior: male with sword and shield, mage: female without weapons, blue) with an Animator controller made from its
    /// in-place animations. Safe to run again.
    /// </summary>
    public static class Ld57ModelsSetupMenu
    {
        private const string EnemiesFolder = "Assets/_Project/Prefabs/Enemies";
        private const string EnemyMaterialsFolder = "Assets/_Project/Materials/Enemies/LD57";
        private const string PlayerMaterialsFolder = "Assets/_Project/Materials/Player";
        private const string AnimationsFolder = "Assets/_Project/Animations/Player";
        private const string ControllerPath = AnimationsFolder + "/Player.controller";
        private const string ClassesFolder = "Assets/_Project/Data/Classes";
        private const string HeroFolder = "Assets/RPG Tiny Hero Duo";
        private const string ClipsFolder = HeroFolder + "/Animation/SwordAndShield";

        private static readonly string[] Weapons = { "OHS03Polyart", "OHS06Polyart", "Shield05Polyart", "Shield08Polyart" };

        /// <summary>Also run at the end of <see cref="SkillsSetupMenu"/>, so one menu sets up the mage with its model.</summary>
        [MenuItem("WSF GameJam/Setup/Apply LD57 Models")]
        internal static void ApplyModels()
        {
            int enemies = ApplyEnemyModels();
            bool hasPlayer = ApplyPlayerModel();

            // The quetzal priest (when built) replaces the RPG Tiny Hero Duo model in both classes.
            if (hasPlayer)
                HeroSetupMenu.ApplyPriest();

            AssetDatabase.SaveAssets();
            Debug.Log($"LD57 models: {enemies} enemy prefabs updated" + (hasPlayer ? ", player character set." : ", no player (run Create Skills And Mage first)."));
        }

        // Enemies

        private static int ApplyEnemyModels()
        {
            int count = 0;
            if (UpdatePrefab($"{EnemiesFolder}/Runner.prefab", root => BuildSlime(root, GetGroundOffset(root))))
                count++;
            if (UpdatePrefab($"{EnemiesFolder}/Shooter.prefab", root => BuildShooter(root, GetGroundOffset(root))))
                count++;

            return count;
        }

        /// <summary>LD57 slime: a flattened green sphere with two eyes, hopping when it moves.</summary>
        internal static void BuildSlime(GameObject root, float groundOffset)
        {
            var visual = PrepareVisual(root, groundOffset, 0.9f);
            CreatePart("Body", PrimitiveType.Sphere, visual, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.7f, 1f), "Slime_Body");
            var eyes = CreateGroup("Eyes", visual);
            CreatePart("EyeL", PrimitiveType.Sphere, eyes, new Vector3(0.18f, 0.42f, 0.42f), Vector3.one * 0.16f, "Slime_Eye");
            CreatePart("EyeR", PrimitiveType.Sphere, eyes, new Vector3(-0.18f, 0.42f, 0.42f), Vector3.one * 0.16f, "Slime_Eye");

            ConfigureAnimator(root, visual, null, serialized =>
            {
                serialized.FindProperty("hopsWhenMoving").boolValue = true;
                serialized.FindProperty("swayDegrees").floatValue = 0f;
                serialized.FindProperty("windupScale").floatValue = 1.3f;
                serialized.FindProperty("windupSeconds").floatValue = 0.5f;
                serialized.FindProperty("releaseKick").floatValue = 0.5f;
            });
        }

        /// <summary>LD57 shooter: a see-through capsule with a glowing core, a barrel and eyes, swaying and recoiling.</summary>
        internal static void BuildShooter(GameObject root, float groundOffset)
        {
            var visual = PrepareVisual(root, groundOffset, 1.2f);
            CreatePart("Body", PrimitiveType.Capsule, visual, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.6f, "Shooter_Body");
            var core = CreatePart("Core", PrimitiveType.Sphere, visual, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.3f, "Shooter_Core");
            var barrel = CreatePart("Barrel", PrimitiveType.Cylinder, visual, new Vector3(0f, 0.55f, 0.45f), new Vector3(0.12f, 0.25f, 0.12f), "Shooter_Barrel");
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var eyes = CreateGroup("Eyes", visual);
            CreatePart("EyeL", PrimitiveType.Sphere, eyes, new Vector3(0.1f, 0.85f, 0.26f), Vector3.one * 0.11f, "Shooter_Eye");
            CreatePart("EyeR", PrimitiveType.Sphere, eyes, new Vector3(-0.1f, 0.85f, 0.26f), Vector3.one * 0.11f, "Shooter_Eye");

            var lightObject = new GameObject("Core Light", typeof(Light));
            lightObject.transform.SetParent(core, false);
            var coreLight = lightObject.GetComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.8f, 1f, 0.3f);
            coreLight.range = 3f;
            coreLight.intensity = 1.5f;
            coreLight.shadows = LightShadows.None;

            ConfigureAnimator(root, visual, core, serialized =>
            {
                serialized.FindProperty("hopsWhenMoving").boolValue = false;
                serialized.FindProperty("swayDegrees").floatValue = 4f;
                serialized.FindProperty("windupScale").floatValue = 1.05f;
                serialized.FindProperty("windupSeconds").floatValue = 0.6f;
                serialized.FindProperty("releaseKick").floatValue = -0.2f;
            });
        }

        // Removes the placeholder parts and makes an empty "Visual" with its base on the ground.
        private static Transform PrepareVisual(GameObject root, float groundOffset, float scale)
        {
            foreach (var oldName in new[] { "Body", "Nose", "Visual" })
            {
                var old = root.transform.Find(oldName);
                if (old)
                    Object.DestroyImmediate(old.gameObject);
            }

            var visual = CreateGroup("Visual", root.transform);
            visual.localPosition = new Vector3(0f, -groundOffset, 0f);
            visual.localScale = Vector3.one * scale;
            return visual;
        }

        private static void ConfigureAnimator(GameObject root, Transform visual, Transform core, System.Action<SerializedObject> configure)
        {
            if (root.TryGetComponent<EnemyVisualAnimator>(out var animator) == false)
                animator = root.AddComponent<EnemyVisualAnimator>();

            var serialized = new SerializedObject(animator);
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("core").objectReferenceValue = core;
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<StatusEffects>() == null)
                root.AddComponent<StatusEffects>();
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static Transform CreatePart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, string materialName)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;

            // Hits are measured on the torus surface, colliders are not needed.
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            var material = AssetDatabase.LoadAssetAtPath<Material>($"{EnemyMaterialsFolder}/{materialName}.mat");
            if (material)
                part.GetComponent<Renderer>().sharedMaterial = material;

            return part.transform;
        }

        private static float GetGroundOffset(GameObject root)
        {
            var torusObject = root.GetComponent<TorusObject>();
            return torusObject ? new SerializedObject(torusObject).FindProperty("groundOffset").floatValue : 0f;
        }

        private static bool UpdatePrefab(string path, System.Action<GameObject> change)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return false;

            var root = PrefabUtility.LoadPrefabContents(path);
            change(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return true;
        }

        // Player

        private static bool ApplyPlayerModel()
        {
            var applier = Object.FindAnyObjectByType<PlayerClassApplier>();
            if (applier == null)
                return false;

            var controller = CreateController();
            SetClassModel("Warrior", $"{HeroFolder}/Prefab/MaleCharacterPolyart.prefab", null, new string[0]);
            SetClassModel("Mage", $"{HeroFolder}/Prefab/FemaleCharacterPolyart.prefab", CreateMageMaterial(), Weapons);

            var player = applier.gameObject;
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player);
            if (string.IsNullOrEmpty(prefabPath))
            {
                ConfigurePlayer(player, controller);
                EditorSceneManager.MarkSceneDirty(player.scene);
                return true;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            ConfigurePlayer(root, controller);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return true;
        }

        private static void ConfigurePlayer(GameObject player, AnimatorController controller)
        {
            var modelRoot = player.transform.Find("Model");
            if (modelRoot == null)
            {
                modelRoot = new GameObject("Model").transform;
                modelRoot.SetParent(player.transform, false);
            }

            // The pivot of the player is above the ground, the model stands on its feet.
            modelRoot.localPosition = new Vector3(0f, -GetGroundOffset(player), 0f);
            modelRoot.localRotation = Quaternion.identity;

            var applier = player.GetComponent<PlayerClassApplier>();
            var serializedApplier = new SerializedObject(applier);
            serializedApplier.FindProperty("modelRoot").objectReferenceValue = modelRoot;
            serializedApplier.FindProperty("placeholderRenderer").objectReferenceValue = player.GetComponent<MeshRenderer>();
            serializedApplier.ApplyModifiedPropertiesWithoutUndo();

            if (player.TryGetComponent<PlayerAnimator>(out var playerAnimator) == false)
                playerAnimator = player.AddComponent<PlayerAnimator>();

            PlayerSetupMenu.SetReference(playerAnimator, "controller", controller);
        }

        private static void SetClassModel(string className, string modelPath, Material material, string[] hiddenParts)
        {
            var playerClass = AssetDatabase.LoadAssetAtPath<PlayerClass>($"{ClassesFolder}/{className}.asset");
            if (playerClass == null)
                return;

            var serialized = new SerializedObject(playerClass);
            serialized.FindProperty("modelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            serialized.FindProperty("modelMaterial").objectReferenceValue = material;
            var parts = serialized.FindProperty("hiddenParts");
            parts.arraySize = hiddenParts.Length;
            for (int i = 0; i < hiddenParts.Length; i++)
                parts.GetArrayElementAtIndex(i).stringValue = hiddenParts[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // LD57 tinted the mage blue at runtime; here it is a material, because BodyFlash clears property blocks.
        private static Material CreateMageMaterial()
        {
            string path = $"{PlayerMaterialsFolder}/Mage_Polyart.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                return material;

            var source = AssetDatabase.LoadAssetAtPath<Material>($"{HeroFolder}/Material/Polyart_Default.mat");
            if (source == null)
                return null;

            EnsureFolder(PlayerMaterialsFolder);
            material = new Material(source) { name = "Mage_Polyart" };
            var tint = new Color(0.55f, 0.55f, 1f, 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AnimatorController CreateController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing)
                return existing;

            EnsureFolder(AnimationsFolder);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dodge", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(LoadClip("InPlace/MoveBWD_Battle_InPlace_SwordAndShield"), -1f);
            tree.AddChild(LoadClip("Idle_Battle_SwordAndShiled"), 0f);
            tree.AddChild(LoadClip("InPlace/MoveFWD_Battle_InPlace_SwordAndShield"), 1f);
            machine.defaultState = locomotion;

            string[] attacks = { "Attack01_SwordAndShiled", "Attack02_SwordAndShiled", "Attack03_SwordAndShiled" };
            for (int i = 0; i < attacks.Length; i++)
            {
                var attack = AddActionState(machine, $"Attack {i + 1}", attacks[i], 2f, locomotion);
                var transition = AddAnyStateTransition(machine, attack);
                transition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                transition.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
            }

            var cast = AddActionState(machine, "Cast", "Attack04_SwordAndShiled", 2f, locomotion);
            AddAnyStateTransition(machine, cast).AddCondition(AnimatorConditionMode.If, 0f, "Cast");

            var hit = AddActionState(machine, "Hit", "GetHit01_SwordAndShield", 1.5f, locomotion);
            AddAnyStateTransition(machine, hit).AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var dodge = machine.AddState("Dodge");
            dodge.motion = LoadClip("InPlace/JumpAir_Spin_InPlace_SwordAndShield");
            dodge.speed = 2f;
            AddAnyStateTransition(machine, dodge).AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            var dodgeEnd = dodge.AddTransition(locomotion);
            dodgeEnd.duration = 0.1f;
            dodgeEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dodge");

            var die = machine.AddState("Die");
            die.motion = LoadClip("Die01_SwordAndShield");
            var toDie = machine.AddAnyStateTransition(die);
            toDie.duration = 0.1f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            var revive = die.AddTransition(locomotion);
            revive.duration = 0.2f;
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AddActionState(AnimatorStateMachine machine, string name, string clip, float speed, AnimatorState back)
        {
            var state = machine.AddState(name);
            state.motion = LoadClip(clip);
            state.speed = speed;
            var exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.15f;
            return state;
        }

        // Every action can interrupt the others, but not death.
        private static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine machine, AnimatorState target)
        {
            var transition = machine.AddAnyStateTransition(target);
            transition.duration = 0.08f;
            transition.canTransitionToSelf = true;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            return transition;
        }

        private static AnimationClip LoadClip(string fileName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{ClipsFolder}/{fileName}.fbx"))
                if (asset is AnimationClip clip && clip.name.StartsWith("__preview__") == false)
                    return clip;

            Debug.LogWarning($"No animation clip in {fileName}.fbx");
            return null;
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
