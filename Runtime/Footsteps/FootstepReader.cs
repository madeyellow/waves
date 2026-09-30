using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

[assembly: InternalsVisibleTo("MadeYellow.WAVES.Editor")]
[assembly: InternalsVisibleTo("MadeYellow.WAVES.Editor.Tests")]

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>When the reader reads footstep curves from the animator.</summary>
    public enum FootstepSampleTiming
    {
        /// <summary>Read curves in Update.</summary>
        Update,

        /// <summary>Read curves in LateUpdate, after the animator has applied the pose.</summary>
        LateUpdate,

        /// <summary>Read curves in FixedUpdate.</summary>
        FixedUpdate,

        /// <summary>Do not read curves automatically. Call <see cref="FootstepReader.SampleFootsteps"/>.</summary>
        Manual
    }

    /// <summary>
    /// Samples baked footstep curves and raises start and finish events.
    /// </summary>
    /// <remarks>
    /// Each named track in the assigned profile becomes a <see cref="FootstepTrack"/>.
    /// A track handle always has <see cref="FootstepTrack.Events"/>. A missing slot is created and saved.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("MadeYellow/WAVES/Footstep Reader")]
    [Icon("Packages/com.madeyellow.waves/Editor/Icons/footstep-window-icon.png")]
    public sealed class FootstepReader : MonoBehaviour, ISerializationCallbackReceiver
    {
        /// <summary>Curve values below this are treated as no contact.</summary>
        const float ContactEpsilon = 0.0001f;

        /// <summary>Extra ray length below the foot, in meters.</summary>
        const float RaycastReach = 2f;

        /// <summary>Profile whose track names match the baked curves.</summary>
        [SerializeField] FootstepTracksProfile _profile;

        /// <summary>Known footstep types, gathered in the editor and used to match curve weight.</summary>
        [SerializeField, HideInInspector] List<FootstepType> _types = new List<FootstepType>();

        /// <summary>Foot bone assigned to each profile track.</summary>
        [SerializeField] List<FootBinding> _feet = new List<FootBinding>();

        /// <summary>When curves are read.</summary>
        [SerializeField] FootstepSampleTiming _sampleTiming = FootstepSampleTiming.LateUpdate;

        /// <summary>Casts a ray down from each foot while it is in contact.</summary>
        [SerializeField]
        [Tooltip("Casts a ray down from the foot during contact and stores the hit on the footstep. Turn this off to skip physics and keep only the foot transform.")]
        bool _useRaycasting = true;

        /// <summary>Layers included in the foot raycast.</summary>
        [SerializeField]
        [Tooltip("Layers the foot ray can hit. Include the ground and exclude the character, or the ray stops on the body collider.")]
        LayerMask _raycastMask = ~0;

        /// <summary>How far above the foot the ray starts, in meters.</summary>
        [SerializeField, Min(0f)]
        [Tooltip("Meters above the foot where the ray starts. Increase it when the foot is already inside the ground. The ray then travels this distance plus 2 meters downward.")]
        float _raycastOffset = 0.1f;

        /// <summary>Raised when any track enters the contact phase.</summary>
        [SerializeField] FootstepEvent _onFootstepStarted = new FootstepEvent();

        /// <summary>Raised when any track leaves the contact phase.</summary>
        [SerializeField, FormerlySerializedAs("_onFootstepEnded")]
        FootstepEvent _onFootstepFinished = new FootstepEvent();

        /// <summary>Animator hashes for saved per-track subscriptions. Kept even if the track leaves the profile.</summary>
        [SerializeField] List<int> _trackEventHashes = new List<int>();

        /// <summary>Subscriptions paired by index with <see cref="_trackEventHashes"/>.</summary>
        [SerializeField] List<TrackFootstepEvents> _trackEventSubscriptions = new List<TrackFootstepEvents>();

#if UNITY_EDITOR
        /// <summary>Draws contact gizmos while this reader is selected.</summary>
        [SerializeField] bool _showDebug = true;

        /// <summary>Radius of the contact gizmo, in meters.</summary>
        [SerializeField, Min(0f)] float _gizmoSize = 0.1f;
#endif

        /// <summary>Animator whose float parameters carry the baked curves.</summary>
        Animator _animator;

        /// <summary>Tracks built from the current profile.</summary>
        FootstepTrack[] _tracks = Array.Empty<FootstepTrack>();

        /// <summary>Same tracks as <see cref="_tracks"/>, keyed by <see cref="FootstepTrack.Hash"/>.</summary>
        readonly Dictionary<int, FootstepTrack> _tracksByHash = new Dictionary<int, FootstepTrack>();

        /// <summary>True after <see cref="BuildTracks"/> has run for this profile.</summary>
        bool _tracksReady;

#if UNITY_EDITOR
        /// <summary>Parameter names already reported as the wrong animator type.</summary>
        static readonly HashSet<string> WarnedParameterTypes = new HashSet<string>();
#endif

        /// <summary>Profile whose track names match the baked curves.</summary>
        public FootstepTracksProfile Profile => _profile;

        /// <summary>When curves are read.</summary>
        public FootstepSampleTiming SampleTiming
        {
            get => _sampleTiming;
            set => _sampleTiming = value;
        }

        /// <summary>True when a ray is cast down from the foot during contact.</summary>
        /// <remarks>Disable this to skip physics. The sample then keeps the foot rotation and an empty hit.</remarks>
        public bool UseRaycasting
        {
            get => _useRaycasting;
            set => _useRaycasting = value;
        }

        /// <summary>Layers the foot ray can hit.</summary>
        /// <remarks>Include the ground and exclude the character so the ray does not stop on its own colliders.</remarks>
        public LayerMask RaycastMask
        {
            get => _raycastMask;
            set => _raycastMask = value;
        }

        /// <summary>How far above the foot the ray starts, in meters.</summary>
        /// <remarks>Increase this when the foot begins the step inside the ground. Values below zero are stored as zero. The ray length is this offset plus 2 meters.</remarks>
        public float RaycastOffset
        {
            get => _raycastOffset;
            set => _raycastOffset = value < 0f ? 0f : value;
        }

        /// <summary>Raised when any track enters the contact phase.</summary>
        public FootstepEvent OnFootstepStarted => _onFootstepStarted;

        /// <summary>Raised when any track leaves the contact phase.</summary>
        public FootstepEvent OnFootstepFinished => _onFootstepFinished;

        /// <summary>Unity calls this before the reader is written. No extra data is stored.</summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        /// <summary>Attaches subscription slots to tracks that are already built.</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            BindTrackEvents(false);
        }

        /// <summary>Caches the animator and builds one handle per profile track.</summary>
        void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_raycastOffset < 0f)
                _raycastOffset = 0f;

            BuildTracks();
            if (_profile == null || _animator == null || _animator.runtimeAnimatorController == null)
                enabled = false;

#if UNITY_EDITOR
            EnsureControllerFloatParameters();
#endif
        }

        /// <summary>Samples curves when <see cref="SampleTiming"/> is <see cref="FootstepSampleTiming.Update"/>.</summary>
        void Update()
        {
            if (_sampleTiming == FootstepSampleTiming.Update)
                SampleFootsteps();
        }

        /// <summary>Samples curves when <see cref="SampleTiming"/> is <see cref="FootstepSampleTiming.LateUpdate"/>.</summary>
        void LateUpdate()
        {
            if (_sampleTiming == FootstepSampleTiming.LateUpdate)
                SampleFootsteps();
        }

        /// <summary>Samples curves when <see cref="SampleTiming"/> is <see cref="FootstepSampleTiming.FixedUpdate"/>.</summary>
        void FixedUpdate()
        {
            if (_sampleTiming == FootstepSampleTiming.FixedUpdate)
                SampleFootsteps();
        }

        /// <summary>Returns the latest step for a track name.</summary>
        /// <param name="channelName">Profile track name. Null or empty returns the default value.</param>
        /// <returns>The latest sample, or the default value when the track is missing or has not stepped yet.</returns>
        public FootstepData GetFootstep(string channelName)
        {
            FootstepTrack track = GetTrack(channelName);
            return track != null ? track.CurrentValue : default;
        }

        /// <summary>Returns the latest step for a track hash.</summary>
        /// <param name="channelHash">Animator hash of the track name.</param>
        /// <returns>The latest sample, or the default value when the track is missing or has not stepped yet.</returns>
        public FootstepData GetFootstep(int channelHash)
        {
            FootstepTrack track = GetTrack(channelHash);
            return track != null ? track.CurrentValue : default;
        }

        /// <summary>Returns the live handle for a profile track name.</summary>
        /// <param name="trackName">Profile track name. Matching uses <see cref="Animator.StringToHash"/>.</param>
        /// <returns>The track, or null when the name is empty or not in the current profile.</returns>
        public FootstepTrack GetTrack(string trackName)
        {
            if (string.IsNullOrEmpty(trackName))
                return null;

            return GetTrack(Animator.StringToHash(trackName));
        }

        /// <summary>Returns the live handle for a profile track definition.</summary>
        /// <param name="definition">Track row from a <see cref="FootstepTracksProfile"/>. Null returns null.</param>
        /// <returns>The track, or null when the definition has no name or is not in the current profile.</returns>
        public FootstepTrack GetTrack(FootstepTrackDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.name))
                return null;

            return GetTrack(Animator.StringToHash(definition.name));
        }

        /// <summary>Returns the live handle for an animator track hash.</summary>
        /// <param name="channelHash">Animator hash of the track name.</param>
        /// <returns>The track, or null when the current profile has no track with this hash.</returns>
        /// <remarks>The reader rebuilds handles when it awakens. Read the track from Start onward and keep that reference.</remarks>
        public FootstepTrack GetTrack(int channelHash)
        {
            if (!_tracksReady)
                BuildTracks();

            if (_tracksByHash.TryGetValue(channelHash, out FootstepTrack track))
                return track;

            return null;
        }

        /// <summary>Builds one <see cref="FootstepTrack"/> per named profile track and attaches saved events.</summary>
        void BuildTracks()
        {
            _tracksByHash.Clear();
            if (_profile == null || _profile.tracks == null)
            {
                _tracks = Array.Empty<FootstepTrack>();
                _tracksReady = true;
                return;
            }

            int count = 0;
            for (int i = 0; i < _profile.tracks.Count; i++)
            {
                FootstepTrackDefinition definition = _profile.tracks[i];
                if (definition != null && !string.IsNullOrEmpty(definition.name))
                    count++;
            }

            var tracks = new FootstepTrack[count];
            int index = 0;
            for (int i = 0; i < _profile.tracks.Count; i++)
            {
                FootstepTrackDefinition definition = _profile.tracks[i];
                if (definition == null || string.IsNullOrEmpty(definition.name))
                    continue;

                int hash = Animator.StringToHash(definition.name);
                if (_tracksByHash.ContainsKey(hash))
                    continue;

                int curveHash = Animator.StringToHash(definition.BakedCurveName);
                var track = new FootstepTrack(definition.name, hash, curveHash, definition.color);
                tracks[index] = track;
                _tracksByHash.Add(hash, track);
                index++;
            }

            if (index != tracks.Length)
                Array.Resize(ref tracks, index);

            _tracks = tracks;
            _tracksReady = true;
            BindTrackEvents(true);
        }

        /// <summary>Gives every built track a subscription slot, creating one when it is missing.</summary>
        /// <param name="saveCreated">When true, a newly created slot is saved in the editor.</param>
        void BindTrackEvents(bool saveCreated)
        {
            if (_tracks == null)
                return;

            if (_trackEventHashes == null)
                _trackEventHashes = new List<int>();
            if (_trackEventSubscriptions == null)
                _trackEventSubscriptions = new List<TrackFootstepEvents>();

            bool created = false;
            for (int i = 0; i < _tracks.Length; i++)
            {
                FootstepTrack track = _tracks[i];
                if (track == null)
                    continue;

                int index = IndexOfTrackEvents(track.Hash);
                TrackFootstepEvents events;
                if (index < 0)
                {
                    events = new TrackFootstepEvents();
                    _trackEventHashes.Add(track.Hash);
                    _trackEventSubscriptions.Add(events);
                    created = true;
                }
                else
                {
                    events = index < _trackEventSubscriptions.Count ? _trackEventSubscriptions[index] : null;
                    if (events == null)
                    {
                        events = new TrackFootstepEvents();
                        while (_trackEventSubscriptions.Count <= index)
                            _trackEventSubscriptions.Add(null);
                        _trackEventSubscriptions[index] = events;
                        created = true;
                    }
                }

                if (events.onFootstepStarted == null)
                    events.onFootstepStarted = new FootstepEvent();
                if (events.onFootstepFinished == null)
                    events.onFootstepFinished = new FootstepEvent();

                track.Events = events;
            }

#if UNITY_EDITOR
            if (created && saveCreated && !Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Finds a saved subscription slot by track hash.</summary>
        /// <param name="channelHash">Animator hash of the track name.</param>
        /// <returns>The list index, or -1 when this track has no slot yet.</returns>
        int IndexOfTrackEvents(int channelHash)
        {
            if (_trackEventHashes == null)
                return -1;

            for (int i = 0; i < _trackEventHashes.Count; i++)
            {
                if (_trackEventHashes[i] == channelHash)
                    return i;
            }

            return -1;
        }

        /// <summary>Reads each track curve and opens, updates, or closes its contact.</summary>
        /// <remarks>
        /// Call this when <see cref="SampleTiming"/> is <see cref="FootstepSampleTiming.Manual"/>.
        /// <see cref="FootstepSampleTiming.Update"/>, <see cref="FootstepSampleTiming.LateUpdate"/>, and <see cref="FootstepSampleTiming.FixedUpdate"/> call it on their own.
        /// </remarks>
        public void SampleFootsteps()
        {
            if (_animator == null || _tracks == null)
                return;

            for (int i = 0; i < _tracks.Length; i++)
            {
                FootstepTrack track = _tracks[i];
                if (track == null)
                    continue;

                float weight = _animator.GetFloat(track.CurveHash);
                bool active = Mathf.Abs(weight) > ContactEpsilon;
                if (active)
                {
                    if (!track.InContactPhase)
                        BeginStep(track, weight);
                    else if (Mathf.Abs(weight - track.Weight) > ContactEpsilon)
                        ContinueStep(track, weight);
                }
                else if (track.InContactPhase)
                {
                    EndStep(track);
                }
            }
        }

        /// <summary>Opens a contact and raises the start events.</summary>
        /// <param name="track">Track whose curve just became active.</param>
        /// <param name="weight">Animator curve value that opened the contact.</param>
        void BeginStep(FootstepTrack track, float weight)
        {
            Transform foot = ResolveFoot(track.Name);
            Quaternion rotation = foot != null ? foot.rotation : Quaternion.identity;
            RaycastHit hit = default;
            if (_useRaycasting && foot != null)
                TryRaycast(foot, out hit);

            var step = new FootstepData
            {
                channelHash = track.Hash,
                type = MatchType(weight),
                hit = hit,
                rotation = rotation
            };
            track.Begin(step, weight);
            _onFootstepStarted?.Invoke(step);
            track.Events.OnFootstepStarted.Invoke(step);
        }

        /// <summary>Updates the step type while the foot stays down.</summary>
        /// <param name="track">Track that is already in contact.</param>
        /// <param name="weight">Latest animator curve value.</param>
        void ContinueStep(FootstepTrack track, float weight)
        {
            track.Continue(weight, MatchType(weight));
        }

        /// <summary>Closes a contact and raises the finish events.</summary>
        /// <param name="track">Track whose curve just went quiet.</param>
        void EndStep(FootstepTrack track)
        {
            FootstepData ended = track.End(ResolveFoot(track.Name));
            _onFootstepFinished?.Invoke(ended);
            track.Events.OnFootstepFinished.Invoke(ended);
        }

        /// <summary>Casts down from the foot and writes the first solid hit.</summary>
        /// <param name="foot">Foot transform used as the ray origin.</param>
        /// <param name="hit">The hit, or the default value when nothing is hit.</param>
        /// <returns>True when a collider is hit.</returns>
        bool TryRaycast(Transform foot, out RaycastHit hit)
        {
            float offset = _raycastOffset < 0f ? 0f : _raycastOffset;
            Vector3 origin = foot.position + Vector3.up * offset;
            float distance = offset + RaycastReach;
            if (distance < 0.001f)
                distance = 0.001f;

            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out hit,
                    distance,
                    _raycastMask,
                    QueryTriggerInteraction.Ignore))
                return true;

            hit = default;
            return false;
        }

        /// <summary>Finds the foot transform assigned to a track name.</summary>
        /// <param name="trackName">Profile track name.</param>
        /// <returns>The assigned transform, or null.</returns>
        Transform ResolveFoot(string trackName)
        {
            if (_feet == null || string.IsNullOrEmpty(trackName))
                return null;

            for (int i = 0; i < _feet.Count; i++)
            {
                FootBinding binding = _feet[i];
                if (binding != null && binding.trackName == trackName)
                    return binding.foot;
            }

            return null;
        }

        /// <summary>Picks the footstep type whose weight is closest to the curve.</summary>
        /// <param name="weight">Animator curve value.</param>
        /// <returns>The closest type, or null when no types are known.</returns>
        FootstepType MatchType(float weight)
        {
            if (_types == null)
                return null;

            FootstepType best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _types.Count; i++)
            {
                FootstepType type = _types[i];
                if (type == null)
                    continue;

                float distance = Mathf.Abs(type.weight - weight);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = type;
            }

            return best;
        }

#if UNITY_EDITOR
        /// <summary>Clamps settings and schedules animator parameter setup.</summary>
        void OnValidate()
        {
            if (_raycastOffset < 0f)
                _raycastOffset = 0f;

            EditorApplication.delayCall -= EnsureControllerFloatParameters;
            EditorApplication.delayCall += EnsureControllerFloatParameters;
        }

        /// <summary>Adds a float parameter for each track's baked curve.</summary>
        /// <remarks>Warns once when a parameter with that curve name exists but is not a float.</remarks>
        internal void EnsureControllerFloatParameters()
        {
            if (this == null)
                return;

            SyncKnownStepTypes();
            if (_profile == null || _profile.tracks == null)
                return;

            Animator animator = _animator != null ? _animator : GetComponent<Animator>();
            if (animator == null)
                return;

            AnimatorController controller = ResolveController(animator.runtimeAnimatorController);
            if (controller == null)
                return;

            AnimatorControllerParameter[] parameters = controller.parameters;
            bool dirty = false;
            for (int i = 0; i < _profile.tracks.Count; i++)
            {
                FootstepTrackDefinition track = _profile.tracks[i];
                if (track == null || string.IsNullOrEmpty(track.BakedCurveName))
                    continue;

                string curveName = track.BakedCurveName;
                int existing = IndexOfParameter(parameters, curveName);
                if (existing >= 0)
                {
                    if (parameters[existing].type != AnimatorControllerParameterType.Float
                        && WarnedParameterTypes.Add(controller.GetInstanceID() + ":" + curveName))
                    {
                        Debug.LogWarning(
                            "Footstep expected a float parameter named '" + curveName
                            + "' on '" + controller.name + "', but it is "
                            + parameters[existing].type + ".",
                            controller);
                    }

                    continue;
                }

                controller.AddParameter(curveName, AnimatorControllerParameterType.Float);
                parameters = controller.parameters;
                dirty = true;
            }

            if (dirty)
                EditorUtility.SetDirty(controller);
        }

        /// <summary>Stores every <see cref="FootstepType"/> asset, sorted by <see cref="FootstepType.order"/>.</summary>
        void SyncKnownStepTypes()
        {
            string[] guids = AssetDatabase.FindAssets("t:FootstepType");
            var found = new List<FootstepType>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                FootstepType type = AssetDatabase.LoadAssetAtPath<FootstepType>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (type != null)
                    found.Add(type);
            }

            found.Sort(FootstepType.CompareByOrder);
            if (SameStepTypes(found))
                return;

            _types = found;
            EditorUtility.SetDirty(this);
        }

        /// <summary>True when the saved type list already matches the assets that were found.</summary>
        /// <param name="found">Types gathered from the project, in compare order.</param>
        /// <returns>True when nothing would change.</returns>
        bool SameStepTypes(List<FootstepType> found)
        {
            if (_types == null || _types.Count != found.Count)
                return false;

            for (int i = 0; i < found.Count; i++)
            {
                if (_types[i] != found[i])
                    return false;
            }

            return true;
        }

        /// <summary>Draws a sphere and ground normal for each foot that is in contact.</summary>
        void OnDrawGizmosSelected()
        {
            if (!_showDebug || _tracks == null)
                return;

            float radius = _gizmoSize > 0f ? _gizmoSize : 0f;
            float normalLength = Mathf.Max(radius, 0.01f) * 4f;
            for (int i = 0; i < _tracks.Length; i++)
            {
                FootstepTrack track = _tracks[i];
                if (track == null || !track.InContactPhase)
                    continue;

                FootstepData state = track.CurrentValue;
                bool grounded = state.hit.collider != null;
                Transform foot = ResolveFoot(track.Name);
                Vector3 center = grounded
                    ? state.hit.point
                    : foot != null ? foot.position : state.hit.point;

                Vector3 normal = _useRaycasting ? state.hit.normal : Vector3.up;
                Vector3 origin = grounded ? state.hit.point : center;
                Gizmos.color = track.Color;
                Gizmos.DrawSphere(center, radius);
                if (normal.sqrMagnitude <= ContactEpsilon)
                    continue;

                Gizmos.DrawRay(origin, normal * normalLength);
            }
        }

        /// <summary>Resolves the editable controller behind a runtime animator controller.</summary>
        /// <param name="runtime">Controller or override controller on the animator.</param>
        /// <returns>The animator controller, or null.</returns>
        static AnimatorController ResolveController(RuntimeAnimatorController runtime)
        {
            if (runtime is AnimatorController controller)
                return controller;

            if (runtime is AnimatorOverrideController over)
                return over.runtimeAnimatorController as AnimatorController;

            return null;
        }

        /// <summary>Finds a parameter by name.</summary>
        /// <param name="parameters">Parameters on the controller.</param>
        /// <param name="name">Parameter name to find.</param>
        /// <returns>The index, or -1.</returns>
        static int IndexOfParameter(AnimatorControllerParameter[] parameters, string name)
        {
            if (parameters == null)
                return -1;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] != null && parameters[i].name == name)
                    return i;
            }

            return -1;
        }
#endif

        /// <summary>Foot bone assigned to one profile track.</summary>
        [Serializable]
        sealed class FootBinding
        {
            /// <summary>Profile track name this bone belongs to.</summary>
            public string trackName = string.Empty;

            /// <summary>Transform used as the foot origin.</summary>
            public Transform foot;
        }

        /// <summary>Start and finish subscriptions saved for one track.</summary>
        /// <remarks>
        /// Present on every track returned by <see cref="GetTrack(string)"/>.
        /// The reader creates the slot when the track has none yet.
        /// </remarks>
        [Serializable]
        public sealed class TrackFootstepEvents
        {
            /// <summary>Raised when this track enters the contact phase.</summary>
            [SerializeField] internal FootstepEvent onFootstepStarted = new FootstepEvent();

            /// <summary>Raised when this track leaves the contact phase.</summary>
            [SerializeField] internal FootstepEvent onFootstepFinished = new FootstepEvent();

            /// <summary>Raised when this track enters the contact phase.</summary>
            public FootstepEvent OnFootstepStarted => onFootstepStarted;

            /// <summary>Raised when this track leaves the contact phase.</summary>
            public FootstepEvent OnFootstepFinished => onFootstepFinished;
        }
    }
}
