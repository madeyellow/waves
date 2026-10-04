using MadeYellow.WAVES.Actors;
using UnityEngine;
using UnityEngine.VFX;

namespace MadeYellow.WAVES.AudioVisualEffects.Abstractions
{
    /// <summary>Plays a one-shot world visual for an emitter.</summary>
    public interface IWAVESVisualFX
    {
        /// <summary>Emits <paramref name="particles"/> at <paramref name="position"/> when the camera is inside <paramref name="cullingDistance"/>.</summary>
        void PlayParticles(
            EntityId emitterId,
            ActorProfile actor,
            Vector3 position,
            Quaternion rotation,
            float cullingDistance,
            ParticleSystem particles);

        /// <summary>
        /// Sends a play event to <paramref name="graph"/> with position and angle attributes.
        /// Ignored when the camera is outside <paramref name="cullingDistance"/>.
        /// </summary>
        void PlayGraph(
            EntityId emitterId,
            ActorProfile actor,
            Vector3 position,
            Quaternion rotation,
            float cullingDistance,
            VisualEffectAsset graph);
    }
}
