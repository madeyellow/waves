using System;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.Surfaces;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects.Modules
{
    /// <summary>
    /// Sparse preset entry for one actor on one surface.
    /// A null <see cref="Surface"/> or <see cref="Actor"/> is that axis's fallback.
    /// </summary>
    /// <typeparam name="TPayload">Effect data owned by the concrete module.</typeparam>
    [Serializable]
    public sealed class ModuleCell<TPayload>
    {
        [SerializeField] SurfaceTypeDefinition _surface;
        [SerializeField] ActorProfile _actor;
        [SerializeField] bool _enabled = true;
        [SerializeField] TPayload _data;

        /// <summary>Surface for this group. Null is the fallback surface.</summary>
        public SurfaceTypeDefinition Surface => _surface;

        /// <summary>Actor for this group. Null is the fallback actor.</summary>
        public ActorProfile Actor => _actor;

        /// <summary>False skips this group during resolve and keeps its data.</summary>
        public bool Enabled => _enabled;

        /// <summary>Module-specific effect data for this pair.</summary>
        public TPayload Data => _data;

        internal void Configure(SurfaceTypeDefinition surface, ActorProfile actor, bool enabled, TPayload data)
        {
            _surface = surface;
            _actor = actor;
            _enabled = enabled;
            _data = data;
        }

        internal void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        internal void EnsureData(TPayload data)
        {
            if (_data == null)
                _data = data;
        }
    }
}
