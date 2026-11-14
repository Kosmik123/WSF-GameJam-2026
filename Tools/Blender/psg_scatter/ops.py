import random

import bpy
from bpy.props import BoolProperty, EnumProperty, IntProperty, StringProperty
from bpy_extras.io_utils import ExportHelper

from . import core


def active_path(context):
    """Active generator: a path curve, an area mesh, or the generator of the active instance."""
    obj = context.active_object
    if obj is None:
        return None
    if core.is_generator(obj):
        return obj
    return obj.psg_inst.path


def selected_paths(context):
    paths = {o for o in context.selected_objects if core.is_generator(o)}
    current = active_path(context)
    if current is not None:
        paths.add(current)
    return sorted(paths, key=lambda o: o.name)


def selected_instances(context):
    return [o for o in context.selected_objects if o.psg_inst.path is not None]


class PathOperator(bpy.types.Operator):
    @classmethod
    def poll(cls, context):
        return active_path(context) is not None


class InstanceOperator(bpy.types.Operator):
    @classmethod
    def poll(cls, context):
        return any(o.psg_inst.path is not None for o in context.selected_objects)


# ------------------------------------------------------------------ create

class PSG_OT_draw_annotation(bpy.types.Operator):
    bl_idname = "psg.draw_annotation"
    bl_label = "Draw Path"
    bl_description = "Annotate tool, strokes placed on surfaces"

    def execute(self, context):
        context.scene.tool_settings.annotation_stroke_placement_view3d = 'SURFACE'
        bpy.ops.wm.tool_set_by_id(name="builtin.annotate")
        return {'FINISHED'}


class PSG_OT_annotation_to_path(bpy.types.Operator):
    bl_idname = "psg.annotation_to_path"
    bl_label = "Annotation to Path"
    bl_description = "Convert every 3D annotation stroke into an editable PSG path curve"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        scene = context.scene
        defaults = scene.psg
        strokes = core.annotation_strokes(scene)
        if not strokes:
            self.report({'WARNING'}, "No 3D annotation strokes (draw with placement 3D Cursor / Surface)")
            return {'CANCELLED'}
        created = []
        for frame, stroke, points in strokes:
            points = core.simplify(points, defaults.simplify)
            if len(points) < 2:
                continue
            path = core.create_path(points, scene, defaults.smooth)
            core.init_path(path, defaults)
            created.append(path)
            if defaults.remove_annotation:
                frame.strokes.remove(stroke)
        for path in created:
            core.regenerate(path, context.evaluated_depsgraph_get())
        select_only(context, created)
        self.report({'INFO'}, "Created %d path(s)" % len(created))
        return {'FINISHED'}


class PSG_OT_make_path(bpy.types.Operator):
    bl_idname = "psg.make_path"
    bl_label = "Use Curve as Path"
    bl_description = "Turn the selected curve objects into PSG paths"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return any(o.type == 'CURVE' for o in context.selected_objects)

    def execute(self, context):
        for obj in context.selected_objects:
            if obj.type == 'CURVE':
                core.init_path(obj, context.scene.psg)
                core.regenerate(obj, context.evaluated_depsgraph_get())
        return {'FINISHED'}


BIOMES = [(b, b, "") for b in ("Las", "Pustynia", "Wybrzeze", "Lod", "Wulkan", "Bagno")]


class PSG_OT_make_area(bpy.types.Operator):
    bl_idname = "psg.make_area"
    bl_label = "Scatter on Selected Mesh"
    bl_description = "Turn the selected mesh (plane, terrain) into a PSG area with scatter layers"
    bl_options = {'REGISTER', 'UNDO'}

    preset: BoolProperty(name="Biome Preset", default=True,
                         description="Add trees in clusters + bushes and grass around them + grass and rocks")
    biome: EnumProperty(name="Biome", items=BIOMES)

    @classmethod
    def poll(cls, context):
        return any(o.type == 'MESH' and o.psg_inst.path is None for o in context.selected_objects)

    def invoke(self, context, _event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        missing = []
        for obj in context.selected_objects:
            if obj.type != 'MESH' or obj.psg_inst.path is not None:
                continue
            obj.psg.is_area = True
            if self.preset and not obj.psg.layers:
                missing += core.apply_biome_preset(obj, self.biome, replace=False)
            core.init_area(obj, context.scene.psg)
            core.regenerate(obj, context.evaluated_depsgraph_get())
        if missing:
            self.report({'WARNING'}, "Missing collections: " + ", ".join(sorted(set(missing))))
        return {'FINISHED'}


class PSG_OT_area_preset(PathOperator):
    bl_idname = "psg.area_preset"
    bl_label = "Apply Biome Preset"
    bl_description = "Replace the layers of the active area with the biome preset"
    bl_options = {'REGISTER', 'UNDO'}

    biome: EnumProperty(name="Biome", items=BIOMES)
    replace: BoolProperty(name="Replace Layers", default=True)

    @classmethod
    def poll(cls, context):
        return core.is_area(active_path(context))

    def invoke(self, context, _event):
        return context.window_manager.invoke_props_dialog(self)

    def execute(self, context):
        area = active_path(context)
        missing = core.apply_biome_preset(area, self.biome, self.replace)
        core.regenerate(area, context.evaluated_depsgraph_get())
        if missing:
            self.report({'WARNING'}, "Missing collections: " + ", ".join(sorted(set(missing))))
        return {'FINISHED'}


class PSG_OT_create_ground(bpy.types.Operator):
    bl_idname = "psg.create_ground"
    bl_label = "Create Ground Plane"
    bl_description = "Grid plane under the selected paths, used for snapping and vertex-color paint"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        path = active_path(context)
        return path is not None and path.type == 'CURVE'

    def execute(self, context):
        paths = [p for p in selected_paths(context) if p.type == 'CURVE']
        defaults = context.scene.psg
        ground = core.create_ground(paths, defaults.ground_cell, defaults.ground_margin, context.scene)
        if ground is None:
            return {'CANCELLED'}
        if defaults.target is None:
            defaults.target = ground
        for path in paths:
            core.regenerate(path, context.evaluated_depsgraph_get())
        return {'FINISHED'}


# ------------------------------------------------------------------ path

class PSG_OT_generate(bpy.types.Operator):
    bl_idname = "psg.generate"
    bl_label = "Regenerate"
    bl_description = "Rebuild instances (locked and edited instances are kept)"
    bl_options = {'REGISTER', 'UNDO'}

    all_paths: BoolProperty(name="All Paths", default=False)

    def execute(self, context):
        paths = core.all_generators() if self.all_paths else selected_paths(context)
        # areas first: paths may snap onto an area that also gets scattered
        paths.sort(key=lambda p: (not core.is_area(p), p.name))
        depsgraph = context.evaluated_depsgraph_get()
        total = sum(core.regenerate(path, depsgraph) for path in paths)
        self.report({'INFO'}, "PSG: %d path(s), %d new instance(s)" % (len(paths), total))
        return {'FINISHED'}


class PSG_OT_clear(PathOperator):
    bl_idname = "psg.clear"
    bl_label = "Clear"
    bl_options = {'REGISTER', 'UNDO'}

    include_locked: BoolProperty(name="Include Locked", default=False)

    def execute(self, context):
        for path in selected_paths(context):
            core.clear(path, self.include_locked)
            if not core.is_area(path):
                core.repaint_targets(path)
        return {'FINISHED'}


class PSG_OT_reset_edits(PathOperator):
    bl_idname = "psg.reset_edits"
    bl_label = "Reset Edits"
    bl_description = "Forget deletions and unlock everything, then regenerate"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        for path in selected_paths(context):
            core.reset_edits(path)
            core.regenerate(path, context.evaluated_depsgraph_get())
        return {'FINISHED'}


class PSG_OT_bake(PathOperator):
    bl_idname = "psg.bake"
    bl_label = "Make Permanent"
    bl_description = "Detach all instances of the path from the generator (they become plain linked duplicates)"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        for path in selected_paths(context):
            for obj in core.path_instances(path):
                core.detach(obj)
            path.psg.state_json = ""
            path.psg.excluded_json = ""
        return {'FINISHED'}


class PSG_OT_repaint(bpy.types.Operator):
    bl_idname = "psg.repaint"
    bl_label = "Repaint Ground"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        grounds = {p.psg.target for p in core.all_paths() if p.psg.target is not None}
        obj = context.active_object
        if obj is not None and obj.type == 'MESH' and obj.psg_ground.is_ground:
            grounds.add(obj)
        for ground in grounds:
            core.repaint_ground(ground)
        return {'FINISHED'}


class PSG_OT_select_instances(PathOperator):
    bl_idname = "psg.select_instances"
    bl_label = "Select Instances"
    bl_options = {'REGISTER', 'UNDO'}

    layer_only: BoolProperty(name="Active Layer Only", default=False)
    locked_only: BoolProperty(name="Locked Only", default=False)

    def execute(self, context):
        path = active_path(context)
        objects = core.path_instances(path)
        if self.layer_only and path.psg.layers:
            uid = path.psg.layers[path.psg.active_layer].uid
            objects = [o for o in objects if o.psg_inst.layer_uid == uid]
        if self.locked_only:
            objects = [o for o in objects if o.psg_inst.locked]
        select_only(context, objects)
        return {'FINISHED'}


class PSG_OT_select_path(InstanceOperator):
    bl_idname = "psg.select_path"
    bl_label = "Select Path"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        paths = {o.psg_inst.path for o in selected_instances(context)}
        select_only(context, list(paths))
        return {'FINISHED'}


# ------------------------------------------------------------------ layers

class PSG_OT_layer_add(PathOperator):
    bl_idname = "psg.layer_add"
    bl_label = "Add Layer"
    bl_options = {'REGISTER', 'UNDO'}

    duplicate: BoolProperty(default=False)

    def execute(self, context):
        path = active_path(context)
        settings = path.psg
        copy_from = None
        if self.duplicate and settings.layers:
            copy_from = settings.layers[settings.active_layer]
        core.add_layer(path, context.scene.psg.source, copy_from)
        core.regenerate(path, context.evaluated_depsgraph_get())
        return {'FINISHED'}


class PSG_OT_layer_remove(PathOperator):
    bl_idname = "psg.layer_remove"
    bl_label = "Remove Layer"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        path = active_path(context)
        settings = path.psg
        if not settings.layers:
            return {'CANCELLED'}
        uid = settings.layers[settings.active_layer].uid
        for obj in core.path_instances(path):
            if obj.psg_inst.layer_uid == uid:
                core.remove_object(obj)
        settings.layers.remove(settings.active_layer)
        settings.active_layer = max(min(settings.active_layer, len(settings.layers) - 1), 0)
        core.regenerate(path, context.evaluated_depsgraph_get())
        return {'FINISHED'}


class PSG_OT_layer_move(PathOperator):
    bl_idname = "psg.layer_move"
    bl_label = "Move Layer"
    bl_options = {'REGISTER', 'UNDO'}

    direction: EnumProperty(items=[('UP', "Up", ""), ('DOWN', "Down", "")])

    def execute(self, context):
        settings = active_path(context).psg
        index = settings.active_layer
        target = index - 1 if self.direction == 'UP' else index + 1
        if 0 <= target < len(settings.layers):
            settings.layers.move(index, target)
            settings.active_layer = target
        return {'FINISHED'}


class PSG_OT_layer_reseed(PathOperator):
    bl_idname = "psg.layer_reseed"
    bl_label = "New Seed"
    bl_description = "Random seed for the active layer (edited instances are kept)"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        path = active_path(context)
        settings = path.psg
        if not settings.layers:
            return {'CANCELLED'}
        layer = settings.layers[settings.active_layer]
        layer.seed = random.randint(1, 99999)
        core.regenerate(path, context.evaluated_depsgraph_get())
        return {'FINISHED'}


# ------------------------------------------------------------------ instances

class PSG_OT_lock(InstanceOperator):
    bl_idname = "psg.lock"
    bl_label = "Lock"
    bl_options = {'REGISTER', 'UNDO'}

    lock: BoolProperty(default=True)

    def execute(self, context):
        for obj in selected_instances(context):
            obj.psg_inst.locked = self.lock
            if not self.lock:
                obj.psg_inst.gen_matrix = core.matrix_to_list(obj.matrix_basis)
                obj.psg_inst.source_mesh = obj.data.name if obj.data else ""
        return {'FINISHED'}


class PSG_OT_reroll(InstanceOperator):
    bl_idname = "psg.reroll"
    bl_label = "Reroll"
    bl_description = "Change selected instances (result is locked)"
    bl_options = {'REGISTER', 'UNDO'}

    mode: EnumProperty(items=[
        ('VARIANT', "Random Variant", "Swap mesh to another random source"),
        ('NEXT', "Next Variant", "Cycle through source meshes by name"),
        ('TRANSFORM', "Rotation/Scale", "New random yaw and scale"),
    ])
    seed: IntProperty(name="Seed", default=0)

    def execute(self, context):
        rng = random.Random(self.seed or None)
        changed = 0
        for obj in selected_instances(context):
            if self.mode == 'TRANSFORM':
                changed += core.reroll_transform(obj, rng)
            else:
                changed += core.swap_variant(obj, self.mode, rng)
        self.report({'INFO'}, "Rerolled %d instance(s)" % changed)
        return {'FINISHED'}


class PSG_OT_snap(InstanceOperator):
    bl_idname = "psg.snap"
    bl_label = "Snap to Ground"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        depsgraph = context.evaluated_depsgraph_get()
        for obj in selected_instances(context):
            core.snap_to_ground(obj, depsgraph)
        return {'FINISHED'}


class PSG_OT_detach(InstanceOperator):
    bl_idname = "psg.detach"
    bl_label = "Detach"
    bl_description = "Selected instances stop being managed by the generator"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        for obj in selected_instances(context):
            core.detach(obj)
        return {'FINISHED'}


# ------------------------------------------------------------------ export

class PSG_OT_export_unity(bpy.types.Operator, ExportHelper):
    bl_idname = "psg.export_unity"
    bl_label = "Export FBX for Unity"
    bl_description = "FBX with shared meshes per variant, Y-up, sRGB vertex colors (see docs/PSG.md)"
    filename_ext = ".fbx"
    filter_glob: StringProperty(default="*.fbx", options={'HIDDEN'})

    scope: EnumProperty(name="Scope", items=[
        ('ALL', "All Paths", "Every PSG instance and painted ground in the scene"),
        ('SELECTED', "Selected Paths", "Instances and grounds of the selected paths"),
    ])
    include_ground: BoolProperty(name="Include Ground", default=True)

    def execute(self, context):
        paths = core.all_generators() if self.scope == 'ALL' else selected_paths(context)
        objects = []
        for path in paths:
            objects.extend(core.path_instances(path))
            if self.include_ground:
                ground = path if core.is_area(path) else path.psg.target
                if ground is not None:
                    objects.append(ground)
        view_layer = context.view_layer
        objects = [o for o in dict.fromkeys(objects) if o.name in view_layer.objects and o.visible_get()]
        if not objects:
            self.report({'WARNING'}, "Nothing to export")
            return {'CANCELLED'}
        previous = list(context.selected_objects)
        previous_active = view_layer.objects.active
        select_only(context, objects)
        try:
            bpy.ops.export_scene.fbx(
                filepath=self.filepath, use_selection=True, object_types={'MESH'},
                apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='OFF',
                colors_type='SRGB', add_leaf_bones=False, bake_anim=False)
        finally:
            select_only(context, previous)
            view_layer.objects.active = previous_active
        self.report({'INFO'}, "Exported %d object(s)" % len(objects))
        return {'FINISHED'}


def select_only(context, objects):
    view_layer = context.view_layer
    for obj in context.selected_objects:
        obj.select_set(False)
    last = None
    for obj in objects:
        if obj is not None and obj.name in view_layer.objects:
            obj.select_set(True)
            last = obj
    if last is not None:
        view_layer.objects.active = last


CLASSES = (
    PSG_OT_draw_annotation, PSG_OT_annotation_to_path, PSG_OT_make_path, PSG_OT_make_area,
    PSG_OT_area_preset, PSG_OT_create_ground,
    PSG_OT_generate, PSG_OT_clear, PSG_OT_reset_edits, PSG_OT_bake, PSG_OT_repaint,
    PSG_OT_select_instances, PSG_OT_select_path,
    PSG_OT_layer_add, PSG_OT_layer_remove, PSG_OT_layer_move, PSG_OT_layer_reseed,
    PSG_OT_lock, PSG_OT_reroll, PSG_OT_snap, PSG_OT_detach, PSG_OT_export_unity,
)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
