using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>A named ground material, such as grass, stone, or wood.</summary>
    [CreateAssetMenu(fileName = "SurfaceType", menuName = "MadeYellow/WAVES/Surface Type")]
    public sealed class SurfaceTypeDefinition : ScriptableObject, IColorCodedElement
    {
        /// <summary>Swatch used by the footstep preset editor. Alpha is kept at 1.</summary>
        [SerializeField]
        [ColorUsage(false, false)]
        Color _color = WAVESPalette.Neutral;

        [SerializeField]
        int _order;

        /// <summary>Position among surface types in the browser. Lower values come first.</summary>
        public int Order => _order;

        /// <summary>Swatch used by the footstep preset editor. Alpha is 1.</summary>
        public Color Color
        {
            get
            {
                Color color = _color;
                color.a = 1f;
                return color;
            }
        }

        void OnValidate()
        {
            _color.a = 1f;
        }
    }
}
