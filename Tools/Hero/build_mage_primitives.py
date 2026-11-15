"""
Builds the Mage "Kaplan Pierzastego Weza" (Q'uq'umatz priest) from primitives, as close as possible to the concept
drawing 02 on the "Bohater z Xibalby" page: boxes, faceted cylinders and flat feathers in the concept palette,
written as vertex colors ("Col", face corner) with one material. Every corner is painted (paint_corner): baked key light
with warm/cool tints, height gradients, ambient occlusion from the whole body, highlights on gold and jade, face jitter.
In Unity the heroes use the shader WSF/Hero Vertex Color (Assets/_Project/Shaders/HeroVertexColor.shader).

Every part is rigid and parented to a bone of the RPG Tiny Hero Duo skeleton (the LD57 hero), so the humanoid
animations of the game (idle, run, attacks, cast, dodge, hit, death) play on it. Only the armature of
ModularCharacterPolyart.fbx is used; its meshes are deleted.

Output: Assets/_Project/Models/Hero/QuqumatzMage.fbx
Run:    blender -b --python Tools/Hero/build_mage_primitives.py -- <repo root>
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = sys.argv[-1] if len(sys.argv) > 1 and os.path.isdir(sys.argv[-1]) else os.getcwd()
SOURCE_FBX = os.path.join(ROOT, "Assets", "RPG Tiny Hero Duo", "Mesh", "ModularCharacterPolyart.fbx")
OUTPUT = os.path.join(ROOT, "Assets", "_Project", "Models", "Hero", "QuqumatzMage.fbx")
MATERIAL_NAME = "Ququmatz Vertex Color"


def hex_color(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


# Palette of concept 02 (and the skin of the concept page).
QUETZAL = hex_color("1F8A5B")
DEEP = hex_color("0F4C4A")
JADE = hex_color("6CC4A1")
GOLD = hex_color("D9A441")
RED = hex_color("C0392B")
LIMESTONE = hex_color("D8D1C0")
SKIN = hex_color("9A6744")
SKIN_SHADE = hex_color("74482D")
EYE = hex_color("1A1614")


def srgb_to_linear(color):
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in color)


def shade(color, amount):
    return tuple(max(0.0, min(1.0, c * amount)) for c in color)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def multiply(a, b):
    return tuple(a[i] * b[i] for i in range(3))


def clamp01(value):
    return max(0.0, min(1.0, value))


def smoothstep(edge0, edge1, value):
    t = clamp01((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------- corner painting
# Every face corner gets its own color: the face color (the palette of the drawing) times a baked light, a height
# gradient, ambient occlusion from the whole body and a small per-face jitter. All in sRGB, like the palette.

PENDING = []

# Key light from the top, the front and the left of the viewer (the character faces -Y, the viewer's left is -X).
KEY_LIGHT = Vector((-0.45, -0.6, 0.85)).normalized()
WARM_LIGHT = (1.04, 1.0, 0.93)
COOL_SHADOW = (0.9, 0.95, 1.03)
OCCLUSION_TINT = (0.56, 0.62, 0.7)
HIGHLIGHT = (1.0, 0.97, 0.86)
AO_DISTANCE = 0.11
AO_RAYS = 24


def face_normal(points):
    """Newell normal of a polygon (works for the slightly non planar quads of the frustums)."""
    normal = Vector((0.0, 0.0, 0.0))
    for i, a in enumerate(points):
        b = points[(i + 1) % len(points)]
        normal.x += (a.y - b.y) * (a.z + b.z)
        normal.y += (a.z - b.z) * (a.x + b.x)
        normal.z += (a.x - b.x) * (a.y + b.y)
    return normal.normalized() if normal.length > 1e-9 else Vector((0.0, 0.0, 1.0))


def face_jitter(part_name, face_index):
    """Deterministic small value change per face, so the facets read even where the light is the same."""
    return random.Random(f"{part_name}:{face_index}").uniform(0.965, 1.035)


def hemisphere_directions(count):
    """Cosine weighted directions around +Z (a Fibonacci spiral, the same every run)."""
    directions = []
    golden = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(count):
        r = math.sqrt((i + 0.5) / count)
        angle = golden * i
        directions.append(Vector((r * math.cos(angle), r * math.sin(angle), math.sqrt(max(0.0, 1.0 - r * r)))))
    return directions


HEMISPHERE = hemisphere_directions(AO_RAYS)


class Occluder:
    """All parts of the body in the rest pose, for the baked ambient occlusion."""

    def __init__(self, parts):
        verts, polygons = [], []
        self.two_sided = []
        for part in parts:
            offset = len(verts)
            verts.extend(part.verts)
            polygons.extend([offset + i for i in indices] for indices, _ in part.faces)
            self.two_sided.extend(i in part.double_sided for i in range(len(part.faces)))
        self.tree = BVHTree.FromPolygons(verts, polygons, all_triangles=False)

    def occlusion(self, corner, normal):
        tangent = normal.orthogonal().normalized()
        bitangent = normal.cross(tangent)
        origin = corner + normal * 0.002
        hidden = 0.0
        for d in HEMISPHERE:
            direction = (tangent * d.x + bitangent * d.y + normal * d.z).normalized()
            hit = self.tree.ray_cast(origin, direction, AO_DISTANCE)
            # A back face hit means the corner is inside another part (the decals sunk in the collar): not occluded.
            if hit[0] is not None and hit[3] > 0.003 and (hit[1].dot(direction) < 0.0 or self.two_sided[hit[2]]):
                hidden += 1.0 - hit[3] / AO_DISTANCE * 0.6
        return hidden / len(HEMISPHERE)


def chroma_close(color, reference, tolerance=0.03):
    total, reference_total = sum(color), sum(reference)
    if total < 1e-6:
        return False
    return sum((color[i] / total - reference[i] / reference_total) ** 2 for i in range(3)) ** 0.5 < tolerance


def paint_corner(color, corner, normal, z_range, jitter, occluder):
    if color == EYE:
        return EYE

    # Baked key light: a wrapped lambert, warm where lit and a cool tint on the faces turned away.
    wrap = clamp01((normal.dot(KEY_LIGHT) + 0.35) / 1.35)
    light = 0.84 + 0.24 * wrap
    tint = mix(COOL_SHADOW, WARM_LIGHT, wrap)

    # Darker towards the ground and towards the bottom of each part.
    bottom, top = z_range
    local = (corner.z - bottom) / (top - bottom) if top - bottom > 1e-4 else 1.0
    gradient = (0.86 + 0.16 * smoothstep(0.0, 0.85, corner.z)) * (0.9 + 0.13 * local)

    # Ambient occlusion: other parts close above the face (helmet brim, cape, joints), and the sides facing the body core.
    occlusion = occluder.occlusion(corner, normal)
    to_core = Vector((-corner.x, 0.04 - corner.y, 0.0))
    if to_core.length > 1e-4:
        facing = clamp01(Vector((normal.x, normal.y, 0.0)).dot(to_core.normalized()))
        occlusion += 0.25 * facing * (1.0 - smoothstep(0.08, 0.35, to_core.length))
    occlusion = clamp01(occlusion)

    value = light * gradient * jitter
    result = tuple(c * value for c in multiply(color, tint))
    result = multiply(result, mix((1.0, 1.0, 1.0), OCCLUSION_TINT, occlusion * 0.7))

    # Gold and jade are the jewels: a bit brighter overall, and they catch the light on their top faces (more at the top
    # of the part).
    accent = chroma_close(color, GOLD) or chroma_close(color, JADE)
    if accent:
        result = tuple(c * 1.07 for c in result)
    if accent and normal.z > 0.3:
        shine = (normal.z - 0.3) / 0.7 * (0.6 + 0.4 * local) * (1.0 - occlusion)
        result = mix(result, multiply(HIGHLIGHT, tuple(min(1.0, c * 1.3) for c in color)), 0.45 * shine)
    return tuple(clamp01(c) for c in result)


# ---------------------------------------------------------------- builder

class Part:
    """Faces with a color each, in world space (rest pose), built into one object parented to a bone."""

    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []
        self.double_sided = set()

    def face(self, points, color, double_sided=False):
        start = len(self.verts)
        self.verts.extend(Vector(p) for p in points)
        self.faces.append((list(range(start, start + len(points))), color))
        if double_sided:
            self.double_sided.update((len(self.faces) - 1, len(self.faces)))
            start = len(self.verts)
            self.verts.extend(Vector(p) for p in reversed(points))
            self.faces.append((list(range(start, start + len(points))), shade(color, 0.9)))

    def box(self, center, half, colors, axes=None):
        """
        Box with half sizes along three axes (default world X, Y, Z). colors: one color, or a dict with the keys
        left (-X), right (+X), front (-Y), back (+Y), bottom, top. The character faces -Y, so "front" is its face side.
        """
        c = Vector(center)
        ax = axes or (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        x, y, z = ax[0] * half[0], ax[1] * half[1], ax[2] * half[2]
        p = {(sx, sy, sz): c + x * sx + y * sy + z * sz for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)}
        palette = colors if isinstance(colors, dict) else {}
        base = colors if isinstance(colors, tuple) else palette.get("front", LIMESTONE)

        def tone(key, amount):
            return palette.get(key, shade(base, amount))

        # The automatic side tones are mild: the corner painting (paint_corner) bakes the light and dark sides.
        self.face([p[(-1, -1, -1)], p[(1, -1, -1)], p[(1, -1, 1)], p[(-1, -1, 1)]], tone("front", 1.0))
        self.face([p[(1, 1, -1)], p[(-1, 1, -1)], p[(-1, 1, 1)], p[(1, 1, 1)]], tone("back", 0.9))
        self.face([p[(-1, 1, -1)], p[(-1, -1, -1)], p[(-1, -1, 1)], p[(-1, 1, 1)]], tone("left", 1.0))
        self.face([p[(1, -1, -1)], p[(1, 1, -1)], p[(1, 1, 1)], p[(1, -1, 1)]], tone("right", 0.93))
        self.face([p[(-1, -1, 1)], p[(1, -1, 1)], p[(1, 1, 1)], p[(-1, 1, 1)]], tone("top", 1.0))
        self.face([p[(-1, 1, -1)], p[(1, 1, -1)], p[(1, -1, -1)], p[(-1, -1, -1)]], tone("bottom", 0.85))

    def limb(self, start, end, half_width, half_depth, color, light_side=None):
        """Box from one joint to the next (along a bone in the rest pose). light_side: (light, dark) colors of the sides."""
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        up = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((0, -1, 0))
        side = axis.cross(up).normalized()
        depth = side.cross(axis).normalized()
        half_length = (b - a).length * 0.5
        colors = color
        if light_side:
            colors = {"front": light_side[0], "back": shade(light_side[1], 0.85), "left": light_side[0], "right": light_side[1],
                      "top": light_side[0], "bottom": light_side[1]}
        self.box((a + b) * 0.5, (half_length, half_width, half_depth), colors, (axis, side, depth))

    def frustum(self, center_bottom, radius_bottom, radius_top, height, sides, color_left, color_right, depth_scale=1.0, cap=True):
        """Faceted cone section around the Z axis (skirt, belt, collar). The left half of the front is lighter, like in the drawing."""
        c = Vector(center_bottom)
        bottom, top = [], []
        for i in range(sides):
            angle = math.tau * (i + 0.5) / sides
            direction = Vector((math.sin(angle), -math.cos(angle) * depth_scale, 0.0))
            bottom.append(c + direction * radius_bottom)
            top.append(c + direction * radius_top + Vector((0, 0, height)))
        for i in range(sides):
            j = (i + 1) % sides
            middle_x = (bottom[i].x + bottom[j].x) * 0.5 - c.x
            color = color_left if middle_x < 0 else color_right
            light = 0.93 + 0.07 * abs(math.cos(math.tau * (i + 1) / sides))
            self.face([bottom[i], bottom[j], top[j], top[i]], shade(color, light))
        if cap:
            self.face(list(reversed(bottom)), shade(color_right, 0.85))
            self.face(top, color_left)

    def feather(self, base, tip, width, color, thickness=0.012):
        """Long flat quetzal feather: a thin diamond prism from base to tip, light front and darker back."""
        a, b = Vector(base), Vector(tip)
        axis = (b - a).normalized()
        side = axis.cross(Vector((0, -1, 0))).normalized()
        normal = side.cross(axis).normalized()
        middle = a + (b - a) * 0.3
        left, right = middle - side * width, middle + side * width
        front, back = normal * thickness, -normal * thickness
        self.face([a, right + front, b], color)
        self.face([a, b, left + front], shade(color, 0.95))
        self.face([a, b, right + back], shade(color, 0.8))
        self.face([a, left + back, b], shade(color, 0.82))
        self.face([a, left + front, left + back], shade(color, 0.75))
        self.face([a, right + back, right + front], shade(color, 0.75))

    def build(self, armature, bone_name):
        """Queues the part: the corner colors need the whole body (ambient occlusion), so build_all makes the objects."""
        PENDING.append((self, armature, bone_name))

    def bounds_z(self):
        heights = [v.z for v in self.verts]
        return min(heights), max(heights)

    def build_now(self, armature, bone_name, occluder):
        mesh = bpy.data.meshes.new(self.name)
        bm = bmesh.new()
        bm_verts = [bm.verts.new(v) for v in self.verts]
        faces = []
        for face_index, (indices, color) in enumerate(self.faces):
            try:
                bm.faces.new([bm_verts[i] for i in indices])
                faces.append((face_index, indices, color))
            except ValueError:
                pass
        bm.to_mesh(mesh)
        bm.free()

        z_range = self.bounds_z()
        attribute = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for polygon, (face_index, indices, color) in zip(mesh.polygons, faces):
            points = [self.verts[i] for i in indices]
            normal = face_normal(points)
            jitter = face_jitter(self.name, face_index)
            for loop_index in polygon.loop_indices:
                corner = mesh.vertices[mesh.loops[loop_index].vertex_index].co
                painted = paint_corner(color, corner, normal, z_range, jitter, occluder)
                attribute.data[loop_index].color = (*srgb_to_linear(painted), 1.0)
        mesh.color_attributes.active_color = attribute
        material = bpy.data.materials.get(MATERIAL_NAME) or bpy.data.materials.new(MATERIAL_NAME)
        mesh.materials.append(material)

        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        bone = armature.data.bones[bone_name]
        bone_matrix = armature.matrix_world @ bone.matrix_local @ Matrix.Translation((0.0, bone.length, 0.0))
        obj.parent = armature
        obj.parent_type = "BONE"
        obj.parent_bone = bone_name
        obj.matrix_parent_inverse = bone_matrix.inverted()
        return obj


def head_of(armature, bone):
    return armature.matrix_world @ armature.data.bones[bone].head_local


# ---------------------------------------------------------------- body

def build_legs(armature):
    for side, sign in (("l", 1), ("r", -1)):
        light = (SKIN, SKIN_SHADE) if sign < 0 else (SKIN_SHADE, shade(SKIN_SHADE, 0.85))
        hip, knee, ankle, ball = (head_of(armature, f"{name}_{side}") for name in ("thigh", "calf", "foot", "ball"))
        thigh = Part(f"Thigh_{side.upper()}")
        thigh.limb(hip + Vector((0, 0, 0.03)), knee, 0.042, 0.045, SKIN, light)
        thigh.build(armature, f"thigh_{side}")

        calf = Part(f"Calf_{side.upper()}")
        calf.limb(knee, ankle + Vector((0, 0, 0.01)), 0.036, 0.038, SKIN, light)
        # Gold sandal strap under the knee, like the bands in the drawing.
        calf.box(knee + (ankle - knee) * 0.75, (0.04, 0.042, 0.012), GOLD)
        calf.build(armature, f"calf_{side}")

        foot = Part(f"Sandal_{side.upper()}")
        toe = Vector((ball.x, ball.y - 0.04, 0.02))
        foot.box(((ankle.x + toe.x) * 0.5, (ankle.y + toe.y) * 0.5, 0.014), (0.04, abs(toe.y - ankle.y) * 0.5 + 0.015, 0.014),
                 {"front": GOLD, "top": shade(GOLD, 1.1), "left": GOLD, "right": shade(GOLD, 0.8), "back": shade(GOLD, 0.7), "bottom": shade(GOLD, 0.5)})
        foot.box((ankle.x, ankle.y, 0.05), (0.035, 0.036, 0.024), SKIN)
        foot.build(armature, f"foot_{side}")


def build_hips(armature, pelvis):
    part = Part("Skirt")
    # Skirt: quetzal green on the left half, deep green on the right half, gold hem (drawing: y 222-262).
    part.frustum((pelvis.x, pelvis.y, 0.24), 0.135, 0.115, 0.2, 8, QUETZAL, DEEP, 0.85)
    part.frustum((pelvis.x, pelvis.y, 0.235), 0.14, 0.138, 0.03, 8, GOLD, shade(GOLD, 0.8), 0.86)
    # Belt (drawing: gold band y 212-222).
    part.frustum((pelvis.x, pelvis.y, 0.43), 0.12, 0.12, 0.045, 8, GOLD, shade(GOLD, 0.8), 0.85)
    part.build(armature, "pelvis")


def build_torso(armature):
    part = Part("Tunic")
    spine = head_of(armature, "spine_02")
    # Tunic: limestone, the right half in shadow (drawing: y 152-224).
    part.box((spine.x, spine.y, 0.585), (0.112, 0.085, 0.12),
             {"front": LIMESTONE, "left": LIMESTONE, "right": mix(LIMESTONE, DEEP, 0.35), "top": LIMESTONE,
              "back": mix(LIMESTONE, DEEP, 0.4), "bottom": shade(LIMESTONE, 0.6)})
    # Right half of the front darker, as in the drawing.
    part.face([(0.0, spine.y - 0.0855, 0.466), (0.112, spine.y - 0.0855, 0.466), (0.112, spine.y - 0.0855, 0.704), (0.0, spine.y - 0.0855, 0.704)],
              mix(LIMESTONE, DEEP, 0.3))
    # Jade triangle on the chest (drawing: y 160-180).
    y = spine.y - 0.087
    part.face([(-0.045, y, 0.69), (0.045, y, 0.69), (0.018, y, 0.6), (-0.018, y, 0.6)], JADE)
    part.build(armature, "spine_02")


def build_arms(armature):
    for side, sign in (("l", 1), ("r", -1)):
        light = (SKIN, SKIN_SHADE) if sign < 0 else (SKIN_SHADE, shade(SKIN_SHADE, 0.85))
        shoulder, elbow, wrist = (head_of(armature, f"{name}_{side}") for name in ("upperarm", "lowerarm", "hand"))
        upper = Part(f"UpperArm_{side.upper()}")
        upper.limb(shoulder - Vector((0.03 * sign, 0, 0)), elbow, 0.034, 0.036, SKIN, light)
        upper.build(armature, f"upperarm_{side}")

        lower = Part(f"Forearm_{side.upper()}")
        lower.limb(elbow, wrist, 0.031, 0.033, SKIN, light)
        lower.build(armature, f"lowerarm_{side}")

        hand = Part(f"Hand_{side.upper()}")
        hand.box(wrist + Vector((0.045 * sign, -0.005, -0.004)), (0.045, 0.04, 0.038), SKIN)
        hand.build(armature, f"hand_{side}")


def build_cape(armature):
    """Feather cape behind the back (drawing: wide deep green wedge with a zigzag of feathers at the bottom)."""
    part = Part("Cape")
    columns = 6
    top_z, bottom_z = 0.7, 0.17
    top_half, bottom_half = 0.17, 0.26
    back_top, back_bottom = 0.1, 0.19
    for i in range(columns):
        t0, t1 = i / columns, (i + 1) / columns
        a = Vector((-top_half + 2 * top_half * t0, back_top, top_z))
        b = Vector((-top_half + 2 * top_half * t1, back_top, top_z))
        c = Vector((-bottom_half + 2 * bottom_half * t1, back_bottom, bottom_z))
        d = Vector((-bottom_half + 2 * bottom_half * t0, back_bottom, bottom_z))
        part.face([a, d, c, b], DEEP if i % 2 == 0 else shade(DEEP, 0.85), True)
        tip = (c + d) * 0.5 + Vector((0, 0.01, -0.1))
        part.face([d, tip, c], QUETZAL if i % 2 == 0 else JADE, True)
    part.build(armature, "spine_03")


def build_collar(armature):
    """Lower jaw of the serpent around the neck: a green ring with two white teeth at the front (drawing: y 142-156)."""
    part = Part("Collar")
    neck = head_of(armature, "neck_01")
    part.frustum((neck.x, neck.y, 0.675), 0.125, 0.105, 0.05, 10, QUETZAL, shade(QUETZAL, 0.8), 0.85)
    y = neck.y - 0.11
    for x in (-0.05, 0.05):
        part.face([(x - 0.022, y, 0.722), (x + 0.022, y, 0.722), (x, y - 0.004, 0.77)], LIMESTONE, True)
    part.build(armature, "neck_01")


def build_head(armature):
    """Face from the drawing (y 100-146): skin, two dark eyes, a gold mouth; the serpent helmet with its feathers on top."""
    part = Part("Head")
    head = head_of(armature, "head")
    cx, cy = head.x, head.y - 0.01
    # Faceted face: an octagonal prism, lighter on the left half.
    part.frustum((cx, cy, 0.715), 0.112, 0.122, 0.185, 8, SKIN, SKIN_SHADE, 0.85)
    front = cy - 0.104
    for x in (-0.042, 0.042):
        part.box((cx + x, front, 0.83), (0.019, 0.008, 0.009), EYE)
    part.box((cx, front + 0.002, 0.77), (0.028, 0.008, 0.009), GOLD)

    # Upper jaw of the serpent as the helmet (y 68-110): wide at the brow, narrower at the top, pushed a bit forward.
    brow, crown = 0.9, 1.065
    bl, br = Vector((cx - 0.155, cy - 0.16, brow)), Vector((cx + 0.155, cy - 0.16, brow))
    tl, tr = Vector((cx - 0.08, cy - 0.11, crown)), Vector((cx + 0.08, cy - 0.11, crown))
    back_bl, back_br = Vector((cx - 0.15, cy + 0.12, brow)), Vector((cx + 0.15, cy + 0.12, brow))
    back_tl, back_tr = Vector((cx - 0.08, cy + 0.08, crown)), Vector((cx + 0.08, cy + 0.08, crown))
    part.face([bl, br, tr, tl], QUETZAL)                         # front
    part.face([(bl + br) * 0.5, br, tr, (tl + tr) * 0.5], DEEP)  # right half of the front in shadow, as drawn
    part.face([br, back_br, back_tr, tr], shade(DEEP, 0.9))      # right side
    part.face([back_bl, bl, tl, back_tl], QUETZAL)               # left side
    part.face([tl, tr, back_tr, back_tl], shade(QUETZAL, 1.1))   # top
    part.face([back_br, back_bl, back_tl, back_tr], shade(DEEP, 0.8))  # back
    part.face([bl, back_bl, back_br, br], shade(DEEP, 0.6))      # underside
    # Red band on the edge of the jaw, golden eyes, white fangs at the corners.
    y = cy - 0.163
    part.face([(cx - 0.155, y, brow - 0.004), (cx + 0.155, y, brow - 0.004), (cx + 0.155, y, brow + 0.018), (cx - 0.155, y, brow + 0.018)], RED)
    for sign in (-1, 1):
        x0 = cx + 0.07 * sign
        part.face([(x0 - 0.022 * sign, y - 0.002, 0.985), (x0 + 0.018 * sign, y - 0.002, 0.995), (x0 + 0.018 * sign, y - 0.002, 0.965)], GOLD, True)
        xf = cx + 0.135 * sign
        part.face([(xf - 0.016, y, brow), (xf + 0.016, y, brow), (xf, y - 0.002, brow - 0.055)], LIMESTONE, True)

    # Quetzal feathers fanning from the top of the helmet (drawing: red, green, jade, green, jade; the middle one tallest).
    base_z = crown - 0.01
    feathers = [(-0.04, (-0.26, 1.2), RED), (-0.02, (-0.15, 1.32), QUETZAL), (0.0, (-0.03, 1.4), JADE),
                (0.02, (0.1, 1.36), QUETZAL), (0.04, (0.22, 1.26), JADE)]
    for offset, (tip_x, tip_z), color in feathers:
        part.feather((cx + offset, cy + 0.02, base_z), (cx + tip_x, cy + 0.07, tip_z), 0.035, color)
    part.build(armature, "head")


IDLE_FBX = os.path.join(ROOT, "Assets", "RPG Tiny Hero Duo", "Animation", "SwordAndShield", "Idle_Battle_SwordAndShiled.fbx")


def idle_direction(armature, bone_name, world_direction):
    """
    Direction in the rest pose (T-pose) that points along world_direction when the bone is in the idle pose of the game.
    Used to hold the staff upright at the side in idle, like in the drawing, while it stays rigid in the hand.
    """
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=IDLE_FBX, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    source = next(o for o in new if o.type == "ARMATURE")
    armature.animation_data_create()
    armature.animation_data.action = source.animation_data.action
    try:
        armature.animation_data.action_slot = armature.animation_data.action_suitable_slots[0]
    except (AttributeError, IndexError):
        pass
    bpy.context.scene.frame_set(10)
    bpy.context.view_layer.update()
    posed = (armature.matrix_world @ armature.pose.bones[bone_name].matrix).to_3x3().normalized()
    rest = (armature.matrix_world @ armature.data.bones[bone_name].matrix_local).to_3x3().normalized()
    local = posed.inverted() @ world_direction
    armature.animation_data.action = None
    # Back to the rest pose: the bones keep the last evaluated pose after the action is removed.
    for pose_bone in armature.pose.bones:
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)
    bpy.context.scene.frame_set(0)
    for obj in new:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.update()
    return (rest @ local).normalized()


def build_staff(armature):
    """Gold staff in the right hand with a feathered serpent head on top (drawing: left side, y 112-334)."""
    part = Part("Staff")
    hand = head_of(armature, "hand_r") + Vector((-0.045, -0.005, -0.004))
    grip = idle_direction(armature, "hand_r", Vector((0, 0, 1)))
    bottom, top = hand - grip * 0.42, hand + grip * 0.45
    axis = grip
    side = Vector((1, 0, 0)) if abs(axis.x) < 0.9 else Vector((0, 1, 0))
    side = (side - axis * side.dot(axis)).normalized()
    other = axis.cross(side)
    # Hexagonal shaft.
    ring_a, ring_b = [], []
    for i in range(6):
        angle = math.tau * i / 6
        offset = (side * math.cos(angle) + other * math.sin(angle)) * 0.014
        ring_a.append(bottom + offset)
        ring_b.append(top + offset)
    for i in range(6):
        j = (i + 1) % 6
        part.face([ring_a[i], ring_a[j], ring_b[j], ring_b[i]], shade(GOLD, 0.75 + 0.25 * math.cos(math.tau * i / 6)))
    # Red wrap under the head (drawing: y 118-126).
    part.box(hand + grip * 0.3, (0.02, 0.02, 0.022), RED, (side, other, axis))
    # Serpent head: green block bending outward, gold eye, red forked tongue.
    head_center = top + grip * 0.04 - side * 0.03
    part.box(head_center, (0.055, 0.03, 0.035), {"front": QUETZAL, "right": DEEP, "left": QUETZAL, "top": shade(QUETZAL, 1.1),
                                                  "back": DEEP, "bottom": shade(DEEP, 0.7)}, (side, other, axis))
    part.box(head_center - side * 0.07 + grip * 0.01, (0.025, 0.026, 0.026), DEEP, (side, other, axis))
    part.box(head_center - side * 0.03 + other * -0.031 + grip * 0.015, (0.008, 0.004, 0.008), GOLD, (side, other, axis))
    tongue = head_center - side * 0.095
    part.face([tongue + grip * 0.006, tongue - grip * 0.006, tongue - side * 0.05 + grip * 0.012], RED, True)
    part.face([tongue + grip * 0.006, tongue - grip * 0.006, tongue - side * 0.05 - grip * 0.012], RED, True)
    part.build(armature, "hand_r")


def build_orb(armature):
    """Jade orb held in the left hand (drawing: right side, y 212-234): an octahedron with a light facet."""
    part = Part("Orb")
    center = head_of(armature, "hand_l") + Vector((0.11, -0.01, 0.0))
    r = 0.05
    top, bottom = center + Vector((0, 0, r)), center - Vector((0, 0, r))
    ring = [center + Vector((r, 0, 0)), center + Vector((0, -r, 0)), center + Vector((-r, 0, 0)), center + Vector((0, r, 0))]
    for i in range(4):
        j = (i + 1) % 4
        part.face([ring[i], ring[j], top], LIMESTONE if i == 1 else JADE)
        part.face([ring[j], ring[i], bottom], shade(JADE, 0.75))
    part.build(armature, "hand_l")


# ---------------------------------------------------------------- main

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=SOURCE_FBX, automatic_bone_orientation=False)
    armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
        bpy.data.objects.remove(obj, do_unlink=True)

    pelvis = head_of(armature, "pelvis")
    build_legs(armature)
    build_hips(armature, pelvis)
    build_torso(armature)
    build_arms(armature)
    build_cape(armature)
    build_collar(armature)
    build_head(armature)
    build_staff(armature)
    build_orb(armature)

    occluder = Occluder([part for part, _, _ in PENDING])
    for part, part_armature, bone_name in PENDING:
        part.build_now(part_armature, bone_name, occluder)

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
    triangles = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    print("MAGE EXPORTED", OUTPUT, "faces", triangles)


main()
