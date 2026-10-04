using System;
using System.Collections.Generic;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects.Abstractions;
using UnityEngine;
using UnityEngine.Audio;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>
    /// One AudioSource per emitter and audio resource, kept long enough for Audio Random Container sequences.
    /// The same resource is skipped when another play arrives inside the debounce window.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MadeYellow/WAVES/WAVES Audio FX")]
    public sealed class WAVESAudioFX : MonoBehaviour, IWAVESAudioFX
    {
        struct VoiceSlot
        {
            public AudioSource Source;
            public EntityId EmitterId;
            public EntityId ResourceId;
            public float ReleaseAt;
        }

        readonly struct VoiceKey : IEquatable<VoiceKey>
        {
            public readonly EntityId Emitter;
            public readonly EntityId Resource;

            public VoiceKey(EntityId emitter, EntityId resource)
            {
                Emitter = emitter;
                Resource = resource;
            }

            public bool Equals(VoiceKey other)
            {
                return Emitter == other.Emitter && Resource == other.Resource;
            }

            public override bool Equals(object obj)
            {
                return obj is VoiceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Emitter.GetHashCode() * 397) ^ Resource.GetHashCode();
                }
            }
        }

        /// <summary>Seconds. A second play of the same resource inside this window is dropped.</summary>
        [SerializeField, Min(0f)]
        [Tooltip("Seconds. A second play of the same audio resource inside this window is dropped, even from another emitter. 0 disables the skip.")]
        float _debounce = 0.075f;

        /// <summary>Seconds a voice stays reserved after it plays.</summary>
        [SerializeField, Min(0f)]
        [Tooltip("Seconds a voice stays reserved for its emitter and audio resource after a play, so a sequence can continue.")]
        float _holdSeconds = 5f;

        readonly List<VoiceSlot> _active = new List<VoiceSlot>(32);
        readonly Dictionary<VoiceKey, int> _activeIndex = new Dictionary<VoiceKey, int>(32);
        readonly Stack<AudioSource> _idle = new Stack<AudioSource>(16);
        readonly Dictionary<EntityId, float> _suppressUntil = new Dictionary<EntityId, float>(16);

        /// <inheritdoc />
        public void Play(
            EntityId emitterId,
            ActorProfile actor,
            Vector3 position,
            float cullingDistance,
            float minDistance,
            AudioRolloffMode rolloff,
            float dopplerLevel,
            AudioResource resource)
        {
            if (resource == null || WAVESView.BeyondListener(position, cullingDistance))
                return;

            EntityId resourceId = resource.GetEntityId();
            float now = Time.time;
            if (_debounce > 0f
                && _suppressUntil.TryGetValue(resourceId, out float until)
                && now < until)
                return;

            if (_debounce > 0f)
                _suppressUntil[resourceId] = now + _debounce;

            var key = new VoiceKey(emitterId, resourceId);
            if (_activeIndex.TryGetValue(key, out int index))
            {
                VoiceSlot slot = _active[index];
                if (slot.Source == null)
                {
                    ReleaseAt(index);
                }
                else
                {
                    Configure(slot.Source, resource, position, cullingDistance, minDistance, rolloff, dopplerLevel);
                    slot.Source.Play();
                    slot.ReleaseAt = now + _holdSeconds;
                    _active[index] = slot;
                    return;
                }
            }

            AudioSource source = TakeIdle();
            Configure(source, resource, position, cullingDistance, minDistance, rolloff, dopplerLevel);
            source.Play();
            _active.Add(new VoiceSlot
            {
                Source = source,
                EmitterId = emitterId,
                ResourceId = resourceId,
                ReleaseAt = now + _holdSeconds
            });
            _activeIndex[key] = _active.Count - 1;
        }

        void Update()
        {
            float now = Time.time;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                VoiceSlot slot = _active[i];
                bool expired = slot.ReleaseAt <= now || slot.Source == null;
                if (!expired && Resources.EntityIdToObject(slot.EmitterId) == null)
                    expired = true;

                if (expired)
                    ReleaseAt(i);
            }
        }

        void OnDisable()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                ReleaseAt(i);

            while (_idle.Count > 0)
            {
                AudioSource source = _idle.Pop();
                if (source != null)
                    Destroy(source.gameObject);
            }

            _suppressUntil.Clear();
        }

        void ReleaseAt(int index)
        {
            VoiceSlot slot = _active[index];
            _activeIndex.Remove(new VoiceKey(slot.EmitterId, slot.ResourceId));
            if (slot.Source != null)
            {
                slot.Source.Stop();
                slot.Source.resource = null;
                _idle.Push(slot.Source);
            }

            int last = _active.Count - 1;
            if (index != last)
            {
                VoiceSlot moved = _active[last];
                _active[index] = moved;
                _activeIndex[new VoiceKey(moved.EmitterId, moved.ResourceId)] = index;
            }

            _active.RemoveAt(last);
        }

        AudioSource TakeIdle()
        {
            while (_idle.Count > 0)
            {
                AudioSource source = _idle.Pop();
                if (source != null)
                    return source;
            }

            var go = new GameObject("WAVES Audio");
            go.transform.SetParent(transform, false);
            AudioSource created = go.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.loop = false;
            created.spatialBlend = 1f;
            return created;
        }

        static void Configure(
            AudioSource source,
            AudioResource resource,
            Vector3 position,
            float cullingDistance,
            float minDistance,
            AudioRolloffMode rolloff,
            float dopplerLevel)
        {
            float maxDistance = cullingDistance > 0f ? cullingDistance : 0f;
            float min = minDistance > 0f ? minDistance : 0f;
            if (min > maxDistance)
                min = maxDistance;

            source.transform.position = position;
            source.spatialBlend = 1f;
            source.dopplerLevel = dopplerLevel > 0f ? dopplerLevel : 0f;
            source.rolloffMode = rolloff;
            source.minDistance = min;
            source.maxDistance = maxDistance;
            if (source.resource != resource)
                source.resource = resource;
        }

    }
}
