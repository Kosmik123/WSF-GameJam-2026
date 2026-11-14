"""PSG terrain shaping: low-poly plateaus, valleys and small hills with a hard height budget.

Non-destructive: the original vertex positions are kept in the `psg_rest` attribute, every
reshape starts from them. Height is normalized to [0, Max Height], so the terrain never gets
taller than the budget (important once the segment is bent around the torus).
"""

import json
import math
import random

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise
from mathutils.kdtree import KDTree  # noqa: F401  (used by edge_mask and lake_crossings)

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


def edge_mask(xy, boundary, width):
    """0 on the mesh boundary, 1 farther than `width` from it (any outline, not just rectangles)."""
    if width <= 0.0:
        return np.ones(len(xy))
    border = np.flatnonzero(boundary)
    if len(border) == 0:
        return np.ones(len(xy))
    tree = KDTree(len(border))
    for i, index in enumerate(border):
        tree.insert((xy[index, 0], xy[index, 1], 0.0), i)
    tree.balance()
    distance = np.fromiter((tree.find((x, y, 0.0))[2] for x, y in xy), dtype=np.float64, count=len(xy))
    return _smoothstep(0.0, width, distance)


def boundary_vertices(mesh):
    """Vertices on open (boundary) edges: edges used by exactly one face."""
    edge_count = len(mesh.edges)
    loop_edges = np.empty(len(mesh.loops), dtype=np.int32)
    mesh.loops.foreach_get("edge_index", loop_edges)
    uses = np.bincount(loop_edges, minlength=edge_count)
    edge_verts = np.empty(edge_count * 2, dtype=np.int32)
    mesh.edges.foreach_get("vertices", edge_verts)
    edge_verts = edge_verts.reshape(-1, 2)
    on_border = np.zeros(len(mesh.vertices), dtype=bool)
    on_border[edge_verts[uses == 1].ravel()] = True
    return on_border


def shape_terrain(obj):
    """Reshape a mesh from its rest positions using obj.psg_shape.

    Seamless segments: boundary vertices keep their rest XY (no jitter) and all get the same
    height (Edge Height), the terrain blends into that level over Flatten Edges. Neighbouring
    meshes with the same border spacing and Edge Height therefore share identical, flat edges.
    """
    settings = obj.psg_shape
    mesh = obj.data
    if settings.triangulate:
        rest_positions(mesh)  # store before topology changes keep vertex order
        triangulate(mesh)
    rest = rest_positions(mesh)
    border = boundary_vertices(mesh)
    xy = rest[:, :2].copy()

    if settings.jitter > 0.0:
        # break the grid: random XY offset; boundary vertices stay put so edges match neighbours
        cell = cell_size(rest)
        ids = np.arange(len(rest))
        offset = (np.stack([_hash01(ids, 11 + settings.seed), _hash01(ids, 977 + settings.seed)], axis=1)
                  * 2.0 - 1.0) * settings.jitter * cell * 0.5
        offset[border] = 0.0
        if settings.edge_flatten > 0.0:
            # also keep the first ring near the edge calm, so the flat band stays straight
            offset *= edge_mask(xy, border, min(settings.edge_flatten, cell * 1.5))[:, None]
        xy = xy + offset

    # noise is sampled in world XY, so neighbouring segments continue each other instead of repeating
    matrix = np.array(obj.matrix_world, dtype=np.float64)
    world_xy = (np.column_stack([xy, rest[:, 2]]) @ matrix[:3, :3].T + matrix[:3, 3])[:, :2]
    height = heights(settings, world_xy)
    if settings.flat_edges:
        mask = edge_mask(xy, border, max(settings.edge_flatten, 1e-4))
    else:
        mask = np.ones(len(xy))
    edge_level = min(settings.edge_level, settings.max_height)
    lift = edge_level + (height * settings.max_height - edge_level) * mask

    if settings.liquid_enabled:
        lift = apply_liquid(obj, settings, world_xy, lift, edge_level, border, xy)
    else:
        settings.crossings_json = ""
        for name in (LIQUID_ATTRIBUTE, FORD_ATTRIBUTE):
            if name in mesh.attributes:
                mesh.attributes.remove(mesh.attributes[name])

    co = np.empty((len(rest), 3))
    co[:, :2] = xy
    co[:, 2] = rest[:, 2] + lift
    mesh.vertices.foreach_set("co", co.astype(np.float32).ravel())
    mesh.update()
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    return float(co[:, 2].max() - co[:, 2].min())


# ----------------------------------------------------------------------------------------------
# Liquid: lava lakes / deep bog, carved as a Voronoi channel network around islands (a maze)
# ----------------------------------------------------------------------------------------------

LIQUID_ATTRIBUTE = "psg_liquid"  # per vertex: distance to the channel center / local width (<1 = liquid)
FORD_ATTRIBUTE = "psg_ford"      # per vertex: 1 on a shallow crossing (bog fords)


def _cell_hash(cx, cy, salt):
    key = (cx.astype(np.int64) * 73856093) ^ (cy.astype(np.int64) * 19349663) ^ np.int64(salt * 83492791)
    return _hash01(np.abs(key), salt)


def voronoi_2d(xy, size, seed):
    """Exact 2D Voronoi on a jittered grid (one site per cell), world space and seamless.

    Returns (gap, id_a, id_b, site_a, site_b): gap = F2 - F1 in meters (0 on a cell border,
    about 2x the distance to the border), ids/sites of the nearest and second nearest site.
    """
    cell = np.floor(xy / size).astype(np.int64)
    best = [np.full(len(xy), np.inf), np.full(len(xy), np.inf)]
    ids = [np.zeros(len(xy), dtype=np.int64), np.zeros(len(xy), dtype=np.int64)]
    sites = [np.zeros((len(xy), 2)), np.zeros((len(xy), 2))]
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            cx, cy = cell[:, 0] + dx, cell[:, 1] + dy
            jitter = np.stack([_cell_hash(cx, cy, seed + 1), _cell_hash(cx, cy, seed + 2)], axis=1)
            site = (np.stack([cx, cy], axis=1) + 0.1 + jitter * 0.8) * size
            dist = np.linalg.norm(xy - site, axis=1)
            site_id = cx * 1000003 + cy
            first = dist < best[0]
            second = ~first & (dist < best[1])
            # shift first -> second where a new nearest appears
            best[1] = np.where(first, best[0], np.where(second, dist, best[1]))
            ids[1] = np.where(first, ids[0], np.where(second, site_id, ids[1]))
            sites[1] = np.where(first[:, None], sites[0], np.where(second[:, None], site, sites[1]))
            best[0] = np.where(first, dist, best[0])
            ids[0] = np.where(first, site_id, ids[0])
            sites[0] = np.where(first[:, None], site, sites[0])
    return best[1] - best[0], ids[0], ids[1], sites[0], sites[1]


def liquid_fields(settings, world_xy):
    """(ratio, id_a, id_b, site_a, site_b, width) per point; ratio < 1 means inside the liquid."""
    size = max(settings.liquid_size, 1.0)
    # rotate and domain-warp, so the channels wind organically instead of following a grid
    angle = 0.47 + settings.liquid_seed * 0.61
    cos_a, sin_a = math.cos(angle), math.sin(angle)
    rotated = world_xy @ np.array([[cos_a, sin_a], [-sin_a, cos_a]])
    warp_inv = 1.0 / (size * 1.3)
    seed_z = settings.liquid_seed * 0.53
    warp_x = np.fromiter((_fbm(x * warp_inv, y * warp_inv, seed_z, 2) for x, y in rotated),
                         dtype=np.float64, count=len(rotated))
    warp_y = np.fromiter((_fbm(x * warp_inv + 31.7, y * warp_inv - 12.9, seed_z, 2) for x, y in rotated),
                         dtype=np.float64, count=len(rotated))
    warped = rotated + np.stack([warp_x, warp_y], axis=1) * size * settings.liquid_warp
    gap, id_a, id_b, site_a, site_b = voronoi_2d(warped, size, settings.liquid_seed)
    inv = 0.45 / size
    vary = np.fromiter((noise.noise(Vector((x * inv + 7.1, y * inv + 3.3, settings.liquid_seed * 0.31)))
                        for x, y in world_xy), dtype=np.float64, count=len(world_xy)) * 0.5 + 0.5
    width = settings.liquid_width * (1.0 - settings.liquid_variation + 2.0 * settings.liquid_variation * vary)
    return gap / np.maximum(width, 1e-3), id_a, id_b, site_a, site_b, width


LAKE_DEPTH = 0.12  # noise units from the shore to the hottest / deepest part of a lake


def lake_fields(settings, world_xy):
    """Lakes of mixed sizes: big lakes, small pools, islands and peninsulas.

    Returns (ratio, meters_per_ratio): ratio < 1 = liquid (0 = lake center), > 1 = land, growing
    with the distance from the shore. World space, so it is seamless across pieces with the same
    liquid settings.
    """
    size = max(settings.liquid_size, 1.0)
    seed_z = settings.liquid_seed * 0.53
    inv = 1.0 / size
    warp_inv = 1.0 / (size * 1.6)
    points = world_xy
    warp = np.array([[_fbm(x * warp_inv, y * warp_inv, seed_z + 3.0, 2),
                      _fbm(x * warp_inv + 31.7, y * warp_inv - 12.9, seed_z + 3.0, 2)] for x, y in points])
    warped = points + warp * size * settings.liquid_warp
    big = np.fromiter((_fbm(x * inv, y * inv, seed_z, 3) for x, y in warped), dtype=np.float64, count=len(points))
    pools = np.fromiter((_fbm(x * inv * 2.7, y * inv * 2.7, seed_z + 9.0, 2) for x, y in warped),
                        dtype=np.float64, count=len(points))
    # a very large, slow field changes the character from region to region:
    # here one huge lake, there a scatter of pools, elsewhere mostly dry land
    region = np.fromiter((noise.noise(Vector((x * inv * 0.3 + 5.5, y * inv * 0.3 - 2.5, seed_z)))
                          for x, y in points), dtype=np.float64, count=len(points))
    field = big + pools * (0.35 + 0.4 * settings.liquid_variation) + region * settings.liquid_variation * 0.35
    threshold = (0.5 - settings.liquid_coverage) * 0.55
    ratio = 1.0 - (field - threshold) / LAKE_DEPTH
    meters_per_ratio = LAKE_DEPTH * size / 1.4  # noise changes ~1.4 units per `size` meters
    return ratio, meters_per_ratio


def liquid_sample(settings, x, y):
    """(ratio, meters per ratio unit) at one world point."""
    point = np.array([[x, y]], dtype=np.float64)
    if settings.liquid_style == 'LAKES':
        ratio, meters = lake_fields(settings, point)
        return float(ratio[0]), meters
    ratio, _a, _b, _sa, _sb, width = liquid_fields(settings, point)
    return float(ratio[0]), float(width[0]) * 0.5


def liquid_blocked(settings, x, y, margin):
    """True in the liquid or closer than `margin` meters to its shore."""
    ratio, meters = liquid_sample(settings, x, y)
    return ratio < 1.15 + margin / max(meters, 1e-3)


def lake_crossings(settings, mesh, world_xy, ratios):
    """Crossings over narrow straits between separate pieces of land (maze over the lakes).

    Land vertices are split into connected regions; for every pair of regions the shortest
    shore-to-shore gap that runs over the liquid becomes a candidate, then a random spanning tree
    (plus Extra Routes) is kept. Returns [(x, y, dir_x, dir_y, length)].
    """
    edges = np.empty(len(mesh.edges) * 2, dtype=np.int32)
    mesh.edges.foreach_get("vertices", edges)
    return crossings_from_graph(settings, world_xy, ratios, edges.reshape(-1, 2))


def grid_crossings(settings, x0, x1, y0, y1, step=1.0, dry_sides=(), dry_width=2.5):
    """Crossings for a whole area (e.g. one biome segment split into several pieces).

    Evaluates the same world-space liquid field on a temporary grid, so every piece of the area
    can share one maze (set the result on all pieces and enable Lock Crossings).
    """
    xs = np.arange(x0, x1 + step * 0.5, step)
    ys = np.arange(y0, y1 + step * 0.5, step)
    gx, gy = np.meshgrid(xs, ys)
    world_xy = np.stack([gx.ravel(), gy.ravel()], axis=1)
    if settings.liquid_style == 'LAKES':
        ratios, _meters = lake_fields(settings, world_xy)
    else:
        ratios = liquid_fields(settings, world_xy)[0]
    dry = np.ones(len(world_xy))
    distances = {'X_MIN': world_xy[:, 0] - x0, 'X_MAX': x1 - world_xy[:, 0],
                 'Y_MIN': world_xy[:, 1] - y0, 'Y_MAX': y1 - world_xy[:, 1]}
    for side in dry_sides:
        dry = np.minimum(dry, _smoothstep(0.0, max(dry_width, 1.0), distances[side]))
    ratios = np.maximum(ratios, (1.0 - dry) * 3.0)
    nx, ny = len(xs), len(ys)
    index = np.arange(nx * ny).reshape(ny, nx)
    edges = np.concatenate([np.stack([index[:, :-1].ravel(), index[:, 1:].ravel()], axis=1),
                            np.stack([index[:-1, :].ravel(), index[1:, :].ravel()], axis=1)])
    return crossings_from_graph(settings, world_xy, ratios, edges)


def crossings_from_graph(settings, world_xy, ratios, edges):
    land = ratios >= 1.0
    parent = np.arange(len(world_xy))

    def find(i):
        root = i
        while parent[root] != root:
            root = parent[root]
        while parent[i] != root:
            parent[i], i = root, parent[i]
        return root

    for a, b in edges[land[edges[:, 0]] & land[edges[:, 1]]]:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    region = np.array([find(i) for i in range(len(world_xy))])
    shore_flags = np.zeros(len(world_xy), dtype=bool)
    mixed = edges[land[edges[:, 0]] != land[edges[:, 1]]]
    shore_flags[mixed[land[mixed[:, 0]], 0]] = True
    shore_flags[mixed[land[mixed[:, 1]], 1]] = True
    shore = np.flatnonzero(shore_flags)
    if len(shore) == 0:
        return []
    tree = KDTree(len(shore))
    for i, index in enumerate(shore):
        tree.insert((world_xy[index, 0], world_xy[index, 1], 0.0), i)
    tree.balance()
    max_span = settings.max_crossing
    best = {}
    for index in shore:
        x, y = world_xy[index]
        for (_co, j, dist) in tree.find_range((x, y, 0.0), max_span):
            other = shore[j]
            ra, rb = region[index], region[other]
            if ra == rb or dist < 1.0:
                continue
            key = (ra, rb) if ra < rb else (rb, ra)
            if key not in best or dist < best[key][0]:
                best[key] = (dist, index, other)
    rng = random.Random(settings.seed * 7919 + 17)
    candidates = []
    for key, (dist, a, b) in best.items():
        pa, pb = world_xy[a], world_xy[b]
        mid = (pa + pb) * 0.5
        samples = np.array([pa + (pb - pa) * t for t in (0.3, 0.5, 0.7)])
        if settings.liquid_style == 'LAKES':
            over, _m = lake_fields(settings, samples)
        else:
            over = liquid_fields(settings, samples)[0]
        if (over < 1.0).sum() < 2:
            continue  # the gap is mostly land (e.g. a thin land bridge the mesh resolution missed)
        direction = (pb - pa) / dist
        weight = dist / max_span + rng.random() * 0.8  # prefer short crossings, still random
        candidates.append((weight, key, (float(mid[0]), float(mid[1]), float(direction[0]), float(direction[1]),
                                         float(dist))))
    groups = {}

    def find_group(key):
        groups.setdefault(key, key)
        while groups[key] != key:
            groups[key] = groups[groups[key]]
            key = groups[key]
        return key

    crossings = []
    for _weight, (ra, rb), crossing in sorted(candidates):
        ga, gb = find_group(ra), find_group(rb)
        if ga != gb:
            groups[ga] = gb
            crossings.append(crossing)
        elif rng.random() < settings.liquid_loops:
            crossings.append(crossing)
    return crossings


def liquid_ratio_at(settings, x, y):
    return liquid_sample(settings, x, y)[0]


def dry_border_mask(settings, local_xy):
    """1 inside, fading to 0 towards the sides marked dry (neighbour without liquid)."""
    mask = np.ones(len(local_xy))
    width = max(settings.edge_flatten, 1.0)
    low, high = local_xy.min(axis=0), local_xy.max(axis=0)
    sides = ((settings.dry_x_min, local_xy[:, 0] - low[0]), (settings.dry_x_max, high[0] - local_xy[:, 0]),
             (settings.dry_y_min, local_xy[:, 1] - low[1]), (settings.dry_y_max, high[1] - local_xy[:, 1]))
    for enabled, distance in sides:
        if enabled:
            mask = np.minimum(mask, _smoothstep(0.0, width, distance))
    return mask


def build_maze(settings, world_xy, ratios, pairs, widths):
    """Crossings so every island is reachable (random spanning tree) plus a few extra loops.

    Returns [(x, y, dir_x, dir_y, width)], one per chosen crossing, in world space.
    """
    size = max(settings.liquid_size, 1.0)
    groups = {}
    for index, pair in enumerate(pairs):
        if pair is not None:
            groups.setdefault(pair[:2], []).append(index)
    rng = random.Random(settings.seed * 7919 + 17)
    edges = []
    for (a, b), members in groups.items():
        if len(members) < 3:
            continue  # cells that only touch in a corner
        members = np.array(members)
        center = world_xy[members].mean(axis=0)
        # a central, narrow spot on the channel's middle line
        score = (ratios[members] * 4.0 + np.linalg.norm(world_xy[members] - center, axis=1) / size
                 + widths[members] / size * 2.0)
        best = members[int(np.argmin(score))]
        # across the channel = along the gradient of the distance field (works with the warp)
        x, y = world_xy[best]
        step = max(widths[best] * 0.35, 0.3)
        probe = np.array([[x + step, y], [x - step, y], [x, y + step], [x, y - step]])
        probe_ratio, _a, _b, _sa, _sb, probe_width = liquid_fields(settings, probe)
        gap = probe_ratio * probe_width
        direction = np.array([gap[0] - gap[1], gap[2] - gap[3]])
        norm = np.linalg.norm(direction)
        if norm < 1e-6:
            continue
        direction /= norm
        edges.append((rng.random(), a, b, (float(world_xy[best, 0]), float(world_xy[best, 1]),
                                           float(direction[0]), float(direction[1]), float(widths[best]))))
    parent = {}

    def find(key):
        parent.setdefault(key, key)
        while parent[key] != key:
            parent[key] = parent[parent[key]]
            key = parent[key]
        return key

    crossings = []
    for _weight, a, b, crossing in sorted(edges):
        root_a, root_b = find(a), find(b)
        if root_a != root_b:
            parent[root_a] = root_b
            crossings.append(crossing)
        elif rng.random() < settings.liquid_loops:
            crossings.append(crossing)
    return crossings


def ford_mask(world_xy, crossings, ford_width):
    """1 inside a strip along each crossing (perpendicular to the channel), 0 elsewhere."""
    mask = np.zeros(len(world_xy))
    half = max(ford_width * 0.5, 0.05)
    for x, y, dx, dy, width in crossings:
        rel = world_xy - np.array([x, y])
        along = rel @ np.array([dx, dy])
        across = np.abs(rel @ np.array([-dy, dx]))
        length = width * 0.5 + 1.0
        inside = (1.0 - _smoothstep(half * 0.6, half, across)) * (1.0 - _smoothstep(length * 0.8, length, np.abs(along)))
        mask = np.maximum(mask, inside)
    return mask


def apply_liquid(obj, settings, world_xy, lift, edge_level, border, local_xy):
    """Carve the liquid network into the terrain heights, store masks and the crossing list.

    The liquid comes from a world-space field with a shared Liquid Seed, so it continues across
    neighbouring pieces of a biome and the shared border stays identical. Sides marked dry fade the
    liquid out (neighbour without liquid). Fords are per piece, so they never touch the border.
    """
    mesh = obj.data
    count = len(world_xy)
    dry = dry_border_mask(settings, local_xy)
    if settings.liquid_style == 'LAKES':
        ratios, _meters = lake_fields(settings, world_xy)
        ratios = np.maximum(ratios, (1.0 - dry) * 3.0)  # dry side: always land
        if settings.lock_crossings:
            crossings = globals()["crossings"](obj)  # shared maze of the whole area, set from outside
        else:
            crossings = lake_crossings(settings, mesh, world_xy, ratios)
    else:
        ratios, id_a, id_b, site_a, site_b, widths = liquid_fields(settings, world_xy)
        ratios = np.maximum(ratios, (1.0 - dry) * 3.0)  # dry side: always land
        pairs = [None] * count
        for i in np.flatnonzero(ratios < 1.0):
            if id_a[i] < id_b[i]:
                pairs[i] = (int(id_a[i]), int(id_b[i]), tuple(site_a[i]), tuple(site_b[i]))
            else:
                pairs[i] = (int(id_b[i]), int(id_a[i]), tuple(site_b[i]), tuple(site_a[i]))
        crossings = build_maze(settings, world_xy, ratios, pairs, widths)
    settings.crossings_json = json.dumps([[round(v, 4) for v in c] for c in crossings])

    level = settings.liquid_level
    floor = min(level + settings.bank_height, edge_level)
    land = np.maximum(lift, floor)  # land always stands above the liquid
    liquid = 1.0 - _smoothstep(0.85, 1.15, ratios)
    result = land + (level - land) * liquid
    fords = np.zeros(count)
    if crossings:
        # crossings are part of the terrain: a raised causeway over lava, a shallow ford in a bog
        fords = ford_mask(world_xy, crossings, settings.ford_width) * liquid
        if not settings.lock_crossings:
            fords[border] = 0.0  # per-piece crossings would not match the neighbour's border
        result = result + (level + settings.ford_height - result) * fords

    _store_point_float(mesh, LIQUID_ATTRIBUTE, ratios)
    _store_point_float(mesh, FORD_ATTRIBUTE, fords)
    return result


def _store_point_float(mesh, name, values):
    attr = mesh.attributes.get(name)
    if attr is None or attr.domain != 'POINT' or attr.data_type != 'FLOAT':
        if attr is not None:
            mesh.attributes.remove(attr)
        attr = mesh.attributes.new(name, 'FLOAT', 'POINT')
    attr.data.foreach_set("value", values.astype(np.float32))


def crossings(obj):
    try:
        return [tuple(c) for c in json.loads(obj.psg_shape.crossings_json or "[]")]
    except ValueError:
        return []


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
