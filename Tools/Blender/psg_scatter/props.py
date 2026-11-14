import math

import bpy
from bpy.props import (BoolProperty, CollectionProperty, EnumProperty, FloatProperty,
                       FloatVectorProperty, IntProperty, PointerProperty, StringProperty)

from . import core


def _scatter_changed(self, _context):
    core.schedule(self.id_data, scatter=True)


def _paint_changed(self, _context):
    core.schedule(self.id_data, scatter=False)


def _ground_changed(self, _context):
    core.schedule_ground(self.id_data)


def _poll_ground(_self, obj):
    return obj.type == 'MESH' and obj.psg_inst.path is None


_parent_cache = []


def _parent_items(self, _context):
    """Earlier layers of the same generator; the enum value is the layer uid (stable on rename/reorder)."""
    items = [("0", "-", "No parent", 0)]
    for layer in self.id_data.psg.layers:
        if layer.uid != self.uid:
            items.append((str(layer.uid), layer.name, "", layer.uid))
    _parent_cache[:] = items
    return _parent_cache


PATH_MODES = ('LINE', 'EDGES', 'BAND', 'AROUND')
AREA_MODES = ('SCATTER', 'CLUSTER', 'AROUND')


class PSG_Layer(bpy.types.PropertyGroup):
    name: StringProperty(name="Name", default="Layer")
    uid: IntProperty()
    enabled: BoolProperty(name="Enabled", default=True, update=_scatter_changed)
    source: PointerProperty(
        name="Source", type=bpy.types.Collection, update=_scatter_changed,
        description="Collection with source meshes (variants). Custom prop 'psg_weight' on an object sets its pick weight")
    source_filter: StringProperty(
        name="Filter", update=_scatter_changed,
        description="Comma-separated name parts to use from the collection, '!' excludes (e.g. 'Trawa, Kwiaty, !v3')")
    mode: EnumProperty(
        name="Mode", update=_scatter_changed, default='LINE',
        items=[
            ('LINE', "Along", "Path: one instance every Spacing, randomly offset inside Width"),
            ('EDGES', "Edges", "Path: pairs of instances on both edges at distance Width (fences, borders)"),
            ('BAND', "Band", "Path: random fill of the band between Inner Width and Width, by Density"),
            ('SCATTER', "Scatter", "Area: uniform random scatter by Density"),
            ('CLUSTER', "Clusters", "Area: groups of instances around random centers, some single"),
            ('AROUND', "Around", "Instances around every instance of the Parent layer (grass and bushes around trees)"),
        ])
    cluster_density: FloatProperty(name="Clusters / 100 m²", default=0.5, min=0.0, soft_max=20.0,
                                   update=_scatter_changed)
    cluster_radius: FloatProperty(name="Cluster Radius", default=4.0, min=0.0, unit='LENGTH', update=_scatter_changed)
    cluster_min: IntProperty(name="Min Count", default=2, min=1, max=127, update=_scatter_changed)
    cluster_max: IntProperty(name="Max Count", default=4, min=1, max=127, update=_scatter_changed)
    single_chance: FloatProperty(name="Single Chance", default=0.3, min=0.0, max=1.0, subtype='FACTOR',
                                 update=_scatter_changed, description="Chance that a cluster is a single instance")
    parent: EnumProperty(name="Parent", items=_parent_items, update=_scatter_changed,
                         description="Layer whose instances this layer surrounds")
    around_min: FloatProperty(name="Radius Min", default=0.8, min=0.0, unit='LENGTH', update=_scatter_changed)
    around_max: FloatProperty(name="Radius Max", default=3.0, min=0.0, unit='LENGTH', update=_scatter_changed)
    around_count_min: IntProperty(name="Min Count", default=3, min=0, max=127, update=_scatter_changed)
    around_count_max: IntProperty(name="Max Count", default=8, min=0, max=127, update=_scatter_changed)
    around_chance: FloatProperty(name="Chance", default=1.0, min=0.0, max=1.0, subtype='FACTOR',
                                 update=_scatter_changed, description="Chance that a parent instance gets surroundings")
    around_scale_radius: BoolProperty(name="Scale with Parent", default=True, update=_scatter_changed,
                                      description="Multiply the radius by the parent instance scale")
    avoid_distance: FloatProperty(name="Avoid Other Layers", default=0.0, min=0.0, unit='LENGTH',
                                  update=_scatter_changed,
                                  description="Keep this distance from instances of the layers above (except Parent)")
    seed: IntProperty(name="Seed", default=1, update=_scatter_changed)
    spacing: FloatProperty(name="Spacing", default=2.0, min=0.05, unit='LENGTH', update=_scatter_changed)
    jitter: FloatProperty(name="Jitter", default=0.3, min=0.0, max=1.0, subtype='FACTOR', update=_scatter_changed)
    width: FloatProperty(name="Width", default=2.0, min=0.0, unit='LENGTH', update=_scatter_changed,
                         description="Half-width of the scatter band (distance from the centerline)")
    inner_width: FloatProperty(name="Inner Width", default=0.0, min=0.0, unit='LENGTH', update=_scatter_changed,
                               description="Keep this distance from the centerline clear (roads, rivers)")
    density: FloatProperty(name="Density", default=0.5, min=0.0, soft_max=10.0, update=_scatter_changed,
                           description="Band / Scatter: instances per square meter")
    edge_falloff: FloatProperty(name="Edge Falloff", default=0.0, min=0.0, max=1.0, subtype='FACTOR',
                                update=_scatter_changed, description="Thin out instances towards the outer edge")
    min_distance: FloatProperty(name="Min Distance", default=0.0, min=0.0, unit='LENGTH', update=_scatter_changed)
    scale_min: FloatProperty(name="Scale Min", default=0.8, min=0.01, update=_scatter_changed)
    scale_max: FloatProperty(name="Scale Max", default=1.2, min=0.01, update=_scatter_changed)
    yaw_random: FloatProperty(name="Random Yaw", default=math.tau, min=0.0, max=math.tau, subtype='ANGLE',
                              update=_scatter_changed)
    yaw_offset: FloatProperty(name="Yaw Offset", default=0.0, subtype='ANGLE', update=_scatter_changed)
    align_to_path: BoolProperty(name="Align to Path", default=False, update=_scatter_changed)
    align_to_normal: FloatProperty(name="Align to Normal", default=0.0, min=0.0, max=1.0, subtype='FACTOR',
                                   update=_scatter_changed)
    max_slope: FloatProperty(name="Max Slope", default=math.radians(90.0), min=0.0, max=math.radians(90.0),
                             subtype='ANGLE', update=_scatter_changed)
    z_offset: FloatProperty(name="Z Offset", default=0.0, unit='LENGTH', update=_scatter_changed)
    skip_misses: BoolProperty(name="Skip Off-Ground", default=True, update=_scatter_changed,
                              description="Skip points where the ray misses the target ground")


class PSG_PathSettings(bpy.types.PropertyGroup):
    is_path: BoolProperty(name="PSG Path", default=False)
    is_area: BoolProperty(name="PSG Area", default=False)
    target: PointerProperty(name="Ground", type=bpy.types.Object, poll=_poll_ground, update=_scatter_changed,
                            description="Mesh the instances snap to and that gets painted")
    live_update: BoolProperty(name="Live Update", default=True,
                              description="Regenerate when the path or a setting changes")
    auto_lock_edited: BoolProperty(name="Keep Edited", default=True,
                                   description="Instances you moved, rotated, scaled or swapped are locked automatically")
    keep_deletions: BoolProperty(name="Keep Deletions", default=True,
                                 description="Instances you deleted stay deleted after regeneration")
    layers: CollectionProperty(type=PSG_Layer)
    active_layer: IntProperty()
    next_uid: IntProperty()
    output: PointerProperty(type=bpy.types.Collection)
    state_json: StringProperty()
    excluded_json: StringProperty()
    painted_target: StringProperty()

    paint_enabled: BoolProperty(name="Paint Ground", default=True, update=_paint_changed)
    paint_color: FloatVectorProperty(name="Color", subtype='COLOR', size=4, min=0.0, max=1.0,
                                     default=(0.30, 0.20, 0.11, 1.0), update=_paint_changed)
    paint_width: FloatProperty(name="Width", default=1.5, min=0.0, unit='LENGTH', update=_paint_changed,
                               description="Full-strength half-width")
    paint_falloff: FloatProperty(name="Blend", default=1.5, min=0.0, unit='LENGTH', update=_paint_changed,
                                 description="Smooth blend distance beyond Width")
    paint_strength: FloatProperty(name="Strength", default=1.0, min=0.0, max=1.0, subtype='FACTOR',
                                  update=_paint_changed)
    paint_noise: FloatProperty(name="Edge Noise", default=0.4, min=0.0, unit='LENGTH', update=_paint_changed)
    paint_noise_scale: FloatProperty(name="Noise Scale", default=0.6, min=0.001, update=_paint_changed)
    paint_seed: IntProperty(name="Noise Seed", default=0, update=_paint_changed)
    paint_order: IntProperty(name="Order", default=0, update=_paint_changed,
                             description="Higher paints on top of lower")


class PSG_Instance(bpy.types.PropertyGroup):
    path: PointerProperty(type=bpy.types.Object)
    layer_uid: IntProperty()
    index: IntProperty()
    locked: BoolProperty(name="Locked", default=False,
                         description="Locked instances survive regeneration untouched")
    source_mesh: StringProperty()
    gen_matrix: FloatVectorProperty(size=16)


class PSG_Ground(bpy.types.PropertyGroup):
    is_ground: BoolProperty(default=False)
    base_color: FloatVectorProperty(name="Base Color", subtype='COLOR', size=4, min=0.0, max=1.0,
                                    default=(0.22, 0.32, 0.12, 1.0), update=_ground_changed)
    mask_in_alpha: BoolProperty(name="Mask in Alpha", default=False, update=_ground_changed,
                                description="Write combined paint coverage into vertex color alpha")


class PSG_SceneSettings(bpy.types.PropertyGroup):
    source: PointerProperty(name="Default Source", type=bpy.types.Collection)
    target: PointerProperty(name="Default Ground", type=bpy.types.Object, poll=_poll_ground)
    smooth: BoolProperty(name="Smooth (Bezier)", default=True)
    simplify: FloatProperty(name="Simplify", default=0.15, min=0.0, unit='LENGTH')
    remove_annotation: BoolProperty(name="Remove Strokes", default=True)
    ground_cell: FloatProperty(name="Ground Cell", default=0.5, min=0.05, unit='LENGTH')
    ground_margin: FloatProperty(name="Ground Margin", default=3.0, min=0.0, unit='LENGTH')


CLASSES = (PSG_Layer, PSG_PathSettings, PSG_Instance, PSG_Ground, PSG_SceneSettings)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Object.psg = PointerProperty(type=PSG_PathSettings)
    bpy.types.Object.psg_inst = PointerProperty(type=PSG_Instance)
    bpy.types.Object.psg_ground = PointerProperty(type=PSG_Ground)
    bpy.types.Scene.psg = PointerProperty(type=PSG_SceneSettings)


def unregister():
    del bpy.types.Scene.psg
    del bpy.types.Object.psg_ground
    del bpy.types.Object.psg_inst
    del bpy.types.Object.psg
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
