using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>
    /// A named ground material, such as grass, stone, or wood.
    /// Listed materials and textures identify it when a collider has no surface marker.
    /// </summary>
    [CreateAssetMenu(fileName = "SurfaceType", menuName = "MadeYellow/WAVES/Surface Type")]
    public sealed class SurfaceTypeDefinition : ScriptableObject, IColorCodedElement
    {
        /// <summary>Swatch used by the footstep preset editor. Alpha is kept at 1.</summary>
        [SerializeField]
        [ColorUsage(false, false)]
        Color _color = WAVESPalette.Neutral;

        [SerializeField]
        int _order;

        [SerializeField]
        [Tooltip("Names matched against materials and textures. * matches any text. Case is ignored.")]
        string[] _keywords;

        [SerializeField]
        [Tooltip("Materials that resolve to this surface when the collider has no marker.")]
        Material[] _materials;

        [SerializeField]
        [Tooltip("Textures that resolve to this surface when none of the renderer's materials are listed.")]
        Texture[] _textures;

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

        /// <summary>Keyword count. Empty names are stored as they were typed.</summary>
        public int KeywordCount => _keywords == null ? 0 : _keywords.Length;

        /// <summary>Keyword at <paramref name="index"/>.</summary>
        public string GetKeyword(int index)
        {
            return _keywords[index];
        }

        /// <summary>Materials that identify this surface.</summary>
        public int MaterialCount => _materials == null ? 0 : _materials.Length;

        /// <summary>Material at <paramref name="index"/>.</summary>
        public Material GetMaterial(int index)
        {
            return _materials[index];
        }

        /// <summary>Textures that identify this surface when no listed material is on the renderer.</summary>
        public int TextureCount => _textures == null ? 0 : _textures.Length;

        /// <summary>Texture at <paramref name="index"/>.</summary>
        public Texture GetTexture(int index)
        {
            return _textures[index];
        }

        void OnEnable()
        {
            SurfaceRegistry.Register(this);
        }

        void OnDisable()
        {
            SurfaceRegistry.Unregister(this);
        }

        void OnValidate()
        {
            _color.a = 1f;
            SurfaceRegistry.Touch();
        }

        internal void SetOrder(int order)
        {
            _order = order;
            SurfaceRegistry.Touch();
        }

        internal void UseBindingsForTests(Material[] materials, Texture[] textures)
        {
            _materials = materials ?? new Material[0];
            _textures = textures ?? new Texture[0];
            SurfaceRegistry.Touch();
        }
    }
}
