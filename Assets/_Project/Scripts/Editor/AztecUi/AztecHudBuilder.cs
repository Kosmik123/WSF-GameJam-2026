using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WSFGameJam2026.Hud;
using WSFGameJam2026.Player;
using WSFGameJam2026.Progression;
using WSFGameJam2026.Rounds;
using WSFGameJam2026.Skills;
using static WSFGameJam2026.Editor.AztecUi.UiKit;

namespace WSFGameJam2026.Editor.AztecUi
{
    /// <summary>
    /// Builds the HUD of the design "HUD w trakcie gry": the player panel (level badge, class, health, experience),
    /// the round number with dots and the record, the lap ring, the jaw warning, the arrow to the jaw,
    /// the skill bar and the round banner. Objects made by an earlier run are replaced.
    /// </summary>
    internal static class AztecHudBuilder
    {
        private static readonly string[] OwnObjects = { "PlayerPanel", "RoundCaption", "RoundDots", "RoundRecord", "LapRing", "MawWarning", "MawDistance", "RoundBanner", "SkillBar" };

        public static RoundBanner Build(Transform canvas, HealthBar healthBar, RoundState roundState, RoundController controller, LapTracker lapTracker, PlayerLevel playerLevel)
        {
            // The health bar may sit in the panel made by an earlier run; take it out before the panel is removed.
            healthBar.transform.SetParent(canvas, false);
            foreach (var skillBar in Object.FindObjectsByType<SkillBar>(FindObjectsInactive.Include))
                Object.DestroyImmediate(skillBar.gameObject);

            foreach (string objectName in OwnObjects)
                Remove(canvas, objectName);

            BuildMawWarning(canvas, lapTracker);
            BuildPlayerPanel(canvas, healthBar, playerLevel);
            BuildRoundNumber(canvas, roundState, controller);
            BuildLapPanel(canvas, lapTracker);
            BuildMawDistance(canvas, lapTracker);
            BuildSkillBar(canvas);
            return BuildRoundBanner(canvas, roundState, controller);
        }

        // Places a rect inside a panel by its top-left corner, in design pixels (y grows down, like in the design).
        private static RectTransform At(RectTransform rect, float x, float y, float width, float height)
        {
            return Place(rect, TopLeft, TopLeft, new Vector2(Px(x), -Px(y)), new Vector2(Px(width), Px(height)));
        }

        private static void BuildPlayerPanel(Transform canvas, HealthBar healthBar, PlayerLevel playerLevel)
        {
            var panel = Rect(canvas, "PlayerPanel");
            Frame(panel, PanelFill, FrameLine);
            Place(panel, TopLeft, TopLeft, new Vector2(Px(28f), -Px(24f)), new Vector2(Px(442f), Px(104f)));

            // Level badge: the sun rays, the disc with its gold ring, "POZ." and the level.
            var badge = At(Rect(panel, "LevelBadge"), 14f, 14f, 76f, 76f);
            Layer(badge, "Rays", AztecUiSprites.SunRays, FretGold);
            var disc = Place(Rect(badge, "Disc"), Center, Center, Vector2.zero, new Vector2(Px(56f), Px(56f)));
            Paint(disc, AztecUiSprites.Dot, SlotFill);
            Layer(disc, "Ring", AztecUiSprites.ThinRing, Gold);
            var levelCaption = Label(disc, "Caption", "POZ.", 10f, Muted, spacingEm: 0.1f, alignment: TextAlignmentOptions.Center);
            Place(levelCaption.rectTransform, Center, Center, new Vector2(0f, Px(9f)), new Vector2(Px(56f), Px(13f)));
            var levelNumber = Label(disc, "Level", "1", 22f, Gold, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
            Place(levelNumber.rectTransform, Center, Center, new Vector2(0f, -Px(6f)), new Vector2(Px(56f), Px(24f)));

            // Class and health text above the bar.
            var className = Label(panel, "Class", "MAG", 12f, Muted, bold: true, spacingEm: 0.12f);
            At(className.rectTransform, 104f, 21.5f, 200f, 17f);
            var classLabel = className.gameObject.AddComponent<ClassLabel>();
            AztecUiData.Set(classLabel, "playerClass", Object.FindAnyObjectByType<PlayerClassApplier>());
            AztecUiData.Set(classLabel, "label", className);

            var healthText = Label(panel, "Health", "100 / 100", 14f, Ivory, bold: true, alignment: TextAlignmentOptions.Right);
            At(healthText.rectTransform, 224f, 21.5f, 200f, 17f);

            // The health bar: track, trail, fill and the turquoise mosaic over them.
            var bar = (RectTransform)healthBar.transform;
            bar.SetParent(panel, false);
            bar.name = "HealthBar";
            At(bar, 104f, 46.5f, 320f, 14f);
            if (bar.TryGetComponent<Image>(out var track))
            {
                track.sprite = null;
                track.type = UnityEngine.UI.Image.Type.Simple;
                track.color = Track;
            }

            for (int i = bar.childCount - 1; i >= 0; i--)
            {
                var child = bar.GetChild(i);
                if (child.name != "Fill" && child.name != "Trail")
                    Object.DestroyImmediate(child.gameObject);
            }

            StyleBarPart(bar.Find("Trail"), Trail, 0);
            StyleBarPart(bar.Find("Fill"), Turquoise, 1);
            var mosaic = Layer(bar, "Mosaic", AztecUiSprites.Mosaic, new Color(0.04f, 0.03f, 0.03f, 0.85f), UnityEngine.UI.Image.Type.Tiled);
            mosaic.pixelsPerUnitMultiplier = 1f / Scale;
            AztecUiData.Set(healthBar, "label", healthText);

            // Experience bar and its text.
            var experienceTrack = At(Rect(panel, "Experience"), 104f, 73f, 260f, 5f);
            Paint(experienceTrack, AztecUiSprites.Pill, Track, UnityEngine.UI.Image.Type.Sliced);
            var experienceFill = FillFromLeft(Rect(experienceTrack, "Fill"), 0f);
            Paint(experienceFill, AztecUiSprites.Pill, Gold, UnityEngine.UI.Image.Type.Sliced);
            var experienceText = Label(panel, "ExperienceText", "XP 0 / 60", 11f, Muted);
            At(experienceText.rectTransform, 374f, 68.5f, 70f, 14f);

            var levelView = panel.gameObject.AddComponent<PlayerLevelView>();
            AztecUiData.Set(levelView, "playerLevel", playerLevel);
            AztecUiData.Set(levelView, "levelLabel", levelNumber);
            AztecUiData.Set(levelView, "experienceFill", experienceFill);
            AztecUiData.Set(levelView, "experienceLabel", experienceText);
        }

        private static void StyleBarPart(Transform part, Color color, int siblingIndex)
        {
            if (part == null)
                return;

            part.SetSiblingIndex(siblingIndex);
            if (part.TryGetComponent<Image>(out var image))
            {
                image.sprite = null;
                image.type = UnityEngine.UI.Image.Type.Simple;
                image.color = color;
            }
        }

        private static void BuildRoundNumber(Transform canvas, RoundState roundState, RoundController controller)
        {
            var caption = Label(canvas, "RoundCaption", "RUNDA", 11f, Muted, spacingEm: 0.3f, alignment: TextAlignmentOptions.Center);
            Place(caption.rectTransform, TopCenter, TopCenter, new Vector2(0f, -Px(22f)), new Vector2(Px(200f), Px(14f)));

            var roundLabel = Object.FindAnyObjectByType<RoundLabel>();
            if (roundLabel)
            {
                roundLabel.transform.SetParent(canvas, false);
                var number = roundLabel.GetComponent<TMP_Text>();
                if (number)
                {
                    if (HeadingFont)
                        number.font = HeadingFont;

                    number.fontSize = Px(40f);
                    number.fontStyle = FontStyles.Bold;
                    number.color = Ivory;
                    number.characterSpacing = 0f;
                    number.alignment = TextAlignmentOptions.Center;
                    number.textWrappingMode = TextWrappingModes.NoWrap;
                    Place(number.rectTransform, TopCenter, TopCenter, new Vector2(0f, -Px(39f)), new Vector2(Px(200f), Px(44f)));
                }

                var serialized = new SerializedObject(roundLabel);
                serialized.FindProperty("format").stringValue = "{0}";
                serialized.ApplyModifiedProperties();
            }

            var dots = Row(canvas, "RoundDots", 6f, TextAnchor.MiddleCenter);
            Place(dots, TopCenter, TopCenter, new Vector2(0f, -Px(87f)), new Vector2(Px(200f), Px(7f)));
            const int DotCount = 10;
            var dotObjects = new GameObject[DotCount];
            for (int i = 0; i < DotCount; i++)
            {
                var dot = Rect(dots, "Dot" + (i + 1));
                Paint(dot, AztecUiSprites.Dot, FretGold);
                Size(dot, 7f, 7f);
                dotObjects[i] = dot.gameObject;
            }

            var roundDots = dots.gameObject.AddComponent<RoundDots>();
            AztecUiData.Set(roundDots, "roundState", roundState);
            var serializedDots = new SerializedObject(roundDots);
            var dotsProperty = serializedDots.FindProperty("dots");
            dotsProperty.arraySize = DotCount;
            for (int i = 0; i < DotCount; i++)
                dotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = dotObjects[i];

            serializedDots.ApplyModifiedProperties();

            var record = Label(canvas, "RoundRecord", "rekord: 0", 12f, Muted, alignment: TextAlignmentOptions.Center);
            Place(record.rectTransform, TopCenter, TopCenter, new Vector2(0f, -Px(100f)), new Vector2(Px(200f), Px(16f)));
            var recordLabel = record.gameObject.AddComponent<RecordLabel>();
            AztecUiData.Set(recordLabel, "controller", controller);
            AztecUiData.Set(recordLabel, "label", record);
        }

        private static void BuildLapPanel(Transform canvas, LapTracker lapTracker)
        {
            var panel = Rect(canvas, "LapRing");
            Frame(panel, PanelFill, FrameLine);
            Place(panel, TopRight, TopRight, new Vector2(-Px(28f), -Px(24f)), new Vector2(Px(307f), Px(148f)));

            var lapCaption = Label(panel, "LapCaption", "OKRĄŻENIE", 11f, Muted, spacingEm: 0.14f, alignment: TextAlignmentOptions.Right);
            At(lapCaption.rectTransform, 18f, 29f, 135f, 13f);
            var lapValue = Label(panel, "LapValue", "0%", 22f, Ivory, bold: true, alignment: TextAlignmentOptions.Right);
            At(lapValue.rectTransform, 18f, 42f, 135f, 27f);
            var distanceCaption = Label(panel, "DistanceCaption", "PASZCZA ZA TOBĄ", 11f, Muted, spacingEm: 0.14f, alignment: TextAlignmentOptions.Right);
            At(distanceCaption.rectTransform, 18f, 79f, 135f, 13f);
            var distanceValue = Label(panel, "DistanceValue", "0 m", 22f, Ivory, bold: true, alignment: TextAlignmentOptions.Right);
            At(distanceValue.rectTransform, 18f, 92f, 135f, 27f);

            // The ring: 20 marks, the track, the lead of the player, the jaw at the top, the goal and the player.
            var ring = At(Rect(panel, "Ring"), 169f, 14f, 120f, 120f);
            var ticks = Place(Rect(ring, "Ticks"), Center, Center, Vector2.zero, new Vector2(Px(118f), Px(118f)));
            Paint(ticks, AztecUiSprites.RingTicks, WithAlpha(FretGold, 0.6f));
            var trackRect = Place(Rect(ring, "Track"), Center, Center, Vector2.zero, new Vector2(Px(105f), Px(105f)));
            Paint(trackRect, AztecUiSprites.Ring, RingTrack);
            var fill = RadialRing(ring, "Fill", WithAlpha(Turquoise, 0.55f), 0f);
            RadialRing(ring, "Jaw", MawRed, 0.035f);

            var goalPosition = new Vector2(Mathf.Sin(-20f * Mathf.Deg2Rad), Mathf.Cos(-20f * Mathf.Deg2Rad)) * Px(46f);
            Marker(ring, "Goal", goalPosition, Obsidian, Gold);

            var pivot = Stretch(Rect(ring, "PlayerPivot"));
            Marker(pivot, "Player", new Vector2(0f, Px(46f)), Ivory, Obsidian);

            var goalText = Label(ring, "GoalText", "CEL", 11f, Muted, heading: true, alignment: TextAlignmentOptions.Center);
            Place(goalText.rectTransform, Center, Center, Vector2.zero, new Vector2(Px(80f), Px(14f)));

            var lapRing = panel.gameObject.AddComponent<LapRing>();
            AztecUiData.Set(lapRing, "lapTracker", lapTracker);
            AztecUiData.Set(lapRing, "progressFill", fill);
            AztecUiData.Set(lapRing, "playerMarkerPivot", pivot);
            AztecUiData.Set(lapRing, "progressLabel", lapValue);
            AztecUiData.Set(lapRing, "distanceLabel", distanceValue);
            var serialized = new SerializedObject(lapRing);
            serialized.FindProperty("safeColor").colorValue = WithAlpha(Turquoise, 0.55f);
            serialized.FindProperty("dangerColor").colorValue = MawRed;
            serialized.ApplyModifiedProperties();
        }

        private static Image RadialRing(RectTransform ring, string objectName, Color color, float amount)
        {
            var rect = Place(Rect(ring, objectName), Center, Center, Vector2.zero, new Vector2(Px(105f), Px(105f)));
            var image = Paint(rect, AztecUiSprites.Ring, color, UnityEngine.UI.Image.Type.Filled);
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = amount;
            return image;
        }

        // A round marker on the ring (r = 7 in the design): a filled disc and a ring around it.
        private static void Marker(RectTransform parent, string objectName, Vector2 centerOffset, Color fill, Color outline)
        {
            var marker = Place(Rect(parent, objectName), Center, Center, centerOffset, new Vector2(Px(15f), Px(15f)));
            Paint(marker, AztecUiSprites.Dot, fill);
            Layer(marker, "Outline", AztecUiSprites.DotOutline, outline);
        }

        private static void BuildMawWarning(Transform canvas, LapTracker lapTracker)
        {
            var warning = Stretch(Rect(canvas, "MawWarning"));
            warning.SetAsFirstSibling();
            var group = warning.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Layer(warning, "Vignette", AztecUiSprites.Vignette, WithAlpha(MawRed, 0.55f));

            var banner = Place(Rect(warning, "Banner"), TopCenter, TopCenter, new Vector2(0f, -Px(132f)), new Vector2(Px(300f), Px(38f)));
            Pill(banner, WithAlpha(MawRed, 0.92f));
            var text = Label(banner, "Text", "PASZCZA TUŻ ZA TOBĄ", 15f, Color.white, bold: true, spacingEm: 0.16f, alignment: TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            var mawWarning = warning.gameObject.AddComponent<MawWarning>();
            AztecUiData.Set(mawWarning, "lapTracker", lapTracker);
            AztecUiData.Set(mawWarning, "group", group);
        }

        private static void BuildMawDistance(Transform canvas, LapTracker lapTracker)
        {
            var row = Row(canvas, "MawDistance", 8f, TextAnchor.MiddleCenter, fit: true);
            Place(row, BottomCenter, BottomCenter, new Vector2(0f, Px(132f)), Vector2.zero);
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var arrow = Rect(row, "Arrow");
            var arrowImage = Paint(arrow, AztecUiSprites.Chevron, Ivory);
            Size(arrow, 18f, 18f);
            var label = Label(row, "Text", "paszcza · 0 m", 13f, Ivory, bold: true);

            var distance = row.gameObject.AddComponent<MawDistanceLabel>();
            AztecUiData.Set(distance, "lapTracker", lapTracker);
            AztecUiData.Set(distance, "label", label);
            AztecUiData.Set(distance, "arrow", arrowImage);
            AztecUiData.Set(distance, "group", group);
        }

        private static void BuildSkillBar(Transform canvas)
        {
            var skillUser = Object.FindAnyObjectByType<SkillUser>();
            var bar = Row(canvas, "SkillBar", 10f, TextAnchor.LowerCenter, fit: true);
            Frame(bar, PanelFill, FrameLine);
            Padding(bar, 16f, 16f, 12f, 12f);
            Place(bar, BottomCenter, BottomCenter, new Vector2(0f, Px(26f)), Vector2.zero);

            var attack = BasicSlot(bar, "Attack", AztecUiSprites.IconShot, "LPM · strzał", out var attackIcon, out var attackLabel, out _, out _);
            BasicSlot(bar, "Dodge", AztecUiSprites.IconDodge, "Spacja · unik", out _, out _, out var dodgeCooldown, out var dodgeCooldownText);

            var separator = Rect(bar, "Separator");
            Size(separator, 13f, 78f);
            var line = Place(Rect(separator, "Line"), TopCenter, TopCenter, Vector2.zero, new Vector2(Mathf.Max(1f, Px(1f)), Px(60f)));
            Paint(line, null, Line);

            var basicActions = bar.gameObject.AddComponent<BasicActionsView>();
            AztecUiData.Set(basicActions, "playerClass", Object.FindAnyObjectByType<PlayerClassApplier>());
            AztecUiData.Set(basicActions, "playerCombat", Object.FindAnyObjectByType<PlayerCombat>());
            AztecUiData.Set(basicActions, "attackIcon", attackIcon);
            AztecUiData.Set(basicActions, "rangedIcon", AztecUiSprites.IconShot);
            AztecUiData.Set(basicActions, "meleeIcon", AztecUiSprites.IconSword);
            AztecUiData.Set(basicActions, "attackLabel", attackLabel);
            AztecUiData.Set(basicActions, "dodgeCooldown", dodgeCooldown);
            AztecUiData.Set(basicActions, "dodgeCooldownLabel", dodgeCooldownText);

            int slotCount = 4;
            if (skillUser)
                slotCount = Mathf.Max(1, skillUser.GetComponent<SkillSlots>().SlotCount);

            var views = new SkillSlotView[slotCount];
            for (int i = 0; i < slotCount; i++)
                views[i] = SkillSlot(bar, i);

            var skillBar = bar.gameObject.AddComponent<SkillBar>();
            AztecUiData.Set(skillBar, "skillUser", skillUser);
            var serialized = new SerializedObject(skillBar);
            var viewsProperty = serialized.FindProperty("views");
            viewsProperty.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++)
                viewsProperty.GetArrayElementAtIndex(i).objectReferenceValue = views[i];

            serialized.ApplyModifiedProperties();
        }

        // A 56 px slot with an icon and a label under it (attack, dodge). The cooldown cover starts hidden.
        private static RectTransform BasicSlot(RectTransform bar, string objectName, Sprite iconSprite, string text, out Image icon, out TMP_Text label, out GameObject cooldown, out TMP_Text cooldownText)
        {
            var column = Column(bar, objectName, 6f, TextAnchor.LowerCenter);
            var box = Rect(column, "Box");
            Size(box, 56f, 56f);
            Frame(box, SlotFill, Line);
            var iconRect = Place(Rect(box, "Icon"), Center, Center, Vector2.zero, new Vector2(Px(26f), Px(26f)));
            icon = Paint(iconRect, iconSprite, Ivory);

            var cover = Layer(box, "Cooldown", null, new Color(0.02f, 0.03f, 0.03f, 0.78f), inset: Px(2f));
            cooldownText = Label(cover.transform, "Seconds", "", 13f, Ivory, bold: true, alignment: TextAlignmentOptions.Center);
            Stretch(cooldownText.rectTransform);
            cooldown = cover.gameObject;
            cooldown.SetActive(false);

            label = Label(column, "Label", text, 11f, Muted, alignment: TextAlignmentOptions.Center);
            return column;
        }

        private static SkillSlotView SkillSlot(RectTransform bar, int index)
        {
            var column = Column(bar, "Skill" + (index + 1), 6f, TextAnchor.LowerCenter);
            Size(column, 84f, -1f);

            var box = Rect(column, "Box");
            Size(box, 64f, 64f);
            Paint(box, AztecUiSprites.Panel, SlotFill, UnityEngine.UI.Image.Type.Sliced);
            var iconRect = Place(Rect(box, "Icon"), Center, Center, Vector2.zero, new Vector2(Px(40f), Px(40f)));
            var icon = Paint(iconRect, null, Color.white);
            icon.enabled = false;
            var glyph = Label(box, "Glyph", "", 24f, Ivory, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
            Stretch(glyph.rectTransform);

            var cooldown = Layer(box, "Cooldown", null, new Color(0.02f, 0.03f, 0.03f, 0.78f), UnityEngine.UI.Image.Type.Filled, Px(2f));
            cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            cooldown.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
            cooldown.fillAmount = 0f;
            var seconds = Label(box, "Seconds", "", 15f, Ivory, bold: true, alignment: TextAlignmentOptions.Center);
            Stretch(seconds.rectTransform);
            var frame = Layer(box, "Frame", AztecUiSprites.PanelFrame, Line, UnityEngine.UI.Image.Type.Sliced);
            var key = Label(box, "Key", "", 11f, Ivory, bold: true);
            Place(key.rectTransform, TopLeft, TopLeft, new Vector2(Px(5f), -Px(3f)), new Vector2(Px(20f), Px(14f)));

            var pips = Row(column, "Tiers", 3f, TextAnchor.MiddleCenter);
            var pipImages = new Image[4];
            for (int i = 0; i < pipImages.Length; i++)
            {
                var pip = Rect(pips, "Tier" + (i + 1));
                pipImages[i] = Paint(pip, AztecUiSprites.Pill, Line, UnityEngine.UI.Image.Type.Sliced);
                pipImages[i].pixelsPerUnitMultiplier = 8f;
                Size(pip, 9f, 4f);
            }

            var caption = Label(column, "Name", "", 11f, Muted, alignment: TextAlignmentOptions.Center);
            caption.overflowMode = TextOverflowModes.Ellipsis;
            Size(caption.rectTransform, 84f, 14f);

            var view = column.gameObject.AddComponent<SkillSlotView>();
            AztecUiData.Set(view, "icon", icon);
            AztecUiData.Set(view, "cooldownFill", cooldown);
            AztecUiData.Set(view, "cooldownLabel", seconds);
            AztecUiData.Set(view, "glyphLabel", glyph);
            AztecUiData.Set(view, "captionLabel", caption);
            AztecUiData.Set(view, "keyLabel", key);
            AztecUiData.Set(view, "frame", frame);
            var serialized = new SerializedObject(view);
            var pipsProperty = serialized.FindProperty("tierPips");
            pipsProperty.arraySize = pipImages.Length;
            for (int i = 0; i < pipImages.Length; i++)
                pipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = pipImages[i];

            serialized.ApplyModifiedProperties();
            return view;
        }

        private static RoundBanner BuildRoundBanner(Transform canvas, RoundState roundState, RoundController controller)
        {
            var root = Place(Rect(canvas, "RoundBanner"), TopCenter, TopCenter, new Vector2(0f, -Px(170f)), new Vector2(Px(400f), Px(115f)));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var caption = Label(root, "Caption", "RUNDA", 12f, Muted, spacingEm: 0.3f, alignment: TextAlignmentOptions.Center);
            At(caption.rectTransform, 0f, 0f, 400f, 15f);
            var number = Label(root, "Number", "1", 64f, Ivory, heading: true, bold: true, alignment: TextAlignmentOptions.Center);
            At(number.rectTransform, 0f, 21f, 400f, 66f);
            var flee = Label(root, "Flee", "UCIEKAJ!", 15f, Danger, bold: true, spacingEm: 0.1f, alignment: TextAlignmentOptions.Center);
            At(flee.rectTransform, 0f, 95f, 400f, 20f);

            var banner = root.gameObject.AddComponent<RoundBanner>();
            AztecUiData.Set(banner, "controller", controller);
            AztecUiData.Set(banner, "roundState", roundState);
            AztecUiData.Set(banner, "group", group);
            AztecUiData.Set(banner, "roundLabel", number);
            return banner;
        }
    }
}
