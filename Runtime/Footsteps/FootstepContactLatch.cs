using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>What a new curve sample did to a footstep contact.</summary>
    internal enum FootstepLatchSignal
    {
        /// <summary>The contact did not start, change type, or finish.</summary>
        None,

        /// <summary>The curve held a footstep weight long enough to open a contact.</summary>
        Began,

        /// <summary>The curve held a different footstep weight while the foot was already down.</summary>
        Retyped,

        /// <summary>The curve went quiet and the open contact finished.</summary>
        Ended
    }

    /// <summary>
    /// Decides when a baked footstep curve is a real step.
    /// </summary>
    /// <remarks>
    /// A rising or falling curve passes through every weight between silence and the baked value.
    /// The nearest type to those samples is often the wrong step. A contact opens only after two
    /// samples in a row sit near the same type and barely move. One sample on the way through
    /// another weight does not count. The start is therefore one sample later than the first
    /// active reading. If the curve goes quiet before that, nothing is reported.
    /// </remarks>
    internal sealed class FootstepContactLatch
    {
        /// <summary>Curve values at or below this are silence.</summary>
        internal const float ContactEpsilon = 0.0001f;

        /// <summary>How far a sample may move and still count as the same plateau.</summary>
        internal const float StableEpsilon = 0.02f;

        /// <summary>Share of the gap to the nearest other weight that still counts as that weight.</summary>
        /// <remarks>
        /// Half of that gap is the midpoint, which belongs to neither type. A quarter of the gap
        /// stays near the baked weight, so a sample halfway between Walk and Run matches nothing.
        /// </remarks>
        internal const float PlateauFraction = 0.25f;

        /// <summary>True after <see cref="FootstepLatchSignal.Began"/> until the curve goes quiet.</summary>
        internal bool InContact { get; private set; }

        /// <summary>Type confirmed for the current contact. Null when no types exist, or when no contact is open.</summary>
        internal FootstepType Type { get; private set; }

        /// <summary>Curve value stored with <see cref="Type"/>.</summary>
        internal float Weight { get; private set; }

        bool _watching;
        float _previous;
        float _peak;
        int _steadyCount;
        FootstepType _candidate;

        /// <summary>Feeds one animator sample.</summary>
        /// <param name="weight">Curve value for this sample.</param>
        /// <param name="types">Known footstep types. Null or empty still reports contact, with a null type.</param>
        /// <returns>Whether this sample opened, retyped, or closed a contact.</returns>
        internal FootstepLatchSignal Sample(float weight, IReadOnlyList<FootstepType> types)
        {
            if (Mathf.Abs(weight) <= ContactEpsilon)
            {
                bool ended = InContact;
                Reset();
                return ended ? FootstepLatchSignal.Ended : FootstepLatchSignal.None;
            }

            bool typed = HasTypes(types);
            FootstepType near = typed ? MatchPlateau(weight, types) : null;
            bool onPlateau = !typed || near != null;

            if (!_watching)
            {
                _watching = true;
                _previous = weight;
                _peak = weight;
                _candidate = near;
                _steadyCount = onPlateau ? 1 : 0;
                return FootstepLatchSignal.None;
            }

            float delta = Mathf.Abs(weight - _previous);
            _previous = weight;
            if (Mathf.Abs(weight) > Mathf.Abs(_peak))
                _peak = weight;

            bool sameType = !typed || near == _candidate;
            bool steady = delta <= StableEpsilon && onPlateau && sameType;
            if (!steady)
            {
                _candidate = near;
                _steadyCount = onPlateau ? 1 : 0;
                return FootstepLatchSignal.None;
            }

            if (_steadyCount < 2)
                _steadyCount++;

            if (_steadyCount < 2)
                return FootstepLatchSignal.None;

            float reported = weight;
            if (typed && MatchPlateau(_peak, types) == near)
                reported = _peak;

            if (!InContact)
            {
                InContact = true;
                Type = near;
                Weight = reported;
                return FootstepLatchSignal.Began;
            }

            if (near != Type)
            {
                Type = near;
                Weight = reported;
                return FootstepLatchSignal.Retyped;
            }

            return FootstepLatchSignal.None;
        }

        /// <summary>Drops a pending or open contact without a further signal.</summary>
        internal void Reset()
        {
            InContact = false;
            Type = null;
            Weight = 0f;
            _watching = false;
            _previous = 0f;
            _peak = 0f;
            _steadyCount = 0;
            _candidate = null;
        }

        /// <summary>Picks the type this sample is close enough to count as.</summary>
        /// <param name="weight">Curve value.</param>
        /// <param name="types">Known footstep types.</param>
        /// <returns>The type whose weight is within a quarter of the gap to its neighbor, or null.</returns>
        internal static FootstepType MatchPlateau(float weight, IReadOnlyList<FootstepType> types)
        {
            if (types == null)
                return null;

            FootstepType best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < types.Count; i++)
            {
                FootstepType type = types[i];
                if (type == null)
                    continue;

                float distance = Mathf.Abs(type.weight - weight);
                if (distance >= bestDistance || !IsOnPlateau(distance, type, types))
                    continue;

                bestDistance = distance;
                best = type;
            }

            return best;
        }

        static bool HasTypes(IReadOnlyList<FootstepType> types)
        {
            if (types == null)
                return false;

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != null)
                    return true;
            }

            return false;
        }

        static bool IsOnPlateau(float distance, FootstepType type, IReadOnlyList<FootstepType> types)
        {
            if (distance <= ContactEpsilon)
                return true;

            return distance < PlateauRadius(type, types);
        }

        static float PlateauRadius(FootstepType type, IReadOnlyList<FootstepType> types)
        {
            float gap = float.PositiveInfinity;
            for (int i = 0; i < types.Count; i++)
            {
                FootstepType other = types[i];
                if (other == null || ReferenceEquals(other, type))
                    continue;

                float distance = Mathf.Abs(type.weight - other.weight);
                if (distance < gap)
                    gap = distance;
            }

            return gap * PlateauFraction;
        }
    }
}
