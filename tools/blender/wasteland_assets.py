"""Builds the Wasteland test assets in Blender and exports them for the game.

Run headless:
  blender -b --python tools/blender/wasteland_assets.py -- <models_out_dir> <preview_png>

Each asset is modelled from primitives with bevelled edges, given procedural materials
(colour variation, edge wear, rust, grime in the crevices), then the look is baked into one
texture per asset so the game shows it without doing any heavy lighting itself.
"""
import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
OUT, PREVIEW = argv[0], argv[1]
TEX = os.path.join(os.path.dirname(PREVIEW), 'waste_tex')
os.makedirs(OUT, exist_ok=True)
os.makedirs(TEX, exist_ok=True)
random.seed(11)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'

# ---------- materials ----------

def _mix(nt, a, b, fac, blend='MIX'):
    m = nt.nodes.new('ShaderNodeMix'); m.data_type = 'RGBA'; m.blend_type = blend
    if isinstance(fac, float): m.inputs[0].default_value = fac
    else: nt.links.new(fac, m.inputs[0])
    for idx, v in ((6, a), (7, b)):
        if isinstance(v, tuple): m.inputs[idx].default_value = (*v, 1)
        else: nt.links.new(v, m.inputs[idx])
    return m.outputs[2]

def _ramp(nt, src, stops):
    r = nt.nodes.new('ShaderNodeValToRGB'); nt.links.new(src, r.inputs['Fac'])
    els = r.color_ramp.elements
    while len(els) < len(stops): els.new(0.5)
    for el, (pos, col) in zip(els, stops):
        el.position = pos; el.color = (*col, 1) if len(col) == 3 else col
    return r.outputs['Color']

def _noise(nt, scale, detail=8):
    tc = nt.nodes.new('ShaderNodeTexCoord')
    n = nt.nodes.new('ShaderNodeTexNoise'); n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
    nt.links.new(tc.outputs['Object'], n.inputs['Vector'])
    return n.outputs['Fac']

def worn(name, base, rough=0.85, metal=0.0, rust=None, char=0.0, wear=0.55, grime=0.45, scale=7, bricks=False):
    """Base colour with mottling, lighter worn edges, optional rust and soot, and dirt in the crevices."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = rough; bsdf.inputs['Metallic'].default_value = metal
    dark = tuple(c * 0.6 for c in base); light = tuple(min(1, c * 1.22) for c in base)
    col = _ramp(nt, _noise(nt, scale), [(0.32, dark), (0.68, light)])
    stains = _ramp(nt, _noise(nt, 1.6, 3), [(0.45, (0.62, 0.6, 0.57)), (0.62, (1, 1, 1))])
    col = _mix(nt, col, stains, 1.0, 'MULTIPLY')
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(tc.outputs['Object'], sep.inputs['Vector'])
    if bricks:  # courses that run along a wall whichever way it faces: u = x + y, v = height
        add = nt.nodes.new('ShaderNodeMath'); nt.links.new(sep.outputs['X'], add.inputs[0]); nt.links.new(sep.outputs['Y'], add.inputs[1])
        uv = nt.nodes.new('ShaderNodeCombineXYZ'); nt.links.new(add.outputs[0], uv.inputs['X']); nt.links.new(sep.outputs['Z'], uv.inputs['Y'])
        bt = nt.nodes.new('ShaderNodeTexBrick'); nt.links.new(uv.outputs['Vector'], bt.inputs['Vector'])
        bt.inputs['Scale'].default_value = 7; bt.inputs['Mortar Size'].default_value = 0.012
        bt.inputs['Color1'].default_value = (1, 1, 1, 1); bt.inputs['Color2'].default_value = (0.78, 0.74, 0.72, 1); bt.inputs['Mortar'].default_value = (0.5, 0.48, 0.45, 1)
        col = _mix(nt, col, bt.outputs['Color'], 1.0, 'MULTIPLY')
    dirt = _ramp(nt, sep.outputs['Z'], [(0.0, (0.5, 0.47, 0.43)), (0.35, (1, 1, 1))])  # dirt splashed up the bottom of everything
    col = _mix(nt, col, dirt, 1.0, 'MULTIPLY')
    if rust:
        mask = _ramp(nt, _noise(nt, 2.2, 4), [(0.5, (0, 0, 0)), (0.62, (1, 1, 1))])
        col = _mix(nt, col, rust, mask)
    if char > 0:
        soot = _ramp(nt, _noise(nt, 3.5, 6), [(0.42, (0, 0, 0)), (0.6, (1, 1, 1))])
        col = _mix(nt, col, (0.05, 0.045, 0.04), soot)
        bsdf_char = nt.nodes.new('ShaderNodeMath')  # keep the node tree readable in the UI
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    edge = _ramp(nt, geo.outputs['Pointiness'], [(0.5, (0, 0, 0)), (0.56, (wear, wear, wear))])
    col = _mix(nt, col, tuple(min(1, c * 1.45 + 0.08) for c in base), edge)
    ao = nt.nodes.new('ShaderNodeAmbientOcclusion'); ao.samples = 16; ao.inputs['Distance'].default_value = 0.5
    shade = _ramp(nt, ao.outputs['AO'], [(0.0, (grime, grime * 0.95, grime * 0.9)), (1.0, (1, 1, 1))])
    col = _mix(nt, col, shade, 1.0, 'MULTIPLY')
    nt.links.new(col, bsdf.inputs['Base Color'])
    m['rough'] = rough; m['metal'] = metal
    return m

def glow(name, color, strength=6):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*color, 1)
    b.inputs['Emission Color'].default_value = (*color, 1); b.inputs['Emission Strength'].default_value = strength
    return m

CONCRETE = worn('concrete', (0.36, 0.355, 0.34), scale=9)
CONC_DARK = worn('concrete_dark', (0.25, 0.25, 0.255), scale=9)
BRICK = worn('brick', (0.36, 0.2, 0.14), scale=14, wear=0.3, bricks=True)
REBAR = worn('rebar', (0.28, 0.17, 0.11), rough=0.7, metal=0.4, wear=0.2)
RUST = worn('rusted', (0.36, 0.22, 0.13), rough=0.8, metal=0.3, rust=(0.5, 0.26, 0.12), char=0.6, wear=0.4)
PAINT = worn('old_paint', (0.27, 0.25, 0.21), rough=0.75, metal=0.2, rust=(0.4, 0.2, 0.1), char=0.6)
CHAR = worn('charred', (0.08, 0.075, 0.07), rough=0.9, wear=0.25)
TYRE = worn('tyre', (0.07, 0.07, 0.075), rough=0.95, wear=0.15)
GUNMETAL = worn('gunmetal', (0.17, 0.18, 0.2), rough=0.45, metal=0.7, wear=0.75, scale=5)
GUN_PLATE = worn('gun_plate', (0.24, 0.25, 0.28), rough=0.5, metal=0.6, wear=0.8, scale=5)
RED = glow('red_eye', (1.0, 0.04, 0.02), 2.5)
CYAN = glow('cyan_screen', (0.1, 0.65, 1.0), 2.0)

# ---------- modelling helpers ----------

def box(size, loc, mat, rot=(0, 0, 0), bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object; o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    if bevel:
        mod = o.modifiers.new('bevel', 'BEVEL'); mod.width = bevel; mod.segments = 2; mod.limit_method = 'ANGLE'
    return o

def cyl(r, depth, loc, mat, rot=(0, 0, 0), verts=16, bevel=0.01):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, location=loc, rotation=rot, vertices=verts)
    o = bpy.context.active_object; o.data.materials.append(mat)
    if bevel:
        mod = o.modifiers.new('bevel', 'BEVEL'); mod.width = bevel; mod.segments = 2; mod.limit_method = 'ANGLE'
    return o

def join(objs, name):
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:  # bake the modifiers into the meshes
        if o.modifiers:
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
            o.modifiers.clear(); o.data = me
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.active_object; o.name = name
    return o

def bake(o, size):
    """Bakes everything the materials show (mottling, wear, rust, crevice dirt) into one texture."""
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new(o.name + '_tex', size, size)
    mats = [s.material for s in o.material_slots]
    for m in mats:
        n = m.node_tree.nodes.new('ShaderNodeTexImage'); n.image = img
        m.node_tree.nodes.active = n
    scene.cycles.samples = 32
    b = scene.render.bake; b.use_pass_direct = False; b.use_pass_indirect = False; b.use_pass_color = True; b.margin = 6
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})
    img.filepath_raw = os.path.join(TEX, o.name + '.png'); img.file_format = 'PNG'; img.save()
    rough = sum(m.get('rough', 0.85) for m in mats) / len(mats); metal = max(m.get('metal', 0) for m in mats) * 0.6
    nm = bpy.data.materials.new(o.name + '_baked'); nm.use_nodes = True
    pb = nm.node_tree.nodes['Principled BSDF']; t = nm.node_tree.nodes.new('ShaderNodeTexImage'); t.image = img
    nm.node_tree.links.new(t.outputs['Color'], pb.inputs['Base Color'])
    pb.inputs['Roughness'].default_value = rough; pb.inputs['Metallic'].default_value = metal
    o.data.materials.clear(); o.data.materials.append(nm)
    return o

def export(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    path = os.path.join(OUT, name + '.glb')
    try:
        bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_image_format='JPEG', export_apply=True)
    except TypeError:
        bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True)
    print('exported', path, os.path.getsize(path))

def move(objs, dx, dy, rz=0):
    for o in objs:
        o.location.x += dx; o.location.y += dy; o.rotation_euler.z += rz

# ---------- the assets ----------

def broken_wall(x0, y0, length, along_x, h_lo, h_hi, mat, thick=0.16, window=None):
    """One solid wall with a ragged, sloping broken top, built as a single prism; optional window holes cut out of it."""
    n = max(4, int(length / 0.14)); pts = []; h = random.uniform(h_lo, h_hi)
    for k in range(n + 1):
        h = min(h_hi, max(h_lo, h + random.uniform(-0.16, 0.16) - (0.45 if random.random() < 0.12 else 0)))
        pts.append((k * length / n, h))
    bm = bmesh.new()
    def V(u, z, t):
        x, y = (x0 + u, y0 + t) if along_x else (x0 + t, y0 + u)
        return bm.verts.new((x, y, 0.1 + z))
    front = [V(0, 0, -thick / 2)] + [V(u, z, -thick / 2) for u, z in pts] + [V(length, 0, -thick / 2)]
    back = [V(0, 0, thick / 2)] + [V(u, z, thick / 2) for u, z in pts] + [V(length, 0, thick / 2)]
    bm.faces.new(front); bm.faces.new(list(reversed(back)))
    for a in range(len(front)):
        b = (a + 1) % len(front); bm.faces.new((front[a], front[b], back[b], back[a]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    me = bpy.data.meshes.new('wall'); bm.to_mesh(me); bm.free()
    wall = bpy.data.objects.new('wall', me); scene.collection.objects.link(wall); wall.data.materials.append(mat)
    cutters = []
    if window:
        for u in window:
            top = min(z for uu, z in pts if abs(uu - u) < 0.25)
            if top < 1.05: continue
            sz = (0.3, thick * 3, 0.36) if along_x else (thick * 3, 0.3, 0.36)
            loc = (x0 + u, y0, 0.1 + 0.72) if along_x else (x0, y0 + u, 0.1 + 0.72)
            c = box(sz, loc, mat, bevel=0); cutters.append(c)
            mod = wall.modifiers.new('hole', 'BOOLEAN'); mod.operation = 'DIFFERENCE'; mod.object = c; mod.solver = 'EXACT'
    dg = bpy.context.evaluated_depsgraph_get()
    if cutters:
        wall.data = bpy.data.meshes.new_from_object(wall.evaluated_get(dg)); wall.modifiers.clear()
        for c in cutters: bpy.data.objects.remove(c)
    mod = wall.modifiers.new('bevel', 'BEVEL'); mod.width = 0.016; mod.segments = 2; mod.limit_method = 'ANGLE'
    parts = [wall]
    for u, z in pts:
        if random.random() < 0.18:  # rebar poking out of the broken top
            c = (x0 + u, y0) if along_x else (x0, y0 + u)
            parts.append(cyl(0.012, 0.25, (c[0], c[1], 0.1 + z + 0.08), REBAR, rot=(random.uniform(-0.35, 0.35), random.uniform(-0.35, 0.35), 0), verts=6, bevel=0))
    return parts

def chunks(n, cx, cy, spread, mats, smin=0.05, smax=0.22, z0=0.0, pile=0.25):
    out = []
    for _ in range(n):
        r = abs(random.gauss(0, spread)); a = random.uniform(0, 6.283)
        s = random.uniform(smin, smax)
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
        z = z0 + s / 2 + max(0, pile * (1 - r / (spread * 2.2))) * random.uniform(0.3, 1)
        out.append(box((s * random.uniform(0.7, 1.5), s * random.uniform(0.7, 1.3), s * random.uniform(0.4, 0.9)), (x, y, z), random.choice(mats),
                       rot=(random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.5), random.uniform(0, 3.14)), bevel=0.012))
    return out

def ruin(name, wall_mat, seed):
    random.seed(seed)
    parts = [box((2.0, 2.0, 0.12), (0, 0, 0.06), CONC_DARK, bevel=0.03)]
    parts += broken_wall(-1.0, 0.92, 2.0, True, 0.7, 1.75, wall_mat, window=(0.5, 1.4))
    parts += broken_wall(-0.92, -1.0, 1.85, False, 0.35, 1.35, wall_mat, window=(0.6,))
    parts += broken_wall(0.3, -0.92, 0.7, True, 0.15, 0.55, wall_mat)
    parts.append(box((0.9, 0.7, 0.08), (0.25, 0.1, 0.32), CONCRETE, rot=(0.35, -0.2, 0.4), bevel=0.02))  # a fallen floor slab
    parts += chunks(14, 0.3, 0.3, 0.35, [wall_mat, CONCRETE], z0=0.12, pile=0.2)
    o = join(parts, name)
    return [bake(o, 1024)]

def rubble(name):
    random.seed(23)
    o = join(chunks(34, 0, 0, 0.32, [CONCRETE, CONCRETE, CONC_DARK, BRICK], 0.06, 0.26, pile=0.35) + [cyl(0.012, 0.5, (0.1, -0.1, 0.32), REBAR, rot=(0.9, 0.3, 0), verts=6, bevel=0)], name)
    return [bake(o, 512)]

def car(name):
    random.seed(5)
    parts = [box((1.0, 0.48, 0.2), (0, 0, 0.2), PAINT, bevel=0.04)]
    cab = box((0.5, 0.44, 0.17), (-0.08, 0, 0.385), RUST, bevel=0.035)
    parts.append(cab)
    parts.append(box((0.46, 0.45, 0.12), (-0.08, 0, 0.39), CHAR, bevel=0.01))  # burnt-out window line
    parts.append(box((0.12, 0.5, 0.06), (0.52, 0, 0.16), RUST, bevel=0.015))    # bumper
    for i, (x, y) in enumerate(((0.32, 0.26), (0.32, -0.26), (-0.3, 0.26), (-0.3, -0.26))):
        if i == 3: continue  # one wheel gone, so it sags
        parts.append(cyl(0.11, 0.08, (x, y, 0.11), TYRE, rot=(math.pi / 2, 0, 0), verts=14, bevel=0.015))
    parts += chunks(6, 0.0, 0.0, 0.5, [RUST, CHAR], 0.03, 0.08)
    o = join(parts, name)
    o.rotation_euler = (0.06, -0.04, 0)  # resting on its missing wheel
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    return [bake(o, 512)]

def turret(name):
    random.seed(9)
    parts = [cyl(0.62, 0.22, (0, 0, 0.11), GUN_PLATE, verts=8, bevel=0.03)]
    parts.append(box((0.82, 0.82, 0.42), (0, 0, 0.43), GUNMETAL, bevel=0.05))
    parts.append(box((0.9, 0.9, 0.07), (0, 0, 0.67), GUN_PLATE, bevel=0.02))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(box((0.22, 0.22, 0.14), (sx * 0.24, sy * 0.24, 0.78), GUNMETAL, bevel=0.025))
    parts.append(cyl(0.11, 0.12, (0, 0, 0.77), GUN_PLATE, verts=12, bevel=0.01))
    for k in range(8):
        a = k / 8 * 6.283
        parts.append(box((0.08, 0.16, 0.12), (math.cos(a) * 0.6, math.sin(a) * 0.6, 0.16), GUN_PLATE, rot=(0, 0, a), bevel=0.01))
    body = bake(join(parts, name), 1024)
    lights = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            lights.append(box((0.13, 0.13, 0.03), (sx * 0.24, sy * 0.24, 0.86), RED, bevel=0.01))
    lights.append(box((0.12, 0.12, 0.04), (0, 0, 0.84), CYAN, bevel=0.01))
    for x, y, rz in ((0.415, 0, 0), (-0.415, 0, 0), (0, 0.415, math.pi / 2), (0, -0.415, math.pi / 2)):
        lights.append(box((0.02, 0.3, 0.16), (x, y, 0.45), CYAN, rot=(0, 0, rz), bevel=0.005))
    glowing = join(lights, name + '_lights')
    return [body, glowing]

assets = {
    'ruin-concrete': ruin('ruin_concrete', CONCRETE, 3),
    'ruin-brick': ruin('ruin_brick', BRICK, 8),
    'rubble': rubble('rubble'),
    'car-burnt': car('car_burnt'),
    'turret': turret('caretaker_turret'),
}
for name, objs in assets.items():
    export(objs, name)

# ---------- preview: the assets together, lit and framed like the reference ----------
move(assets['ruin-concrete'], -1.6, 1.4, 0.1)
move(assets['ruin-brick'], 2.0, 1.8, -0.3)
move(assets['rubble'], 0.4, -0.2)
move(assets['car-burnt'], -1.4, -1.4, 0.5)
move(assets['turret'], 1.9, -1.4)

gm = worn('ground', (0.33, 0.33, 0.335), rough=0.95, scale=3, wear=0.0, grime=0.7)
nt = gm.node_tree
vor = nt.nodes.new('ShaderNodeTexVoronoi'); vor.feature = 'DISTANCE_TO_EDGE'; vor.inputs['Scale'].default_value = 2.4; vor.inputs['Randomness'].default_value = 0.9
cracks = _ramp(nt, vor.outputs['Distance'], [(0.0, (0.12, 0.11, 0.1)), (0.012, (0.75, 0.74, 0.72)), (0.03, (1, 1, 1))])
bsdf = nt.nodes['Principled BSDF']
prev = bsdf.inputs['Base Color'].links[0].from_socket
nt.links.new(_mix(nt, prev, cracks, 1.0, 'MULTIPLY'), bsdf.inputs['Base Color'])
bpy.ops.mesh.primitive_plane_add(size=9, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(gm)

bpy.ops.object.light_add(type='SUN', rotation=(math.radians(42), math.radians(8), math.radians(-55)))
sun = bpy.context.active_object.data; sun.energy = 2.6; sun.angle = math.radians(1.0); sun.color = (1.0, 0.95, 0.86)
world = bpy.data.worlds.new('w'); scene.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.3, 0.37, 0.46, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.75

cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 7.2
cam = bpy.data.objects.new('cam', cam_data); scene.collection.objects.link(cam); scene.camera = cam
rot = Euler((math.radians(52), 0, math.radians(38)))
cam.rotation_euler = rot
view = rot.to_matrix() @ Vector((0, 0, -1))
cam.location = Vector((0.2, 0.1, 0)) - view * 20

scene.cycles.samples = 64; scene.cycles.use_denoising = True
scene.render.resolution_x = 1200; scene.render.resolution_y = 900
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print('preview', PREVIEW)
