#!/usr/bin/env python3
"""Turn an AI-generated animation sheet into game-ready frames for DoctorSimiBoss.

Input: one image per animation, frames laid out in a grid (left-to-right, top-to-bottom),
on a flat chroma background (default pure green #00FF00) or already transparent.

For each cell the tool removes the background (with despill), finds the character, scales
every frame by the SAME factor (taken from the tallest frame, so the character keeps its size
across the animation) and pastes it into a fixed-size transparent frame with the feet on a
common baseline and the body centred horizontally. It writes:

  <out>/<Anim>/<anim>_00.png ...           one PNG per frame
  <out>/<Anim>/<anim>_spritesheet.png      all frames in a row
  <out>/<Anim>/<anim>_preview.gif          quick animated preview

and prints the frame list to paste into animation_manifest.json.

Requires Pillow:  pip install pillow
Example:
  python import_sheet.py idle_sheet.png --anim Idle --cols 4 --rows 2 --frames 8 --out ../Sprites
"""
import argparse
import os
import sys

from PIL import Image


def chroma_to_alpha(img, key, tolerance, softness):
    """Make pixels close to `key` transparent; partially transparent near the edge; remove colour spill."""
    img = img.convert("RGBA")
    px = img.load()
    kr, kg, kb = key
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            dist = ((r - kr) ** 2 + (g - kg) ** 2 + (b - kb) ** 2) ** 0.5
            if dist <= tolerance:
                px[x, y] = (0, 0, 0, 0)
                continue
            if dist <= tolerance + softness:
                a = int(a * (dist - tolerance) / softness)
            # Despill: pull the key's dominant channel down to the other two.
            if kg >= kr and kg >= kb:
                g = min(g, max(r, b))
            elif kr >= kg and kr >= kb:
                r = min(r, max(g, b))
            else:
                b = min(b, max(r, g))
            px[x, y] = (r, g, b, a)
    return img


def alpha_bbox(img, threshold):
    return img.getchannel("A").point(lambda v: 255 if v > threshold else 0).getbbox()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("sheet", help="sheet image (PNG/JPG/WEBP)")
    ap.add_argument("--anim", required=True, help="animation name, e.g. Idle, Attack, SpecialSlam")
    ap.add_argument("--cols", type=int, required=True)
    ap.add_argument("--rows", type=int, required=True)
    ap.add_argument("--frames", type=int, help="frames to use (default cols*rows)")
    ap.add_argument("--out", default="../Sprites", help="output root (a folder per animation is created)")
    ap.add_argument("--frame-size", type=int, default=256, help="square output frame size in px (default 256)")
    ap.add_argument("--char-height", type=int, default=224, help="height of the tallest frame's character, px")
    ap.add_argument("--baseline", type=float, default=0.97, help="feet row as a fraction of frame height (manifest origin.y)")
    ap.add_argument("--key", default="00ff00", help="chroma key colour as hex; 'none' if the sheet is already transparent")
    ap.add_argument("--tolerance", type=float, default=90.0)
    ap.add_argument("--softness", type=float, default=60.0)
    ap.add_argument("--alpha-threshold", type=int, default=40)
    ap.add_argument("--fps", type=float, default=10.0, help="preview GIF speed")
    ap.add_argument("--no-scale-lock", action="store_true",
                    help="scale every frame to --char-height on its own (default keeps one scale for the animation)")
    args = ap.parse_args()

    sheet = Image.open(args.sheet).convert("RGBA")
    count = args.frames or args.cols * args.rows
    cw, ch = sheet.width / args.cols, sheet.height / args.rows

    cells = []
    for i in range(count):
        c, r = i % args.cols, i // args.cols
        cell = sheet.crop((round(c * cw), round(r * ch), round((c + 1) * cw), round((r + 1) * ch)))
        if args.key.lower() != "none":
            k = args.key.lstrip("#")
            cell = chroma_to_alpha(cell, (int(k[0:2], 16), int(k[2:4], 16), int(k[4:6], 16)), args.tolerance, args.softness)
        box = alpha_bbox(cell, args.alpha_threshold)
        if box is None:
            sys.exit(f"cell {i} (col {c}, row {r}) is empty after removing the background")
        cells.append(cell.crop(box))

    tallest = max(img.height for img in cells)
    fs = args.frame_size
    base_y = round(args.baseline * fs)
    frames = []
    for i, img in enumerate(cells):
        scale = (args.char_height / img.height) if args.no_scale_lock else (args.char_height / tallest)
        sized = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
        if sized.width > fs or sized.height > base_y:
            print(f"warning: frame {i} ({sized.width}x{sized.height}) does not fit; shrinking it", file=sys.stderr)
            f = min(fs / sized.width, base_y / sized.height)
            sized = sized.resize((max(1, int(sized.width * f)), max(1, int(sized.height * f))), Image.LANCZOS)
        frame = Image.new("RGBA", (fs, fs), (0, 0, 0, 0))
        frame.alpha_composite(sized, ((fs - sized.width) // 2, base_y - sized.height))
        frames.append(frame)

    name = args.anim
    folder = os.path.join(args.out, name)
    os.makedirs(folder, exist_ok=True)
    low = name.lower()
    paths = []
    for i, frame in enumerate(frames):
        p = os.path.join(folder, f"{low}_{i:02d}.png")
        frame.save(p, optimize=True)
        paths.append(f"Assets/Sprites/{name}/{low}_{i:02d}.png")

    strip = Image.new("RGBA", (fs * len(frames), fs), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        strip.alpha_composite(frame, (i * fs, 0))
    strip.save(os.path.join(folder, f"{low}_spritesheet.png"), optimize=True)

    preview = [Image.alpha_composite(Image.new("RGBA", (fs, fs), (40, 44, 52, 255)), f).convert("P", palette=Image.ADAPTIVE) for f in frames]
    preview[0].save(os.path.join(folder, f"{low}_preview.gif"), save_all=True, append_images=preview[1:],
                    duration=int(1000 / args.fps), loop=0)

    print(f"{len(frames)} frames of {fs}x{fs} written to {folder}")
    print('Manifest "frames" for %s:' % name)
    print(",\n".join(f'        "{p}"' for p in paths))


if __name__ == "__main__":
    main()
