"""Build the first production-style Operation Severance infiltration room.

Run from the repository root:
  blender -b --python tools/blender/infiltration_room.py

Outputs:
  client/models/facility/infiltration-room.blend
  client/models/facility/infiltration-room.glb
  client/models/facility/textures/*.png
  client/models/facility/infiltration-room-preview.png
"""

import bpy
import math
import os
import random
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "client", "models", "facility")
TEX = os.path.join(OUT, "textures")
os.makedirs(TEX, exist_ok=True)


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def noise_texture(name, base, accent, seed, panel=False, hazard=False):
    size = 256
    rng = random.Random(seed)
    img = bpy.data.images.new(name, width=size, height=size, alpha=True)
    pixels = [0.0] * (size * size * 4)
    scratches = [(rng.randrange(size), rng.randrange(size), rng.randrange(12, 70)) for _ in range(42)]
    for y in range(size):
        for x in range(size):
            if hazard:
                stripe = ((x + y) // 28) % 2
                c = base if stripe == 0 else accent
            else:
                coarse = math.sin(x * 0.055) * math.sin(y * 0.043) * 0.035
                grain = (rng.random() - 0.5) * 0.07
                seam = -0.10 if panel and (x % 64 < 2 or y % 64 < 2) else 0.0
                mix = max(0.0, min(1.0, 0.18 + coarse + grain + seam))
                c = tuple(base[i] * (1 - mix) + accent[i] * mix for i in range(3))
            for sx, sy, length in scratches:
                if abs(y - sy) <= 1 and sx <= x <= sx + length:
                    c = tuple(min(1.0, v * 1.35 + 0.04) for v in c)
            i = (y * size + x) * 4
            pixels[i:i + 4] = (*c, 1.0)
    img.pixels.foreach_set(pixels)
    img.filepath_raw = os.path.join(TEX, name + ".png")
    img.file_format = "PNG"
    img.save()
    img.pack()
    return img


def textured_material(name, image, roughness=0.65, metallic=0.25, emission=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission[0], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission[1]
    return mat


def solid_material(name, color, roughness=0.55, metallic=0.25, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission[0], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission[1]
    return mat


def assign(obj, mat):
    obj.data.materials.append(mat)
    return obj


def cube(name, dimensions, location, mat, bevel=0.04, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(obj, mat)
    if bevel:
        mod = obj.modifiers.new("Edge softening", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def cylinder(name, radius, depth, location, mat, vertices=24, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    assign(obj, mat)
    bevel = obj.modifiers.new("Edge softening", "BEVEL")
    bevel.width = min(radius * 0.12, 0.035)
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return obj


def pipe_between(name, a, b, radius, mat):
    a, b = Vector(a), Vector(b)
    delta = b - a
    obj = cylinder(name, radius, delta.length, (a + b) * 0.5, mat, vertices=16)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = delta.to_track_quat("Z", "Y")
    return obj


def add_floor(mats):
    cube("Floor.Foundation", (14.4, 9.4, 0.34), (0, 0, -0.19), mats["dark"], 0.05)
    for ix in range(7):
        for iy in range(5):
            x = -5.95 + ix * 1.98
            y = -3.52 + iy * 1.76
            panel = cube(f"Floor.Panel.{ix}.{iy}", (1.86, 1.64, 0.10), (x, y, 0.03), mats["floor"], 0.025)
            panel.data.materials.clear()
            panel.data.materials.append(mats["floor_alt"] if (ix + iy) % 5 == 0 else mats["floor"])
            for corner in ((-.76, -.65), (.76, -.65), (-.76, .65), (.76, .65)):
                cylinder(f"Floor.Bolt.{ix}.{iy}", .035, .016, (x + corner[0], y + corner[1], .09), mats["steel"], vertices=10)
    for x in (-4.95, 4.95):
        cube("Floor.HazardLane", (.28, 8.3, .018), (x, 0, .095), mats["hazard"], 0)


def wall_panel(name, x, y, width, height, mats, rotation=(0, 0, 0)):
    panel = cube(name, (width, .22, height), (x, y, height * .5), mats["wall"], .035, rotation)
    inset = cube(name + ".Inset", (width * .72, .035, height * .55), (x, y - .13, height * .52), mats["wall_dark"], .018, rotation)
    return panel, inset


def add_walls(mats):
    # Back wall, panelled rather than one blank slab.
    for i in range(6):
        x = -5.72 + i * 2.29
        wall_panel(f"BackWall.Panel.{i}", x, 4.48, 2.18, 3.55, mats)
        cube(f"BackWall.LightSlot.{i}", (1.16, .035, .075), (x, 4.34, 2.72), mats["cyan"], .018)
    for x in (-6.88, -4.58, -2.29, 0, 2.29, 4.58, 6.88):
        cube("BackWall.Column", (.26, .48, 3.92), (x, 4.38, 1.96), mats["steel"], .035)

    # Left wall is complete. Right wall contains the security doorway used by gameplay.
    for side in (-1, 1):
        x = side * 6.92
        segments = [(-3.35, 2.0), (-1.6, 1.5), (1.6, 1.5), (3.35, 2.0)] if side == 1 else [(-3.3, 2.25), (-1.1, 2.25), (1.1, 2.25), (3.3, 2.25)]
        for i, (y, length) in enumerate(segments):
            cube(f"SideWall.{side}.{i}", (.28, length, 3.45), (x, y, 1.72), mats["wall"], .035)
            cube(f"SideWall.Trim.{side}.{i}", (.30, length * .72, .09), (x - side * .15, y, 2.65), mats["cyan"], .018)
    # Heavy portal frame on the right wall.
    for y in (-.88, .88):
        cube("Door.Frame.Side", (.55, .30, 3.05), (6.72, y, 1.52), mats["steel"], .04)
    cube("Door.Frame.Top", (.55, 2.05, .36), (6.72, 0, 2.88), mats["steel"], .04)
    cube("Door.Status", (.58, .18, .12), (6.39, -.69, 2.62), mats["red"], .025)


def add_ceiling_and_services(mats):
    for x in (-5.8, -2.9, 0, 2.9, 5.8):
        cube("Ceiling.Beam", (.24, 8.6, .26), (x, 0, 3.72), mats["steel"], .035)
        cube("Ceiling.Light", (1.25, .24, .075), (x, -.9, 3.54), mats["white_light"], .018)
    for y in (-3.9, 3.9):
        pipe_between("Service.Pipe.Red", (-5.9, y, 3.25), (5.9, y, 3.25), .075, mats["pipe_red"])
        pipe_between("Service.Pipe.Blue", (-5.9, y + .19, 3.08), (5.9, y + .19, 3.08), .055, mats["pipe_blue"])
        for x in (-5.4, -2.7, 0, 2.7, 5.4):
            cube("Service.Clamp", (.16, .20, .32), (x, y + .09, 3.17), mats["dark"], .02)
    cube("Ceiling.CableTray", (8.8, .46, .12), (0, 2.9, 3.46), mats["dark"], .025)
    for x in range(-4, 5):
        pipe_between("Cable", (x, 2.7, 3.48), (x, 3.1, 3.48), .022, mats["rubber"])


def add_server_bank(mats):
    for i in range(4):
        x = -5.85 + i * 1.02
        cube(f"Server.{i}.Body", (.86, .72, 2.18), (x, 3.65, 1.09), mats["dark"], .035)
        cube(f"Server.{i}.Front", (.68, .055, 1.82), (x, 3.27, 1.12), mats["steel"], .018)
        for row in range(6):
            cube(f"Server.{i}.Bay.{row}", (.53, .025, .16), (x, 3.235, .38 + row * .27), mats["wall_dark"], .006)
            cube(f"Server.{i}.LED.{row}", (.16, .018, .025), (x - .14, 3.215, .38 + row * .27), mats["cyan"] if row % 3 else mats["amber"], .004)
        cube(f"Server.{i}.Vent", (.42, .025, .28), (x, 3.215, 1.86), mats["rubber"], .006)


def add_security_station(mats):
    cube("Security.Desk", (2.35, .82, .78), (3.95, 2.75, .39), mats["wall_dark"], .065)
    cube("Security.DeskTrim", (2.05, .06, .12), (3.95, 2.31, .56), mats["amber"], .02)
    for i, x in enumerate((3.45, 4.45)):
        cube(f"Security.Screen.{i}", (.76, .08, .52), (x, 2.66, 1.08), mats["screen"], .035, rotation=(math.radians(-12), 0, 0))
        cube(f"Security.ScreenLine.{i}", (.48, .015, .035), (x, 2.605, 1.13), mats["cyan"], .005, rotation=(math.radians(-12), 0, 0))
    cylinder("Security.Stool", .28, .12, (3.95, 1.95, .42), mats["dark"], vertices=20)
    cylinder("Security.StoolStem", .07, .55, (3.95, 1.95, .18), mats["steel"], vertices=14)


def add_cover_and_story(mats):
    # Two believable waist-high cover blocks, offset so the squad has a route through the room.
    for i, (x, y, rot) in enumerate(((-2.2, -.25, -.12), (2.05, -.95, .12))):
        cube(f"Cover.{i}.Body", (1.78, .62, .72), (x, y, .36), mats["wall_dark"], .055, rotation=(0, 0, rot))
        cube(f"Cover.{i}.Armour", (1.52, .10, .32), (x, y - .34, .49), mats["steel"], .025, rotation=(0, 0, rot))
        cube(f"Cover.{i}.Marker", (.72, .035, .075), (x, y - .405, .57), mats["amber"], .012, rotation=(0, 0, rot))
    # Cargo and maintenance clutter provide scale without obstructing combat lanes.
    for i, (x, y, z) in enumerate(((-5.5, -2.75, .33), (-4.75, -2.9, .25), (5.55, -3.05, .33))):
        cube(f"Cargo.{i}", (.72, .72, z * 2), (x, y, z), mats["crate"], .045)
        cube(f"Cargo.{i}.Band", (.76, .14, z * 2 + .02), (x, y, z), mats["hazard"], .012)
    cylinder("Maintenance.Barrel", .30, .72, (5.72, 3.15, .36), mats["pipe_red"], vertices=24)
    cylinder("Maintenance.BarrelBand", .315, .08, (5.72, 3.15, .53), mats["steel"], vertices=24)
    cylinder("Maintenance.BarrelBand", .315, .08, (5.72, 3.15, .18), mats["steel"], vertices=24)


def add_signage(mats):
    cube("Sign.Operation", (2.55, .055, .56), (0, 4.18, 3.03), mats["screen"], .035)
    cube("Sign.OperationLine", (1.76, .025, .055), (0, 4.145, 3.10), mats["cyan"], .008)
    cube("Sign.Restricted", (1.32, .04, .38), (5.65, 4.16, 2.34), mats["red"], .025)


def setup_camera_and_lights():
    world = bpy.data.worlds.new("Facility.World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.008, 0.015, 0.021, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .32
    bpy.context.scene.world = world
    for location, color, energy, size in (
        ((-4.6, -2.4, 5.5), (0.42, .78, 1.0), 950, 4.0),
        ((4.8, -1.4, 4.6), (1.0, .43, .24), 650, 3.0),
        ((0, 3.7, 4.3), (.35, .86, 1.0), 800, 3.0),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.data.color = color
        light.data.energy = energy
        light.data.shape = "RECTANGLE"
        light.data.size = size
        light.data.size_y = size * .55
        direction = Vector((0, 0, 1.1)) - light.location
        light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.light_add(type="POINT", location=(6.1, -.75, 2.5))
    bpy.context.object.data.color = (1.0, .08, .03)
    bpy.context.object.data.energy = 210
    bpy.context.object.data.shadow_soft_size = .35

    bpy.ops.object.camera_add(location=(11.8, -14.6, 10.2))
    camera = bpy.context.object
    camera.name = "Facility.PreviewCamera"
    camera.data.lens = 45
    camera.data.sensor_width = 36
    camera.rotation_euler = (Vector((0, .55, 1.2)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera


def build():
    reset()
    floor_img = noise_texture("facility_floor", (.105, .145, .16), (.24, .31, .33), 10, panel=True)
    floor_alt_img = noise_texture("facility_floor_alt", (.125, .155, .16), (.31, .24, .17), 11, panel=True)
    wall_img = noise_texture("facility_wall", (.13, .18, .20), (.33, .40, .42), 12, panel=True)
    crate_img = noise_texture("facility_crate", (.22, .17, .095), (.46, .34, .13), 13, panel=True)
    hazard_img = noise_texture("facility_hazard", (.95, .62, .06), (.035, .043, .045), 14, hazard=True)
    mats = {
        "floor": textured_material("Facility Floor", floor_img, .56, .48),
        "floor_alt": textured_material("Facility Floor Alternate", floor_alt_img, .62, .40),
        "wall": textured_material("Facility Wall", wall_img, .52, .36),
        "crate": textured_material("Cargo Crate", crate_img, .72, .16),
        "hazard": textured_material("Hazard Stripe", hazard_img, .48, .22),
        "wall_dark": solid_material("Recessed Armour", (.055, .075, .082), .48, .55),
        "dark": solid_material("Structural Black", (.025, .035, .04), .43, .68),
        "steel": solid_material("Brushed Steel", (.24, .29, .30), .34, .78),
        "rubber": solid_material("Rubber and Cable", (.018, .021, .022), .88, .02),
        "pipe_red": solid_material("Emergency Pipe", (.39, .075, .045), .42, .5),
        "pipe_blue": solid_material("Coolant Pipe", (.06, .24, .31), .38, .58),
        "cyan": solid_material("Caretaker Cyan", (.04, .42, .52), .25, .32, ((.08, .78, 1.0), 5.5)),
        "amber": solid_material("Warning Amber", (.58, .32, .035), .28, .28, ((1.0, .48, .04), 4.2)),
        "red": solid_material("Security Red", (.48, .025, .02), .3, .34, ((1.0, .035, .02), 5.0)),
        "white_light": solid_material("Ceiling Light", (.82, .94, .96), .18, .08, ((.68, .93, 1.0), 6.5)),
        "screen": solid_material("Security Screen", (.025, .105, .13), .18, .25, ((.03, .48, .64), 2.5)),
    }
    add_floor(mats)
    add_walls(mats)
    add_ceiling_and_services(mats)
    add_server_bank(mats)
    add_security_station(mats)
    add_cover_and_story(mats)
    add_signage(mats)
    setup_camera_and_lights()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 820
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(OUT, "infiltration-room-preview.png")
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.image_settings.color_depth = "8"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "infiltration-room.blend"))
    bpy.ops.render.render(write_still=True)

    # Cameras and preview lights stay in the .blend. The live GLB also omits the
    # overhead service lattice so the game camera gets a clean tactical cutaway;
    # those pieces remain available in Blender for cinematics and close-ups.
    cutaway_prefixes = ("Ceiling.Beam", "Ceiling.CableTray", "Service.", "Cable")
    for obj in bpy.context.scene.objects:
        obj.select_set(obj.type == "MESH" and not obj.name.startswith(cutaway_prefixes))
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(OUT, "infiltration-room.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
    )
    print("INFILTRATION_ROOM_COMPLETE")


if __name__ == "__main__":
    build()
