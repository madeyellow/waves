using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>Collider ids mapped to surface types. Cleared on domain reload.</summary>
    static class SurfaceRegistry
    {
        struct MarkerBinding
        {
            public EntityId OwnerId;
            public SurfaceTypeDefinition Type;
        }

        static readonly Dictionary<EntityId, MarkerBinding> Markers = new Dictionary<EntityId, MarkerBinding>();
        static readonly Dictionary<EntityId, TerrainSurfaceMap> Terrains = new Dictionary<EntityId, TerrainSurfaceMap>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Markers.Clear();
            Terrains.Clear();
        }

        /// <summary>Clears registrations. Used by tests.</summary>
        internal static void ResetForTests()
        {
            Markers.Clear();
            Terrains.Clear();
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
        /// Marker first, then a terrain sample. False when neither names a surface.
        /// </summary>
        public static bool TryResolve(Collider collider, Vector3 point, out SurfaceTypeDefinition surface)
        {
            surface = null;
            if (collider == null)
                return false;

            EntityId id = collider.GetEntityId();
            if (Markers.TryGetValue(id, out MarkerBinding marker) && marker.Type != null)
            {
                surface = marker.Type;
                return true;
            }

            if (Terrains.TryGetValue(id, out TerrainSurfaceMap terrain)
                && terrain.TrySample(point, out surface))
                return true;

            surface = null;
            return false;
        }
    }
}
