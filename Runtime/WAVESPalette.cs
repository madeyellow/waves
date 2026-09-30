using UnityEngine;

namespace MadeYellow.WAVES
{
    /// <summary>Default colors used by WAVES editors and new color-coded assets.</summary>
    /// <remarks>The editor can override these from Preferences/WAVES. Asset fields keep the value saved on the asset.</remarks>
    public static class WAVESPalette
    {
        public static readonly Color Track0 = new Color(0.95f, 0.55f, 0.2f, 1f);
        public static readonly Color Track1 = new Color(0.36f, 0.70f, 0.42f, 1f);
        public static readonly Color Track2 = new Color(0.42f, 0.56f, 0.86f, 1f);
        public static readonly Color Track3 = new Color(0.70f, 0.42f, 0.72f, 1f);
        public static readonly Color Track4 = new Color(0.86f, 0.40f, 0.38f, 1f);
        public static readonly Color Track5 = new Color(0.28f, 0.66f, 0.70f, 1f);

        public static readonly Color Playhead = new Color(235f / 255f, 148f / 255f, 131f / 255f, 1f);
        public static readonly Color StepBorder = new Color(0f, 0f, 0f, 0.28f);
        public static readonly Color GridLine = new Color(0f, 0f, 0f, 0.22f);

        public const float LaneAlpha = 0.08f;
        public const float LaneSelectedAlpha = 0.14f;
        public const float NameAlpha = 0.35f;
        public const float NameSelectedAlpha = 1f;

        public static readonly Color Title = new Color32(0x21, 0x21, 0x21, 0xFF);
        public static readonly Color Subtitle = new Color(0.129f, 0.129f, 0.129f, 0.55f);
        public static readonly Color Ink = new Color(0.08f, 0.08f, 0.08f, 1f);
        public static readonly Color InkStrong = new Color(0.12f, 0.12f, 0.12f, 1f);
        public static readonly Color Connected = new Color(0.55f, 0.78f, 0.56f, 1f);
        public static readonly Color Missing = new Color(0.86f, 0.48f, 0.45f, 1f);
        public static readonly Color Neutral = new Color(0.62f, 0.62f, 0.62f, 1f);
        public static readonly Color Actor = new Color(0.28f, 0.28f, 0.28f, 1f);
        public static readonly Color FallbackSurface = new Color(0.42f, 0.42f, 0.42f, 1f);

        public static readonly Color Gradient0 = new Color32(0xB4, 0xE3, 0xF6, 0xFF);
        public static readonly Color Gradient1 = new Color32(0xC5, 0xD4, 0xF8, 0xFF);
        public static readonly Color Gradient2 = new Color32(0xE3, 0xC6, 0xF1, 0xFF);
        public static readonly Color Gradient3 = new Color32(0xF6, 0xC4, 0xE3, 0xFF);
        public static readonly Color Gradient4 = new Color32(0xFF, 0xD4, 0xBE, 0xFF);
        public static readonly Color Gradient5 = new Color32(0xFF, 0xE6, 0xC2, 0xFF);

        public static readonly Color RowLeft = new Color32(0x2C, 0x32, 0x3C, 0xFF);
        public static readonly Color RowRight = new Color32(0x38, 0x33, 0x38, 0xFF);
        public static readonly Color RowHoverPro = new Color(1f, 1f, 1f, 0.14f);
        public static readonly Color RowHoverPersonal = new Color(0f, 0f, 0f, 0.18f);
        public static readonly Color RackPreset = new Color(1f, 1f, 1f, 0.5f);
        public static readonly Color RemoveIdle = new Color(1f, 1f, 1f, 0.82f);

        public static readonly Color BrandLeft = new Color32(0xFF, 0xA9, 0x8F, 0xFF);
        public static readonly Color BrandRight = new Color32(0xD5, 0xA4, 0x9A, 0xFF);

        public static readonly Color SectionHeaderPro = new Color(0.30f, 0.30f, 0.30f, 1f);
        public static readonly Color SectionHeaderPersonal = new Color(0.68f, 0.68f, 0.68f, 1f);
        public static readonly Color SectionContentPro = new Color(0.16f, 0.16f, 0.16f, 1f);
        public static readonly Color SectionContentPersonal = new Color(0.74f, 0.74f, 0.74f, 1f);
        public static readonly Color StepHeaderPro = new Color(0.36f, 0.36f, 0.36f, 1f);
        public static readonly Color StepHeaderPersonal = new Color(0.62f, 0.62f, 0.62f, 1f);
        public static readonly Color StepContentPro = new Color(0.18f, 0.18f, 0.18f, 1f);
        public static readonly Color StepContentPersonal = new Color(0.78f, 0.78f, 0.78f, 1f);

        public static readonly Color EffectFillPro = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color EffectFillPersonal = new Color(0f, 0f, 0f, 0.05f);
        public static readonly Color EffectLinePro = new Color(1f, 1f, 1f, 0.18f);
        public static readonly Color EffectLinePersonal = new Color(0f, 0f, 0f, 0.18f);
        public static readonly Color DropLabelPro = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color DropLabelPersonal = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        public static readonly Color OutlineTextPro = new Color(1f, 1f, 1f, 0.78f);
        public static readonly Color OutlineTextPersonal = new Color(0.12f, 0.12f, 0.12f, 0.85f);

        public static Color TrackSwatch(int index)
        {
            switch (index % 6)
            {
                case 0: return Track0;
                case 1: return Track1;
                case 2: return Track2;
                case 3: return Track3;
                case 4: return Track4;
                default: return Track5;
            }
        }

        public static Color White(float alpha)
        {
            return new Color(1f, 1f, 1f, alpha);
        }

        public static Color Black(float alpha)
        {
            return new Color(0f, 0f, 0f, alpha);
        }

        public static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }
    }
}
