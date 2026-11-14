import bpy

from . import core
from .ops import active_path, terrain_target
from .props import AREA_MODES, PATH_MODES


class PSG_UL_layers(bpy.types.UIList):
    def draw_item(self, _context, layout, _data, item, _icon, _active_data, _active_prop, _index=0):
        row = layout.row(align=True)
        row.prop(item, "enabled", text="")
        row.prop(item, "name", text="", emboss=False)
        bounds = item.display_type == 'BOUNDS'
        row.label(text="", icon='SHADING_BBOX' if bounds else 'BLANK1')
        row.label(text=item.source.name if item.source else "-", icon='OUTLINER_COLLECTION')


class PSGPanel:
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "PSG"


class PSG_PT_main(PSGPanel, bpy.types.Panel):
    bl_label = "PSG Path Scatter"

    def draw(self, context):
        layout = self.layout
        defaults = context.scene.psg
        col = layout.column(align=True)
        col.operator("psg.draw_annotation", icon='GREASEPENCIL')
        col.operator("psg.annotation_to_path", icon='CURVE_PATH')
        col.operator("psg.make_path", icon='OUTLINER_OB_CURVE')
        col.operator("psg.make_area", icon='MESH_PLANE')
        box = layout.box()
        box.label(text="Defaults for new paths")
        box.prop(defaults, "source")
        box.prop(defaults, "target")
        row = box.row()
        row.prop(defaults, "smooth")
        row.prop(defaults, "remove_annotation")
        box.prop(defaults, "simplify")
        row = layout.row(align=True)
        row.operator("psg.generate", text="Regenerate All", icon='FILE_REFRESH').all_paths = True
        row.operator("psg.repaint", text="", icon='BRUSH_DATA')
        layout.operator("psg.export_unity", icon='EXPORT')


class PSG_PT_path(PSGPanel, bpy.types.Panel):
    bl_label = "Generator"
    bl_parent_id = "PSG_PT_main"

    @classmethod
    def poll(cls, context):
        return active_path(context) is not None

    def draw(self, context):
        layout = self.layout
        path = active_path(context)
        settings = path.psg
        if core.is_area(path):
            layout.label(text="Area: " + path.name, icon='MESH_PLANE')
            layout.operator("psg.area_preset", icon='PRESET')
        else:
            layout.label(text="Path: " + path.name, icon='CURVE_PATH')
            layout.prop(settings, "target")
            layout.operator("psg.create_ground", icon='MESH_GRID')
        row = layout.row(align=True)
        row.prop(settings, "live_update", toggle=True)
        row.prop(settings, "auto_lock_edited", toggle=True)
        row.prop(settings, "keep_deletions", toggle=True)
        row = layout.row(align=True)
        row.operator("psg.generate", icon='FILE_REFRESH').all_paths = False
        row.operator("psg.clear", icon='TRASH')
        row = layout.row(align=True)
        row.operator("psg.reset_edits", icon='LOOP_BACK')
        row.operator("psg.bake", icon='CHECKMARK')
        layout.label(text="%d instances, %d locked" % (settings.instance_count, settings.locked_count), icon='INFO')
        row = layout.row(align=True)
        row.label(text="Display all layers:")
        for display, icon in (('TEXTURED', 'SHADING_TEXTURE'), ('WIRE', 'SHADING_WIRE'), ('BOUNDS', 'SHADING_BBOX')):
            row.operator("psg.display_all", text="", icon=icon).display_type = display
        row = layout.row(align=True)
        row.operator("psg.select_instances", text="Select All").layer_only = False
        op = row.operator("psg.select_instances", text="Select Locked")
        op.locked_only = True


class PSG_PT_layers(PSGPanel, bpy.types.Panel):
    bl_label = "Scatter Layers"
    bl_parent_id = "PSG_PT_main"

    @classmethod
    def poll(cls, context):
        return active_path(context) is not None

    def draw(self, context):
        layout = self.layout
        settings = active_path(context).psg
        row = layout.row()
        row.template_list("PSG_UL_layers", "", settings, "layers", settings, "active_layer", rows=3)
        col = row.column(align=True)
        col.operator("psg.layer_add", text="", icon='ADD').duplicate = False
        col.operator("psg.layer_remove", text="", icon='REMOVE')
        col.operator("psg.layer_add", text="", icon='DUPLICATE').duplicate = True
        col.separator()
        col.operator("psg.layer_move", text="", icon='TRIA_UP').direction = 'UP'
        col.operator("psg.layer_move", text="", icon='TRIA_DOWN').direction = 'DOWN'
        if not settings.layers:
            return
        layer = settings.layers[settings.active_layer]
        area = core.is_area(active_path(context))
        layout.prop(layer, "display_type", expand=True)
        layout.prop(layer, "source")
        layout.prop(layer, "source_filter")
        sources = core.layer_sources(layer)
        layout.label(text="%d variant(s)" % len(sources), icon='MESH_DATA')
        row = layout.row(align=True)
        for mode in (AREA_MODES if area else PATH_MODES):
            row.prop_enum(layer, "mode", mode)
        row = layout.row(align=True)
        row.prop(layer, "seed")
        row.operator("psg.layer_reseed", text="", icon='FILE_REFRESH')
        col = layout.column(align=True)
        if layer.mode == 'AROUND':
            col.prop(layer, "parent")
            row = col.row(align=True)
            row.prop(layer, "around_min")
            row.prop(layer, "around_max")
            row = col.row(align=True)
            row.prop(layer, "around_count_min")
            row.prop(layer, "around_count_max")
            col.prop(layer, "around_chance")
            col.prop(layer, "around_scale_radius")
        elif layer.mode == 'CLUSTER':
            col.prop(layer, "cluster_density")
            col.prop(layer, "cluster_radius")
            row = col.row(align=True)
            row.prop(layer, "cluster_min")
            row.prop(layer, "cluster_max")
            col.prop(layer, "single_chance")
        elif layer.mode in {'BAND', 'SCATTER'}:
            col.prop(layer, "density")
        else:
            col.prop(layer, "spacing")
            col.prop(layer, "jitter")
        if not area and layer.mode != 'AROUND':
            col.prop(layer, "width")
            col.prop(layer, "inner_width")
            col.prop(layer, "edge_falloff")
        col.prop(layer, "min_distance")
        col.prop(layer, "avoid_distance")
        col = layout.column(align=True)
        row = col.row(align=True)
        row.prop(layer, "scale_min")
        row.prop(layer, "scale_max")
        col.prop(layer, "yaw_random")
        col.prop(layer, "yaw_offset")
        if not area:
            col.prop(layer, "align_to_path")
        col.prop(layer, "align_to_normal")
        col.prop(layer, "max_slope")
        col.prop(layer, "z_offset")
        col.prop(layer, "skip_misses")


class PSG_PT_paint(PSGPanel, bpy.types.Panel):
    bl_label = "Ground Paint"
    bl_parent_id = "PSG_PT_main"

    @classmethod
    def poll(cls, context):
        path = active_path(context)
        return path is not None and path.type == 'CURVE'

    def draw_header(self, context):
        self.layout.prop(active_path(context).psg, "paint_enabled", text="")

    def draw(self, context):
        layout = self.layout
        settings = active_path(context).psg
        layout.active = settings.paint_enabled
        layout.prop(settings, "paint_color")
        col = layout.column(align=True)
        col.prop(settings, "paint_width")
        col.prop(settings, "paint_falloff")
        col.prop(settings, "paint_strength")
        col = layout.column(align=True)
        col.prop(settings, "paint_noise")
        col.prop(settings, "paint_noise_scale")
        col.prop(settings, "paint_seed")
        layout.prop(settings, "paint_order")
        ground = settings.target
        if ground is not None:
            layout.label(text="Ground colors: Terrain Paint panel", icon='MESH_GRID')


class PSG_PT_terrain(PSGPanel, bpy.types.Panel):
    bl_label = "Terrain Paint"
    bl_parent_id = "PSG_PT_main"

    @classmethod
    def poll(cls, context):
        return terrain_target(context) is not None

    def draw(self, context):
        layout = self.layout
        ground_obj = terrain_target(context)
        ground = ground_obj.psg_ground
        layout.label(text=ground_obj.name, icon='MESH_GRID')
        layout.operator("psg.paint_terrain", text="Paint / Sample Biome Palette", icon='BRUSH_DATA')
        if not ground.is_ground:
            return
        layout.prop(ground, "terrain_enabled")
        if not ground.terrain_enabled:
            layout.prop(ground, "base_color")
            layout.prop(ground, "mask_in_alpha")
            return
        box = layout.box()
        row = box.row(align=True)
        row.prop(ground, "flat_dark", text="")
        row.prop(ground, "flat_light", text="")
        row.label(text="Flat")
        row = box.row(align=True)
        row.prop(ground, "slope_dark", text="")
        row.prop(ground, "slope_light", text="")
        row.label(text="Slope")
        row = box.row(align=True)
        row.prop(ground, "bias_color", text="")
        row.label(text="Bias")
        col = layout.column(align=True)
        col.prop(ground, "gradient_strength")
        col.prop(ground, "gradient_auto")
        if not ground.gradient_auto:
            row = col.row(align=True)
            row.prop(ground, "gradient_min")
            row.prop(ground, "gradient_max")
        col = layout.column(align=True)
        col.prop(ground, "slope_angle")
        col.prop(ground, "slope_blend")
        col = layout.column(align=True)
        col.prop(ground, "noise_scale")
        col.prop(ground, "noise_brightness")
        col.prop(ground, "noise_bias")
        col = layout.column(align=True)
        col.prop(ground, "faceted")
        col.prop(ground, "facet_jitter")
        col.prop(ground, "seed")
        layout.prop(ground, "mask_in_alpha")


class PSG_PT_instances(PSGPanel, bpy.types.Panel):
    bl_label = "Selected Instances"
    bl_parent_id = "PSG_PT_main"

    @classmethod
    def poll(cls, context):
        return any(o.psg_inst.path is not None for o in context.selected_objects)

    def draw(self, context):
        layout = self.layout
        obj = context.active_object
        if obj is not None and obj.psg_inst.path is not None:
            inst = obj.psg_inst
            _path, layer = core.instance_layer(obj)
            layout.label(text="%s / %s #%d" % (inst.path.name, layer.name if layer else "?", inst.index))
            layout.prop(inst, "locked")
        row = layout.row(align=True)
        row.operator("psg.lock", text="Lock", icon='LOCKED').lock = True
        row.operator("psg.lock", text="Unlock", icon='UNLOCKED').lock = False
        col = layout.column(align=True)
        col.operator("psg.reroll", text="Random Variant", icon='MOD_ARRAY').mode = 'VARIANT'
        col.operator("psg.reroll", text="Next Variant", icon='FORWARD').mode = 'NEXT'
        col.operator("psg.reroll", text="Reroll Rotation/Scale", icon='DRIVER_ROTATIONAL_DIFFERENCE').mode = 'TRANSFORM'
        row = layout.row(align=True)
        row.operator("psg.snap", icon='SNAP_ON')
        row.operator("psg.detach", icon='UNLINKED')
        layout.operator("psg.select_path", icon='CURVE_PATH')


CLASSES = (PSG_UL_layers, PSG_PT_main, PSG_PT_path, PSG_PT_layers, PSG_PT_paint, PSG_PT_terrain,
           PSG_PT_instances)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)
