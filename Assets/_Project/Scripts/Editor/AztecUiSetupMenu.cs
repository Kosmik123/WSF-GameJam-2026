using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WSFGameJam2026.Cameras;
using WSFGameJam2026.Controls;
using WSFGameJam2026.Editor.AztecUi;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Menus;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Builds the Aztec inspired UI in the open scene, one to one with the design canvas "Uroboros — propozycja UI":
    /// the main menu with the loading and the camera flight from above the torus behind the player, the HUD,
    /// the level up, round won and death screens, the pause menu with the settings and controls screens.
    /// Also translates the classes and skills to Polish and adds the components that feed the screens
    /// (level and experience, run statistics, lap). Needs the HUD and the round loop (their setup menus).
    /// Safe to run again: everything it made is made again.
    /// </summary>
    public static class AztecUiSetupMenu
    {
        private const string InputAssetPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";

        [MenuItem("WSF GameJam/Setup/Create Aztec UI")]
        private static void CreateAztecUi()
        {
            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            var controller = Object.FindAnyObjectByType<RoundController>();
            var roundState = Object.FindAnyObjectByType<RoundState>();
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (healthBar == null || controller == null || roundState == null || playerCombat == null)
            {
                EditorUtility.DisplayDialog("Aztec UI setup", "Run WSF GameJam/Setup/Create HUD and Create Round Loop first (the scene needs the player, the health bar and the round loop).", "OK");
                return;
            }

            if (TMP_Settings.instance == null)
            {
                HudSetupMenu.AskToImportTextMeshPro();
                return;
            }

            AztecUiFonts.Load();
            AztecUiData.TranslateContent();

            var hudCanvas = healthBar.GetComponentInParent<Canvas>();
            Undo.RegisterFullObjectHierarchyUndo(hudCanvas.gameObject, "Create Aztec UI");
            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "Create Aztec UI");
            Undo.RegisterFullObjectHierarchyUndo(playerCombat.gameObject, "Create Aztec UI");

            var lapTracker = AztecUiData.SetUpGameplayComponents(controller, out var playerLevel, out var runStats);
            EnsureEventSystem();

            // The new screens replace the placeholder ones of Create Round Screens.
            foreach (var oldScreens in Object.FindObjectsByType<RoundScreens>(FindObjectsInactive.Include))
                Object.DestroyImmediate(oldScreens.gameObject);

            var hudGroup = hudCanvas.TryGetComponent<CanvasGroup>(out var existingGroup) ? existingGroup : hudCanvas.gameObject.AddComponent<CanvasGroup>();
            var roundBanner = AztecHudBuilder.Build(hudCanvas.transform, healthBar, roundState, controller, lapTracker, playerLevel);

            var cameraIntro = SetUpCameraIntro();
            var menus = BuildMenus(controller, roundState, lapTracker, playerLevel, runStats, roundBanner, hudGroup, cameraIntro);
            AztecUiData.Set(roundBanner, "pause", menus.GetComponent<GamePause>());

            EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);
            Selection.activeObject = menus;
            Debug.Log("Aztec UI setup done: main menu, HUD, level up, round won, death, pause, settings and controls.");
        }

        private static GameObject BuildMenus(RoundController controller, RoundState roundState, LapTracker lapTracker, Progression.PlayerLevel playerLevel, RunStats runStats, RoundBanner roundBanner, CanvasGroup hud, CameraIntro cameraIntro)
        {
            var old = GameObject.Find("Menus");
            if (old)
                Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Menus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Create Aztec UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var uiInput = root.AddComponent<InputActionMapEnabler>();
            AztecUiData.Set(uiInput, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath));
            var serializedInput = new SerializedObject(uiInput);
            serializedInput.FindProperty("mapName").stringValue = "UI";
            serializedInput.ApplyModifiedProperties();

            var pause = root.AddComponent<GamePause>();
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat.TryGetComponent<InputActionMapEnabler>(out var playerInput))
                AztecUiData.Set(pause, "playerInput", playerInput);

            var cancel = PlayerSetupMenu.FindActionReference("UI", "Cancel");
            var settings = AztecMenuBuilder.BuildSettings(root.transform, cancel);
            var controls = AztecMenuBuilder.BuildControls(root.transform, cancel);

            AztecMenuBuilder.BuildMainMenu(root.transform, pause, controller, roundBanner, hud, cameraIntro, settings);
            AztecScreenBuilder.BuildRoundWon(root.transform, controller, runStats);
            AztecScreenBuilder.BuildRunEnd(root.transform, controller, runStats, playerLevel);
            AztecScreenBuilder.BuildLevelUp(root.transform, pause, playerLevel);
            AztecMenuBuilder.BuildPauseMenu(root.transform, pause, roundState, lapTracker, playerLevel, settings, controls);

            // Settings and controls open over the main menu and the pause menu.
            root.transform.Find("SettingsScreen").SetAsLastSibling();
            root.transform.Find("ControlsScreen").SetAsLastSibling();
            return root;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create Aztec UI");
                return;
            }

            if (eventSystem.TryGetComponent<StandaloneInputModule>(out var oldModule))
                Undo.DestroyObjectImmediate(oldModule);

            if (eventSystem.TryGetComponent<InputSystemUIInputModule>(out _) == false)
                Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
        }

        private static CameraIntro SetUpCameraIntro()
        {
            var followCamera = Object.FindAnyObjectByType<TorusFollowCamera>();
            if (followCamera == null)
                return null;

            if (followCamera.TryGetComponent<CameraIntro>(out var intro) == false)
                intro = Undo.AddComponent<CameraIntro>(followCamera.gameObject);

            AztecUiData.Set(intro, "world", Object.FindAnyObjectByType<TorusWorld>());
            return intro;
        }
    }
}
