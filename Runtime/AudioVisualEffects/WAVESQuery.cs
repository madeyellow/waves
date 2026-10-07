using MadeYellow.WAVES.Surfaces;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>Reads a surface type from a collider at a world point.</summary>
    public sealed class WAVESQuery
    {
        readonly WAVES _waves;
        readonly WAVESSurfaceCache _cache;

        /// <summary>Query with no remembered surfaces.</summary>
        public WAVESQuery()
        {
        }

        internal WAVESQuery(WAVESSurfaceCache cache)
        {
            _cache = cache;
        }

        internal WAVESQuery(WAVES waves, WAVESSurfaceCache cache)
        {
            _waves = waves;
            _cache = cache;
        }

        /// <summary>
        /// Surface at this point on the collider. A remembered marker or material match is reused until its lifetime ends.
        /// Terrain is sampled every time. A marker wins, then a terrain map, then materials and textures on a mesh renderer.
        /// False when the collider is null or nothing identifies a surface.
        /// </summary>
        public bool TryGetSurface(Collider collider, Vector3 worldPosition, out SurfaceTypeDefinition surface)
        {
            return TryGetSurface(collider, worldPosition, Time.unscaledTime, out surface);
        }

        /// <summary>
        /// Surface for this hit. False when the ray missed or nothing identifies a surface.
        /// </summary>
        public bool TryGetSurface(in RaycastHit hit, out SurfaceTypeDefinition surface)
        {
            return TryGetSurface(hit.collider, hit.point, out surface);
        }

        internal bool TryGetSurface(Collider collider, Vector3 worldPosition, float now, out SurfaceTypeDefinition surface)
        {
            surface = null;
            if (collider == null)
                return false;

            PullSettings();
            EntityId id = collider.GetEntityId();
            if (_cache != null && _cache.TryGet(id, now, out surface, out bool found))
                return found;

            bool resolved = SurfaceRegistry.TryResolve(collider, worldPosition, out surface, out bool fromTerrain);
            if (!fromTerrain && _cache != null)
                _cache.Store(id, resolved, surface, now);

            return resolved;
        }

        void PullSettings()
        {
            if (_waves == null || _cache == null)
                return;

            _cache.Enabled = _waves.CacheSurfaces;
            _cache.Lifetime = _waves.SurfaceCacheLifetime;
        }
    }
}
