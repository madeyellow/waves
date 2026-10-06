using System;
using MadeYellow.WAVES.Actors;
using UnityEngine;

namespace MadeYellow.WAVES.Jumps
{
    /// <summary>A landing that just hit the ground, tagged with the actor and the fall type.</summary>
    [Serializable]
    public struct ActorLanded
    {
        /// <summary>Identity of the GameObject that published the landing.</summary>
        public EntityId emitterId;

        /// <summary>Kind of actor that landed.</summary>
        public ActorProfile actor;

        /// <summary>Fall type the caller chose. Null uses the group's fallback landing.</summary>
        public ActorFallType type;

        /// <summary>Ground collider that was hit. Null when the caller had no surface.</summary>
        public Collider collider;

        /// <summary>World point of the landing.</summary>
        public Vector3 point;

        /// <summary>Ground normal at <see cref="point"/>.</summary>
        public Vector3 normal;
    }
}
