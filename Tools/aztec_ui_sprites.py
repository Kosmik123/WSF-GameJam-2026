"""Generates the Aztec styled UI sprites (PNG + Unity .meta) into Assets/_Project/Ui/Aztec.

Run from the repository root:  python tools/aztec_ui_sprites.py
Everything is drawn 4x bigger and scaled down, so the edges are smooth. The sprites are placeholders drawn with
geometry only (steps, frets, diamonds): an artist can redraw them with the same sizes and 9-slice borders.
Existing .meta files are kept (the GUIDs stay), so running the script again only redraws the images.
"""
import os
import uuid

from PIL import Image, ImageDraw

OUT = "Assets/_Project/Ui/Aztec"
SS = 4  # supersampling

OBSIDIAN = (20, 17, 15, 255)
STONE = (42, 36, 32, 255)
STONE_LIGHT = (66, 57, 50, 255)
GOLD = (217, 164, 65, 255)
GOLD_DARK = (140, 100, 36, 255)
TURQUOISE = (43, 179, 163, 255)
TURQUOISE_LIGHT = (110, 226, 210, 255)
TURQUOISE_DARK = (20, 108, 100, 255)
CINNABAR = (184, 55, 43, 255)
CINNABAR_LIGHT = (232, 110, 90, 255)
IVORY = (239, 230, 208, 255)
JADE = (63, 143, 94, 255)
GREY = (110, 104, 98, 255)
CLEAR = (0, 0, 0, 0)


def new(w, h):
    return Image.new("RGBA", (w * SS, h * SS), CLEAR)


def finish(image, w, h):
    return image.resize((w, h), Image.LANCZOS)


def stepped(w, h, notch, steps=2, inset=0):
    """Polygon of a rectangle with stepped (pyramid like) corners, in 1x units."""
    n = notch
    s = n / steps
    x0, y0, x1, y1 = inset, inset, w - inset, h - inset
    pts = []
    # top-left corner, clockwise
    for i in range(steps + 1):
        pts.append((x0 + n - i * s, y0 + i * s))
        if i < steps:
            pts.append((x0 + n - i * s, y0 + (i + 1) * s))
    pts = []
    corners = [
        ((x0, y0), (1, 1)),
        ((x1, y0), (-1, 1)),
        ((x1, y1), (-1, -1)),
        ((x0, y1), (1, -1)),
    ]
    for (cx, cy), (sx, sy) in corners:
        for i in range(steps):
            # outer step: along the edge, then in
            pts.append((cx + sx * (n - i * s), cy + sy * (i * s)))
            pts.append((cx + sx * (n - (i + 1) * s), cy + sy * (i * s)))
            pts.append((cx + sx * (n - (i + 1) * s), cy + sy * ((i + 1) * s)))
        pts.append((cx, cy + sy * n))
    return pts


def poly(draw, pts, fill):
    draw.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)


def corner_fixed(w, h, notch, steps, inset):
    """Stepped rectangle, built corner by corner so the order is always clockwise."""
    n, s = notch, notch / steps
    x0, y0, x1, y1 = inset, inset, w - inset, h - inset
    pts = []
    # top edge, left to right
    pts.append((x0 + n, y0))
    pts.append((x1 - n, y0))
    for i in range(steps):
        pts.append((x1 - n + (i + 1) * s, y0 + i * s))
        pts.append((x1 - n + (i + 1) * s, y0 + (i + 1) * s))
    # right edge
    pts.append((x1, y1 - n))
    for i in range(steps):
        pts.append((x1 - i * s, y1 - n + (i + 1) * s))
        pts.append((x1 - (i + 1) * s, y1 - n + (i + 1) * s))
    # bottom edge
    pts.append((x0 + n, y1))
    for i in range(steps):
        pts.append((x0 + n - (i + 1) * s, y1 - i * s))
        pts.append((x0 + n - (i + 1) * s, y1 - (i + 1) * s))
    # left edge
    pts.append((x0, y0 + n))
    for i in range(steps):
        pts.append((x0 + i * s, y0 + n - (i + 1) * s))
        pts.append((x0 + (i + 1) * s, y0 + n - (i + 1) * s))
    return pts


def rim(w, h, notch, steps, layers):
    """A stepped shape filled by concentric layers: [(inset, colour), ...] from the outside to the inside."""
    image = new(w, h)
    draw = ImageDraw.Draw(image)
    for inset, colour in layers:
        poly(draw, corner_fixed(w, h, max(1, notch - inset * 0.6), steps, inset), colour)
    return finish(image, w, h)


def window(w=256, h=256):
    """Window frame, 9-slice with a border of 48 px: gold rim, stone body, turquoise inner line, stepped corner studs."""
    layers = [(0, GOLD_DARK), (2, GOLD), (7, OBSIDIAN), (9, TURQUOISE_DARK), (11, STONE), (14, OBSIDIAN)]
    image = rim(w, h, 32, 3, layers)
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    # a stepped pyramid stud in every corner
    for cx, cy, sx, sy in [(0, 0, 1, 1), (w, 0, -1, 1), (w, h, -1, -1), (0, h, 1, -1)]:
        for i, (size, colour) in enumerate([(22, GOLD_DARK), (16, GOLD), (10, TURQUOISE), (5, TURQUOISE_LIGHT)]):
            a = (cx + sx * 12, cy + sy * 12)
            b = (a[0] + sx * size, a[1] + sy * size)
            draw.rectangle([min(a[0], b[0]) * SS, min(a[1], b[1]) * SS, max(a[0], b[0]) * SS, max(a[1], b[1]) * SS], fill=colour)
    image.alpha_composite(finish(big, w, h))
    return image


def header(w=768, h=128):
    """Band for titles, 9-slice with a border of 64 px on the sides: a stepped pyramid shape with a turquoise diamond in the middle."""
    image = rim(w, h, 40, 3, [(0, GOLD_DARK), (3, GOLD), (8, OBSIDIAN), (10, TURQUOISE_DARK), (12, STONE)])
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    cx, cy = w / 2, h / 2
    for r, colour in [(34, GOLD_DARK), (28, GOLD), (20, OBSIDIAN), (14, TURQUOISE), (7, TURQUOISE_LIGHT)]:
        draw.polygon([((cx) * SS, (cy - r) * SS), ((cx + r) * SS, cy * SS), (cx * SS, (cy + r) * SS), ((cx - r) * SS, cy * SS)], fill=colour)
    image.alpha_composite(finish(big, w, h))
    return image


def button(state, w=256, h=96):
    """Button, 9-slice with a border of 32 px. States: normal, hover, pressed, disabled."""
    if state == "normal":
        layers = [(0, GOLD_DARK), (2, GOLD), (5, OBSIDIAN), (7, STONE), (9, STONE_LIGHT)]
        glow = TURQUOISE
    elif state == "hover":
        layers = [(0, GOLD), (2, TURQUOISE_LIGHT), (5, OBSIDIAN), (7, TURQUOISE_DARK), (9, TURQUOISE)]
        glow = TURQUOISE_LIGHT
    elif state == "pressed":
        layers = [(0, GOLD_DARK), (2, GOLD_DARK), (5, OBSIDIAN), (7, OBSIDIAN), (9, STONE)]
        glow = TURQUOISE_DARK
    else:
        layers = [(0, GREY), (2, (80, 76, 72, 255)), (5, OBSIDIAN), (7, STONE), (9, STONE)]
        glow = GREY
    image = rim(w, h, 26, 2, layers)
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    # small diamonds at both ends
    for x in (22, w - 22):
        r = 6
        draw.polygon([(x * SS, (h / 2 - r) * SS), ((x + r) * SS, h / 2 * SS), (x * SS, (h / 2 + r) * SS), ((x - r) * SS, h / 2 * SS)], fill=glow)
    image.alpha_composite(finish(big, w, h))
    return image


def bar_frame(w=512, h=64):
    """Ring of a bar, 9-slice with a border of 20 px, the inside is clear."""
    full = rim(w, h, 14, 2, [(0, GOLD_DARK), (2, GOLD), (5, OBSIDIAN)])
    inner = rim(w, h, 14, 2, [(7, (255, 255, 255, 255))])
    mask = inner.split()[3]
    ring = Image.new("RGBA", (w, h), CLEAR)
    ring.paste(full, (0, 0))
    clear = Image.new("RGBA", (w, h), CLEAR)
    ring = Image.composite(clear, ring, mask)
    # studs at the ends
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    for x in (8, w - 8):
        draw.polygon([(x * SS, (h / 2 - 8) * SS), ((x + 8) * SS, h / 2 * SS), (x * SS, (h / 2 + 8) * SS), ((x - 8) * SS, h / 2 * SS)], fill=TURQUOISE)
    ring.alpha_composite(finish(big, w, h))
    return ring


def bar_background(w=512, h=64):
    return rim(w, h, 14, 2, [(7, OBSIDIAN)])


def bar_fill(colour, light, dark, w=512, h=64):
    """Fill of a bar, 9-slice with a border of 16 px (set as Image Type Filled/Sliced). Zig-zag highlight on top."""
    image = rim(w, h, 12, 2, [(7, dark), (9, colour)])
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    step = 16
    for i in range(0, w, step * 2):
        draw.polygon([(i * SS, 14 * SS), ((i + step) * SS, 14 * SS), ((i + step / 2) * SS, 20 * SS)], fill=light)
    image.alpha_composite(finish(big, w, h))
    return image


def slot(state, w=128, h=128):
    """Frame of a skill slot, 9-slice with a border of 24 px, the inside is clear. States: ready, cooldown, locked."""
    colours = {
        "ready": [(0, GOLD_DARK), (2, GOLD), (5, OBSIDIAN)],
        "cooldown": [(0, GOLD_DARK), (2, GREY), (5, OBSIDIAN)],
        "locked": [(0, (60, 56, 52, 255)), (2, (84, 78, 72, 255)), (5, OBSIDIAN)],
    }[state]
    full = rim(w, h, 20, 2, colours)
    inner = rim(w, h, 20, 2, [(7, (255, 255, 255, 255))])
    clear = Image.new("RGBA", (w, h), CLEAR)
    ring = Image.composite(clear, full, inner.split()[3])
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    accent = {"ready": TURQUOISE, "cooldown": GREY, "locked": (60, 56, 52, 255)}[state]
    for x, y in [(w / 2, 5), (w / 2, h - 5)]:
        r = 5
        draw.polygon([(x * SS, (y - r) * SS), ((x + r) * SS, y * SS), (x * SS, (y + r) * SS), ((x - r) * SS, y * SS)], fill=accent)
    ring.alpha_composite(finish(big, w, h))
    return ring


def slot_background(w=128, h=128):
    return rim(w, h, 20, 2, [(7, OBSIDIAN)])


def dot(filled, size=48):
    """A round marker: empty = a gold ring with a dark centre, filled = a turquoise diamond in a gold ring."""
    big = new(size, size)
    draw = ImageDraw.Draw(big)
    c = size / 2
    draw.ellipse([2 * SS, 2 * SS, (size - 2) * SS, (size - 2) * SS], fill=GOLD_DARK)
    draw.ellipse([4 * SS, 4 * SS, (size - 4) * SS, (size - 4) * SS], fill=GOLD)
    draw.ellipse([8 * SS, 8 * SS, (size - 8) * SS, (size - 8) * SS], fill=OBSIDIAN)
    if filled:
        r = size / 2 - 11
        draw.polygon([(c * SS, (c - r) * SS), ((c + r) * SS, c * SS), (c * SS, (c + r) * SS), ((c - r) * SS, c * SS)], fill=TURQUOISE)
        r2 = r * 0.5
        draw.polygon([(c * SS, (c - r2) * SS), ((c + r2) * SS, c * SS), (c * SS, (c + r2) * SS), ((c - r2) * SS, c * SS)], fill=TURQUOISE_LIGHT)
    return finish(big, size, size)


def separator(w=512, h=16):
    """A line of stepped teeth, tileable along X (the period is 32 px)."""
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    draw.rectangle([0, 6 * SS, w * SS, 10 * SS], fill=GOLD_DARK)
    for x in range(0, w, 32):
        draw.polygon([((x + 4) * SS, 10 * SS), ((x + 16) * SS, 14 * SS), ((x + 28) * SS, 10 * SS)], fill=GOLD)
        draw.polygon([((x + 4) * SS, 6 * SS), ((x + 16) * SS, 2 * SS), ((x + 28) * SS, 6 * SS)], fill=GOLD)
        draw.polygon([((x + 12) * SS, 8 * SS), ((x + 16) * SS, 5 * SS), ((x + 20) * SS, 8 * SS), ((x + 16) * SS, 11 * SS)], fill=TURQUOISE)
    return finish(big, w, h)


def frieze(w=512, h=32):
    """Stepped fret ('xicalcoliuhqui') band, tileable along X (the period is 64 px)."""
    big = new(w, h)
    draw = ImageDraw.Draw(big)
    draw.rectangle([0, 0, w * SS, 4 * SS], fill=GOLD_DARK)
    draw.rectangle([0, (h - 4) * SS, w * SS, h * SS], fill=GOLD_DARK)
    for x in range(0, w, 64):
        pts = [(0, 28), (0, 4), (24, 4), (24, 16), (12, 16), (12, 10), (18, 10), (18, 10)]
        # one hook: a stepped spiral
        steps = [(0, 28), (0, 6), (28, 6), (28, 22), (14, 22), (14, 14), (22, 14)]
        draw.line([((x + a + 4) * SS, b * SS) for a, b in steps], fill=GOLD, width=3 * SS, joint="curve")
        draw.rectangle([(x + 38) * SS, 12 * SS, (x + 44) * SS, 20 * SS], fill=TURQUOISE)
    return finish(big, w, h)


def palette(w=640, h=128):
    names = [OBSIDIAN, STONE, GOLD, TURQUOISE, CINNABAR, IVORY, JADE]
    image = Image.new("RGBA", (w, h), CLEAR)
    draw = ImageDraw.Draw(image)
    cell = w // len(names)
    for i, colour in enumerate(names):
        draw.rectangle([i * cell, 0, (i + 1) * cell, h], fill=colour)
    return image


def write_meta(path, border=None, wrap_repeat=False):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    left, bottom, right, top = border or (0, 0, 0, 0)
    text = f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: {0 if wrap_repeat else 1}
    wrapV: {0 if wrap_repeat else 1}
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {left}, y: {bottom}, z: {right}, w: {top}}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexture
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
    with open(meta, "w", newline="\n") as handle:
        handle.write(text)


def main():
    os.makedirs(OUT, exist_ok=True)
    # name: (image, 9-slice border (left, bottom, right, top) or None, repeats along X)
    sprites = {
        "Window": (window(), (48, 48, 48, 48), False),
        "Header": (header(), (64, 40, 64, 40), False),
        "Button_Normal": (button("normal"), (32, 32, 32, 32), False),
        "Button_Hover": (button("hover"), (32, 32, 32, 32), False),
        "Button_Pressed": (button("pressed"), (32, 32, 32, 32), False),
        "Button_Disabled": (button("disabled"), (32, 32, 32, 32), False),
        "Bar_Frame": (bar_frame(), (20, 20, 20, 20), False),
        "Bar_Background": (bar_background(), (20, 20, 20, 20), False),
        "Bar_Fill_Turquoise": (bar_fill(TURQUOISE, TURQUOISE_LIGHT, TURQUOISE_DARK), (16, 16, 16, 16), False),
        "Bar_Fill_Cinnabar": (bar_fill(CINNABAR, CINNABAR_LIGHT, (110, 30, 24, 255)), (16, 16, 16, 16), False),
        "Slot_Ready": (slot("ready"), (24, 24, 24, 24), False),
        "Slot_Cooldown": (slot("cooldown"), (24, 24, 24, 24), False),
        "Slot_Locked": (slot("locked"), (24, 24, 24, 24), False),
        "Slot_Background": (slot_background(), (24, 24, 24, 24), False),
        "RoundDot_Empty": (dot(False), None, False),
        "RoundDot_Filled": (dot(True), None, False),
        "Separator": (separator(), None, True),
        "Frieze": (frieze(), None, True),
        "Palette": (palette(), None, False),
    }
    for name, (image, border, repeat) in sprites.items():
        path = os.path.join(OUT, name + ".png")
        image.save(path)
        write_meta(path, border, repeat)
        print(name, image.size)


if __name__ == "__main__":
    main()
