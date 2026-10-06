using System;
using MadeYellow.WAVES.Actors;
using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>A footstep that just entered contact, tagged with the actor that made it.</summary>
    [Serializable]
    public struct ActorFootstepStarted
    {
        /// <summary>Identity of the GameObject that published the step.</summary>
        public EntityId emitterId;

        /// <summary>Kind of actor that started the step. Null when the dispatcher has no profile.</summary>
        public ActorProfile actor;

        /// <summary>Animator hash of the track name.</summary>
        public int channelHash;

        /// <summary>Footstep type the curve held. Null when no types exist.</summary>
        public FootstepType type;

        /// <summary>Ground collider for this sample. Null when the ray missed, or when raycasting is off and no surface collider is set.</summary>
        public Collider collider;

        /// <summary>World point of the step. A hit point, the closest point on <see cref="collider"/>, or the foot position.</summary>
        public Vector3 point;

        /// <summary>Ground normal. Up when the sample did not come from a raycast.</summary>
        public Vector3 normal;

        /// <summary>World rotation of the foot at this sample.</summary>
        public Quaternion rotation;
    }
}
