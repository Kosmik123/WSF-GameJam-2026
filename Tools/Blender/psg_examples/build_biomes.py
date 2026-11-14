"""Builds one full biome per terrain segment and an assembly of all of them.

Every biome is one mesh of the size of the game's terrain segment `SM_Terrain_S1` (314 x 354 m),
shaped into low-poly landforms, painted with the biome palette and scattered with the biome assets
linked from `LD57_Biomes.blend` (not copied). Its border is flat at Edge Level, so the biomes join
without gaps.

Output (Assets/Blender):
    PSG_Biome_<Name>.blend   one biome, root collection `Biom_<Name>` (open these to edit)
    PSG_Biomes.blend         all biomes linked as collection instances, side by side along X

Run (from the repository root); without names it builds all six:
    blender -b --factory-startup --python Tools/Blender/psg_examples/build_biomes.py -- Bagno Wulkan
"""

import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import psg_scatter  # noqa: E402
from psg_scatter import core, shape  # noqa: E402

REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
LIBRARY = os.path.join(REPO, "Assets", "Blender", "LD57_Biomes.blend")  # biome assets, separate from all_models.blend
OUTPUT = os.path.join(REPO, "Assets", "Blender", "PSG_Biomes.blend")

SEGMENT_X = 314.0  # SM_Terrain_S1: 314 x 354 m
SEGMENT_Y = 354.0
CELL = 2.0  # mesh spacing in meters (157 x 177 quads, triangulated)
EDGE_LEVEL = 0.5
EDGE_BLEND = 8.0

# biome -> (shape preset, shape overrides, seed); landform sizes scaled up for a 314 x 354 m segment
BIOMES = {
    "Pustynia": ('PLATEAUS', dict(max_height=2.5, base_size=90.0, plateau_levels=3, cliff=0.12,
                                  valley_size=120.0, valley_amount=0.5, hill_size=16.0, hill_amount=0.15), 21),
    "Las": ('MIXED', dict(max_height=2.2, base_size=90.0, valley_size=110.0, hill_size=14.0), 11),
    "Bagno": ('MIXED', dict(max_height=1.6, base_size=90.0, valley_size=110.0, hill_size=14.0, hill_amount=0.4,
                            plateau_levels=2, cliff=0.3), 41),
    "Wybrzeze": ('GENTLE', dict(max_height=1.4, base_size=100.0, hill_size=20.0, valley_size=120.0), 51),
    "Wulkan": ('PLATEAUS', dict(max_height=2.5, base_size=80.0, plateau_levels=4, cliff=0.15, valley_size=100.0,
                                hill_size=12.0, hill_amount=0.25), 61),
    "Lod": ('VALLEYS', dict(max_height=2.4, base_size=90.0, valley_size=100.0, valley_amount=0.9,
                            valley_width=0.25, cliff=0.35, hill_size=14.0), 31),
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


def build_segment(biome):
    preset, overrides, seed = BIOMES[biome]
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=int(SEGMENT_X / CELL), y_subdivisions=int(SEGMENT_Y / CELL),
                                    size=1.0, location=(0.0, 0.0, 0.0))
    piece = bpy.context.active_object
    for vertex in piece.data.vertices:  # unit grid -> 314 x 354 m, the object transform stays clean
        vertex.co.x *= SEGMENT_X
        vertex.co.y *= SEGMENT_Y
    piece.name = "Teren_" + biome
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
    """Liquid lakes; crossings are raised strips of the terrain itself (lava causeway / bog ford)."""
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
    s.dry_x_min = True  # every side dry: the whole border stays flat land at Edge Level,
    s.dry_x_max = True  # so the segment joins any neighbour (other biome or itself around the torus)
    s.dry_y_min = True
    s.dry_y_max = True
    core.set_liquid_palette(piece, liquid_type)


def volcano(piece):
    """Big glowing lava lakes; land joined only by raised basalt causeways (a maze)."""
    clear_layers(piece)
    lakes(piece, 'LAVA', seed=61, size=55.0, coverage=0.6, variation=0.6, max_crossing=14.0,
          crossing_width=3.0, crossing_height=0.3)
    landmarks, big, medium, small = (collection("Wulkan_" + n) for n in ("Landmarki", "Duze", "Srednie", "Drobne"))
    add_layer(piece, "Iglice", landmarks, 'SCATTER', source_filter="Iglica", density=0.0004, min_distance=30.0,
              scale_min=0.8, scale_max=1.3, max_slope=deg(25), liquid_margin=2.0)
    columns = add_layer(piece, "Kolumny bazaltowe", big, 'CLUSTER', source_filter="Kolumny",
                        cluster_density=0.3, cluster_radius=5.0, cluster_min=2, cluster_max=6,
                        single_chance=0.3, min_distance=1.8, scale_min=0.6, scale_max=1.6, max_slope=deg(30))
    add_layer(piece, "Skaly z lawa (brzeg)", big, 'SCATTER', source_filter="Skala", density=0.006,
              liquid_zone='SHORE', shore_band=4.0, min_distance=5.0, scale_min=0.5, scale_max=1.4)
    burned = add_layer(piece, "Spalone drzewa", medium, 'CLUSTER', source_filter="Spalone",
                       cluster_density=0.35, cluster_radius=6.0, cluster_min=3, cluster_max=8,
                       single_chance=0.3, min_distance=1.6, scale_min=0.7, scale_max=1.3,
                       max_slope=deg(30), avoid_distance=1.5)
    add_layer(piece, "Obsydian", medium, 'AROUND', source_filter="Obsydian", parent=str(columns.uid),
              around_min=1.5, around_max=4.0, around_count_min=0, around_count_max=2, around_chance=0.6,
              scale_min=0.5, scale_max=1.2)
    add_layer(piece, "Kominy", medium, 'SCATTER', source_filter="Komin", density=0.0012, min_distance=20.0,
              scale_min=0.8, scale_max=1.5, avoid_distance=3.0, max_slope=deg(20))
    add_layer(piece, "Zuzel wokol drzew", small, 'AROUND', source_filter="Zuzel, Kamyki", parent=str(burned.uid),
              around_min=0.6, around_max=2.5, around_count_min=1, around_count_max=4, scale_min=0.6,
              scale_max=1.4, min_distance=0.5)
    add_layer(piece, "Zuzel", small, 'SCATTER', source_filter="Zuzel, Kamyki", density=0.03, min_distance=1.0,
              scale_min=0.5, scale_max=1.7, avoid_distance=0.8)
    add_layer(piece, "Zuzel (brzeg)", small, 'SCATTER', source_filter="Zuzel", density=0.15,
              liquid_zone='SHORE', shore_band=2.5, min_distance=0.8, scale_min=0.6, scale_max=1.6)
    add_layer(piece, "Kaluze lawy", small, 'SCATTER', source_filter="Kaluza", density=0.001, min_distance=12.0,
              scale_min=0.6, scale_max=1.6, max_slope=deg(10), avoid_distance=2.0, align_to_normal=1.0)


def swamp(piece):
    """Dead marshes: deep bog you sink in, land joined only by narrow shallow fords."""
    clear_layers(piece)
    lakes(piece, 'BOG', seed=41, size=55.0, coverage=0.55, variation=0.65, max_crossing=14.0,
          crossing_width=2.0, crossing_height=0.05)
    landmarks, big, medium, small = (collection("Bagno_" + n) for n in ("Landmarki", "Duze", "Srednie", "Drobne"))
    add_layer(piece, "Stare cyprysy", landmarks, 'SCATTER', source_filter="Cyprys", density=0.0008,
              min_distance=25.0, scale_min=0.8, scale_max=1.25, liquid_zone='SHORE', shore_band=5.0)
    add_layer(piece, "Powalone olbrzymy", landmarks, 'SCATTER', source_filter="Powalony", density=0.0003,
              min_distance=30.0, scale_min=0.8, scale_max=1.15, max_slope=deg(15))
    trees = add_layer(piece, "Drzewa", big, 'CLUSTER', source_filter="Drzewo_na", cluster_density=0.3,
                      cluster_radius=6.0, cluster_min=2, cluster_max=5, single_chance=0.35, min_distance=2.4,
                      scale_min=0.75, scale_max=1.35, max_slope=deg(25))
    add_layer(piece, "Martwe drzewa (brzeg)", big, 'SCATTER', source_filter="Martwe", density=0.01,
              liquid_zone='SHORE', shore_band=3.0, min_distance=6.0, scale_min=0.6, scale_max=1.3)
    add_layer(piece, "Klody i pniaki", medium, 'AROUND', source_filter="Kloda, Pniak", parent=str(trees.uid),
              around_min=1.5, around_max=4.5, around_count_min=0, around_count_max=2, around_chance=0.7,
              scale_min=0.7, scale_max=1.2)
    add_layer(piece, "Swiecace grzyby", medium, 'AROUND', source_filter="Grzyby", parent=str(trees.uid),
              around_min=0.8, around_max=2.5, around_count_min=0, around_count_max=2, around_chance=0.45,
              scale_min=0.6, scale_max=1.3)
    add_layer(piece, "Trzciny (brzeg)", small, 'SCATTER', source_filter="Trzciny, Turzyca", density=0.2,
              liquid_zone='SHORE', shore_band=2.5, min_distance=0.7, scale_min=0.7, scale_max=1.6)
    add_layer(piece, "Lilie wodne", small, 'SCATTER', source_filter="Lilie", density=0.012,
              liquid_zone='LIQUID', min_distance=2.0, scale_min=0.6, scale_max=1.8)
    add_layer(piece, "Turzyca i paprocie", small, 'SCATTER', source_filter="Turzyca, Paproc", density=0.03,
              min_distance=1.0, scale_min=0.6, scale_max=1.5, avoid_distance=0.8)
    add_layer(piece, "Trawa wokol drzew", small, 'AROUND', source_filter="Turzyca, Paproc, Trzciny",
              parent=str(trees.uid), around_min=0.8, around_max=3.0, around_count_min=2, around_count_max=6,
              min_distance=0.4, scale_min=0.6, scale_max=1.3)


# Layer stacks for the biomes without a liquid, sized for a 314 x 354 m segment (~4-5k instances).
# key -> (landmarks filter, tree filter, extra big filter, medium filter, small filter, rocks filter)
STANDARD = {
    "Las": dict(landmarks="", trees="Drzewo, !Martwe", big_extra="Martwe", medium="Krzak, Glaz, Pniak, Kloda",
                small="Trawa, Kwiaty, Paproc, Grzyby", rocks="Kamyki", tree_density=0.25, max_height_trees=28),
    "Pustynia": dict(landmarks="", trees="Kaktus, Palma", big_extra="Slup", medium="Opuncja, Skala, Szkielet",
                     small="Kaktus_kulisty, Sucha, Toczace", rocks="Kamyki", tree_density=0.14,
                     max_height_trees=30),
    "Wybrzeze": dict(landmarks="", trees="Palma", big_extra="Skala", medium="Drewno, Koral",
                     small="Trawa, Muszle, Rozgwiazda, Wodorosty", rocks="Otoczaki", tree_density=0.14,
                     max_height_trees=25),
    "Lod": dict(landmarks="", trees="Sosna", big_extra="Bryla", medium="Skala, Krysztaly, Zaspa",
                small="Kolce, Krzak", rocks="Kamyki", tree_density=0.22, max_height_trees=28),
}


def standard(piece, biome):
    """Clusters of trees with bushes and grass around them, scattered extras, rare landmarks."""
    spec = STANDARD[biome]
    clear_layers(piece)
    landmarks, big, medium, small = (collection(biome + "_" + n) for n in ("Landmarki", "Duze", "Srednie", "Drobne"))
    add_layer(piece, "Landmarki", landmarks, 'SCATTER', source_filter=spec["landmarks"], density=0.00025,
              min_distance=45.0, scale_min=0.85, scale_max=1.2, max_slope=deg(15))
    trees = add_layer(piece, "Drzewa", big, 'CLUSTER', source_filter=spec["trees"],
                      cluster_density=spec["tree_density"], cluster_radius=6.0, cluster_min=2, cluster_max=5,
                      single_chance=0.35, min_distance=2.6, scale_min=0.75, scale_max=1.35,
                      max_slope=deg(spec["max_height_trees"]))
    add_layer(piece, "Duze pojedyncze", big, 'SCATTER', source_filter=spec["big_extra"], density=0.0012,
              min_distance=12.0, scale_min=0.7, scale_max=1.3, avoid_distance=3.0, max_slope=deg(30))
    add_layer(piece, "Krzewy wokol", medium, 'AROUND', source_filter=spec["medium"], parent=str(trees.uid),
              around_min=1.5, around_max=4.5, around_count_min=0, around_count_max=2, around_chance=0.7,
              min_distance=1.2, scale_min=0.6, scale_max=1.2, max_slope=deg(35))
    add_layer(piece, "Srednie", medium, 'SCATTER', source_filter=spec["medium"], density=0.003,
              min_distance=4.0, scale_min=0.6, scale_max=1.3, avoid_distance=2.0, max_slope=deg(35))
    add_layer(piece, "Drobne wokol", small, 'AROUND', source_filter=spec["small"], parent=str(trees.uid),
              around_min=0.8, around_max=3.0, around_count_min=1, around_count_max=4, min_distance=0.5,
              scale_min=0.6, scale_max=1.3, max_slope=deg(35))
    add_layer(piece, "Drobne", small, 'SCATTER', source_filter=spec["small"], density=0.014, min_distance=1.0,
              scale_min=0.6, scale_max=1.4, avoid_distance=1.0, max_slope=deg(35))
    add_layer(piece, "Kamyki", small, 'SCATTER', source_filter=spec["rocks"], density=0.003, min_distance=3.0,
              scale_min=0.6, scale_max=1.6, avoid_distance=1.0)


CUSTOM_LAYERS = {"Wulkan": volcano, "Bagno": swamp}
CUSTOM_LAYERS.update({name: (lambda piece, name=name: standard(piece, name)) for name in STANDARD})

ORDER = ("Pustynia", "Las", "Bagno", "Wybrzeze", "Wulkan", "Lod")  # along X, i.e. around the torus


def biome_file(biome):
    return os.path.join(REPO, "Assets", "Blender", "PSG_Biome_%s.blend" % biome)


def build_biome_file(biome):
    """One biome per file (light enough for a laptop); root collection `Biom_<name>` for linking."""
    path = biome_file(biome)
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.wm.save_as_mainfile(filepath=path)  # saved first, so the library link is relative
    link_biome_collections()
    piece = build_segment(biome)
    root = bpy.data.collections.new("Biom_" + biome)
    bpy.context.scene.collection.children.link(root)
    for child in (piece.users_collection[0], piece.psg.output):
        for parent in [c for c in bpy.data.collections if child.name in c.children] + [bpy.context.scene.collection]:
            if child.name in parent.children:
                parent.children.unlink(child)
        root.children.link(child)
    bpy.ops.wm.save_as_mainfile(filepath=path, relative_remap=True)
    print("PSG biome %-9s %.0f x %.0f m, instances %d -> %s" % (
        biome, piece.dimensions.x, piece.dimensions.y, piece.psg.instance_count, os.path.basename(path)))


def build_assembly(biomes):
    """PSG_Biomes.blend: every biome file linked as one collection instance, side by side along X."""
    bpy.ops.wm.read_homefile(use_empty=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT)
    for index, biome in enumerate(biomes):
        with bpy.data.libraries.load(biome_file(biome), link=True, relative=True) as (data_from, data_to):
            data_to.collections = ["Biom_" + biome]
        empty = bpy.data.objects.new("Biom_" + biome, None)
        empty.instance_type = 'COLLECTION'
        empty.instance_collection = data_to.collections[0]
        empty.location = (index * SEGMENT_X, 0.0, 0.0)
        bpy.context.scene.collection.objects.link(empty)
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT, relative_remap=True)
    print("PSG assembly saved", OUTPUT)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    biomes = [b for b in ORDER if not args or "all" in args or b in args]
    psg_scatter.register()
    for biome in biomes:
        build_biome_file(biome)
    build_assembly([b for b in ORDER if os.path.exists(biome_file(b))])


if __name__ == "__main__":
    main()
