using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the "round complete" and death screens to the HUD canvas and connects them to the round loop.
    /// Needs the HUD (WSF GameJam/Setup/Create HUD) and the round loop (WSF GameJam/Setup/Create Round Loop).
    /// Safe to run again: existing screens are only connected again.
    /// </summary>
    public static class RoundScreensSetupMenu
    {
        private static readonly Color RoundCompleteBackground = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color DeathBackground = new Color(0.12f, 0f, 0f, 0.8f);
        private static readonly Color RecordColor = new Color(0.95f, 0.8f, 0.3f, 1f);

        [MenuItem("WSF GameJam/Setup/Create Round Screens")]
        private static void CreateRoundScreens()
        {
            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            var controller = Object.FindAnyObjectByType<RoundController>();
            if (healthBar == null || controller == null)
            {
                EditorUtility.DisplayDialog("Round screens setup", "Run WSF GameJam/Setup/Create HUD and WSF GameJam/Setup/Create Round Loop first.", "OK");
                return;
            }

            if (TMP_Settings.instance == null)
            {
                HudSetupMenu.AskToImportTextMeshPro();
                return;
            }

            var screens = Object.FindAnyObjectByType<RoundScreens>();
            if (screens == null)
                screens = CreateScreens(healthBar.GetComponentInParent<Canvas>().transform);

            HudSetupMenu.Connect(screens, "controller", controller);

            EditorSceneManager.MarkSceneDirty(screens.gameObject.scene);
            Selection.activeObject = screens.gameObject;
            Debug.Log("Round screens setup done: round complete and death screens are connected to the round loop.");
        }

        private static RoundScreens CreateScreens(Transform canvas)
        {
            var root = new GameObject("RoundScreens", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create Round Screens");
            root.transform.SetParent(canvas, false);
            HudSetupMenu.Stretch(root.GetComponent<RectTransform>());

            var roundComplete = CreateScreen("RoundCompleteScreen", root.transform, RoundCompleteBackground);
            var roundCompleteTitle = CreateText("Title", roundComplete.transform, 64f, 0f, "Round 1 complete", Color.white);

            var death = CreateScreen("DeathScreen", root.transform, DeathBackground);
            CreateText("Title", death.transform, 84f, 150f, "The jaw got you", Color.white);
            var deathResult = CreateText("Result", death.transform, 44f, 40f, "Rounds completed: 0", Color.white);
            var deathBest = CreateText("Best", death.transform, 36f, -20f, "Best: 0", Color.white);
            var newRecord = CreateText("NewRecord", death.transform, 40f, -90f, "New record!", RecordColor);

            var screens = root.AddComponent<RoundScreens>();
            var serialized = new SerializedObject(screens);
            serialized.FindProperty("roundCompleteScreen").objectReferenceValue = roundComplete.GetComponent<CanvasGroup>();
            serialized.FindProperty("roundCompleteTitle").objectReferenceValue = roundCompleteTitle;
            serialized.FindProperty("deathScreen").objectReferenceValue = death.GetComponent<CanvasGroup>();
            serialized.FindProperty("deathResult").objectReferenceValue = deathResult;
            serialized.FindProperty("deathBest").objectReferenceValue = deathBest;
            serialized.FindProperty("newRecordLabel").objectReferenceValue = newRecord.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return screens;
        }

        // A full screen dimming panel. It stays inactive until the round loop shows it.
        private static GameObject CreateScreen(string objectName, Transform parent, Color background)
        {
            var screen = HudSetupMenu.CreateImage(objectName, parent, background);
            screen.AddComponent<CanvasGroup>();
            screen.SetActive(false);
            return screen;
        }

        // A text in the middle of the screen, moved up or down by offsetY.
        private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, float offsetY, string text, Color color)
        {
            var label = HudSetupMenu.CreateText(objectName, parent, fontSize, TextAlignmentOptions.Center, text);
            label.color = color;

            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1200f, fontSize * 1.4f);
            rect.anchoredPosition = new Vector2(0f, offsetY);
            return label;
        }
    }
}
