"""PSG core: path sampling, instanced scatter, ground vertex-color paint, live updates."""

import json
import math
import random
import time
import zlib

import bpy
import numpy as np
from bpy.app.handlers import persistent
from mathutils import Matrix, Quaternion, Vector, noise
from mathutils.bvhtree import BVHTree
from mathutils.geometry import interpolate_bezier

from . import shape

OUTPUT_ROOT = "PSG_Generated"
PATHS_COLLECTION = "PSG_Paths"
COLOR_ATTRIBUTE = "Col"
GROUND_MATERIAL = "LD57 Stylized"
MATRIX_TOLERANCE = 1e-4


# --------------------------------------------------------------------------------------------
# Path sampling
# --------------------------------------------------------------------------------------------

class Polyline:
    """World-space polyline with arc-length sampling."""

    def __init__(self, points):
        self.points = points
        self.lengths = [0.0]
        for a, b in zip(points, points[1:]):
            self.lengths.append(self.lengths[-1] + (b - a).length)

    @property
    def length(self):
        return self.lengths[-1]

    def sample(self, distance):
        distance = min(max(distance, 0.0), self.length)
        lo, hi = 0, len(self.lengths) - 1
        while hi - lo > 1:
            mid = (lo + hi) // 2
            if self.lengths[mid] <= distance:
                lo = mid
            else:
                hi = mid
        a, b = self.points[lo], self.points[hi]
        seg = self.lengths[hi] - self.lengths[lo]
        t = 0.0 if seg <= 1e-9 else (distance - self.lengths[lo]) / seg
        tangent = b - a
        if tangent.length_squared < 1e-12:
            tangent = Vector((1.0, 0.0, 0.0))
        return a.lerp(b, t), tangent.normalized()

    def xy_segments(self):
        pts = np.array([(p.x, p.y) for p in self.points], dtype=np.float64)
        return pts[:-1], pts[1:]


def side_vector(tangent):
    side = Vector((-tangent.y, tangent.x, 0.0))
    if side.length_squared < 1e-12:
        return Vector((1.0, 0.0, 0.0))
    return side.normalized()


def path_polylines(path_obj):
    """Polylines (world space) of every spline of a curve object. Works in edit mode too."""
    if path_obj is None or path_obj.type != 'CURVE':
        return []
    matrix = path_obj.matrix_world
    result = []
    for spline in path_obj.data.splines:
        points = []
        if spline.type == 'BEZIER':
            bezier = spline.bezier_points
            count = len(bezier)
            resolution = max(spline.resolution_u, 2)
            segments = count - 1 + (1 if spline.use_cyclic_u and count > 1 else 0)
            for i in range(segments):
                a = bezier[i]
                b = bezier[(i + 1) % count]
                chunk = interpolate_bezier(a.co, a.handle_right, b.handle_left, b.co, resolution + 1)
                points.extend(chunk if not points else chunk[1:])
        else:
            points = [p.co.xyz for p in spline.points]
            if spline.use_cyclic_u and len(points) > 1:
                points.append(points[0].copy())
        world = []
        for p in points:
            w = matrix @ Vector(p)
            if not world or (w - world[-1]).length_squared > 1e-10:
                world.append(w)
        if len(world) >= 2:
            result.append(Polyline(world))
    return result


def path_signature(path_obj):
    """Cheap hash of path geometry + transform, used to detect real edits for live update."""
    values = [round(v, 5) for row in path_obj.matrix_world for v in row]
    for spline in path_obj.data.splines:
        if spline.type == 'BEZIER':
            for p in spline.bezier_points:
                values.extend(round(v, 5) for v in (*p.co, *p.handle_left, *p.handle_right))
        else:
            for p in spline.points:
                values.extend(round(v, 5) for v in p.co)
        values.append(spline.resolution_u)
        values.append(int(spline.use_cyclic_u))
    return hash(tuple(values))


# --------------------------------------------------------------------------------------------
# Lookups
# --------------------------------------------------------------------------------------------

def all_paths():
    return [o for o in bpy.data.objects if o.type == 'CURVE' and o.psg.is_path]


def is_area(obj):
    return obj is not None and obj.type == 'MESH' and obj.psg.is_area


def is_generator(obj):
    return obj is not None and ((obj.type == 'CURVE' and obj.psg.is_path) or is_area(obj))


def all_generators():
    return [o for o in bpy.data.objects if is_generator(o)]


def path_instances(path_obj):
    return [o for o in bpy.data.objects if o.psg_inst.path == path_obj]


def find_layer(path_obj, uid):
    for layer in path_obj.psg.layers:
        if layer.uid == uid:
            return layer
    return None


def name_matches(name, name_filter):
    tokens = [t.strip().lower() for t in name_filter.split(",") if t.strip()]
    if not tokens:
        return True
    name = name.lower()
    include = [t for t in tokens if not t.startswith("!")]
    exclude = [t[1:] for t in tokens if t.startswith("!") and len(t) > 1]
    if any(t in name for t in exclude):
        return False
    return not include or any(t in name for t in include)


def source_objects(collection, name_filter=""):
    if collection is None:
        return []
    objects = [o for o in collection.all_objects
               if o.type == 'MESH' and o.psg_inst.path is None and not o.psg.is_area
               and name_matches(o.name, name_filter)]
    return sorted(objects, key=lambda o: o.name)


def layer_sources(layer):
    return source_objects(layer.source, layer.source_filter)


def source_weight(obj):
    try:
        return max(float(obj.get("psg_weight", 1.0)), 0.0)
    except (TypeError, ValueError):
        return 1.0


def pick_weighted(objects, roll):
    weights = [source_weight(o) for o in objects]
    total = sum(weights)
    if total <= 0.0:
        return objects[int(roll * len(objects)) % len(objects)]
    acc = 0.0
    target = roll * total
    for obj, weight in zip(objects, weights):
        acc += weight
        if target < acc:
            return obj
    return objects[-1]


def load_json(text):
    try:
        data = json.loads(text) if text else {}
        return {str(k): set(v) for k, v in data.items()}
    except (ValueError, TypeError):
        return {}


def dump_json(data):
    return json.dumps({k: sorted(v) for k, v in data.items() if v})


def matrix_to_list(matrix):
    return [v for col in matrix.col for v in col]


def stored_matrix_differs(obj):
    stored = obj.psg_inst.gen_matrix
    current = matrix_to_list(obj.matrix_basis)
    return any(abs(a - b) > MATRIX_TOLERANCE for a, b in zip(stored, current))


def instance_was_edited(obj):
    inst = obj.psg_inst
    if stored_matrix_differs(obj):
        return True
    return obj.data is not None and obj.data.name != inst.source_mesh


# --------------------------------------------------------------------------------------------
# Collections and ground
# --------------------------------------------------------------------------------------------

def scene_for(obj):
    scenes = obj.users_scene
    return scenes[0] if scenes else bpy.context.scene


def ensure_child_collection(parent, name):
    for child in parent.children:
        if child.name == name or child.name.startswith(name + "."):
            return child
    coll = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if coll.name not in parent.children:
        parent.children.link(coll)
    return coll


def output_collection(path_obj):
    settings = path_obj.psg
    if settings.output is not None and settings.output.users > 0:
        return settings.output
    root = ensure_child_collection(scene_for(path_obj).collection, OUTPUT_ROOT)
    coll = bpy.data.collections.new("PSG_" + path_obj.name)
    root.children.link(coll)
    settings.output = coll
    return coll


def paths_collection(scene):
    return ensure_child_collection(scene.collection, PATHS_COLLECTION)


def ground_material():
    material = bpy.data.materials.get(GROUND_MATERIAL)
    if material is not None:
        return material
    material = bpy.data.materials.new(GROUND_MATERIAL)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is not None:
        color = nodes.new("ShaderNodeVertexColor")
        color.layer_name = COLOR_ATTRIBUTE
        color.location = (bsdf.location.x - 300, bsdf.location.y)
        material.node_tree.links.new(color.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.9
    return material


def ground_raycaster(ground, depsgraph):
    if ground is None or ground.type != 'MESH':
        return None
    tree = BVHTree.FromObject(ground, depsgraph)
    matrix = ground.matrix_world
    inverse = matrix.inverted_safe()
    normal_matrix = inverse.transposed().to_3x3()
    corners = [matrix @ Vector(c) for c in ground.bound_box]
    top = max(c.z for c in corners) + 100.0

    def cast(x, y):
        origin = inverse @ Vector((x, y, top))
        direction = (inverse.to_3x3() @ Vector((0.0, 0.0, -1.0))).normalized()
        location, normal, _index, _dist = tree.ray_cast(origin, direction)
        if location is None:
            return None, None
        world_normal = (normal_matrix @ normal).normalized()
        if world_normal.z < 0.0:
            world_normal = -world_normal
        return matrix @ location, world_normal

    return cast


def create_ground(paths, cell, margin, scene):
    """Grid plane under the given paths, sized to their XY bounds."""
    points = [p for path in paths for pl in path_polylines(path) for p in pl.points]
    if not points:
        return None
    extra = margin
    for path in paths:
        reach = [path.psg.paint_width + path.psg.paint_falloff]
        reach.extend(layer.width for layer in path.psg.layers)
        extra = max(extra, margin + max(reach))
    min_x = min(p.x for p in points) - extra
    max_x = max(p.x for p in points) + extra
    min_y = min(p.y for p in points) - extra
    max_y = max(p.y for p in points) + extra
    z = min(p.z for p in points)
    cell = max(cell, 0.05)
    nx = max(int(math.ceil((max_x - min_x) / cell)), 1)
    ny = max(int(math.ceil((max_y - min_y) / cell)), 1)

    xs = np.linspace(min_x, max_x, nx + 1)
    ys = np.linspace(min_y, max_y, ny + 1)
    gx, gy = np.meshgrid(xs, ys)
    verts = np.stack([gx.ravel(), gy.ravel(), np.full(gx.size, z)], axis=1)
    i = np.arange(ny)[:, None] * (nx + 1) + np.arange(nx)[None, :]
    i = i.ravel()
    faces = np.stack([i, i + 1, i + nx + 2, i + nx + 1], axis=1)

    mesh = bpy.data.meshes.new("PSG_Ground")
    mesh.vertices.add(len(verts))
    mesh.vertices.foreach_set("co", verts.astype(np.float32).ravel())
    mesh.loops.add(faces.size)
    mesh.loops.foreach_set("vertex_index", faces.astype(np.int32).ravel())
    mesh.polygons.add(len(faces))
    mesh.polygons.foreach_set("loop_start", (np.arange(len(faces)) * 4).astype(np.int32))
    mesh.update(calc_edges=True)
    mesh.validate()
    mesh.materials.append(ground_material())

    ground = bpy.data.objects.new("PSG_Ground", mesh)
    scene.collection.objects.link(ground)
    ground.psg_ground.is_ground = True
    for path in paths:
        path.psg.target = ground
    repaint_ground(ground)
    return ground


# --------------------------------------------------------------------------------------------
# Ground paint (vertex colors with blended edges)
# --------------------------------------------------------------------------------------------

def distance_to_polylines_xy(points_xy, polylines):
    best = np.full(len(points_xy), np.inf)
    block = 16
    for pl in polylines:
        seg_a, seg_b = pl.xy_segments()
        for start in range(0, len(seg_a), block):
            a = seg_a[start:start + block]
            ab = seg_b[start:start + block] - a
            denom = (ab * ab).sum(axis=1)
            denom[denom < 1e-12] = 1e-12
            ap = points_xy[:, None, :] - a[None, :, :]
            t = np.clip((ap * ab[None]).sum(axis=2) / denom[None], 0.0, 1.0)
            diff = ap - t[..., None] * ab[None]
            dist = np.sqrt((diff * diff).sum(axis=2)).min(axis=1)
            np.minimum(best, dist, out=best)
    return best


def ensure_color_attribute(mesh):
    attr = mesh.color_attributes.get(COLOR_ATTRIBUTE)
    if attr is not None and (attr.domain != 'CORNER' or attr.data_type != 'FLOAT_COLOR'):
        mesh.color_attributes.remove(attr)
        attr = None
    if attr is None:
        attr = mesh.color_attributes.new(COLOR_ATTRIBUTE, 'FLOAT_COLOR', 'CORNER')
    mesh.color_attributes.active_color = attr
    for index, candidate in enumerate(mesh.color_attributes):
        if candidate.name == attr.name:
            mesh.color_attributes.render_color_index = index
    return attr


def paint_mask(path, points_xy):
    settings = path.psg
    polylines = path_polylines(path)
    if not polylines:
        return np.zeros(len(points_xy))
    dist = distance_to_polylines_xy(points_xy, polylines)
    if settings.paint_noise > 0.0:
        scale = max(settings.paint_noise_scale, 1e-4)
        seed = float(settings.paint_seed) * 7.31
        offsets = np.fromiter(
            (noise.noise(Vector((x * scale, y * scale, seed))) for x, y in points_xy),
            dtype=np.float64, count=len(points_xy))
        dist = dist + offsets * settings.paint_noise
    width = settings.paint_width
    falloff = settings.paint_falloff
    if falloff <= 1e-6:
        return (dist <= width).astype(np.float64)
    t = np.clip((dist - width) / falloff, 0.0, 1.0)
    return 1.0 - t * t * (3.0 - 2.0 * t)


def _smoothstep(edge0, edge1, x):
    if edge1 - edge0 <= 1e-9:
        return (x >= edge1).astype(np.float64)
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _lerp(a, b, t):
    return a + (b - a) * t[:, None]


def _noise_field(points, scale, seed):
    return np.fromiter(
        (noise.noise(Vector((p[0] * scale, p[1] * scale, p[2] * scale + seed))) for p in points),
        dtype=np.float64, count=len(points))


def _hash01(indices, seed):
    """Deterministic per-element random in [0, 1) (per-face jitter like the assets)."""
    x = (indices.astype(np.uint64) * np.uint64(2654435761) + np.uint64(seed * 97 + 13)) & np.uint64(0xFFFFFFFF)
    x ^= x >> np.uint64(15)
    x = (x * np.uint64(2246822519)) & np.uint64(0xFFFFFFFF)
    x ^= x >> np.uint64(13)
    return (x & np.uint64(0xFFFFFF)).astype(np.float64) / float(0x1000000)


def shaped_height_range(obj):
    """World Z range a shaped terrain can span: rest level .. rest level + Max Height.

    Fixed per budget (not per mesh), so equal heights on neighbouring pieces get equal colors.
    """
    if not obj.psg_shape.enabled:
        return None
    attr = obj.data.attributes.get(shape.REST_ATTRIBUTE)
    if attr is None:
        return None
    rest = np.empty(len(obj.data.vertices) * 3, dtype=np.float32)
    attr.data.foreach_get("vector", rest)
    matrix = np.array(obj.matrix_world, dtype=np.float64)
    world = rest.reshape(-1, 3).astype(np.float64) @ matrix[:3, :3].T + matrix[:3, 3]
    low = float(world[:, 2].min())
    return low, low + obj.psg_shape.max_height * float(np.linalg.norm(matrix[:3, 2]))


def terrain_colors(settings, points, normals, element_ids, z_range=None):
    """Asset-style terrain shading: Z gradient (dark low, light high), slope palette, biased noise."""
    count = len(points)
    z = points[:, 2]
    if settings.gradient_auto and z_range is not None:
        z_min, z_max = z_range
    elif settings.gradient_auto:
        z_min, z_max = float(z.min()), float(z.max())
    else:
        z_min, z_max = settings.gradient_min, settings.gradient_max
    if z_max - z_min > 1e-4:
        height = np.clip((z - z_min) / (z_max - z_min), 0.0, 1.0)
    else:
        height = np.full(count, 0.5)
    # Z grading: the same dark -> light rule as the assets, blended towards the middle by strength
    t = 0.5 + (height - 0.5) * settings.gradient_strength
    if settings.noise_brightness > 0.0 or settings.noise_bias > 0.0:
        field = _noise_field(points, max(settings.noise_scale, 1e-4), settings.seed * 3.17)
    else:
        field = np.zeros(count)
    t = np.clip(t + field * settings.noise_brightness, 0.0, 1.0)

    flat = _lerp(np.array(settings.flat_dark), np.array(settings.flat_light), t)
    slope = _lerp(np.array(settings.slope_dark), np.array(settings.slope_light), t)
    angle = np.arccos(np.clip(np.abs(normals[:, 2]), 0.0, 1.0))
    half = settings.slope_blend * 0.5
    slope_mask = _smoothstep(settings.slope_angle - half, settings.slope_angle + half, angle)
    color = _lerp(flat, slope, slope_mask)

    if settings.noise_bias > 0.0:
        # second, larger noise picks patches tinted towards the bias color (dry grass, sand, ...)
        patches = _noise_field(points, max(settings.noise_scale, 1e-4) * 0.45, settings.seed * 3.17 + 41.0)
        amount = _smoothstep(-0.1, 0.6, patches) * settings.noise_bias * (1.0 - slope_mask)
        color = _lerp(color, np.tile(np.array(settings.bias_color), (count, 1)), amount)
    return color


def repaint_ground(ground):
    """Recompute the whole ground color: terrain shading (or flat base) + every path that targets it."""
    if ground is None or ground.type != 'MESH':
        return
    mesh = ground.data
    settings = ground.psg_ground
    count = len(mesh.vertices)
    if count == 0:
        return
    matrix = np.array(ground.matrix_world, dtype=np.float64)
    rotation = matrix[:3, :3]
    normal_matrix = np.linalg.inv(rotation).T if abs(np.linalg.det(rotation)) > 1e-12 else rotation

    faceted = settings.terrain_enabled and settings.faceted
    if faceted:
        element_count = len(mesh.polygons)
        co = np.empty(element_count * 3, dtype=np.float32)
        mesh.polygons.foreach_get("center", co)
        nrm = np.empty(element_count * 3, dtype=np.float32)
        mesh.polygons.foreach_get("normal", nrm)
    else:
        element_count = count
        co = np.empty(count * 3, dtype=np.float32)
        mesh.vertices.foreach_get("co", co)
        nrm = np.empty(count * 3, dtype=np.float32)
        mesh.vertex_normals.foreach_get("vector", nrm)
    points = co.reshape(-1, 3).astype(np.float64) @ rotation.T + matrix[:3, 3]
    normals = nrm.reshape(-1, 3).astype(np.float64) @ normal_matrix.T
    normals /= np.maximum(np.linalg.norm(normals, axis=1), 1e-12)[:, None]
    ids = np.arange(element_count)

    if settings.terrain_enabled:
        rgb = terrain_colors(settings, points, normals, ids, shaped_height_range(ground))
    else:
        rgb = np.tile(np.array(settings.base_color[:3], dtype=np.float64), (element_count, 1))

    coverage = np.zeros(element_count)
    paths = [p for p in all_paths() if p.psg.target == ground and p.psg.paint_enabled]
    paths.sort(key=lambda p: (p.psg.paint_order, p.name))
    for path in paths:
        mask = paint_mask(path, points[:, :2]) * path.psg.paint_strength
        tint = np.array(path.psg.paint_color[:3], dtype=np.float64)
        rgb = rgb * (1.0 - mask[:, None]) + tint[None, :] * mask[:, None]
        coverage = np.maximum(coverage, mask)

    if settings.terrain_enabled and settings.facet_jitter > 0.0:
        # per-element brightness spread, the "hand painted facets" rule of the assets
        jitter = 1.0 + (_hash01(ids, settings.seed) * 2.0 - 1.0) * settings.facet_jitter
        rgb = rgb * jitter[:, None]

    color = np.empty((element_count, 4))
    color[:, :3] = np.clip(rgb, 0.0, None)
    color[:, 3] = coverage if settings.mask_in_alpha else 1.0

    if faceted:
        loop_totals = np.empty(len(mesh.polygons), dtype=np.int32)
        mesh.polygons.foreach_get("loop_total", loop_totals)
        corner_source = np.repeat(np.arange(len(mesh.polygons)), loop_totals)
    else:
        corner_source = np.empty(len(mesh.loops), dtype=np.int32)
        mesh.loops.foreach_get("vertex_index", corner_source)
    attr = ensure_color_attribute(mesh)
    attr.data.foreach_set("color", color[corner_source].astype(np.float32).ravel())
    mesh.update()


# Terrain palettes sampled from the biome assets: role -> (object name filter, low pct, high pct).
TERRAIN_SOURCES = {
    "Las": {"flat": ("Las_Trawa_v0, Las_Trawa_v1", 0.1, 0.6), "slope": ("Las_Kamyki", 0.1, 0.9),
            "bias": ("Las_Trawa_v2, Las_Trawa_v3", 0.4, 0.6)},
    "Pustynia": {"flat": ("Pustynia_Skala_warstwowa", 0.55, 0.95), "slope": ("Pustynia_Kamyki", 0.1, 0.9),
                 "bias": ("Pustynia_Sucha_trawa", 0.4, 0.6)},
    "Wybrzeze": {"flat": ("Wybrzeze_Trawa_wydmowa", 0.3, 0.9), "slope": ("Wybrzeze_Otoczaki", 0.1, 0.9),
                 "bias": ("Wybrzeze_Wyrzucone_drewno", 0.4, 0.6)},
    "Lod": {"flat": ("Lod_Zaspa", 0.1, 0.9), "slope": ("Lod_Skala_ze_sniegiem", 0.05, 0.45),
            "bias": ("Lod_Lodowe_kolce", 0.4, 0.6)},
    "Wulkan": {"flat": ("Wulkan_Kamyki", 0.1, 0.9), "slope": ("Wulkan_Kolumny_bazaltowe", 0.1, 0.6),
               "bias": ("Wulkan_Zuzel", 0.4, 0.6)},
    "Bagno": {"flat": ("Bagno_Turzyca", 0.1, 0.8), "slope": ("Bagno_Omszala_kloda", 0.1, 0.6),
              "bias": ("Bagno_Trzciny", 0.4, 0.6)},
}


def asset_colors(name_filter):
    """Linear RGB of every face corner of the matching asset objects, sorted by luminance."""
    chunks = []
    for obj in bpy.data.objects:
        if obj.type != 'MESH' or obj.psg_inst.path is not None or not name_matches(obj.name, name_filter):
            continue
        attr = obj.data.color_attributes.get(COLOR_ATTRIBUTE)
        if attr is None:
            continue
        data = np.empty(len(attr.data) * 4, dtype=np.float32)
        attr.data.foreach_get("color", data)
        chunks.append(data.reshape(-1, 4)[:, :3].astype(np.float64))
    if not chunks:
        return None
    colors = np.concatenate(chunks)
    luminance = colors @ np.array([0.2126, 0.7152, 0.0722])
    return colors[np.argsort(luminance)]


def _percentile_color(colors, pct):
    """Average of a small luminance window around the percentile (stable against single faces)."""
    n = len(colors)
    center = int(pct * (n - 1))
    half = max(n // 40, 1)
    return colors[max(center - half, 0):min(center + half + 1, n)].mean(axis=0)


def sample_terrain_palette(ground, biome):
    """Set the ground palette from the biome assets. Returns the list of roles that found no assets."""
    settings = ground.psg_ground
    missing = []
    for role, (name_filter, low, high) in TERRAIN_SOURCES[biome].items():
        colors = asset_colors(name_filter)
        if colors is None:
            missing.append(name_filter)
            continue
        dark, light = _percentile_color(colors, low), _percentile_color(colors, high)
        if role == "flat":
            settings.flat_dark, settings.flat_light = dark, light
        elif role == "slope":
            settings.slope_dark, settings.slope_light = dark, light
        else:
            settings.bias_color = (dark + light) * 0.5
    return missing


# --------------------------------------------------------------------------------------------
# Scatter
# --------------------------------------------------------------------------------------------

def lateral_offset(layer, rng):
    """Signed distance from the path centerline, honoring the inner (clear) zone."""
    inner = min(layer.inner_width, layer.width)
    u = rng.random()
    side = -1.0 if rng.random() < 0.5 else 1.0
    return side * (inner + (layer.width - inner) * u), u


def layer_candidates(layer, polylines):
    """Deterministic candidates: (index, position, tangent, rolls). Index is stable per seed."""
    rng = random.Random("psg:%d:%d" % (layer.seed, layer.uid))
    candidates = []
    index = 0

    def emit(pl, distance, lateral, edge_t):
        nonlocal index
        rolls = (rng.random(), rng.random(), rng.random(), rng.random())
        position, tangent = pl.sample(distance)
        position = position + side_vector(tangent) * lateral
        keep = not (layer.edge_falloff > 0.0 and rolls[3] < layer.edge_falloff * edge_t)
        if keep:
            candidates.append((index, position, tangent, rolls))
        index += 1

    for pl in polylines:
        length = pl.length
        if layer.mode == 'BAND':
            band = max(layer.width - min(layer.inner_width, layer.width), 0.0) * 2.0
            count = int(round(layer.density * length * max(band, 1e-3)))
            for _ in range(count):
                distance = rng.random() * length
                lateral, u = lateral_offset(layer, rng)
                emit(pl, distance, lateral, u)
            continue
        step = max(layer.spacing, 0.05)
        distance = step * 0.5
        while distance <= length:
            jittered = distance + (rng.random() - 0.5) * layer.jitter * step
            if layer.mode == 'EDGES':
                for side in (-1.0, 1.0):
                    spread = (rng.random() - 0.5) * layer.jitter * step
                    emit(pl, jittered, side * max(layer.width + spread, 0.0), 0.0)
            else:
                if layer.width > 0.0:
                    lateral, u = lateral_offset(layer, rng)
                else:
                    lateral, u = 0.0, 0.0
                emit(pl, jittered, lateral, u)
            distance += step
    return candidates


class SpatialHash:
    def __init__(self, radius):
        self.radius = radius
        self.cells = {}

    def _key(self, p):
        return (int(math.floor(p.x / self.radius)), int(math.floor(p.y / self.radius)))

    def blocked(self, p):
        if self.radius <= 0.0:
            return False
        kx, ky = self._key(p)
        r2 = self.radius * self.radius
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for q in self.cells.get((kx + dx, ky + dy), ()):
                    if (q.x - p.x) ** 2 + (q.y - p.y) ** 2 < r2:
                        return True
        return False

    def add(self, p):
        if self.radius > 0.0:
            self.cells.setdefault(self._key(p), []).append(p)


def placement_matrix(layer, position, tangent, normal, rolls, source):
    yaw = layer.yaw_offset + (rolls[1] - 0.5) * layer.yaw_random
    if layer.align_to_path:
        yaw += math.atan2(tangent.y, tangent.x)
    rotation = Quaternion((0.0, 0.0, 1.0), yaw)
    if normal is not None and layer.align_to_normal > 0.0:
        tilt = Vector((0.0, 0.0, 1.0)).rotation_difference(normal)
        rotation = Quaternion().slerp(tilt, layer.align_to_normal) @ rotation
    scale = layer.scale_min + (layer.scale_max - layer.scale_min) * rolls[2]
    up = normal if normal is not None else Vector((0.0, 0.0, 1.0))
    location = position + up * layer.z_offset
    source_basis = source.matrix_world.to_3x3().to_4x4()
    return (Matrix.Translation(location) @ rotation.to_matrix().to_4x4()
            @ Matrix.Scale(scale, 4) @ source_basis)


class SurfaceSampler:
    """Uniform sampling and top-down ray casting on a mesh surface (world space)."""

    def __init__(self, obj, depsgraph):
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            count = len(mesh.vertices)
            co = np.empty(count * 3, dtype=np.float32)
            mesh.vertices.foreach_get("co", co)
            co = co.reshape(-1, 3).astype(np.float64)
            tris = np.empty(len(mesh.loop_triangles) * 3, dtype=np.int32)
            mesh.loop_triangles.foreach_get("vertices", tris)
            tris = tris.reshape(-1, 3)
        finally:
            evaluated.to_mesh_clear()
        matrix = np.array(obj.matrix_world, dtype=np.float64)
        self.verts = co @ matrix[:3, :3].T + matrix[:3, 3]
        self.tris = tris
        a, b, c = (self.verts[tris[:, i]] for i in range(3))
        areas = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1) if len(tris) else np.zeros(0)
        self.cumulative = np.cumsum(areas)
        self.area = float(self.cumulative[-1]) if len(areas) else 0.0
        self.tree = BVHTree.FromPolygons([tuple(v) for v in self.verts], [tuple(t) for t in tris]) if len(tris) else None
        self.top = float(self.verts[:, 2].max()) + 100.0 if count else 100.0

    def sample(self, rng):
        tri = int(np.searchsorted(self.cumulative, rng.random() * self.area, side='right'))
        tri = min(tri, len(self.tris) - 1)
        u, v = rng.random(), rng.random()
        if u + v > 1.0:
            u, v = 1.0 - u, 1.0 - v
        a, b, c = (self.verts[self.tris[tri, i]] for i in range(3))
        return Vector(a + (b - a) * u + (c - a) * v)

    def raycast(self, x, y):
        if self.tree is None:
            return None, None
        location, normal, _index, _dist = self.tree.ray_cast(Vector((x, y, self.top)), Vector((0.0, 0.0, -1.0)))
        if location is None:
            return None, None
        if normal.z < 0.0:
            normal = -normal
        return location, normal.normalized()


def stable_index(*parts):
    return zlib.crc32(":".join(str(p) for p in parts).encode()) & 0x7FFFFFFF


def random_rolls(rng):
    return (rng.random(), rng.random(), rng.random(), rng.random())


def area_candidates(layer, sampler):
    rng = random.Random("psg:%d:%d" % (layer.seed, layer.uid))
    no_tangent = Vector((1.0, 0.0, 0.0))
    if sampler is None or sampler.area <= 0.0:
        return []
    candidates = []
    if layer.mode == 'SCATTER':
        for index in range(int(round(layer.density * sampler.area))):
            candidates.append((index, sampler.sample(rng), no_tangent, random_rolls(rng)))
    elif layer.mode == 'CLUSTER':
        clusters = int(round(layer.cluster_density * sampler.area / 100.0))
        low, high = sorted((layer.cluster_min, layer.cluster_max))
        for cluster in range(clusters):
            center = sampler.sample(rng)
            single = rng.random() < layer.single_chance
            count = 1 if single else rng.randint(low, high)
            for k in range(count):
                angle = rng.random() * math.tau
                radius = 0.0 if single else layer.cluster_radius * math.sqrt(rng.random())
                offset = Vector((math.cos(angle) * radius, math.sin(angle) * radius, 0.0))
                candidates.append((cluster * 128 + k, center + offset, no_tangent, random_rolls(rng)))
    return candidates


def around_candidates(layer, parents):
    """Candidates around parent instances; index derives from the parent index (stable per parent)."""
    candidates = []
    no_tangent = Vector((1.0, 0.0, 0.0))
    low, high = sorted((layer.around_count_min, layer.around_count_max))
    r_low, r_high = sorted((layer.around_min, layer.around_max))
    for parent_index, parent_position, parent_scale in sorted(parents, key=lambda p: p[0]):
        rng = random.Random("psg:%d:%d:%d" % (layer.seed, layer.uid, parent_index))
        if rng.random() > layer.around_chance:
            continue
        factor = parent_scale if layer.around_scale_radius else 1.0
        for k in range(rng.randint(low, high)):
            angle = rng.random() * math.tau
            radius = math.sqrt(r_low * r_low + (r_high * r_high - r_low * r_low) * rng.random()) * factor
            offset = Vector((math.cos(angle) * radius, math.sin(angle) * radius, 0.0))
            candidates.append((stable_index(parent_index, k), parent_position + offset, no_tangent,
                               random_rolls(rng)))
    return candidates


def layer_placements(layer, candidates, raycast, occupied, avoid):
    """Yield (index, source, matrix, position, scale) for one layer."""
    sources = layer_sources(layer)
    if not sources:
        return
    spatial = SpatialHash(layer.min_distance)
    for p in occupied:
        spatial.add(p)
    max_slope_cos = math.cos(layer.max_slope)
    for index, position, tangent, rolls in candidates:
        normal = None
        if raycast is not None:
            hit, normal = raycast(position.x, position.y)
            if hit is None:
                if layer.skip_misses:
                    continue
            else:
                position = hit
                if normal.z < max_slope_cos - 1e-6:
                    continue
        if spatial.blocked(position) or (avoid is not None and avoid.blocked(position)):
            continue
        spatial.add(position)
        source = pick_weighted(sources, rolls[0])
        scale = layer.scale_min + (layer.scale_max - layer.scale_min) * rolls[2]
        yield index, source, placement_matrix(layer, position, tangent, normal, rolls, source), position, scale


def instance_name(path, layer, index, source):
    return "%s__%s_%s_%04d" % (source.name, path.name, layer.name, index)


def make_instance(path, layer, index, source, matrix, collection, reuse=None):
    """Linked duplicate: a real object sharing the source mesh (exports as shared mesh to FBX).

    With `reuse`, an existing instance is updated in place instead of creating a new object,
    which is much cheaper than removing and re-adding objects.
    """
    if reuse is None:
        obj = bpy.data.objects.new(instance_name(path, layer, index, source), source.data)
        collection.objects.link(obj)
        copy_material_slots(source, obj)
    else:
        obj = reuse
        if obj.data != source.data:
            obj.data = source.data
            obj.name = instance_name(path, layer, index, source)
            copy_material_slots(source, obj)
    obj.matrix_basis = matrix
    if obj.display_type != layer.display_type:
        obj.display_type = layer.display_type
    inst = obj.psg_inst
    if reuse is None:
        inst.path = path
        inst.layer_uid = layer.uid
        inst.index = index
    inst.locked = False
    inst.source_mesh = source.data.name
    inst.gen_matrix = matrix_to_list(matrix)
    return obj


def apply_layer_display(path, layer):
    for obj in path_instances(path):
        if obj.psg_inst.layer_uid == layer.uid and obj.display_type != layer.display_type:
            obj.display_type = layer.display_type


def remove_objects(objects):
    """Batch removal: removing objects one by one is O(n^2) in Blender."""
    objects = list(objects)
    if objects:
        bpy.data.batch_remove(objects)


def copy_material_slots(source, obj):
    """Match the source's slot layout (object-linked materials), otherwise FBX loses material indices."""
    for src_slot, slot in zip(source.material_slots, obj.material_slots):
        slot.link = src_slot.link
        if src_slot.link == 'OBJECT':
            slot.material = src_slot.material


_busy = False


def regenerate(path, depsgraph=None):
    """Rebuild a generator's (path or area) instances, keeping locked/edited ones and remembering deletions."""
    global _busy
    if not is_generator(path):
        return 0
    _busy = True
    try:
        return _regenerate(path, depsgraph or bpy.context.evaluated_depsgraph_get())
    finally:
        _busy = False
        _signatures[path.name] = generator_signature(path)
        _mute_until[0] = time.monotonic() + 0.25


def generator_signature(obj):
    if is_area(obj):
        return area_signature(obj)
    return path_signature(obj)


def area_signature(obj):
    mesh = obj.data
    co = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get("co", co)
    matrix = tuple(round(v, 5) for row in obj.matrix_world for v in row)
    return hash((matrix, len(mesh.polygons), np.round(co, 4).tobytes()))


def scale_of(matrix):
    return sum(matrix.to_scale()) / 3.0


def _regenerate(path, depsgraph):
    settings = path.psg
    previous = load_json(settings.state_json)
    excluded = load_json(settings.excluded_json)
    layer_uids = {str(layer.uid) for layer in settings.layers}

    instances = []
    orphans = []
    for obj in path_instances(path):
        if obj.users == 0 or not obj.users_collection:
            orphans.append(obj)  # deleted/unlinked by the user, counts as a deletion
        else:
            instances.append(obj)
    remove_objects(orphans)
    present = {}
    for obj in instances:
        present.setdefault(str(obj.psg_inst.layer_uid), set()).add(obj.psg_inst.index)

    if settings.keep_deletions:
        for uid, indices in previous.items():
            missing = indices - present.get(uid, set())
            if missing:
                excluded.setdefault(uid, set()).update(missing)

    kept = {}
    placed = {}  # uid -> [(index, position, scale)], feeds Around layers and avoidance
    reusable = {}  # (uid, index) -> unedited instance, updated in place if still generated
    leftovers = []
    for obj in instances:
        inst = obj.psg_inst
        uid = str(inst.layer_uid)
        if inst.locked or (settings.auto_lock_edited and instance_was_edited(obj)):
            if uid in layer_uids:
                inst.locked = True
                kept.setdefault(uid, set()).add(inst.index)
                placed.setdefault(uid, []).append(
                    (inst.index, obj.matrix_basis.translation.copy(), scale_of(obj.matrix_basis)))
                continue
            if inst.locked:
                continue
        key = (uid, inst.index)
        if key in reusable:
            leftovers.append(obj)  # duplicate (e.g. Shift+D copy that was not edited)
        else:
            reusable[key] = obj

    area = is_area(path)
    polylines = [] if area else path_polylines(path)
    sampler = SurfaceSampler(path, depsgraph) if area else None
    raycast = sampler.raycast if area else ground_raycaster(settings.target, depsgraph)
    collection = output_collection(path)
    state = {}
    created = 0
    for layer in settings.layers:
        uid = str(layer.uid)
        state[uid] = set(kept.get(uid, set()))
        occupied = [p for _i, p, _s in placed.get(uid, [])]
        if not layer.enabled:
            continue
        if layer.mode == 'AROUND':
            candidates = around_candidates(layer, placed.get(layer.parent, []))
        elif area:
            candidates = area_candidates(layer, sampler)
        else:
            candidates = layer_candidates(layer, polylines)
        avoid = None
        if layer.avoid_distance > 0.0:
            avoid = SpatialHash(layer.avoid_distance)
            for other_uid, items in placed.items():
                if other_uid != uid and not (layer.mode == 'AROUND' and other_uid == layer.parent):
                    for _i, p, _s in items:
                        avoid.add(p)
        skip = kept.get(uid, set()) | excluded.get(uid, set())
        for index, source, matrix, position, scale in layer_placements(layer, candidates, raycast, occupied, avoid):
            if index in skip:
                continue
            reuse = reusable.pop((uid, index), None)
            make_instance(path, layer, index, source, matrix, collection, reuse)
            state[uid].add(index)
            placed.setdefault(uid, []).append((index, position, scale))
            if reuse is None:
                created += 1

    leftovers.extend(reusable.values())
    remove_objects(leftovers)

    settings.state_json = dump_json(state)
    settings.excluded_json = dump_json({k: v for k, v in excluded.items() if k in layer_uids})
    settings.instance_count = sum(len(v) for v in state.values())
    settings.locked_count = sum(len(v) for v in kept.values())

    if not area:
        repaint_targets(path)
    elif path.psg_ground.is_ground:
        repaint_ground(path)  # terrain shading follows the edited area shape
    return created


def repaint_targets(path):
    settings = path.psg
    grounds = {settings.target} if settings.target is not None else set()
    if settings.painted_target and settings.painted_target in bpy.data.objects:
        grounds.add(bpy.data.objects[settings.painted_target])
    for ground in grounds:
        repaint_ground(ground)
    settings.painted_target = settings.target.name if settings.target is not None else ""


def clear(path, include_locked=False):
    instances = path_instances(path)
    remove_objects(o for o in instances if include_locked or not o.psg_inst.locked)
    path.psg.state_json = ""
    update_counts(path)


def update_counts(path):
    instances = path_instances(path)
    path.psg.instance_count = len(instances)
    path.psg.locked_count = sum(1 for o in instances if o.psg_inst.locked)


def reset_edits(path):
    path.psg.excluded_json = ""
    for obj in path_instances(path):
        obj.psg_inst.locked = False
        obj.psg_inst.gen_matrix = matrix_to_list(obj.matrix_basis)
        if obj.data is not None:
            obj.psg_inst.source_mesh = obj.data.name


def detach(obj):
    obj.psg_inst.path = None
    obj.psg_inst.locked = False


# --------------------------------------------------------------------------------------------
# Instance editing helpers
# --------------------------------------------------------------------------------------------

def instance_layer(obj):
    path = obj.psg_inst.path
    if path is None:
        return None, None
    return path, find_layer(path, obj.psg_inst.layer_uid)


def swap_variant(obj, mode, rng):
    path, layer = instance_layer(obj)
    if layer is None:
        return False
    sources = layer_sources(layer)
    if not sources:
        return False
    meshes = [s.data for s in sources]
    if mode == 'NEXT':
        try:
            current = meshes.index(obj.data)
        except ValueError:
            current = -1
        new_mesh = meshes[(current + 1) % len(meshes)]
    else:
        others = [s for s in sources if s.data != obj.data] or sources
        new_mesh = pick_weighted(others, rng.random()).data
    obj.data = new_mesh
    source = next((s for s in sources if s.data == new_mesh), None)
    if source is not None:
        copy_material_slots(source, obj)
    obj.psg_inst.locked = True
    return True


def reroll_transform(obj, rng):
    path, layer = instance_layer(obj)
    if layer is None:
        return False
    location, rotation, scale = obj.matrix_basis.decompose()
    normal = rotation @ Vector((0.0, 0.0, 1.0))
    spin = Quaternion(normal, (rng.random() - 0.5) * layer.yaw_random)
    ratio = (layer.scale_min + (layer.scale_max - layer.scale_min) * rng.random()) / max(max(scale), 1e-6)
    obj.matrix_basis = (Matrix.Translation(location) @ (spin @ rotation).to_matrix().to_4x4()
                        @ Matrix.Diagonal((*(scale * ratio), 1.0)))
    obj.psg_inst.locked = True
    return True


def snap_to_ground(obj, depsgraph):
    path = obj.psg_inst.path
    if is_area(path):
        ground = path
    else:
        ground = path.psg.target if path is not None else None
    raycast = ground_raycaster(ground, depsgraph)
    if raycast is None:
        return False
    location, rotation, scale = obj.matrix_basis.decompose()
    hit, _normal = raycast(location.x, location.y)
    if hit is None:
        return False
    _path, layer = instance_layer(obj)
    offset = layer.z_offset if layer is not None else 0.0
    obj.matrix_basis = (Matrix.Translation(hit + Vector((0.0, 0.0, offset)))
                        @ rotation.to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1.0)))
    obj.psg_inst.locked = True
    return True


# --------------------------------------------------------------------------------------------
# Annotation -> path
# --------------------------------------------------------------------------------------------

def current_frame(layer, frame_number):
    frames = sorted(layer.frames, key=lambda f: f.frame_number)
    shown = [f for f in frames if f.frame_number <= frame_number]
    if shown:
        return shown[-1]
    return frames[0] if frames else None


def annotation_strokes(scene):
    annotation = scene.annotation
    if annotation is None:
        return []
    strokes = []
    for layer in annotation.layers:
        if layer.annotation_hide:
            continue
        frame = layer.active_frame or current_frame(layer, scene.frame_current)
        if frame is None:
            continue
        for stroke in frame.strokes:
            if stroke.display_mode != '3DSPACE' or len(stroke.points) < 2:
                continue
            strokes.append((frame, stroke, [Vector(p.co) for p in stroke.points]))
    return strokes


def simplify(points, tolerance):
    """Ramer-Douglas-Peucker."""
    if tolerance <= 0.0 or len(points) < 3:
        return points
    first, last = points[0], points[-1]
    axis = last - first
    best_index, best_dist = 0, -1.0
    for i in range(1, len(points) - 1):
        if axis.length_squared < 1e-12:
            dist = (points[i] - first).length
        else:
            dist = (points[i] - first).cross(axis).length / axis.length
        if dist > best_dist:
            best_index, best_dist = i, dist
    if best_dist <= tolerance:
        return [first, last]
    left = simplify(points[:best_index + 1], tolerance)
    right = simplify(points[best_index:], tolerance)
    return left[:-1] + right


def create_path(points, scene, smooth, name="PSG_Path"):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 8
    if smooth:
        spline = curve.splines.new('BEZIER')
        spline.bezier_points.add(len(points) - 1)
        for point, co in zip(spline.bezier_points, points):
            point.co = co
            point.handle_left_type = 'AUTO'
            point.handle_right_type = 'AUTO'
    else:
        spline = curve.splines.new('POLY')
        spline.points.add(len(points) - 1)
        for point, co in zip(spline.points, points):
            point.co = (*co, 1.0)
    obj = bpy.data.objects.new(name, curve)
    paths_collection(scene).objects.link(obj)
    obj.show_in_front = True
    return obj


def init_path(obj, defaults):
    settings = obj.psg
    settings.is_path = True
    if settings.target is None and defaults.target is not None:
        settings.target = defaults.target
    if not settings.layers:
        add_layer(obj, defaults.source)


def init_area(obj, defaults):
    obj.psg.is_area = True
    if not obj.psg.layers:
        add_layer(obj, defaults.source)


def add_layer(path, source=None, copy_from=None):
    settings = path.psg
    layer = settings.layers.add()
    settings.next_uid += 1
    layer.uid = settings.next_uid
    if copy_from is not None:
        for prop in copy_from.bl_rna.properties:
            if not prop.is_readonly and prop.identifier not in {"uid", "rna_type"}:
                try:
                    setattr(layer, prop.identifier, getattr(copy_from, prop.identifier))
                except TypeError:
                    pass
        layer.name = copy_from.name + "_copy"
    else:
        layer.name = source.name if source is not None else "Layer"
        layer.source = source
        if is_area(path):
            layer.mode = 'SCATTER'
            layer.density = 0.05
    layer.seed = layer.uid
    settings.active_layer = len(settings.layers) - 1
    return layer


# Biome preset: (layer name, collection suffix, filter, mode, settings)
BIOME_PRESET = (
    ("Drzewa", "_Duze", "", 'CLUSTER',
     dict(cluster_density=0.6, cluster_radius=4.0, cluster_min=2, cluster_max=4, single_chance=0.35,
          min_distance=2.5, scale_min=0.85, scale_max=1.2, max_slope=math.radians(30.0))),
    ("Krzewy", "_Srednie", "", 'AROUND',
     dict(parent="Drzewa", around_min=1.5, around_max=4.0, around_count_min=0, around_count_max=2,
          around_chance=0.8, min_distance=1.0, scale_min=0.7, scale_max=1.1, max_slope=math.radians(35.0))),
    ("Trawa wokol", "_Drobne", "!Kamyki, !Otoczaki", 'AROUND',
     dict(parent="Drzewa", around_min=0.7, around_max=3.5, around_count_min=6, around_count_max=14,
          min_distance=0.3, scale_min=0.7, scale_max=1.2, max_slope=math.radians(35.0))),
    ("Trawa", "_Drobne", "!Kamyki, !Otoczaki, !Grzyby", 'SCATTER',
     dict(density=0.03, min_distance=0.5, avoid_distance=1.0, scale_min=0.7, scale_max=1.1,
          max_slope=math.radians(35.0))),
    ("Kamienie", "_Drobne", "Kamyki, Otoczaki", 'SCATTER',
     dict(density=0.008, min_distance=2.0, avoid_distance=1.5, scale_min=0.6, scale_max=1.3)),
)

# Per biome: name filter for the cluster (tree) layer, so big rocks do not form forests.
BIOME_TREE_FILTERS = {
    "Las": "Drzewo, !Martwe",
    "Pustynia": "Kaktus, Palma",
    "Wybrzeze": "Palma",
    "Lod": "Sosna",
    "Wulkan": "",
    "Bagno": "Drzewo",
}


def apply_biome_preset(area, biome, replace):
    """Adds the preset layers. Caller regenerates (live-update scheduling is suppressed here)."""
    global _busy
    previous_busy = _busy
    _busy = True
    try:
        return _apply_biome_preset(area, biome, replace)
    finally:
        _busy = previous_busy


def _apply_biome_preset(area, biome, replace):
    settings = area.psg
    if replace:
        remove_objects(path_instances(area))
        settings.layers.clear()
        settings.state_json = ""
        settings.excluded_json = ""
    uids = {}
    missing = []
    for name, suffix, name_filter, mode, values in BIOME_PRESET:
        collection = bpy.data.collections.get(biome + suffix)
        if collection is None:
            missing.append(biome + suffix)
        layer = add_layer(area, collection)
        layer.name = name
        layer.mode = mode
        layer.source_filter = BIOME_TREE_FILTERS.get(biome, "") if mode == 'CLUSTER' else name_filter
        for key, value in values.items():
            if key == "parent":
                value = str(uids.get(value, 0))
            setattr(layer, key, value)
        uids[name] = layer.uid
    return missing


# --------------------------------------------------------------------------------------------
# Live update (debounced, never touches data from inside the depsgraph handler)
# --------------------------------------------------------------------------------------------

_pending = {}
_signatures = {}
_mute_until = [0.0]
_last_request = [0.0]
DEBOUNCE = 0.35  # seconds of quiet (e.g. after releasing a slider) before regenerating


def _request():
    _last_request[0] = time.monotonic()
    if not bpy.app.timers.is_registered(_flush):
        bpy.app.timers.register(_flush, first_interval=DEBOUNCE)


def schedule(path, scatter=True):
    if _busy or not is_generator(path) or not path.psg.live_update:
        return
    if not scatter and is_area(path):
        return
    _pending[path.name] = _pending.get(path.name, False) or scatter
    _request()


def schedule_ground(ground):
    if not _busy and ground is not None:
        _pending["\0ground:" + ground.name] = False
        _request()


def schedule_shape(obj):
    if not _busy and obj is not None and obj.type == 'MESH':
        _pending["\0shape:" + obj.name] = False
        _request()


def run_shape(obj):
    """Reshape terrain, then re-snap its scatter and repaint it. Returns the height range."""
    if obj.mode == 'EDIT':
        return 0.0
    obj.psg_shape.enabled = True
    height_range = shape.shape_terrain(obj)
    # a depsgraph taken before reshaping still holds the old surface, so evaluate again
    depsgraph = bpy.context.evaluated_depsgraph_get()
    if is_area(obj):
        regenerate(obj, depsgraph)  # also repaints when it is a ground
    elif obj.psg_ground.is_ground:
        repaint_ground(obj)
    for path in all_paths():
        if path.psg.target == obj:
            regenerate(path, depsgraph)
    return height_range


def _flush():
    wait = DEBOUNCE - (time.monotonic() - _last_request[0])
    if wait > 0.01:
        return wait  # still changing: run again later
    jobs = dict(_pending)
    _pending.clear()
    for key, scatter in jobs.items():
        if key.startswith("\0ground:"):
            repaint_ground(bpy.data.objects.get(key[len("\0ground:"):]))
            continue
        if key.startswith("\0shape:"):
            target = bpy.data.objects.get(key[len("\0shape:"):])
            if target is not None and target.type == 'MESH':
                try:
                    run_shape(target)
                except Exception as error:
                    print("PSG: shaping %s failed: %s" % (key, error))
            continue
        path = bpy.data.objects.get(key)
        if not is_generator(path):
            continue
        try:
            if scatter:
                regenerate(path)
            else:
                repaint_targets(path)
        except Exception as error:  # keep the timer alive, report in console
            print("PSG: update of %s failed: %s" % (key, error))
    return None


@persistent
def on_depsgraph_update(_scene, depsgraph):
    if _busy or time.monotonic() < _mute_until[0]:
        return
    for update in depsgraph.updates:
        obj = update.id
        if not isinstance(obj, bpy.types.Object):
            continue
        obj = obj.original
        if not is_generator(obj) or not obj.psg.live_update:
            continue
        if not (update.is_updated_geometry or update.is_updated_transform):
            continue
        if is_area(obj) and obj.mode == 'EDIT':
            continue
        signature = generator_signature(obj)
        if _signatures.get(obj.name) != signature:
            _signatures[obj.name] = signature
            schedule(obj, scatter=True)


@persistent
def on_load(_dummy):
    _signatures.clear()
    _pending.clear()
    for generator in all_generators():
        _signatures[generator.name] = generator_signature(generator)


def register():
    bpy.app.handlers.depsgraph_update_post.append(on_depsgraph_update)
    bpy.app.handlers.load_post.append(on_load)


def unregister():
    if on_depsgraph_update in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.remove(on_depsgraph_update)
    if on_load in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.remove(on_load)
    if bpy.app.timers.is_registered(_flush):
        bpy.app.timers.unregister(_flush)
