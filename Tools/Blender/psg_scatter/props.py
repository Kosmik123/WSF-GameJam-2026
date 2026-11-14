import math

import bpy
from bpy.props import (BoolProperty, CollectionProperty, EnumProperty, FloatProperty,
                       FloatVectorProperty, IntProperty, PointerProperty, StringProperty)

from . import core


def _scatter_changed(self, _context):
    core.schedule(self.id_data, scatter=True)


def _paint_changed(self, _context):
    core.schedule(self.id_data, scatter=False)


def _display_changed(self, _context):
    core.apply_layer_display(self.id_data, self)


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
    display_type: EnumProperty(
        name="Display As", update=_display_changed, default='TEXTURED',
        description="Viewport display of this layer's instances (Bounds is the fastest). Not exported",
        items=[
            ('TEXTURED', "Textured", "Full display"),
            ('SOLID', "Solid", "Solid shading only"),
            ('WIRE', "Wire", "Wireframe"),
            ('BOUNDS', "Bounds", "Bounding box only, fastest"),
        ])
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
    instance_count: IntProperty()
    locked_count: IntProperty()
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

    terrain_enabled: BoolProperty(name="Terrain Shading", default=False, update=_ground_changed,
                                  description="Color from height (Z), slope and noise instead of a flat Base Color")
    flat_dark: FloatVectorProperty(name="Flat Dark", subtype='COLOR', size=3, min=0.0, max=1.0,
                                   default=(0.026, 0.082, 0.009), update=_ground_changed)
    flat_light: FloatVectorProperty(name="Flat Light", subtype='COLOR', size=3, min=0.0, max=1.0,
                                    default=(0.10, 0.21, 0.023), update=_ground_changed)
    slope_dark: FloatVectorProperty(name="Slope Dark", subtype='COLOR', size=3, min=0.0, max=1.0,
                                    default=(0.24, 0.22, 0.27), update=_ground_changed)
    slope_light: FloatVectorProperty(name="Slope Light", subtype='COLOR', size=3, min=0.0, max=1.0,
                                     default=(0.46, 0.42, 0.48), update=_ground_changed)
    bias_color: FloatVectorProperty(name="Bias Color", subtype='COLOR', size=3, min=0.0, max=1.0,
                                    default=(0.42, 0.33, 0.10), update=_ground_changed,
                                    description="Color that noise patches lean towards (dry grass, sand)")
    gradient_strength: FloatProperty(name="Z Gradient", default=0.8, min=0.0, max=1.0, subtype='FACTOR',
                                     update=_ground_changed,
                                     description="Dark at the lowest point, light at the highest (same rule as the assets)")
    gradient_auto: BoolProperty(name="Auto Range", default=True, update=_ground_changed,
                                description="Use the mesh's own lowest and highest point")
    gradient_min: FloatProperty(name="Z Min", default=0.0, unit='LENGTH', update=_ground_changed)
    gradient_max: FloatProperty(name="Z Max", default=5.0, unit='LENGTH', update=_ground_changed)
    slope_angle: FloatProperty(name="Slope Angle", default=math.radians(28.0), min=0.0, max=math.radians(90.0),
                               subtype='ANGLE', update=_ground_changed,
                               description="Steeper than this uses the slope (rock) palette")
    slope_blend: FloatProperty(name="Slope Blend", default=math.radians(14.0), min=0.0, max=math.radians(90.0),
                               subtype='ANGLE', update=_ground_changed)
    noise_scale: FloatProperty(name="Noise Scale", default=0.12, min=0.001, update=_ground_changed)
    noise_brightness: FloatProperty(name="Noise Brightness", default=0.25, min=0.0, max=1.0, subtype='FACTOR',
                                    update=_ground_changed, description="Noise shift along the dark/light palette")
    noise_bias: FloatProperty(name="Noise Bias", default=0.3, min=0.0, max=1.0, subtype='FACTOR',
                              update=_ground_changed, description="How much noise patches lean to the Bias Color")
    faceted: BoolProperty(name="Faceted", default=True, update=_ground_changed,
                          description="One color per face, like the assets (flat facets)")
    facet_jitter: FloatProperty(name="Facet Jitter", default=0.08, min=0.0, max=0.5, subtype='FACTOR',
                                update=_ground_changed, description="Random brightness per face (assets: 5-12%)")
    seed: IntProperty(name="Seed", default=0, update=_ground_changed)


def _shape_changed(self, _context):
    if self.enabled:
        core.schedule_shape(self.id_data)


class PSG_Shape(bpy.types.PropertyGroup):
    enabled: BoolProperty(name="Shaped", default=False)
    seed: IntProperty(name="Seed", default=1, update=_shape_changed)
    max_height: FloatProperty(name="Max Height", default=2.0, min=0.0, soft_max=10.0, unit='LENGTH',
                              update=_shape_changed,
                              description="Hard height budget: lowest to highest point (keep low for the torus)")
    base_size: FloatProperty(name="Base Size", default=30.0, min=1.0, unit='LENGTH', update=_shape_changed,
                             description="Size of the large landforms")
    plateau_amount: FloatProperty(name="Plateaus", default=0.6, min=0.0, max=1.0, subtype='FACTOR',
                                  update=_shape_changed, description="Terracing into flat levels")
    plateau_levels: IntProperty(name="Levels", default=3, min=1, max=8, update=_shape_changed)
    cliff: FloatProperty(name="Cliff Width", default=0.22, min=0.02, max=1.0, subtype='FACTOR',
                         update=_shape_changed, description="Part of each level used by the slope (small = steep)")
    valley_amount: FloatProperty(name="Valleys", default=0.5, min=0.0, max=1.0, subtype='FACTOR',
                                 update=_shape_changed)
    valley_width: FloatProperty(name="Valley Width", default=0.15, min=0.01, max=1.0, subtype='FACTOR',
                                update=_shape_changed)
    valley_size: FloatProperty(name="Valley Size", default=38.0, min=1.0, unit='LENGTH', update=_shape_changed)
    hill_amount: FloatProperty(name="Hills", default=0.3, min=0.0, max=1.0, subtype='FACTOR',
                               update=_shape_changed, description="Small bumps on top of everything")
    hill_size: FloatProperty(name="Hill Size", default=6.0, min=0.5, unit='LENGTH', update=_shape_changed)
    edge_flatten: FloatProperty(name="Flatten Edges", default=0.0, min=0.0, unit='LENGTH', update=_shape_changed,
                                description="Blend to Edge Height near the mesh border (seamless segments)")
    edge_height: FloatProperty(name="Edge Height", default=0.0, min=0.0, max=1.0, subtype='FACTOR',
                               update=_shape_changed)
    jitter: FloatProperty(name="Vertex Jitter", default=0.35, min=0.0, max=0.9, subtype='FACTOR',
                          update=_shape_changed,
                          description="Random XY offset of vertices to break the grid (low poly look)")
    triangulate: BoolProperty(name="Triangulate", default=True, update=_shape_changed,
                              description="Flat triangles like low poly assets (cannot be undone by Reset)")


class PSG_SceneSettings(bpy.types.PropertyGroup):
    source: PointerProperty(name="Default Source", type=bpy.types.Collection)
    target: PointerProperty(name="Default Ground", type=bpy.types.Object, poll=_poll_ground)
    smooth: BoolProperty(name="Smooth (Bezier)", default=True)
    simplify: FloatProperty(name="Simplify", default=0.15, min=0.0, unit='LENGTH')
    remove_annotation: BoolProperty(name="Remove Strokes", default=True)
    ground_cell: FloatProperty(name="Ground Cell", default=0.5, min=0.05, unit='LENGTH')
    ground_margin: FloatProperty(name="Ground Margin", default=3.0, min=0.0, unit='LENGTH')


CLASSES = (PSG_Layer, PSG_PathSettings, PSG_Instance, PSG_Ground, PSG_Shape, PSG_SceneSettings)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Object.psg = PointerProperty(type=PSG_PathSettings)
    bpy.types.Object.psg_inst = PointerProperty(type=PSG_Instance)
    bpy.types.Object.psg_ground = PointerProperty(type=PSG_Ground)
    bpy.types.Object.psg_shape = PointerProperty(type=PSG_Shape)
    bpy.types.Scene.psg = PointerProperty(type=PSG_SceneSettings)


def unregister():
    del bpy.types.Scene.psg
    del bpy.types.Object.psg_shape
    del bpy.types.Object.psg_ground
    del bpy.types.Object.psg_inst
    del bpy.types.Object.psg
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
