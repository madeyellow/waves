using MadeYellow.WAVES.Surfaces;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>Reads a surface type from a collider at a world point.</summary>
    public sealed class WAVESQuery
    {
        /// <summary>
        /// Surface at this point on the collider. False when the collider is null or nothing is mapped to it.
        /// </summary>
        public bool TryGetSurface(Collider collider, Vector3 worldPosition, out SurfaceTypeDefinition surface)
        {
            return SurfaceRegistry.TryResolve(collider, worldPosition, out surface);
        }

        /// <summary>
        /// Surface for this hit. False when the ray missed or nothing is mapped to the collider.
        /// </summary>
        public bool TryGetSurface(in RaycastHit hit, out SurfaceTypeDefinition surface)
        {
            return TryGetSurface(hit.collider, hit.point, out surface);
        }
    }
}
