using MadeYellow.WAVES.Actors;
using UnityEngine;
using UnityEngine.Audio;

namespace MadeYellow.WAVES.AudioVisualEffects.Abstractions
{
    /// <summary>Plays a one-shot world sound for an emitter.</summary>
    public interface IWAVESAudioFX
    {
        /// <summary>
        /// Plays <paramref name="resource"/> at <paramref name="position"/> when the listener is inside <paramref name="cullingDistance"/>.
        /// Mixer, volume, spatial blend, and reverb mix are applied to the voice that plays it.
        /// A repeat of the same resource inside the debounce window is ignored.
        /// </summary>
        void Play(
            EntityId emitterId,
            ActorProfile actor,
            Vector3 position,
            float cullingDistance,
            float minDistance,
            AudioRolloffMode rolloff,
            float dopplerLevel,
            AudioResource resource,
            AudioMixerGroup mixerGroup,
            float volume,
            float spatialBlend,
            float reverbZoneMix);
    }
}
