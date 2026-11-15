using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WSFGameJam2026.Editor.AztecUi
{
    /// <summary>
    /// Building blocks of the Aztec inspired UI: the palette and fonts of the design, and helpers that make panels,
    /// texts, buttons and layout groups. Sizes are given in pixels of the design (1280 x 720) and scaled with
    /// <see cref="Px"/> to the 1920 x 1080 reference resolution of the canvases.
    /// </summary>
    internal static class UiKit
    {
        public const float Scale = 1.5f;

        public static readonly Color Obsidian = Hex(0x0F0C0A);
        public static readonly Color PanelFill = new Color(0.055f, 0.04f, 0.03f, 0.74f);
        public static readonly Color PanelFillStrong = new Color(0.055f, 0.04f, 0.03f, 0.85f);
        public static readonly Color SlotFill = Hex(0x1A1512);
        public static readonly Color CardFill = Hex(0x17120F);
        public static readonly Color GlyphBoxFill = Hex(0x120E0C);
        public static readonly Color FrameLine = new Color(0.93f, 0.9f, 0.84f, 0.14f);
        public static readonly Color Line = Hex(0x3B302A);
        public static readonly Color Ivory = Hex(0xEDE6D6);
        public static readonly Color Muted = Hex(0xA9A493);
        public static readonly Color MutedWarm = Hex(0xB9AFA3);
        public static readonly Color Description = Hex(0xC9C3B2);
        public static readonly Color Gold = Hex(0xE3A23B);
        public static readonly Color FretGold = Hex(0xC9A24A);
        public static readonly Color DarkGoldText = Hex(0x1A1206);
        public static readonly Color Turquoise = Hex(0x36B3A0);
        public static readonly Color Trail = Hex(0xF0C68A);
        public static readonly Color Track = Hex(0x2A211C);
        public static readonly Color RingTrack = Hex(0x2E2520);
        public static readonly Color MawRed = Hex(0xE0483C);
        public static readonly Color Danger = Hex(0xFF6B5E);
        public static readonly Color DangerSoft = Hex(0xFF8A7F);
        public static readonly Color DeathBorder = Hex(0x4A3532);

        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 LeftCenter = new Vector2(0f, 0.5f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        /// <summary>Font of the titles and numbers (Cinzel in the design). Set by <see cref="AztecUiFonts"/>.</summary>
        public static TMP_FontAsset HeadingFont { get; set; }

        /// <summary>Font of everything else (Inter in the design). Set by <see cref="AztecUiFonts"/>.</summary>
        public static TMP_FontAsset BodyFont { get; set; }

        /// <summary>Pixels of the design to pixels of the canvas.</summary>
        public static float Px(float designPixels)
        {
            return designPixels * Scale;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        // ---------- Objects and rects ----------

        public static RectTransform Rect(Transform parent, string objectName)
        {
            var child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        public static void Remove(Transform parent, string objectName)
        {
            var old = parent.Find(objectName);
            if (old)
                Object.DestroyImmediate(old.gameObject);
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>Stretches over the left part of the parent, from the left edge to <paramref name="value"/> (0-1).</summary>
        public static RectTransform FillFromLeft(RectTransform rect, float value)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(value, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        // ---------- Graphics ----------

        public static Image Paint(RectTransform rect, Sprite sprite, Color color, Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            if (rect.TryGetComponent<Image>(out var image) == false)
                image = rect.gameObject.AddComponent<Image>();

            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A child image stretched over its parent.</summary>
        public static Image Layer(Transform parent, string objectName, Sprite sprite, Color color, Image.Type type = UnityEngine.UI.Image.Type.Simple, float inset = 0f)
        {
            var rect = Stretch(Rect(parent, objectName), inset);
            IgnoreLayout(rect);
            return Paint(rect, sprite, color, type);
        }

        /// <summary>A panel with stepped corners and a thin frame (the "step" panels of the design).</summary>
        public static Image Frame(RectTransform panel, Color fill, Color frame)
        {
            var image = Paint(panel, AztecUiSprites.Panel, fill, UnityEngine.UI.Image.Type.Sliced);
            Layer(panel, "Frame", AztecUiSprites.PanelFrame, frame, UnityEngine.UI.Image.Type.Sliced);
            return image;
        }

        public static Image Pill(RectTransform rect, Color color)
        {
            return Paint(rect, AztecUiSprites.Pill, color, UnityEngine.UI.Image.Type.Sliced);
        }

        // ---------- Text ----------

        public static TMP_Text Label(Transform parent, string objectName, string text, float designSize, Color color, bool heading = false, bool bold = false, float spacingEm = 0f, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Rect(parent, objectName);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = heading ? HeadingFont : BodyFont;
            if (font)
                label.font = font;

            label.text = text;
            label.fontSize = Px(designSize);
            label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.characterSpacing = spacingEm * 100f;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        /// <summary>A text that wraps inside the given width (design pixels).</summary>
        public static TMP_Text Paragraph(Transform parent, string objectName, string text, float designSize, Color color, float designWidth, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = Label(parent, objectName, text, designSize, color, alignment: alignment);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.lineSpacing = 10f;
            Size(label.rectTransform, designWidth, -1f);
            return label;
        }

        // ---------- Buttons ----------

        public static ColorBlock ButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            return colors;
        }

        /// <summary>For buttons without a fill: invisible at rest, a faint light wash when selected or under the mouse.</summary>
        public static ColorBlock OutlineButtonColors()
        {
            var colors = ButtonColors();
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.08f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.15f);
            return colors;
        }

        /// <summary>A button with stepped corners. <paramref name="outline"/> draws a frame (pass a clear color for none).</summary>
        public static Button TextButton(Transform parent, string objectName, string text, float designFontSize, Color fill, Color textColor, Color outline, bool bold, float spacingEm, float designWidth, float designHeight)
        {
            var rect = Rect(parent, objectName);
            var image = Paint(rect, AztecUiSprites.Panel, fill, UnityEngine.UI.Image.Type.Sliced);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = fill.a > 0f ? ButtonColors() : OutlineButtonColors();
            if (fill.a <= 0f)
                image.color = Ivory;

            if (outline.a > 0f)
                Layer(rect, "Frame", AztecUiSprites.PanelFrame, outline, UnityEngine.UI.Image.Type.Sliced);

            var label = Label(rect, "Label", text, designFontSize, textColor, bold: bold, spacingEm: spacingEm, alignment: TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            Size(rect, designWidth, designHeight);
            return button;
        }

        // ---------- Layout ----------

        /// <summary>A vertical group sized by its children. <paramref name="fit"/> only for groups that are not inside another group.</summary>
        public static RectTransform Column(Transform parent, string objectName, float designSpacing, TextAnchor alignment, bool fit = false)
        {
            var rect = Rect(parent, objectName);
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            Configure(group, designSpacing, alignment);
            if (fit)
                Fit(rect);

            return rect;
        }

        public static RectTransform Row(Transform parent, string objectName, float designSpacing, TextAnchor alignment, bool fit = false)
        {
            var rect = Rect(parent, objectName);
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(group, designSpacing, alignment);
            if (fit)
                Fit(rect);

            return rect;
        }

        public static void Padding(RectTransform group, float left, float right, float top, float bottom)
        {
            group.GetComponent<HorizontalOrVerticalLayoutGroup>().padding = new RectOffset(
                Mathf.RoundToInt(Px(left)), Mathf.RoundToInt(Px(right)), Mathf.RoundToInt(Px(top)), Mathf.RoundToInt(Px(bottom)));
        }

        /// <summary>Preferred size in a layout group, in design pixels. A negative value leaves that axis to the content.</summary>
        public static LayoutElement Size(RectTransform rect, float designWidth, float designHeight)
        {
            if (rect.TryGetComponent<LayoutElement>(out var element) == false)
                element = rect.gameObject.AddComponent<LayoutElement>();

            if (designWidth >= 0f)
            {
                element.preferredWidth = Px(designWidth);
                element.minWidth = Px(designWidth);
                rect.sizeDelta = new Vector2(Px(designWidth), rect.sizeDelta.y);
            }

            if (designHeight >= 0f)
            {
                element.preferredHeight = Px(designHeight);
                element.minHeight = Px(designHeight);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, Px(designHeight));
            }

            return element;
        }

        public static void Flexible(RectTransform rect, float width, float height)
        {
            if (rect.TryGetComponent<LayoutElement>(out var element) == false)
                element = rect.gameObject.AddComponent<LayoutElement>();

            element.flexibleWidth = width;
            element.flexibleHeight = height;
        }

        public static void IgnoreLayout(RectTransform rect)
        {
            if (rect.TryGetComponent<LayoutElement>(out var element) == false)
                element = rect.gameObject.AddComponent<LayoutElement>();

            element.ignoreLayout = true;
        }

        private static void Configure(HorizontalOrVerticalLayoutGroup group, float designSpacing, TextAnchor alignment)
        {
            group.spacing = Px(designSpacing);
            group.childAlignment = alignment;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
        }

        private static void Fit(RectTransform rect)
        {
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ---------- Decoration ----------

        /// <summary>The bands of stepped pyramids at the top and the bottom of a full screen.</summary>
        public static void FretBands(Transform screen)
        {
            var color = WithAlpha(FretGold, 0.5f);
            var top = Rect(screen, "FretTop");
            IgnoreLayout(top);
            Paint(top, AztecUiSprites.Fret, color, UnityEngine.UI.Image.Type.Tiled);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = Vector2.one;
            top.pivot = TopCenter;
            top.sizeDelta = new Vector2(0f, Px(14f));
            top.anchoredPosition = new Vector2(0f, -Px(16f));

            var bottom = Rect(screen, "FretBottom");
            IgnoreLayout(bottom);
            Paint(bottom, AztecUiSprites.Fret, color, UnityEngine.UI.Image.Type.Tiled);
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = BottomCenter;
            bottom.sizeDelta = new Vector2(0f, Px(14f));
            bottom.anchoredPosition = new Vector2(0f, Px(16f) + Px(14f));
            bottom.localScale = new Vector3(1f, -1f, 1f);
        }

        /// <summary>The sun stone behind the content, 640 design pixels wide, in the middle of the screen.</summary>
        public static void SunStone(Transform screen, float alpha = 0.12f)
        {
            var rect = Rect(screen, "SunStone");
            IgnoreLayout(rect);
            Paint(rect, AztecUiSprites.SunStone, WithAlpha(FretGold, alpha));
            Place(rect, Center, Center, Vector2.zero, new Vector2(Px(640f), Px(640f)));
        }

        /// <summary>The stepped spiral, 44 design pixels.</summary>
        public static RectTransform Spiral(Transform parent)
        {
            var rect = Rect(parent, "Spiral");
            Paint(rect, AztecUiSprites.Spiral, FretGold);
            Size(rect, 44f, 44f);
            return rect;
        }

        public static Image Dim(Transform screen, Color color)
        {
            var image = Layer(screen, "Dim", null, color);
            image.raycastTarget = true;
            return image;
        }

        public static void MarkDirty(Object target)
        {
            EditorUtility.SetDirty(target);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
