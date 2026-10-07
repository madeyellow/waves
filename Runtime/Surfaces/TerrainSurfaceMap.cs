using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>Dominant terrain layer at a point, mapped to a surface type through its textures.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    [RequireComponent(typeof(TerrainCollider))]
    [AddComponentMenu("MadeYellow/WAVES/Terrain Surface Map")]
    public sealed class TerrainSurfaceMap : MonoBehaviour
    {
        Terrain _terrain;
        TerrainCollider _collider;
        byte[] _dominant;
        SurfaceTypeDefinition[] _layerToSurface;
        int _width;
        int _height;

        void Awake()
        {
            if (!Application.isPlaying)
                return;

            _terrain = GetComponent<Terrain>();
            _collider = GetComponent<TerrainCollider>();
            BuildMap();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _terrain = GetComponent<Terrain>();
            _collider = GetComponent<TerrainCollider>();
            if (_collider != null)
                SurfaceRegistry.RegisterTerrain(_collider.GetEntityId(), this);

            if (_dominant == null)
                BuildMap();
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_collider != null)
                SurfaceRegistry.UnregisterTerrain(_collider.GetEntityId(), this);

            _dominant = null;
            _layerToSurface = null;
            _width = 0;
            _height = 0;
        }

        /// <summary>Installs a grid for tests. Cells are row-major, and each value is a layer index into <paramref name="surfaces"/>.</summary>
        internal void UseMapForTests(SurfaceTypeDefinition[] surfaces, byte[] dominant, int width, int height)
        {
            _terrain = GetComponent<Terrain>();
            if (_terrain.terrainData == null)
                _terrain.terrainData = new TerrainData();

            _terrain.terrainData.size = new Vector3(Mathf.Max(width, 1), 1f, Mathf.Max(height, 1));
            _width = width;
            _height = height;
            _dominant = dominant;
            _layerToSurface = surfaces;
        }

        /// <summary>Rebuilds the layer map from each layer's textures. Used by tests.</summary>
        internal void RebuildForTests()
        {
            _terrain = GetComponent<Terrain>();
            _collider = GetComponent<TerrainCollider>();
            BuildMap();
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
                _layerToSurface[i] = ResolveLayer(layer);
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

        SurfaceTypeDefinition ResolveLayer(TerrainLayer layer)
        {
            if (layer == null)
                return null;

            if (SurfaceRegistry.TryGetSurface(layer.diffuseTexture, out SurfaceTypeDefinition fromDiffuse))
                return fromDiffuse;

            if (SurfaceRegistry.TryGetSurface(layer.normalMapTexture, out SurfaceTypeDefinition fromNormal))
                return fromNormal;

            if (SurfaceRegistry.TryGetSurface(layer.maskMapTexture, out SurfaceTypeDefinition fromMask))
                return fromMask;

            return null;
        }
    }
}
