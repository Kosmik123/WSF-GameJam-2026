"""PSG - Path Scatter Generator.

Scatter instanced (linked-duplicate) assets on a selected mesh (clusters, surroundings,
uniform layers) or along a path drawn with an annotation, and paint the ground under
paths with vertex colors blended at the edges. Output exports to Unity 6 as FBX with
shared meshes.
"""

bl_info = {
    "name": "PSG Path Scatter",
    "author": "WSF GameJam 2026",
    "version": (0, 6, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > PSG",
    "description": "Instanced area and path scatter with vertex-color ground paint, Unity-ready",
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
