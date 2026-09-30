using System;
using MadeYellow.WAVES.Actors;
using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>A footstep that just entered contact, tagged with the actor that made it.</summary>
    [Serializable]
    public struct ActorFootstepStarted
    {
        /// <summary>Instance id of the GameObject that published the step.</summary>
        public int emitterId;

        /// <summary>Kind of actor that started the step. Null when the dispatcher has no profile.</summary>
        public ActorProfile actor;

        /// <summary>Animator hash of the track name.</summary>
        public int channelHash;

        /// <summary>Footstep type closest to the curve weight. Null when no types exist.</summary>
        public FootstepType type;

        /// <summary>Ground hit for this sample. The collider is null when the ray missed or raycasting is off.</summary>
        public RaycastHit hit;

        /// <summary>World rotation of the foot at this sample.</summary>
        public Quaternion rotation;
    }
}
