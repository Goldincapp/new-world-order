"""Finishes the Last Stand and Extraction Run renders from tools/blender/operations_art.py with the same glow,
trimming and arena treatment as Leviathan Assault (see leviathan_post.py).

Run with a Python that has Pillow:
  python tools/blender/operations_post.py <raw_render_dir> client/assets/operations
"""
import os, sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from leviathan_post import sprites, arena

# sprite name -> width in the game files
SPRITES = {n: (w, []) for n, w in {
    'ls-bike': 150, 'ls-technical': 200, 'ls-truck': 230, 'ls-mortar': 180, 'ls-turret': 180,
    'ex-rover': 150, 'ex-patrol': 150, 'ex-sentry': 130, 'ex-drone': 150,
    'ex-crate-fuel': 110, 'ex-crate-trade': 110, 'ex-crate-tech': 110, 'ex-crate-black': 120}.items()}

if __name__ == '__main__':
    raw, out = sys.argv[1], sys.argv[2]
    sprites(raw, out, SPRITES)
    arena(raw, out, 'laststand-arena', brightness=0.85)
    arena(raw, out, 'extraction-arena', brightness=0.9)
