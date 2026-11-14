"""PSG terrain shaping: low-poly plateaus, valleys and small hills with a hard height budget.

Non-destructive: the original vertex positions are kept in the `psg_rest` attribute, every
reshape starts from them. Height is normalized to [0, Max Height], so the terrain never gets
taller than the budget (important once the segment is bent around the torus).
"""

import math

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise

REST_ATTRIBUTE = "psg_rest"

# name -> settings; used by the UI preset buttons
PRESETS = {
    'GENTLE': dict(max_height=1.5, base_size=28.0, hill_amount=0.45, hill_size=7.0, valley_amount=0.25,
                   valley_width=0.12, valley_size=45.0, plateau_amount=0.0, plateau_levels=3, cliff=0.3),
    'PLATEAUS': dict(max_height=2.5, base_size=30.0, hill_amount=0.15, hill_size=6.0, valley_amount=0.35,
                     valley_width=0.12, valley_size=40.0, plateau_amount=0.9, plateau_levels=3, cliff=0.18),
    'VALLEYS': dict(max_height=2.2, base_size=35.0, hill_amount=0.25, hill_size=6.0, valley_amount=0.8,
                    valley_width=0.22, valley_size=30.0, plateau_amount=0.35, plateau_levels=4, cliff=0.3),
    'MIXED': dict(max_height=2.0, base_size=30.0, hill_amount=0.3, hill_size=6.0, valley_amount=0.5,
                  valley_width=0.15, valley_size=38.0, plateau_amount=0.6, plateau_levels=3, cliff=0.22),
}


def _fbm(x, y, z, octaves):
    value, amplitude, frequency, total = 0.0, 1.0, 1.0, 0.0
    for _ in range(octaves):
        value += noise.noise(Vector((x * frequency, y * frequency, z))) * amplitude
        total += amplitude
        amplitude *= 0.5
        frequency *= 2.03
    return value / total


def _field(xy, size, seed, octaves):
    inv = 1.0 / max(size, 1e-3)
    return np.fromiter((_fbm(x * inv, y * inv, seed, octaves) for x, y in xy), dtype=np.float64, count=len(xy))


def _smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / max(edge1 - edge0, 1e-9), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _normalize(values):
    low, high = np.percentile(values, 1.0), np.percentile(values, 99.0)
    return np.clip((values - low) / max(high - low, 1e-9), 0.0, 1.0)


def _hash01(indices, salt):
    x = (indices.astype(np.uint64) * np.uint64(2654435761) + np.uint64(salt)) & np.uint64(0xFFFFFFFF)
    x ^= x >> np.uint64(15)
    x = (x * np.uint64(2246822519)) & np.uint64(0xFFFFFFFF)
    x ^= x >> np.uint64(13)
    return (x & np.uint64(0xFFFFFF)).astype(np.float64) / float(0x1000000)


def rest_positions(mesh):
    """Original vertex positions (local), stored once in a point attribute."""
    attr = mesh.attributes.get(REST_ATTRIBUTE)
    count = len(mesh.vertices)
    if attr is None or attr.domain != 'POINT' or attr.data_type != 'FLOAT_VECTOR':
        if attr is not None:
            mesh.attributes.remove(attr)
        co = np.empty(count * 3, dtype=np.float32)
        mesh.vertices.foreach_get("co", co)
        attr = mesh.attributes.new(REST_ATTRIBUTE, 'FLOAT_VECTOR', 'POINT')
        attr.data.foreach_set("vector", co)
    rest = np.empty(count * 3, dtype=np.float32)
    attr.data.foreach_get("vector", rest)
    return rest.reshape(-1, 3).astype(np.float64)


def triangulate(mesh):
    if all(len(p.vertices) == 3 for p in mesh.polygons):
        return
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='SHORT_EDGE', ngon_method='BEAUTY')
    bm.to_mesh(mesh)
    bm.free()


def cell_size(rest):
    span = rest[:, :2].max(axis=0) - rest[:, :2].min(axis=0)
    area = max(float(span[0] * span[1]), 1e-6)
    return math.sqrt(area / max(len(rest), 1))


def heights(settings, xy):
    """Height in [0, 1] from base noise, valleys, terraces (plateaus) and small hills."""
    seed = settings.seed * 17.31
    base = _field(xy, settings.base_size, seed, 4)
    base = _normalize(base)

    if settings.valley_amount > 0.0:
        ridge = np.abs(_field(xy, settings.valley_size, seed + 101.0, 2))
        carve = 1.0 - _smoothstep(0.0, max(settings.valley_width, 1e-3), ridge)
        base = base * (1.0 - carve * settings.valley_amount)
        base = _normalize(base)

    if settings.plateau_amount > 0.0 and settings.plateau_levels > 0:
        levels = settings.plateau_levels
        scaled = base * levels
        step = np.floor(scaled)
        frac = scaled - step
        cliff = max(settings.cliff, 0.02)
        rise = _smoothstep(1.0 - cliff, 1.0, frac)
        terraced = np.minimum((step + rise) / levels, 1.0)
        base = base + (terraced - base) * settings.plateau_amount

    if settings.hill_amount > 0.0:
        hills = _field(xy, settings.hill_size, seed + 211.0, 2)
        base = base + hills * settings.hill_amount * 0.18

    return np.clip(base, 0.0, 1.0)


def edge_mask(xy, width):
    if width <= 0.0:
        return np.ones(len(xy))
    low, high = xy.min(axis=0), xy.max(axis=0)
    distance = np.minimum(xy - low, high - xy).min(axis=1)
    return _smoothstep(0.0, width, distance)


def shape_terrain(obj):
    """Reshape a mesh from its rest positions using obj.psg_shape."""
    settings = obj.psg_shape
    mesh = obj.data
    if settings.triangulate:
        rest_positions(mesh)  # store before topology changes keep vertex order
        triangulate(mesh)
    rest = rest_positions(mesh)
    xy = rest[:, :2].copy()

    if settings.jitter > 0.0:
        # break the grid: random XY offset, border vertices only slide along their border
        cell = cell_size(rest)
        ids = np.arange(len(rest))
        offset = (np.stack([_hash01(ids, 11 + settings.seed), _hash01(ids, 977 + settings.seed)], axis=1)
                  * 2.0 - 1.0) * settings.jitter * cell * 0.5
        low, high = xy.min(axis=0), xy.max(axis=0)
        on_border = (np.abs(xy - low) < 1e-5) | (np.abs(xy - high) < 1e-5)
        offset[on_border] = 0.0
        xy = xy + offset

    height = heights(settings, xy)
    mask = edge_mask(xy, settings.edge_flatten)
    height = settings.edge_height + (height - settings.edge_height) * mask

    co = np.empty((len(rest), 3))
    co[:, :2] = xy
    co[:, 2] = rest[:, 2] + height * settings.max_height
    mesh.vertices.foreach_set("co", co.astype(np.float32).ravel())
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    return float(co[:, 2].max() - co[:, 2].min())


def reset_shape(obj):
    mesh = obj.data
    attr = mesh.attributes.get(REST_ATTRIBUTE)
    if attr is None:
        return False
    rest = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    attr.data.foreach_get("vector", rest)
    mesh.vertices.foreach_set("co", rest)
    mesh.update()
    return True


def apply_preset(settings, name):
    for key, value in PRESETS[name].items():
        setattr(settings, key, value)
