using System;
using DoctorSimiBoss.Core.Manifest;

namespace DoctorSimiBoss.Core.Arena
{
    /// <summary>
    /// Arena positions in world units, derived from the background image measurements.
    /// The image's bottom-left corner sits at world (0, 0) with Y pointing up, so the scene
    /// layer only has to place the background there and offset everything by the same origin.
    /// </summary>
    public sealed class ArenaLayout
    {
        public ArenaInfo Info { get; }
        public float PixelsPerUnit { get; }

        public float Width => Info.ImageWidth / PixelsPerUnit;
        public float Height => Info.ImageHeight / PixelsPerUnit;
        public float FloorY => ToWorldY(Info.FloorY);
        public float LeftWallX => ToWorldX(Info.LeftWallX);
        public float RightWallX => ToWorldX(Info.RightWallX);
        public float BossLandingX => ToWorldX(Info.BossLandingX);
        public float PlayerEntryX => ToWorldX(Info.PlayerEntryX);

        public ArenaLayout(ArenaInfo info)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            if (info.PixelsPerUnit <= 0) throw new ArgumentException("Arena pixelsPerUnit must be positive", nameof(info));
            PixelsPerUnit = info.PixelsPerUnit;
        }

        public float ToWorldX(float pixelX) { return pixelX / PixelsPerUnit; }

        /// <summary>Image rows grow downwards; world Y grows upwards.</summary>
        public float ToWorldY(float pixelY) { return (Info.ImageHeight - pixelY) / PixelsPerUnit; }

        /// <summary>Keeps an X position between the invisible walls, leaving room for half a body width.</summary>
        public float ClampX(float worldX, float halfWidth)
        {
            float min = LeftWallX + halfWidth, max = RightWallX - halfWidth;
            if (min > max) return (LeftWallX + RightWallX) / 2f;
            return Math.Max(min, Math.Min(max, worldX));
        }
    }
}
