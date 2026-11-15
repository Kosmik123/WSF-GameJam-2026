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
using WSFGameJam2026.Head;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Menus;
using WSFGameJam2026.Player;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Builds the Aztec inspired UI in the open scene (design: the "Uroboros: UI proposal" canvas):
    /// the main menu with loading, class choice and the camera flight from above the torus behind the player,
    /// the pause menu, the restyled HUD (mosaic health bar, round number with dots, lap ring, jaw warning, round banner)
    /// and the restyled round screens. Needs the HUD, the round loop and the round screens (their setup menus).
    /// Safe to run again: the objects it made are removed and made again.
    /// </summary>
    public static class AztecUiSetupMenu
    {
        private const string InputAssetPath = "Assets/_Project/Input/InputSystem_Actions.inputactions";
        private const string MageClassPath = "Assets/_Project/Data/Classes/Mage.asset";
        private const string WarriorClassPath = "Assets/_Project/Data/Classes/Warrior.asset";

        private static readonly Color Obsidian = Hex(0x0F0C0A);
        private static readonly Color PanelColor = new Color(0.055f, 0.04f, 0.03f, 0.82f);
        private static readonly Color ButtonColor = Hex(0x1A1512);
        private static readonly Color FrameColor = new Color(0.93f, 0.9f, 0.84f, 0.16f);
        private static readonly Color IdleFrameColor = Hex(0x3B302A);
        private static readonly Color TextColor = Hex(0xEDE6D6);
        private static readonly Color MutedColor = Hex(0xA9A493);
        private static readonly Color Gold = Hex(0xE3A23B);
        private static readonly Color FretGold = Hex(0xC9A24A);
        private static readonly Color DarkGoldText = Hex(0x1A1206);
        private static readonly Color Turquoise = Hex(0x36B3A0);
        private static readonly Color Trail = Hex(0xF0C68A);
        private static readonly Color Track = Hex(0x2A211C);
        private static readonly Color MawRed = Hex(0xE0483C);
        private static readonly Color DangerText = Hex(0xFF6B5E);
        private static readonly Color MosaicGap = new Color(0.04f, 0.03f, 0.03f, 0.85f);

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 LeftCenter = new Vector2(0f, 0.5f);

        [MenuItem("WSF GameJam/Setup/Create Aztec UI")]
        private static void CreateAztecUi()
        {
            var healthBar = Object.FindAnyObjectByType<HealthBar>();
            var controller = Object.FindAnyObjectByType<RoundController>();
            var roundState = Object.FindAnyObjectByType<RoundState>();
            if (healthBar == null || controller == null || roundState == null)
            {
                EditorUtility.DisplayDialog("Aztec UI setup", "Run WSF GameJam/Setup/Create HUD, Create Round Loop and Create Round Screens first.", "OK");
                return;
            }

            if (TMP_Settings.instance == null)
            {
                HudSetupMenu.AskToImportTextMeshPro();
                return;
            }

            var hudCanvas = healthBar.GetComponentInParent<Canvas>();
            Undo.RegisterFullObjectHierarchyUndo(hudCanvas.gameObject, "Create Aztec UI");

            EnsureEventSystem();
            var lapTracker = SetUpLapTracker(controller);
            StyleHealthBar(healthBar);
            var roundBanner = BuildHud(hudCanvas.transform, roundState, lapTracker, controller);
            StyleSkillSlots();
            StyleRoundScreens();

            var hudGroup = GetOrAdd<CanvasGroup>(hudCanvas.gameObject);
            var cameraIntro = SetUpCameraIntro();
            var menus = BuildMenus(controller, roundState, lapTracker, roundBanner, hudGroup, cameraIntro);
            Connect(roundBanner, "pause", menus.GetComponent<GamePause>());

            EditorSceneManager.MarkSceneDirty(hudCanvas.gameObject.scene);
            Selection.activeObject = menus;
            Debug.Log("Aztec UI setup done: main menu with the camera intro, pause menu, HUD and round screens.");
        }

        // ---------- HUD ----------

        private static void StyleHealthBar(HealthBar healthBar)
        {
            var bar = healthBar.transform;
            if (bar.TryGetComponent<Image>(out var background))
            {
                background.sprite = AztecUiSprites.Panel;
                background.type = Image.Type.Sliced;
                background.color = Track;
            }

            SetImageColor(bar.Find("Fill"), Turquoise);
            SetImageColor(bar.Find("Trail"), Trail);

            var mosaic = Recreate(bar, "Mosaic");
            var mosaicImage = mosaic.AddComponent<Image>();
            mosaicImage.sprite = AztecUiSprites.Mosaic;
            mosaicImage.type = Image.Type.Tiled;
            mosaicImage.color = MosaicGap;
            mosaicImage.raycastTarget = false;
            Stretch(mosaic.GetComponent<RectTransform>());

            var label = bar.Find("Label");
            if (label)
            {
                mosaic.transform.SetSiblingIndex(label.GetSiblingIndex());
                if (label.TryGetComponent<TMP_Text>(out var labelText))
                {
                    labelText.color = TextColor;
                    labelText.fontStyle = FontStyles.Bold;
                }
            }

            var frame = Recreate(bar, "Frame");
            AddImage(frame, AztecUiSprites.PanelFrame, Image.Type.Sliced, FrameColor);
            Stretch(frame.GetComponent<RectTransform>());
        }

        private static RoundBanner BuildHud(Transform canvas, RoundState roundState, LapTracker lapTracker, RoundController controller)
        {
            BuildRoundNumber(canvas, roundState);
            BuildLapRing(canvas, lapTracker);
            BuildMawWarning(canvas, lapTracker);
            return BuildRoundBanner(canvas, roundState, controller);
        }

        private static void BuildRoundNumber(Transform canvas, RoundState roundState)
        {
            Remove(canvas, "RoundCaption");
            var caption = Text(canvas, "RoundCaption", "RUNDA", 20f, MutedColor, TextAlignmentOptions.Center);
            caption.characterSpacing = 30f;
            Place(caption.rectTransform, TopCenter, TopCenter, new Vector2(0f, -28f), new Vector2(300f, 30f));

            var roundLabel = Object.FindAnyObjectByType<RoundLabel>();
            if (roundLabel)
            {
                var label = roundLabel.GetComponent<TMP_Text>();
                if (label)
                {
                    label.fontSize = 72f;
                    label.fontStyle = FontStyles.Bold;
                    label.color = TextColor;
                    label.alignment = TextAlignmentOptions.Center;
                    Place(label.rectTransform, TopCenter, TopCenter, new Vector2(0f, -60f), new Vector2(300f, 84f));
                }

                SetString(roundLabel, "format", "{0}");
            }

            var dots = Recreate(canvas, "RoundDots");
            var dotsRect = dots.AddComponent<RectTransform>();
            Place(dotsRect, TopCenter, TopCenter, new Vector2(0f, -152f), new Vector2(300f, 14f));
            var layout = dots.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            const int DotCount = 10;
            var dotObjects = new GameObject[DotCount];
            for (int i = 0; i < DotCount; i++)
            {
                var dot = Child(dots.transform, "Dot" + (i + 1));
                AddImage(dot, AztecUiSprites.Dot, Image.Type.Simple, FretGold);
                dot.GetComponent<RectTransform>().sizeDelta = new Vector2(12f, 12f);
                dotObjects[i] = dot;
            }

            var roundDots = dots.AddComponent<RoundDots>();
            Connect(roundDots, "roundState", roundState);
            var serialized = new SerializedObject(roundDots);
            var dotsProperty = serialized.FindProperty("dots");
            dotsProperty.arraySize = DotCount;
            for (int i = 0; i < DotCount; i++)
                dotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = dotObjects[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLapRing(Transform canvas, LapTracker lapTracker)
        {
            var panel = Recreate(canvas, "LapRing");
            Panel(panel, new Vector2(400f, 200f));
            Place(panel.GetComponent<RectTransform>(), TopRight, TopRight, new Vector2(-40f, -40f), new Vector2(400f, 200f));

            var ring = Child(panel.transform, "Ring");
            Place(ring.GetComponent<RectTransform>(), LeftCenter, Center, new Vector2(110f, 0f), new Vector2(164f, 164f));

            var ticks = Child(ring.transform, "Ticks");
            AddImage(ticks, AztecUiSprites.RingTicks, Image.Type.Simple, new Color(FretGold.r, FretGold.g, FretGold.b, 0.7f));
            Stretch(ticks.GetComponent<RectTransform>(), -14f);

            var track = Child(ring.transform, "Track");
            AddImage(track, AztecUiSprites.Ring, Image.Type.Simple, Track);
            Stretch(track.GetComponent<RectTransform>());

            var fill = Child(ring.transform, "Fill");
            var fillImage = AddImage(fill, AztecUiSprites.Ring, Image.Type.Filled, Turquoise);
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            Stretch(fill.GetComponent<RectTransform>());

            var maw = Child(ring.transform, "Maw");
            AddImage(maw, AztecUiSprites.Panel, Image.Type.Sliced, MawRed);
            Place(maw.GetComponent<RectTransform>(), TopCenter, Center, new Vector2(0f, -9f), new Vector2(14f, 26f));

            var pivot = Child(ring.transform, "PlayerPivot");
            Stretch(pivot.GetComponent<RectTransform>());
            var marker = Child(pivot.transform, "Player");
            AddImage(marker, AztecUiSprites.Dot, Image.Type.Simple, TextColor);
            Place(marker.GetComponent<RectTransform>(), TopCenter, Center, new Vector2(0f, -9f), new Vector2(22f, 22f));

            var goal = Text(ring.transform, "Goal", "CEL", 16f, MutedColor, TextAlignmentOptions.Center);
            goal.characterSpacing = 20f;
            Place(goal.rectTransform, Center, Center, Vector2.zero, new Vector2(100f, 24f));

            var lapCaption = Text(panel.transform, "LapCaption", "OKRĄŻENIE", 16f, MutedColor, TextAlignmentOptions.Right);
            lapCaption.characterSpacing = 12f;
            Place(lapCaption.rectTransform, TopRight, TopRight, new Vector2(-22f, -26f), new Vector2(180f, 22f));
            var lapValue = Text(panel.transform, "LapValue", "0%", 38f, TextColor, TextAlignmentOptions.Right);
            lapValue.fontStyle = FontStyles.Bold;
            Place(lapValue.rectTransform, TopRight, TopRight, new Vector2(-22f, -48f), new Vector2(180f, 44f));

            var distanceCaption = Text(panel.transform, "DistanceCaption", "PASZCZA ZA TOBĄ", 16f, MutedColor, TextAlignmentOptions.Right);
            distanceCaption.characterSpacing = 12f;
            Place(distanceCaption.rectTransform, TopRight, TopRight, new Vector2(-22f, -106f), new Vector2(200f, 22f));
            var distanceValue = Text(panel.transform, "DistanceValue", "0 m", 38f, TextColor, TextAlignmentOptions.Right);
            distanceValue.fontStyle = FontStyles.Bold;
            Place(distanceValue.rectTransform, TopRight, TopRight, new Vector2(-22f, -128f), new Vector2(180f, 44f));

            var lapRing = panel.AddComponent<LapRing>();
            Connect(lapRing, "lapTracker", lapTracker);
            Connect(lapRing, "progressFill", fillImage);
            Connect(lapRing, "playerMarkerPivot", pivot.GetComponent<RectTransform>());
            Connect(lapRing, "progressLabel", lapValue);
            Connect(lapRing, "distanceLabel", distanceValue);
        }

        private static void BuildMawWarning(Transform canvas, LapTracker lapTracker)
        {
            var warning = Recreate(canvas, "MawWarning");
            Stretch(warning.AddComponent<RectTransform>());
            warning.transform.SetAsFirstSibling();
            var group = warning.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var vignette = Child(warning.transform, "Vignette");
            AddImage(vignette, AztecUiSprites.Vignette, Image.Type.Simple, new Color(MawRed.r, MawRed.g, MawRed.b, 0.85f));
            Stretch(vignette.GetComponent<RectTransform>());

            var banner = Child(warning.transform, "Banner");
            AddImage(banner, AztecUiSprites.Panel, Image.Type.Sliced, new Color(MawRed.r, MawRed.g, MawRed.b, 0.92f));
            Place(banner.GetComponent<RectTransform>(), TopCenter, TopCenter, new Vector2(0f, -190f), new Vector2(560f, 60f));
            var bannerText = Text(banner.transform, "Text", "PASZCZA TUŻ ZA TOBĄ", 26f, Color.white, TextAlignmentOptions.Center);
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.characterSpacing = 14f;

            var mawWarning = warning.AddComponent<MawWarning>();
            Connect(mawWarning, "lapTracker", lapTracker);
            Connect(mawWarning, "group", group);
        }

        private static RoundBanner BuildRoundBanner(Transform canvas, RoundState roundState, RoundController controller)
        {
            var root = Recreate(canvas, "RoundBanner");
            Place(root.AddComponent<RectTransform>(), Center, Center, new Vector2(0f, 200f), new Vector2(600f, 260f));
            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var caption = Text(root.transform, "Caption", "RUNDA", 24f, MutedColor, TextAlignmentOptions.Center);
            caption.characterSpacing = 30f;
            Place(caption.rectTransform, TopCenter, TopCenter, Vector2.zero, new Vector2(600f, 34f));

            var number = Text(root.transform, "Number", "1", 150f, TextColor, TextAlignmentOptions.Center);
            number.fontStyle = FontStyles.Bold;
            Place(number.rectTransform, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(600f, 170f));

            var flee = Text(root.transform, "Flee", "UCIEKAJ!", 30f, DangerText, TextAlignmentOptions.Center);
            flee.fontStyle = FontStyles.Bold;
            flee.characterSpacing = 16f;
            Place(flee.rectTransform, TopCenter, TopCenter, new Vector2(0f, -205f), new Vector2(600f, 44f));

            var banner = root.AddComponent<RoundBanner>();
            Connect(banner, "controller", controller);
            Connect(banner, "roundState", roundState);
            Connect(banner, "group", group);
            Connect(banner, "roundLabel", number);
            return banner;
        }

        private static void StyleSkillSlots()
        {
            foreach (var slot in Object.FindObjectsByType<SkillSlotView>(FindObjectsInactive.Include))
            {
                if (slot.TryGetComponent<Image>(out var image) == false)
                    continue;

                image.sprite = AztecUiSprites.Panel;
                image.type = Image.Type.Sliced;
                image.color = ButtonColor;
                var frame = Recreate(slot.transform, "Frame");
                AddImage(frame, AztecUiSprites.PanelFrame, Image.Type.Sliced, IdleFrameColor);
                Stretch(frame.GetComponent<RectTransform>());
            }
        }

        // ---------- Round screens ----------

        private static void StyleRoundScreens()
        {
            var screens = Object.FindAnyObjectByType<RoundScreens>(FindObjectsInactive.Include);
            if (screens == null)
                return;

            var serialized = new SerializedObject(screens);
            serialized.FindProperty("roundCompleteFormat").stringValue = "Runda {0} ukończona";
            serialized.FindProperty("deathResultFormat").stringValue = "Przetrwane rundy: {0}";
            serialized.FindProperty("deathBestFormat").stringValue = "Rekord: {0}";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var roundComplete = serialized.FindProperty("roundCompleteScreen").objectReferenceValue as CanvasGroup;
            if (roundComplete)
            {
                DecorateScreen(roundComplete.transform, new Color(Obsidian.r, Obsidian.g, Obsidian.b, 0.62f));
                SetText(roundComplete.transform.Find("Title"), null, TextColor);
                Remove(roundComplete.transform, "Subtitle");
                var subtitle = Text(roundComplete.transform, "Subtitle", "Pętla zamknięta. Koniec jest początkiem: paszcza przyspiesza.", 28f, MutedColor, TextAlignmentOptions.Center);
                Place(subtitle.rectTransform, Center, Center, new Vector2(0f, -70f), new Vector2(1200f, 40f));
            }

            var death = serialized.FindProperty("deathScreen").objectReferenceValue as CanvasGroup;
            if (death)
            {
                DecorateScreen(death.transform, new Color(0.07f, 0.03f, 0.03f, 0.9f));
                SetText(death.transform.Find("Title"), "Koniec pętli", TextColor);
                SetText(death.transform.Find("Result"), null, TextColor);
                SetText(death.transform.Find("Best"), null, Gold);
                SetText(death.transform.Find("NewRecord"), "NOWY REKORD", Gold);
            }
        }

        // A dark full screen panel with the sun stone, the stepped spiral and two stepped pyramid bands.
        private static void DecorateScreen(Transform screen, Color background)
        {
            SetImageColor(screen, background);

            var sun = Recreate(screen, "SunStone");
            AddImage(sun, AztecUiSprites.SunStone, Image.Type.Simple, new Color(FretGold.r, FretGold.g, FretGold.b, 0.13f));
            Place(sun.GetComponent<RectTransform>(), Center, Center, Vector2.zero, new Vector2(960f, 960f));
            sun.transform.SetAsFirstSibling();

            AddFretBands(screen);

            var spiral = Recreate(screen, "Spiral");
            AddImage(spiral, AztecUiSprites.Spiral, Image.Type.Simple, FretGold);
            Place(spiral.GetComponent<RectTransform>(), Center, Center, new Vector2(0f, 250f), new Vector2(72f, 72f));
        }

        private static void AddFretBands(Transform parent)
        {
            var top = Recreate(parent, "FretTop");
            AddImage(top, AztecUiSprites.Fret, Image.Type.Tiled, new Color(FretGold.r, FretGold.g, FretGold.b, 0.5f));
            var topRect = top.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = TopCenter;
            topRect.sizeDelta = new Vector2(0f, 24f);
            topRect.anchoredPosition = new Vector2(0f, -24f);

            var bottom = Recreate(parent, "FretBottom");
            AddImage(bottom, AztecUiSprites.Fret, Image.Type.Tiled, new Color(FretGold.r, FretGold.g, FretGold.b, 0.5f));
            var bottomRect = bottom.GetComponent<RectTransform>();
            bottomRect.anchorMin = Vector2.zero;
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.sizeDelta = new Vector2(0f, 24f);
            bottomRect.anchoredPosition = new Vector2(0f, 48f);
            bottomRect.localScale = new Vector3(1f, -1f, 1f);
        }

        // ---------- Menus ----------

        private static GameObject BuildMenus(RoundController controller, RoundState roundState, LapTracker lapTracker, RoundBanner roundBanner, CanvasGroup hud, CameraIntro cameraIntro)
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

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            var uiInput = root.AddComponent<InputActionMapEnabler>();
            Connect(uiInput, "actions", actions);
            SetString(uiInput, "mapName", "UI");

            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            var pause = root.AddComponent<GamePause>();
            if (playerCombat && playerCombat.TryGetComponent<InputActionMapEnabler>(out var playerInput))
                Connect(pause, "playerInput", playerInput);

            var mainMenu = BuildMainMenu(root.transform, pause, controller, roundBanner, hud, cameraIntro);
            BuildPauseMenu(root.transform, pause, mainMenu, roundState, lapTracker);
            return root;
        }

        private static MainMenu BuildMainMenu(Transform root, GamePause pause, RoundController controller, RoundBanner roundBanner, CanvasGroup hud, CameraIntro cameraIntro)
        {
            var screen = Child(root, "MainMenuScreen");
            Stretch(screen.GetComponent<RectTransform>());
            var screenGroup = screen.AddComponent<CanvasGroup>();

            var shade = Child(screen.transform, "Shade");
            AddImage(shade, null, Image.Type.Simple, new Color(Obsidian.r, Obsidian.g, Obsidian.b, 0.72f));
            var shadeRect = shade.GetComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = new Vector2(0f, 1f);
            shadeRect.pivot = LeftCenter;
            shadeRect.sizeDelta = new Vector2(860f, 0f);
            shadeRect.anchoredPosition = Vector2.zero;

            AddFretBands(screen.transform);

            var column = Child(screen.transform, "Column");
            Place(column.GetComponent<RectTransform>(), LeftCenter, LeftCenter, new Vector2(140f, 0f), new Vector2(620f, 900f));
            var columnTransform = column.transform;

            var spiral = Child(columnTransform, "Spiral");
            AddImage(spiral, AztecUiSprites.Spiral, Image.Type.Simple, FretGold);
            PlaceInColumn(spiral, 330f, new Vector2(64f, 64f));

            var jam = Text(columnTransform, "Jam", "WSF GAMEJAM 2026", 20f, MutedColor, TextAlignmentOptions.Left);
            jam.characterSpacing = 30f;
            PlaceInColumn(jam.gameObject, 268f, new Vector2(620f, 28f));

            var title = Text(columnTransform, "Title", "UROBOROS", 118f, TextColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6f;
            PlaceInColumn(title.gameObject, 196f, new Vector2(620f, 130f));

            var tagline = Text(columnTransform, "Tagline", "Uciekaj po ciele węża przed jego własną paszczą.\nOkrąż świat i stań na czubku głowy.", 26f, MutedColor, TextAlignmentOptions.Left);
            PlaceInColumn(tagline.gameObject, 100f, new Vector2(620f, 80f));

            // Loading
            var loading = Child(columnTransform, "Loading");
            PlaceInColumn(loading, -20f, new Vector2(540f, 96f));
            var ouroboros = Child(loading.transform, "Ouroboros");
            AddImage(ouroboros, AztecUiSprites.Ouroboros, Image.Type.Simple, Turquoise);
            Place(ouroboros.GetComponent<RectTransform>(), LeftCenter, LeftCenter, Vector2.zero, new Vector2(96f, 96f));
            ouroboros.AddComponent<UiSpinner>();
            var tip = Text(loading.transform, "Tip", "Budzenie węża…", 22f, MutedColor, TextAlignmentOptions.Left);
            Place(tip.rectTransform, LeftCenter, LeftCenter, new Vector2(120f, 14f), new Vector2(420f, 30f));
            var barBackground = Child(loading.transform, "Bar");
            AddImage(barBackground, AztecUiSprites.Panel, Image.Type.Sliced, Track);
            Place(barBackground.GetComponent<RectTransform>(), LeftCenter, LeftCenter, new Vector2(120f, -18f), new Vector2(420f, 14f));
            var barFill = Child(barBackground.transform, "Fill");
            var barFillImage = AddImage(barFill, null, Image.Type.Filled, Gold);
            barFillImage.fillMethod = Image.FillMethod.Horizontal;
            barFillImage.fillAmount = 0f;
            Stretch(barFill.GetComponent<RectTransform>(), 3f);

            // Menu
            var panel = Child(columnTransform, "Menu");
            PlaceInColumn(panel, -170f, new Vector2(620f, 400f));
            var panelGroup = panel.AddComponent<CanvasGroup>();
            var panelTransform = panel.transform;

            var classCaption = Text(panelTransform, "ClassCaption", "KLASA", 18f, MutedColor, TextAlignmentOptions.Left);
            classCaption.characterSpacing = 24f;
            Place(classCaption.rectTransform, TopLeft, TopLeft, Vector2.zero, new Vector2(540f, 26f));

            var classes = new[]
            {
                (AssetDatabase.LoadAssetAtPath<PlayerClass>(MageClassPath), "Mag", "pociski i cztery czary"),
                (AssetDatabase.LoadAssetAtPath<PlayerClass>(WarriorClassPath), "Wojownik", "miecz, tarcza, unik"),
            };

            var classButtons = new Button[classes.Length];
            var classFrames = new Image[classes.Length];
            for (int i = 0; i < classes.Length; i++)
            {
                var button = ClassButton(panelTransform, "Class" + (i + 1), classes[i].Item2, classes[i].Item3, out var frame);
                Place(button.GetComponent<RectTransform>(), TopLeft, TopLeft, new Vector2(i * 280f, -36f), new Vector2(260f, 96f));
                classButtons[i] = button;
                classFrames[i] = frame;
            }

            var play = TextButton(panelTransform, "Play", "GRAJ", 30f, Gold, DarkGoldText);
            Place(play.GetComponent<RectTransform>(), TopLeft, TopLeft, new Vector2(0f, -156f), new Vector2(540f, 84f));

            var quit = TextButton(panelTransform, "Quit", "Wyjście", 24f, ButtonColor, TextColor);
            Place(quit.GetComponent<RectTransform>(), TopLeft, TopLeft, new Vector2(0f, -256f), new Vector2(540f, 64f));

            var record = Text(panelTransform, "Record", "Rekord: 0", 22f, MutedColor, TextAlignmentOptions.Left);
            Place(record.rectTransform, TopLeft, TopLeft, new Vector2(0f, -340f), new Vector2(540f, 30f));

            var mainMenu = root.gameObject.AddComponent<MainMenu>();
            Connect(mainMenu, "pause", pause);
            Connect(mainMenu, "cameraIntro", cameraIntro);
            Connect(mainMenu, "roundController", controller);
            Connect(mainMenu, "playerClass", Object.FindAnyObjectByType<PlayerClassApplier>());
            Connect(mainMenu, "roundBanner", roundBanner);
            Connect(mainMenu, "menuScreen", screenGroup);
            Connect(mainMenu, "hud", hud);
            Connect(mainMenu, "loadingPanel", loading);
            Connect(mainMenu, "loadingFill", barFillImage);
            Connect(mainMenu, "loadingTip", tip);
            Connect(mainMenu, "menuPanel", panelGroup);
            Connect(mainMenu, "playButton", play);
            Connect(mainMenu, "quitButton", quit);
            Connect(mainMenu, "recordLabel", record);

            var serialized = new SerializedObject(mainMenu);
            serialized.FindProperty("selectedFrameColor").colorValue = Gold;
            serialized.FindProperty("idleFrameColor").colorValue = IdleFrameColor;
            var options = serialized.FindProperty("classOptions");
            options.arraySize = classes.Length;
            for (int i = 0; i < classes.Length; i++)
            {
                var option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("playerClass").objectReferenceValue = classes[i].Item1;
                option.FindPropertyRelative("button").objectReferenceValue = classButtons[i];
                option.FindPropertyRelative("frame").objectReferenceValue = classFrames[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return mainMenu;
        }

        private static void BuildPauseMenu(Transform root, GamePause pause, MainMenu mainMenu, RoundState roundState, LapTracker lapTracker)
        {
            var screen = Child(root, "PauseScreen");
            AddImage(screen, null, Image.Type.Simple, new Color(0.02f, 0.02f, 0.02f, 0.75f));
            Stretch(screen.GetComponent<RectTransform>());
            var group = screen.AddComponent<CanvasGroup>();

            var sun = Child(screen.transform, "SunStone");
            AddImage(sun, AztecUiSprites.SunStone, Image.Type.Simple, new Color(FretGold.r, FretGold.g, FretGold.b, 0.12f));
            Place(sun.GetComponent<RectTransform>(), Center, Center, Vector2.zero, new Vector2(960f, 960f));
            AddFretBands(screen.transform);

            var title = Text(screen.transform, "Title", "PAUZA", 84f, TextColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 10f;
            Place(title.rectTransform, Center, Center, new Vector2(-300f, 210f), new Vector2(480f, 100f));

            var resume = TextButton(screen.transform, "Resume", "WRÓĆ DO GRY", 26f, Gold, DarkGoldText);
            Place(resume.GetComponent<RectTransform>(), Center, Center, new Vector2(-300f, 90f), new Vector2(480f, 78f));
            var restart = TextButton(screen.transform, "Restart", "Zacznij od nowa", 24f, ButtonColor, TextColor);
            Place(restart.GetComponent<RectTransform>(), Center, Center, new Vector2(-300f, 0f), new Vector2(480f, 70f));
            var quit = TextButton(screen.transform, "Quit", "Wyjście", 24f, ButtonColor, DangerText);
            Place(quit.GetComponent<RectTransform>(), Center, Center, new Vector2(-300f, -86f), new Vector2(480f, 70f));

            var stats = Child(screen.transform, "Run");
            Panel(stats, new Vector2(520f, 300f));
            Place(stats.GetComponent<RectTransform>(), Center, Center, new Vector2(320f, 40f), new Vector2(520f, 300f));
            var statsCaption = Text(stats.transform, "Caption", "TO PRZEJŚCIE", 18f, MutedColor, TextAlignmentOptions.Left);
            statsCaption.characterSpacing = 24f;
            Place(statsCaption.rectTransform, TopLeft, TopLeft, new Vector2(36f, -32f), new Vector2(440f, 26f));
            var roundCaption = Text(stats.transform, "RoundCaption", "Runda", 22f, MutedColor, TextAlignmentOptions.Left);
            Place(roundCaption.rectTransform, TopLeft, TopLeft, new Vector2(36f, -90f), new Vector2(200f, 30f));
            var roundValue = Text(stats.transform, "RoundValue", "1", 64f, TextColor, TextAlignmentOptions.Left);
            roundValue.fontStyle = FontStyles.Bold;
            Place(roundValue.rectTransform, TopLeft, TopLeft, new Vector2(36f, -122f), new Vector2(200f, 80f));
            var lapCaption = Text(stats.transform, "LapCaption", "Okrążenie", 22f, MutedColor, TextAlignmentOptions.Left);
            Place(lapCaption.rectTransform, TopLeft, TopLeft, new Vector2(270f, -90f), new Vector2(220f, 30f));
            var lapValue = Text(stats.transform, "LapValue", "0%", 64f, Turquoise, TextAlignmentOptions.Left);
            lapValue.fontStyle = FontStyles.Bold;
            Place(lapValue.rectTransform, TopLeft, TopLeft, new Vector2(270f, -122f), new Vector2(220f, 80f));

            var pauseMenu = root.gameObject.AddComponent<PauseMenu>();
            Connect(pauseMenu, "pause", pause);
            Connect(pauseMenu, "mainMenu", mainMenu);
            Connect(pauseMenu, "toggleAction", PlayerSetupMenu.FindActionReference("UI", "Cancel"));
            Connect(pauseMenu, "screen", group);
            Connect(pauseMenu, "resumeButton", resume);
            Connect(pauseMenu, "restartButton", restart);
            Connect(pauseMenu, "quitButton", quit);
            Connect(pauseMenu, "roundState", roundState);
            Connect(pauseMenu, "lapTracker", lapTracker);
            Connect(pauseMenu, "roundLabel", roundValue);
            Connect(pauseMenu, "lapLabel", lapValue);
            screen.SetActive(false);
        }

        // ---------- Scene ----------

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

        // The lap ring, the jaw warning and the pause menu read the lap. The round loop no longer measures it
        // (the weak point ends the round), so the tracker follows the rounds by itself.
        private static LapTracker SetUpLapTracker(RoundController controller)
        {
            var lapTracker = Object.FindAnyObjectByType<LapTracker>();
            if (lapTracker == null)
                lapTracker = Undo.AddComponent<LapTracker>(controller.gameObject);

            Connect(lapTracker, "head", Object.FindAnyObjectByType<HeadMovement>());
            var playerCombat = Object.FindAnyObjectByType<PlayerCombat>();
            if (playerCombat)
                Connect(lapTracker, "player", playerCombat.GetComponent<TorusObject>());

            Connect(lapTracker, "roundController", controller);
            return lapTracker;
        }

        private static CameraIntro SetUpCameraIntro()
        {
            var followCamera = Object.FindAnyObjectByType<TorusFollowCamera>();
            if (followCamera == null)
                return null;

            if (followCamera.TryGetComponent<CameraIntro>(out var intro) == false)
                intro = Undo.AddComponent<CameraIntro>(followCamera.gameObject);

            Connect(intro, "world", Object.FindAnyObjectByType<TorusWorld>());
            return intro;
        }

        // ---------- Building blocks ----------

        private static Button TextButton(Transform parent, string objectName, string label, float fontSize, Color background, Color textColor)
        {
            var buttonObject = Child(parent, objectName);
            var image = AddImage(buttonObject, AztecUiSprites.Panel, Image.Type.Sliced, background);
            image.raycastTarget = true;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ButtonColors();

            var frame = Child(buttonObject.transform, "Frame");
            AddImage(frame, AztecUiSprites.PanelFrame, Image.Type.Sliced, FrameColor);
            Stretch(frame.GetComponent<RectTransform>());

            var text = Text(buttonObject.transform, "Label", label, fontSize, textColor, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 12f;
            return button;
        }

        private static Button ClassButton(Transform parent, string objectName, string className, string description, out Image frame)
        {
            var buttonObject = Child(parent, objectName);
            var image = AddImage(buttonObject, AztecUiSprites.Panel, Image.Type.Sliced, ButtonColor);
            image.raycastTarget = true;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = ButtonColors();

            var frameObject = Child(buttonObject.transform, "Frame");
            frame = AddImage(frameObject, AztecUiSprites.PanelFrame, Image.Type.Sliced, IdleFrameColor);
            Stretch(frameObject.GetComponent<RectTransform>());

            var nameText = Text(buttonObject.transform, "Name", className, 30f, TextColor, TextAlignmentOptions.Left);
            nameText.fontStyle = FontStyles.Bold;
            Place(nameText.rectTransform, TopLeft, TopLeft, new Vector2(20f, -14f), new Vector2(230f, 40f));
            var descriptionText = Text(buttonObject.transform, "Description", description, 18f, MutedColor, TextAlignmentOptions.Left);
            Place(descriptionText.rectTransform, TopLeft, TopLeft, new Vector2(20f, -56f), new Vector2(230f, 26f));
            return button;
        }

        private static ColorBlock ButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static void Panel(GameObject panel, Vector2 size)
        {
            if (panel.TryGetComponent<RectTransform>(out var rect) == false)
                rect = panel.AddComponent<RectTransform>();

            rect.sizeDelta = size;
            AddImage(panel, AztecUiSprites.Panel, Image.Type.Sliced, PanelColor);
            var frame = Child(panel.transform, "Frame");
            AddImage(frame, AztecUiSprites.PanelFrame, Image.Type.Sliced, FrameColor);
            Stretch(frame.GetComponent<RectTransform>());
        }

        private static Image AddImage(GameObject target, Sprite sprite, Image.Type type, Color color)
        {
            if (target.TryGetComponent<RectTransform>(out _) == false)
                target.AddComponent<RectTransform>();

            var image = GetOrAdd<Image>(target);
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(Transform parent, string objectName, string text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var label = HudSetupMenu.CreateText(objectName, parent, fontSize, alignment, text);
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static GameObject Child(Transform parent, string objectName)
        {
            var child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Remove(Transform parent, string objectName)
        {
            var old = parent.Find(objectName);
            if (old)
                Object.DestroyImmediate(old.gameObject);
        }

        // Removes the child made by an earlier run and makes an empty one in its place.
        private static GameObject Recreate(Transform parent, string objectName)
        {
            var old = parent.Find(objectName);
            int siblingIndex = -1;
            if (old)
            {
                siblingIndex = old.GetSiblingIndex();
                Object.DestroyImmediate(old.gameObject);
            }

            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            if (siblingIndex >= 0)
                child.transform.SetSiblingIndex(siblingIndex);

            return child;
        }

        private static void PlaceInColumn(GameObject target, float y, Vector2 size)
        {
            Place(target.GetComponent<RectTransform>(), LeftCenter, LeftCenter, new Vector2(0f, y), size);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            HudSetupMenu.Stretch(rect);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetImageColor(Transform target, Color color)
        {
            if (target && target.TryGetComponent<Image>(out var image))
                image.color = color;
        }

        private static void SetText(Transform target, string text, Color color)
        {
            if (target == null || target.TryGetComponent<TMP_Text>(out var label) == false)
                return;

            if (text != null)
                label.text = text;

            label.color = color;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out var component) ? component : target.AddComponent<T>();
        }

        private static void Connect(Component component, string propertyName, Object target)
        {
            if (component)
                HudSetupMenu.Connect(component, propertyName, target);
        }

        private static void SetString(Component component, string propertyName, string value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
