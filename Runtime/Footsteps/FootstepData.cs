using System;
using UnityEngine;
using UnityEngine.Events;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>One sampled footstep: which track, which type, and where the foot was.</summary>
    [Serializable]
    public struct FootstepData
    {
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

    /// <summary>UnityEvent that receives a <see cref="FootstepData"/> sample.</summary>
    [Serializable]
    public sealed class FootstepEvent : UnityEvent<FootstepData>
    {
    }
}
