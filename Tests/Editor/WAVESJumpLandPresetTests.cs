using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects.Modules.JumpsAndLandsModule;
using MadeYellow.WAVES.Footsteps;
using MadeYellow.WAVES.Jumps;
using MadeYellow.WAVES.Surfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class WAVESJumpLandPresetTests
    {
        WAVESJumpLandModule _preset;
        SurfaceTypeDefinition _surface;
        ActorProfile _actor;
        ActorFallType _fall;
        AudioClip _jumpClip;
        AudioClip _landClip;
        AudioClip _fallbackClip;

        [SetUp]
        public void SetUp()
        {
            _preset = ScriptableObject.CreateInstance<WAVESJumpLandModule>();
            _surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            _actor = ScriptableObject.CreateInstance<ActorProfile>();
            _fall = ScriptableObject.CreateInstance<ActorFallType>();
            _jumpClip = AudioClip.Create("jump", 8, 1, 1000, false);
            _landClip = AudioClip.Create("land", 8, 1, 1000, false);
            _fallbackClip = AudioClip.Create("fallback", 8, 1, 1000, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_preset);
            Object.DestroyImmediate(_surface);
            Object.DestroyImmediate(_actor);
            Object.DestroyImmediate(_fall);
            Object.DestroyImmediate(_jumpClip);
            Object.DestroyImmediate(_landClip);
            Object.DestroyImmediate(_fallbackClip);
        }

        [Test]
        public void TryResolveJump_UsesSurfaceFallbackWhenTheSurfaceHasNoAudio()
        {
            _preset.SetJump(
                null, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f,
                true);

            bool found = _preset.TryResolveJump(_actor, _surface, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(found);
            Assert.AreSame(_fallbackClip, effect.Audio);
            Assert.AreEqual(12f, effect.AudibleDistance);
            Assert.AreEqual(AudioRolloffMode.Linear, effect.Rolloff);
        }

        [Test]
        public void TryResolveJump_PrefersTheExactGroup()
        {
            _preset.SetJump(
                null, null,
                _fallbackClip, 10f, 1f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 8f);
            _preset.SetJump(
                _surface, _actor,
                _jumpClip, 4f, 0.5f, AudioRolloffMode.Linear, 0f,
                null, null, 3f,
                true);

            bool found = _preset.TryResolveJump(_actor, _surface, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(found);
            Assert.AreSame(_jumpClip, effect.Audio);
            Assert.AreEqual(4f, effect.AudibleDistance);
        }

        [Test]
        public void TryResolveJump_IgnoresLandingEffects()
        {
            _preset.SetLand(
                _surface, _actor, _fall,
                _landClip, 4f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);

            bool found = _preset.TryResolveJump(_actor, _surface, out WAVESResolvedJumpLand effect);

            Assert.IsFalse(found);
            Assert.IsFalse(effect.HasAudio);
        }

        [Test]
        public void TryResolveLand_UsesAnyWhenTheTypeHasNoEffect()
        {
            _preset.SetLand(
                _surface, _actor, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f,
                true);

            bool found = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(found);
            Assert.AreSame(_fallbackClip, effect.Audio);
            Assert.AreEqual(12f, effect.AudibleDistance);
        }

        [Test]
        public void TryResolveLand_PrefersTheExactType()
        {
            _preset.SetLand(
                _surface, _actor, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f);
            _preset.SetLand(
                _surface, _actor, _fall,
                _landClip, 4f, 0.5f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 3f,
                true);

            bool found = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(found);
            Assert.AreSame(_landClip, effect.Audio);
            Assert.AreEqual(4f, effect.AudibleDistance);
        }

        [Test]
        public void TryResolveLand_DoesNotUseADifferentFallType()
        {
            var other = ScriptableObject.CreateInstance<ActorFallType>();
            _preset.SetLand(
                _surface, _actor, _fall,
                _landClip, 4f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);

            bool found = _preset.TryResolveLand(_actor, _surface, other, out WAVESResolvedJumpLand effect);

            Assert.IsFalse(found);
            Assert.IsFalse(effect.HasAudio);
            Object.DestroyImmediate(other);
        }

        [Test]
        public void TryResolveLand_IgnoresTheJumpSlot()
        {
            _preset.SetJump(
                _surface, _actor,
                _jumpClip, 4f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);

            bool found = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand effect);

            Assert.IsFalse(found);
            Assert.IsFalse(effect.HasAudio);
        }

        [Test]
        public void TryResolveLand_FillsAudioAndVisualFromDifferentSlots()
        {
            var particles = new GameObject("burst").AddComponent<ParticleSystem>();
            _preset.SetLand(
                _surface, _actor, null,
                _fallbackClip, 9f, 1f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 8f);
            _preset.SetLand(
                _surface, _actor, _fall,
                null, 1f, 1f, AudioRolloffMode.Logarithmic, 0f,
                particles, null, 6f,
                overrideVisual: true);

            bool found = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(found);
            Assert.IsTrue(effect.HasAudio);
            Assert.AreSame(_fallbackClip, effect.Audio);
            Assert.IsTrue(effect.HasVisual);
            Assert.AreSame(particles, effect.Particles);
            Assert.AreEqual(6f, effect.VisibleDistance);
            Object.DestroyImmediate(particles.gameObject);
        }

        [Test]
        public void TryResolveLand_UsesCommonPlaybackUnlessTheTypeOverridesIt()
        {
            _preset.SetLand(
                _surface, _actor, _fall,
                _landClip, 4f, 0.5f, AudioRolloffMode.Linear, 0.2f,
                null, null, 3f);
            _preset.SetCommonAudio(
                _surface, _actor,
                null, 0.35f, 0.1f, 0.7f,
                11f, 2f, AudioRolloffMode.Logarithmic, 0.4f);

            bool shared = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand common);

            Assert.IsTrue(shared);
            Assert.AreEqual(0.35f, common.Volume);
            Assert.AreEqual(11f, common.AudibleDistance);
            Assert.AreEqual(AudioRolloffMode.Logarithmic, common.Rolloff);

            _preset.SetLand(
                _surface, _actor, _fall,
                _landClip, 4f, 0.5f, AudioRolloffMode.Linear, 0.2f,
                null, null, 3f,
                true, null, 0.8f, 1f, 0.15f);

            bool custom = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(custom);
            Assert.AreEqual(0.8f, effect.Volume);
            Assert.AreEqual(1f, effect.SpatialBlend);
            Assert.AreEqual(0.15f, effect.ReverbZoneMix);
            Assert.AreEqual(4f, effect.AudibleDistance);
            Assert.AreEqual(0.5f, effect.MinDistance);
            Assert.AreEqual(AudioRolloffMode.Linear, effect.Rolloff);
            Assert.AreEqual(0.2f, effect.DopplerLevel);
        }

        [Test]
        public void TryResolveLand_UsesTheGroupVisibleDistanceUnlessTheTypeOverridesIt()
        {
            var particles = new GameObject("burst").AddComponent<ParticleSystem>();
            _preset.SetLand(
                _surface, _actor, _fall,
                null, 1f, 1f, AudioRolloffMode.Logarithmic, 0f,
                particles, null, 6f);
            _preset.SetCommonAudio(
                _surface, _actor,
                null, 1f, 1f, 1f,
                15f, 1f, AudioRolloffMode.Logarithmic, 0f,
                30f);

            bool shared = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand common);

            Assert.IsTrue(shared);
            Assert.AreEqual(30f, common.VisibleDistance);

            _preset.SetLand(
                _surface, _actor, _fall,
                null, 1f, 1f, AudioRolloffMode.Logarithmic, 0f,
                particles, null, 6f,
                overrideVisual: true);
            bool custom = _preset.TryResolveLand(_actor, _surface, _fall, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(custom);
            Assert.AreEqual(6f, effect.VisibleDistance);
            Object.DestroyImmediate(particles.gameObject);
        }

        [Test]
        public void TryResolve_SkipsADisabledGroupUntilItIsEnabledAgain()
        {
            _preset.SetJump(
                _surface, _actor,
                _jumpClip, 4f, 0.5f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);
            _preset.SetJump(
                null, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f);

            _preset.SetGroupEnabled(_surface, _actor, false);
            bool skipped = _preset.TryResolveJump(_actor, _surface, out WAVESResolvedJumpLand muted);

            Assert.IsTrue(skipped);
            Assert.AreSame(_fallbackClip, muted.Audio);

            _preset.SetGroupEnabled(_surface, _actor, true);
            bool restored = _preset.TryResolveJump(_actor, _surface, out WAVESResolvedJumpLand effect);

            Assert.IsTrue(restored);
            Assert.AreSame(_jumpClip, effect.Audio);
        }

        [Test]
        public void SetJump_KeepsTheLandingInTheSameCell()
        {
            _preset.SetJump(
                _surface, _actor,
                _jumpClip, 4f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);
            _preset.SetLand(
                _surface, _actor, _fall,
                _landClip, 5f, 1f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 4f);

            var preset = new UnityEditor.SerializedObject(_preset);
            UnityEditor.SerializedProperty cells = preset.FindProperty("_cells");
            UnityEditor.SerializedProperty lands = cells.GetArrayElementAtIndex(0).FindPropertyRelative("_data._lands");
            UnityEditor.SerializedProperty audio = cells.GetArrayElementAtIndex(0).FindPropertyRelative("_data._jump._audio");

            Assert.AreEqual(1, cells.arraySize);
            Assert.AreEqual(1, lands.arraySize);
            Assert.AreSame(_jumpClip, audio.objectReferenceValue);
        }

        [Test]
        public void PublishJump_DoesNothingWithoutABusOrActor()
        {
            var go = new GameObject("jumper");
            try
            {
                WAVESDispatcher dispatcher = go.AddComponent<WAVESDispatcher>();
                Assert.DoesNotThrow(() => dispatcher.PublishJump(null, Vector3.zero, Vector3.up));
                Assert.DoesNotThrow(() => dispatcher.PublishLand(_fall, null, Vector3.zero, Vector3.up));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
