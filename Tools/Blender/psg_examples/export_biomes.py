"""Exports every PSG biome to Assets/Bioms/<Name>.fbx: the terrain plus all scattered assets.

Assets are linked duplicates of the library meshes, so the FBX stores one mesh per asset variant
and every placement references it (Unity imports them as shared meshes: instancing / SRP batching).
Settings follow docs/WSF_Biom.md: FBX scale All, -Z forward, Y up, transforms applied,
sRGB vertex colors in `Col`.

Run (from the repository root); without names it exports all biome files found:
    blender -b --factory-startup --python Tools/Blender/psg_examples/export_biomes.py -- Bagno
"""

import glob
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
SOURCE = os.path.join(REPO, "Assets", "Blender")
TARGET = os.path.join(REPO, "Assets", "Bioms")


def export_biome(path):
    biome = os.path.splitext(os.path.basename(path))[0][len("PSG_Biome_"):]
    bpy.ops.wm.open_mainfile(filepath=path)
    root = bpy.data.collections.get("Biom_" + biome)
    objects = [o for o in root.all_objects if o.type == 'MESH'] if root else []
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    output = os.path.join(TARGET, biome + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=output, use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='OFF',
        colors_type='SRGB', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    meshes = {o.data for o in objects}
    print("PSG export %-9s objects %5d, unique meshes %3d -> %s (%.1f MB)" % (
        biome, len(objects), len(meshes), os.path.relpath(output, REPO), os.path.getsize(output) / 1e6))


def main():
    names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    os.makedirs(TARGET, exist_ok=True)
    for path in sorted(glob.glob(os.path.join(SOURCE, "PSG_Biome_*.blend"))):
        biome = os.path.splitext(os.path.basename(path))[0][len("PSG_Biome_"):]
        if not names or biome in names:
            export_biome(path)


if __name__ == "__main__":
    main()
