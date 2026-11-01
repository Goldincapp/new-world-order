from pathlib import Path

import bpy


ROOT = Path(r"C:\Users\jerem\Documents\ChatGPT\New Build")
OUTPUT = ROOT / "client" / "models" / "units" / "leviathan-ai-assault-bot.glb"
LOG = ROOT / "artifacts" / "leviathan-reference-export.log"


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    LOG.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.object.select_all(action="DESELECT")
    root = bpy.data.objects.get("Leviathan.AI.AssaultBot")
    if root is None:
        raise RuntimeError("Leviathan.AI.AssaultBot root was not found.")
    export_objects = [
        obj
        for obj in root.children_recursive
        if obj.type in {"MESH", "CURVE"} and not obj.hide_render
    ]
    for obj in export_objects:
        obj.select_set(True)

    if not export_objects:
        raise RuntimeError("No visible Leviathan geometry was found for export.")

    bpy.context.view_layer.objects.active = export_objects[0]
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )
    LOG.write_text(
        f"Exported {len(export_objects)} objects to {OUTPUT}\n",
        encoding="utf-8",
    )
    print("LEVIATHAN_REFERENCE_EXPORT_COMPLETE")


if __name__ == "__main__":
    main()
