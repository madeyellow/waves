using System;
using System.Collections.Generic;
using MadeYellow.EventBus;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using MadeYellow.WAVES.Footsteps;
using MadeYellow.WAVES.Surfaces;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.VFX;

namespace MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule
{
    /// <summary>Audio and visual chosen for one footstep after fallback.</summary>
    public readonly struct WAVESResolvedFootstep
    {
        /// <summary>True when an audio resource was found.</summary>
        public readonly bool HasAudio;

        /// <summary>Resource to play. Null when <see cref="HasAudio"/> is false.</summary>
        public readonly AudioResource Audio;

        /// <summary>Distance at which the sound is heard and culled, in meters.</summary>
        public readonly float AudibleDistance;

        /// <summary>Distance inside which the sound stays at full volume, in meters.</summary>
        public readonly float MinDistance;

        /// <summary>How the sound fades with distance.</summary>
        public readonly AudioRolloffMode Rolloff;

        /// <summary>Doppler strength of the voice. Zero keeps a moved source from pitching.</summary>
        public readonly float DopplerLevel;

        /// <summary>Mixer group for this sound. Null plays straight to the listener.</summary>
        public readonly AudioMixerGroup MixerGroup;

        /// <summary>Linear volume, from 0 to 1.</summary>
        public readonly float Volume;

        /// <summary>0 plays in 2D. 1 plays in 3D at the foot.</summary>
        public readonly float SpatialBlend;

        /// <summary>How much of this sound is sent to reverb zones, from 0 to 1.</summary>
        public readonly float ReverbZoneMix;

        /// <summary>True when a particle system or a graph was found.</summary>
        public readonly bool HasVisual;

        /// <summary>Particle prefab. Null when this effect has no particles.</summary>
        public readonly ParticleSystem Particles;

        /// <summary>Visual effect asset. Null when this effect has no graph.</summary>
        public readonly VisualEffectAsset Graph;

        /// <summary>Distance at which the visual is shown and culled, in meters.</summary>
        public readonly float VisibleDistance;

        /// <summary>Stores one resolved footstep.</summary>
        public WAVESResolvedFootstep(
            bool hasAudio,
            AudioResource audio,
            float audibleDistance,
            float minDistance,
            AudioRolloffMode rolloff,
            float dopplerLevel,
            AudioMixerGroup mixerGroup,
            float volume,
            float spatialBlend,
            float reverbZoneMix,
            bool hasVisual,
            ParticleSystem particles,
            VisualEffectAsset graph,
            float visibleDistance)
        {
            HasAudio = hasAudio;
            Audio = audio;
            AudibleDistance = audibleDistance;
            MinDistance = minDistance;
            Rolloff = rolloff;
            DopplerLevel = dopplerLevel;
            MixerGroup = mixerGroup;
            Volume = volume;
            SpatialBlend = spatialBlend;
            ReverbZoneMix = reverbZoneMix;
            HasVisual = hasVisual;
            Particles = particles;
            Graph = graph;
            VisibleDistance = visibleDistance;
        }
    }

    /// <summary>
    /// Sparse footstep reactions. A missing or disabled actor/surface group is skipped.
    /// A missing step type uses that group's fallback step, then the next group.
    /// An empty audio block or an empty visual block is filled from the next fallback on its own.
    /// </summary>
    [CreateAssetMenu(fileName = "FootstepModule", menuName = "MadeYellow/WAVES/Footstep Module")]
    [WAVESModuleName("Footsteps")]
    public sealed class WAVESFootstepModule : WAVESModuleBase
    {
        readonly struct CellKey : IEquatable<CellKey>
        {
            public readonly EntityId SurfaceId;
            public readonly EntityId ActorId;

            public CellKey(EntityId surfaceId, EntityId actorId)
            {
                SurfaceId = surfaceId;
                ActorId = actorId;
            }

            public bool Equals(CellKey other)
            {
                return SurfaceId == other.SurfaceId && ActorId == other.ActorId;
            }

            public override bool Equals(object obj)
            {
                return obj is CellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SurfaceId.GetHashCode() * 397) ^ ActorId.GetHashCode();
                }
            }
        }

        [SerializeField] List<ModuleCell<FootstepCell>> _cells = new List<ModuleCell<FootstepCell>>();

        /// <summary>Previous flat list. Moved into <see cref="_cells"/> on load.</summary>
        [SerializeField, HideInInspector] List<LegacyBinding> _bindings = new List<LegacyBinding>();

        /// <summary>1 after older per-step distances are kept as overrides.</summary>
        [SerializeField, HideInInspector] int _settingsVersion;

        Dictionary<CellKey, int> _index;
        WAVES _waves;
        bool _listening;
        bool _validating;
        bool _migrationDirty;

        void OnEnable()
        {
            if (MigrateLegacyBindings() | MigratePlaybackSettings())
                _migrationDirty = true;
            _index = null;
        }

        void OnValidate()
        {
            if (_validating)
                return;

            _validating = true;
            if (MigrateLegacyBindings() | MigratePlaybackSettings())
                _migrationDirty = true;
            EnsurePayloads();
            Deduplicate();
            _index = null;
#if UNITY_EDITOR
            if (_migrationDirty && !Application.isPlaying)
            {
                _migrationDirty = false;
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
            _validating = false;
        }

        /// <summary>Subscribes this module to <paramref name="waves"/>.</summary>
        public override void Bind(WAVES waves)
        {
            if (waves == null)
                return;

            if (_waves == null)
                _listening = false;
            else if (_waves != waves)
                Unbind(_waves);

            _waves = waves;
            if (_listening || waves.Bus == null)
                return;

            waves.Bus.Subscribe<ActorFootstepStarted>(OnFootstep);
            _listening = true;
        }

        /// <summary>Unsubscribes this module from <paramref name="waves"/>.</summary>
        public override void Unbind(WAVES waves)
        {
            if (_waves == null)
                return;
            if (waves != null && _waves != waves)
                return;

            if (_listening)
            {
                ScriptableEventBase bus = _waves.Bus;
                if (bus != null)
                    bus.Unsubscribe<ActorFootstepStarted>(OnFootstep);
                _listening = false;
            }

            _waves = null;
        }

        /// <summary>
        /// Fills <paramref name="effect"/> from the first audio block and the first visual block in fallback order.
        /// Playback settings come from that audio block when it overrides them, otherwise from its group's common settings.
        /// False when both are missing.
        /// </summary>
        public bool TryResolve(
            ActorProfile actor,
            SurfaceTypeDefinition surface,
            FootstepType step,
            out WAVESResolvedFootstep effect)
        {
            EnsureIndex();
            EntityId surfaceId = IdOf(surface);
            EntityId actorId = IdOf(actor);
            EntityId stepId = IdOf(step);

            bool hasAudio = false;
            AudioResource audio = null;
            float audible = 0f;
            float min = 0f;
            AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
            float doppler = 0f;
            AudioMixerGroup mixer = null;
            float volume = 1f;
            float spatialBlend = 1f;
            float reverbZoneMix = 1f;
            bool hasVisual = false;
            ParticleSystem particles = null;
            VisualEffectAsset graph = null;
            float visible = 0f;

            for (int pair = 0; pair < 4 && !(hasAudio && hasVisual); pair++)
            {
                CellKey key = PairAt(pair, surfaceId, actorId);
                if (!_index.TryGetValue(key, out int cellIndex))
                    continue;

                ModuleCell<FootstepCell> cell = _cells[cellIndex];
                FootstepCell data = cell != null ? cell.Data : null;
                if (data == null)
                    continue;

                for (int stepOrder = 0; stepOrder < 2 && !(hasAudio && hasVisual); stepOrder++)
                {
                    StepSlot slot = data.FindStep(stepOrder == 0 ? stepId : EntityId.None);
                    if (slot == null)
                        continue;

                    if (!hasAudio && slot.Audio != null)
                    {
                        hasAudio = true;
                        audio = slot.Audio;
                        bool custom = slot.OverrideSettings;
                        audible = custom ? slot.AudibleDistance : data.AudibleDistance;
                        min = custom ? slot.MinDistance : data.MinDistance;
                        rolloff = custom ? slot.Rolloff : data.Rolloff;
                        doppler = custom ? slot.DopplerLevel : data.DopplerLevel;
                        mixer = custom ? slot.MixerGroup : data.MixerGroup;
                        volume = custom ? slot.Volume : data.Volume;
                        spatialBlend = custom ? slot.SpatialBlend : data.SpatialBlend;
                        reverbZoneMix = custom ? slot.ReverbZoneMix : data.ReverbZoneMix;
                    }

                    if (!hasVisual && (slot.Particles != null || slot.Graph != null))
                    {
                        hasVisual = true;
                        particles = slot.Particles;
                        graph = slot.Graph;
                        visible = slot.OverrideVisual ? slot.VisibleDistance : data.VisibleDistance;
                    }
                }
            }

            effect = new WAVESResolvedFootstep(
                hasAudio,
                audio,
                audible,
                min,
                rolloff,
                doppler,
                mixer,
                volume,
                spatialBlend,
                reverbZoneMix,
                hasVisual,
                particles,
                graph,
                visible);
            return hasAudio || hasVisual;
        }

        /// <summary>Writes or removes one step inside its actor and surface group. An empty step is removed.</summary>
        internal void SetBinding(
            SurfaceTypeDefinition surface,
            ActorProfile actor,
            FootstepType step,
            AudioResource audio,
            float audibleDistance,
            float minDistance,
            AudioRolloffMode rolloff,
            float dopplerLevel,
            ParticleSystem particles,
            VisualEffectAsset graph,
            float visibleDistance,
            bool overrideSettings = false,
            AudioMixerGroup mixerGroup = null,
            float volume = 1f,
            float spatialBlend = 1f,
            float reverbZoneMix = 1f,
            bool overrideVisual = false)
        {
            bool empty = audio == null && particles == null && graph == null;
            int cellIndex = FindCell(surface, actor);
            if (empty)
            {
                if (cellIndex >= 0)
                {
                    _cells[cellIndex].EnsureData(new FootstepCell());
                    _cells[cellIndex].Data.RemoveStep(step);
                    if (_cells[cellIndex].Data.StepCount == 0)
                        _cells.RemoveAt(cellIndex);
                }
            }
            else
            {
                ModuleCell<FootstepCell> cell = cellIndex >= 0 ? _cells[cellIndex] : AddCell(surface, actor, true);
                cell.EnsureData(new FootstepCell());
                cell.Data.Upsert(
                    step,
                    audio,
                    audibleDistance,
                    minDistance,
                    rolloff,
                    dopplerLevel,
                    particles,
                    graph,
                    visibleDistance,
                    overrideSettings,
                    mixerGroup,
                    volume,
                    spatialBlend,
                    reverbZoneMix,
                    overrideVisual);
            }

            _index = null;
        }

        /// <summary>Playback used when a step in this group does not override its own settings.</summary>
        internal void SetCommonAudio(
            SurfaceTypeDefinition surface,
            ActorProfile actor,
            AudioMixerGroup mixerGroup,
            float volume,
            float spatialBlend,
            float reverbZoneMix,
            float audibleDistance,
            float minDistance,
            AudioRolloffMode rolloff,
            float dopplerLevel,
            float visibleDistance = 20f)
        {
            int cellIndex = FindCell(surface, actor);
            if (cellIndex < 0)
                return;

            _cells[cellIndex].EnsureData(new FootstepCell());
            _cells[cellIndex].Data.SetCommon(
                mixerGroup,
                volume,
                spatialBlend,
                reverbZoneMix,
                audibleDistance,
                minDistance,
                rolloff,
                dopplerLevel,
                visibleDistance);
        }

        /// <summary>Creates an enabled group with no steps when that pair is missing.</summary>
        internal void EnsureGroup(SurfaceTypeDefinition surface, ActorProfile actor)
        {
            if (FindCell(surface, actor) >= 0)
                return;

            AddCell(surface, actor, true);
            _index = null;
        }

        /// <summary>Turns an existing group on or off. Missing groups are left missing.</summary>
        internal void SetGroupEnabled(SurfaceTypeDefinition surface, ActorProfile actor, bool enabled)
        {
            int cellIndex = FindCell(surface, actor);
            if (cellIndex < 0)
                return;

            _cells[cellIndex].SetEnabled(enabled);
            _index = null;
        }

        void OnFootstep(ActorFootstepStarted step)
        {
            WAVES waves = _waves;
            if (waves == null || waves.Query == null)
                return;

            if (step.collider == null)
                return;

            waves.Query.TryGetSurface(step.collider, step.point, out SurfaceTypeDefinition surface);
            if (!TryResolve(step.actor, surface, step.type, out WAVESResolvedFootstep effect))
                return;

            Vector3 point = step.point;
            if (effect.HasAudio && waves.Audio != null)
            {
                waves.Audio.Play(
                    step.emitterId,
                    step.actor,
                    point,
                    effect.AudibleDistance,
                    effect.MinDistance,
                    effect.Rolloff,
                    effect.DopplerLevel,
                    effect.Audio,
                    effect.MixerGroup,
                    effect.Volume,
                    effect.SpatialBlend,
                    effect.ReverbZoneMix);
            }

            if (!effect.HasVisual || waves.Visual == null)
                return;

            Quaternion rotation = AlignFoot(step.rotation, step.normal);
            if (effect.Particles != null)
            {
                waves.Visual.PlayParticles(
                    step.emitterId,
                    step.actor,
                    point,
                    rotation,
                    effect.VisibleDistance,
                    effect.Particles);
            }

            if (effect.Graph != null)
            {
                waves.Visual.PlayGraph(
                    step.emitterId,
                    step.actor,
                    point,
                    rotation,
                    effect.VisibleDistance,
                    effect.Graph);
            }
        }

        /// <summary>Foot forward flattened to the ground plane, then laid onto the ground normal.</summary>
        static Quaternion AlignFoot(Quaternion foot, Vector3 normal)
        {
            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.up;
            else
                normal.Normalize();

            Vector3 forward = foot * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = foot * Vector3.right;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 1e-6f)
                return Quaternion.FromToRotation(Vector3.up, normal);

            forward = Vector3.ProjectOnPlane(forward, normal);
            if (forward.sqrMagnitude < 1e-6f)
                return Quaternion.FromToRotation(Vector3.up, normal);

            return Quaternion.LookRotation(forward, normal);
        }

        /// <summary>Same fallback order as before: exact pair, then actor, then surface, then both.</summary>
        static CellKey PairAt(int order, EntityId surfaceId, EntityId actorId)
        {
            switch (order)
            {
                case 0: return new CellKey(surfaceId, actorId);
                case 1: return new CellKey(surfaceId, EntityId.None);
                case 2: return new CellKey(EntityId.None, actorId);
                default: return new CellKey(EntityId.None, EntityId.None);
            }
        }

        void EnsureIndex()
        {
            if (_index != null)
                return;

            int count = _cells != null ? _cells.Count : 0;
            _index = new Dictionary<CellKey, int>(count);
            for (int i = 0; i < count; i++)
            {
                ModuleCell<FootstepCell> cell = _cells[i];
                if (cell == null || !cell.Enabled)
                    continue;

                var key = new CellKey(IdOf(cell.Surface), IdOf(cell.Actor));
                if (_index.ContainsKey(key))
                    _index[key] = i;
                else
                    _index.Add(key, i);
            }
        }

        int FindCell(SurfaceTypeDefinition surface, ActorProfile actor)
        {
            if (_cells == null)
                return -1;

            for (int i = 0; i < _cells.Count; i++)
            {
                ModuleCell<FootstepCell> cell = _cells[i];
                if (cell != null && cell.Surface == surface && cell.Actor == actor)
                    return i;
            }

            return -1;
        }

        ModuleCell<FootstepCell> AddCell(SurfaceTypeDefinition surface, ActorProfile actor, bool enabled)
        {
            if (_cells == null)
                _cells = new List<ModuleCell<FootstepCell>>();

            var cell = new ModuleCell<FootstepCell>();
            cell.Configure(surface, actor, enabled, new FootstepCell());
            _cells.Add(cell);
            return cell;
        }

        void EnsurePayloads()
        {
            if (_cells == null)
                return;

            for (int i = 0; i < _cells.Count; i++)
            {
                ModuleCell<FootstepCell> cell = _cells[i];
                if (cell != null)
                    cell.EnsureData(new FootstepCell());
            }
        }

        internal bool MigrateLegacyBindings()
        {
            if (_bindings == null || _bindings.Count == 0)
                return false;

            if (_cells == null)
                _cells = new List<ModuleCell<FootstepCell>>();

            for (int i = 0; i < _bindings.Count; i++)
            {
                LegacyBinding legacy = _bindings[i];
                if (legacy == null)
                    continue;

                int cellIndex = FindCell(legacy.Surface, legacy.Actor);
                ModuleCell<FootstepCell> cell = cellIndex >= 0
                    ? _cells[cellIndex]
                    : AddCell(legacy.Surface, legacy.Actor, true);
                cell.EnsureData(new FootstepCell());
                cell.Data.Upsert(
                    legacy.Step,
                    legacy.Audio,
                    legacy.AudibleDistance,
                    legacy.MinDistance,
                    legacy.Rolloff,
                    legacy.DopplerLevel,
                    legacy.Particles,
                    legacy.Graph,
                    legacy.VisibleDistance);
            }

            _bindings.Clear();
            return true;
        }

        /// <summary>
        /// Older assets stored distance and rolloff on every step, and a visible distance on every visual.
        /// Those values stay in use by turning the matching override on.
        /// Steps that already match the common defaults keep sharing the group settings.
        /// </summary>
        internal bool MigratePlaybackSettings()
        {
            if (_settingsVersion >= 2)
                return false;

            if (_cells != null)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    ModuleCell<FootstepCell> cell = _cells[i];
                    if (cell == null)
                        continue;

                    cell.EnsureData(new FootstepCell());
                    if (_settingsVersion < 1)
                        cell.Data.PromoteCustomPlayback();
                    cell.Data.PromoteCustomVisual();
                }
            }

            _settingsVersion = 2;
            return true;
        }

        static float Unit(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        void Deduplicate()
        {
            if (_cells == null)
                return;

            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                ModuleCell<FootstepCell> cell = _cells[i];
                if (cell == null)
                {
                    _cells.RemoveAt(i);
                    continue;
                }

                cell.EnsureData(new FootstepCell());
                cell.Data.Deduplicate();
                int earlier = 0;
                while (earlier < i)
                {
                    ModuleCell<FootstepCell> other = _cells[earlier];
                    if (other != null && other.Surface == cell.Surface && other.Actor == cell.Actor)
                    {
                        _cells.RemoveAt(earlier);
                        i--;
                        continue;
                    }

                    earlier++;
                }
            }
        }

        static EntityId IdOf(UnityEngine.Object asset)
        {
            return asset != null ? asset.GetEntityId() : EntityId.None;
        }

        [Serializable]
        internal sealed class FootstepCell
        {
            [SerializeField] List<StepSlot> _steps = new List<StepSlot>();
            [SerializeField] AudioMixerGroup _mixerGroup;
            [SerializeField] float _volume = WAVESEffectDefaults.Volume;
            [SerializeField] float _spatialBlend = WAVESEffectDefaults.SpatialBlend;
            [SerializeField] float _reverbZoneMix = WAVESEffectDefaults.ReverbZoneMix;
            [SerializeField] float _audibleDistance = WAVESEffectDefaults.AudibleDistance;
            [SerializeField] float _minDistance = WAVESEffectDefaults.MinDistance;
            [SerializeField] AudioRolloffMode _rolloff = WAVESEffectDefaults.Rolloff;
            [SerializeField] float _dopplerLevel;
            [SerializeField] float _visibleDistance = WAVESEffectDefaults.VisibleDistance;

            public int StepCount => _steps != null ? _steps.Count : 0;
            public AudioMixerGroup MixerGroup => _mixerGroup;
            public float Volume => _volume;
            public float SpatialBlend => _spatialBlend;
            public float ReverbZoneMix => _reverbZoneMix;
            public float AudibleDistance => _audibleDistance;
            public float MinDistance => _minDistance;
            public AudioRolloffMode Rolloff => _rolloff;
            public float DopplerLevel => _dopplerLevel;
            public float VisibleDistance => _visibleDistance;

            public void SetCommon(
                AudioMixerGroup mixerGroup,
                float volume,
                float spatialBlend,
                float reverbZoneMix,
                float audibleDistance,
                float minDistance,
                AudioRolloffMode rolloff,
                float dopplerLevel,
                float visibleDistance = 20f)
            {
                _mixerGroup = mixerGroup;
                _volume = Unit(volume);
                _spatialBlend = Unit(spatialBlend);
                _reverbZoneMix = Unit(reverbZoneMix);
                _audibleDistance = audibleDistance < 0f ? 0f : audibleDistance;
                _minDistance = minDistance < 0f ? 0f : minDistance;
                _rolloff = rolloff;
                _dopplerLevel = dopplerLevel < 0f ? 0f : dopplerLevel;
                _visibleDistance = visibleDistance < 0f ? 0f : visibleDistance;
            }

            public void PromoteCustomPlayback()
            {
                if (_steps == null)
                    return;

                for (int i = 0; i < _steps.Count; i++)
                {
                    StepSlot slot = _steps[i];
                    if (slot != null)
                        slot.PromoteCustomPlayback();
                }
            }

            public void PromoteCustomVisual()
            {
                if (_steps == null)
                    return;

                for (int i = 0; i < _steps.Count; i++)
                {
                    StepSlot slot = _steps[i];
                    if (slot != null)
                        slot.PromoteCustomVisual();
                }
            }

            public StepSlot FindStep(EntityId stepId)
            {
                if (_steps == null)
                    return null;

                for (int i = 0; i < _steps.Count; i++)
                {
                    StepSlot slot = _steps[i];
                    if (slot != null && IdOf(slot.Step) == stepId)
                        return slot;
                }

                return null;
            }

            public void RemoveStep(FootstepType step)
            {
                if (_steps == null)
                    return;

                for (int i = _steps.Count - 1; i >= 0; i--)
                {
                    StepSlot slot = _steps[i];
                    if (slot != null && slot.Step == step)
                        _steps.RemoveAt(i);
                }
            }

            public void Upsert(
                FootstepType step,
                AudioResource audio,
                float audibleDistance,
                float minDistance,
                AudioRolloffMode rolloff,
                float dopplerLevel,
                ParticleSystem particles,
                VisualEffectAsset graph,
                float visibleDistance,
                bool overrideSettings = false,
                AudioMixerGroup mixerGroup = null,
                float volume = 1f,
                float spatialBlend = 1f,
                float reverbZoneMix = 1f,
                bool overrideVisual = false)
            {
                if (_steps == null)
                    _steps = new List<StepSlot>();

                for (int i = 0; i < _steps.Count; i++)
                {
                    StepSlot slot = _steps[i];
                    if (slot != null && slot.Step == step)
                    {
                        slot.Set(
                            step,
                            audio,
                            audibleDistance,
                            minDistance,
                            rolloff,
                            dopplerLevel,
                    particles,
                    graph,
                    visibleDistance,
                    overrideSettings,
                    mixerGroup,
                    volume,
                    spatialBlend,
                    reverbZoneMix,
                    overrideVisual);
                        return;
                    }
                }

                var created = new StepSlot();
                created.Set(
                    step,
                    audio,
                    audibleDistance,
                    minDistance,
                    rolloff,
                    dopplerLevel,
                    particles,
                    graph,
                    visibleDistance,
                    overrideSettings,
                    mixerGroup,
                    volume,
                    spatialBlend,
                    reverbZoneMix,
                    overrideVisual);
                _steps.Add(created);
            }

            public void Deduplicate()
            {
                if (_steps == null)
                    return;

                for (int i = _steps.Count - 1; i >= 0; i--)
                {
                    StepSlot slot = _steps[i];
                    if (slot == null)
                    {
                        _steps.RemoveAt(i);
                        continue;
                    }

                    int earlier = 0;
                    while (earlier < i)
                    {
                        StepSlot other = _steps[earlier];
                        if (other != null && other.Step == slot.Step)
                        {
                            _steps.RemoveAt(earlier);
                            i--;
                            continue;
                        }

                        earlier++;
                    }
                }
            }
        }

        [Serializable]
        internal sealed class StepSlot
        {
            [SerializeField] FootstepType _step;
            [SerializeField] AudioResource _audio;
            [SerializeField] float _audibleDistance = WAVESEffectDefaults.AudibleDistance;
            [SerializeField] float _minDistance = WAVESEffectDefaults.MinDistance;
            [SerializeField] AudioRolloffMode _rolloff = WAVESEffectDefaults.Rolloff;
            [SerializeField] float _dopplerLevel;
            [SerializeField] bool _overrideSettings;
            [SerializeField] bool _overrideVisual;
            [SerializeField] AudioMixerGroup _mixerGroup;
            [SerializeField] float _volume = WAVESEffectDefaults.Volume;
            [SerializeField] float _spatialBlend = WAVESEffectDefaults.SpatialBlend;
            [SerializeField] float _reverbZoneMix = WAVESEffectDefaults.ReverbZoneMix;
            [SerializeField] ParticleSystem _particles;
            [SerializeField] VisualEffectAsset _graph;
            [SerializeField] float _visibleDistance = WAVESEffectDefaults.VisibleDistance;

            public FootstepType Step => _step;
            public AudioResource Audio => _audio;
            public float AudibleDistance => _audibleDistance;
            public float MinDistance => _minDistance;
            public AudioRolloffMode Rolloff => _rolloff;
            public float DopplerLevel => _dopplerLevel;
            public bool OverrideSettings => _overrideSettings;
            public bool OverrideVisual => _overrideVisual;
            public AudioMixerGroup MixerGroup => _mixerGroup;
            public float Volume => _volume;
            public float SpatialBlend => _spatialBlend;
            public float ReverbZoneMix => _reverbZoneMix;
            public ParticleSystem Particles => _particles;
            public VisualEffectAsset Graph => _graph;
            public float VisibleDistance => _visibleDistance;

            public void Set(
                FootstepType step,
                AudioResource audio,
                float audibleDistance,
                float minDistance,
                AudioRolloffMode rolloff,
                float dopplerLevel,
                ParticleSystem particles,
                VisualEffectAsset graph,
                float visibleDistance,
                bool overrideSettings = false,
                AudioMixerGroup mixerGroup = null,
                float volume = 1f,
                float spatialBlend = 1f,
                float reverbZoneMix = 1f,
                bool overrideVisual = false)
            {
                _step = step;
                _audio = audio;
                _audibleDistance = audibleDistance < 0f ? 0f : audibleDistance;
                _minDistance = minDistance < 0f ? 0f : minDistance;
                _rolloff = rolloff;
                _dopplerLevel = dopplerLevel < 0f ? 0f : dopplerLevel;
                _overrideSettings = overrideSettings;
                _overrideVisual = overrideVisual;
                _mixerGroup = mixerGroup;
                _volume = Unit(volume);
                _spatialBlend = Unit(spatialBlend);
                _reverbZoneMix = Unit(reverbZoneMix);
                _particles = particles;
                _graph = graph;
                _visibleDistance = visibleDistance < 0f ? 0f : visibleDistance;
            }

            /// <summary>Keeps a pre-existing custom distance or rolloff by switching this step to its own settings.</summary>
            public void PromoteCustomPlayback()
            {
                if (_overrideSettings)
                    return;

                if (!Mathf.Approximately(_audibleDistance, 15f)
                    || !Mathf.Approximately(_minDistance, 1f)
                    || _rolloff != AudioRolloffMode.Logarithmic
                    || !Mathf.Approximately(_dopplerLevel, 0f))
                    _overrideSettings = true;
            }

            /// <summary>Keeps a pre-existing custom visible distance by switching this step to its own visual settings.</summary>
            public void PromoteCustomVisual()
            {
                if (_overrideVisual)
                    return;

                if (!Mathf.Approximately(_visibleDistance, 20f))
                    _overrideVisual = true;
            }
        }

        [Serializable]
        internal sealed class LegacyBinding
        {
            [SerializeField] SurfaceTypeDefinition _surface;
            [SerializeField] ActorProfile _actor;
            [SerializeField] FootstepType _step;
            [SerializeField] AudioResource _audio;
            [SerializeField] float _audibleDistance = WAVESEffectDefaults.AudibleDistance;
            [SerializeField] float _minDistance = WAVESEffectDefaults.MinDistance;
            [SerializeField] AudioRolloffMode _rolloff = WAVESEffectDefaults.Rolloff;
            [SerializeField] float _dopplerLevel;
            [SerializeField] ParticleSystem _particles;
            [SerializeField] VisualEffectAsset _graph;
            [SerializeField] float _visibleDistance = WAVESEffectDefaults.VisibleDistance;

            public SurfaceTypeDefinition Surface => _surface;
            public ActorProfile Actor => _actor;
            public FootstepType Step => _step;
            public AudioResource Audio => _audio;
            public float AudibleDistance => _audibleDistance;
            public float MinDistance => _minDistance;
            public AudioRolloffMode Rolloff => _rolloff;
            public float DopplerLevel => _dopplerLevel;
            public ParticleSystem Particles => _particles;
            public VisualEffectAsset Graph => _graph;
            public float VisibleDistance => _visibleDistance;
        }
    }
}
