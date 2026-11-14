"""PSG - Path Scatter Generator.

Draw an annotation line, turn it into an editable path curve, scatter instanced
(linked-duplicate) assets along it and paint the ground under it with vertex colors
blended at the edges. Output exports to Unity 6 as FBX with shared meshes.
"""

bl_info = {
    "name": "PSG Path Scatter",
    "author": "WSF GameJam 2026",
    "version": (0, 1, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > PSG",
    "description": "Annotation-driven instanced scatter with vertex-color ground paint, Unity-ready",
    "category": "Object",
}

from . import core, ops, props, ui  # noqa: E402

MODULES = (props, ops, ui, core)


def register():
    for module in MODULES:
        module.register()


def unregister():
    for module in reversed(MODULES):
        module.unregister()
