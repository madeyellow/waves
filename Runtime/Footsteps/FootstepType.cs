using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    [CreateAssetMenu(
        fileName = "FootstepType",
        menuName = "MadeYellow/WAVES/Footstep Type")]
    /// <summary>A named step style, matched by how close its weight is to the baked curve.</summary>
    public sealed class FootstepType : ScriptableObject
    {
        /// <summary>Icon shown for this type in the editor.</summary>
        public Texture2D icon;

        /// <summary>Curve value this type represents. The closest weight wins.</summary>
        public float weight = 1f;

        /// <summary>Position in lists. Lower values are shown first.</summary>
        public int order;

        /// <summary>Orders types by <see cref="order"/>, then by name.</summary>
        public static int CompareByOrder(FootstepType left, FootstepType right)
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
