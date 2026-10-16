#!/usr/bin/env python3
"""Make AI-ready reference sheets from the existing 3x2 spritesheets.

Each Sprites/<Anim>/<anim>_spritesheet_128.png (384x256, transparent) is enlarged x4 with
nearest-neighbour (crisp pixels, 1536x1024) and placed on flat green #00FF00, the background
ArtGuide.md asks the AI to keep. Originals are not modified.

  python make_reference_sheets.py --sprites ../Sprites --out ./reference_sheets
"""
import argparse
import glob
import os

from PIL import Image


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--sprites", default="../Sprites")
    ap.add_argument("--out", default="./reference_sheets")
    ap.add_argument("--scale", type=int, default=4)
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    for path in sorted(glob.glob(os.path.join(args.sprites, "*", "*_spritesheet_128.png"))):
        anim = os.path.basename(os.path.dirname(path))
        sheet = Image.open(path).convert("RGBA")
        big = sheet.resize((sheet.width * args.scale, sheet.height * args.scale), Image.NEAREST)
        canvas = Image.new("RGBA", big.size, (0, 255, 0, 255))
        canvas.alpha_composite(big)
        out = os.path.join(args.out, f"{anim}_reference.png")
        canvas.convert("RGB").save(out, optimize=True)
        print(f"{anim}: {out} ({canvas.width}x{canvas.height})")


if __name__ == "__main__":
    main()
