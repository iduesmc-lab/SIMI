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


def segment_by_components(keyed, cols, rows, threshold, step=4, merge=1, min_pixels=6):
    """Split a keyed sheet into one image per grid cell using connected components.

    Poses drawn by AI often cross the grid lines a little (boots, punches, sparks), so cutting
    on the grid would chop them. Instead the opaque mask is downsampled by `step`, dilated by
    `merge` cells so sparks join their pose, labelled, and each component goes to the cell that
    contains its centre. Each cell's image keeps only its own components.
    """
    w, h = keyed.size
    sw, sh = (w + step - 1) // step, (h + step - 1) // step
    alpha = keyed.getchannel("A").point(lambda v: 255 if v > threshold else 0).resize((sw, sh), Image.BOX)
    occ = [[alpha.getpixel((x, y)) > 0 for x in range(sw)] for y in range(sh)]
    grown = [[False] * sw for _ in range(sh)]
    for y in range(sh):
        for x in range(sw):
            if occ[y][x]:
                for yy in range(max(0, y - merge), min(sh, y + merge + 1)):
                    row = grown[yy]
                    for xx in range(max(0, x - merge), min(sw, x + merge + 1)):
                        row[xx] = True
    label = [[-1] * sw for _ in range(sh)]
    comps = []
    for y in range(sh):
        for x in range(sw):
            if not grown[y][x] or label[y][x] >= 0:
                continue
            idx = len(comps)
            stack, cells, real = [(x, y)], [], 0
            label[y][x] = idx
            while stack:
                cx, cy = stack.pop()
                cells.append((cx, cy))
                if occ[cy][cx]:
                    real += 1
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < sw and 0 <= ny < sh and grown[ny][nx] and label[ny][nx] < 0:
                        label[ny][nx] = idx
                        stack.append((nx, ny))
            comps.append((cells, real))

    cw, ch = sw / cols, sh / rows
    owner = []
    for cells, real in comps:
        if real < min_pixels:
            owner.append(None)
            continue
        occ_cells = [(x, y) for x, y in cells if occ[y][x]]
        mx = sum(x for x, _ in occ_cells) / len(occ_cells)
        my = sum(y for _, y in occ_cells) / len(occ_cells)
        owner.append(min(rows - 1, int(my // ch)) * cols + min(cols - 1, int(mx // cw)))

    results = []
    for cell_index in range(cols * rows):
        mine = [i for i, o in enumerate(owner) if o == cell_index]
        if not mine:
            results.append(None)
            continue
        xs = [x for i in mine for x, _ in comps[i][0]]
        ys = [y for i in mine for _, y in comps[i][0]]
        box = (min(xs) * step, min(ys) * step, min(w, (max(xs) + 1) * step), min(h, (max(ys) + 1) * step))
        crop = keyed.crop(box)
        px = crop.load()
        keep = set(mine)
        for yy in range(crop.height):
            ly = min(sh - 1, (yy + box[1]) // step)
            for xx in range(crop.width):
                lx = min(sw - 1, (xx + box[0]) // step)
                if label[ly][lx] not in keep:
                    px[xx, yy] = (0, 0, 0, 0)
        results.append(crop)
    return results


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
    ap.add_argument("--scale", type=float,
                    help="fixed source-to-frame scale for every frame (use the same value for all animations "
                         "of a character so it keeps its size); overrides --char-height")
    ap.add_argument("--anchor", choices=["bottom", "center"], default="bottom",
                    help="bottom: feet on --baseline (characters, ground waves); center: centred (impacts, bursts)")
    ap.add_argument("--merge", type=int, default=1,
                    help="components closer than about 4*merge px are joined (default 1)")
    ap.add_argument("--segment", choices=["components", "grid"], default="components",
                    help="components (default) keeps poses that cross the grid lines whole; grid cuts on the lines")
    args = ap.parse_args()

    sheet = Image.open(args.sheet).convert("RGBA")
    count = args.frames or args.cols * args.rows
    cw, ch = sheet.width / args.cols, sheet.height / args.rows

    key = None if args.key.lower() == "none" else tuple(int(args.key.lstrip("#")[i:i + 2], 16) for i in (0, 2, 4))
    keyed = chroma_to_alpha(sheet, key, args.tolerance, args.softness) if key else sheet

    if args.segment == "components":
        parts = segment_by_components(keyed, args.cols, args.rows, args.alpha_threshold, merge=args.merge)[:count]
    else:
        parts = [keyed.crop((round((i % args.cols) * cw), round((i // args.cols) * ch),
                             round((i % args.cols + 1) * cw), round((i // args.cols + 1) * ch))) for i in range(count)]
    cells = []
    for i, cell in enumerate(parts):
        box = alpha_bbox(cell, args.alpha_threshold) if cell is not None else None
        if box is None:
            sys.exit(f"cell {i} (col {i % args.cols}, row {i // args.cols}) is empty after removing the background")
        cells.append(cell.crop(box))

    tallest = max(img.height for img in cells)
    fs = args.frame_size
    base_y = round(args.baseline * fs)
    frames = []
    for i, img in enumerate(cells):
        if args.scale:
            scale = args.scale
        else:
            scale = (args.char_height / img.height) if args.no_scale_lock else (args.char_height / tallest)
        sized = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
        limit_h = fs if args.anchor == "center" else base_y
        if sized.width > fs or sized.height > limit_h:
            print(f"warning: frame {i} ({sized.width}x{sized.height}) does not fit; shrinking it", file=sys.stderr)
            f = min(fs / sized.width, limit_h / sized.height)
            sized = sized.resize((max(1, int(sized.width * f)), max(1, int(sized.height * f))), Image.LANCZOS)
        frame = Image.new("RGBA", (fs, fs), (0, 0, 0, 0))
        y = (fs - sized.height) // 2 if args.anchor == "center" else base_y - sized.height
        frame.alpha_composite(sized, ((fs - sized.width) // 2, y))
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
