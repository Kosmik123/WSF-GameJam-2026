using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Menus;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;
using static WSFGameJam2026.Editor.AztecUi.UiKit;

namespace WSFGameJam2026.Editor.AztecUi
{
    /// <summary>
    /// Builds the full screens of the design: "Awans: wybór karty", "Zwycięstwo rundy" and "Śmierć".
    /// </summary>
    internal static class AztecScreenBuilder
    {
        private static readonly Color ScreenDim = new Color(0.016f, 0.024f, 0.024f, 0.7f);

        public static void BuildLevelUp(Transform root, GamePause pause, PlayerLevel playerLevel)
        {
            var screen = Stretch(Rect(root, "LevelUpScreen"));
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            Dim(screen, ScreenDim);
            SunStone(screen);
            FretBands(screen);

            var column = Column(screen, "Content", 36f, TextAnchor.MiddleCenter, fit: true);
            Place(column, Center, Center, Vector2.zero, Vector2.zero);

            var header = Column(column, "Header", 8f, TextAnchor.UpperCenter);
            Label(header, "Caption", "AWANS", 12f, Gold, spacingEm: 0.3f, alignment: TextAlignmentOptions.Center);
            Spiral(header);
            var title = Label(header, "Title", "Poziom 2", 52f, Ivory, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
            var bonus = Label(header, "Bonus", "+12 HP · +6% obrażeń · wybierz jedną kartę (1 / 2 / 3)", 14f, Muted, alignment: TextAlignmentOptions.Center);

            var cardRow = Row(column, "Cards", 24f, TextAnchor.MiddleCenter);
            var cards = new LevelUpCardView[3];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = Card(cardRow, i);

            Label(column, "Footer", "Paszcza czeka — gra jest zatrzymana na czas wyboru.", 13f, Muted, alignment: TextAlignmentOptions.Center);

            var levelUp = root.gameObject.AddComponent<LevelUpScreen>();
            AztecUiData.Set(levelUp, "playerLevel", playerLevel);
            AztecUiData.Set(levelUp, "skills", Object.FindAnyObjectByType<SkillSlots>());
            AztecUiData.Set(levelUp, "pause", pause);
            AztecUiData.Set(levelUp, "screen", group);
            AztecUiData.Set(levelUp, "titleLabel", title);
            AztecUiData.Set(levelUp, "bonusLabel", bonus);
            var serialized = new SerializedObject(levelUp);
            var cardsProperty = serialized.FindProperty("cards");
            cardsProperty.arraySize = cards.Length;
            var actionsProperty = serialized.FindProperty("chooseActions");
            actionsProperty.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                cardsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
                actionsProperty.GetArrayElementAtIndex(i).objectReferenceValue = PlayerSetupMenu.FindActionReference("UI", $"Choose{i + 1}");
            }

            serialized.ApplyModifiedProperties();
            screen.gameObject.SetActive(false);
        }

        // A 280 x 360 card. The button stays in place; its content rises when the card is selected.
        private static LevelUpCardView Card(RectTransform row, int index)
        {
            var card = Rect(row, "Card" + (index + 1));
            Size(card, 280f, 360f);
            var hitArea = Paint(card, null, Color.clear);
            hitArea.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitArea;

            var content = Rect(card, "Content");
            Place(content, Center, Center, Vector2.zero, new Vector2(Px(280f), Px(360f)));
            Paint(content, AztecUiSprites.Panel, CardFill, UnityEngine.UI.Image.Type.Sliced);
            var frame = Layer(content, "Frame", AztecUiSprites.PanelFrame, Line, UnityEngine.UI.Image.Type.Sliced);
            frame.pixelsPerUnitMultiplier = 0.67f;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(Px(24f)), Mathf.RoundToInt(Px(24f)), Mathf.RoundToInt(Px(24f)), Mathf.RoundToInt(Px(24f)));
            layout.spacing = Px(14f);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var top = Row(content, "Top", 0f, TextAnchor.MiddleLeft);
            var kind = Row(top, "Kind", 0f, TextAnchor.MiddleCenter);
            Padding(kind, 10f, 10f, 5f, 5f);
            var kindBackground = Pill(kind, new Color(0.16f, 0.14f, 0.22f, 1f));
            var kindLabel = Label(kind, "Text", "NOWY SKILL", 11f, Ivory, bold: true, spacingEm: 0.16f, alignment: TextAlignmentOptions.Center);
            var spacer = Rect(top, "Spacer");
            Flexible(spacer, 1f, 0f);
            var number = Label(top, "Number", (index + 1).ToString(), 12f, Muted, alignment: TextAlignmentOptions.Right);

            var glyphBox = Rect(content, "GlyphBox");
            Size(glyphBox, -1f, 110f);
            Frame(glyphBox, GlyphBoxFill, Line);
            var glyph = Label(glyphBox, "Glyph", "", 40f, Ivory, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
            Stretch(glyph.rectTransform);
            var iconRect = Place(Rect(glyphBox, "Icon"), Center, Center, Vector2.zero, new Vector2(Px(72f), Px(72f)));
            var icon = Paint(iconRect, null, Color.white);
            icon.enabled = false;

            var skillName = Label(content, "Name", "", 22f, Ivory, heading: true, bold: true);
            var description = Paragraph(content, "Description", "", 14f, Description, 232f);

            var fill = Rect(content, "Fill");
            Flexible(fill, 0f, 1f);

            var pips = Row(content, "Tiers", 4f, TextAnchor.MiddleCenter);
            var pipImages = new Image[4];
            for (int i = 0; i < pipImages.Length; i++)
            {
                var pip = Rect(pips, "Tier" + (i + 1));
                pipImages[i] = Paint(pip, AztecUiSprites.Pill, Line, UnityEngine.UI.Image.Type.Sliced);
                pipImages[i].pixelsPerUnitMultiplier = 6f;
                Size(pip, 55f, 5f);
            }

            var view = card.gameObject.AddComponent<LevelUpCardView>();
            AztecUiData.Set(view, "kindLabel", kindLabel);
            AztecUiData.Set(view, "kindBackground", kindBackground);
            AztecUiData.Set(view, "numberLabel", number);
            AztecUiData.Set(view, "glyphLabel", glyph);
            AztecUiData.Set(view, "iconImage", icon);
            AztecUiData.Set(view, "nameLabel", skillName);
            AztecUiData.Set(view, "descriptionLabel", description);
            AztecUiData.Set(view, "content", content);
            AztecUiData.Set(view, "frame", frame);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("selectedLift").floatValue = Px(10f);
            var pipsProperty = serialized.FindProperty("tierPips");
            pipsProperty.arraySize = pipImages.Length;
            for (int i = 0; i < pipImages.Length; i++)
                pipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = pipImages[i];

            serialized.ApplyModifiedProperties();
            return view;
        }

        public static void BuildRoundWon(Transform root, RoundController controller, RunStats stats)
        {
            var screen = Stretch(Rect(root, "RoundWonScreen"));
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            Dim(screen, new Color(0.016f, 0.024f, 0.024f, 0.62f));
            SunStone(screen);
            FretBands(screen);

            var column = Column(screen, "Content", 30f, TextAnchor.MiddleCenter, fit: true);
            Place(column, Center, Center, Vector2.zero, Vector2.zero);

            // The closed loop: the lap ring, the jaw and the gold head at the top.
            var icon = Rect(column, "Loop");
            Size(icon, 132f, 132f);
            var loop = Place(Rect(icon, "Ring"), Center, Center, Vector2.zero, new Vector2(Px(119f), Px(119f)));
            var loopImage = Paint(loop, AztecUiSprites.Ring, Turquoise, UnityEngine.UI.Image.Type.Filled);
            loopImage.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            loopImage.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            loopImage.fillClockwise = true;
            loopImage.fillAmount = 0.92f;
            loop.localRotation = Quaternion.Euler(0f, 0f, -10f);
            var jaw = Place(Rect(icon, "Jaw"), Center, Center, new Vector2(Px(42f), Px(42f)), new Vector2(Px(22f), Px(22f)));
            Paint(jaw, AztecUiSprites.Triangle, MawRed);
            var head = Place(Rect(icon, "Head"), Center, Center, new Vector2(0f, Px(52f)), new Vector2(Px(16f), Px(16f)));
            Paint(head, AztecUiSprites.Dot, Gold);

            var header = Column(column, "Header", 10f, TextAnchor.UpperCenter);
            Label(header, "Caption", "PĘTLA ZAMKNIĘTA", 12f, Turquoise, spacingEm: 0.3f, alignment: TextAlignmentOptions.Center);
            Spiral(header);
            var title = Label(header, "Title", "Runda 1 ukończona", 60f, Ivory, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
            Label(header, "Subtitle", "Stoisz na czubku głowy. Koniec jest początkiem.", 15f, Muted, alignment: TextAlignmentOptions.Center);

            var tiles = Row(column, "Numbers", 16f, TextAnchor.MiddleCenter);
            var lapTime = Tile(tiles, "LapTime", "Czas okrążenia", Ivory);
            var kills = Tile(tiles, "Kills", "Pokonani wrogowie", Ivory);
            var closest = Tile(tiles, "ClosestJaw", "Najbliżej paszczy", Danger);

            var next = Row(column, "NextRound", 0f, TextAnchor.MiddleCenter);
            Frame(next, WithAlpha(MawRed, 0.1f), WithAlpha(MawRed, 0.5f));
            Padding(next, 22f, 22f, 14f, 14f);
            var nextLabel = Label(next, "Text", "", 14f, Ivory);

            var continueButton = TextButton(column, "Continue", "DALEJ", 16f, Gold, DarkGoldText, Color.clear, true, 0.14f, 300f, 54f);

            var roundWon = root.gameObject.AddComponent<RoundWonScreen>();
            AztecUiData.Set(roundWon, "controller", controller);
            AztecUiData.Set(roundWon, "stats", stats);
            AztecUiData.Set(roundWon, "screen", group);
            AztecUiData.Set(roundWon, "titleLabel", title);
            AztecUiData.Set(roundWon, "lapTimeLabel", lapTime);
            AztecUiData.Set(roundWon, "killsLabel", kills);
            AztecUiData.Set(roundWon, "closestJawLabel", closest);
            AztecUiData.Set(roundWon, "nextRoundLabel", nextLabel);
            AztecUiData.Set(roundWon, "continueButton", continueButton);
            AztecUiData.Set(roundWon, "continueLabel", continueButton.GetComponentInChildren<TMP_Text>());
            screen.gameObject.SetActive(false);
        }

        private static TMP_Text Tile(RectTransform row, string objectName, string caption, Color valueColor)
        {
            var tile = Column(row, objectName, 4f, TextAnchor.UpperLeft);
            Frame(tile, PanelFillStrong, FrameLine);
            Padding(tile, 18f, 18f, 18f, 18f);
            Size(tile, 200f, -1f);
            Label(tile, "Caption", caption, 12f, Muted);
            return Label(tile, "Value", "0", 26f, valueColor, bold: true);
        }

        public static void BuildRunEnd(Transform root, RoundController controller, RunStats stats, PlayerLevel playerLevel)
        {
            var screen = Stretch(Rect(root, "RunEndScreen"));
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            Dim(screen, new Color(0.07f, 0.03f, 0.027f, 0.94f));
            SunStone(screen);
            Layer(screen, "Vignette", AztecUiSprites.Vignette, WithAlpha(MawRed, 0.35f));
            FretBands(screen);

            var column = Column(screen, "Content", 30f, TextAnchor.MiddleCenter, fit: true);
            Place(column, Center, Center, Vector2.zero, Vector2.zero);

            var header = Column(column, "Header", 10f, TextAnchor.UpperCenter);
            var cause = Label(header, "Cause", "DOPADŁA CIĘ PASZCZA", 12f, DangerSoft, spacingEm: 0.3f, alignment: TextAlignmentOptions.Center);
            Spiral(header);
            Label(header, "Title", "Koniec pętli", 72f, Ivory, heading: true, bold: true, spacingEm: 0.04f, alignment: TextAlignmentOptions.Center);
            Label(header, "Subtitle", "Wąż zamyka pętlę. Poziom i sprzęt przepadają.", 15f, MutedWarm, alignment: TextAlignmentOptions.Center);

            var tiles = Row(column, "Result", 20f, TextAnchor.UpperCenter);
            var rounds = ResultTile(tiles, "Rounds", "PRZETRWANE RUNDY", Ivory, out _);
            var best = ResultTile(tiles, "Best", "REKORD", Gold, out var bestFrame);
            var badge = Row(best.transform.parent, "NewRecord", 0f, TextAnchor.MiddleCenter);
            Padding(badge, 10f, 10f, 4f, 4f);
            Pill(badge, Gold);
            Label(badge, "Text", "NOWY REKORD", 12f, DarkGoldText, bold: true, spacingEm: 0.14f, alignment: TextAlignmentOptions.Center);

            var summary = Label(column, "Summary", "", 14f, MutedWarm, alignment: TextAlignmentOptions.Center);

            var buttons = Row(column, "Buttons", 12f, TextAnchor.MiddleCenter);
            var restart = TextButton(buttons, "Restart", "ZACZNIJ OD NOWA", 16f, Gold, DarkGoldText, Color.clear, true, 0.14f, 250f, 54f);
            var mainMenu = TextButton(buttons, "MainMenu", "Menu główne", 15f, Color.clear, Ivory, DeathBorder, false, 0f, 180f, 54f);

            var runEnd = root.gameObject.AddComponent<RunEndScreen>();
            AztecUiData.Set(runEnd, "controller", controller);
            AztecUiData.Set(runEnd, "stats", stats);
            AztecUiData.Set(runEnd, "playerLevel", playerLevel);
            AztecUiData.Set(runEnd, "playerClass", Object.FindAnyObjectByType<PlayerClassApplier>());
            AztecUiData.Set(runEnd, "screen", group);
            AztecUiData.Set(runEnd, "causeLabel", cause);
            AztecUiData.Set(runEnd, "roundsLabel", rounds);
            AztecUiData.Set(runEnd, "bestLabel", best);
            AztecUiData.Set(runEnd, "newRecordBadge", badge.gameObject);
            AztecUiData.Set(runEnd, "bestFrame", bestFrame);
            AztecUiData.Set(runEnd, "summaryLabel", summary);
            AztecUiData.Set(runEnd, "restartButton", restart);
            AztecUiData.Set(runEnd, "mainMenuButton", mainMenu);
            var serialized = new SerializedObject(runEnd);
            serialized.FindProperty("bestFrameColor").colorValue = FrameLine;
            serialized.FindProperty("newRecordFrameColor").colorValue = Gold;
            serialized.ApplyModifiedProperties();
            screen.gameObject.SetActive(false);
        }

        private static TMP_Text ResultTile(RectTransform row, string objectName, string caption, Color valueColor, out Image frame)
        {
            var tile = Column(row, objectName, 4f, TextAnchor.UpperCenter);
            Paint(tile, AztecUiSprites.Panel, new Color(0.04f, 0.024f, 0.024f, 0.85f), UnityEngine.UI.Image.Type.Sliced);
            frame = Layer(tile, "Frame", AztecUiSprites.PanelFrame, FrameLine, UnityEngine.UI.Image.Type.Sliced);
            Padding(tile, 22f, 22f, 22f, 22f);
            Size(tile, 240f, -1f);
            Label(tile, "Caption", caption, 12f, MutedWarm, spacingEm: 0.16f, alignment: TextAlignmentOptions.Center);
            return Label(tile, "Value", "0", 64f, valueColor, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
        }
    }
}
