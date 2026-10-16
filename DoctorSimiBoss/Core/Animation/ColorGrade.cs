using System;

namespace DoctorSimiBoss.Core.Animation
{
    /// <summary>
    /// Colour adjustment applied to the boss and effect sprites when they are loaded, so bright
    /// AI-generated art sits in the dim, cool palette of Hollow Knight. Order: saturation,
    /// brightness, tint, contrast. The files on disk are never changed.
    /// </summary>
    public sealed class ColorGrade
    {
        public bool Enabled;
        /// <summary>1 keeps the colour, 0 is greyscale.</summary>
        public float Saturation = 1f;
        /// <summary>Multiplier; below 1 darkens.</summary>
        public float Brightness = 1f;
        /// <summary>Above 1 increases contrast around mid-grey.</summary>
        public float Contrast = 1f;
        public byte TintR = 255, TintG = 255, TintB = 255;
        /// <summary>0 = no tint, 1 = fully the tint colour.</summary>
        public float TintAmount;

        public static readonly ColorGrade None = new ColorGrade();

        public void Apply(ref byte r, ref byte g, ref byte b)
        {
            if (!Enabled) return;
            float fr = r, fg = g, fb = b;

            float grey = 0.299f * fr + 0.587f * fg + 0.114f * fb;
            fr = grey + (fr - grey) * Saturation;
            fg = grey + (fg - grey) * Saturation;
            fb = grey + (fb - grey) * Saturation;

            fr *= Brightness; fg *= Brightness; fb *= Brightness;

            fr += (TintR - fr) * TintAmount;
            fg += (TintG - fg) * TintAmount;
            fb += (TintB - fb) * TintAmount;

            fr = 128f + (fr - 128f) * Contrast;
            fg = 128f + (fg - 128f) * Contrast;
            fb = 128f + (fb - 128f) * Contrast;

            r = ToByte(fr); g = ToByte(fg); b = ToByte(fb);
        }

        private static byte ToByte(float v)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v)));
        }
    }
}
