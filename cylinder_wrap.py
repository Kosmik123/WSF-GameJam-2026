bl_info = {
    "name": "Cylinder Wrap",
    "author": "",
    "version": (2, 0, 0),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar (N) > Cyl Wrap",
    "description": "Zwija teren modyfikatorem Simple Deform (Bend) i przykleja do niego obiekty z kolekcji",
    "category": "Object",
}

import math
import bpy
from bpy.app.handlers import persistent
from mathutils import Matrix

FLAG = "cylwrap_plane"    # na obiekcie: nazwa terenu, do którego jest zawinięty
STATE = "cylwrap_state"   # na terenie: parametry użyte przy zawijaniu
HINT = "cylwrap_theta"    # na obiekcie: kąt przy zawinięciu (rozstrzyga szew przy 360°)
AXES = {'X': 0, 'Y': 1, 'Z': 2}

_busy = False


# ---------------------------------------------------------------------------
# Modyfikator
# ---------------------------------------------------------------------------

def find_mod(plane, create=False):
    for m in plane.modifiers:
        if m.type == 'SIMPLE_DEFORM' and m.deform_method == 'BEND':
            return m
    if create:
        m = plane.modifiers.new("SimpleDeform", 'SIMPLE_DEFORM')
        m.deform_method = 'BEND'
        m.deform_axis = 'Z'
        m.angle = math.tau
        m.show_viewport = m.show_render = False
        return m
    return None


def set_mod_visible(mod, vis):
    mod.show_viewport = vis
    mod.show_render = vis


# ---------------------------------------------------------------------------
# Matematyka Simple Deform / Bend (identyczna z kodem Blendera)
#   a = oś deformacji, l = oś limitów (wzdłuż niej idzie obwód), h = "wysokość"
#   theta = f * clamp(u),  R = 1/f
#   l' = (R - v) sin(theta) + d cos(theta)
#   h' = R - (R - v) cos(theta) + d sin(theta)      (d = to, co wystaje poza limity)
# ---------------------------------------------------------------------------

def compute_params(plane, mod):
    a = AXES[mod.deform_axis]
    l = 0 if a == 2 else 2
    h = 3 - a - l
    origin = mod.origin if (mod.origin and mod.origin != plane) else None
    D = (origin.matrix_world if origin else plane.matrix_world).copy()
    M = D.inverted() @ plane.matrix_world
    verts = plane.data.vertices
    if len(verts) == 0:
        return None
    vals = [(M @ v.co)[l] for v in verts]
    lo, hi = min(vals), max(vals)
    k0, k1 = mod.limits
    k0 = min(k0, k1)
    lim0, lim1 = lo + (hi - lo) * k0, lo + (hi - lo) * k1
    f = mod.angle / max(1e-7, lim1 - lim0)
    return {"a": a, "l": l, "h": h, "f": f, "lim0": lim0, "lim1": lim1,
            "D": [D[r][c] for r in range(4) for c in range(4)]}


def _D(P):
    d = P["D"]
    return Matrix([d[0:4], d[4:8], d[8:12], d[12:16]])


def _rot(P, th):
    l, h = P["l"], P["h"]
    c, s = math.cos(th), math.sin(th)
    m = Matrix.Identity(4)
    m[l][l], m[h][l] = c, s
    m[l][h], m[h][h] = -s, c
    return m


def fwd_point(p, P):
    f = P["f"]
    if abs(f) < 1e-6:
        return p.copy(), 0.0
    l, h = P["l"], P["h"]
    R = 1.0 / f
    u = p[l]
    uc = min(max(u, P["lim0"]), P["lim1"])
    d = u - uc
    th = f * uc
    s, c = math.sin(th), math.cos(th)
    v = p[h]
    q = p.copy()
    q[l] = (R - v) * s + d * c
    q[h] = R - (R - v) * c + d * s
    return q, th


def inv_point(q, P, hint=None):
    f = P["f"]
    if abs(f) < 1e-6:
        return q.copy(), 0.0
    l, h = P["l"], P["h"]
    R = 1.0 / f
    sg = 1.0 if R > 0 else -1.0
    dl, dh = q[l], R - q[h]
    th = math.atan2(sg * dl, sg * dh)
    t0, t1 = sorted((f * P["lim0"], f * P["lim1"]))
    ref = 0.5 * (t0 + t1) if hint is None else hint
    best, best_key = th, None
    for k in range(-3, 4):
        t = th + math.tau * k
        out = 0.0 if t0 - 1e-5 <= t <= t1 + 1e-5 else min(abs(t - t0), abs(t - t1))
        key = (round(out, 6), abs(t - ref))
        if best_key is None or key < best_key:
            best, best_key = t, key
    tc = min(max(best, t0), t1)
    s, c = math.sin(tc), math.cos(tc)
    rv = dl * s + dh * c          # = R - v
    d = dl * c - dh * s
    p = q.copy()
    p[h] = R - rv
    p[l] = tc / f + d
    return p, tc


def fwd_matrix(L, P):
    p = L.to_translation()
    q, th = fwd_point(p, P)
    return Matrix.Translation(q) @ _rot(P, th) @ Matrix.Translation(-p) @ L, th


def inv_matrix(L, P, hint=None):
    q = L.to_translation()
    p, th = inv_point(q, P, hint)
    return Matrix.Translation(p) @ _rot(P, -th) @ Matrix.Translation(-q) @ L


# ---------------------------------------------------------------------------
# Zawijanie obiektów
# ---------------------------------------------------------------------------

def root_objects(plane, collection):
    objs = set(collection.all_objects) - {plane}
    return [o for o in objs if o.parent not in objs and o.parent != plane]


def wrapped_objects(plane):
    return [o for o in bpy.data.objects if o.get(FLAG) == plane.name]


def do_wrap(plane, collection):
    mod = find_mod(plane, create=True)
    P = compute_params(plane, mod)
    if P is None:
        return "Teren nie ma wierzchołków"
    plane[STATE] = P
    D = _D(P)
    Dinv = D.inverted()
    for o in root_objects(plane, collection):
        if o.get(FLAG):
            continue
        Lw, th = fwd_matrix(Dinv @ o.matrix_world, P)
        o.matrix_world = D @ Lw
        o[FLAG] = plane.name
        o[HINT] = th
    set_mod_visible(mod, True)
    return None


def do_unwrap(plane):
    mod = find_mod(plane)
    if mod:
        set_mod_visible(mod, False)
    st = plane.get(STATE)
    if st is None:
        return
    P = st.to_dict() if hasattr(st, "to_dict") else dict(st)
    D = _D(P)
    Dinv = D.inverted()
    for o in wrapped_objects(plane):
        o.matrix_world = D @ inv_matrix(Dinv @ o.matrix_world, P, o.get(HINT))
        del o[FLAG]
        if HINT in o:
            del o[HINT]
    del plane[STATE]


def rewrap(st):
    if not (st.wrapped and st.plane and st.collection):
        return None
    do_unwrap(st.plane)
    return do_wrap(st.plane, st.collection)


def _params_changed(old, new):
    for k in ("a", "l", "h"):
        if int(old[k]) != new[k]:
            return True
    if abs(old["f"] - new["f"]) > 1e-7 * max(1.0, abs(new["f"])):
        return True
    for k in ("lim0", "lim1"):
        if abs(old[k] - new[k]) > 1e-6:
            return True
    return any(abs(x - y) > 1e-6 for x, y in zip(old["D"], new["D"]))


# ---------------------------------------------------------------------------
# Synchronizacja z modyfikatorem (oko w panelu modyfikatorów, zmiana kąta, osi,
# limitów, origin, przesunięcie terenu) – działa automatycznie
# ---------------------------------------------------------------------------

@persistent
def _on_depsgraph(scene, depsgraph=None):
    global _busy
    if _busy:
        return
    st = getattr(scene, "cyl_wrap", None)
    if st is None or not (st.plane and st.collection):
        return
    plane = st.plane
    if plane.mode == 'EDIT':
        return
    mod = find_mod(plane)
    if mod is None:
        return
    if mod.show_viewport != st.wrapped:
        st.wrapped = mod.show_viewport          # wywoła wrap/unwrap
        return
    if st.wrapped and plane.get(STATE) is not None:
        new = compute_params(plane, mod)
        if new and _params_changed(plane[STATE].to_dict(), new):
            _busy = True
            try:
                rewrap(st)
            finally:
                _busy = False


# ---------------------------------------------------------------------------
# Właściwości
# ---------------------------------------------------------------------------

def _upd_wrapped(self, context):
    global _busy
    if _busy:
        return
    _busy = True
    try:
        if not (self.plane and self.collection):
            self.wrapped = False
            return
        if self.wrapped:
            err = do_wrap(self.plane, self.collection)
            if err:
                print("[Cylinder Wrap]", err)
                self.wrapped = False
        else:
            do_unwrap(self.plane)
    finally:
        _busy = False


def _upd_plane(self, context):
    # Przy wyborze terenu startujemy z płaskiego stanu; obiektów nie ruszamy.
    global _busy
    if _busy or not self.plane:
        return
    _busy = True
    try:
        mod = find_mod(self.plane)
        if mod and self.plane.get(STATE) is None:
            set_mod_visible(mod, False)
        self.wrapped = bool(mod and mod.show_viewport and self.plane.get(STATE) is not None)
    finally:
        _busy = False


class CylWrapSettings(bpy.types.PropertyGroup):
    plane: bpy.props.PointerProperty(
        name="Teren", type=bpy.types.Object,
        poll=lambda self, o: o.type == 'MESH', update=_upd_plane)
    collection: bpy.props.PointerProperty(name="Obiekty", type=bpy.types.Collection)
    wrapped: bpy.props.BoolProperty(name="Zwinięty", default=False, update=_upd_wrapped)


# ---------------------------------------------------------------------------
# Operatory / UI
# ---------------------------------------------------------------------------

class CYLWRAP_OT_refresh(bpy.types.Operator):
    """Zawiń obiekty dodane do kolekcji (traktowane jako ustawione na płaskim terenie)"""
    bl_idname = "cylwrap.refresh"
    bl_label = "Zawiń nowe obiekty"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        global _busy
        _busy = True
        try:
            err = rewrap(context.scene.cyl_wrap)
        finally:
            _busy = False
        if err:
            self.report({'ERROR'}, err)
            return {'CANCELLED'}
        return {'FINISHED'}


class CYLWRAP_OT_adopt(bpy.types.Operator):
    """Nowe obiekty kolekcji stoją już na zwiniętym terenie – tylko je oznacz, nie przesuwaj"""
    bl_idname = "cylwrap.adopt"
    bl_label = "Przyjmij nowe jako zawinięte"
    bl_options = {'REGISTER', 'UNDO'}

    def execute(self, context):
        st = context.scene.cyl_wrap
        if not (st.wrapped and st.plane and st.collection):
            return {'CANCELLED'}
        n = 0
        for o in root_objects(st.plane, st.collection):
            if not o.get(FLAG):
                o[FLAG] = st.plane.name
                n += 1
        self.report({'INFO'}, f"Oznaczono {n} obiektów")
        return {'FINISHED'}


class CYLWRAP_PT_panel(bpy.types.Panel):
    bl_label = "Cylinder Wrap"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Cyl Wrap"

    def draw(self, context):
        st = context.scene.cyl_wrap
        lay = self.layout

        col = lay.column()
        col.enabled = not st.wrapped
        col.prop(st, "plane")
        col.prop(st, "collection")

        row = lay.row()
        row.scale_y = 1.6
        row.enabled = bool(st.plane and st.collection)
        row.prop(st, "wrapped")

        if not st.plane:
            return
        mod = find_mod(st.plane)
        box = lay.box()
        if mod is None:
            box.label(text="Brak Simple Deform (Bend) – zostanie dodany", icon='INFO')
            return
        box.label(text=f"Modyfikator: {mod.name}", icon='MOD_SIMPLEDEFORM')
        box.prop(mod, "angle")
        box.row().prop(mod, "deform_axis", expand=True)
        box.prop(mod, "origin")
        box.prop(mod, "limits", slider=True)
        P = compute_params(st.plane, mod)
        if P and abs(P["f"]) > 1e-6:
            sx = st.plane.matrix_world.to_scale()
            box.label(text=f"Promień: {abs(1.0 / P['f']):.3f} (lokalnie)")
            if max(sx) - min(sx) > 1e-4 * max(sx):
                box.label(text="Niejednolita skala terenu – zastosuj ją", icon='ERROR')

        if st.wrapped:
            lay.operator("cylwrap.refresh", icon='FILE_REFRESH')
            lay.operator("cylwrap.adopt", icon='CHECKMARK')


classes = (CylWrapSettings, CYLWRAP_OT_refresh, CYLWRAP_OT_adopt, CYLWRAP_PT_panel)


def register():
    for c in classes:
        bpy.utils.register_class(c)
    bpy.types.Scene.cyl_wrap = bpy.props.PointerProperty(type=CylWrapSettings)
    if _on_depsgraph not in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.append(_on_depsgraph)


def unregister():
    if _on_depsgraph in bpy.app.handlers.depsgraph_update_post:
        bpy.app.handlers.depsgraph_update_post.remove(_on_depsgraph)
    del bpy.types.Scene.cyl_wrap
    for c in reversed(classes):
        bpy.utils.unregister_class(c)


if __name__ == "__main__":
    register()
