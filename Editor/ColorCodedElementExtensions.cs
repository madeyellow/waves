using MadeYellow.WAVES;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Timeline fills derived from an element's opaque color.</summary>
    public static class ColorCodedElementExtensions
    {
        /// <summary>Quiet lane fill used when the element is not selected.</summary>
        public static Color GetBackgroundColor(this IColorCodedElement element)
        {
            return Lane(element, WAVESPalettePreferences.LaneAlpha);
        }

        /// <summary>Stronger lane fill used when the element is selected.</summary>
        public static Color GetSelectedBackgroundColor(this IColorCodedElement element)
        {
            return Lane(element, WAVESPalettePreferences.LaneSelectedAlpha);
        }

        static Color Lane(IColorCodedElement element, float alpha)
        {
            Color color = element != null ? element.Color : WAVESPalettePreferences.Neutral;
            color.a = alpha;
            return color;
        }
    }

    /// <summary>Opaque color wrapped as a <see cref="MadeYellow.WAVES.IColorCodedElement"/>.</summary>
    public readonly struct ColorCodedColor : IColorCodedElement
    {
        public Color Color { get; }

        public ColorCodedColor(Color color)
        {
            Color = WAVESPalette.Opaque(color);
        }
    }
}
