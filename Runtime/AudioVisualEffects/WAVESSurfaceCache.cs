using System.Collections.Generic;
using MadeYellow.WAVES.Surfaces;
using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>Marker lookups remembered by collider id. Terrain samples are not stored.</summary>
    sealed class WAVESSurfaceCache
    {
        /// <summary>Room for collider ids before the map has to grow.</summary>
        internal const int DefaultCapacity = 64;

        struct Slot
        {
            public SurfaceTypeDefinition Surface;
            public float Created;
            public bool Found;
        }

        readonly Dictionary<EntityId, Slot> _slots;

        internal bool Enabled;
        internal float Lifetime;

        internal WAVESSurfaceCache()
            : this(DefaultCapacity)
        {
        }

        internal WAVESSurfaceCache(int capacity)
        {
            if (capacity < 1)
                capacity = 1;

            _slots = new Dictionary<EntityId, Slot>(capacity);
        }

        /// <summary>
        /// Fresh entry for <paramref name="colliderId"/>. False when caching is off, the id is unknown, or the entry is older than <see cref="Lifetime"/>.
        /// </summary>
        internal bool TryGet(EntityId colliderId, float now, out SurfaceTypeDefinition surface, out bool found)
        {
            surface = null;
            found = false;
            if (!Enabled)
                return false;

            if (!_slots.TryGetValue(colliderId, out Slot slot))
                return false;

            if (now - slot.Created > Lifetime)
                return false;

            surface = slot.Surface;
            found = slot.Found;
            return true;
        }

        /// <summary>Remembers <paramref name="colliderId"/>. An existing entry is overwritten. Does nothing while caching is off.</summary>
        internal void Store(EntityId colliderId, bool found, SurfaceTypeDefinition surface, float created)
        {
            if (!Enabled)
                return;

            _slots[colliderId] = new Slot
            {
                Surface = found ? surface : null,
                Created = created,
                Found = found
            };
        }
    }
}
