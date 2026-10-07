using System;
using System.Collections.Generic;
using MadeYellow.EventBus;
using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects;
using MadeYellow.WAVES.AudioVisualEffects.Modules;
using MadeYellow.WAVES.Jumps;
using MadeYellow.WAVES.Surfaces;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.VFX;

namespace MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule
{
    /// <summary>Audio and visual chosen for one jump or landing after fallback.</summary>
    public readonly struct WAVESResolvedJumpLand
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

        /// <summary>0 plays in 2D. 1 plays in 3D at the contact.</summary>
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

        /// <summary>Stores one resolved jump or landing.</summary>
        public WAVESResolvedJumpLand(
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
    /// Sparse jump and landing reactions. A missing or disabled actor/surface group is skipped.
    /// A jump uses that group's jump slot. A landing uses its fall type, then that group's fallback landing.
    /// An empty audio block or an empty visual block is filled from the next fallback on its own.
    /// </summary>
    [CreateAssetMenu(fileName = "New Jumps & Lands Preset", menuName = "MadeYellow/WAVES/Jumps & Lands Module")]
    [WAVESModuleName("Jumps & Lands")]
    public sealed class WAVESJumpLandModule : WAVESModuleBase
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

        [SerializeField] List<ModuleCell<JumpLandCell>> _cells = new List<ModuleCell<JumpLandCell>>();

        /// <summary>1 after a custom visible distance is kept as an override.</summary>
        [SerializeField, HideInInspector] int _settingsVersion;

        Dictionary<CellKey, int> _index;
        WAVES _waves;
        bool _listening;
        bool _validating;
        bool _migrationDirty;

        void OnEnable()
        {
            _index = null;
        }

        void OnValidate()
        {
            if (_validating)
                return;

            _validating = true;
            if (MigrateVisualSettings())
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

        /// <summary>
        /// Older presets stored visible distance on every jump and landing.
        /// A distance other than the shared default stays in use by turning override on.
        /// </summary>
        bool MigrateVisualSettings()
        {
            if (_settingsVersion >= 1)
                return false;

            if (_cells != null)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    ModuleCell<JumpLandCell> cell = _cells[i];
                    if (cell == null)
                        continue;

                    cell.EnsureData(new JumpLandCell());
                    cell.Data.PromoteCustomVisual();
                }
            }

            _settingsVersion = 1;
            return true;
        }

        /// <summary>Subscribes this module to jump and landing signals on <paramref name="waves"/>.</summary>
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

            waves.Bus.Subscribe<ActorJumpStarted>(OnJump);
            waves.Bus.Subscribe<ActorLanded>(OnLand);
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
                {
                    bus.Unsubscribe<ActorJumpStarted>(OnJump);
                    bus.Unsubscribe<ActorLanded>(OnLand);
                }

                _listening = false;
            }

            _waves = null;
        }

        /// <summary>
        /// Fills <paramref name="effect"/> from the first jump audio block and the first jump visual block in fallback order.
        /// Playback settings come from that audio block when it overrides them, otherwise from its group's common settings.
        /// False when both are missing.
        /// </summary>
        public bool TryResolveJump(
            ActorProfile actor,
            SurfaceTypeDefinition surface,
            out WAVESResolvedJumpLand effect)
        {
            return TryResolve(actor, surface, null, true, out effect);
        }

        /// <summary>
        /// Fills <paramref name="effect"/> from the first landing audio block and the first landing visual block in fallback order.
        /// A missing <paramref name="fall"/> uses that group's fallback landing, then the next group.
        /// Playback settings come from that audio block when it overrides them, otherwise from its group's common settings.
        /// False when both are missing.
        /// </summary>
        public bool TryResolveLand(
            ActorProfile actor,
            SurfaceTypeDefinition surface,
            ActorFallType fall,
            out WAVESResolvedJumpLand effect)
        {
            return TryResolve(actor, surface, fall, false, out effect);
        }

        bool TryResolve(
            ActorProfile actor,
            SurfaceTypeDefinition surface,
            ActorFallType fall,
            bool jump,
            out WAVESResolvedJumpLand effect)
        {
            EnsureIndex();
            EntityId surfaceId = IdOf(surface);
            EntityId actorId = IdOf(actor);
            EntityId fallId = IdOf(fall);

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

                ModuleCell<JumpLandCell> cell = _cells[cellIndex];
                JumpLandCell data = cell != null ? cell.Data : null;
                if (data == null)
                    continue;

                if (jump)
                {
                    Take(
                        data.Jump,
                        data,
                        ref hasAudio,
                        ref audio,
                        ref audible,
                        ref min,
                        ref rolloff,
                        ref doppler,
                        ref mixer,
                        ref volume,
                        ref spatialBlend,
                        ref reverbZoneMix,
                        ref hasVisual,
                        ref particles,
                        ref graph,
                        ref visible);
                    continue;
                }

                for (int fallOrder = 0; fallOrder < 2 && !(hasAudio && hasVisual); fallOrder++)
                {
                    EffectSlot slot = data.FindLand(fallOrder == 0 ? fallId : EntityId.None);
                    Take(
                        slot,
                        data,
                        ref hasAudio,
                        ref audio,
                        ref audible,
                        ref min,
                        ref rolloff,
                        ref doppler,
                        ref mixer,
                        ref volume,
                        ref spatialBlend,
                        ref reverbZoneMix,
                        ref hasVisual,
                        ref particles,
                        ref graph,
                        ref visible);
                }
            }

            effect = new WAVESResolvedJumpLand(
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

        static void Take(
            EffectSlot slot,
            JumpLandCell data,
            ref bool hasAudio,
            ref AudioResource audio,
            ref float audible,
            ref float min,
            ref AudioRolloffMode rolloff,
            ref float doppler,
            ref AudioMixerGroup mixer,
            ref float volume,
            ref float spatialBlend,
            ref float reverbZoneMix,
            ref bool hasVisual,
            ref ParticleSystem particles,
            ref VisualEffectAsset graph,
            ref float visible)
        {
            if (slot == null || data == null)
                return;

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

        /// <summary>Writes or clears the jump inside its actor and surface group. An empty jump is cleared.</summary>
        internal void SetJump(
            SurfaceTypeDefinition surface,
            ActorProfile actor,
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
                    _cells[cellIndex].EnsureData(new JumpLandCell());
                    _cells[cellIndex].Data.ClearJump();
                    if (_cells[cellIndex].Data.IsEmpty)
                        _cells.RemoveAt(cellIndex);
                }
            }
            else
            {
                ModuleCell<JumpLandCell> cell = cellIndex >= 0 ? _cells[cellIndex] : AddCell(surface, actor, true);
                cell.EnsureData(new JumpLandCell());
                cell.Data.SetJump(
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

        /// <summary>Writes or removes one landing inside its actor and surface group. An empty landing is removed.</summary>
        internal void SetLand(
            SurfaceTypeDefinition surface,
            ActorProfile actor,
            ActorFallType fall,
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
                    _cells[cellIndex].EnsureData(new JumpLandCell());
                    _cells[cellIndex].Data.RemoveLand(fall);
                    if (_cells[cellIndex].Data.IsEmpty)
                        _cells.RemoveAt(cellIndex);
                }
            }
            else
            {
                ModuleCell<JumpLandCell> cell = cellIndex >= 0 ? _cells[cellIndex] : AddCell(surface, actor, true);
                cell.EnsureData(new JumpLandCell());
                cell.Data.UpsertLand(
                    fall,
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

        /// <summary>Playback used when a jump or landing in this group does not override its own settings.</summary>
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

            _cells[cellIndex].EnsureData(new JumpLandCell());
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

        /// <summary>Creates an enabled group with no effects when that pair is missing.</summary>
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

        void OnJump(ActorJumpStarted jump)
        {
            WAVES waves = _waves;
            if (waves == null || waves.Query == null || jump.collider == null)
                return;

            waves.Query.TryGetSurface(jump.collider, jump.point, out SurfaceTypeDefinition surface);
            if (!TryResolveJump(jump.actor, surface, out WAVESResolvedJumpLand effect))
                return;

            Play(waves, jump.emitterId, jump.actor, jump.point, jump.normal, effect);
        }

        void OnLand(ActorLanded land)
        {
            WAVES waves = _waves;
            if (waves == null || waves.Query == null || land.collider == null)
                return;

            waves.Query.TryGetSurface(land.collider, land.point, out SurfaceTypeDefinition surface);
            if (!TryResolveLand(land.actor, surface, land.type, out WAVESResolvedJumpLand effect))
                return;

            Play(waves, land.emitterId, land.actor, land.point, land.normal, effect);
        }

        static void Play(
            WAVES waves,
            EntityId emitterId,
            ActorProfile actor,
            Vector3 point,
            Vector3 normal,
            WAVESResolvedJumpLand effect)
        {
            if (effect.HasAudio && waves.Audio != null)
            {
                waves.Audio.Play(
                    emitterId,
                    actor,
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

            Quaternion rotation = AlignToGround(normal);
            if (effect.Particles != null)
            {
                waves.Visual.PlayParticles(
                    emitterId,
                    actor,
                    point,
                    rotation,
                    effect.VisibleDistance,
                    effect.Particles);
            }

            if (effect.Graph != null)
            {
                waves.Visual.PlayGraph(
                    emitterId,
                    actor,
                    point,
                    rotation,
                    effect.VisibleDistance,
                    effect.Graph);
            }
        }

        /// <summary>Lays world up onto the ground normal.</summary>
        static Quaternion AlignToGround(Vector3 normal)
        {
            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.up;
            else
                normal.Normalize();

            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        /// <summary>Same fallback order as footsteps: exact pair, then actor, then surface, then both.</summary>
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
                ModuleCell<JumpLandCell> cell = _cells[i];
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
                ModuleCell<JumpLandCell> cell = _cells[i];
                if (cell != null && cell.Surface == surface && cell.Actor == actor)
                    return i;
            }

            return -1;
        }

        ModuleCell<JumpLandCell> AddCell(SurfaceTypeDefinition surface, ActorProfile actor, bool enabled)
        {
            if (_cells == null)
                _cells = new List<ModuleCell<JumpLandCell>>();

            var cell = new ModuleCell<JumpLandCell>();
            cell.Configure(surface, actor, enabled, new JumpLandCell());
            _cells.Add(cell);
            return cell;
        }

        void EnsurePayloads()
        {
            if (_cells == null)
                return;

            for (int i = 0; i < _cells.Count; i++)
            {
                ModuleCell<JumpLandCell> cell = _cells[i];
                if (cell != null)
                    cell.EnsureData(new JumpLandCell());
            }
        }

        void Deduplicate()
        {
            if (_cells == null)
                return;

            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                ModuleCell<JumpLandCell> cell = _cells[i];
                if (cell == null)
                {
                    _cells.RemoveAt(i);
                    continue;
                }

                cell.EnsureData(new JumpLandCell());
                cell.Data.Deduplicate();
                int earlier = 0;
                while (earlier < i)
                {
                    ModuleCell<JumpLandCell> other = _cells[earlier];
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

        static float Unit(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        [Serializable]
        internal sealed class JumpLandCell
        {
            [SerializeField] EffectSlot _jump = new EffectSlot();
            [SerializeField] List<EffectSlot> _lands = new List<EffectSlot>();
            [SerializeField] AudioMixerGroup _mixerGroup;
            [SerializeField] float _volume = WAVESEffectDefaults.Volume;
            [SerializeField] float _spatialBlend = WAVESEffectDefaults.SpatialBlend;
            [SerializeField] float _reverbZoneMix = WAVESEffectDefaults.ReverbZoneMix;
            [SerializeField] float _audibleDistance = WAVESEffectDefaults.AudibleDistance;
            [SerializeField] float _minDistance = WAVESEffectDefaults.MinDistance;
            [SerializeField] AudioRolloffMode _rolloff = WAVESEffectDefaults.Rolloff;
            [SerializeField] float _dopplerLevel;
            [SerializeField] float _visibleDistance = WAVESEffectDefaults.VisibleDistance;

            public EffectSlot Jump => _jump;
            public int LandCount => _lands != null ? _lands.Count : 0;
            public bool IsEmpty => !HasContent(_jump) && LandCount == 0;
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

            public void PromoteCustomVisual()
            {
                if (_jump != null)
                    _jump.PromoteCustomVisual();
                if (_lands == null)
                    return;

                for (int i = 0; i < _lands.Count; i++)
                {
                    EffectSlot slot = _lands[i];
                    if (slot != null)
                        slot.PromoteCustomVisual();
                }
            }

            public void ClearJump()
            {
                if (_jump == null)
                    _jump = new EffectSlot();

                _jump.Clear();
            }

            public void SetJump(
                AudioResource audio,
                float audibleDistance,
                float minDistance,
                AudioRolloffMode rolloff,
                float dopplerLevel,
                ParticleSystem particles,
                VisualEffectAsset graph,
                float visibleDistance,
                bool overrideSettings,
                AudioMixerGroup mixerGroup,
                float volume,
                float spatialBlend,
                float reverbZoneMix,
                bool overrideVisual)
            {
                if (_jump == null)
                    _jump = new EffectSlot();

                _jump.Set(
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

            public EffectSlot FindLand(EntityId fallId)
            {
                if (_lands == null)
                    return null;

                for (int i = 0; i < _lands.Count; i++)
                {
                    EffectSlot slot = _lands[i];
                    if (slot != null && IdOf(slot.Fall) == fallId)
                        return slot;
                }

                return null;
            }

            public void RemoveLand(ActorFallType fall)
            {
                if (_lands == null)
                    return;

                for (int i = _lands.Count - 1; i >= 0; i--)
                {
                    EffectSlot slot = _lands[i];
                    if (slot != null && slot.Fall == fall)
                        _lands.RemoveAt(i);
                }
            }

            public void UpsertLand(
                ActorFallType fall,
                AudioResource audio,
                float audibleDistance,
                float minDistance,
                AudioRolloffMode rolloff,
                float dopplerLevel,
                ParticleSystem particles,
                VisualEffectAsset graph,
                float visibleDistance,
                bool overrideSettings,
                AudioMixerGroup mixerGroup,
                float volume,
                float spatialBlend,
                float reverbZoneMix,
                bool overrideVisual)
            {
                if (_lands == null)
                    _lands = new List<EffectSlot>();

                for (int i = 0; i < _lands.Count; i++)
                {
                    EffectSlot slot = _lands[i];
                    if (slot != null && slot.Fall == fall)
                    {
                        slot.Set(
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

                var created = new EffectSlot();
                created.Bind(fall);
                created.Set(
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
                _lands.Add(created);
            }

            public void Deduplicate()
            {
                if (_lands == null)
                    return;

                for (int i = _lands.Count - 1; i >= 0; i--)
                {
                    EffectSlot slot = _lands[i];
                    if (slot == null)
                    {
                        _lands.RemoveAt(i);
                        continue;
                    }

                    int earlier = 0;
                    while (earlier < i)
                    {
                        EffectSlot other = _lands[earlier];
                        if (other != null && other.Fall == slot.Fall)
                        {
                            _lands.RemoveAt(earlier);
                            i--;
                            continue;
                        }

                        earlier++;
                    }
                }
            }

            static bool HasContent(EffectSlot slot)
            {
                return slot != null && (slot.Audio != null || slot.Particles != null || slot.Graph != null);
            }
        }

        [Serializable]
        internal sealed class EffectSlot
        {
            [SerializeField] ActorFallType _fall;
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

            public ActorFallType Fall => _fall;
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

            public void Bind(ActorFallType fall)
            {
                _fall = fall;
            }

            public void Clear()
            {
                _audio = null;
                _particles = null;
                _graph = null;
                _overrideSettings = false;
                _overrideVisual = false;
            }

            public void Set(
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

            /// <summary>Keeps a pre-existing custom visible distance by switching this effect to its own visual settings.</summary>
            public void PromoteCustomVisual()
            {
                if (_overrideVisual)
                    return;

                if (!Mathf.Approximately(_visibleDistance, 20f))
                    _overrideVisual = true;
            }
        }
    }
}
