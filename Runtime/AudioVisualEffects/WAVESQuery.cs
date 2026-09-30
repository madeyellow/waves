using MadeYellow.WAVES.Surfaces;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>Reads a surface type from a footstep hit.</summary>
    public sealed class WAVESQuery
    {
        /// <summary>
        /// Surface for this hit. False when the ray missed or nothing is mapped to the collider.
        /// </summary>
        public bool TryGetSurface(in RaycastHit hit, out SurfaceTypeDefinition surface)
        {
            return SurfaceRegistry.TryResolve(hit.collider, hit.point, out surface);
        }
    }
}
