using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Creates the HUD in the open scene: a canvas with the player's hit point bar and the round number.
    /// Safe to run again: parts that exist are only connected again, missing parts are added.
    /// </summary>
    public static class HudSetupMenu
    {
        private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        private static readonly Color TrailColor = new Color(0.95f, 0.8f, 0.3f, 1f);
        private static readonly Color FillColor = new Color(0.8f, 0.15f, 0.15f, 1f);

        [MenuItem("WSF GameJam/Setup/Create HUD")]
        private static void CreateHud()
        {
            var playerHealth = FindPlayerHealth();
            if (playerHealth == null)
            {
                EditorUtility.DisplayDialog("HUD setup", "The scene needs a player with Health (WSF GameJam/Setup/Create Combat And Enemies).", "OK");
                return;
            }

            if (TMP_Settings.instance == null && AskToImportTextMeshPro())
                return;

            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            var canvas = healthBar != null ? healthBar.GetComponentInParent<Canvas>() : CreateCanvas();
            if (healthBar == null)
                healthBar = CreateHealthBar(canvas.transform);

            Connect(healthBar, "health", playerHealth);

            var roundState = Object.FindAnyObjectByType<RoundState>();
            if (roundState == null)
                roundState = CreateRoundState();

            var roundLabel = Object.FindAnyObjectByType<RoundLabel>();
            if (roundLabel == null)
                roundLabel = CreateRoundLabel(canvas.transform);

            if (roundLabel != null)
                Connect(roundLabel, "roundState", roundState);

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeObject = canvas.gameObject;
            Debug.Log("HUD setup done: health bar" + (roundLabel != null ? " and round label" : " (no round label without TextMeshPro Essentials)") + ".");
        }

        // The texts need the default font of TextMeshPro. Returns true when the import was started and the menu has to be run again.
        private static bool AskToImportTextMeshPro()
        {
            var shouldImport = EditorUtility.DisplayDialog(
                "HUD setup",
                "TextMeshPro Essentials are not imported, so the HUD can not show texts. Import them now? Run this menu again after the import.",
                "Import",
                "Skip texts");

            if (shouldImport)
                EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");

            return shouldImport;
        }

        private static Health FindPlayerHealth()
        {
            foreach (var health in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health.Team == CombatTeam.Player)
                    return health;
            }

            return null;
        }

        private static void Connect(Component component, string propertyName, Object target)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Hud", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create HUD");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static HealthBar CreateHealthBar(Transform canvas)
        {
            // Top left corner: the anchor and the pivot are in the same corner.
            var barObject = CreateImage("HealthBar", canvas, BackgroundColor);
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.sizeDelta = new Vector2(480f, 36f);
            barRect.anchoredPosition = new Vector2(40f, -40f);

            var trail = CreateImage("Trail", barObject.transform, TrailColor);
            var fill = CreateImage("Fill", barObject.transform, FillColor);
            var label = CreateText("Label", barObject.transform, 22f, TextAlignmentOptions.Center, "100 / 100");

            var healthBar = barObject.AddComponent<HealthBar>();
            var serialized = new SerializedObject(healthBar);
            serialized.FindProperty("fill").objectReferenceValue = fill.GetComponent<RectTransform>();
            serialized.FindProperty("trail").objectReferenceValue = trail.GetComponent<RectTransform>();
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return healthBar;
        }

        private static RoundState CreateRoundState()
        {
            var roundObject = new GameObject("Rounds", typeof(RoundState));
            Undo.RegisterCreatedObjectUndo(roundObject, "Create HUD");
            return roundObject.GetComponent<RoundState>();
        }

        private static RoundLabel CreateRoundLabel(Transform canvas)
        {
            var labelText = CreateText("RoundLabel", canvas, 32f, TextAlignmentOptions.TopLeft, "Round 1");
            if (labelText == null)
                return null;

            // Under the health bar, in the same corner.
            var rect = labelText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(480f, 44f);
            rect.anchoredPosition = new Vector2(40f, -88f);

            var roundLabel = labelText.gameObject.AddComponent<RoundLabel>();
            Connect(roundLabel, "label", labelText);
            return roundLabel;
        }

        private static GameObject CreateImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            Stretch(imageObject.GetComponent<RectTransform>());
            return imageObject;
        }

        // Needs the default font of TextMeshPro, so it returns null when TMP Essentials are not imported.
        private static TMP_Text CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment, string text)
        {
            if (TMP_Settings.instance == null)
                return null;

            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.alignment = alignment;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            Stretch(textObject.GetComponent<RectTransform>());
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
