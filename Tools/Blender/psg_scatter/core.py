"""PSG core: path sampling, instanced scatter, ground vertex-color paint, live updates."""

import json
import math
import random
import time

import bpy
import numpy as np
from bpy.app.handlers import persistent
from mathutils import Matrix, Quaternion, Vector, noise
from mathutils.bvhtree import BVHTree
from mathutils.geometry import interpolate_bezier

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


def path_instances(path_obj):
    return [o for o in bpy.data.objects if o.psg_inst.path == path_obj]


def find_layer(path_obj, uid):
    for layer in path_obj.psg.layers:
        if layer.uid == uid:
            return layer
    return None


def source_objects(collection):
    if collection is None:
        return []
    objects = [o for o in collection.all_objects if o.type == 'MESH' and o.psg_inst.path is None]
    return sorted(objects, key=lambda o: o.name)


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


def repaint_ground(ground):
    """Recompute the whole ground color from its base color and every path that targets it."""
    if ground is None or ground.type != 'MESH':
        return
    mesh = ground.data
    count = len(mesh.vertices)
    if count == 0:
        return
    co = np.empty(count * 3, dtype=np.float32)
    mesh.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3).astype(np.float64)
    matrix = np.array(ground.matrix_world, dtype=np.float64)
    world = co @ matrix[:3, :3].T + matrix[:3, 3]
    points_xy = world[:, :2]

    color = np.tile(np.array(ground.psg_ground.base_color, dtype=np.float64), (count, 1))
    coverage = np.zeros(count)
    paths = [p for p in all_paths() if p.psg.target == ground and p.psg.paint_enabled]
    paths.sort(key=lambda p: (p.psg.paint_order, p.name))
    for path in paths:
        mask = paint_mask(path, points_xy) * path.psg.paint_strength
        tint = np.array(path.psg.paint_color[:3], dtype=np.float64)
        color[:, :3] = color[:, :3] * (1.0 - mask[:, None]) + tint[None, :] * mask[:, None]
        coverage = np.maximum(coverage, mask)
    color[:, 3] = coverage if ground.psg_ground.mask_in_alpha else 1.0

    loops = np.empty(len(mesh.loops), dtype=np.int32)
    mesh.loops.foreach_get("vertex_index", loops)
    attr = ensure_color_attribute(mesh)
    attr.data.foreach_set("color", color[loops].astype(np.float32).ravel())
    mesh.update()


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


def layer_placements(path, layer, polylines, raycast, occupied):
    """Yield (index, source, matrix) for one layer."""
    sources = source_objects(layer.source)
    if not sources:
        return
    spatial = SpatialHash(layer.min_distance)
    for p in occupied:
        spatial.add(p)
    max_slope_cos = math.cos(layer.max_slope)
    for index, position, tangent, rolls in layer_candidates(layer, polylines):
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
        if spatial.blocked(position):
            continue
        spatial.add(position)
        source = pick_weighted(sources, rolls[0])
        yield index, source, placement_matrix(layer, position, tangent, normal, rolls, source)


def make_instance(path, layer, index, source, matrix, collection):
    """Linked duplicate: a real object sharing the source mesh (exports as shared mesh to FBX)."""
    name = "%s__%s_%s_%04d" % (source.name, path.name, layer.name, index)
    obj = bpy.data.objects.new(name, source.data)
    collection.objects.link(obj)
    obj.matrix_basis = matrix
    inst = obj.psg_inst
    inst.path = path
    inst.layer_uid = layer.uid
    inst.index = index
    inst.locked = False
    inst.source_mesh = source.data.name
    inst.gen_matrix = matrix_to_list(matrix)
    return obj


def remove_object(obj):
    bpy.data.objects.remove(obj, do_unlink=True)


_busy = False


def regenerate(path, depsgraph=None):
    """Rebuild a path's instances, keeping locked/edited ones and remembering deletions."""
    global _busy
    if path is None or not path.psg.is_path:
        return 0
    _busy = True
    try:
        return _regenerate(path, depsgraph or bpy.context.evaluated_depsgraph_get())
    finally:
        _busy = False
        _signatures[path.name] = path_signature(path)
        _mute_until[0] = time.monotonic() + 0.25


def _regenerate(path, depsgraph):
    settings = path.psg
    previous = load_json(settings.state_json)
    excluded = load_json(settings.excluded_json)
    layer_uids = {str(layer.uid) for layer in settings.layers}

    instances = []
    for obj in path_instances(path):
        if obj.users == 0 or not obj.users_collection:
            remove_object(obj)  # deleted/unlinked by the user, counts as a deletion
        else:
            instances.append(obj)
    present = {}
    for obj in instances:
        present.setdefault(str(obj.psg_inst.layer_uid), set()).add(obj.psg_inst.index)

    if settings.keep_deletions:
        for uid, indices in previous.items():
            missing = indices - present.get(uid, set())
            if missing:
                excluded.setdefault(uid, set()).update(missing)

    kept = {}
    occupied = {}
    for obj in instances:
        inst = obj.psg_inst
        uid = str(inst.layer_uid)
        if inst.locked or (settings.auto_lock_edited and instance_was_edited(obj)):
            if uid in layer_uids:
                inst.locked = True
                kept.setdefault(uid, set()).add(inst.index)
                occupied.setdefault(uid, []).append(obj.matrix_basis.translation.copy())
                continue
            if inst.locked:
                continue
        remove_object(obj)

    polylines = path_polylines(path)
    raycast = ground_raycaster(settings.target, depsgraph)
    collection = output_collection(path)
    state = {}
    created = 0
    for layer in settings.layers:
        uid = str(layer.uid)
        state[uid] = set(kept.get(uid, set()))
        if not layer.enabled:
            continue
        skip = kept.get(uid, set()) | excluded.get(uid, set())
        for index, source, matrix in layer_placements(path, layer, polylines, raycast, occupied.get(uid, [])):
            if index in skip:
                continue
            make_instance(path, layer, index, source, matrix, collection)
            state[uid].add(index)
            created += 1

    settings.state_json = dump_json(state)
    settings.excluded_json = dump_json({k: v for k, v in excluded.items() if k in layer_uids})

    repaint_targets(path)
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
    for obj in path_instances(path):
        if include_locked or not obj.psg_inst.locked:
            remove_object(obj)
    path.psg.state_json = ""


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
    sources = source_objects(layer.source)
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


def add_layer(path, source=None, copy_from=None):
    settings = path.psg
    layer = settings.layers.add()
    if copy_from is not None:
        for prop in copy_from.bl_rna.properties:
            if not prop.is_readonly and prop.identifier not in {"uid", "rna_type"}:
                setattr(layer, prop.identifier, getattr(copy_from, prop.identifier))
        layer.name = copy_from.name + "_copy"
    else:
        layer.name = source.name if source is not None else "Layer"
        layer.source = source
    settings.next_uid += 1
    layer.uid = settings.next_uid
    layer.seed = layer.uid
    settings.active_layer = len(settings.layers) - 1
    return layer


# --------------------------------------------------------------------------------------------
# Live update (debounced, never touches data from inside the depsgraph handler)
# --------------------------------------------------------------------------------------------

_pending = {}
_signatures = {}
_mute_until = [0.0]


def schedule(path, scatter=True):
    if _busy or path is None or not path.psg.is_path or not path.psg.live_update:
        return
    _pending[path.name] = _pending.get(path.name, False) or scatter
    if not bpy.app.timers.is_registered(_flush):
        bpy.app.timers.register(_flush, first_interval=0.15)


def schedule_ground(ground):
    if not _busy and ground is not None:
        _pending["\0ground:" + ground.name] = False
        if not bpy.app.timers.is_registered(_flush):
            bpy.app.timers.register(_flush, first_interval=0.15)


def _flush():
    jobs = dict(_pending)
    _pending.clear()
    for key, scatter in jobs.items():
        if key.startswith("\0ground:"):
            repaint_ground(bpy.data.objects.get(key[len("\0ground:"):]))
            continue
        path = bpy.data.objects.get(key)
        if path is None or not path.psg.is_path:
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
        if obj.type != 'CURVE' or not obj.psg.is_path or not obj.psg.live_update:
            continue
        if not (update.is_updated_geometry or update.is_updated_transform):
            continue
        signature = path_signature(obj)
        if _signatures.get(obj.name) != signature:
            _signatures[obj.name] = signature
            schedule(obj, scatter=True)


@persistent
def on_load(_dummy):
    _signatures.clear()
    _pending.clear()
    for path in all_paths():
        _signatures[path.name] = path_signature(path)


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
