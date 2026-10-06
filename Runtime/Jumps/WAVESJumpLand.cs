using MadeYellow.WAVES.Footsteps;
using UnityEngine;

namespace MadeYellow.WAVES.Jumps
{
    /// <summary>Publishes jump and landing signals through a <see cref="WAVESDispatcher"/>.</summary>
    public static class WAVESJumpLand
    {
        /// <summary>
        /// Publishes one jump on <paramref name="dispatcher"/>'s bus.
        /// The dispatcher copies its actor profile and the identity of its GameObject into the signal.
        /// Nothing is published when the dispatcher, its bus, or its actor profile is missing.
        /// </summary>
        /// <param name="dispatcher">Dispatcher on the character.</param>
        /// <param name="collider">Ground the jump left.</param>
        /// <param name="position">World point of the takeoff.</param>
        /// <param name="normal">Ground normal at <paramref name="position"/>.</param>
        public static void PublishJump(this WAVESDispatcher dispatcher, Collider collider, Vector3 position, Vector3 normal)
        {
            if (dispatcher == null || dispatcher.Bus == null || dispatcher.Actor == null)
                return;

            dispatcher.PublishEvent(new ActorJumpStarted
            {
                emitterId = dispatcher.gameObject.GetEntityId(),
                actor = dispatcher.Actor,
                collider = collider,
                point = position,
                normal = normal
            });
        }

        /// <summary>
        /// Publishes one landing on <paramref name="dispatcher"/>'s bus.
        /// The dispatcher copies its actor profile and the identity of its GameObject into the signal.
        /// A null <paramref name="fallType"/> resolves as the fallback landing.
        /// Nothing is published when the dispatcher, its bus, or its actor profile is missing.
        /// </summary>
        /// <param name="dispatcher">Dispatcher on the character.</param>
        /// <param name="fallType">Landing intensity. Null uses the fallback landing.</param>
        /// <param name="collider">Ground that was hit.</param>
        /// <param name="position">World point of the landing.</param>
        /// <param name="normal">Ground normal at <paramref name="position"/>.</param>
        public static void PublishLand(
            this WAVESDispatcher dispatcher,
            ActorFallType fallType,
            Collider collider,
            Vector3 position,
            Vector3 normal)
        {
            if (dispatcher == null || dispatcher.Bus == null || dispatcher.Actor == null)
                return;

            dispatcher.PublishEvent(new ActorLanded
            {
                emitterId = dispatcher.gameObject.GetEntityId(),
                actor = dispatcher.Actor,
                type = fallType,
                collider = collider,
                point = position,
                normal = normal
            });
        }
    }
}
