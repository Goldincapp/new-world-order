"""Finishes the Leviathan Assault renders from tools/blender/leviathan_assault.py for the game: adds bloom to
everything that glows (Cycles has none), trims the unit sprites, and darkens the arena's edges.

Run with a Python that has Pillow:
  python tools/blender/leviathan_post.py <raw_render_dir> client/assets/operations
"""
import os, sys
from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

# sprite name -> width in the game files; variants share their base's crop so they swap in place
SPRITES = {'caretaker-mech': (720, ['caretaker-mech-rage']), 'strike-leader': (180, []), 'partner-drone': (128, [])}


def bloom(im, radius, strength, threshold=170):
    """Blurs the lights and adds them back, so they spill a soft halo over their surroundings. Only strongly coloured
    bright pixels count as lights, so white armour and highlights don't glow."""
    rgb = im.convert('RGB')
    hsv = rgb.convert('HSV'); _, sat, val = hsv.split()
    lit = val.point(lambda v: 255 if v >= threshold else 0)
    mask = ImageChops.multiply(ImageChops.multiply(lit, sat.point(lambda v: min(255, v * 2))), val)
    glow = Image.composite(rgb, Image.new('RGB', rgb.size), mask).filter(ImageFilter.GaussianBlur(radius))
    glow = glow.point(lambda v: min(255, int(v * strength)))
    out = ImageChops.add(rgb, glow)
    if im.mode == 'RGBA':  # let the halo reach past the silhouette
        a = ImageChops.lighter(im.getchannel('A'), glow.convert('L').point(lambda v: min(255, v * 2)))
        out = out.convert('RGBA'); out.putalpha(a)
    return out


def sprites(raw, out, table):
    """Blooms and trims unit renders. table: name -> (width in the game files, variants that share its crop)."""
    for name, (width, variants) in table.items():
        images = [Image.open(os.path.join(raw, n + '.png')).convert('RGBA') for n in [name] + variants]
        radius = images[0].width * 0.012
        images = [bloom(im, radius, 1.4) for im in images]
        box = images[0].getchannel('A').point(lambda v: 255 if v > 6 else 0).getbbox()
        for im in images[1:]:
            b = im.getchannel('A').point(lambda v: 255 if v > 6 else 0).getbbox()
            box = (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
        for n, im in zip([name] + variants, images):
            im = im.crop(box); h = round(im.height * width / im.width)
            im.resize((width, h), Image.LANCZOS).save(os.path.join(out, n + '.png'), optimize=True)
            print('sprite', n, width, h)


def arena(raw, out, name, brightness=0.8):
    """Darkens an arena backdrop a little so shots stand out, blooms its lights and adds a soft vignette."""
    im = Image.open(os.path.join(raw, name + '.png')).convert('RGB')
    im = ImageEnhance.Contrast(ImageEnhance.Brightness(im).enhance(brightness)).enhance(1.12)
    im = bloom(im, im.width * 0.01, 1.3, threshold=150)
    shade = Image.new('L', im.size, 0); d = ImageDraw.Draw(shade)
    w, h = im.size
    d.ellipse((-w * 0.25, -h * 0.12, w * 1.25, h * 1.12), fill=255)  # pulls the eye to the floor
    shade = shade.filter(ImageFilter.GaussianBlur(w * 0.12))
    im = Image.composite(im, ImageChops.multiply(im, Image.new('RGB', im.size, (60, 70, 78))), shade)
    im.save(os.path.join(out, name + '.jpg'), quality=82, optimize=True, progressive=True)
    print('arena', name, im.size)


if __name__ == '__main__':
    RAW, OUT = sys.argv[1], sys.argv[2]
    sprites(RAW, OUT, SPRITES)
    arena(RAW, OUT, 'leviathan-arena')
