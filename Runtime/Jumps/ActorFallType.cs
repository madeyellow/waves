using UnityEngine;

namespace MadeYellow.WAVES.Jumps
{
    /// <summary>A named landing intensity, chosen by the character when it hits the ground.</summary>
    [CreateAssetMenu(
        fileName = "FallType",
        menuName = "MadeYellow/WAVES/Fall Type")]
    public sealed class ActorFallType : ScriptableObject
    {
        /// <summary>Position in lists. Lower values are shown first.</summary>
        public int order;

        /// <summary>Orders types by <see cref="order"/>, then by name.</summary>
        public static int CompareByOrder(ActorFallType left, ActorFallType right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int order = left.order.CompareTo(right.order);
            if (order != 0)
                return order;

            return string.CompareOrdinal(left.name, right.name);
        }
    }
}
