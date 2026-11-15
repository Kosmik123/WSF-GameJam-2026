"""Generates the Greek-Egyptian UI sprites (window, header, buttons, health bar, meander) into Assets/_Project/Ui/Ancient.

Run from the repository root:  python tools/ancient_ui_sprites.py
Egyptian palette and symbols (gold, lapis, turquoise, cinnabar, the winged sun), Greek meander on the borders.
Reuses the helpers of aztec_ui_sprites.py (supersampling and the Unity .meta writer). Existing .meta files are kept.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aztec_ui_sprites as base  # noqa: E402

OUT = "Assets/_Project/Ui/Ancient"
SS = base.SS

GOLD = (222, 174, 70, 255)
GOLD_LIGHT = (246, 214, 124, 255)
GOLD_DARK = (138, 98, 34, 255)
LAPIS = (30, 62, 132, 255)
LAPIS_LIGHT = (52, 94, 170, 255)
LAPIS_DARK = (14, 28, 70, 255)
NIGHT = (10, 14, 32, 255)
TURQUOISE = (46, 190, 176, 255)
TURQUOISE_LIGHT = (130, 232, 218, 255)
TURQUOISE_DARK = (20, 112, 106, 255)
CINNABAR = (190, 58, 44, 255)
CINNABAR_LIGHT = (236, 116, 92, 255)
CINNABAR_DARK = (112, 30, 24, 255)
SAND = (206, 184, 140, 255)
STONE = (96, 92, 98, 255)
STONE_DARK = (58, 56, 62, 255)
CLEAR = (0, 0, 0, 0)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), CLEAR)


def shrink(image, w, h):
    return image.resize((w, h), Image.LANCZOS)


def chamfer(w, h, cut, inset):
    """Rectangle with cut corners (in 1x units)."""
    x0, y0, x1, y1 = inset, inset, w - inset, h - inset
    c = max(0, cut - inset * 0.4)
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def fill_poly(draw, pts, colour):
    draw.polygon([(x * SS, y * SS) for x, y in pts], fill=colour)


def layered(w, h, cut, layers):
    """Concentric chamfered layers [(inset, colour), ...], outside to inside."""
    image = canvas(w, h)
    draw = ImageDraw.Draw(image)
    for inset, colour in layers:
        fill_poly(draw, chamfer(w, h, cut, inset), colour)
    return image


def diamond(draw, x, y, r, colour):
    draw.polygon([(x * SS, (y - r) * SS), ((x + r) * SS, y * SS), (x * SS, (y + r) * SS), ((x - r) * SS, y * SS)], fill=colour)


def window(w=256, h=256):
    """Window, 9-slice with a border of 48 px: gold rim, lapis band, thin gold line, dark body, a turquoise gem in every corner."""
    image = layered(w, h, 14, [(0, GOLD_DARK), (2, GOLD), (5, GOLD_DARK), (7, LAPIS), (20, GOLD_DARK), (21, GOLD), (23, NIGHT)])
    draw = ImageDraw.Draw(image)
    for cx, cy in [(26, 26), (w - 26, 26), (26, h - 26), (w - 26, h - 26)]:
        diamond(draw, cx, cy, 15, GOLD_DARK)
        diamond(draw, cx, cy, 12, GOLD)
        diamond(draw, cx, cy, 8, TURQUOISE_DARK)
        diamond(draw, cx, cy, 5, TURQUOISE_LIGHT)
    return shrink(image, w, h)


def header(w=768, h=192):
    """Title piece, not sliced: a cavetto cornice (the curved Egyptian crown) over a lapis band, with the winged sun disc."""
    image = canvas(w, h)
    draw = ImageDraw.Draw(image)
    # cornice: a flared band with vertical grooves
    top, flare = 30, 22
    draw.polygon([((0) * SS, top * SS), (w * SS, top * SS), ((w - 4) * SS, (top + flare) * SS), (4 * SS, (top + flare) * SS)], fill=GOLD_DARK)
    draw.polygon([(6 * SS, (top + 3) * SS), ((w - 6) * SS, (top + 3) * SS), ((w - 10) * SS, (top + flare - 2) * SS), (10 * SS, (top + flare - 2) * SS)], fill=GOLD)
    for x in range(14, w - 10, 14):
        draw.rectangle([x * SS, (top + 6) * SS, (x + 5) * SS, (top + flare - 4) * SS], fill=GOLD_DARK)
    # lapis band
    band_top, band_h = top + flare, 70
    draw.rectangle([4 * SS, band_top * SS, (w - 4) * SS, (band_top + band_h) * SS], fill=GOLD_DARK)
    draw.rectangle([8 * SS, (band_top + 4) * SS, (w - 8) * SS, (band_top + band_h - 4) * SS], fill=LAPIS)
    draw.rectangle([8 * SS, (band_top + band_h - 14) * SS, (w - 8) * SS, (band_top + band_h - 4) * SS], fill=LAPIS_DARK)
    # winged sun
    cx, cy = w / 2, band_top + band_h / 2 - 2
    for side in (-1, 1):
        for row, (length, dy, colour) in enumerate([(250, -18, GOLD), (220, -4, TURQUOISE), (186, 10, GOLD), (150, 22, TURQUOISE_DARK)]):
            x0 = cx + side * 36
            x1 = cx + side * (36 + length)
            y = cy + dy
            draw.polygon([(x0 * SS, (y - 7) * SS), (x1 * SS, (y - 3) * SS), (x1 * SS, (y + 3) * SS), (x0 * SS, (y + 7) * SS)], fill=colour)
    for r, colour in [(34, GOLD_DARK), (29, GOLD), (23, CINNABAR_DARK), (18, CINNABAR), (9, CINNABAR_LIGHT)]:
        draw.ellipse([(cx - r) * SS, (cy - r) * SS, (cx + r) * SS, (cy + r) * SS], fill=colour)
    # the uraeus (cobra) on both sides of the disc: a small hood and head
    for side in (-1, 1):
        x = cx + side * 46
        draw.polygon([(x * SS, (cy - 4) * SS), ((x + side * 7) * SS, (cy - 12) * SS), ((x + side * 7) * SS, (cy + 12) * SS), (x * SS, (cy + 6) * SS)], fill=GOLD_LIGHT)
    return shrink(image, w, h)


def button(state, w=256, h=96):
    """Button, 9-slice with a border of 24 px: gold double rim, lapis face with a lighter upper half, diamonds at both ends."""
    palette = {
        "normal": dict(rim=(GOLD_DARK, GOLD), face=LAPIS, top=LAPIS_LIGHT, shadow=LAPIS_DARK, gem=TURQUOISE),
        "hover": dict(rim=(GOLD, GOLD_LIGHT), face=(40, 130, 176, 255), top=(80, 190, 220, 255), shadow=LAPIS, gem=TURQUOISE_LIGHT),
        "pressed": dict(rim=(GOLD_DARK, GOLD_DARK), face=LAPIS_DARK, top=LAPIS_DARK, shadow=NIGHT, gem=TURQUOISE_DARK),
        "disabled": dict(rim=(STONE_DARK, STONE), face=STONE_DARK, top=(72, 70, 76, 255), shadow=(40, 38, 44, 255), gem=STONE),
    }[state]
    image = layered(w, h, 12, [(0, palette["rim"][0]), (2, palette["rim"][1]), (5, palette["rim"][0]), (7, palette["shadow"]), (9, palette["face"])])
    draw = ImageDraw.Draw(image)
    # lighter upper half of the face
    fill_poly(draw, [(18, 9), (w - 18, 9), (w - 12, 10), (w - 12, h * 0.46), (12, h * 0.46), (12, 10)], palette["top"])
    for x in (22, w - 22):
        diamond(draw, x, h / 2, 7, palette["rim"][0])
        diamond(draw, x, h / 2, 5, palette["gem"])
    return shrink(image, w, h)


def bar_frame(w=512, h=64):
    """Ring of a bar, 9-slice with a border of 20 px, the inside is clear. Gold caps at both ends with a turquoise gem."""
    outer = layered(w, h, 10, [(0, GOLD_DARK), (2, GOLD), (5, GOLD_DARK)])
    inner = layered(w, h, 10, [(7, (255, 255, 255, 255))])
    ring = Image.composite(canvas(w, h), outer, inner.split()[3])
    draw = ImageDraw.Draw(ring)
    for x in (10, w - 10):
        diamond(draw, x, h / 2, 12, GOLD_DARK)
        diamond(draw, x, h / 2, 9, GOLD)
        diamond(draw, x, h / 2, 5, TURQUOISE)
    return shrink(ring, w, h)


def bar_background(w=512, h=64):
    return shrink(layered(w, h, 10, [(7, NIGHT), (9, LAPIS_DARK)]), w, h)


def bar_fill(colour, light, dark, w=512, h=64):
    """Fill of a bar, 9-slice with a border of 16 px: a bright upper strip and a row of small feathers."""
    image = layered(w, h, 8, [(7, dark), (9, colour)])
    draw = ImageDraw.Draw(image)
    draw.rectangle([12 * SS, 11 * SS, (w - 12) * SS, 17 * SS], fill=light)
    for x in range(12, w - 24, 20):
        draw.polygon([(x * SS, 24 * SS), ((x + 10) * SS, 24 * SS), ((x + 5) * SS, 34 * SS)], fill=light)
    return shrink(image, w, h)


def meander(w=512, h=32):
    """Greek key (meander), tileable along X (period 48 px)."""
    image = canvas(w, h)
    draw = ImageDraw.Draw(image)
    draw.rectangle([0, 0, w * SS, 4 * SS], fill=GOLD_DARK)
    draw.rectangle([0, (h - 4) * SS, w * SS, h * SS], fill=GOLD_DARK)
    for x in range(0, w, 48):
        # one hook of the key: a square spiral
        path = [(0, 26), (0, 6), (32, 6), (32, 22), (12, 22), (12, 14), (22, 14)]
        draw.line([((x + a + 6) * SS, b * SS) for a, b in path], fill=GOLD, width=3 * SS, joint="curve")
        draw.rectangle([(x + 6) * SS, 24 * SS, (x + 48) * SS, 26 * SS], fill=GOLD)
    return shrink(image, w, h)


def main():
    os.makedirs(OUT, exist_ok=True)
    sprites = {
        "Window": (window(), (48, 48, 48, 48), False),
        "Header": (header(), None, False),
        "Button_Normal": (button("normal"), (24, 24, 24, 24), False),
        "Button_Hover": (button("hover"), (24, 24, 24, 24), False),
        "Button_Pressed": (button("pressed"), (24, 24, 24, 24), False),
        "Button_Disabled": (button("disabled"), (24, 24, 24, 24), False),
        "Bar_Frame": (bar_frame(), (20, 20, 20, 20), False),
        "Bar_Background": (bar_background(), (20, 20, 20, 20), False),
        "Bar_Fill_Turquoise": (bar_fill(TURQUOISE, TURQUOISE_LIGHT, TURQUOISE_DARK), (16, 16, 16, 16), False),
        "Bar_Fill_Cinnabar": (bar_fill(CINNABAR, CINNABAR_LIGHT, CINNABAR_DARK), (16, 16, 16, 16), False),
        "Meander": (meander(), None, True),
    }
    for name, (image, border, repeat) in sprites.items():
        path = os.path.join(OUT, name + ".png")
        image.save(path)
        base.write_meta(path, border, repeat)
        print(name, image.size)


if __name__ == "__main__":
    main()
