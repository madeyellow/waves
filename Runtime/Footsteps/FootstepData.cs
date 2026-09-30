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

        /// <summary>Footstep type closest to the curve weight. Null when no types exist.</summary>
        public FootstepType type;

        /// <summary>Ground hit for this sample. The collider is null when the ray missed or raycasting is off.</summary>
        public RaycastHit hit;

        /// <summary>World rotation of the foot at this sample.</summary>
        public Quaternion rotation;
    }

    /// <summary>UnityEvent that receives a <see cref="FootstepData"/> sample.</summary>
    [Serializable]
    public sealed class FootstepEvent : UnityEvent<FootstepData>
    {
    }
}
