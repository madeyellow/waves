using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Surfaces
{
    /// <summary>Writes this surface onto the colliders of this object and its children.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MadeYellow/WAVES/Surface Marker")]
    public sealed class SurfaceMarker : MonoBehaviour
    {
        static readonly List<Collider> ColliderBuffer = new List<Collider>();

        /// <summary>Surface used for these colliders.</summary>
        [SerializeField]
        [Tooltip("Surface of the colliders on this object and its children. A marker wins over a terrain map.")]
        SurfaceTypeDefinition _surface;

        int _ownerId;

        /// <summary>Surface used for these colliders.</summary>
        public SurfaceTypeDefinition Surface => _surface;

        void OnEnable()
        {
            if (!Application.isPlaying || _surface == null)
                return;

            _ownerId = GetInstanceID();
            CollectColliders();
            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                Collider collider = ColliderBuffer[i];
                if (collider != null)
                    SurfaceRegistry.RegisterMarker(collider.GetInstanceID(), _ownerId, _surface);
            }

            ColliderBuffer.Clear();
        }

        void OnDisable()
        {
            if (!Application.isPlaying || _ownerId == 0)
                return;

            CollectColliders();
            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                Collider collider = ColliderBuffer[i];
                if (collider != null)
                    SurfaceRegistry.UnregisterMarker(collider.GetInstanceID(), _ownerId);
            }

            ColliderBuffer.Clear();
            _ownerId = 0;
        }

        void CollectColliders()
        {
            ColliderBuffer.Clear();
            GetComponentsInChildren(true, ColliderBuffer);
        }
    }
}
