using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>
    /// Live handle for one footstep track on a <see cref="FootstepReader"/>.
    /// </summary>
    /// <remarks>
    /// The reader creates a handle for every named track in the current profile.
    /// Contact state is updated by the reader while it samples the animator.
    /// <see cref="Events"/> is always present. A missing slot is created with the track.
    /// </remarks>
    public sealed class FootstepTrack : IColorCodedElement
    {
        /// <summary>Profile name of this track.</summary>
        public string Name { get; }

        /// <summary>Animator hash of <see cref="Name"/>.</summary>
        public int Hash { get; }

        /// <summary>Animator hash of the baked curve. This can differ from <see cref="Hash"/>.</summary>
        internal int CurveHash { get; }

        /// <summary>Curve weight sampled for the current contact.</summary>
        internal float Weight { get; private set; }

        /// <summary>Color used to draw this track.</summary>
        internal Color Color => _color;

        readonly Color _color;

        /// <summary>Opaque track color.</summary>
        Color IColorCodedElement.Color => WAVESPalette.Opaque(_color);

        /// <summary>Latest step sample for this track.</summary>
        /// <remarks>
        /// Updated while <see cref="InContactPhase"/> is true.
        /// After the foot lifts, this stays at the sample sent with the finish event.
        /// It is the default value only before the first step.
        /// </remarks>
        public FootstepData CurrentValue { get; private set; }

        /// <summary>
        /// True while this foot is in the contact phase.
        /// </summary>
        public bool InContactPhase { get; private set; }

        /// <summary>Subscriptions for this track.</summary>
        /// <remarks>Never null on a track returned by the reader. Listeners added in code are not saved; inspector listeners are.</remarks>
        public FootstepReader.TrackFootstepEvents Events { get; internal set; }

        /// <summary>Creates a handle for one profile track.</summary>
        /// <param name="name">Track name from the actor profile.</param>
        /// <param name="hash">Animator hash of <paramref name="name"/>.</param>
        /// <param name="curveHash">Animator hash of the baked curve name.</param>
        /// <param name="color">Display color from the profile.</param>
        internal FootstepTrack(string name, int hash, int curveHash, Color color)
        {
            Name = name;
            Hash = hash;
            CurveHash = curveHash;
            _color = color;
        }

        /// <summary>Enters the contact phase and stores the first sample.</summary>
        /// <param name="step">World sample for this contact.</param>
        /// <param name="weight">Animator curve value that opened the contact.</param>
        internal void Begin(FootstepData step, float weight)
        {
            InContactPhase = true;
            Weight = weight;
            CurrentValue = step;
        }

        /// <summary>Keeps the contact open and refreshes its step type.</summary>
        /// <param name="weight">Latest animator curve value.</param>
        /// <param name="type">Footstep type closest to <paramref name="weight"/>.</param>
        internal void Continue(float weight, FootstepType type)
        {
            Weight = weight;
            FootstepData step = CurrentValue;
            step.type = type;
            CurrentValue = step;
        }

        /// <summary>Leaves the contact phase and keeps the finishing sample.</summary>
        /// <param name="foot">Foot transform used to refresh rotation. Ignored when null.</param>
        /// <returns>The sample sent with the finish event. The same value stays in <see cref="CurrentValue"/>.</returns>
        internal FootstepData End(Transform foot)
        {
            FootstepData ended = CurrentValue;
            if (foot != null)
                ended.rotation = foot.rotation;

            InContactPhase = false;
            Weight = 0f;
            CurrentValue = ended;
            return ended;
        }
    }
}
