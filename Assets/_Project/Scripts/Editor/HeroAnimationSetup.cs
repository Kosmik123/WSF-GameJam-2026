using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WSFGameJam2026.Player;
using WSFGameJam2026.Skills;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// The animations of the hero from the RPG Tiny Hero Duo clips (humanoid, so they play on every hero model):
    /// builds Animations/Player/Hero.controller (locomotion back / idle / walk / run / sprint with the step rate driven by
    /// the real speed, three combo swings, the shot, a "Skill" state whose clip each skill replaces, dodge, hit, death,
    /// soft transitions), gives every skill its own animation and puts the controller on the player.
    /// </summary>
    public static class HeroAnimationSetup
    {
        private const string ControllerPath = "Assets/_Project/Animations/Player/Hero.controller";
        private const string ClipsFolder = "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string SkillsFolder = "Assets/_Project/Data/Skills";
        private const string PlaceholderClip = "Attack04_SwordAndShiled";

        // Skill asset name -> clip and how long the cast animation lasts (seconds).
        private static readonly (string Skill, string Clip, float Duration)[] SkillClips =
        {
            ("Fireball", "Attack04_SwordAndShiled", 0.45f),
            ("FrostNova", "InPlace/JumpEnd_Normal_InPlace_SwordAndShield", 0.5f),
            ("ChainLightning", "Attack03_SwordAndShiled", 0.4f),
            ("Blink", "InPlace/JumpAir_Spin_InPlace_SwordAndShield", 0.3f),
            ("ArcaneOrbs", "LevelUp_Battle_SwordAndShield", 0.9f),
            ("Spin", "Attack04_Spinning_SwordAndShield", 0.5f),
            ("Charge", "InPlace/SprintFWD_Battle_InPlace_SwordAndShield", 0.4f),
            ("GroundSlam", "InPlace/JumpFull_Normal_InPlace_SwordAndShield", 0.7f),
            ("AxeThrow", "Attack02_SwordAndShiled", 0.4f),
            ("WarCry", "Victory_Battle_SwordAndShield", 0.8f),
        };

        [MenuItem("WSF GameJam/Setup/Apply Hero Animations")]
        internal static void Apply()
        {
            var controller = GetOrCreateController();
            AssignSkillClips();
            ConfigurePlayerPrefab(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Hero animations: Hero.controller (smooth locomotion, skill state), one animation per skill, controller on the player.");
        }

        /// <summary>True when the hero controller or its use on the player is missing.</summary>
        internal static bool IsNeeded()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (controller == null || player == null)
                return true;

            var playerAnimator = player.GetComponent<PlayerAnimator>();
            return playerAnimator && new SerializedObject(playerAnimator).FindProperty("controller").objectReferenceValue != controller;
        }

        internal static AnimatorController GetOrCreateController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing)
                return existing;

            EnsureFolder(Path.GetDirectoryName(ControllerPath).Replace('\\', '/'));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "MoveRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = "SkillSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dodge", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;

            // Locomotion: back, idle, walk, run, sprint (faster than the run when a skill or item speeds the hero up).
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(LoadClip("InPlace/MoveBWD_Battle_InPlace_SwordAndShield"), -1f);
            tree.AddChild(LoadClip("Idle_Battle_SwordAndShiled"), 0f);
            tree.AddChild(LoadClip("InPlace/MoveFWD_Normal_InPlace_SwordAndShield"), 0.45f);
            tree.AddChild(LoadClip("InPlace/MoveFWD_Battle_InPlace_SwordAndShield"), 1f);
            tree.AddChild(LoadClip("InPlace/SprintFWD_Battle_InPlace_SwordAndShield"), 1.6f);
            locomotion.speedParameter = "MoveRate";
            locomotion.speedParameterActive = true;
            machine.defaultState = locomotion;

            string[] attacks = { "Attack01_SwordAndShiled", "Attack02_SwordAndShiled", "Attack03_SwordAndShiled" };
            for (int i = 0; i < attacks.Length; i++)
            {
                var attack = AddActionState(machine, $"Attack {i + 1}", attacks[i], 2f, locomotion, 0.8f);
                var transition = AddAnyStateTransition(machine, attack, 0.05f);
                transition.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                transition.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
            }

            // The mage's basic shot: a quick thrust of the hand.
            var cast = AddActionState(machine, "Cast", "Attack01_SwordAndShiled", 2.4f, locomotion, 0.75f);
            AddAnyStateTransition(machine, cast, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Cast");

            // Skills: the clip is replaced per skill at runtime (AnimatorOverrideController), its speed fits the skill.
            var skill = AddActionState(machine, "Skill", PlaceholderClip, 1f, locomotion, 0.88f);
            skill.speedParameter = "SkillSpeed";
            skill.speedParameterActive = true;

            var hit = AddActionState(machine, "Hit", "GetHit01_SwordAndShield", 1.6f, locomotion, 0.8f);
            AddAnyStateTransition(machine, hit, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, "Hit");

            var dodge = machine.AddState("Dodge");
            dodge.motion = LoadClip("InPlace/JumpAir_Spin_InPlace_SwordAndShield");
            dodge.speed = 2f;
            AddAnyStateTransition(machine, dodge, 0.04f).AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            var dodgeEnd = dodge.AddTransition(locomotion);
            dodgeEnd.duration = 0.15f;
            dodgeEnd.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dodge");

            var die = machine.AddState("Die");
            die.motion = LoadClip("Die01_SwordAndShield");
            var toDie = machine.AddAnyStateTransition(die);
            toDie.duration = 0.12f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            var revive = die.AddTransition(locomotion);
            revive.duration = 0.25f;
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // A one-shot action that blends back to the locomotion near its end, softly.
        private static AnimatorState AddActionState(AnimatorStateMachine machine, string name, string clip, float speed, AnimatorState back, float exitTime)
        {
            var state = machine.AddState(name);
            state.motion = LoadClip(clip);
            state.speed = speed;
            var exit = state.AddTransition(back);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.duration = 0.18f;
            return state;
        }

        // Every action can interrupt the others, but not death.
        private static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine machine, AnimatorState target, float duration)
        {
            var transition = machine.AddAnyStateTransition(target);
            transition.duration = duration;
            transition.canTransitionToSelf = true;
            transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            return transition;
        }

        // Fills only skills without an animation, so a clip chosen by hand stays.
        private static void AssignSkillClips()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { SkillsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                string name = Path.GetFileNameWithoutExtension(path);
                foreach (var entry in SkillClips)
                {
                    if (entry.Skill != name || skill.CastAnimation)
                        continue;

                    var serialized = new SerializedObject(skill);
                    serialized.FindProperty("castAnimation").objectReferenceValue = LoadClip(entry.Clip);
                    serialized.FindProperty("castAnimationDuration").floatValue = entry.Duration;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void ConfigurePlayerPrefab(AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root.TryGetComponent<PlayerAnimator>(out var playerAnimator))
            {
                var serialized = new SerializedObject(playerAnimator);
                serialized.FindProperty("controller").objectReferenceValue = controller;
                serialized.FindProperty("skillPlaceholderClip").objectReferenceValue = LoadClip(PlaceholderClip);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        internal static AnimationClip LoadClip(string fileName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{ClipsFolder}/{fileName}.fbx"))
                if (asset is AnimationClip clip && clip.name.StartsWith("__preview__") == false)
                    return clip;

            Debug.LogWarning($"Hero animations: no animation clip in {fileName}.fbx");
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
