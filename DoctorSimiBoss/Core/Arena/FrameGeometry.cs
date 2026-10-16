using System;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Arena
{
    /// <summary>Axis-aligned box relative to the sprite pivot, in world units (Y up).</summary>
    public struct LocalBox
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;
    }

    /// <summary>Converts rectangles measured on the 128x128 frames into boxes around the sprite pivot.</summary>
    public static class FrameGeometry
    {
        /// <param name="facing">-1 left, +1 right; mirrored when it differs from the manifest's spriteFacing.</param>
        public static LocalBox ToLocal(AnimationManifest manifest, PixelRect rect, int facing)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (rect == null) throw new ArgumentNullException(nameof(rect));
            float ppu = manifest.SpritePixelsPerUnit;
            if (ppu <= 0) throw new InvalidOperationException("spriteScale.pixelsPerUnit is not set");

            float pivotX = manifest.OriginX * manifest.FrameWidth;
            float pivotY = manifest.OriginY * manifest.FrameHeight;
            float centerX = (rect.X + rect.Width / 2f - pivotX) / ppu;
            float centerY = (pivotY - (rect.Y + rect.Height / 2f)) / ppu;

            int drawnFacing = manifest.SpriteFacing == "right" ? 1 : -1;
            if (facing != 0 && facing != drawnFacing) centerX = -centerX;

            return new LocalBox { CenterX = centerX, CenterY = centerY, Width = rect.Width / ppu, Height = rect.Height / ppu };
        }

        /// <summary>True when the sprite must be mirrored to face <paramref name="facing"/>.</summary>
        public static bool NeedsFlip(AnimationManifest manifest, int facing)
        {
            int drawnFacing = manifest.SpriteFacing == "right" ? 1 : -1;
            return facing != 0 && facing != drawnFacing;
        }
    }
}
