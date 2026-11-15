"""
Builds the hero "Kaplan Pierzastego Weza" (Q'uq'umatz priest) for Uroboros as a rigged low-poly FBX.

Base: ModularCharacterPolyart.fbx from RPG Tiny Hero Duo (the LD57 hero), so the humanoid rig and all its
animations stay. The script:
  - keeps both bodies (female for the Mage, male for the Warrior), recolors them from the texture atlas into
    vertex colors in the priest palette (quetzal greens, gold, limestone, red), skin stays,
  - adds low-poly parts parented to bones: serpent-jaw helmet with fangs, quetzal feather plume, lower-jaw collar,
    feather cape, staff with a serpent head (Mage), spear and feather shield (Warrior), cheek guards (Warrior),
  - writes everything as vertex colors ("Col", face corner) with one material, like the biome assets,
  - exports Assets/_Project/Models/Hero/QuqumatzPriest.fbx.
The class decides what is shown (PlayerClass.hiddenParts): see PART NAMES below.

Run:  blender -b --python Tools/Hero/build_quqmatz_priest.py -- <repo root>
"""
import colorsys
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = sys.argv[-1] if len(sys.argv) > 1 and os.path.isdir(sys.argv[-1]) else os.getcwd()
SOURCE_FBX = os.path.join(ROOT, "Assets", "RPG Tiny Hero Duo", "Mesh", "ModularCharacterPolyart.fbx")
ALBEDO = os.path.join(ROOT, "Assets", "RPG Tiny Hero Duo", "Texture", "Albedo.png")
OUTPUT = os.path.join(ROOT, "Assets", "_Project", "Models", "Hero", "QuqumatzPriest.fbx")
MATERIAL_NAME = "Ququmatz Vertex Color"

# Palette from the concept page (sRGB).
def hex_color(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4))

QUETZAL = hex_color("1F8A5B")
DEEP = hex_color("0F4C4A")
JADE = hex_color("6CC4A1")
GOLD = hex_color("D9A441")
RED = hex_color("C0392B")
LIMESTONE = hex_color("D8D1C0")
FLINT = hex_color("8C8C88")

# PART NAMES (used by the classes to hide what they do not need)
MAGE_ONLY = ["Priest_Plume", "Priest_Cape", "Priest_Staff"]
WARRIOR_ONLY = ["Warrior_Plume", "Warrior_Cape", "Warrior_Spear", "Warrior_Shield", "Warrior_CheekGuards"]
FEMALE_BODY = ["Body10", "Head02_Female", "Eye02", "Mouth02"]
MALE_BODY = ["Body05", "Head01_Male", "Eye01", "Mouth01"]
REMOVED = ["Hair01", "Hair06", "Cloak02", "Cloak03"]   # hair is under the helmet, the cloth cloaks are replaced by the feather cape


def srgb_to_linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def shade(color, amount):
    return tuple(max(0.0, min(1.0, x * amount)) for x in color)


# ---------------------------------------------------------------- scene

def load_base():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE_FBX, automatic_bone_orientation=False)
    armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for name in REMOVED:
        obj = bpy.data.objects.get(name)
        if obj:
            bpy.data.objects.remove(obj, do_unlink=True)
    return armature


def get_material():
    material = bpy.data.materials.get(MATERIAL_NAME) or bpy.data.materials.new(MATERIAL_NAME)
    return material


# ---------------------------------------------------------------- body recolor

def load_albedo():
    image = bpy.data.images.load(ALBEDO)
    width, height = image.size
    pixels = list(image.pixels)
    return width, height, pixels


def sample(texture, uv):
    width, height, pixels = texture
    x = min(width - 1, max(0, int((uv[0] % 1.0) * width)))
    y = min(height - 1, max(0, int((uv[1] % 1.0) * height)))
    index = (y * width + x) * 4
    return pixels[index], pixels[index + 1], pixels[index + 2]


def by_value(v, dark, middle, light):
    if v < 0.26:
        return dark
    return middle if v < 0.62 else light


def priest_color(color):
    """
    Maps a texture color of the base hero to the priest palette by color family, keeping the light and dark steps:
    the main cloth (magenta of the female body, brown leather of the male one) becomes quetzal green, blue accents
    become jade, purple becomes red, yellow stays gold, skin stays.
    """
    h, s, v = colorsys.rgb_to_hsv(*color)
    hue = h * 360.0
    # Greys: dark ones (hair line under the helmet, belts) go deep green, light ones limestone.
    if s < 0.15:
        return shade(DEEP, 0.7) if v < 0.5 else LIMESTONE

    is_skin = 22.0 <= hue <= 40.0 and 0.35 <= s <= 0.75 and v >= 0.6
    if is_skin:
        return color

    is_gold = 30.0 <= hue <= 55.0 and s >= 0.75 and v >= 0.55
    if is_gold:
        return GOLD

    if hue >= 290.0 or hue < 15.0:
        return by_value(v, DEEP, shade(QUETZAL, 0.8), QUETZAL)
    if hue >= 245.0:
        return by_value(v, shade(RED, 0.55), RED, shade(RED, 1.15))
    if hue >= 180.0:
        return by_value(v, DEEP, shade(JADE, 0.75), JADE)
    if hue >= 55.0:
        return by_value(v, DEEP, QUETZAL, JADE)
    # Browns and dark yellows (leather, wood): deep green and bronze.
    return by_value(v, DEEP, shade(GOLD, 0.65), LIMESTONE)


def bake_body_colors(obj, texture, remap):
    mesh = obj.data
    uv_layer = mesh.uv_layers.active
    attribute = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for loop_index in range(len(mesh.loops)):
        color = sample(texture, uv_layer.data[loop_index].uv)
        if remap:
            color = priest_color(color)
        attribute.data[loop_index].color = (*srgb_to_linear(color), 1.0)

    mesh.color_attributes.active_color = attribute
    mesh.materials.clear()
    mesh.materials.append(get_material())


# ---------------------------------------------------------------- low-poly builder

class Part:
    """Collects faces with a color each, in world space, and turns them into one bone-parented object."""

    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []

    def face(self, points, color, double_sided=False):
        start = len(self.verts)
        self.verts.extend(Vector(p) for p in points)
        self.faces.append((list(range(start, start + len(points))), color))
        if double_sided:
            # Own vertices for the back side: bmesh refuses a second face on the same vertices.
            start = len(self.verts)
            self.verts.extend(Vector(p) for p in reversed(points))
            self.faces.append((list(range(start, start + len(points))), shade(color, 0.85)))

    def box(self, center, size, color, axes=None, facet=0.9):
        """Box with half sizes along the given axes (default world axes). Side faces get a slightly darker color."""
        c = Vector(center)
        ax = axes or (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        x, y, z = (ax[0] * size[0], ax[1] * size[1], ax[2] * size[2])
        corners = [c + x * sx + y * sy + z * sz for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        quads = [((0, 1, 3, 2), 0.75), ((4, 6, 7, 5), 1.0), ((0, 4, 5, 1), facet), ((2, 3, 7, 6), facet), ((0, 2, 6, 4), 0.85), ((1, 5, 7, 3), 0.85)]
        for quad, amount in quads:
            self.face([corners[i] for i in quad], shade(color, amount))

    def prism(self, start, end, radius, sides, color, alternate=None, cap_color=None):
        """Faceted cylinder from start to end."""
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        side = axis.orthogonal().normalized()
        other = axis.cross(side)
        ring_a, ring_b = [], []
        for i in range(sides):
            angle = math.tau * i / sides
            offset = (side * math.cos(angle) + other * math.sin(angle)) * radius
            ring_a.append(a + offset)
            ring_b.append(b + offset)
        for i in range(sides):
            j = (i + 1) % sides
            tone = color if alternate is None or i % 2 == 0 else alternate
            self.face([ring_a[i], ring_a[j], ring_b[j], ring_b[i]], shade(tone, 0.8 + 0.2 * math.cos(math.tau * i / sides)))
        cap = cap_color or color
        self.face(list(reversed(ring_a)), shade(cap, 0.8))
        self.face(ring_b, cap)

    def feather(self, base, direction, side, length, width, color, tip_color=None):
        """Flat diamond feather (two-sided) with a darker spine half."""
        base = Vector(base)
        direction = Vector(direction).normalized()
        side = Vector(side).normalized()
        middle = base + direction * length * 0.35
        tip = base + direction * length
        left = middle - side * width
        right = middle + side * width
        self.face([base, right, tip], color, True)
        self.face([base, tip, left], shade(color, 0.8), True)
        if tip_color:
            tip_left = base + direction * length * 0.8 - side * width * 0.4
            tip_right = base + direction * length * 0.8 + side * width * 0.4
            self.face([tip_left, tip_right, tip], tip_color, True)

    def build(self, armature, bone_name):
        mesh = bpy.data.meshes.new(self.name)
        bm = bmesh.new()
        bm_verts = [bm.verts.new(v) for v in self.verts]
        colors = []
        for indices, color in self.faces:
            try:
                bm.faces.new([bm_verts[i] for i in indices])
                colors.append(color)
            except ValueError:
                pass  # duplicate face (two-sided parts reuse vertices in reverse, which bmesh may refuse)
        bm.to_mesh(mesh)
        bm.free()

        attribute = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for polygon, color in zip(mesh.polygons, colors):
            for loop_index in polygon.loop_indices:
                attribute.data[loop_index].color = (*srgb_to_linear(color), 1.0)
        mesh.color_attributes.active_color = attribute
        mesh.materials.append(get_material())

        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        parent_to_bone(obj, armature, bone_name)
        return obj


def parent_to_bone(obj, armature, bone_name):
    """Keeps the world position: the vertices are in world space, the parent inverse cancels the bone matrix."""
    bone = armature.data.bones[bone_name]
    bone_matrix = armature.matrix_world @ bone.matrix_local @ Matrix.Translation((0.0, bone.length, 0.0))
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_parent_inverse = bone_matrix.inverted()


def world_head(armature, bone_name):
    return armature.matrix_world @ armature.data.bones[bone_name].head_local


# ---------------------------------------------------------------- parts

def helmet(center, radius):
    """Upper jaw of the feathered serpent over the head: a faceted cap, a snout over the brow, fangs, golden eyes."""
    part = Part("Priest_Helmet")
    sides = 10
    brow_z = center.z + 0.04
    rings = [(brow_z, radius * 1.12), (center.z + radius * 0.55, radius * 1.0), (center.z + radius * 0.95, radius * 0.55)]
    top = Vector((center.x, center.y + 0.02, center.z + radius * 1.12))
    points = []
    for z, r in rings:
        ring = []
        for i in range(sides):
            angle = math.tau * (i + 0.5) / sides
            ring.append(Vector((center.x + math.sin(angle) * r, center.y + math.cos(angle) * r, z)))
        points.append(ring)
    for level in range(len(points) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            tone = QUETZAL if (i + level) % 2 == 0 else shade(QUETZAL, 0.82)
            part.face([points[level][i], points[level][j], points[level + 1][j], points[level + 1][i]], tone)
    for i in range(sides):
        j = (i + 1) % sides
        part.face([points[-1][i], points[-1][j], top], DEEP if i % 2 else shade(DEEP, 1.2))
    # Underside of the rim, so the cap is closed when seen from below.
    part.face(list(reversed(points[0])), DEEP)

    # Snout reaching forward (-Y) over the face.
    front = center.y - radius * 1.05
    snout_back = [Vector((-radius * 0.75, front + 0.1, brow_z)), Vector((radius * 0.75, front + 0.1, brow_z)),
                  Vector((radius * 0.6, front + 0.12, brow_z + radius * 0.5)), Vector((-radius * 0.6, front + 0.12, brow_z + radius * 0.5))]
    tip_y = front - radius * 0.55
    snout_tip = [Vector((-radius * 0.42, tip_y, brow_z - 0.01)), Vector((radius * 0.42, tip_y, brow_z - 0.01)),
                 Vector((radius * 0.3, tip_y + 0.03, brow_z + radius * 0.25)), Vector((-radius * 0.3, tip_y + 0.03, brow_z + radius * 0.25))]
    part.face([snout_back[3], snout_back[2], snout_tip[2], snout_tip[3]], QUETZAL)
    part.face([snout_back[0], snout_tip[0], snout_tip[1], snout_back[1]], shade(RED, 0.9))
    part.face([snout_back[1], snout_tip[1], snout_tip[2], snout_back[2]], shade(QUETZAL, 0.75))
    part.face([snout_back[0], snout_back[3], snout_tip[3], snout_tip[0]], shade(QUETZAL, 0.75))
    part.face([snout_tip[0], snout_tip[3], snout_tip[2], snout_tip[1]], DEEP)
    # Red band on the jaw edge and fangs hanging over the face.
    for side in (-1, 1):
        x = side * radius * 0.36
        base_y = tip_y + 0.05
        part.face([Vector((x - 0.035, base_y, brow_z)), Vector((x + 0.035, base_y, brow_z)), Vector((x, base_y + 0.01, brow_z - 0.12))], LIMESTONE, True)
        part.box((side * radius * 0.42, front - 0.02, brow_z + radius * 0.42), (0.045, 0.03, 0.035), GOLD)
    return part


def cheek_guards(center, radius):
    part = Part("Warrior_CheekGuards")
    for side in (-1, 1):
        part.box((side * radius * 0.98, center.y - radius * 0.35, center.z - radius * 0.3), (0.04, radius * 0.45, radius * 0.45), QUETZAL)
        part.box((side * radius * 1.0, center.y - radius * 0.72, center.z - radius * 0.62), (0.035, 0.05, 0.08), GOLD)
    return part


def plume(name, center, radius, long):
    """Fan of quetzal feathers rising from the back of the helmet, tilted back."""
    part = Part(name)
    base = Vector((center.x, center.y + radius * 0.55, center.z + radius * 0.85))
    lengths = (0.62, 0.78, 0.95, 0.78, 0.62) if long else (0.34, 0.42, 0.5, 0.42, 0.34)
    angles = (-55, -27, 0, 27, 55)
    colors = (RED, QUETZAL, JADE, QUETZAL, RED)
    for angle, length, color in zip(angles, lengths, colors):
        a = math.radians(angle)
        direction = Vector((math.sin(a), 0.45, math.cos(a)))
        side = Vector((math.cos(a), 0.0, -math.sin(a)))
        part.feather(base, direction, side, length, 0.075 if long else 0.06, color, JADE if color != JADE else LIMESTONE)
    part.box(base + Vector((0, -0.02, 0.02)), (0.09, 0.05, 0.05), GOLD)
    return part


def collar(neck, body_radius):
    """Lower jaw of the serpent as a collar: a ring with teeth pointing up at the front."""
    part = Part("Priest_Collar")
    sides = 12
    outer, inner = body_radius * 1.05, body_radius * 0.55
    z_bottom, z_top = neck.z - 0.07, neck.z - 0.02
    for i in range(sides):
        a0, a1 = math.tau * i / sides, math.tau * (i + 1) / sides
        o0 = Vector((math.sin(a0) * outer, neck.y + math.cos(a0) * outer * 0.85, z_bottom))
        o1 = Vector((math.sin(a1) * outer, neck.y + math.cos(a1) * outer * 0.85, z_bottom))
        i0 = Vector((math.sin(a0) * inner, neck.y + math.cos(a0) * inner * 0.85, z_top))
        i1 = Vector((math.sin(a1) * inner, neck.y + math.cos(a1) * inner * 0.85, z_top))
        tone = QUETZAL if i % 2 == 0 else shade(QUETZAL, 0.8)
        part.face([o0, o1, i1, i0], tone, True)
    # Teeth at the front (-Y is the face of the hero).
    for k in range(5):
        a = math.pi + (k - 2) * 0.32
        x, y = math.sin(a) * outer * 0.95, neck.y + math.cos(a) * outer * 0.8
        part.face([Vector((x - 0.03, y, z_bottom + 0.01)), Vector((x + 0.03, y, z_bottom + 0.01)), Vector((x, y - 0.005, z_top + 0.07))], LIMESTONE, True)
    part.box((0, neck.y - outer * 0.82, z_bottom + 0.01), (0.06, 0.02, 0.04), GOLD)
    return part


def cape(name, top_z, bottom_z, back_y):
    """Feather cape behind the back: vertical strips flaring down, a zigzag edge of feathers."""
    part = Part(name)
    columns = 6
    top_half, bottom_half = 0.26, 0.38
    for i in range(columns):
        t0, t1 = i / columns, (i + 1) / columns
        x_top0, x_top1 = -top_half + 2 * top_half * t0, -top_half + 2 * top_half * t1
        x_bot0, x_bot1 = -bottom_half + 2 * bottom_half * t0, -bottom_half + 2 * bottom_half * t1
        y_top, y_bottom = back_y, back_y + 0.12
        tone = QUETZAL if i % 2 == 0 else DEEP
        part.face([Vector((x_top0, y_top, top_z)), Vector((x_bot0, y_bottom, bottom_z)), Vector((x_bot1, y_bottom, bottom_z)), Vector((x_top1, y_top, top_z))], tone, True)
        tip = Vector(((x_bot0 + x_bot1) * 0.5, y_bottom + 0.02, bottom_z - 0.12))
        part.face([Vector((x_bot0, y_bottom, bottom_z)), tip, Vector((x_bot1, y_bottom, bottom_z))], JADE if i % 2 == 0 else RED, True)
    part.box((0, back_y - 0.01, top_z), (top_half + 0.03, 0.03, 0.03), GOLD)
    return part


def serpent_head(part, neck, up, forward, side, scale):
    """Low-poly serpent head: tapered upper jaw pointing forward, an open lower jaw, golden eyes, a forked red tongue."""
    s = scale
    back = neck + up * 0.05 * s
    snout = back + forward * 0.2 * s + up * 0.02 * s

    def corner(base, x, y):
        return base + side * x * s + up * y * s

    back_quad = [corner(back, -0.06, -0.02), corner(back, 0.06, -0.02), corner(back, 0.05, 0.07), corner(back, -0.05, 0.07)]
    front_quad = [corner(snout, -0.03, 0.0), corner(snout, 0.03, 0.0), corner(snout, 0.02, 0.035), corner(snout, -0.02, 0.035)]
    part.face([back_quad[3], back_quad[2], front_quad[2], front_quad[3]], QUETZAL)
    part.face([back_quad[1], front_quad[1], front_quad[2], back_quad[2]], shade(QUETZAL, 0.75))
    part.face([back_quad[0], back_quad[3], front_quad[3], front_quad[0]], shade(QUETZAL, 0.75))
    part.face([back_quad[0], front_quad[0], front_quad[1], back_quad[1]], RED)
    part.face(list(reversed(back_quad)), DEEP)
    part.face(front_quad, DEEP)
    # Lower jaw, opened downward.
    jaw_tip = back + forward * 0.17 * s - up * 0.07 * s
    part.face([corner(back, -0.05, -0.03), corner(back, 0.05, -0.03), jaw_tip + side * 0.02 * s, jaw_tip - side * 0.02 * s], shade(QUETZAL, 0.85), True)
    # Eyes and the forked tongue.
    for sign in (-1, 1):
        part.box(corner(back + forward * 0.06 * s, 0.05 * sign, 0.05), (0.014 * s, 0.014 * s, 0.014 * s), GOLD)
    tongue = back + forward * 0.17 * s - up * 0.02 * s
    part.face([tongue - side * 0.008, tongue + side * 0.008, tongue + forward * 0.1 * s + side * 0.025 * s], RED, True)
    part.face([tongue - side * 0.008, tongue + side * 0.008, tongue + forward * 0.1 * s - side * 0.025 * s], RED, True)


def staff(hand, grip):
    """Golden staff through the right fist, topped by a serpent head (Mage)."""
    part = Part("Priest_Staff")
    bottom = hand - grip * 0.38
    top = hand + grip * 0.92
    part.prism(bottom, top, 0.025, 6, GOLD, shade(GOLD, 0.85))
    part.prism(hand + grip * 0.55, hand + grip * 0.6, 0.032, 6, RED)
    # The serpent head bends forward from the top of the staff, with an open jaw and a red tongue.
    side = Vector((1.0, 0.0, 0.0)) if abs(grip.x) < 0.9 else Vector((0.0, 1.0, 0.0))
    side = (side - grip * side.dot(grip)).normalized()
    forward = grip.cross(side).normalized()
    serpent_head(part, top, grip, forward, side, 1.0)
    # Feathers around the neck of the serpent.
    for k in range(4):
        a = math.tau * k / 4
        direction = (side * math.cos(a) + forward * math.sin(a)) * 0.8 - grip * 0.5
        part.feather(top - grip * 0.04, direction, grip, 0.16, 0.035, JADE if k % 2 else QUETZAL)
    return part


def spear(hand, grip):
    """Spear with a flint head and a red ribbon (Warrior)."""
    part = Part("Warrior_Spear")
    # Held low: most of the shaft below the fist, so the head does not cut through the helmet when running.
    bottom = hand - grip * 0.62
    top = hand + grip * 0.5
    part.prism(bottom, top, 0.022, 6, DEEP, shade(DEEP, 1.3))
    side = grip.orthogonal().normalized()
    forward = grip.cross(side).normalized()
    blade_base = top
    blade_tip = top + grip * 0.24
    for s in (-1, 1):
        part.face([blade_base + side * 0.06 * s, blade_tip, blade_base + forward * 0.015], LIMESTONE if s > 0 else FLINT, True)
        part.face([blade_base + side * 0.06 * s, blade_base - forward * 0.015, blade_tip], shade(FLINT, 1.1), True)
    part.prism(top - grip * 0.03, top + grip * 0.01, 0.035, 6, GOLD)
    part.feather(top - grip * 0.05, -grip * 0.6 + side * 0.4, forward, 0.2, 0.03, RED)
    part.feather(top - grip * 0.05, -grip * 0.6 - side * 0.4, forward, 0.18, 0.03, JADE)
    return part


def shield(forearm_center):
    """Rectangular feather shield on the outer side of the left forearm (Warrior)."""
    part = Part("Warrior_Shield")
    center = forearm_center + Vector((0.0, 0.0, 0.07))
    part.box(center, (0.13, 0.2, 0.025), QUETZAL)
    part.box(center + Vector((0, 0, 0.03)), (0.09, 0.15, 0.008), JADE)
    part.box(center + Vector((0, 0, 0.04)), (0.045, 0.045, 0.01), GOLD)
    for k in range(5):
        x = -0.1 + k * 0.05
        base = center + Vector((x, 0.2, 0.0))
        part.feather(base, Vector((0, 1, -0.25)), Vector((1, 0, 0)), 0.16, 0.025, RED if k % 2 == 0 else JADE)
    return part


# ---------------------------------------------------------------- main

def main():
    armature = load_base()
    texture = load_albedo()
    for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
        is_face_detail = obj.name.startswith(("Eye", "Mouth"))
        bake_body_colors(obj, texture, remap=is_face_detail is False)

    head_mesh = bpy.data.objects["Head02_Female"]
    corners = [head_mesh.matrix_world @ Vector(c) for c in head_mesh.bound_box]
    head_min = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    head_max = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    head_center = (head_min + head_max) * 0.5
    head_radius = max(head_max.x - head_min.x, head_max.y - head_min.y) * 0.5

    hand_r = world_head(armature, "hand_r")
    thumb_r = world_head(armature, "thumb_01_r")
    arm_axis = (hand_r - world_head(armature, "lowerarm_r")).normalized()
    to_thumb = thumb_r - hand_r
    grip = (to_thumb - arm_axis * to_thumb.dot(arm_axis)).normalized()
    forearm_l = (world_head(armature, "lowerarm_l") + world_head(armature, "hand_l")) * 0.5

    helmet(head_center, head_radius).build(armature, "head")
    cheek_guards(head_center, head_radius).build(armature, "head")
    plume("Priest_Plume", head_center, head_radius, True).build(armature, "head")
    plume("Warrior_Plume", head_center, head_radius, False).build(armature, "head")
    collar(world_head(armature, "neck_01"), 0.24).build(armature, "spine_03")
    cape("Priest_Cape", 0.66, 0.2, 0.2).build(armature, "spine_03")
    cape("Warrior_Cape", 0.66, 0.42, 0.2).build(armature, "spine_03")
    staff(hand_r, grip).build(armature, "hand_r")
    spear(hand_r, grip).build(armature, "hand_r")
    shield(forearm_l).build(armature, "lowerarm_l")

    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        if obj.type in ("MESH", "ARMATURE"):
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=OUTPUT,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        bake_anim=False,
        mesh_smooth_type="FACE",
        colors_type="SRGB",
        prioritize_active_color=True,
    )
    print("QUQUMATZ EXPORTED", OUTPUT)
    print("QUQUMATZ PARTS", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))


main()
