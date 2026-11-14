using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;
using WSFGameJam2026.Skills.Mage;
using WSFGameJam2026.Skills.Warrior;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Sets up the skills and the classes (port of the LD57 P9Wiring_Mage and P1Wiring_Skills): placeholder projectile prefabs,
    /// the 5 mage and 5 warrior skill assets with the LD57 numbers (damage x4 for the WSF health scale), the Mage and Warrior
    /// classes, the skill components on the player prefab, status effects on the enemy prefabs, the skill bar in the HUD and
    /// the combat effects (<see cref="VfxSetupMenu"/>).
    /// Safe to run again: existing assets keep their (balanced) values, components are not duplicated.
    /// </summary>
    public static class SkillsSetupMenu
    {
        private const string PrefabsFolder = "Assets/_Project/Prefabs/Skills";
        private const string MaterialsFolder = "Assets/_Project/Materials/Skills";
        private const string MageSkillsFolder = "Assets/_Project/Data/Skills/Mage";
        private const string WarriorSkillsFolder = "Assets/_Project/Data/Skills/Warrior";
        private const string ClassesFolder = "Assets/_Project/Data/Classes";
        private const string EnemiesFolder = "Assets/_Project/Prefabs/Enemies";
        private const string WarriorDescription = "Melee combo and dodge, 100 HP. Skills: spin, charge, ground slam, axe throw, war cry.";

        private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.8f);
        private static readonly Color CooldownColor = new Color(0f, 0f, 0f, 0.65f);

        private readonly struct TierData
        {
            public string Name { get; }
            public string Description { get; }
            public float Cooldown { get; }
            public int Damage { get; }
            public float Range { get; }
            public float Duration { get; }
            public float Stun { get; }
            public float Knockback { get; }

            public TierData(string name, string description, float cooldown, int damage, float range, float duration, float stun = 0f, float knockback = 0f)
            {
                Name = name;
                Description = description;
                Cooldown = cooldown;
                Damage = damage;
                Range = range;
                Duration = duration;
                Stun = stun;
                Knockback = knockback;
            }
        }

        [MenuItem("WSF GameJam/Setup/Create Skills And Mage")]
        private static void CreateSkillsAndMage()
        {
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat == null)
            {
                EditorUtility.DisplayDialog("Skills setup", "The scene needs a player with combat (WSF GameJam/Setup/Create Combat And Enemies).", "OK");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex("Skills setup", "Which class should the player start with?", "Mage", "Cancel", "Warrior");
            if (choice == 1)
                return;

            Run(playerCombat, choice == 0);
        }

        /// <summary>The whole setup without dialogs (also used by <see cref="SkillsAutoSetup"/>).</summary>
        internal static void Run(PlayerCombat playerCombat, bool startAsMage)
        {
            int choice = startAsMage ? 0 : 2;

            EnsureFolder(PrefabsFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MageSkillsFolder);
            EnsureFolder(ClassesFolder);

            var bolt = CreateProjectilePrefab("Mage Bolt", 0.35f, 0.3f, new Color(0.45f, 0.65f, 1f));
            var fireball = CreateProjectilePrefab("Fireball", 0.6f, 0.5f, new Color(1f, 0.4f, 0.1f));
            var arcaneShot = CreateProjectilePrefab("Arcane Shot", 0.25f, 0.3f, new Color(0.7f, 0.4f, 1f));

            var fireballSkill = CreateFireball(fireball);
            var frostNova = CreateFrostNova();
            var chainLightning = CreateChainLightning();
            var blink = CreateBlink();
            var arcaneOrbs = CreateArcaneOrbs(arcaneShot);

            var mage = CreateClass("Mage", "Mage", "Shoots bolts from range, 80 HP. Skills: fireball, frost nova, chain lightning, blink, arcane orbs.",
                true, 80, new SkillDefinition[] { fireballSkill, frostNova, chainLightning, blink, arcaneOrbs },
                new SkillDefinition[] { fireballSkill, frostNova, chainLightning, blink });
            var warrior = SetupWarrior();

            SetupPlayer(playerCombat.gameObject, choice == 0 ? mage : warrior, bolt);
            AddStatusEffectsToEnemies();

            // Looks of the projectiles and the combat effects in the scene.
            VfxSetupMenu.AddProjectileVisuals();
            VfxSetupMenu.EnsureCombatVfx(playerCombat);

            // Player character (mage or warrior model, animations) and the LD57 enemy models.
            Ld57ModelsSetupMenu.ApplyModels();

            var playerSkills = playerCombat.GetComponent<SkillSlots>();
            var roundController = Object.FindAnyObjectByType<RoundController>();
            if (roundController)
                PlayerSetupMenu.SetReference(roundController, "playerSkills", playerSkills);

            bool hasSkillBar = CreateSkillBar(playerCombat.GetComponent<SkillUser>());

            EditorSceneManager.MarkSceneDirty(playerCombat.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Skills setup done: mage and warrior skills, classes, player skill components, combat effects" + (hasSkillBar ? ", skill bar." : " (no skill bar without TextMeshPro Essentials)."));
        }

        private static FireballSkill CreateFireball(SkillProjectile projectile)
        {
            return CreateSkill<FireballSkill>(MageSkillsFolder, "Fireball", "Fireball", serialized => serialized.FindProperty("projectilePrefab").objectReferenceValue = projectile,
                new TierData("Base", "Flies straight and explodes: 24 damage within 2 m.", 4f, 24, 2f, 0f),
                new TierData("Upgrade I", "The explosion deals 32 damage.", 4f, 32, 2f, 0f),
                new TierData("Upgrade II", "The explosion deals 40 damage.", 4f, 40, 2f, 0f),
                new TierData("Evolution: Blaze", "The ground keeps burning for 1.5 s and hurts enemies there three times.", 4f, 40, 2f, 1.5f));
        }

        private static FrostNovaSkill CreateFrostNova()
        {
            return CreateSkill<FrostNovaSkill>(MageSkillsFolder, "FrostNova", "Frost Nova", null,
                new TierData("Base", "Ring of ice within 3 m: 12 damage and 2 s of slow.", 8f, 12, 3f, 2f),
                new TierData("Upgrade I", "Radius 3.5 m, 16 damage.", 8f, 16, 3.5f, 2f),
                new TierData("Upgrade II", "Radius 4 m, 20 damage.", 8f, 20, 4f, 2f),
                new TierData("Evolution: Freeze", "Enemies in the ring are also frozen for 1.5 s.", 8f, 20, 4f, 2f, 1.5f));
        }

        private static ArcaneOrbsSkill CreateArcaneOrbs(SkillProjectile shot)
        {
            return CreateSkill<ArcaneOrbsSkill>(MageSkillsFolder, "ArcaneOrbs", "Arcane Orbs", serialized => serialized.FindProperty("shotPrefab").objectReferenceValue = shot,
                new TierData("Base", "3 orbs circle you for 6 s and hurt enemies (8 damage).", 12f, 8, 1.8f, 6f),
                new TierData("Upgrade I", "4 orbs, 12 damage.", 12f, 12, 1.8f, 6f),
                new TierData("Upgrade II", "5 orbs, 16 damage.", 12f, 16, 1.8f, 6f),
                new TierData("Evolution: Guardians", "Every second one of the orbs shoots the nearest enemy.", 12f, 16, 1.8f, 6f));
        }

        private static BlinkSkill CreateBlink()
        {
            return CreateSkill<BlinkSkill>(MageSkillsFolder, "Blink", "Blink", null,
                new TierData("Base", "Teleport 5 m in the direction of movement, 0.3 s of invulnerability, a blast at the end (12 damage).", 4f, 12, 5f, 0.3f),
                new TierData("Upgrade I", "Teleport 6 m.", 4f, 12, 6f, 0.3f),
                new TierData("Upgrade II", "Teleport 7 m.", 4f, 12, 7f, 0.3f),
                new TierData("Evolution: Rift", "A blast also at the start.", 4f, 12, 7f, 0.3f));
        }

        private static ChainLightningSkill CreateChainLightning()
        {
            return CreateSkill<ChainLightningSkill>(MageSkillsFolder, "ChainLightning", "Chain Lightning", null,
                new TierData("Base", "Hits the nearest enemy (8 m) and jumps to 3 more: 16 damage, every jump 4 less.", 5f, 16, 8f, 0f),
                new TierData("Upgrade I", "4 jumps, 20 damage.", 5f, 20, 8f, 0f),
                new TierData("Upgrade II", "5 jumps, 24 damage.", 5f, 24, 8f, 0f),
                new TierData("Evolution: Storm", "Every hit stuns the enemy for 0.5 s.", 5f, 24, 8f, 0f, 0.5f));
        }

        /// <summary>
        /// The 5 warrior skills (LD57 Spin, Charge, GroundSlam, AxeThrow, WarCry with the LD57 numbers, damage x4) and the
        /// Warrior class with them: all in the pool, all but the war cry from the start. Safe to run again: existing skill assets
        /// keep their values and an existing Warrior class only gets the skills when its pool is empty.
        /// </summary>
        internal static PlayerClass SetupWarrior()
        {
            EnsureFolder(WarriorSkillsFolder);
            EnsureFolder(ClassesFolder);

            var spin = CreateSpin();
            var charge = CreateCharge();
            var groundSlam = CreateGroundSlam();
            var axeThrow = CreateAxeThrow();
            var warCry = CreateWarCry();

            return CreateClass("Warrior", "Warrior", WarriorDescription, false, 100,
                new SkillDefinition[] { spin, charge, groundSlam, axeThrow, warCry },
                new SkillDefinition[] { spin, charge, groundSlam, axeThrow });
        }

        private static SpinSkill CreateSpin()
        {
            return CreateSkill<SpinSkill>(WarriorSkillsFolder, "Spin", "Spin", null,
                new TierData("Base", "Spin with the sword: 8 damage every 0.25 s within 2.5 m for 1 s.", 5f, 8, 2.5f, 1f),
                new TierData("Upgrade I", "Radius 3 m, 12 damage per tick.", 5f, 12, 3f, 1f),
                new TierData("Upgrade II", "Radius 3.5 m, 16 damage per tick.", 5f, 16, 3.5f, 1f),
                new TierData("Evolution: Tornado", "Spins for 2 s and pulls enemies to the middle.", 5f, 16, 3.5f, 2f));
        }

        private static ChargeSkill CreateCharge()
        {
            return CreateSkill<ChargeSkill>(WarriorSkillsFolder, "Charge", "Charge", null,
                new TierData("Base", "Dash 6 m forward through enemies, 20 damage. Invulnerable on the way.", 4f, 20, 6f, 0f),
                new TierData("Upgrade I", "Dash 8 m.", 4f, 20, 8f, 0f),
                new TierData("Upgrade II", "28 damage.", 4f, 28, 8f, 0f),
                new TierData("Evolution: Fire Charge", "Leaves a trail of fire for 3 s (8 damage per second).", 4f, 28, 8f, 3f));
        }

        private static GroundSlamSkill CreateGroundSlam()
        {
            return CreateSkill<GroundSlamSkill>(WarriorSkillsFolder, "GroundSlam", "Ground Slam", null,
                new TierData("Base", "Slam the ground: 16 damage within 3 m, knockback 3 m.", 6f, 16, 3f, 0.5f, 0f, 3f),
                new TierData("Upgrade I", "Radius 4 m.", 6f, 16, 4f, 0.5f, 0f, 3f),
                new TierData("Upgrade II", "24 damage.", 6f, 24, 4f, 0.5f, 0f, 3f),
                new TierData("Evolution: Fissure", "Also stuns enemies for 1 s.", 6f, 24, 4f, 0.5f, 1f, 3f));
        }

        private static AxeThrowSkill CreateAxeThrow()
        {
            return CreateSkill<AxeThrowSkill>(WarriorSkillsFolder, "AxeThrow", "Axe Throw", null,
                new TierData("Base", "Throw an axe 10 m: 12 damage on the way out and 12 on the way back.", 3f, 12, 10f, 0f),
                new TierData("Upgrade I", "Range 13 m.", 3f, 12, 13f, 0f),
                new TierData("Upgrade II", "16 damage.", 3f, 16, 13f, 0f),
                new TierData("Evolution: Two Axes", "Throw two axes 15 degrees apart.", 3f, 16, 13f, 0f));
        }

        private static WarCrySkill CreateWarCry()
        {
            return CreateSkill<WarCrySkill>(WarriorSkillsFolder, "WarCry", "War Cry", null,
                new TierData("Base", "Pushes enemies within 4 m away by 4 m and stuns them for 1 s.", 8f, 0, 4f, 1f, 1f, 4f),
                new TierData("Upgrade I", "Radius 5 m.", 8f, 0, 5f, 1f, 1f, 4f),
                new TierData("Upgrade II", "Stun 1.5 s.", 8f, 0, 5f, 1f, 1.5f, 4f),
                new TierData("Evolution: Roar", "Radius 6 m and 12 damage.", 8f, 12, 6f, 1f, 1.5f, 4f));
        }

        private static T CreateSkill<T>(string folder, string fileName, string displayName, System.Action<SerializedObject> configure, params TierData[] tiers) where T : SkillDefinition
        {
            string path = $"{folder}/{fileName}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<T>(path);
            if (skill)
                return skill;

            skill = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(skill, path);

            var serialized = new SerializedObject(skill);
            serialized.FindProperty("displayName").stringValue = displayName;
            var tiersProperty = serialized.FindProperty("tiers");
            tiersProperty.arraySize = SkillDefinition.MaxTier + 1;
            for (int i = 0; i < tiers.Length && i < tiersProperty.arraySize; i++)
                SetTier(tiersProperty.GetArrayElementAtIndex(i), tiers[i]);

            configure?.Invoke(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skill;
        }

        private static void SetTier(SerializedProperty tier, TierData data)
        {
            tier.FindPropertyRelative("tierName").stringValue = data.Name;
            tier.FindPropertyRelative("description").stringValue = data.Description;
            tier.FindPropertyRelative("cooldown").floatValue = data.Cooldown;
            tier.FindPropertyRelative("damage").intValue = data.Damage;
            tier.FindPropertyRelative("range").floatValue = data.Range;
            tier.FindPropertyRelative("duration").floatValue = data.Duration;
            tier.FindPropertyRelative("knockback").floatValue = data.Knockback;
            tier.FindPropertyRelative("stun").floatValue = data.Stun;
        }

        private static PlayerClass CreateClass(string fileName, string displayName, string description, bool isRanged, int maxHealth,
            SkillDefinition[] pool, SkillDefinition[] startingSkills)
        {
            string path = $"{ClassesFolder}/{fileName}.asset";
            var playerClass = AssetDatabase.LoadAssetAtPath<PlayerClass>(path);
            if (playerClass)
            {
                FillEmptySkills(playerClass, description, pool, startingSkills);
                return playerClass;
            }

            playerClass = ScriptableObject.CreateInstance<PlayerClass>();
            AssetDatabase.CreateAsset(playerClass, path);

            var serialized = new SerializedObject(playerClass);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("isRanged").boolValue = isRanged;
            serialized.FindProperty("rangedDamage").intValue = 8;
            serialized.FindProperty("rangedCooldown").floatValue = 0.35f;
            serialized.FindProperty("maxHealth").intValue = maxHealth;
            SetArray(serialized.FindProperty("skillPool"), pool);
            SetArray(serialized.FindProperty("startingSkills"), startingSkills);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return playerClass;
        }

        /// <summary>An existing class without skills (the Warrior made before it had any) gets the pool, the starting skills and the description.</summary>
        private static void FillEmptySkills(PlayerClass playerClass, string description, SkillDefinition[] pool, SkillDefinition[] startingSkills)
        {
            var serialized = new SerializedObject(playerClass);
            var poolProperty = serialized.FindProperty("skillPool");
            if (poolProperty.arraySize > 0 || pool.Length == 0)
                return;

            SetArray(poolProperty, pool);
            var startingProperty = serialized.FindProperty("startingSkills");
            if (startingProperty.arraySize == 0)
                SetArray(startingProperty, startingSkills);

            serialized.FindProperty("description").stringValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerClass);
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void SetupPlayer(GameObject player, PlayerClass playerClass, SkillProjectile bolt)
        {
            // Changes go to the prefab, when the player is an instance, so every scene gets them.
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player);
            if (string.IsNullOrEmpty(prefabPath))
            {
                ConfigurePlayer(player, playerClass, bolt);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            ConfigurePlayer(root, playerClass, bolt);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void ConfigurePlayer(GameObject player, PlayerClass playerClass, SkillProjectile bolt)
        {
            GetOrAdd<SkillSlots>(player);

            var skillUser = GetOrAdd<SkillUser>(player);
            var serializedUser = new SerializedObject(skillUser);
            var actions = serializedUser.FindProperty("slotActions");
            actions.arraySize = 4;
            for (int i = 0; i < 4; i++)
                actions.GetArrayElementAtIndex(i).objectReferenceValue = PlayerSetupMenu.FindActionReference("Player", $"Skill{i + 1}");

            serializedUser.ApplyModifiedPropertiesWithoutUndo();

            var rangedAttack = GetOrAdd<RangedAttack>(player);
            PlayerSetupMenu.SetReference(rangedAttack, "attackAction", PlayerSetupMenu.FindActionReference("Player", "Attack"));
            PlayerSetupMenu.SetReference(rangedAttack, "projectilePrefab", bolt);

            var applier = GetOrAdd<PlayerClassApplier>(player);
            PlayerSetupMenu.SetReference(applier, "playerClass", playerClass);
        }

        private static void AddStatusEffectsToEnemies()
        {
            if (AssetDatabase.IsValidFolder(EnemiesFolder) == false)
                return;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemiesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var enemy = root.GetComponent<Enemies.Enemy>();
                if (enemy && root.GetComponent<StatusEffects>() == null)
                {
                    root.AddComponent<StatusEffects>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SkillProjectile CreateProjectilePrefab(string name, float size, float hitRadius, Color color)
        {
            string path = $"{PrefabsFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<SkillProjectile>(path);
            if (existing)
                return existing;

            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = name;
            root.transform.localScale = Vector3.one * size;

            // Hits are measured on the torus surface, colliders are not needed.
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.GetComponent<Renderer>().sharedMaterial = GetMaterial(name, color);

            var torusObject = root.AddComponent<TorusObject>();
            var serializedObject = new SerializedObject(torusObject);
            serializedObject.FindProperty("groundOffset").floatValue = 0.9f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var projectile = root.AddComponent<SkillProjectile>();
            var serializedProjectile = new SerializedObject(projectile);
            serializedProjectile.FindProperty("hitRadius").floatValue = hitRadius;
            serializedProjectile.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<SkillProjectile>();
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static bool CreateSkillBar(SkillUser skillUser)
        {
            if (TMP_Settings.instance == null)
                return false;

            var skillBar = Object.FindAnyObjectByType<SkillBar>();
            if (skillBar == null)
                skillBar = BuildSkillBar(FindOrCreateCanvas().transform);

            HudSetupMenu.Connect(skillBar, "skillUser", skillUser);
            return true;
        }

        private static SkillBar BuildSkillBar(Transform canvas)
        {
            const int SlotCount = 4;
            const float SlotSize = 96f;
            const float Spacing = 16f;

            // Bottom centre of the screen.
            var barObject = new GameObject("SkillBar", typeof(RectTransform));
            barObject.transform.SetParent(canvas, false);
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(SlotCount * SlotSize + (SlotCount - 1) * Spacing, SlotSize);
            barRect.anchoredPosition = new Vector2(0f, 40f);

            var views = new SkillSlotView[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                views[i] = BuildSlot(barObject.transform, i, SlotSize, Spacing);

            var skillBar = barObject.AddComponent<SkillBar>();
            var serialized = new SerializedObject(skillBar);
            SetArray(serialized.FindProperty("views"), views);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(barObject, "Create Skill Bar");
            return skillBar;
        }

        private static SkillSlotView BuildSlot(Transform bar, int index, float size, float spacing)
        {
            var slotObject = HudSetupMenu.CreateImage($"Slot {index + 1}", bar, BackgroundColor);
            var rect = slotObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(index * (size + spacing), 0f);

            var icon = HudSetupMenu.CreateImage("Icon", slotObject.transform, Color.white).GetComponent<Image>();
            icon.preserveAspect = true;
            icon.enabled = false;

            var nameLabel = HudSetupMenu.CreateText("Name", slotObject.transform, 16f, TextAlignmentOptions.Center, string.Empty);

            var cooldown = HudSetupMenu.CreateImage("Cooldown", slotObject.transform, CooldownColor).GetComponent<Image>();
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;
            cooldown.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var keyLabel = HudSetupMenu.CreateText("Key", slotObject.transform, 18f, TextAlignmentOptions.TopLeft, string.Empty);
            keyLabel.margin = new Vector4(6f, 4f, 6f, 4f);
            var tierLabel = HudSetupMenu.CreateText("Tier", slotObject.transform, 18f, TextAlignmentOptions.BottomRight, string.Empty);
            tierLabel.margin = new Vector4(6f, 4f, 6f, 4f);

            var view = slotObject.AddComponent<SkillSlotView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("cooldownFill").objectReferenceValue = cooldown;
            serialized.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            serialized.FindProperty("keyLabel").objectReferenceValue = keyLabel;
            serialized.FindProperty("tierLabel").objectReferenceValue = tierLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static Canvas FindOrCreateCanvas()
        {
            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            if (healthBar)
                return healthBar.GetComponentInParent<Canvas>();

            var canvasObject = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create HUD");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
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
