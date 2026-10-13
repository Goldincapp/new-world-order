import bpy
import math
from pathlib import Path


ROOT = Path(r"C:\Users\jerem\Documents\ChatGPT\New Build")
OUT = ROOT / "client" / "models" / "units"
SOURCE = ROOT / "client" / "models" / "source"
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)


def clean_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for data in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        pass


def mat(name, color, metallic=0.45, roughness=0.34, emission=None, strength=0.0):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    return material


ARMOUR = mat("Obsidian Armour", (0.035, 0.055, 0.075), 0.82, 0.22)
STEEL = mat("Gunmetal", (0.16, 0.20, 0.24), 0.75, 0.27)
RED = mat("Warlord Red", (0.34, 0.025, 0.018), 0.66, 0.25)
BLACK = mat("Tactical Black", (0.012, 0.016, 0.021), 0.3, 0.42)
CYAN = mat("Ion Blue", (0.01, 0.25, 0.42), 0.2, 0.14, (0.0, 0.65, 1.0), 8.0)
ORANGE = mat("Hellfire", (0.38, 0.035, 0.008), 0.25, 0.18, (1.0, 0.06, 0.005), 10.0)
AMBER = mat("War Amber", (0.35, 0.13, 0.01), 0.3, 0.19, (1.0, 0.24, 0.015), 7.0)
GLASS = mat("Smoked Canopy", (0.015, 0.08, 0.12), 0.72, 0.1, (0.0, 0.18, 0.3), 1.6)
RUBBER = mat("Rubber", (0.008, 0.009, 0.011), 0.05, 0.7)


def finish(obj, material, bevel=0.05):
    obj.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel and obj.type == "MESH":
        mod = obj.modifiers.new("Machined Edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    return obj


def box(name, loc, scale, material=ARMOUR, rot=(0, 0, 0), bevel=0.05):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, material, bevel)


def uv(name, loc, scale, material=ARMOUR, segments=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=12, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, material, 0.025)


def ico(name, loc, scale, material=ARMOUR, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, material, 0.035)


def cyl(name, loc, radius, depth, material=STEEL, rot=(0, 0, 0), vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, material, 0.025)


def cone(name, loc, r1, r2, depth, material=ARMOUR, rot=(0, 0, 0), vertices=16):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, material, 0.025)


def torus(name, loc, major, minor, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=10, location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    return obj


def build_skimmer():
    box("Skimmer_Core", (0, 0.38, 0), (0.42, 0.22, 1.12), ARMOUR, bevel=0.11)
    cone("Skimmer_Nose", (0, 0.34, -1.25), 0.43, 0.05, 0.95, ARMOUR, (math.pi / 2, 0, 0))
    uv("Skimmer_Canopy", (0, 0.62, -0.35), (0.31, 0.2, 0.54), GLASS)
    for side in (-1, 1):
        wing = box(f"Skimmer_Wing_{side}", (side * 0.78, 0.29, 0.05), (0.72, 0.055, 0.55), STEEL, (0, side * -0.18, side * -0.11), 0.035)
        box(f"Skimmer_WingTip_{side}", (side * 1.34, 0.25, 0.22), (0.12, 0.09, 0.5), RED, (0, side * -0.18, side * -0.11), 0.03)
        cyl(f"Skimmer_Engine_{side}", (side * 0.64, 0.3, 0.62), 0.2, 0.75, STEEL, (math.pi / 2, 0, 0))
        torus(f"Skimmer_Thruster_{side}", (side * 0.64, 0.3, 1.03), 0.15, 0.045, CYAN, (math.pi / 2, 0, 0))
        uv(f"Skimmer_Glow_{side}", (side * 0.64, 0.3, 1.05), (0.11, 0.11, 0.08), CYAN, 16)
        box(f"Skimmer_Cannon_{side}", (side * 0.42, 0.18, -0.82), (0.055, 0.055, 0.42), BLACK, bevel=0.02)
    box("Skimmer_Spine", (0, 0.48, 0.66), (0.08, 0.2, 0.55), RED, bevel=0.035)
    for z in (-0.98, -0.72, -0.46):
        box(f"Skimmer_CyanMark_{z}", (0, 0.6, z), (0.035, 0.018, 0.08), CYAN, bevel=0.01)


def build_bike():
    for z in (-0.72, 0.72):
        torus(f"wheel_{z}", (0, 0.4, z), 0.33, 0.105, RUBBER, (0, math.pi / 2, 0))
        cyl(f"Hub_{z}", (0, 0.4, z), 0.12, 0.16, STEEL, (0, math.pi / 2, 0), 18)
    box("Bike_Frame", (0, 0.56, 0), (0.16, 0.13, 0.64), RED, (0.08, 0, 0), 0.055)
    box("Bike_Armour", (0, 0.7, -0.2), (0.28, 0.17, 0.36), ARMOUR, (-0.12, 0, 0), 0.07)
    cyl("Bike_Fork", (0, 0.58, -0.58), 0.045, 0.63, STEEL, (-0.3, 0, 0), 12)
    box("Bike_Handle", (0, 0.88, -0.48), (0.36, 0.035, 0.035), STEEL, bevel=0.015)
    uv("Bike_Headlight", (0, 0.68, -0.72), (0.11, 0.09, 0.06), AMBER, 16)
    box("Bike_Seat", (0, 0.79, 0.22), (0.2, 0.055, 0.27), BLACK, bevel=0.05)
    uv("Bike_RiderTorso", (0, 1.08, 0.12), (0.22, 0.36, 0.2), ARMOUR)
    uv("Bike_RiderHead", (0, 1.47, -0.08), (0.16, 0.18, 0.17), BLACK, 18)
    box("Bike_Visor", (0, 1.48, -0.23), (0.12, 0.055, 0.025), ORANGE, bevel=0.015)
    for side in (-1, 1):
        cyl(f"Bike_Exhaust_{side}", (side * 0.19, 0.54, 0.5), 0.055, 0.48, BLACK, (math.pi / 2, 0, 0), 12)
        uv(f"Bike_ExhaustGlow_{side}", (side * 0.19, 0.54, 0.77), (0.045, 0.045, 0.04), ORANGE, 12)


def build_leviathan():
    ico("Leviathan_CoreHull", (0, 0.45, 0), (1.38, 0.58, 0.9), ARMOUR, 2)
    ico("Leviathan_Crown", (0, 0.85, 0.02), (0.78, 0.48, 0.62), STEEL, 2)
    uv("Leviathan_Core", (0, 0.52, -0.74), (0.31, 0.31, 0.19), ORANGE, 24)
    torus("Leviathan_CoreRing", (0, 0.52, -0.91), 0.42, 0.065, RED)
    for row, y, z, width in ((0, 0.85, -0.36, 1.12), (1, 0.68, 0.05, 1.3), (2, 0.48, 0.44, 1.1)):
        box(f"Leviathan_ArmorBand_{row}", (0, y, z), (width, 0.09, 0.25), STEEL, (0.06, 0, 0), 0.07)
    for side in (-1, 1):
        box(f"Leviathan_Shoulder_{side}", (side * 1.28, 0.52, 0.02), (0.62, 0.3, 0.56), ARMOUR, (0, side * 0.15, side * -0.16), 0.1)
        box(f"Leviathan_Arm_{side}", (side * 1.75, 0.3, 0.22), (0.38, 0.26, 0.72), RED, (0, side * 0.14, side * -0.22), 0.08)
        cyl(f"Leviathan_Cannon_{side}", (side * 1.92, 0.12, -0.36), 0.26, 1.34, BLACK, (math.pi / 2, 0, 0), 16)
        torus(f"Leviathan_Muzzle_{side}", (side * 1.92, 0.12, -1.04), 0.23, 0.055, ORANGE)
        for barrel in (-0.13, 0.13):
            cyl(f"Leviathan_Barrel_{side}_{barrel}", (side * 1.92 + barrel, 0.12, -0.84), 0.07, 0.62, STEEL, (math.pi / 2, 0, 0), 12)
        for i in range(3):
            cone(f"Leviathan_WingSpike_{side}_{i}", (side * (1.1 + i * 0.32), 0.85 - i * 0.15, 0.55 + i * 0.16), 0.18, 0, 0.78, STEEL, (math.pi / 2 + 0.45, side * 0.2, 0), 10)
        for i in range(3):
            uv(f"Leviathan_Eye_{side}_{i}", (side * (0.34 + i * 0.22), 0.73 - i * 0.04, -0.76), (0.07, 0.07, 0.045), ORANGE, 12)
    for i in range(5):
        cone(f"Leviathan_Spine_{i}", (0, 1.1 + i * 0.04, 0.52 + i * 0.28), 0.18 - i * 0.018, 0, 0.64, STEEL, (math.pi / 2 - 0.25, 0, 0), 10)
    box("Leviathan_Brow", (0, 0.84, -0.63), (0.72, 0.1, 0.15), RED, bevel=0.045)


def export_model(label, builder, filename, display_offset):
    before = set(bpy.context.scene.objects)
    builder()
    objects = [obj for obj in bpy.context.scene.objects if obj not in before]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.gltf(
        filepath=str(OUT / filename),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
    )
    for obj in objects:
        obj.location.x += display_offset
    return objects


clean_scene()
skimmer_objects = export_model("Strike Skimmer", build_skimmer, "strike-skimmer.glb", -4.2)
leviathan_objects = export_model("Leviathan", build_leviathan, "leviathan.glb", 0.0)
bike_objects = export_model("Raider Bike", build_bike, "raider-bike.glb", 4.2)

for obj in bpy.context.scene.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = next(iter(bpy.context.scene.objects), None)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "operation-models.blend"))
print("NWO_MODELS_COMPLETE", len(skimmer_objects), len(leviathan_objects), len(bike_objects))
