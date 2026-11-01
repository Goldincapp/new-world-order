"""Build a detailed, game-ready interpretation of the original Leviathan concept.

The source artwork is a single frontal image, so unseen surfaces are an authored
interpretation. The result preserves its important language: fortress-like torso,
layered gun arms, exposed cable bundles, central red optic and heavy underside.
"""

import math
import os

import bpy
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "client", "models", "units")
os.makedirs(OUT, exist_ok=True)


def mat(name, color, metallic=.7, roughness=.34, emission=None, strength=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.metallic = metallic
    m.roughness = roughness
    if emission:
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Emission Color"].default_value = (*emission, 1)
        bsdf.inputs["Emission Strength"].default_value = strength
    return m


def bevel(obj, amount=.06, segments=2):
    mod = obj.modifiers.new("Edge wear", "BEVEL")
    mod.width = amount
    mod.segments = segments
    return obj


def cube(name, dims, loc, material, rotation=(0, 0, 0), edge=.05):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rotation)
    o = bpy.context.object
    o.name = name
    o.dimensions = dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    if edge:
        bevel(o, edge)
    return o


def cyl(name, radius, depth, loc, material, rotation=(0, 0, 0), vertices=20, edge=.025):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    if edge:
        bevel(o, edge)
    return o


def sphere(name, radius, loc, material, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=radius, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    o.data.materials.append(material)
    return o


def torus(name, major, minor, loc, material, rotation=(math.pi / 2, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=10, location=loc, rotation=rotation)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    return o


def wedge(name, width, depth, height, loc, material, point_front=True):
    w, d, h = width / 2, depth / 2, height / 2
    front = -d if point_front else d
    back = d if point_front else -d
    verts = [(-w, back, -h), (w, back, -h), (-w, back, h), (w, back, h),
             (-w * .66, front, -h), (w * .66, front, -h), (0, front, h), (0, front, h)]
    faces = [(0, 1, 3, 2), (4, 6, 2, 0), (1, 3, 6, 5), (0, 1, 5, 4), (2, 6, 3), (4, 5, 6)]
    mesh = bpy.data.meshes.new(name + ".Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(material)
    o = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(o)
    o.location = loc
    bevel(o, .045, 2)
    return o


def cable(name, points, radius, material):
    curve = bpy.data.curves.new(name + ".Curve", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bp, co in zip(spline.bezier_points, points):
        bp.co = co
        bp.handle_left_type = "AUTO"
        bp.handle_right_type = "AUTO"
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(material)
    return o


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    armour = mat("Leviathan worn armour", (.055, .065, .075), .82, .29)
    armour2 = mat("Leviathan edge armour", (.11, .12, .13), .88, .25)
    dark = mat("Leviathan recess", (.012, .016, .021), .65, .5)
    steel = mat("Leviathan mechanisms", (.20, .22, .23), .9, .22)
    red = mat("Caretaker red", (.32, .008, .004), .48, .25, (1, .018, .006), 7)
    red_hot = mat("Leviathan core", (.68, .012, .005), .35, .18, (1, .025, .008), 15)
    orange = mat("Heat vents", (.40, .025, .004), .38, .27, (1, .09, .01), 6)
    cable_mat = mat("Armoured cables", (.018, .021, .023), .38, .64)

    root = bpy.data.objects.new("Leviathan.Concept", None)
    bpy.context.collection.objects.link(root)

    def add(obj):
        obj.parent = root
        return obj

    # Central fortress torso and layered prow.
    add(cube("Torso.Core", (3.65, 2.25, 2.15), (0, .12, 2.35), dark, edge=.22))
    add(wedge("Torso.FrontArmour", 3.55, 1.48, 1.72, (0, -1.18, 2.4), armour))
    add(wedge("Torso.LowerProw", 2.55, 1.62, 1.45, (0, -1.58, 1.38), armour2))
    add(cube("Torso.Keel", (.78, 1.3, 2.75), (0, -1.92, 1.84), armour2, edge=.09))
    for side in (-1, 1):
        add(wedge("Torso.Cheek", 1.15, 1.18, 1.16, (side * 1.33, -1.62, 2.08), armour))
        add(cube("Torso.RedSeam", (.10, .055, .92), (side * .82, -2.34, 1.55), red, edge=.012))
        for i in range(3):
            add(cube("Torso.Vent", (.34, .08, .065), (side * (1.13 + i * .18), -2.02, 2.74 - i * .13), orange, rotation=(0, 0, side * -.22), edge=.01))

    # Concentric central optic, deliberately readable at gameplay distance.
    add(cyl("Core.Housing", .58, .30, (0, -2.34, 2.85), armour2, rotation=(math.pi / 2, 0, 0), vertices=32, edge=.04))
    add(torus("Core.OuterRing", .47, .075, (0, -2.53, 2.85), red))
    add(torus("Core.InnerRing", .29, .055, (0, -2.57, 2.85), orange))
    add(sphere("Core.Eye", .20, (0, -2.65, 2.85), red_hot, scale=(1, .42, 1)))

    # Shoulder citadels and layered armour blades.
    for side in (-1, 1):
        sx = side * 2.25
        add(sphere("Shoulder.Joint", .82, (sx, -.18, 2.66), steel, scale=(1.15, 1, 1)))
        add(cube("Shoulder.Block", (1.44, 1.72, 1.72), (sx, -.18, 2.78), armour, rotation=(0, side * -.12, side * -.08), edge=.15))
        for layer in range(3):
            add(wedge("Shoulder.Plate", 1.62 - layer * .12, .58, .54,
                      (sx + side * (layer * .10), -1.05 - layer * .17, 3.18 - layer * .33), armour2))
        add(cube("Shoulder.Light", (.11, .08, .70), (sx + side * .45, -1.10, 2.82), red, edge=.02))

        # Arm linkage and forearm armour.
        add(cyl("Arm.Pivot", .44, 1.30, (side * 2.62, -.28, 1.72), steel, rotation=(0, math.pi / 2, side * .13), vertices=20))
        add(cube("Arm.Forearm", (1.15, 2.06, .98), (side * 2.84, -1.17, 1.52), armour, rotation=(0, 0, side * -.10), edge=.16))
        add(cube("Arm.Trim", (.12, 1.48, .18), (side * 2.84, -2.14, 1.67), red, edge=.025))

        # Rotary cannon cluster: central shroud plus six independently readable barrels.
        add(cyl("Cannon.Shroud", .57, 1.42, (side * 2.91, -2.58, 1.27), armour2, rotation=(math.pi / 2, 0, 0), vertices=24, edge=.06))
        add(torus("Cannon.Collar", .54, .075, (side * 2.91, -3.31, 1.27), steel))
        for barrel in range(6):
            a = barrel * math.tau / 6
            bx = side * 2.91 + math.cos(a) * .31
            bz = 1.27 + math.sin(a) * .31
            add(cyl("Cannon.Barrel", .105, 1.78, (bx, -3.63, bz), dark, rotation=(math.pi / 2, 0, 0), vertices=14, edge=.018))
            add(torus("Cannon.MuzzleRing", .118, .027, (bx, -4.53, bz), red_hot))
            add(cyl("Cannon.Muzzle", .079, .08, (bx, -4.57, bz), red_hot, rotation=(math.pi / 2, 0, 0), vertices=14, edge=0))

        # Flexible power and ammunition feeds.
        for cable_i in range(4):
            z = 2.08 + cable_i * .20
            add(cable("Arm.Cable", [(side * 1.35, -.55, z + .45),
                                     (side * 1.8, -1.12 - cable_i * .05, z),
                                     (side * 2.45, -1.30, 1.70 - cable_i * .08)], .072, cable_mat))

    # Rear reactor stacks, fins and antennae create the dense fortress silhouette.
    for side in (-1.45, 1.45):
        add(cube("Reactor.Tower", (.72, .82, 1.62), (side, .62, 3.68), armour, edge=.09))
        add(cube("Reactor.Glow", (.40, .055, .94), (side, -.02, 3.72), red, edge=.025))
        for level in range(3):
            add(cube("Reactor.Cap", (.84 - level * .12, .72 - level * .08, .18), (side, .62, 4.48 + level * .16), armour2, edge=.035))
    for x in (-2.8, -2.1, -.72, .72, 2.1, 2.8):
        add(cyl("Signal.Spire", .075, .82 + (1 - abs(x) / 3) * .5, (x, .64, 4.25), steel, vertices=10, edge=.012))
        add(cyl("Signal.RedTip", .105, .16, (x, .64, 4.72), red_hot, vertices=10, edge=.01))
    for side in (-1, 1):
        for i in range(4):
            add(wedge("Back.Fin", .38, .84, .72, (side * (.55 + i * .34), .98 + i * .08, 3.42 - i * .12), armour2, point_front=False))

    # Underside machinery and hover emitters.
    add(cube("Undercarriage", (2.35, 1.72, .65), (0, .18, .72), dark, edge=.10))
    for x in (-1.35, 0, 1.35):
        add(cyl("Hover.Pod", .42, .25, (x, .20, .37), armour2, vertices=24, edge=.035))
        add(torus("Hover.Glow", .34, .065, (x, .20, .22), red_hot, rotation=(0, 0, 0)))

    # A few armour fasteners and warm vents add scale without texture dependence.
    for side in (-1, 1):
        for row in range(4):
            for col in range(2):
                add(cyl("Armour.Bolt", .035, .055, (side * (1.05 + col * .30), -2.12, 1.24 + row * .34), steel,
                        rotation=(math.pi / 2, 0, 0), vertices=8, edge=0))

    # Convert cable curves for portable GLB export.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in list(bpy.context.scene.objects):
        if obj.type == "CURVE":
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.convert(target="MESH")
            obj.select_set(False)

    # Presentation environment.
    floor_mat = mat("Hangar floor", (.025, .032, .038), .65, .46)
    cube("Preview.Floor", (18, 18, .22), (0, 0, -.18), floor_mat, edge=.02)
    world = bpy.data.worlds.new("Leviathan World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.004, .006, .009, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .12

    for name, loc, energy, color, size in (
        ("Key", (7, -9, 10), 520, (.48, .68, 1.0), 5),
        ("Rim", (-8, 1, 8), 620, (.08, .28, 1.0), 4),
        ("Red fill", (5, 2, 4), 260, (1.0, .025, .008), 3),
    ):
        bpy.ops.object.light_add(type="AREA", location=loc)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.color = color
        light.data.shape = "DISK"
        light.data.size = size
        look_at(light, (0, -1, 2))
    bpy.ops.object.light_add(type="POINT", location=(0, -3.4, 2.7))
    bpy.context.object.data.energy = 85
    bpy.context.object.data.color = (1, .03, .01)

    bpy.ops.object.camera_add(location=(8.7, -13.8, 7.8))
    camera = bpy.context.object
    camera.data.lens = 60
    look_at(camera, (0, -1.15, 2.25))
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.join(OUT, "leviathan-concept-preview.png")
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.05

    blend_path = os.path.join(OUT, "leviathan-concept.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    bpy.ops.render.render(write_still=True)

    # Export the mech only; presentation floor, camera and lighting stay in Blender.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in root.children_recursive:
        if obj.type == "MESH":
            obj.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=os.path.join(OUT, "leviathan-concept.glb"),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_materials="EXPORT",
    )
    print("LEVIATHAN_CONCEPT_COMPLETE")


if __name__ == "__main__":
    build()
