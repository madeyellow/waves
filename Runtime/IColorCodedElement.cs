using UnityEngine;

namespace MadeYellow.WAVES
{
    /// <summary>Something drawn with its own opaque color. Alpha on the stored color is ignored.</summary>
    public interface IColorCodedElement
    {
        /// <summary>Element color with alpha forced to 1.</summary>
        Color Color { get; }
    }
}
