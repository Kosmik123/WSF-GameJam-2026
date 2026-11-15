using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WSFGameJam2026.Editor.AztecUi;
using WSFGameJam2026.Leaderboard;
using WSFGameJam2026.Menu;
using static WSFGameJam2026.Editor.AztecUi.UiKit;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Builds the prefab of the leaderboard (Assets/_Project/Prefabs/Leaderboard.prefab): the window in the Aztec style with the
    /// top 10 places, the field for the name with the confirm button, the service that talks to Unity Services Leaderboards,
    /// the score collector, and the "Ranking" button of the menu. It does not touch any scene: put the prefab in the scene yourself,
    /// set the leaderboard id on its <see cref="LeaderboardService"/> and, to show the button only in the menu, connect the main menu.
    /// Safe to run again: the prefab is built again (the id and the connections you set in it are lost).
    /// </summary>
    public static class LeaderboardSetupMenu
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Leaderboard.prefab";
        private const int Places = 10;

        [MenuItem("WSF GameJam/Setup/Create Leaderboard Prefab")]
        private static void CreateLeaderboardPrefab()
        {
            if (TMP_Settings.instance == null)
            {
                HudSetupMenu.AskToImportTextMeshPro();
                return;
            }

            AztecUiFonts.Load();

            // Built in a preview scene, so the open scene is not changed.
            var previewScene = EditorSceneManager.NewPreviewScene();
            var root = Build(previewScene);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            EditorSceneManager.ClosePreviewScene(previewScene);

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"Leaderboard prefab saved to {PrefabPath}. Put it in the scene and set the leaderboard id on its Leaderboard Service.");
        }

        private static GameObject Build(Scene scene)
        {
            var root = new GameObject("Leaderboard", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var service = root.AddComponent<LeaderboardService>();
            var collector = root.AddComponent<ScoreCollector>();

            var buttonRoot = Rect(root.transform, "OpenButtonGroup");
            Place(buttonRoot, Vector2.zero, Vector2.zero, new Vector2(Px(80f), Px(48f)), new Vector2(Px(360f), Px(48f)));
            var buttonGroup = buttonRoot.gameObject.AddComponent<CanvasGroup>();
            var openButton = TextButton(buttonRoot, "OpenButton", "RANKING", 15f, WithAlpha(Obsidian, 0.6f), Ivory, Line, true, 0.14f, 360f, 48f);
            Stretch((RectTransform)openButton.transform);

            var screen = Stretch(Rect(root.transform, "LeaderboardScreen"));
            screen.gameObject.AddComponent<CanvasGroup>();
            Dim(screen, new Color(0.016f, 0.024f, 0.024f, 0.82f));
            SunStone(screen);
            FretBands(screen);

            var panel = Column(screen, "Panel", 12f, TextAnchor.UpperLeft, fit: true);
            Frame(panel, PanelFillStrong, FrameLine);
            Padding(panel, 32f, 32f, 24f, 24f);
            Place(panel, Center, Center, Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<LayoutElement>().minWidth = Px(460f);
            Label(panel, "Title", "RANKING", 36f, Ivory, heading: true, bold: true, spacingEm: 0.06f);

            var rowViews = BuildTable(panel);
            var status = Label(panel, "Status", string.Empty, 13f, Muted);
            Size(status.rectTransform, 396f, 18f);

            var account = Row(panel, "Account", 10f, TextAnchor.MiddleLeft);
            var accountLabel = Label(account, "Label", "Grasz jako:", 15f, Ivory);
            Size(accountLabel.rectTransform, 284f, 32f);
            var reset = TextButton(account, "Reset", "RESET", 12f, WithAlpha(Obsidian, 0.6f), DangerSoft, WithAlpha(MawRed, 0.6f), true, 0.1f, 102f, 32f);
            account.gameObject.SetActive(false);

            var entry = Column(panel, "Entry", 8f, TextAnchor.UpperLeft);
            var score = Label(entry, "Score", "Twój wynik: 0", 15f, Gold, bold: true);
            Size(score.rectTransform, 396f, 20f);
            var inputRow = Row(entry, "InputRow", 10f, TextAnchor.MiddleLeft);
            var nameInput = BuildInput(inputRow);
            var submit = TextButton(inputRow, "Submit", "ZATWIERDŹ", 13f, Gold, DarkGoldText, Color.clear, true, 0.1f, 126f, 44f);

            var back = TextButton(panel, "Back", "WRÓĆ", 15f, WithAlpha(Obsidian, 0.6f), Ivory, Line, true, 0.14f, 396f, 48f);

            root.AddComponent<KeepVisibleInMenu>();
            screen.gameObject.SetActive(false);

            var leaderboardScreen = root.AddComponent<LeaderboardScreen>();
            AztecUiData.Set(leaderboardScreen, "service", service);
            AztecUiData.Set(leaderboardScreen, "scoreCollector", collector);
            AztecUiData.Set(leaderboardScreen, "window", screen.gameObject);
            AztecUiData.Set(leaderboardScreen, "backButton", back);
            AztecUiData.Set(leaderboardScreen, "cancelAction", PlayerSetupMenu.FindActionReference("UI", "Cancel"));
            AztecUiData.Set(leaderboardScreen, "openButton", openButton);
            AztecUiData.Set(leaderboardScreen, "openButtonGroup", buttonGroup);
            AztecUiData.Set(leaderboardScreen, "statusLabel", status);
            AztecUiData.Set(leaderboardScreen, "entryPanel", entry.gameObject);
            AztecUiData.Set(leaderboardScreen, "scoreLabel", score);
            AztecUiData.Set(leaderboardScreen, "nameInput", nameInput);
            AztecUiData.Set(leaderboardScreen, "submitButton", submit);
            AztecUiData.Set(leaderboardScreen, "accountRow", account.gameObject);
            AztecUiData.Set(leaderboardScreen, "accountLabel", accountLabel);
            AztecUiData.Set(leaderboardScreen, "resetButton", reset);
            SetRows(leaderboardScreen, rowViews);

            var serialized = new SerializedObject(leaderboardScreen);
            serialized.FindProperty("playerColor").colorValue = Gold;
            serialized.FindProperty("rowColor").colorValue = Ivory;
            serialized.ApplyModifiedProperties();
            entry.gameObject.SetActive(false);
            return root;
        }

        private static RectTransform[][] BuildTable(RectTransform panel)
        {
            var table = Column(panel, "Table", 4f, TextAnchor.UpperLeft);
            Size(table, 396f, -1f);
            var views = new RectTransform[Places][];
            for (int i = 0; i < Places; i++)
            {
                var row = Row(table, "Row" + (i + 1), 8f, TextAnchor.MiddleLeft);
                Size(row, 396f, 26f);
                var rank = Label(row, "Rank", (i + 1).ToString(), 16f, Ivory, heading: true, bold: true);
                Size(rank.rectTransform, 44f, 26f);
                var playerName = Label(row, "Name", "—", 16f, Ivory);
                Size(playerName.rectTransform, 240f, 26f);
                var score = Label(row, "Score", "0", 16f, Ivory, heading: true, bold: true, alignment: TextAlignmentOptions.Right);
                Size(score.rectTransform, 96f, 26f);
                views[i] = new[] { row, rank.rectTransform, playerName.rectTransform, score.rectTransform };
                row.gameObject.SetActive(false);
            }

            return views;
        }

        private static TMP_InputField BuildInput(RectTransform row)
        {
            var box = Rect(row, "NameInput");
            var background = Frame(box, SlotFill, Line);
            background.raycastTarget = true;
            Size(box, 260f, 44f);

            var area = Stretch(Rect(box, "Text Area"), Px(10f));
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Label(area, "Placeholder", "Twoje imię", 15f, Muted);
            placeholder.fontStyle = FontStyles.Italic;
            Stretch(placeholder.rectTransform);
            var text = Label(area, "Text", string.Empty, 15f, Ivory);
            Stretch(text.rectTransform);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = background;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 16;
            field.caretColor = Gold;
            field.selectionColor = WithAlpha(Gold, 0.35f);
            field.colors = ButtonColors();
            return field;
        }

        private static void SetRows(LeaderboardScreen screen, RectTransform[][] views)
        {
            var serialized = new SerializedObject(screen);
            var rows = serialized.FindProperty("rows");
            rows.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("root").objectReferenceValue = views[i][0].gameObject;
                row.FindPropertyRelative("rankLabel").objectReferenceValue = views[i][1].GetComponent<TMP_Text>();
                row.FindPropertyRelative("nameLabel").objectReferenceValue = views[i][2].GetComponent<TMP_Text>();
                row.FindPropertyRelative("scoreLabel").objectReferenceValue = views[i][3].GetComponent<TMP_Text>();
            }

            serialized.ApplyModifiedProperties();
        }
    }
}
