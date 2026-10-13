"""Builds the rest of the Wasteland home-base buildings: warehouse, research lab, workshop (2x2 tiles),
generator, solar array, oil pump and crop plot (1x1). Same kit, materials and baking as wasteland_base.py.

Run headless:
  blender -b --python tools/blender/wasteland_buildings.py -- <models_out_dir> <preview_png>
"""
import bpy, bmesh, math, random, sys, os
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import wl_kit as K
from wl_kit import M, box, cyl, rod, put, torus
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
OUT, PREVIEW = argv[0], argv[1]
TEX = os.path.join(os.path.dirname(PREVIEW), 'waste_tex')
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)
K.reset(); M.build()

def finish(name, parts, size, normal=True):
    return K.finish(name, parts, size, TEX, normal)

def gable(w, d, h, z, mat, overhang=0.06):
    """A shallow two-sided roof along Y: two tilted sheets meeting at a ridge."""
    a = math.atan2(h, w / 2); L = math.hypot(w / 2, h) + overhang
    return [box((L, d + 2 * overhang, 0.035), (-w / 4, 0, z + h / 2), mat, rot=(0, -a, 0), bevel=0.006),
            box((L, d + 2 * overhang, 0.035), (w / 4, 0, z + h / 2), mat, rot=(0, a, 0), bevel=0.006)]

def warehouse():
    random.seed(41); rng = random.Random(41)
    P = [box((1.8, 1.8, 0.08), (0, 0, 0.04), M.conc_dark, bevel=0.015)]
    P.append(box((1.5, 1.55, 0.72), (0, 0.08, 0.44), M.tin, bevel=0.02))
    P += gable(1.56, 1.6, 0.22, 0.8, M.scrap)
    for x in (-0.78, 0.78):
        P.append(box((0.04, 1.6, 0.06), (x, 0.08, 0.81), M.steel, bevel=0.005))  # gutters
    # front: a big roller door in a hazard frame, a loading dock with pallets
    P.append(box((0.8, 0.03, 0.56), (0, -0.69, 0.36), M.steel, bevel=0.004))
    for k in range(9): P.append(box((0.8, 0.012, 0.01), (0, -0.71, 0.1 + k * 0.06), M.tin, bevel=0))
    P += [box((0.08, 0.06, 0.64), (-0.44, -0.7, 0.4), M.hazard), box((0.08, 0.06, 0.64), (0.44, -0.7, 0.4), M.hazard), box((0.96, 0.06, 0.08), (0, -0.7, 0.72), M.hazard)]
    P.append(box((1.0, 0.25, 0.12), (0, -0.84, 0.1), M.concrete, bevel=0.015))
    P += put(K.crate(0.2), (0.62, -0.8, 0.0), 0.2) + put(K.crate(0.2), (0.66, -0.78, 0.2), 0.7) + put(K.crate(0.18), (0.42, -0.85, 0.0), -0.3)
    P.append(box((0.3, 0.3, 0.04), (-0.6, -0.85, 0.02), M.wood, bevel=0.004))  # pallet
    P += put(K.barrel(), (-0.62, -0.84, 0.04)) + put(K.barrel(mat=M.yellow), (-0.5, -0.92, 0.04))
    # side door and a drainpipe; a floodlight over the door
    P += put(K.door(0.3, 0.5, hazard=False), (0.76, 0.3, 0.08), math.pi / 2)
    P += K.pipe_run([(0.77, -0.6, 0.82), (0.77, -0.6, 0.12), (0.88, -0.6, 0.1)], r=0.018)
    P += put(K.floodlight(0.12), (0, -0.7, 0.74), 0)
    P += put(K.armour_plates(1.4, 0.3, rng), (0, 0.86, 0.08), math.pi)
    return finish('wh_warehouse', P, 1024)

def research():
    random.seed(43); rng = random.Random(43)
    P = [box((1.8, 1.8, 0.08), (0, 0, 0.04), M.conc_dark, bevel=0.015)]
    P.append(box((1.3, 1.1, 0.6), (-0.15, 0.2, 0.38), M.concrete, bevel=0.03))   # bunker
    P.append(box((1.38, 1.18, 0.06), (-0.15, 0.2, 0.71), M.conc_dark, bevel=0.01))
    P += put(K.door(0.32, 0.5), (-0.4, -0.35, 0.08))
    for x in (0.1, 0.35):
        w = K.window(0.18, 0.14, 0); P += put(w, (x, -0.35, 0.5))
        P.append(box((0.16, 0.01, 0.12), (x, -0.355, 0.5), M.cyan, bevel=0))   # screens glowing behind the glass
    P += put(K.armour_plates(1.0, 0.3, rng), (-0.15, 0.75, 0.08), math.pi)
    # a satellite dish on a mount, an antenna array, cable runs
    P.append(cyl(0.05, 0.3, (0.2, 0.3, 0.89), M.steel, verts=10))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.32, location=(0, 0, 0), segments=24, ring_count=12)
    dish = bpy.context.active_object
    bpy.ops.object.mode_set(mode='EDIT'); bm = bmesh.from_edit_mesh(dish.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z > -0.18], context='VERTS'); bmesh.update_edit_mesh(dish.data)
    bpy.ops.object.mode_set(mode='OBJECT')
    dish.data.materials.append(M.tin); s = dish.modifiers.new('t', 'SOLIDIFY'); s.thickness = 0.015
    dish.location = (0.2, 0.3, 1.32); dish.rotation_euler = (math.radians(140), 0, math.radians(-30)); P.append(dish)
    P.append(rod((0.2, 0.3, 1.05), (0.12, 0.18, 1.2), 0.008, M.steel))
    for k, (x, y) in enumerate(((-0.6, 0.6), (-0.45, 0.65), (-0.3, 0.6))):
        P += put(K.antenna(0.45 + k * 0.12), (x, y, 0.74))
    P += K.pipe_run([(0.52, 0.0, 0.6), (0.7, 0.0, 0.6), (0.7, -0.4, 0.6), (0.7, -0.4, 0.1)], r=0.015)
    # outside: server cabinets and a cooling unit, cables on the ground
    for k in range(3):
        P.append(box((0.16, 0.2, 0.36), (0.68, 0.3 + k * 0.19, 0.26), M.armour, bevel=0.008))
        P.append(box((0.01, 0.12, 0.05), (0.6, 0.3 + k * 0.19, 0.3), M.cyan, bevel=0))
    P += put(K.ac_unit(), (0.35, -0.68, 0.08), 0.0)
    P.append(rod((0.6, -0.1, 0.1), (0.2, -0.6, 0.1), 0.012, M.rubber))
    P += put(K.floodlight(0.4), (-0.8, -0.7, 0.08), 0.6)
    return finish('wh_research', P, 1024)

def workshop():
    random.seed(47)
    P = [box((1.8, 1.8, 0.08), (0, 0, 0.04), M.concrete, bevel=0.015)]
    # a lean-to roof on posts, open at the front
    for x, y in ((-0.8, -0.05), (0.8, -0.05), (-0.8, 0.75), (0.8, 0.75)):
        P.append(cyl(0.03, 0.85 if y < 0.5 else 0.95, (x, y, (0.85 if y < 0.5 else 0.95) / 2 + 0.08), M.steel, verts=8, bevel=0))
    P.append(box((1.8, 0.95, 0.035), (0, 0.37, 0.99), M.scrap, rot=(math.atan2(0.1, 0.9), 0, 0), bevel=0.006))  # roof over the back half only
    P.append(box((1.6, 0.05, 0.6), (0, 0.78, 0.38), M.tin, bevel=0.006))     # back wall
    P.append(box((0.05, 1.5, 0.45), (-0.8, 0.0, 0.3), M.tin, bevel=0.006))    # side wall
    # workbench with a vice and tools, tool board on the wall
    P.append(box((0.8, 0.26, 0.04), (0.1, 0.6, 0.42), M.wood, bevel=0.005))
    for x in (-0.25, 0.45): P.append(box((0.04, 0.22, 0.34), (x, 0.6, 0.25), M.steel, bevel=0.004))
    P.append(box((0.08, 0.06, 0.06), (0.35, 0.55, 0.47), M.steel, bevel=0.006))
    P.append(box((0.7, 0.02, 0.32), (0.1, 0.75, 0.75), M.wood, bevel=0.004))
    for k in range(5): P.append(box((0.02, 0.012, 0.14), (-0.15 + k * 0.12, 0.735, 0.74), M.steel, bevel=0))
    # an engine block on a stand, a hoist gantry over it
    P.append(box((0.3, 0.22, 0.2), (0.2, -0.1, 0.32), M.rubber, bevel=0.02))
    P.append(box((0.24, 0.18, 0.1), (0.2, -0.1, 0.47), M.steel, bevel=0.015))
    for x in (-0.15, 0.55): P.append(cyl(0.025, 0.95, (x, -0.1, 0.55), M.yellow, verts=8, bevel=0))
    P.append(box((0.76, 0.05, 0.05), (0.2, -0.1, 1.02), M.hazard, bevel=0.005))
    P.append(rod((0.2, -0.1, 1.0), (0.2, -0.1, 0.62), 0.006, M.steel))
    P.append(cyl(0.015, 0.22, (0.2, -0.1, 0.22), M.steel, verts=8, bevel=0))
    # tyres, barrels, scrap, a car door leaning on the wall
    P += put(K.tyre(), (-0.55, -0.55, 0.08)) + put(K.tyre(), (-0.52, -0.53, 0.15)) + put(K.barrel(), (0.65, 0.35, 0.08)) + put(K.barrel(mat=M.yellow), (0.68, 0.2, 0.08))
    P.append(box((0.3, 0.03, 0.22), (-0.6, 0.6, 0.2), M.scrap, rot=(0.2, 0, 0.3), bevel=0.01))
    P += put(K.floodlight(0.15), (-0.75, -0.7, 0.85), 0.8)
    return finish('wh_workshop', P, 1024)

def generator():
    random.seed(53)
    P = [box((0.9, 0.9, 0.06), (0, 0, 0.03), M.conc_dark, bevel=0.01)]
    P.append(box((0.5, 0.34, 0.3), (-0.05, 0.05, 0.21), M.yellow, bevel=0.02))
    for k in range(6): P.append(box((0.008, 0.2, 0.012), (0.205, 0.05, 0.12 + k * 0.03), M.steel, bevel=0))   # louvres
    P.append(box((0.1, 0.36, 0.06), (-0.33, 0.05, 0.1), M.steel, bevel=0.008))
    P.append(cyl(0.035, 0.38, (-0.2, 0.15, 0.53), M.rubber, verts=10))                                      # exhaust stack
    P.append(cyl(0.045, 0.04, (-0.2, 0.15, 0.73), M.steel, verts=10))
    P.append(cyl(0.11, 0.42, (0.05, -0.28, 0.17), M.water, rot=(0, math.pi / 2, 0), verts=16, bevel=0.01))  # fuel tank
    for x in (-0.1, 0.2): P.append(box((0.04, 0.2, 0.1), (x, -0.28, 0.05), M.steel, bevel=0.004))
    P.append(rod((0.25, -0.28, 0.17), (0.25, -0.1, 0.12), 0.01, M.rubber))
    P += put(K.barrel(), (0.3, 0.3, 0.06)) + put(K.barrel(mat=M.yellow), (0.32, 0.15, 0.06))
    P.append(rod((-0.3, 0.3, 0.07), (-0.42, 0.42, 0.07), 0.012, M.rubber))
    return finish('wh_generator', P, 512)

def solar():
    random.seed(59)
    P = [box((0.9, 0.9, 0.04), (0, 0, 0.02), M.conc_dark, bevel=0.008)]
    for y in (-0.2, 0.22):
        P.append(box((0.82, 0.36, 0.025), (0, y, 0.3), M.panel, rot=(-0.55, 0, 0), bevel=0.006))
        P.append(box((0.86, 0.4, 0.018), (0, y + 0.004, 0.288), M.tin, rot=(-0.55, 0, 0), bevel=0.004))
        for x in (-0.35, 0.35):
            P.append(cyl(0.012, 0.22, (x, y - 0.1, 0.13), M.steel, verts=6, bevel=0)); P.append(cyl(0.012, 0.4, (x, y + 0.1, 0.22), M.steel, verts=6, bevel=0))
    P.append(box((0.22, 0.14, 0.14), (0.3, 0.4, 0.09), M.armour, bevel=0.01))   # battery box
    P.append(box((0.03, 0.08, 0.03), (0.18, 0.4, 0.12), M.cyan, bevel=0))
    P.append(rod((0.2, 0.36, 0.05), (0.0, 0.1, 0.05), 0.008, M.rubber))
    return finish('wh_solar', P, 512)

def oilpump():
    random.seed(61)
    P = [box((0.9, 0.5, 0.06), (0, 0, 0.03), M.conc_dark, bevel=0.01)]
    for x in (-0.08, 0.08):
        P.append(rod((x, 0.0, 0.06), (0.0, 0.0, 0.5), 0.02, M.steel)); P.append(rod((x, 0.0, 0.06), (0.0, 0.0, 0.5), 0.02, M.steel))
    P.append(rod((-0.08, -0.12, 0.06), (0.0, 0.0, 0.5), 0.018, M.yellow)); P.append(rod((0.08, 0.12, 0.06), (0.0, 0.0, 0.5), 0.018, M.yellow))
    P.append(box((0.75, 0.06, 0.06), (0.0, 0.0, 0.52), M.yellow, rot=(0, -0.12, 0), bevel=0.01))          # walking beam
    P.append(box((0.08, 0.1, 0.2), (0.38, 0.0, 0.48), M.hazard, rot=(0, -0.12, 0), bevel=0.015))          # horse head
    P.append(rod((0.42, 0.0, 0.4), (0.42, 0.0, 0.08), 0.005, M.steel))
    P.append(cyl(0.035, 0.1, (0.42, 0.0, 0.1), M.steel, verts=10))                                         # wellhead
    P.append(cyl(0.13, 0.05, (-0.3, 0.06, 0.22), M.armour, rot=(math.pi / 2, 0, 0), verts=16, bevel=0.01))  # counterweights
    P.append(cyl(0.13, 0.05, (-0.3, -0.06, 0.22), M.armour, rot=(math.pi / 2, 0, 0), verts=16, bevel=0.01))
    P.append(box((0.2, 0.18, 0.14), (-0.3, 0.0, 0.1), M.steel, bevel=0.01))                                # motor
    P += K.pipe_run([(0.42, 0.0, 0.12), (0.42, 0.18, 0.12), (0.2, 0.18, 0.08)], r=0.015)
    P += put(K.barrel(r=0.06, h=0.16), (0.3, -0.2, 0.06))
    return finish('wh_oilpump', P, 512)

def crops():
    random.seed(67)
    P = [box((0.9, 0.9, 0.05), (0, 0, 0.025), M.soil, bevel=0.01)]
    for k in range(4):
        y = -0.3 + k * 0.2
        P.append(box((0.8, 0.12, 0.05), (0, y, 0.07), M.soil, bevel=0.02))   # raised rows
        for j in range(8):
            x = -0.35 + j * 0.1 + random.uniform(-0.02, 0.02); h = random.uniform(0.08, 0.16)
            for b in range(3):  # a tuft of three leaning blades
                a = b * 2.1 + random.uniform(0, 1)
                P.append(box((0.012, 0.05, h), (x + math.cos(a) * 0.012, y + math.sin(a) * 0.012, 0.095 + h / 2), M.crop, rot=(math.cos(a) * 0.35, math.sin(a) * 0.35, a), bevel=0))
    for x, y in ((-0.44, -0.44), (0.44, -0.44), (-0.44, 0.44), (0.44, 0.44), (0, -0.44), (0, 0.44)):
        P.append(cyl(0.012, 0.18, (x, y, 0.09), M.wood, verts=6, bevel=0))
    for y in (-0.44, 0.44): P.append(rod((-0.44, y, 0.15), (0.44, y, 0.15), 0.005, M.steel))
    P += K.pipe_run([(-0.42, -0.36, 0.11), (-0.42, 0.36, 0.11)], r=0.01)
    P += put(K.barrel(r=0.06, h=0.16, mat=M.water), (0.38, 0.38, 0.05))
    return finish('wh_crops', P, 512)

assets = {'home-warehouse': warehouse(), 'home-research': research(), 'home-workshop': workshop(),
          'home-generator': generator(), 'home-solar': solar(), 'home-oilpump': oilpump(), 'home-crops': crops()}
for name, objs in assets.items():
    K.export(objs, os.path.join(OUT, name + '.glb'))

# ---------- preview ----------
for name, (x, y) in {'home-warehouse': (-2.2, 1.0), 'home-research': (0.0, 1.0), 'home-workshop': (2.2, 1.0),
                     'home-generator': (-2.0, -1.3), 'home-solar': (-0.7, -1.3), 'home-oilpump': (0.6, -1.3), 'home-crops': (1.9, -1.3)}.items():
    put(assets[name], (x, y, 0))
gm = K.worn('ground', (0.33, 0.33, 0.335), rough=0.95, scale=3, wear=0.0, grime=0.7, cracks=True)
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(gm)
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(42), math.radians(8), math.radians(-55)))
sun = bpy.context.active_object.data; sun.energy = 2.6; sun.angle = math.radians(1.0); sun.color = (1.0, 0.95, 0.86)
sc = bpy.context.scene
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.3, 0.37, 0.46, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.75
cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 6.4
cam = bpy.data.objects.new('cam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
rot = Euler((math.radians(52), 0, math.radians(32))); cam.rotation_euler = rot
cam.location = Vector((0.0, -0.2, 0.3)) - (rot.to_matrix() @ Vector((0, 0, -1))) * 25
sc.cycles.samples = 64; sc.cycles.use_denoising = True
sc.render.resolution_x = 1400; sc.render.resolution_y = 1000
sc.view_settings.view_transform = 'Standard'
sc.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print('preview', PREVIEW)
