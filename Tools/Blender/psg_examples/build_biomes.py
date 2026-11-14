"""Builds Assets/Blender/PSG_Biomes.blend: 6 biomes x 3 terrain pieces in one seamless row.

Each biome is one 40 x 40 m segment split into 3 pieces (13.3 x 40 m) with different landforms, its own palette and
scatter from the biome assets (linked from all_models.blend, not copied). All pieces share the
same flat Edge Level, so every piece joins its neighbours, also across biomes.

Run (from the repository root):
    blender -b --factory-startup --python Tools/Blender/psg_examples/build_biomes.py
"""

import json
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, HERE)
import psg_scatter  # noqa: E402
from psg_scatter import core, shape  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
LIBRARY = os.path.join(REPO, "Assets", "Blender", "all_models.blend")
OUTPUT = os.path.join(REPO, "Assets", "Blender", "PSG_Biomes.blend")

SEGMENT_SIZE = 40.0  # one biome = one 40 x 40 m segment ...
PIECE_WIDTH = SEGMENT_SIZE / 3.0  # ... split along X into 3 pieces of 13.3 x 40 m
PIECE_CUTS_X = 13
PIECE_CUTS_Y = 40  # same Y spacing on every piece -> shared edges match vertex for vertex
EDGE_LEVEL = 0.5
EDGE_BLEND = 2.5  # narrow pieces: short blend into the flat seam keeps most of the landform

# biome -> 3 pieces: (shape preset, overrides, seed). Order = order along X (and around the torus).
BIOMES = {
    "Pustynia": [
        ('PLATEAUS', dict(max_height=2.5, plateau_levels=3, cliff=0.1, hill_amount=0.05), 21),  # mesas
        ('PLATEAUS', dict(max_height=2.4, plateau_levels=2, cliff=0.14, valley_amount=0.7,
                          valley_width=0.18), 22),  # canyon
        ('GENTLE', dict(max_height=1.4, hill_amount=0.6, hill_size=11.0, base_size=22.0), 23),  # dunes
    ],
    "Las": [
        ('MIXED', {}, 11),
        ('PLATEAUS', {}, 12),
        ('VALLEYS', {}, 13),
    ],
    "Bagno": [
        ('GENTLE', dict(max_height=0.9, hill_amount=0.4, hill_size=5.0), 41),
        ('VALLEYS', dict(max_height=1.1, valley_amount=0.9, valley_width=0.3, plateau_amount=0.0), 42),
        ('MIXED', dict(max_height=1.2, plateau_levels=2, cliff=0.3, hill_amount=0.4), 43),
    ],
    "Wybrzeze": [
        ('GENTLE', dict(max_height=1.0, hill_amount=0.3, hill_size=12.0, base_size=35.0), 51),  # beach
        ('PLATEAUS', dict(max_height=2.2, plateau_levels=2, cliff=0.12, valley_amount=0.2), 52),  # cliffs
        ('VALLEYS', dict(max_height=1.4, valley_amount=0.85, valley_width=0.3, plateau_amount=0.2), 53),  # lagoon
    ],
    "Wulkan": [
        ('PLATEAUS', dict(max_height=2.5, plateau_levels=4, cliff=0.15, hill_amount=0.2), 61),  # lava terraces
        ('VALLEYS', dict(max_height=2.4, valley_amount=0.9, valley_width=0.12, cliff=0.2), 62),  # rift
        ('MIXED', dict(max_height=2.5, hill_amount=0.6, hill_size=4.0, plateau_levels=3), 63),  # rugged
    ],
    "Lod": [
        ('VALLEYS', dict(max_height=2.4, valley_amount=0.9, valley_width=0.25, cliff=0.35), 31),  # glacier valley
        ('MIXED', dict(max_height=2.2, hill_amount=0.5, plateau_levels=4), 32),
        ('GENTLE', dict(max_height=1.2, hill_amount=0.35, hill_size=9.0), 33),  # snowfield
    ],
}


def link_biome_collections():
    with bpy.data.libraries.load(LIBRARY, link=True, relative=True) as (data_from, data_to):
        data_to.collections = [name for name in data_from.collections
                               if name.split("_")[0] in BIOMES
                               and name.endswith(("_Duze", "_Srednie", "_Drobne", "_Landmarki"))]
    sources = bpy.data.collections.new("PSG_Sources (linked)")
    bpy.context.scene.collection.children.link(sources)
    for collection in data_to.collections:
        sources.children.link(collection)
    layer = bpy.context.view_layer.layer_collection.children[sources.name]
    layer.exclude = True  # the library assets are only sources, keep them out of the viewport
    return data_to.collections


def build_piece(biome, index, preset, overrides, seed, location):
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=PIECE_CUTS_X, y_subdivisions=PIECE_CUTS_Y,
                                    size=SEGMENT_SIZE, location=location)
    piece = bpy.context.active_object
    for vertex in piece.data.vertices:
        vertex.co.x *= PIECE_WIDTH / SEGMENT_SIZE  # 40 x 40 grid -> 13.3 x 40 strip, transform stays clean
    piece.name = "Teren_%s_%d" % (biome, index + 1)
    piece.data.name = piece.name
    biome_collection = core.ensure_child_collection(bpy.context.scene.collection, "Teren_" + biome)
    for collection in list(piece.users_collection):
        collection.objects.unlink(piece)
    biome_collection.objects.link(piece)

    settings = piece.psg_shape
    shape.apply_preset(settings, preset)
    for key, value in overrides.items():
        setattr(settings, key, value)
    settings.seed = seed
    settings.flat_edges = True
    settings.edge_level = EDGE_LEVEL
    settings.edge_flatten = EDGE_BLEND

    bpy.ops.object.select_all(action='DESELECT')
    piece.select_set(True)
    bpy.context.view_layer.objects.active = piece
    bpy.ops.psg.make_area('EXEC_DEFAULT', preset=True, biome=biome, paint=True, shape_preset='NONE')
    customize = CUSTOM_LAYERS.get(biome)
    if customize is not None:
        customize(piece, index)
    core.run_shape(piece)  # shape with the overrides above; re-snaps scatter and repaints
    for layer in piece.psg.layers:
        layer.seed = seed * 10 + layer.uid
    core.regenerate(piece)
    return piece


def add_layer(piece, name, collection, mode, **values):
    layer = core.add_layer(piece, collection)
    layer.name = name
    layer.mode = mode
    for key, value in values.items():
        setattr(layer, key, value)
    return layer


def clear_layers(piece):
    core.remove_objects(core.path_instances(piece))
    piece.psg.layers.clear()
    piece.psg.state_json = ""
    piece.psg.excluded_json = ""


def collection(name):
    return bpy.data.collections.get(name)


def deg(value):
    return math.radians(value)


def lakes(piece, liquid_type, seed, size, coverage, variation, max_crossing, crossing_width, crossing_height):
    """Liquid lakes on the same settings for all 3 pieces of the biome, so they continue across the
    seams; crossings are raised strips of the terrain itself (lava causeway / bog ford)."""
    s = piece.psg_shape
    s.liquid_enabled = True
    s.liquid_type = liquid_type
    s.liquid_style = 'LAKES'
    s.liquid_seed = seed
    s.liquid_size = size
    s.liquid_coverage = coverage
    s.liquid_variation = variation
    s.liquid_warp = 0.5
    s.max_crossing = max_crossing
    s.liquid_level = 0.2
    s.bank_height = 0.3
    s.liquid_loops = 0.15
    s.ford_width = crossing_width
    s.ford_height = crossing_height
    core.set_liquid_palette(piece, liquid_type)


_SHARED_MAZE = {}


def biome_sides(piece, index):
    """The outer sides of the biome are dry, so neighbouring biomes (no liquid) meet it on land.

    The maze (crossings) is computed once for the whole biome segment and shared by its 3 pieces,
    so a causeway or ford continues across the seams between them."""
    s = piece.psg_shape
    s.dry_x_min = index == 0
    s.dry_x_max = index == 2
    if index == 0:
        x0 = piece.location.x - PIECE_WIDTH * 0.5
        y0 = piece.location.y - SEGMENT_SIZE * 0.5
        maze = shape.grid_crossings(s, x0, x0 + SEGMENT_SIZE, y0, y0 + SEGMENT_SIZE, step=1.0,
                                    dry_sides=('X_MIN', 'X_MAX'), dry_width=EDGE_BLEND)
        _SHARED_MAZE[s.liquid_type] = json.dumps([[round(v, 4) for v in c] for c in maze])
    s.crossings_json = _SHARED_MAZE[s.liquid_type]
    s.lock_crossings = True


def volcano(piece, index):
    """Big glowing lava lakes; land pieces joined only by raised basalt causeways (a maze)."""
    clear_layers(piece)
    lakes(piece, 'LAVA', seed=61, size=20.0, coverage=0.63, variation=0.6, max_crossing=9.0,
          crossing_width=2.2, crossing_height=0.3)
    biome_sides(piece, index)
    landmarks, big, medium, small = (collection("Wulkan_" + n) for n in ("Landmarki", "Duze", "Srednie", "Drobne"))
    add_layer(piece, "Iglice", landmarks, 'SCATTER', source_filter="Iglica", density=0.003, min_distance=14.0,
              scale_min=0.7, scale_max=1.15, max_slope=deg(25), liquid_margin=2.0)
    columns = add_layer(piece, "Kolumny bazaltowe", big, 'CLUSTER', source_filter="Kolumny",
                        cluster_density=1.2, cluster_radius=3.5, cluster_min=2, cluster_max=5,
                        single_chance=0.3, min_distance=1.6, scale_min=0.6, scale_max=1.5, max_slope=deg(30))
    add_layer(piece, "Skaly z lawa (brzeg)", big, 'SCATTER', source_filter="Skala", density=0.02,
              liquid_zone='SHORE', shore_band=3.0, min_distance=3.0, scale_min=0.5, scale_max=1.3)
    burned = add_layer(piece, "Spalone drzewa", medium, 'CLUSTER', source_filter="Spalone",
                       cluster_density=1.4, cluster_radius=4.0, cluster_min=2, cluster_max=6,
                       single_chance=0.35, min_distance=1.5, scale_min=0.7, scale_max=1.3,
                       max_slope=deg(30), avoid_distance=1.5)
    add_layer(piece, "Obsydian", medium, 'AROUND', source_filter="Obsydian", parent=str(columns.uid),
              around_min=1.5, around_max=4.0, around_count_min=0, around_count_max=2, around_chance=0.7,
              scale_min=0.5, scale_max=1.2)
    add_layer(piece, "Kominy", medium, 'SCATTER', source_filter="Komin", density=0.006, min_distance=10.0,
              scale_min=0.8, scale_max=1.4, avoid_distance=3.0, max_slope=deg(20))
    add_layer(piece, "Zuzel wokol drzew", small, 'AROUND', source_filter="Zuzel, Kamyki", parent=str(burned.uid),
              around_min=0.6, around_max=2.5, around_count_min=2, around_count_max=5, scale_min=0.6,
              scale_max=1.4, min_distance=0.4)
    add_layer(piece, "Zuzel", small, 'SCATTER', source_filter="Zuzel, Kamyki", density=0.12, min_distance=0.6,
              scale_min=0.5, scale_max=1.6, avoid_distance=0.8)
    add_layer(piece, "Zuzel (brzeg)", small, 'SCATTER', source_filter="Zuzel", density=0.5,
              liquid_zone='SHORE', shore_band=2.0, min_distance=0.5, scale_min=0.6, scale_max=1.5)
    add_layer(piece, "Kaluze lawy", small, 'SCATTER', source_filter="Kaluza", density=0.004, min_distance=6.0,
              scale_min=0.5, scale_max=1.4, max_slope=deg(10), avoid_distance=2.0, align_to_normal=1.0)


def swamp(piece, index):
    """Dead marshes: deep bog you sink in, land pieces joined only by narrow shallow fords."""
    clear_layers(piece)
    lakes(piece, 'BOG', seed=41, size=15.0, coverage=0.6, variation=0.65, max_crossing=7.0,
          crossing_width=1.4, crossing_height=0.05)
    biome_sides(piece, index)
    landmarks, big, medium, small = (collection("Bagno_" + n) for n in ("Landmarki", "Duze", "Srednie", "Drobne"))
    add_layer(piece, "Stare cyprysy", landmarks, 'SCATTER', source_filter="Cyprys", density=0.004,
              min_distance=12.0, scale_min=0.8, scale_max=1.2, liquid_zone='SHORE', shore_band=4.0)
    add_layer(piece, "Powalone olbrzymy", landmarks, 'SCATTER', source_filter="Powalony", density=0.0015,
              min_distance=15.0, scale_min=0.8, scale_max=1.1, max_slope=deg(15))
    trees = add_layer(piece, "Drzewa", big, 'CLUSTER', source_filter="Drzewo_na", cluster_density=1.3,
                      cluster_radius=4.0, cluster_min=2, cluster_max=4, single_chance=0.4, min_distance=2.2,
                      scale_min=0.75, scale_max=1.3, max_slope=deg(25))
    add_layer(piece, "Martwe drzewa (brzeg)", big, 'SCATTER', source_filter="Martwe", density=0.025,
              liquid_zone='SHORE', shore_band=2.5, min_distance=3.0, scale_min=0.6, scale_max=1.3)
    add_layer(piece, "Klody i pniaki", medium, 'AROUND', source_filter="Kloda, Pniak", parent=str(trees.uid),
              around_min=1.5, around_max=4.0, around_count_min=0, around_count_max=2, around_chance=0.8,
              scale_min=0.7, scale_max=1.2)
    add_layer(piece, "Swiecace grzyby", medium, 'AROUND', source_filter="Grzyby", parent=str(trees.uid),
              around_min=0.8, around_max=2.5, around_count_min=0, around_count_max=2, around_chance=0.5,
              scale_min=0.6, scale_max=1.3)
    add_layer(piece, "Trzciny (brzeg)", small, 'SCATTER', source_filter="Trzciny, Turzyca", density=0.9,
              liquid_zone='SHORE', shore_band=2.2, min_distance=0.45, scale_min=0.7, scale_max=1.5)
    add_layer(piece, "Lilie wodne", small, 'SCATTER', source_filter="Lilie", density=0.06,
              liquid_zone='LIQUID', min_distance=1.2, scale_min=0.6, scale_max=1.6)
    add_layer(piece, "Turzyca i paprocie", small, 'SCATTER', source_filter="Turzyca, Paproc", density=0.12,
              min_distance=0.6, scale_min=0.6, scale_max=1.4, avoid_distance=0.8)
    add_layer(piece, "Trawa wokol drzew", small, 'AROUND', source_filter="Turzyca, Paproc, Trzciny",
              parent=str(trees.uid), around_min=0.8, around_max=3.0, around_count_min=3, around_count_max=8,
              min_distance=0.35, scale_min=0.6, scale_max=1.3)


CUSTOM_LAYERS = {"Wulkan": volcano, "Bagno": swamp}


def main():
    bpy.ops.wm.read_homefile(use_empty=True)
    psg_scatter.register()
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)  # saved first, so the library link is relative
    link_biome_collections()
    x = 0.0
    for biome, pieces in BIOMES.items():
        for index, (preset, overrides, seed) in enumerate(pieces):
            piece = build_piece(biome, index, preset, overrides, seed, (x, 0.0, 0.0))
            print("PSG piece %-16s %-8s instances %d" % (piece.name, preset, piece.psg.instance_count))
            x += PIECE_WIDTH
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT, relative_remap=True)
    print("PSG saved", OUTPUT)


if __name__ == "__main__":
    main()
