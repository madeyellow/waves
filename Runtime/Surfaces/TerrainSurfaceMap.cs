using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>Dominant terrain layer at a point, mapped to a surface type.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    [RequireComponent(typeof(TerrainCollider))]
    [AddComponentMenu("MadeYellow/WAVES/Terrain Surface Map")]
    public sealed class TerrainSurfaceMap : MonoBehaviour
    {
        /// <summary>Which terrain layer is which surface. An empty surface is not a match.</summary>
        [SerializeField]
        [Tooltip("Surface for each terrain layer. An empty slot is not a match, so the footstep preset uses its fallback.")]
        List<TerrainLayerBinding> _bindings = new List<TerrainLayerBinding>();

        Terrain _terrain;
        TerrainCollider _collider;
        byte[] _dominant;
        SurfaceTypeDefinition[] _layerToSurface;
        int _width;
        int _height;

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _terrain = GetComponent<Terrain>();
            _collider = GetComponent<TerrainCollider>();
            if (_collider != null)
                SurfaceRegistry.RegisterTerrain(_collider.GetInstanceID(), this);

            BuildMap();
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_collider != null)
                SurfaceRegistry.UnregisterTerrain(_collider.GetInstanceID(), this);

            _dominant = null;
            _layerToSurface = null;
            _width = 0;
            _height = 0;
        }

        /// <summary>Surface at a world point. False outside the map or when that layer is unbound.</summary>
        public bool TrySample(Vector3 worldPosition, out SurfaceTypeDefinition surface)
        {
            surface = null;
            if (_dominant == null || _terrain == null || _terrain.terrainData == null)
                return false;

            TerrainData data = _terrain.terrainData;
            Vector3 local = _terrain.transform.InverseTransformPoint(worldPosition);
            float sizeX = data.size.x;
            float sizeZ = data.size.z;
            float nx = sizeX > 0f ? local.x / sizeX : 0f;
            float nz = sizeZ > 0f ? local.z / sizeZ : 0f;
            if (nx < 0f || nz < 0f || nx > 1f || nz > 1f)
                return false;

            int x = (int)(nx * _width);
            int z = (int)(nz * _height);
            if (x < 0)
                x = 0;
            else if (x >= _width)
                x = _width - 1;
            if (z < 0)
                z = 0;
            else if (z >= _height)
                z = _height - 1;

            int layer = _dominant[z * _width + x];
            if (_layerToSurface == null || (uint)layer >= (uint)_layerToSurface.Length)
                return false;

            surface = _layerToSurface[layer];
            return surface != null;
        }

        void BuildMap()
        {
            _dominant = null;
            _layerToSurface = null;
            if (_terrain == null || _terrain.terrainData == null)
                return;

            TerrainData data = _terrain.terrainData;
            _width = data.alphamapWidth;
            _height = data.alphamapHeight;
            int layers = data.alphamapLayers;
            if (_width <= 0 || _height <= 0 || layers <= 0)
                return;

            TerrainLayer[] terrainLayers = data.terrainLayers;
            _layerToSurface = new SurfaceTypeDefinition[layers];
            for (int i = 0; i < layers; i++)
            {
                TerrainLayer layer = terrainLayers != null && i < terrainLayers.Length ? terrainLayers[i] : null;
                _layerToSurface[i] = FindBinding(layer);
            }

            float[,,] alpha = data.GetAlphamaps(0, 0, _width, _height);
            _dominant = new byte[_width * _height];
            int layerLimit = layers < 255 ? layers : 255;
            for (int z = 0; z < _height; z++)
            {
                int row = z * _width;
                for (int x = 0; x < _width; x++)
                {
                    int best = 0;
                    float bestWeight = alpha[z, x, 0];
                    for (int layer = 1; layer < layerLimit; layer++)
                    {
                        float weight = alpha[z, x, layer];
                        if (weight > bestWeight)
                        {
                            bestWeight = weight;
                            best = layer;
                        }
                    }

                    _dominant[row + x] = (byte)best;
                }
            }
        }

        SurfaceTypeDefinition FindBinding(TerrainLayer layer)
        {
            if (layer == null || _bindings == null)
                return null;

            for (int i = 0; i < _bindings.Count; i++)
            {
                TerrainLayerBinding binding = _bindings[i];
                if (binding != null && binding.Layer == layer)
                    return binding.Surface;
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>Adds a slot for every layer on this terrain. Assigned surfaces are kept.</summary>
        [ContextMenu("Bind Terrain Layers")]
        void BindLayers()
        {
            Terrain terrain = GetComponent<Terrain>();
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null)
                return;

            UnityEditor.Undo.RecordObject(this, "Bind Terrain Layers");
            TerrainLayer[] layers = data.terrainLayers;
            var next = new List<TerrainLayerBinding>();
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    TerrainLayer layer = layers[i];
                    if (layer == null)
                        continue;

                    var binding = new TerrainLayerBinding();
                    binding.Set(layer, FindBinding(layer));
                    next.Add(binding);
                }
            }

            _bindings = next;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>One terrain layer and the surface it represents.</summary>
    [System.Serializable]
    public sealed class TerrainLayerBinding
    {
        [SerializeField] TerrainLayer _layer;
        [SerializeField] SurfaceTypeDefinition _surface;

        /// <summary>Layer on this terrain.</summary>
        public TerrainLayer Layer => _layer;

        /// <summary>Surface for <see cref="Layer"/>. Null means the layer is not a match.</summary>
        public SurfaceTypeDefinition Surface => _surface;

        /// <summary>Writes both sides of the pair.</summary>
        public void Set(TerrainLayer layer, SurfaceTypeDefinition surface)
        {
            _layer = layer;
            _surface = surface;
        }
    }
}
