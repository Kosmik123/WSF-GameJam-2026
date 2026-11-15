using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Sets up the progression (port of the LD57 Experience, EssencePickup, LevelUpScreen, XpBarView): XP and stat growth on the
    /// player, the XP orb prefab and the dropper, the XP bar, the level-up card screen and an EventSystem for clicking the cards,
    /// and the reset of the level when a new run starts. Safe to run again.
    /// </summary>
    public static class ProgressionSetupMenu
    {
        private const string PickupsFolder = "Assets/_Project/Prefabs/Pickups";
        private const string MaterialsFolder = "Assets/_Project/Materials/Pickups";
        private const string OrbPath = PickupsFolder + "/Experience Orb.prefab";

        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        private static readonly Color XpColor = new Color(0.35f, 0.85f, 1f, 1f);
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color CardColor = new Color(0.13f, 0.12f, 0.17f, 0.96f);

        [MenuItem("WSF GameJam/Setup/Create Progression")]
        private static void CreateProgression()
        {
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat == null)
            {
                EditorUtility.DisplayDialog("Progression setup", "The scene needs a player with combat (WSF GameJam/Setup/Create Combat And Enemies).", "OK");
                return;
            }

            if (TMP_Settings.instance == null && HudSetupMenu.AskToImportTextMeshPro())
                return;

            Run(playerCombat);
        }

        internal static void Run(PlayerCombat playerCombat)
        {
            SetupPlayer(playerCombat.gameObject);
            var experience = playerCombat.GetComponent<Experience>();

            var orb = CreateOrbPrefab();
            var progressionObject = GameObject.Find("Progression");
            if (progressionObject == null)
            {
                progressionObject = new GameObject("Progression");
                Undo.RegisterCreatedObjectUndo(progressionObject, "Create Progression");
            }

            var dropper = GetOrAdd<ExperienceDropper>(progressionObject);
            PlayerSetupMenu.SetReference(dropper, "player", experience);
            PlayerSetupMenu.SetReference(dropper, "orbPrefab", orb);

            var roundController = Object.FindAnyObjectByType<RoundController>();
            if (roundController)
                PlayerSetupMenu.SetReference(roundController, "playerExperience", experience);

            bool hasUi = TMP_Settings.instance != null;
            if (hasUi)
            {
                var canvas = FindOrCreateCanvas();
                SetupXpBar(canvas.transform, experience);
                SetupLevelUpScreen(canvas.transform, experience, playerCombat);
                EnsureEventSystem();
            }

            // The sounds were set up earlier, before the player had XP: give them the level-up sound now.
            if (Object.FindAnyObjectByType<Audio.SfxHooks>())
                AudioSetupMenu.Run(playerCombat);

            EditorSceneManager.MarkSceneDirty(playerCombat.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Progression setup done: XP and levels on the player, XP orbs" + (hasUi ? ", XP bar and level-up cards." : " (no UI without TextMeshPro Essentials)."));
        }

        private static void SetupPlayer(GameObject player)
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player);
            if (string.IsNullOrEmpty(prefabPath))
            {
                GetOrAdd<Experience>(player);
                GetOrAdd<LevelStats>(player);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            GetOrAdd<Experience>(root);
            GetOrAdd<LevelStats>(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // A glowing cyan crystal (LD57 essence), standing on the torus like everything else.
        private static ExperienceOrb CreateOrbPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ExperienceOrb>(OrbPath);
            if (existing)
                return existing;

            EnsureFolder(PickupsFolder);
            EnsureFolder(MaterialsFolder);
            var material = GetMaterial("Experience Orb", XpColor, new Color(0.5f, 2.2f, 3f));

            var root = new GameObject("Experience Orb");
            var torusObject = root.AddComponent<TorusObject>();
            var serializedObject = new SerializedObject(torusObject);
            serializedObject.FindProperty("groundOffset").floatValue = 0.5f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            CreatePart("Core", visual, new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.36f, 0.2f), new Vector3(0f, 45f, 0f), material);
            CreatePart("Chip A", visual, new Vector3(0.14f, -0.06f, 0.05f), Vector3.one * 0.09f, new Vector3(45f, 45f, 0f), material);
            CreatePart("Chip B", visual, new Vector3(-0.12f, 0.08f, -0.06f), Vector3.one * 0.07f, new Vector3(30f, 10f, 45f), material);

            var orb = root.AddComponent<ExperienceOrb>();
            PlayerSetupMenu.SetReference(orb, "visual", visual);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OrbPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ExperienceOrb>();
        }

        private static void CreatePart(string name, Transform parent, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(euler));
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // Under the round label, in the top left corner like the health bar.
        private static void SetupXpBar(Transform canvas, Experience experience)
        {
            var xpBar = Object.FindAnyObjectByType<XpBar>();
            if (xpBar == null)
            {
                var barObject = HudSetupMenu.CreateImage("XpBar", canvas, PanelColor);
                var rect = barObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(480f, 20f);
                rect.anchoredPosition = new Vector2(40f, -140f);

                var fill = HudSetupMenu.CreateImage("Fill", barObject.transform, XpColor);
                fill.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
                var label = HudSetupMenu.CreateText("Level", barObject.transform, 16f, TextAlignmentOptions.Center, "Lv 1");

                xpBar = barObject.AddComponent<XpBar>();
                PlayerSetupMenu.SetReference(xpBar, "fill", fill.GetComponent<RectTransform>());
                PlayerSetupMenu.SetReference(xpBar, "levelLabel", label);
            }

            PlayerSetupMenu.SetReference(xpBar, "experience", experience);
        }

        private static void SetupLevelUpScreen(Transform canvas, Experience experience, PlayerCombat playerCombat)
        {
            // The HUD canvas was made without a raycaster, and the cards need one to be clicked.
            GetOrAdd<GraphicRaycaster>(canvas.gameObject);

            var screen = Object.FindAnyObjectByType<LevelUpScreen>(FindObjectsInactive.Include);
            if (screen == null)
                screen = BuildLevelUpScreen(canvas);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("experience").objectReferenceValue = experience;
            serialized.FindProperty("skillSlots").objectReferenceValue = playerCombat.GetComponent<SkillSlots>();
            serialized.FindProperty("playerHealth").objectReferenceValue = playerCombat.GetComponent<Combat.Health>();
            var actions = serialized.FindProperty("cardActions");
            actions.arraySize = 3;
            for (int i = 0; i < 3; i++)
                actions.GetArrayElementAtIndex(i).objectReferenceValue = PlayerSetupMenu.FindActionReference("Player", $"Card{i + 1}");

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LevelUpScreen BuildLevelUpScreen(Transform canvas)
        {
            // The screen component stays active (it listens to level ups); only its root with the visuals is hidden.
            var screenObject = new GameObject("LevelUpScreen", typeof(RectTransform));
            screenObject.transform.SetParent(canvas, false);
            HudSetupMenu.Stretch(screenObject.GetComponent<RectTransform>());
            Undo.RegisterCreatedObjectUndo(screenObject, "Create Level Up Screen");

            var root = HudSetupMenu.CreateImage("Overlay", screenObject.transform, OverlayColor);
            root.GetComponent<Image>().raycastTarget = true;

            var header = HudSetupMenu.CreateText("Header", root.transform, 54f, TextAlignmentOptions.Center, "Level up! Choose a card");
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 0.75f);
            headerRect.anchorMax = new Vector2(1f, 0.9f);
            header.color = new Color(1f, 0.85f, 0.4f);

            var cards = new LevelUpCard[3];
            for (int i = 0; i < 3; i++)
                cards[i] = BuildCard(root.transform, i);

            var screen = screenObject.AddComponent<LevelUpScreen>();
            var serialized = new SerializedObject(screen);
            serialized.FindProperty("root").objectReferenceValue = root;
            var cardsProperty = serialized.FindProperty("cards");
            cardsProperty.arraySize = 3;
            for (int i = 0; i < 3; i++)
                cardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return screen;
        }

        private static LevelUpCard BuildCard(Transform parent, int index)
        {
            const float Width = 360f;
            const float Height = 440f;
            const float Spacing = 40f;

            var cardObject = HudSetupMenu.CreateImage($"Card {index + 1}", parent, CardColor);
            var image = cardObject.GetComponent<Image>();
            image.raycastTarget = true;
            var rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.42f);
            rect.anchorMax = new Vector2(0.5f, 0.42f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.anchoredPosition = new Vector2((index - 1) * (Width + Spacing), 0f);

            var button = cardObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.75f);
            colors.pressedColor = new Color(1f, 0.85f, 0.4f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f);
            button.colors = colors;

            var key = HudSetupMenu.CreateText("Key", cardObject.transform, 26f, TextAlignmentOptions.TopLeft, $"{index + 1}");
            key.margin = new Vector4(18f, 12f, 18f, 12f);
            key.color = new Color(1f, 0.85f, 0.4f);

            var icon = HudSetupMenu.CreateImage("Icon", cardObject.transform, Color.white).GetComponent<Image>();
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            iconRect.anchoredPosition = new Vector2(0f, -28f);
            icon.preserveAspect = true;
            icon.enabled = false;

            var title = HudSetupMenu.CreateText("Title", cardObject.transform, 28f, TextAlignmentOptions.Top, "Skill");
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.55f);
            titleRect.anchorMax = new Vector2(1f, 0.72f);
            title.margin = new Vector4(18f, 0f, 18f, 0f);

            var description = HudSetupMenu.CreateText("Description", cardObject.transform, 20f, TextAlignmentOptions.Top, "Description");
            var descriptionRect = description.GetComponent<RectTransform>();
            descriptionRect.anchorMin = new Vector2(0f, 0.05f);
            descriptionRect.anchorMax = new Vector2(1f, 0.55f);
            description.margin = new Vector4(22f, 0f, 22f, 0f);
            description.color = new Color(0.85f, 0.85f, 0.9f);

            var card = cardObject.AddComponent<LevelUpCard>();
            var serialized = new SerializedObject(card);
            serialized.FindProperty("keyLabel").objectReferenceValue = key;
            serialized.FindProperty("title").objectReferenceValue = title;
            serialized.FindProperty("description").objectReferenceValue = description;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // Clicking UI needs an EventSystem; with the Input System it uses its own input module.
        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>())
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static Canvas FindOrCreateCanvas()
        {
            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            if (healthBar)
                return healthBar.GetComponentInParent<Canvas>();

            var canvasObject = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create HUD");
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Material GetMaterial(string name, Color color, Color emission)
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
            material.SetColor("_EmissionColor", emission);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            if (gameObject.TryGetComponent<T>(out var component))
                return component;

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
