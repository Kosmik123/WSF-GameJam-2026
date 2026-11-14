"""Builds Assets/Blender/PSG_Biomes.blend: 6 biomes x 3 terrain pieces in one seamless row.

Each biome is 3 neighbouring 40x40 m pieces with different landforms, its own palette and
scatter from the biome assets (linked from all_models.blend, not copied). All pieces share the
same flat Edge Level, so every piece joins its neighbours, also across biomes.

Run (from the repository root):
    blender -b --factory-startup --python Tools/Blender/psg_examples/build_biomes.py
"""

import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, HERE)
import psg_scatter  # noqa: E402
from bridges import build_bridges  # noqa: E402
from psg_scatter import core, shape  # noqa: E402

BRIDGES = "PSG_Kladki"

REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
LIBRARY = os.path.join(REPO, "Assets", "Blender", "all_models.blend")
OUTPUT = os.path.join(REPO, "Assets", "Blender", "PSG_Biomes.blend")

PIECE_SIZE = 40.0
PIECE_CUTS = 40  # same spacing on every piece -> shared edges match vertex for vertex
EDGE_LEVEL = 0.5

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
    bridges = bpy.data.collections.new(BRIDGES)
    sources.children.link(bridges)
    material = bpy.data.materials.get("LD57 Stylized") or core.ground_material()
    build_bridges(bridges, material)
    layer = bpy.context.view_layer.layer_collection.children[sources.name]
    layer.exclude = True  # the library assets are only sources, keep them out of the viewport
    return data_to.collections


def build_piece(biome, index, preset, overrides, seed, location):
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=PIECE_CUTS, y_subdivisions=PIECE_CUTS,
                                    size=PIECE_SIZE, location=location)
    piece = bpy.context.active_object
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

    bpy.ops.object.select_all(action='DESELECT')
    piece.select_set(True)
    bpy.context.view_layer.objects.active = piece
    bpy.ops.psg.make_area('EXEC_DEFAULT', preset=True, biome=biome, paint=True, shape_preset='NONE')
    customize = CUSTOM_LAYERS.get(biome)
    if customize is not None:
        customize(piece)
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


def volcano_layers(piece):
    """Lava puddles as their own layer with a wide size range, footbridges over some of them."""
    drobne = bpy.data.collections.get("Wulkan_Drobne")
    for layer in piece.psg.layers:
        if layer.source == drobne:
            layer.source_filter = ", ".join(t for t in (layer.source_filter, "!Kaluza") if t)
    puddles = add_layer(piece, "Kaluze lawy", drobne, 'SCATTER', source_filter="Kaluza",
                        density=0.012, scale_min=0.45, scale_max=2.3, min_distance=5.0, avoid_distance=2.5,
                        max_slope=math.radians(12.0), align_to_normal=1.0)
    add_layer(piece, "Kladki", bpy.data.collections.get(BRIDGES), 'AROUND', parent=str(puddles.uid),
              around_min=0.0, around_max=0.0, around_count_min=1, around_count_max=1, around_chance=0.7,
              inherit_parent_scale=True, scale_min=1.0, scale_max=1.1, align_to_normal=1.0)


CUSTOM_LAYERS = {"Wulkan": volcano_layers}


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
            x += PIECE_SIZE
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT, relative_remap=True)
    print("PSG saved", OUTPUT)


if __name__ == "__main__":
    main()
