using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WSFGameJam2026.Cameras;
using WSFGameJam2026.Combat;
using WSFGameJam2026.Head;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Menus;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;
using static WSFGameJam2026.Editor.AztecUi.UiKit;

namespace WSFGameJam2026.Editor.AztecUi
{
    /// <summary>
    /// Builds the menus of the design: the main menu ("Ładowanie i menu") with the loading, the class choice,
    /// the legend and the markers over the torus, the pause menu ("Pauza"), and the settings and controls screens
    /// they both open.
    /// </summary>
    internal static class AztecMenuBuilder
    {
        private const string MageClassPath = "Assets/_Project/Data/Classes/Mage.asset";
        private const string WarriorClassPath = "Assets/_Project/Data/Classes/Warrior.asset";

        public static MainMenu BuildMainMenu(Transform root, GamePause pause, RoundController controller, RoundBanner roundBanner, CanvasGroup hud, CameraIntro cameraIntro, SubScreen settings)
        {
            var screen = Stretch(Rect(root, "MainMenuScreen"));
            var screenGroup = screen.gameObject.AddComponent<CanvasGroup>();

            var shade = Rect(screen, "Shade");
            Paint(shade, null, WithAlpha(Obsidian, 0.55f));
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = new Vector2(0f, 1f);
            shade.pivot = LeftCenter;
            shade.sizeDelta = new Vector2(Px(560f), 0f);
            shade.anchoredPosition = Vector2.zero;
            FretBands(screen);
            var markers = BuildOverviewMarkers(screen);

            var column = Column(screen, "Column", 28f, TextAnchor.MiddleLeft, fit: true);
            Place(column, LeftCenter, LeftCenter, new Vector2(Px(80f), 0f), Vector2.zero);

            var header = Column(column, "Header", 8f, TextAnchor.UpperLeft);
            Spiral(header);
            Label(header, "Jam", "WSF GAMEJAM 2026", 12f, Muted, spacingEm: 0.32f);
            Label(header, "Title", "UROBOROS", 72f, Ivory, heading: true, bold: true, spacingEm: 0.04f);
            Paragraph(header, "Tagline", "Uciekaj po ciele węża przed jego własną paszczą. Okrąż świat i stań na czubku głowy.", 15f, Muted, 360f);

            // Loading: the spinning ouroboros, the tip and the bar.
            var loading = Row(column, "Loading", 16f, TextAnchor.MiddleLeft);
            var ouroboros = Rect(loading, "Ouroboros");
            Paint(ouroboros, AztecUiSprites.Ouroboros, Turquoise);
            Size(ouroboros, 56f, 56f);
            ouroboros.gameObject.AddComponent<UiSpinner>();
            var loadingInfo = Column(loading, "Info", 10f, TextAnchor.MiddleLeft);
            var tip = Label(loadingInfo, "Tip", "Budzenie węża…", 13f, Muted);
            Size(tip.rectTransform, 288f, 18f);
            var barTrack = Rect(loadingInfo, "Bar");
            Size(barTrack, 288f, 6f);
            Paint(barTrack, AztecUiSprites.Pill, Track, UnityEngine.UI.Image.Type.Sliced).pixelsPerUnitMultiplier = 4f;
            var barFill = Stretch(Rect(barTrack, "Fill"));
            var barFillImage = Paint(barFill, null, Gold, UnityEngine.UI.Image.Type.Filled);
            barFillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            barFillImage.fillAmount = 0f;

            // Menu: class choice, play, settings and quit, record.
            var menu = Column(column, "Menu", 18f, TextAnchor.UpperLeft);
            var menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
            var classBlock = Column(menu, "Class", 8f, TextAnchor.UpperLeft);
            Label(classBlock, "Caption", "KLASA", 11f, Muted, spacingEm: 0.2f);
            var classRow = Row(classBlock, "Options", 10f, TextAnchor.UpperLeft);
            var classes = new[]
            {
                (AssetDatabase.LoadAssetAtPath<PlayerClass>(MageClassPath), "Mag", "pociski i 4 czary"),
                (AssetDatabase.LoadAssetAtPath<PlayerClass>(WarriorClassPath), "Wojownik", "miecz, tarcza, unik"),
            };

            var classButtons = new Button[classes.Length];
            var classFrames = new Image[classes.Length];
            for (int i = 0; i < classes.Length; i++)
                classButtons[i] = ClassButton(classRow, "Class" + (i + 1), classes[i].Item2, classes[i].Item3, out classFrames[i]);

            var play = TextButton(menu, "Play", "GRAJ", 17f, Gold, DarkGoldText, Color.clear, true, 0.16f, 360f, 56f);
            var buttonRow = Row(menu, "Buttons", 10f, TextAnchor.UpperLeft);
            var settingsButton = TextButton(buttonRow, "Settings", "Ustawienia", 14f, WithAlpha(Obsidian, 0.6f), Ivory, Line, false, 0f, 175f, 44f);
            var quit = TextButton(buttonRow, "Quit", "Wyjście", 14f, WithAlpha(Obsidian, 0.6f), Ivory, Line, false, 0f, 175f, 44f);
            var record = Label(menu, "Record", "Rekord: 0", 13f, Muted);

            BuildLegend(screen);

            var mainMenu = root.gameObject.AddComponent<MainMenu>();
            AztecUiData.Set(mainMenu, "pause", pause);
            AztecUiData.Set(mainMenu, "cameraIntro", cameraIntro);
            AztecUiData.Set(mainMenu, "roundController", controller);
            AztecUiData.Set(mainMenu, "playerClass", Object.FindAnyObjectByType<PlayerClassApplier>());
            AztecUiData.Set(mainMenu, "roundBanner", roundBanner);
            AztecUiData.Set(mainMenu, "menuScreen", screenGroup);
            AztecUiData.Set(mainMenu, "hud", hud);
            AztecUiData.Set(mainMenu, "loadingPanel", loading.gameObject);
            AztecUiData.Set(mainMenu, "loadingFill", barFillImage);
            AztecUiData.Set(mainMenu, "loadingTip", tip);
            AztecUiData.Set(mainMenu, "menuPanel", menuGroup);
            AztecUiData.Set(mainMenu, "playButton", play);
            AztecUiData.Set(mainMenu, "quitButton", quit);
            AztecUiData.Set(mainMenu, "settingsButton", settingsButton);
            AztecUiData.Set(mainMenu, "settingsScreen", settings);
            AztecUiData.Set(mainMenu, "recordLabel", record);

            var serialized = new SerializedObject(mainMenu);
            serialized.FindProperty("recordFormat").stringValue = "Rekord: <color=#EDE6D6><b>{0}</b></color>";
            serialized.FindProperty("selectedFrameColor").colorValue = Gold;
            serialized.FindProperty("idleFrameColor").colorValue = Line;
            var options = serialized.FindProperty("classOptions");
            options.arraySize = classes.Length;
            for (int i = 0; i < classes.Length; i++)
            {
                var option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("playerClass").objectReferenceValue = classes[i].Item1;
                option.FindPropertyRelative("button").objectReferenceValue = classButtons[i];
                option.FindPropertyRelative("frame").objectReferenceValue = classFrames[i];
            }

            serialized.ApplyModifiedProperties();
            markers.SetAsLastSibling();
            return mainMenu;
        }

        private static Button ClassButton(RectTransform row, string objectName, string className, string description, out Image frame)
        {
            var rect = Column(row, objectName, 2f, TextAnchor.MiddleLeft);
            Padding(rect, 14f, 14f, 10f, 10f);
            Size(rect, 175f, 64f);
            var background = Paint(rect, AztecUiSprites.Panel, WithAlpha(Obsidian, 0.6f), UnityEngine.UI.Image.Type.Sliced);
            background.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = ButtonColors();
            frame = Layer(rect, "Frame", AztecUiSprites.PanelFrame, Line, UnityEngine.UI.Image.Type.Sliced);
            Label(rect, "Name", className, 18f, Ivory, heading: true, bold: true);
            Label(rect, "Description", description, 12f, Muted);
            return button;
        }

        private static void BuildLegend(RectTransform screen)
        {
            var legend = Row(screen, "Legend", 18f, TextAnchor.MiddleRight, fit: true);
            Place(legend, BottomRight, BottomRight, new Vector2(-Px(40f), Px(28f)), Vector2.zero);
            LegendItem(legend, "Hero", AztecUiSprites.Dot, Gold, "start bohatera");
            LegendItem(legend, "Jaw", AztecUiSprites.Panel, MawRed, "paszcza");
            LegendItem(legend, "Goal", AztecUiSprites.DotOutline, Gold, "cel: czubek głowy");
        }

        private static void LegendItem(RectTransform legend, string objectName, Sprite sprite, Color color, string text)
        {
            var item = Row(legend, objectName, 6f, TextAnchor.MiddleLeft);
            var mark = Rect(item, "Mark");
            Paint(mark, sprite, color, sprite == AztecUiSprites.Panel ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple).pixelsPerUnitMultiplier = 8f;
            Size(mark, 10f, 10f);
            Label(item, "Text", text, 12f, Muted);
        }

        // Markers over the torus seen from above: the hero, the jaw and the goal, matching the legend.
        private static RectTransform BuildOverviewMarkers(RectTransform screen)
        {
            var area = Stretch(Rect(screen, "Markers"));
            var hero = Place(Rect(area, "Hero"), Center, Center, Vector2.zero, new Vector2(Px(14f), Px(14f)));
            Paint(hero, AztecUiSprites.Dot, Gold);
            Layer(hero, "Outline", AztecUiSprites.DotOutline, Color.white);
            var ping = Place(Rect(hero, "Ping"), Center, Center, Vector2.zero, new Vector2(Px(30f), Px(30f)));
            Paint(ping, AztecUiSprites.ThinRing, WithAlpha(Gold, 0.6f));
            ping.gameObject.AddComponent<UiSpinner>();

            var jaw = Place(Rect(area, "Jaw"), Center, Center, Vector2.zero, new Vector2(Px(14f), Px(14f)));
            Paint(jaw, AztecUiSprites.Panel, MawRed, UnityEngine.UI.Image.Type.Sliced).pixelsPerUnitMultiplier = 8f;
            var goal = Place(Rect(area, "Goal"), Center, Center, Vector2.zero, new Vector2(Px(16f), Px(16f)));
            Paint(goal, AztecUiSprites.DotOutline, Gold);

            var overview = area.gameObject.AddComponent<OverviewMarkers>();
            AztecUiData.Set(overview, "viewCamera", Camera.main);
            AztecUiData.Set(overview, "area", area);
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            var head = Object.FindAnyObjectByType<HeadMovement>();
            var weakPoint = Object.FindAnyObjectByType<WeakPoint>();
            var serialized = new SerializedObject(overview);
            var markers = serialized.FindProperty("markers");
            markers.arraySize = 3;
            SetMarker(markers.GetArrayElementAtIndex(0), playerCombat ? playerCombat.transform : null, hero, 2f);
            SetMarker(markers.GetArrayElementAtIndex(1), head ? head.transform : null, jaw, 4f);
            SetMarker(markers.GetArrayElementAtIndex(2), weakPoint ? weakPoint.transform : null, goal, 1f);
            serialized.ApplyModifiedProperties();
            return area;
        }

        private static void SetMarker(SerializedProperty marker, Transform target, RectTransform view, float height)
        {
            marker.FindPropertyRelative("target").objectReferenceValue = target;
            marker.FindPropertyRelative("view").objectReferenceValue = view;
            marker.FindPropertyRelative("heightOffset").floatValue = height;
        }

        public static void BuildPauseMenu(Transform root, GamePause pause, RoundState roundState, LapTracker lapTracker, PlayerLevel playerLevel, SubScreen settings, SubScreen controls)
        {
            var screen = Stretch(Rect(root, "PauseScreen"));
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            Dim(screen, new Color(0.016f, 0.024f, 0.024f, 0.72f));
            SunStone(screen);
            FretBands(screen);

            var row = Row(screen, "Content", 48f, TextAnchor.MiddleCenter, fit: true);
            Place(row, Center, Center, Vector2.zero, Vector2.zero);

            var buttons = Column(row, "Buttons", 12f, TextAnchor.UpperLeft);
            Size(buttons, 340f, -1f);
            var title = Label(buttons, "Title", "PAUZA", 48f, Ivory, heading: true, bold: true, spacingEm: 0.06f);
            Size(title.rectTransform, 340f, 72f);
            var resume = TextButton(buttons, "Resume", "WRÓĆ DO GRY", 15f, Gold, DarkGoldText, Color.clear, true, 0.14f, 340f, 52f);
            var settingsButton = TextButton(buttons, "Settings", "Ustawienia", 15f, Color.clear, Ivory, Line, false, 0f, 340f, 48f);
            var controlsButton = TextButton(buttons, "Controls", "Sterowanie", 15f, Color.clear, Ivory, Line, false, 0f, 340f, 48f);
            var surrender = TextButton(buttons, "Surrender", "Poddaj przejście", 15f, Color.clear, DangerSoft, WithAlpha(MawRed, 0.6f), false, 0f, 340f, 48f);

            var panel = Column(row, "Run", 18f, TextAnchor.UpperLeft);
            Frame(panel, PanelFillStrong, FrameLine);
            Padding(panel, 24f, 24f, 24f, 24f);
            Size(panel, 420f, -1f);
            Label(panel, "Caption", "TO PRZEJŚCIE", 11f, Muted, spacingEm: 0.2f);
            var stats = Row(panel, "Stats", 12f, TextAnchor.UpperLeft);
            var roundValue = Stat(stats, "Round", "Runda", Ivory);
            var levelValue = Stat(stats, "Level", "Poziom", Gold);
            var lapValue = Stat(stats, "Lap", "Okrążenie", Ivory);
            var divider = Rect(panel, "Divider");
            Paint(divider, null, Line);
            Size(divider, 372f, 1f);
            Label(panel, "SkillsCaption", "UMIEJĘTNOŚCI", 11f, Muted, spacingEm: 0.2f);

            var skillList = Column(panel, "Skills", 10f, TextAnchor.UpperLeft);
            const int Rows = 4;
            var rowObjects = new GameObject[Rows];
            var keys = new TMP_Text[Rows];
            var names = new TMP_Text[Rows];
            var tiers = new TMP_Text[Rows];
            for (int i = 0; i < Rows; i++)
            {
                var skillRow = Row(skillList, "Skill" + (i + 1), 12f, TextAnchor.MiddleLeft);
                var keyBox = Rect(skillRow, "Key");
                Frame(keyBox, SlotFill, Line);
                Size(keyBox, 28f, 28f);
                keys[i] = Label(keyBox, "Text", "Q", 12f, Ivory, bold: true, alignment: TextAlignmentOptions.Center);
                Stretch(keys[i].rectTransform);
                names[i] = Label(skillRow, "Name", "", 14f, Ivory);
                Size(names[i].rectTransform, 252f, 20f);
                tiers[i] = Label(skillRow, "Tier", "", 12f, Muted, bold: true, alignment: TextAlignmentOptions.Right);
                Size(tiers[i].rectTransform, 80f, 18f);
                rowObjects[i] = skillRow.gameObject;
            }

            var skillListView = panel.gameObject.AddComponent<SkillListView>();
            AztecUiData.Set(skillListView, "skillUser", Object.FindAnyObjectByType<SkillUser>());
            SetArray(skillListView, "rows", rowObjects);
            SetArray(skillListView, "keyLabels", keys);
            SetArray(skillListView, "nameLabels", names);
            SetArray(skillListView, "tierLabels", tiers);

            var levelView = levelValue.gameObject.AddComponent<PlayerLevelView>();
            AztecUiData.Set(levelView, "playerLevel", playerLevel);
            AztecUiData.Set(levelView, "levelLabel", levelValue);

            var pauseMenu = root.gameObject.AddComponent<PauseMenu>();
            AztecUiData.Set(pauseMenu, "pause", pause);
            AztecUiData.Set(pauseMenu, "toggleAction", PlayerSetupMenu.FindActionReference("UI", "Cancel"));
            AztecUiData.Set(pauseMenu, "screen", group);
            AztecUiData.Set(pauseMenu, "resumeButton", resume);
            AztecUiData.Set(pauseMenu, "settingsButton", settingsButton);
            AztecUiData.Set(pauseMenu, "controlsButton", controlsButton);
            AztecUiData.Set(pauseMenu, "surrenderButton", surrender);
            AztecUiData.Set(pauseMenu, "settingsScreen", settings);
            AztecUiData.Set(pauseMenu, "controlsScreen", controls);
            AztecUiData.Set(pauseMenu, "playerHealth", Object.FindAnyObjectByType<PlayerCombat>().GetComponent<Health>());
            AztecUiData.Set(pauseMenu, "roundState", roundState);
            AztecUiData.Set(pauseMenu, "lapTracker", lapTracker);
            AztecUiData.Set(pauseMenu, "roundLabel", roundValue);
            AztecUiData.Set(pauseMenu, "lapLabel", lapValue);
            screen.gameObject.SetActive(false);
        }

        private static TMP_Text Stat(RectTransform row, string objectName, string caption, Color valueColor)
        {
            var column = Column(row, objectName, 2f, TextAnchor.UpperLeft);
            Size(column, 116f, -1f);
            Label(column, "Caption", caption, 12f, Muted);
            return Label(column, "Value", "0", 28f, valueColor, heading: true, bold: true);
        }

        /// <summary>The settings screen: volume and full screen. Returns its sub screen (on the always active root).</summary>
        public static SubScreen BuildSettings(Transform root, InputActionReference cancel)
        {
            var screen = SubScreenFrame(root, "SettingsScreen", "USTAWIENIA", out var content, out var back);

            var volumeRow = Row(content, "Volume", 16f, TextAnchor.MiddleLeft);
            var volumeCaption = Label(volumeRow, "Caption", "Głośność", 15f, Ivory);
            Size(volumeCaption.rectTransform, 150f, 22f);
            var slider = BuildSlider(volumeRow);
            var volumeValue = Label(volumeRow, "Value", "100%", 15f, Muted, bold: true, alignment: TextAlignmentOptions.Right);
            Size(volumeValue.rectTransform, 60f, 22f);

            var fullScreenRow = Row(content, "FullScreen", 16f, TextAnchor.MiddleLeft);
            var fullScreenCaption = Label(fullScreenRow, "Caption", "Pełny ekran", 15f, Ivory);
            Size(fullScreenCaption.rectTransform, 150f, 22f);
            var toggle = BuildToggle(fullScreenRow);

            back.transform.SetAsLastSibling();
            var controls = root.gameObject.AddComponent<SettingsControls>();
            AztecUiData.Set(controls, "volumeSlider", slider);
            AztecUiData.Set(controls, "volumeLabel", volumeValue);
            AztecUiData.Set(controls, "fullScreenToggle", toggle);
            return AddSubScreen(root, screen, back, cancel, slider);
        }

        /// <summary>The controls screen: the keys of every action.</summary>
        public static SubScreen BuildControls(Transform root, InputActionReference cancel)
        {
            var screen = SubScreenFrame(root, "ControlsScreen", "STEROWANIE", out var content, out var back);
            ControlRow(content, new[] { "W", "A", "S", "D" }, "ruch (także strzałki)");
            ControlRow(content, new[] { "LPM" }, "atak (przytrzymaj: seria)");
            ControlRow(content, new[] { "Spacja" }, "unik");
            ControlRow(content, new[] { "Q", "E", "R", "F" }, "umiejętności");
            ControlRow(content, new[] { "1", "2", "3" }, "wybór karty przy awansie");
            ControlRow(content, new[] { "Esc" }, "pauza");
            back.transform.SetAsLastSibling();
            return AddSubScreen(root, screen, back, cancel, back);
        }

        private static RectTransform SubScreenFrame(Transform root, string objectName, string title, out RectTransform content, out Button back)
        {
            var screen = Stretch(Rect(root, objectName));
            screen.gameObject.AddComponent<CanvasGroup>();
            Dim(screen, new Color(0.016f, 0.024f, 0.024f, 0.82f));
            SunStone(screen);
            FretBands(screen);

            content = Column(screen, "Panel", 18f, TextAnchor.UpperLeft, fit: true);
            Frame(content, PanelFillStrong, FrameLine);
            Padding(content, 32f, 32f, 28f, 28f);
            Place(content, Center, Center, Vector2.zero, Vector2.zero);
            content.gameObject.AddComponent<LayoutElement>().minWidth = Px(460f);
            Label(content, "Title", title, 36f, Ivory, heading: true, bold: true, spacingEm: 0.06f);
            back = TextButton(content, "Back", "WRÓĆ", 15f, Gold, DarkGoldText, Color.clear, true, 0.14f, 396f, 52f);
            return screen;
        }

        private static SubScreen AddSubScreen(Transform root, RectTransform screen, Button back, InputActionReference cancel, Selectable firstSelected)
        {
            var subScreen = root.gameObject.AddComponent<SubScreen>();
            AztecUiData.Set(subScreen, "screen", screen.GetComponent<CanvasGroup>());
            AztecUiData.Set(subScreen, "backButton", back);
            AztecUiData.Set(subScreen, "cancelAction", cancel);
            AztecUiData.Set(subScreen, "firstSelected", firstSelected);
            screen.gameObject.SetActive(false);
            return subScreen;
        }

        private static void ControlRow(RectTransform content, string[] keys, string action)
        {
            var row = Row(content, "Control", 6f, TextAnchor.MiddleLeft);
            foreach (string key in keys)
            {
                var box = Rect(row, "Key");
                Frame(box, SlotFill, Line);
                var label = Label(box, "Text", key, 12f, Ivory, bold: true, alignment: TextAlignmentOptions.Center);
                Stretch(label.rectTransform);
                Size(box, Mathf.Max(28f, 10f + key.Length * 8f), 28f);
            }

            var spacer = Rect(row, "Gap");
            Size(spacer, 10f, 1f);
            Label(row, "Action", action, 14f, Muted);
        }

        private static Slider BuildSlider(RectTransform row)
        {
            var root = Rect(row, "Slider");
            Size(root, 220f, 20f);
            var background = Place(Rect(root, "Background"), LeftCenter, LeftCenter, Vector2.zero, Vector2.zero);
            background.anchorMin = new Vector2(0f, 0.5f);
            background.anchorMax = new Vector2(1f, 0.5f);
            background.sizeDelta = new Vector2(0f, Px(6f));
            Paint(background, AztecUiSprites.Pill, Track, UnityEngine.UI.Image.Type.Sliced).pixelsPerUnitMultiplier = 4f;

            var fillArea = Rect(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, Px(6f));
            var fill = Rect(fillArea, "Fill");
            fill.sizeDelta = Vector2.zero;
            Paint(fill, AztecUiSprites.Pill, Gold, UnityEngine.UI.Image.Type.Sliced).pixelsPerUnitMultiplier = 4f;

            var handleArea = Stretch(Rect(root, "Handle Slide Area"));
            var handle = Rect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(Px(18f), 0f);
            var handleImage = Paint(handle, AztecUiSprites.Dot, Ivory);
            handleImage.raycastTarget = true;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.colors = ButtonColors();
            return slider;
        }

        private static Toggle BuildToggle(RectTransform row)
        {
            var box = Rect(row, "Toggle");
            Size(box, 28f, 28f);
            var background = Frame(box, SlotFill, Line);
            background.raycastTarget = true;
            var check = Layer(box, "Check", AztecUiSprites.Panel, Gold, UnityEngine.UI.Image.Type.Sliced, Px(6f));
            check.pixelsPerUnitMultiplier = 6f;
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            toggle.colors = ButtonColors();
            return toggle;
        }

        private static void SetArray(Object target, string propertyName, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedProperties();
        }
    }
}
