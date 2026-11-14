"""Low-poly footbridges (kladki) built from boxes, colored like the LD57 assets.

Faceted vertex colors in the `Col` attribute (Face Corner), material `LD57 Stylized`, pivot on the
ground in the middle. Sized for the Wulkan_Kaluza_lawy puddles (about 2.4-2.9 m): 3.6 m long,
scaled together with the puddle by the scatter layer (Inherit Parent Scale).
"""

import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

LENGTH = 3.6
WIDTH = 0.9


def srgb(hex_color):
    def channel(c):
        c = int(c, 16) / 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    h = hex_color.lstrip("#")
    return (channel(h[0:2]), channel(h[2:4]), channel(h[4:6]), 1.0)


WOOD_DARK, WOOD_LIGHT = srgb("#5c3f29"), srgb("#8a6542")
CHAR = srgb("#3a2a20")
ROPE = srgb("#6e5a3a")
STONE_DARK, STONE_LIGHT = srgb("#2c2b30"), srgb("#57545c")


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def shade(color, factor):
    return tuple(min(c * factor, 1.0) for c in color[:3]) + (1.0,)


class Builder:
    def __init__(self, seed):
        self.bm = bmesh.new()
        self.color = self.bm.loops.layers.float_color.new("Col")
        self.rng = random.Random(seed)

    def box(self, center, size, color, tilt=0.0, yaw=0.0):
        """Box with its own color; every face gets a small brightness jitter (asset rule)."""
        result = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = result["verts"]
        transform = (Matrix.Translation(center) @ Matrix.Rotation(yaw, 4, 'Z')
                     @ Matrix.Rotation(tilt, 4, 'Y') @ Matrix.Diagonal((*size, 1.0)))
        bmesh.ops.transform(self.bm, matrix=transform, verts=verts)
        faces = {f for v in verts for f in v.link_faces}
        for face in faces:
            face.normal_update()
        for face in faces:
            tint = shade(color, 1.0 + self.rng.uniform(-0.1, 0.1) + 0.12 * face.normal.z)
            for loop in face.loops:
                loop[self.color] = tint

    def finish(self, name, material):
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for polygon in mesh.polygons:
            polygon.use_smooth = False
        mesh.color_attributes.active_color = mesh.color_attributes["Col"]
        if material is not None:
            mesh.materials.append(material)
        return mesh


def deck_point(t, arch, base):
    """Point on the deck centerline, t in [0, 1] along the bridge."""
    x = (t - 0.5) * LENGTH
    z = base + arch * math.sin(math.pi * t)
    slope = arch * math.pi * math.cos(math.pi * t) / LENGTH
    return Vector((x, 0.0, z)), math.atan(slope)


def wooden_bridge(builder, arch, planks, rails, missing=()):
    base = 0.14
    for i in range(planks):
        if i in missing:
            continue
        t = (i + 0.5) / planks
        point, angle = deck_point(t, arch, base)
        color = mix(WOOD_DARK, WOOD_LIGHT, builder.rng.random())
        if builder.rng.random() < 0.15:
            color = CHAR  # a burnt plank, it is a volcano after all
        jitter = builder.rng.uniform(-0.04, 0.04)
        builder.box(point + Vector((0.0, jitter, 0.0)),
                    (LENGTH / planks * 0.86, WIDTH * builder.rng.uniform(0.92, 1.05), 0.07),
                    color, tilt=-angle, yaw=builder.rng.uniform(-0.04, 0.04))
    # two beams under the planks
    for side in (-1.0, 1.0):
        for i in range(planks):
            t0, t1 = i / planks, (i + 1) / planks
            p0, _a = deck_point(t0, arch, base - 0.08)
            p1, _b = deck_point(t1, arch, base - 0.08)
            mid, direction = (p0 + p1) * 0.5, p1 - p0
            builder.box(mid + Vector((0.0, side * WIDTH * 0.32, 0.0)), (direction.length * 1.02, 0.09, 0.09),
                        WOOD_DARK, tilt=-math.atan2(direction.z, direction.x))
    for side in rails:
        y = side * (WIDTH * 0.5 + 0.03)
        for t in (0.0, 0.5, 1.0):
            point, _angle = deck_point(t, arch, base)
            height = 0.62
            builder.box(Vector((point.x, y, (point.z + height) * 0.5)), (0.09, 0.09, point.z + height),
                        WOOD_DARK)
        segments = 6
        for i in range(segments):
            p0, _a = deck_point(i / segments, arch, base + 0.55)
            p1, _b = deck_point((i + 1) / segments, arch, base + 0.55)
            mid, direction = (p0 + p1) * 0.5, p1 - p0
            builder.box(Vector((mid.x, y, mid.z)), (direction.length * 1.03, 0.05, 0.05), ROPE,
                        tilt=-math.atan2(direction.z, direction.x))
    # stones holding the ends
    for end in (-1.0, 1.0):
        for k in range(2):
            builder.box(Vector((end * (LENGTH * 0.5 - 0.1), (k - 0.5) * WIDTH * 0.7, 0.07)),
                        (0.35, 0.32, 0.2), mix(STONE_DARK, STONE_LIGHT, builder.rng.random()),
                        yaw=builder.rng.uniform(-0.4, 0.4))


def stone_bridge(builder):
    """Three basalt slabs on stepping stones."""
    arch, base = 0.35, 0.12
    slabs = 3
    for i in range(slabs):
        t = (i + 0.5) / slabs
        point, angle = deck_point(t, arch, base)
        builder.box(point, (LENGTH / slabs * 0.95, WIDTH * builder.rng.uniform(0.85, 1.0), 0.16),
                    mix(STONE_DARK, STONE_LIGHT, builder.rng.random()), tilt=-angle,
                    yaw=builder.rng.uniform(-0.06, 0.06))
    for t in (1.0 / 3.0, 2.0 / 3.0):
        point, _angle = deck_point(t, arch, base)
        builder.box(Vector((point.x, 0.0, point.z * 0.5)), (0.45, WIDTH * 0.7, point.z),
                    mix(STONE_DARK, STONE_LIGHT, builder.rng.random() * 0.5))


VARIANTS = (
    ("Wulkan_Kladka_v0", lambda b: wooden_bridge(b, arch=0.45, planks=9, rails=(-1.0, 1.0))),
    ("Wulkan_Kladka_v1", lambda b: wooden_bridge(b, arch=0.18, planks=8, rails=(1.0,))),
    ("Wulkan_Kladka_v2", lambda b: wooden_bridge(b, arch=0.4, planks=9, rails=(-1.0, 1.0), missing=(3, 6))),
    ("Wulkan_Kladka_v3", stone_bridge),
)


def build_bridges(collection, material):
    objects = []
    for index, (name, build) in enumerate(VARIANTS):
        builder = Builder(seed=1000 + index)
        build(builder)
        obj = bpy.data.objects.new(name, builder.finish(name, material))
        obj.location = (index * 5.0, -30.0, 0.0)
        collection.objects.link(obj)
        objects.append(obj)
    return objects
