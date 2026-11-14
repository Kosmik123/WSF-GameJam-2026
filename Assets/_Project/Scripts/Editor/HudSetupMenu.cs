using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Hud;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Creates the HUD in the open scene: a canvas with the player's hit point bar.
    /// Safe to run again: an existing bar is only connected to the player's health again.
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

            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            if (healthBar == null)
                healthBar = CreateHealthBar();

            var serialized = new SerializedObject(healthBar);
            serialized.FindProperty("health").objectReferenceValue = playerHealth;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(healthBar.gameObject.scene);
            Selection.activeObject = healthBar.gameObject;
            Debug.Log("HUD setup done: the health bar is connected to the player.");
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

        private static HealthBar CreateHealthBar()
        {
            var canvasObject = new GameObject("Hud", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create HUD");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Top left corner: the anchor and the pivot are in the same corner.
            var barObject = CreateImage("HealthBar", canvasObject.transform, BackgroundColor);
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.sizeDelta = new Vector2(480f, 36f);
            barRect.anchoredPosition = new Vector2(40f, -40f);

            var trail = CreateImage("Trail", barObject.transform, TrailColor);
            var fill = CreateImage("Fill", barObject.transform, FillColor);
            var label = CreateLabel(barObject.transform);

            var healthBar = barObject.AddComponent<HealthBar>();
            var serialized = new SerializedObject(healthBar);
            serialized.FindProperty("fill").objectReferenceValue = fill.GetComponent<RectTransform>();
            serialized.FindProperty("trail").objectReferenceValue = trail.GetComponent<RectTransform>();
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return healthBar;
        }

        private static GameObject CreateImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return imageObject;
        }

        // The label needs the default font of TextMeshPro, so it is created only when TMP Essentials are imported.
        private static TMP_Text CreateLabel(Transform parent)
        {
            if (TMP_Settings.instance == null)
            {
                Debug.LogWarning("TextMeshPro Essentials are not imported (Window > TextMeshPro > Import TMP Essential Resources). The bar has no label. To get one, import them, delete the Hud object and run this menu again.");
                return null;
            }

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22f;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "100 / 100";

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return label;
        }
    }
}
