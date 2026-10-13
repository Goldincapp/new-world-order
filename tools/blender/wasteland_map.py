"""Builds the Wasteland pieces seen on the sector map: a player's walled compound (one parcel) and the sector hall.

Run headless:
  blender -b --python tools/blender/wasteland_map.py -- <models_out_dir> <preview_png>
"""
import bpy, math, random, sys, os
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import wl_kit as K
from wl_kit import M, box, cyl, rod, put
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
OUT, PREVIEW = argv[0], argv[1]
TEX = os.path.join(os.path.dirname(PREVIEW), 'waste_tex')
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)
K.reset(); M.build()

def compound():
    """About 1 x 1: barrier walls with a gate gap, a plated command block, a watchtower, sandbags and clutter."""
    random.seed(71); rng = random.Random(71)
    P = [box((1.0, 1.0, 0.04), (0, 0, 0.02), M.conc_dark, bevel=0.008)]
    # barrier ring, open at the front for a gate
    for (sx, sy, x, y) in ((1.0, 0.07, 0, 0.465), (0.07, 1.0, -0.465, 0), (0.07, 1.0, 0.465, 0), (0.36, 0.07, -0.32, -0.465), (0.36, 0.07, 0.32, -0.465)):
        P.append(box((sx, sy, 0.12), (x, y, 0.1), M.concrete, bevel=0.012))
        P.append(box((sx * (0.95 if sx > 0.1 else 1), sy * (1 if sx > 0.1 else 0.95), 0.025), (x, y, 0.045), M.hazard, bevel=0.003))
    for (x, y) in ((-0.14, -0.465), (0.14, -0.465)):
        P.append(box((0.06, 0.06, 0.2), (x, y, 0.14), M.steel, bevel=0.006))
    P.append(box((0.24, 0.02, 0.1), (0, -0.47, 0.09), M.hazard, bevel=0.003))   # gate bar
    # the command block
    P.append(box((0.42, 0.36, 0.28), (0.05, 0.1, 0.18), M.concrete, bevel=0.015))
    P.append(box((0.45, 0.39, 0.03), (0.05, 0.1, 0.335), M.conc_dark, bevel=0.006))
    P.append(box((0.24, 0.22, 0.14), (-0.02, 0.15, 0.42), M.concrete, bevel=0.012))
    P.append(box((0.12, 0.012, 0.16), (0.05, -0.085, 0.12), M.hazard, bevel=0.003))   # door
    for x in (-0.1, 0.18):
        P.append(box((0.07, 0.012, 0.05), (x, -0.085, 0.24), M.glass, bevel=0))
    P += put(K.armour_plates(0.36, 0.2, rng), (0.05, 0.28, 0.06), math.pi)
    P.append(cyl(0.006, 0.3, (0.12, 0.2, 0.64), M.steel, verts=6, bevel=0))     # antenna
    P.append(cyl(0.012, 0.015, (0.12, 0.2, 0.79), M.red, verts=8, bevel=0))
    P.append(cyl(0.05, 0.08, (-0.08, 0.05, 0.53), M.water, verts=12, bevel=0.005))  # water tank
    # a small watchtower in the back corner
    for x, y in ((0.3, 0.3), (0.4, 0.3), (0.4, 0.4), (0.3, 0.4)):
        P.append(cyl(0.008, 0.45, (x, y, 0.245), M.steel, verts=6, bevel=0))
    P.append(box((0.16, 0.16, 0.02), (0.35, 0.35, 0.47), M.wood, bevel=0.003))
    P.append(box((0.17, 0.17, 0.015), (0.35, 0.35, 0.58), M.tin, rot=(0.15, 0, 0), bevel=0.003))
    P.append(box((0.04, 0.03, 0.03), (0.35, 0.29, 0.5), M.lamp, bevel=0))
    # sandbags at the gate, crates and barrels in the yard
    P += put(K.sandbag_wall(0.25, 2), (-0.25, -0.38, 0.04)) + put(K.sandbag_wall(0.25, 2), (0.25, -0.38, 0.04))
    P += put(K.crate(0.09), (-0.32, 0.2, 0.04), 0.3) + put(K.crate(0.08), (-0.3, 0.32, 0.04), 0.9)
    P += put(K.barrel(r=0.035, h=0.1), (0.32, -0.2, 0.04)) + put(K.barrel(r=0.035, h=0.1, mat=M.yellow), (0.38, -0.25, 0.04))
    return K.finish('map_base', P, 1024, TEX)

def hall():
    """About 1.6 x 1.6: an old civic building turned stronghold."""
    random.seed(73); rng = random.Random(73)
    P = [box((1.7, 1.7, 0.06), (0, 0, 0.03), M.conc_dark, bevel=0.01)]
    P.append(box((1.2, 0.95, 0.75), (0, 0.18, 0.43), M.concrete, bevel=0.025))   # main block
    P.append(box((1.28, 1.03, 0.07), (0, 0.18, 0.84), M.conc_dark, bevel=0.012))  # cornice
    P.append(box((0.6, 0.55, 0.32), (0, 0.25, 1.03), M.concrete, bevel=0.02))     # clock tower
    P.append(box((0.66, 0.61, 0.05), (0, 0.25, 1.21), M.conc_dark, bevel=0.01))
    bpy.ops.mesh.primitive_cylinder_add(radius=0.12, depth=0.02, location=(0, -0.031, 1.05), rotation=(math.pi / 2, 0, 0), vertices=24)
    clock = bpy.context.active_object; clock.data.materials.append(M.glass); P.append(clock)
    P.append(box((0.012, 0.01, 0.09), (0.0, -0.045, 1.08), M.steel, bevel=0)); P.append(box((0.07, 0.01, 0.012), (0.03, -0.045, 1.05), M.steel, bevel=0))
    # columned portico with steps, a damaged corner
    P.append(box((0.9, 0.22, 0.05), (0, -0.37, 0.72), M.concrete, bevel=0.01))
    for x in (-0.36, -0.12, 0.12, 0.36):
        P.append(cyl(0.035, 0.62, (x, -0.42, 0.39), M.concrete, verts=12, bevel=0.006))
    for k in range(3):  # steps up to the portico
        P.append(box((1.0 - k * 0.08, 0.1, 0.03), (0, -0.6 + k * 0.04, 0.075 + k * 0.03), M.concrete, bevel=0.005))
    P += put(K.door(0.22, 0.38, hazard=True), (0, -0.295, 0.06))
    for x in (-0.42, 0.42):
        for z in (0.32, 0.62):
            P += put(K.window(0.14, 0.14, 2, depth=0.03), (x, -0.295, z))
    P.append(box((0.3, 0.3, 0.25), (0.55, 0.6, 0.62), M.concrete, rot=(0.25, -0.3, 0.2), bevel=0.02))   # a fallen chunk of the corner
    # banners hanging off the cornice, a roof mast, floodlights, sandbag barricades and concrete barriers on the plaza
    for x in (-0.48, 0.48):
        P.append(box((0.14, 0.012, 0.4), (x, -0.3, 0.6), M.yellow, bevel=0.002))
    P += put(K.antenna(0.55), (0.35, 0.45, 0.88))
    P += put(K.floodlight(0.2), (-0.55, -0.25, 0.88), 0.4) + put(K.floodlight(0.2), (0.55, -0.25, 0.88), -0.4)
    P += put(K.water_tank(0.1, 0.16, 0.1), (-0.35, 0.5, 0.88))
    P += put(K.sandbag_wall(0.5, 3), (-0.55, -0.72, 0.06)) + put(K.sandbag_wall(0.5, 3), (0.55, -0.72, 0.06))
    for x in (-0.25, 0.25):
        P.append(box((0.3, 0.1, 0.12), (x, -0.8, 0.12), M.concrete, bevel=0.012))
        P.append(box((0.3, 0.102, 0.03), (x, -0.8, 0.08), M.hazard, bevel=0.003))
    P += put(K.armour_plates(1.0, 0.4, rng), (0, 0.66, 0.06), math.pi)
    P += put(K.crate(0.12), (0.7, -0.4, 0.06), 0.3) + put(K.barrel(r=0.05, h=0.14), (-0.72, -0.4, 0.06))
    return K.finish('map_hall', P, 1024, TEX)

assets = {'map-base': compound(), 'map-hall': hall()}
for name, objs in assets.items():
    K.export(objs, os.path.join(OUT, name + '.glb'))

# ---------- preview ----------
put(assets['map-base'], (-1.1, 0, 0)); put(assets['map-hall'], (0.7, 0.2, 0))
gm = K.worn('ground', (0.33, 0.33, 0.335), rough=0.95, scale=3, wear=0.0, grime=0.7, cracks=True)
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(gm)
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(42), math.radians(8), math.radians(-55)))
sun = bpy.context.active_object.data; sun.energy = 2.6; sun.angle = math.radians(1.0); sun.color = (1.0, 0.95, 0.86)
sc = bpy.context.scene
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.3, 0.37, 0.46, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.75
cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = 3.6
cam = bpy.data.objects.new('cam', cam_data); sc.collection.objects.link(cam); sc.camera = cam
rot = Euler((math.radians(52), 0, math.radians(32))); cam.rotation_euler = rot
cam.location = Vector((0.0, 0.0, 0.35)) - (rot.to_matrix() @ Vector((0, 0, -1))) * 25
sc.cycles.samples = 64; sc.cycles.use_denoising = True
sc.render.resolution_x = 1400; sc.render.resolution_y = 900
sc.view_settings.view_transform = 'Standard'
sc.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print('preview', PREVIEW)
