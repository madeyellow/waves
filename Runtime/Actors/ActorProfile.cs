using UnityEngine;

namespace MadeYellow.WAVES.Actors
{
    /// <summary>Kind of actor that performs footsteps, such as small, normal, or huge.</summary>
    [CreateAssetMenu(fileName = "ActorProfile", menuName = "MadeYellow/WAVES/Actor Profile")]
    public sealed class ActorProfile : ScriptableObject, IColorCodedElement
    {
        /// <summary>Swatch used by the footstep preset editor. Alpha is kept at 1.</summary>
        [SerializeField]
        [ColorUsage(false, false)]
        Color _color = WAVESPalette.Actor;

        [SerializeField]
        int _order;

        /// <summary>Position among actor profiles in the browser. Lower values come first.</summary>
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
