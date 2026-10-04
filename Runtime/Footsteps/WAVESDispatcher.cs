using MadeYellow.EventBus;
using MadeYellow.WAVES.Actors;
using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>
    /// Accepts commands and publishes signals on the WAVES event bus.
    /// </summary>
    /// <remarks>
    /// The built-in command publishes each footstep start from the <see cref="FootstepReader"/> on this object.
    /// The reader is found on this GameObject. It is not assigned in the inspector.
    /// A missing bus or actor profile disables the component in <see cref="Start"/>.
    /// Subclasses can add their own commands and publish them with <see cref="PublishEvent{T}"/>.
    /// </remarks>
    [RequireComponent(typeof(FootstepReader))]
    [DisallowMultipleComponent]
    [AddComponentMenu("MadeYellow/WAVES/WAVES Dispatcher")]
    public class WAVESDispatcher : MonoBehaviour
    {
        /// <summary>Bus that receives published signals.</summary>
        [SerializeField]
        [Tooltip("Bus that receives published signals. Required. This component disables itself on Start when the bus is empty.")]
        ScriptableEventBus _bus;

        /// <summary>Actor kind copied into each published footstep.</summary>
        [SerializeField]
        [Tooltip("Actor kind published with every footstep. Required. This component disables itself on Start when the profile is empty.")]
        ActorProfile _actor;

        /// <summary>Reader on this GameObject.</summary>
        FootstepReader _reader;

        /// <summary>Game time before which footstep signals are not published. Zero means footsteps are not muted.</summary>
        float _footstepsMutedUntil;

        /// <summary>Bus that receives published signals.</summary>
        public ScriptableEventBus Bus => _bus;

        /// <summary>Actor kind copied into each published footstep.</summary>
        public ActorProfile Actor => _actor;

#if UNITY_EDITOR
        /// <summary>Assigns a project bus named FootstepEventBus when this component is added.</summary>
        void Reset()
        {
            if (_bus != null)
                return;

            _bus = FootstepEventBusLocator.Find();
        }
#endif

        /// <summary>Subscribes to the reader when the bus and actor profile are assigned.</summary>
        void OnEnable()
        {
            if (_bus == null || _actor == null)
                return;

            _reader = GetComponent<FootstepReader>();
            if (_reader != null)
                _reader.OnFootstepStarted.AddListener(PublishFootstepEvent);
        }

        /// <summary>Disables this component when the bus or actor profile is missing.</summary>
        void Start()
        {
            if (_bus != null && _actor != null)
                return;

            if (_bus == null && _actor == null)
            {
                Debug.LogWarning(
                    "WAVES Dispatcher needs an Event Bus and an Actor Profile. The component was disabled.",
                    this);
            }
            else if (_bus == null)
            {
                Debug.LogWarning(
                    "WAVES Dispatcher needs an Event Bus. The component was disabled.",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    "WAVES Dispatcher needs an Actor Profile. The component was disabled.",
                    this);
            }

            enabled = false;
        }

        /// <summary>Stops listening to the reader.</summary>
        void OnDisable()
        {
            if (_reader == null)
                return;

            _reader.OnFootstepStarted.RemoveListener(PublishFootstepEvent);
            _reader = null;
        }

        /// <summary>Stops publishing footstep signals until <paramref name="time"/> seconds have passed.</summary>
        /// <param name="time">Seconds from now. Footstep signals raised during this window are ignored.</param>
        public void MuteFootsteps(float time)
        {
            _footstepsMutedUntil = Time.time + time;
        }

        /// <summary>Stops publishing footstep signals until <see cref="UnmuteFootsteps"/> is called.</summary>
        public void MuteFootsteps()
        {
            MuteFootsteps(float.MaxValue);
        }

        /// <summary>Publishes footstep signals again.</summary>
        public void UnmuteFootsteps()
        {
            _footstepsMutedUntil = 0f;
        }

        /// <summary>Publishes <paramref name="eventData"/> on <see cref="Bus"/>.</summary>
        /// <typeparam name="T">Signal type.</typeparam>
        /// <param name="eventData">Signal to publish.</param>
        public void PublishEvent<T>(T eventData)
        {
            _bus.Publish(eventData);
        }

        /// <summary>
        /// Publishes one footstep on the bus, unless footsteps are muted.
        /// </summary>
        /// <param name="step">Sample raised by the reader.</param>
        public void PublishFootstepEvent(FootstepData step)
        {
            if (Time.time < _footstepsMutedUntil)
                return;

            if (_bus == null || _actor == null)
                return;

            PublishEvent(new ActorFootstepStarted
            {
                emitterId = gameObject.GetEntityId(),
                actor = _actor,
                channelHash = step.channelHash,
                type = step.type,
                hit = step.hit,
                rotation = step.rotation
            });
        }
    }
}
