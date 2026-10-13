"""Builds the detailed Wasteland home-base set: HQ, barracks, wall segment, gate, corner tower and clutter props.

Run headless:
  blender -b --python tools/blender/wasteland_base.py -- <models_out_dir> <preview_png>
"""
import bpy, math, random, sys, os
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import wl_kit as K
from wl_kit import M, box, cyl, rod, put, join, torus
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
OUT, PREVIEW = argv[0], argv[1]
TEX = os.path.join(os.path.dirname(PREVIEW), 'waste_tex')
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)

K.reset(); M.build()
GLOW = None

def finish(name, parts, size, normal=True):
    """Bake everything but the lights into one textured mesh; lights stay a separate glowing mesh."""
    glow_mats = {M.lamp.name, M.red.name, M.cyan.name}
    lights = [o for o in parts if o.data.materials and o.data.materials[0].name in glow_mats]
    solid = [o for o in parts if o not in lights]
    body = K.bake(join(solid, name), size, TEX, normal)
    objs = [body]
    if lights: objs.append(join(lights, name + '_lights'))
    return objs

def hq():
    random.seed(4); rng = random.Random(4)
    P = []
    P.append(box((2.6, 2.2, 1.0), (0, 0, 0.5), M.concrete, bevel=0.03))
    P.append(box((2.72, 2.32, 0.08), (0, 0, 1.04), M.conc_dark, bevel=0.02))
    P.append(box((1.7, 1.5, 0.78), (-0.3, 0.25, 1.47), M.concrete, bevel=0.03))
    P.append(box((1.82, 1.62, 0.07), (-0.3, 0.25, 1.89), M.conc_dark, bevel=0.015))
    P.append(box((2.72, 0.07, 0.12), (0, -1.16 + 0.035, 1.14), M.hazard, bevel=0.01))  # hazard parapet over the door
    # front: armoured door between two armour-plated wings, windows above
    P += put(K.door(0.52, 0.78), (0, -1.1, 0))
    P += put(K.armour_plates(0.8, 0.85, rng), (-0.85, -1.1, 0.05))
    P += put(K.armour_plates(0.8, 0.85, rng), (0.85, -1.1, 0.05))
    for x in (-0.75, 0.15):
        P += put(K.window(0.34, 0.3), (x, -0.5, 1.5))
    # sides: barred windows over a plated skirt
    for rz, at in ((math.pi / 2, (1.3, 0, 0)), (-math.pi / 2, (-1.3, 0, 0))):
        P += put(K.armour_plates(2.0, 0.42, rng), at, rz)
        for y in (-0.5, 0.45):
            w = K.window(0.3, 0.28, 3)
            P += put(w, (at[0], y if rz > 0 else -y, 0.68), rz)
    P += put(K.window(0.3, 0.28), (0.55, 0.0, 1.5), math.pi / 2)
    # back: plates and a service door
    P += put(K.armour_plates(2.4, 0.7, rng), (0, 1.1, 0.05), math.pi)
    P += put(K.door(0.38, 0.62, hazard=False), (-0.7, 1.1, 0), math.pi)
    # pipes running down the right side, a ladder up to the top floor
    P += K.pipe_run([(1.33, -0.75, 1.0), (1.33, -0.75, 0.35), (1.33, -0.95, 0.35), (1.33, -0.95, 0.05)])
    P += K.pipe_run([(1.33, 0.8, 1.0), (1.33, 0.8, 0.08)], r=0.02)
    P += put(K.ladder(0.82), (0.56, 0.55, 1.08), math.pi / 2)
    # lower roof: AC units, a sandbagged gun nest, floodlights
    P += put(K.ac_unit(), (0.85, 0.75, 1.08)); P += put(K.ac_unit(), (0.85, 0.3, 1.08), 0.1)
    nest = []
    for rz, at in ((0, (0.95, -0.95, 1.08)), (math.pi / 2, (1.2, -0.7, 1.08))):
        nest += put(K.sandbag_wall(0.55, 3), at, rz)
    P += nest
    P.append(cyl(0.05, 0.12, (0.85, -0.65, 1.14), M.steel, verts=10))
    P.append(rod((0.85, -0.65, 1.22), (0.85, -1.05, 1.27), 0.02, M.steel))
    P.append(box((0.12, 0.2, 0.08), (0.85, -0.7, 1.22), M.armour, bevel=0.01))
    for at in ((-1.25, -1.05, 1.08), (1.25, 1.05, 1.08)):
        P += put(K.floodlight(0.35), at, 0.6 if at[0] < 0 else 3.6)
    # roof edges: a low parapet lip; clutter on both roofs
    for (sx, sy, x, y, z) in ((2.72, 0.06, 0, 1.13, 1.11), (0.06, 2.32, -1.33, 0, 1.11), (0.06, 2.32, 1.33, 0, 1.11),
                              (1.82, 0.05, -0.3, 1.04, 1.95), (0.05, 1.62, -1.19, 0.25, 1.95), (0.05, 1.62, 0.59, 0.25, 1.95)):
        P.append(box((sx, sy, 0.07), (x, y, z), M.conc_dark, bevel=0.008))
    for at in ((-1.1, -0.85, 1.08), (-0.95, -0.85, 1.08), (1.15, 0.0, 1.08)):
        P.append(box((0.12, 0.12, 0.1), (at[0], at[1], at[2] + 0.05), M.tin, bevel=0.01))  # roof vents
    P.append(box((0.5, 0.32, 0.03), (-0.35, -0.85, 1.22), M.glass, rot=(0.5, 0, 0), bevel=0.005))  # salvaged solar panel
    P.append(box((0.52, 0.34, 0.02), (-0.35, -0.84, 1.205), M.steel, rot=(0.5, 0, 0), bevel=0.004))
    P += [rod((-0.55, -0.92, 1.08), (-0.55, -0.92, 1.12), 0.012, M.steel), rod((-0.15, -0.92, 1.08), (-0.15, -0.92, 1.12), 0.012, M.steel)]
    P += K.pipe_run([(-1.0, 0.95, 1.95), (-1.0, 0.95, 2.1), (-0.6, 0.95, 2.1)], r=0.02)
    P.append(box((0.22, 0.16, 0.12), (-0.05, 0.85, 1.99), M.tin, bevel=0.01))
    for k in range(3):
        P.append(box((0.08, 0.3, 0.02), (-1.0 + k * 0.1, -0.2, 1.935), M.wood, rot=(0, 0, 0.2 * k), bevel=0.003))  # loose planks
    # top floor: water tank, antenna, railing
    P += put(K.water_tank(0.2, 0.3, 0.22), (-0.75, 0.6, 1.93))
    P += put(K.antenna(0.9), (0.25, 0.75, 1.93))
    P += put(K.railing(1.6, 0.18), (-0.3, -0.53, 1.93))
    # out front: sandbags either side of the door, crates and barrels
    P += put(K.sandbag_wall(0.7, 3), (-0.75, -1.45, 0)); P += put(K.sandbag_wall(0.7, 3), (0.75, -1.45, 0))
    P += put(K.crate(0.22), (1.1, -1.35, 0), 0.3); P += put(K.crate(0.18), (1.12, -1.32, 0.22), 0.8)
    P += put(K.barrel(), (-1.15, -1.32, 0)); P += put(K.barrel(), (-1.0, -1.38, 0)); P += put(K.barrel(mat=M.yellow), (-1.08, -1.22, 0))
    return finish('base_hq', P, 2048)

def half_pipe(r, length, mat):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=length, vertices=28, location=(0, 0, 0), rotation=(math.pi / 2, 0, 0), end_fill_type='NOTHING')
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)  # so 'below the middle' means below in the world
    import bmesh
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.001], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    o.data.materials.append(mat)
    s = o.modifiers.new('thick', 'SOLIDIFY'); s.thickness = 0.04
    return o

def barracks():
    random.seed(7); rng = random.Random(7)
    P = [box((1.7, 1.95, 0.06), (0, 0, 0.03), M.conc_dark, bevel=0.01)]
    hut = half_pipe(0.72, 1.7, M.tin); hut.location.z = 0.06; P.append(hut)
    for y in (-0.85, 0.85):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.7, depth=0.05, vertices=28, location=(0, y, 0.06), rotation=(math.pi / 2, 0, 0))
        e = bpy.context.active_object
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        import bmesh
        bm = bmesh.new(); bm.from_mesh(e.data)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0), plane_no=(0, 0, -1), clear_outer=True)
        bmesh.ops.holes_fill(bm, edges=bm.edges[:])
        bm.to_mesh(e.data); bm.free(); e.data.materials.append(M.scrap); P.append(e)
    P += put(K.door(0.34, 0.52), (0, -0.88, 0.06))
    P += put(K.window(0.16, 0.14, 2), (0.36, -0.88, 0.36)); P += put(K.window(0.16, 0.14, 2), (-0.36, -0.88, 0.36))
    for rz, x in ((math.pi / 2, 0.85), (-math.pi / 2, -0.85)):
        P += put(K.sandbag_wall(1.5, 2), (x, 0, 0.06), rz)
    P += put(K.crate(0.2), (0.62, -1.05, 0.0), 0.4); P += put(K.barrel(), (-0.6, -1.05, 0))
    P += put(K.floodlight(0.5), (-0.7, 0.95, 0.06), 2.8)
    P.append(cyl(0.03, 0.35, (0.4, 0.4, 0.75), M.steel, verts=8))  # stove pipe
    return finish('base_barracks', P, 1024)

def wall_seg():
    random.seed(13)
    P = [box((0.38, 1.0, 0.3), (0, 0, 0.15), M.concrete, bevel=0.03), box((0.24, 1.0, 0.2), (0, 0, 0.4), M.concrete, bevel=0.025)]
    P.append(box((0.39, 1.0, 0.06), (0, 0, 0.08), M.hazard, bevel=0.008))
    for k in range(3):
        h = random.uniform(0.18, 0.36)
        P.append(box((0.035, 0.35, h), (random.uniform(-0.03, 0.03), -0.34 + k * 0.34, 0.5 + h / 2), random.choice([M.scrap, M.tin]), rot=(random.uniform(-0.08, 0.08), 0, 0), bevel=0.005))
    for y in (-0.48, 0.0):
        P.append(cyl(0.012, 0.42, (0, y, 0.71), M.steel, verts=6, bevel=0))
    for z in (0.76, 0.86):
        P.append(rod((0, -0.5, z), (0, 0.5, z), 0.005, M.steel, verts=5))
    return finish('base_wall', P, 512)

def gate():
    random.seed(17)
    P = []
    for y in (-0.47, 0.47):
        P.append(box((0.42, 0.14, 0.95), (0, y, 0.475), M.concrete, bevel=0.025))
        P += put(K.floodlight(0.2), (0, y, 0.95), 0)
    P.append(box((0.07, 0.8, 0.6), (0, 0, 0.36), M.hazard, bevel=0.01))
    P.append(box((0.1, 1.08, 0.08), (0, 0, 0.98), M.steel, bevel=0.01))
    for k in range(5): P.append(cyl(0.01, 0.56, (0.045, -0.32 + k * 0.16, 0.36), M.steel, verts=6, bevel=0))
    return finish('base_gate', P, 512)

def tower():
    random.seed(21)
    P = []
    legs = [(-0.3, -0.3), (0.3, -0.3), (0.3, 0.3), (-0.3, 0.3)]
    for x, y in legs: P.append(cyl(0.025, 1.25, (x, y, 0.625), M.steel, verts=8, bevel=0))
    for i in range(4):
        a, b = legs[i], legs[(i + 1) % 4]
        P.append(rod((a[0], a[1], 0.1), (b[0], b[1], 1.1), 0.012, M.steel)); P.append(rod((b[0], b[1], 0.1), (a[0], a[1], 1.1), 0.012, M.steel))
    P.append(box((0.86, 0.86, 0.06), (0, 0, 1.25), M.wood, bevel=0.01))
    for rz, at in ((0, (0, -0.38, 1.28)), (math.pi / 2, (0.38, 0, 1.28)), (math.pi, (0, 0.38, 1.28)), (-math.pi / 2, (-0.38, 0, 1.28))):
        P += put(K.sandbag_wall(0.72, 2), at, rz)
    for x, y in legs: P.append(cyl(0.015, 0.45, (x, y, 1.5), M.steel, verts=6, bevel=0))
    P.append(box((0.95, 0.95, 0.04), (0, 0, 1.75), M.tin, rot=(0.12, 0, 0), bevel=0.01))
    P += put(K.floodlight(0.25), (0.25, -0.25, 1.28), 0.4)
    P += put(K.ladder(1.2, 0.16), (0, 0.32, 0.0), math.pi)
    return finish('base_tower', P, 1024)

def props():
    out = {}
    random.seed(31)
    P = put(K.crate(0.24), (0, 0, 0), 0.2) + put(K.crate(0.22), (0.25, 0.05, 0), -0.3) + put(K.crate(0.2), (0.1, 0.02, 0.24), 0.6)
    out['prop-crates'] = finish('prop_crates', P, 512)
    P = put(K.barrel(), (0, 0, 0)) + put(K.barrel(mat=M.yellow), (0.16, 0.04, 0)) + put(K.barrel(), (0.07, 0.15, 0))
    tip = K.barrel(); put(tip, (0, 0, 0)); [setattr(o, 'rotation_euler', (o.rotation_euler.x + math.pi / 2, 0, 0)) for o in tip]; put(tip, (-0.2, -0.05, 0.07), 0.7)
    out['prop-barrels'] = finish('prop_barrels', P + tip, 512)
    P = put(K.tyre(), (0, 0, 0)) + put(K.tyre(), (0.03, 0.01, 0.07)) + put(K.tyre(), (0.2, 0.1, 0))
    out['prop-tyres'] = finish('prop_tyres', P, 256, normal=False)
    P = []
    for k in range(12):
        s = random.uniform(0.05, 0.16)
        P.append(box((s * 1.4, s, s * 0.6), (random.gauss(0, 0.15), random.gauss(0, 0.15), s * 0.3 + random.uniform(0, 0.08)), random.choice([M.concrete, M.scrap, M.tin, M.brick]), rot=(random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.5), random.uniform(0, 3)), bevel=0.01))
    P += put(K.tyre(0.08), (0.2, -0.1, 0.02))
    P.append(box((0.35, 0.03, 0.25), (-0.05, 0.12, 0.12), M.scrap, rot=(0.9, 0.2, 0.4), bevel=0.005))
    out['prop-junk'] = finish('prop_junk', P, 512)
    return out

assets = {'base-hq': hq(), 'base-barracks': barracks(), 'base-wall': wall_seg(), 'base-gate': gate(), 'base-tower': tower()}
assets.update(props())
for name, objs in assets.items():
    K.export(objs, os.path.join(OUT, name + '.glb'))

# ---------- preview: a corner of a base ----------
def move(objs, x, y, rz=0):
    put(objs, (x, y, 0), rz)
move(assets['base-hq'], 0.4, 0.6)
move(assets['base-barracks'], -2.3, 0.9, math.pi / 2)
for k in range(-3, 4):
    if k == 0:
        continue
for k, x in enumerate([-3.0, -2.0, -1.0, 1.0, 2.0, 3.0]):
    pass
# walls along the front, gate in the middle, towers at the ends (wall pieces run along Y, so turn them)
import copy
def dup(objs):
    out = []
    for o in objs:
        c = o.copy(); c.data = o.data; bpy.context.scene.collection.objects.link(c); out.append(c)
    return out
for x in (-3.0, -2.0, -1.0, 1.0, 2.0):
    move(dup(assets['base-wall']), x + 0.5, -1.9, math.pi / 2)
move(assets['base-gate'], 0.0 + 0.5 - 0.5, -1.9, math.pi / 2)
for o in assets['base-wall']: o.hide_render = True
move(assets['base-tower'], -3.6, -1.9)
move(dup(assets['base-tower']), 3.0, -1.9)
move(assets['prop-crates'], 1.9, -0.9, 0.4); move(assets['prop-barrels'], -1.3, -0.9); move(assets['prop-tyres'], 2.6, 0.6); move(assets['prop-junk'], -0.8, -2.6)

gm = K.worn('ground', (0.33, 0.33, 0.335), rough=0.95, scale=3, wear=0.0, grime=0.7, cracks=True)
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(gm)
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(42), math.radians(8), math.radians(-55)))
sun = bpy.context.active_object.data; sun.energy = 2.6; sun.angle = math.radians(1.0); sun.color = (1.0, 0.95, 0.86)
sc = bpy.context.scene
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.3, 0.37, 0.46, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.75
cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 7.0
cam = bpy.data.objects.new('cam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
rot = Euler((math.radians(52), 0, math.radians(32))); cam.rotation_euler = rot
cam.location = Vector((0.0, -0.4, 0.4)) - (rot.to_matrix() @ Vector((0, 0, -1))) * 25
sc.cycles.samples = 64; sc.cycles.use_denoising = True
sc.render.resolution_x = 1400; sc.render.resolution_y = 1000
sc.view_settings.view_transform = 'Standard'
sc.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print('preview', PREVIEW)
