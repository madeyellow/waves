using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>
    /// Collider ids mapped to surface types, plus materials and textures loaded with those types.
    /// Marker and terrain tables are cleared on domain reload. Definitions register again from OnEnable.
    /// </summary>
    static class SurfaceRegistry
    {
        struct MarkerBinding
        {
            public EntityId OwnerId;
            public SurfaceTypeDefinition Type;
        }

        static readonly Dictionary<EntityId, MarkerBinding> Markers = new Dictionary<EntityId, MarkerBinding>();
        static readonly Dictionary<EntityId, TerrainSurfaceMap> Terrains = new Dictionary<EntityId, TerrainSurfaceMap>();
        static readonly List<SurfaceTypeDefinition> Definitions = new List<SurfaceTypeDefinition>();
        static readonly Dictionary<EntityId, SurfaceTypeDefinition> Materials = new Dictionary<EntityId, SurfaceTypeDefinition>();
        static readonly Dictionary<EntityId, SurfaceTypeDefinition> Textures = new Dictionary<EntityId, SurfaceTypeDefinition>();
        static readonly Dictionary<EntityId, SurfaceTypeDefinition> TextureHits = new Dictionary<EntityId, SurfaceTypeDefinition>();
        static readonly HashSet<EntityId> TextureMisses = new HashSet<EntityId>();
        static readonly Dictionary<EntityId, int[]> ShaderTextures = new Dictionary<EntityId, int[]>();
        static readonly List<Material> MaterialBuffer = new List<Material>(4);
        static bool _indexDirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Markers.Clear();
            Terrains.Clear();
            Definitions.Clear();
            ClearIndex();
        }

        /// <summary>Clears registrations and rebuilds the material index from loaded surface types. Used by tests.</summary>
        internal static void ResetForTests()
        {
            Markers.Clear();
            Terrains.Clear();
            Definitions.Clear();
            SurfaceTypeDefinition[] loaded = Resources.FindObjectsOfTypeAll<SurfaceTypeDefinition>();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null)
                    Definitions.Add(loaded[i]);
            }

            ClearIndex();
        }

        /// <summary>Adds <paramref name="type"/> to the material index. A second call marks the index dirty.</summary>
        internal static void Register(SurfaceTypeDefinition type)
        {
            if (type == null)
                return;

            if (!Definitions.Contains(type))
                Definitions.Add(type);

            _indexDirty = true;
        }

        /// <summary>Drops <paramref name="type"/> from the material index.</summary>
        internal static void Unregister(SurfaceTypeDefinition type)
        {
            if (type == null)
                return;

            if (Definitions.Remove(type))
                _indexDirty = true;
        }

        /// <summary>Rebuilds the material index on the next lookup.</summary>
        internal static void Touch()
        {
            _indexDirty = true;
        }

        /// <summary>Binds every collider id to a surface while <paramref name="ownerId"/> is the current owner.</summary>
        public static void RegisterMarker(EntityId colliderId, EntityId ownerId, SurfaceTypeDefinition type)
        {
            if (type == null)
                return;

            Markers[colliderId] = new MarkerBinding
            {
                OwnerId = ownerId,
                Type = type
            };
        }

        /// <summary>Removes a collider binding when <paramref name="ownerId"/> still owns it.</summary>
        public static void UnregisterMarker(EntityId colliderId, EntityId ownerId)
        {
            if (Markers.TryGetValue(colliderId, out MarkerBinding binding) && binding.OwnerId == ownerId)
                Markers.Remove(colliderId);
        }

        /// <summary>Binds a terrain collider id to its baked map.</summary>
        public static void RegisterTerrain(EntityId colliderId, TerrainSurfaceMap map)
        {
            if (map == null)
                return;

            Terrains[colliderId] = map;
        }

        /// <summary>Removes a terrain binding when <paramref name="map"/> is still the current map.</summary>
        public static void UnregisterTerrain(EntityId colliderId, TerrainSurfaceMap map)
        {
            if (Terrains.TryGetValue(colliderId, out TerrainSurfaceMap current) && current == map)
                Terrains.Remove(colliderId);
        }

        /// <summary>
        /// Marker first, then a terrain sample, then materials and textures on a mesh renderer.
        /// A terrain map owns the collider: a sample that misses does not fall through to materials.
        /// </summary>
        public static bool TryResolve(Collider collider, Vector3 point, out SurfaceTypeDefinition surface)
        {
            return TryResolve(collider, point, out surface, out _);
        }

        /// <summary>
        /// Marker first, then a terrain sample, then materials and textures on a mesh renderer.
        /// <paramref name="fromTerrain"/> is set when this collider has a terrain map, including a sample that missed.
        /// </summary>
        internal static bool TryResolve(Collider collider, Vector3 point, out SurfaceTypeDefinition surface, out bool fromTerrain)
        {
            surface = null;
            fromTerrain = false;
            if (collider == null)
                return false;

            if (TryResolve(collider.GetEntityId(), point, out surface, out fromTerrain))
                return true;

            if (fromTerrain)
                return false;

            return TryResolveRenderer(collider, out surface);
        }

        /// <summary>
        /// Marker first, then a terrain sample. <paramref name="fromTerrain"/> is set when this collider id has a terrain map, including a sample that missed.
        /// </summary>
        internal static bool TryResolve(EntityId colliderId, Vector3 point, out SurfaceTypeDefinition surface, out bool fromTerrain)
        {
            surface = null;
            fromTerrain = false;
            if (Markers.TryGetValue(colliderId, out MarkerBinding marker) && marker.Type != null)
            {
                surface = marker.Type;
                return true;
            }

            if (!Terrains.TryGetValue(colliderId, out TerrainSurfaceMap terrain))
                return false;

            fromTerrain = true;
            if (terrain.TrySample(point, out surface))
                return true;

            surface = null;
            return false;
        }

        static bool TryResolveRenderer(Collider collider, out SurfaceTypeDefinition surface)
        {
            surface = null;
            Renderer renderer = FindRenderer(collider);
            if (renderer == null)
                return false;

            EnsureIndex();
            renderer.GetSharedMaterials(MaterialBuffer);
            for (int i = 0; i < MaterialBuffer.Count; i++)
            {
                Material material = MaterialBuffer[i];
                if (material != null && Materials.TryGetValue(material.GetEntityId(), out surface))
                {
                    MaterialBuffer.Clear();
                    return true;
                }
            }

            for (int i = 0; i < MaterialBuffer.Count; i++)
            {
                Material material = MaterialBuffer[i];
                if (material != null && TryMatchTextures(material, out surface))
                {
                    MaterialBuffer.Clear();
                    return true;
                }
            }

            MaterialBuffer.Clear();
            surface = null;
            return false;
        }

        static Renderer FindRenderer(Collider collider)
        {
            MeshRenderer mesh = collider.GetComponent<MeshRenderer>();
            if (mesh != null)
                return mesh;

            SkinnedMeshRenderer skinned = collider.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null)
                return skinned;

            mesh = collider.GetComponentInChildren<MeshRenderer>(true);
            if (mesh != null)
                return mesh;

            skinned = collider.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned != null)
                return skinned;

            mesh = collider.GetComponentInParent<MeshRenderer>();
            if (mesh != null)
                return mesh;

            return collider.GetComponentInParent<SkinnedMeshRenderer>();
        }

        static bool TryMatchTextures(Material material, out SurfaceTypeDefinition surface)
        {
            EntityId id = material.GetEntityId();
            if (TextureHits.TryGetValue(id, out surface))
                return true;

            if (TextureMisses.Contains(id))
            {
                surface = null;
                return false;
            }

            surface = FindTexture(material);
            if (surface == null)
                TextureMisses.Add(id);
            else
                TextureHits.Add(id, surface);

            return surface != null;
        }

        static SurfaceTypeDefinition FindTexture(Material material)
        {
            Texture main = material.mainTexture;
            if (main != null && Textures.TryGetValue(main.GetEntityId(), out SurfaceTypeDefinition fromMain))
                return fromMain;

            int[] propertyIds = TextureProperties(material);
            for (int i = 0; i < propertyIds.Length; i++)
            {
                Texture texture = material.GetTexture(propertyIds[i]);
                if (texture != null && Textures.TryGetValue(texture.GetEntityId(), out SurfaceTypeDefinition found))
                    return found;
            }

            return null;
        }

        static int[] TextureProperties(Material material)
        {
            Shader shader = material.shader;
            if (shader == null)
                return System.Array.Empty<int>();

            EntityId id = shader.GetEntityId();
            if (ShaderTextures.TryGetValue(id, out int[] cached))
                return cached;

            int[] ids = material.GetTexturePropertyNameIDs();
            if (ids == null)
                ids = System.Array.Empty<int>();

            ShaderTextures.Add(id, ids);
            return ids;
        }

        static void EnsureIndex()
        {
            if (!_indexDirty)
                return;

            _indexDirty = false;
            Materials.Clear();
            Textures.Clear();
            TextureHits.Clear();
            TextureMisses.Clear();
            for (int i = Definitions.Count - 1; i >= 0; i--)
            {
                if (Definitions[i] == null)
                    Definitions.RemoveAt(i);
            }

            Definitions.Sort(CompareDefinition);
            for (int i = 0; i < Definitions.Count; i++)
            {
                SurfaceTypeDefinition type = Definitions[i];
                Index(type, type.MaterialCount, true);
                Index(type, type.TextureCount, false);
            }
        }

        static void Index(SurfaceTypeDefinition type, int count, bool materials)
        {
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object asset = materials ? type.GetMaterial(i) : type.GetTexture(i);
                if (asset == null)
                    continue;

                EntityId id = asset.GetEntityId();
                Dictionary<EntityId, SurfaceTypeDefinition> table = materials ? Materials : Textures;
                if (!table.ContainsKey(id))
                    table.Add(id, type);
            }
        }

        static int CompareDefinition(SurfaceTypeDefinition left, SurfaceTypeDefinition right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;

            int name = string.CompareOrdinal(left.name, right.name);
            if (name != 0)
                return name;

            return left.GetEntityId().GetHashCode().CompareTo(right.GetEntityId().GetHashCode());
        }

        static void ClearIndex()
        {
            Materials.Clear();
            Textures.Clear();
            TextureHits.Clear();
            TextureMisses.Clear();
            ShaderTextures.Clear();
            MaterialBuffer.Clear();
            _indexDirty = true;
        }
    }
}
