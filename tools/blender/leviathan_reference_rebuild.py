"""Reference-led rebuild of the Leviathan assault bot.

The TripoSR output is retained as a hidden modeling reference. The visible model
is rebuilt as a symmetrical hovering AI weapons platform matching the supplied
front-view concept: central optic, layered hard-surface armor, power pylons, and
two oversized multi-barrel cannon arms. No animal legs are used.
"""

import math
import os
import traceback

import bpy
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "leviathan bot.glb")
OUT_DIR = os.path.join(ROOT, "client", "models", "units")
BLEND_OUT = os.path.join(OUT_DIR, "leviathan-ai-assault-bot.blend")
PREVIEW_OUT = os.path.join(OUT_DIR, "leviathan-ai-assault-bot-preview.png")
LOG_OUT = os.path.join(ROOT, "artifacts", "leviathan-reference-rebuild.log")


def active_object():
    return bpy.context.view_layer.objects.active


def material(name, color, metallic=0.7, roughness=0.32, emission=None, strength=0.0,
             detail=False):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    if detail:
        noise = mat.node_tree.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 11.0
        noise.inputs["Detail"].default_value = 4.0
        noise.inputs["Roughness"].default_value = 0.68
        bump = mat.node_tree.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.20
        bump.inputs["Distance"].default_value = 0.075
        mat.node_tree.links.new(noise.outputs["Fac"], bump.inputs["Height"])
        mat.node_tree.links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    if emission:
        shader.inputs["Emission Color"].default_value = (*emission, 1)
        shader.inputs["Emission Strength"].default_value = strength
    return mat


def bevel(obj, width=0.06, segments=2):
    mod = obj.modifiers.new("Machined edges", "BEVEL")
    mod.width = width
    mod.segments = segments
    return obj


def cube(name, dims, loc, mat, rotation=(0, 0, 0), edge=0.06, parent=None):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rotation)
    obj = active_object()
    obj.name = name
    obj.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if edge:
        bevel(obj, edge)
    obj.parent = parent
    return obj


def cylinder(name, radius, depth, loc, mat, rotation=(0, 0, 0), vertices=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=loc, rotation=rotation)
    obj = active_object()
    obj.name = name
    obj.data.materials.append(mat)
    bevel(obj, min(0.035, radius * 0.1), 2)
    obj.parent = parent
    return obj


def torus(name, major, minor, loc, mat, rotation=(math.pi / 2, 0, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                    major_segments=32, minor_segments=10,
                                    location=loc, rotation=rotation)
    obj = active_object()
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def cone(name, radius, depth, loc, mat, rotation=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=radius, radius2=radius * 0.22,
                                   depth=depth, location=loc, rotation=rotation)
    obj = active_object()
    obj.name = name
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def strut(name, start, end, radius, mat, parent=None):
    start, end = Vector(start), Vector(end)
    direction = end - start
    obj = cylinder(name, radius, direction.length, (start + end) * 0.5, mat,
                   vertices=16, parent=parent)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def hose(name, points, radius, mat, parent=None):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 3
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    obj.parent = parent
    return obj


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def import_reference():
    bpy.ops.import_scene.gltf(filepath=SOURCE)
    imported = list(bpy.context.scene.objects)
    ref_collection = bpy.data.collections.new("SOURCE_REFERENCE_HIDDEN")
    bpy.context.scene.collection.children.link(ref_collection)
    for obj in imported:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        ref_collection.objects.link(obj)
        obj.hide_render = True
        obj.hide_viewport = True
    ref_collection.hide_render = True
    ref_collection.hide_viewport = True


def build_bot():
    armor = material("Titanium ceramic armor", (0.19, 0.22, 0.27), 0.88, 0.27, detail=True)
    armor_light = material("Armor edge plates", (0.31, 0.34, 0.39), 0.82, 0.30, detail=True)
    dark = material("Recessed machine core", (0.018, 0.024, 0.032), 0.78, 0.38)
    steel = material("Barrel gunmetal", (0.075, 0.09, 0.115), 0.94, 0.20, detail=True)
    red = material("Leviathan AI energy", (0.22, 0.006, 0.004), 0.38, 0.22,
                   (0.9, 0.009, 0.003), 2.8)
    red_dim = material("Leviathan red seams", (0.16, 0.004, 0.003), 0.45, 0.28,
                       (0.46, 0.004, 0.002), 1.05)

    root = bpy.data.objects.new("Leviathan.AI.AssaultBot", None)
    bpy.context.collection.objects.link(root)

    # Central hovering chassis and tapered lower machine core.
    cube("Torso.Main", (3.35, 1.85, 1.45), (0, 0.05, 2.10), armor,
         rotation=(0.04, 0, 0), edge=0.18, parent=root)
    cube("Torso.FrontArmor", (2.25, 0.48, 1.70), (0, -0.93, 1.95), armor_light,
         rotation=(0.03, 0, 0), edge=0.15, parent=root)
    cube("Torso.LowerCore", (2.15, 1.35, 1.30), (0, 0.02, 1.05), dark,
         rotation=(0, 0, math.pi / 4), edge=0.13, parent=root)
    cube("Torso.Keel", (0.52, 0.42, 2.15), (0, -1.13, 1.48), armor,
         edge=0.09, parent=root)

    # The central eye is one sensor, not a face with two eyes.
    cylinder("AI.EyeHousing", 0.47, 0.34, (0, -1.30, 2.25), dark,
             rotation=(math.pi / 2, 0, 0), vertices=32, parent=root)
    torus("AI.EyeGuard", 0.36, 0.075, (0, -1.49, 2.25), armor_light, parent=root)
    cylinder("AI.Eye", 0.245, 0.08, (0, -1.56, 2.25), red,
             rotation=(math.pi / 2, 0, 0), vertices=32, parent=root)
    for side in (-1, 1):
        for strip in range(3):
            x = side * (0.50 + strip * 0.13)
            cube("Torso.EnergyVent", (0.075, 0.10, 0.56), (x, -1.19, 1.72), red_dim,
                 rotation=(0, side * 0.09, side * -0.05), edge=0.015, parent=root)
        cube("Torso.CheekPlate", (0.72, 0.19, 0.62), (side * 1.10, -1.22, 2.32), armor,
             rotation=(0.08, side * 0.18, side * -0.08), edge=0.08, parent=root)
        cube("Torso.LowerPlate", (0.64, 0.18, 0.48), (side * 0.82, -1.24, 1.26), armor_light,
             rotation=(0.02, side * 0.15, side * -0.10), edge=0.07, parent=root)

    # Layered central armor creates the angular, manufactured silhouette.
    for i, (z, width) in enumerate(((2.85, 2.35), (3.12, 1.75), (3.34, 1.16))):
        cube("Dorsal.Armor", (width, 1.14 - i * 0.12, 0.25), (0, 0.08, z),
             armor_light if i == 0 else armor, rotation=(0.03, 0, 0),
             edge=0.08, parent=root)
    for x in (-0.67, 0.67):
        cylinder("Power.Pylon", 0.23, 0.86, (x, 0.08, 3.66), armor,
                 vertices=20, parent=root)
        cylinder("Power.Core", 0.13, 0.58, (x, 0.08, 3.68), red,
                 vertices=20, parent=root)
        cone("Power.Cap", 0.18, 0.38, (x, 0.08, 4.27), steel, parent=root)

    # Downward armor teeth replace legs and reinforce the hovering siege-machine read.
    for x, length in ((-0.78, 0.82), (-0.38, 1.05), (0, 1.20), (0.38, 1.05), (0.78, 0.82)):
        cone("Ventral.ArmorFang", 0.19, length, (x, -0.25, 0.28 + length * 0.25),
             armor_light, rotation=(math.pi, 0, 0), parent=root)

    for side in (-1, 1):
        sx = side * 2.15
        # Shoulder block, layered pauldron, and exposed red power ring.
        cylinder("Shoulder.Pivot", 0.58, 0.70, (side * 1.74, 0.0, 2.15), dark,
                 rotation=(0, math.pi / 2, 0), vertices=28, parent=root)
        torus("Shoulder.PowerRing", 0.43, 0.09, (side * 1.95, -0.04, 2.15), red_dim,
              rotation=(0, math.pi / 2, 0), parent=root)
        cube("Shoulder.Armor", (1.28, 1.58, 0.68), (sx, 0.04, 2.58), armor,
             rotation=(0, side * -0.12, side * -0.055), edge=0.16, parent=root)
        cube("Shoulder.TopPlate", (1.42, 1.36, 0.20), (sx, -0.05, 3.00), armor_light,
             rotation=(0, side * -0.12, side * -0.055), edge=0.08, parent=root)
        for vent in range(3):
            cube("Shoulder.Vent", (0.075, 0.14, 0.32),
                 (sx + (vent - 1) * 0.15, -0.82, 2.70), red_dim,
                 rotation=(0, 0, side * -0.06), edge=0.015, parent=root)
        cube("Shoulder.FacePlate", (0.92, 0.22, 0.48), (sx, -0.82, 2.33), armor_light,
             rotation=(0.08, side * -0.08, side * -0.06), edge=0.08, parent=root)

        # Heavy braces carry the weapon forward from the shoulder.
        strut("Cannon.UpperBrace", (side * 1.92, -0.10, 2.18),
              (side * 2.52, -1.08, 1.62), 0.23, steel, parent=root)
        strut("Cannon.LowerBrace", (side * 1.72, -0.05, 1.85),
              (side * 2.42, -1.02, 1.20), 0.19, dark, parent=root)
        hose("Cannon.PowerConduit", [(side * 1.58, -0.18, 2.35),
             (side * 1.90, -0.65, 2.02), (side * 2.18, -0.86, 1.53),
             (side * 2.45, -1.05, 1.40)], 0.085, red_dim, parent=root)
        hose("Cannon.CoolantLine", [(side * 1.55, 0.05, 2.06),
             (side * 1.88, -0.42, 1.72), (side * 2.20, -0.74, 1.28),
             (side * 2.48, -0.98, 1.12)], 0.072, steel, parent=root)

        # Arm-mounted cannon housing with layered side armor.
        cannon_x = side * 2.62
        cannon_z = 1.28
        cube("Cannon.Receiver", (1.38, 1.78, 1.34), (cannon_x, -1.22, cannon_z),
             dark, rotation=(0.02, 0, side * -0.025), edge=0.16, parent=root)
        cube("Cannon.OuterArmor", (1.55, 1.12, 0.40),
             (cannon_x, -1.08, cannon_z + 0.68), armor,
             rotation=(0.05, 0, side * -0.025), edge=0.11, parent=root)
        cube("Cannon.SideArmor", (0.28, 1.35, 1.10),
             (cannon_x + side * 0.70, -1.18, cannon_z + 0.08), armor_light,
             rotation=(0, side * 0.08, 0), edge=0.08, parent=root)
        torus("Cannon.BreechRing", 0.49, 0.105, (cannon_x, -2.12, cannon_z),
              armor_light, parent=root)
        torus("Cannon.HeatRing", 0.43, 0.045, (cannon_x, -2.20, cannon_z),
              red_dim, parent=root)

        # Five real barrels per arm: centre plus four surrounding tubes.
        for barrel_i, (ox, oz) in enumerate(((0, 0), (-0.29, 0.26), (0.29, 0.26),
                                              (-0.29, -0.26), (0.29, -0.26))):
            bx = cannon_x + ox
            bz = cannon_z + oz
            cylinder("Cannon.Barrel", 0.135 if barrel_i else 0.16, 2.40,
                     (bx, -2.60, bz), steel, rotation=(math.pi / 2, 0, 0),
                     vertices=20, parent=root)
            cylinder("Cannon.BarrelCollar", 0.19 if barrel_i else 0.22, 0.22,
                     (bx, -2.00, bz), armor_light, rotation=(math.pi / 2, 0, 0),
                     vertices=20, parent=root)
            cylinder("Cannon.MuzzleEnergy", 0.095 if barrel_i else 0.115, 0.07,
                     (bx, -3.83, bz), red, rotation=(math.pi / 2, 0, 0),
                     vertices=20, parent=root)

        # Protected antennae make the machine's scale and symmetry readable.
        for offset in (-0.34, 0.34):
            cylinder("Shoulder.Antenna", 0.055, 0.72,
                     (sx + offset, 0.12, 3.42), steel, vertices=12, parent=root)
            cone("Shoulder.AntennaTip", 0.10, 0.32,
                 (sx + offset, 0.12, 3.94), armor, parent=root)

    return root


def presentation():
    floor_mat = material("Studio floor", (0.12, 0.135, 0.155), 0.28, 0.52)
    cube("Preview.Floor", (18, 18, 0.16), (0, 0, -0.16), floor_mat, edge=0.02)

    world = bpy.data.worlds.new("Bright neutral preview")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.105, 0.115, 0.13, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.52
    bpy.context.scene.world = world

    for name, loc, energy, color, size in (
        ("Front softbox", (0, -8, 8), 1500, (0.92, 0.96, 1.0), 6.0),
        ("Left fill", (-7, -2, 5), 900, (0.45, 0.62, 1.0), 5.0),
        ("Right rim", (7, 1, 5), 720, (1.0, 0.16, 0.08), 4.0),
        ("Top light", (0, 2, 9), 1100, (0.72, 0.82, 1.0), 5.0),
    ):
        bpy.ops.object.light_add(type="AREA", location=loc)
        light = active_object()
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        point_at(light, (0, -0.45, 1.8))

    bpy.ops.object.camera_add(location=(0, -14.2, 4.9))
    camera = active_object()
    camera.name = "Preview.Camera"
    camera.data.lens = 57
    point_at(camera, (0, -0.45, 1.75))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = PREVIEW_OUT
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.35
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_reference()
    build_bot()
    presentation()
    print("LEVIATHAN_REFERENCE_REBUILD_COMPLETE")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        os.makedirs(os.path.dirname(LOG_OUT), exist_ok=True)
        with open(LOG_OUT, "w", encoding="utf-8") as log:
            log.write(traceback.format_exc())
        raise
