using System;
using MadeYellow.WAVES.Actors;
using UnityEngine;

namespace MadeYellow.WAVES.Jumps
{
    /// <summary>A jump that just left the ground, tagged with the actor that made it.</summary>
    [Serializable]
    public struct ActorJumpStarted
    {
        /// <summary>Identity of the GameObject that published the jump.</summary>
        public EntityId emitterId;

        /// <summary>Kind of actor that jumped.</summary>
        public ActorProfile actor;

        /// <summary>Ground collider the jump left. Null when the caller had no surface.</summary>
        public Collider collider;

        /// <summary>World point of the takeoff.</summary>
        public Vector3 point;

        /// <summary>Ground normal at <see cref="point"/>.</summary>
        public Vector3 normal;
    }
}
