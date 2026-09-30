using MadeYellow.WAVES.Actors;
using MadeYellow.WAVES.AudioVisualEffects.Modules.FootstepsModule;
using MadeYellow.WAVES.Footsteps;
using MadeYellow.WAVES.Surfaces;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class WAVESFootstepPresetTests
    {
        WAVESFootstepModule _preset;
        SurfaceTypeDefinition _surface;
        ActorProfile _actor;
        FootstepType _step;
        AudioClip _stepClip;
        AudioClip _fallbackClip;

        [SetUp]
        public void SetUp()
        {
            _preset = ScriptableObject.CreateInstance<WAVESFootstepModule>();
            _surface = ScriptableObject.CreateInstance<SurfaceTypeDefinition>();
            _actor = ScriptableObject.CreateInstance<ActorProfile>();
            _step = ScriptableObject.CreateInstance<FootstepType>();
            _stepClip = AudioClip.Create("step", 8, 1, 1000, false);
            _fallbackClip = AudioClip.Create("fallback", 8, 1, 1000, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_preset);
            Object.DestroyImmediate(_surface);
            Object.DestroyImmediate(_actor);
            Object.DestroyImmediate(_step);
            Object.DestroyImmediate(_stepClip);
            Object.DestroyImmediate(_fallbackClip);
        }

        [Test]
        public void TryResolve_UsesSurfaceFallbackWhenTheSurfaceHasNoAudio()
        {
            _preset.SetBinding(
                null, null, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f);

            bool found = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep effect);

            Assert.IsTrue(found);
            Assert.AreSame(_fallbackClip, effect.Audio);
            Assert.AreEqual(12f, effect.AudibleDistance);
            Assert.AreEqual(AudioRolloffMode.Linear, effect.Rolloff);
        }

        [Test]
        public void TryResolve_PrefersExactStepOverFallbacks()
        {
            _preset.SetBinding(
                null, null, null,
                _fallbackClip, 10f, 1f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 8f);
            _preset.SetBinding(
                _surface, _actor, _step,
                _stepClip, 4f, 0.5f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);

            bool found = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep effect);

            Assert.IsTrue(found);
            Assert.AreSame(_stepClip, effect.Audio);
            Assert.AreEqual(4f, effect.AudibleDistance);
        }

        [Test]
        public void TryResolve_FillsAudioAndVisualFromDifferentLevels()
        {
            var particles = new GameObject("burst").AddComponent<ParticleSystem>();
            _preset.SetBinding(
                _surface, _actor, null,
                _fallbackClip, 9f, 1f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 8f);
            _preset.SetBinding(
                _surface, _actor, _step,
                null, 1f, 1f, AudioRolloffMode.Logarithmic, 0f,
                particles, null, 6f);

            bool found = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep effect);

            Assert.IsTrue(found);
            Assert.IsTrue(effect.HasAudio);
            Assert.AreSame(_fallbackClip, effect.Audio);
            Assert.IsTrue(effect.HasVisual);
            Assert.AreSame(particles, effect.Particles);
            Assert.AreEqual(6f, effect.VisibleDistance);
            Object.DestroyImmediate(particles.gameObject);
        }

        [Test]
        public void TryResolve_ReturnsFalseWhenNothingIsAssigned()
        {
            bool found = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep effect);

            Assert.IsFalse(found);
            Assert.IsFalse(effect.HasAudio);
            Assert.IsFalse(effect.HasVisual);
        }

        [Test]
        public void TryResolve_SkipsADisabledGroupUntilItIsEnabledAgain()
        {
            _preset.SetBinding(
                _surface, _actor, _step,
                _stepClip, 4f, 0.5f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);
            _preset.SetBinding(
                null, null, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f);

            _preset.SetGroupEnabled(_surface, _actor, false);
            bool skipped = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep muted);

            Assert.IsTrue(skipped);
            Assert.AreSame(_fallbackClip, muted.Audio);

            _preset.SetGroupEnabled(_surface, _actor, true);
            bool restored = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep effect);

            Assert.IsTrue(restored);
            Assert.AreSame(_stepClip, effect.Audio);
        }

        [Test]
        public void TryResolve_EmptyEnabledGroupDoesNotBlockFallback()
        {
            _preset.EnsureGroup(_surface, _actor);
            _preset.SetBinding(
                null, null, null,
                _fallbackClip, 12f, 1f, AudioRolloffMode.Linear, 0f,
                null, null, 8f);

            bool found = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep effect);

            Assert.IsTrue(found);
            Assert.AreSame(_fallbackClip, effect.Audio);
        }

        [Test]
        public void SetBinding_KeepsBothStepsInOneActorSurfaceCell()
        {
            _preset.SetBinding(
                _surface, _actor, _step,
                _stepClip, 4f, 0.5f, AudioRolloffMode.Linear, 0f,
                null, null, 3f);
            _preset.SetBinding(
                _surface, _actor, null,
                _fallbackClip, 9f, 1f, AudioRolloffMode.Logarithmic, 0f,
                null, null, 8f);

            var preset = new UnityEditor.SerializedObject(_preset);
            UnityEditor.SerializedProperty cells = preset.FindProperty("_cells");
            UnityEditor.SerializedProperty steps = cells.GetArrayElementAtIndex(0).FindPropertyRelative("_data._steps");

            Assert.AreEqual(1, cells.arraySize);
            Assert.AreEqual(2, steps.arraySize);
        }

        [Test]
        public void LegacyBindings_MoveIntoOneCellAndKeepStepFallback()
        {
            var other = ScriptableObject.CreateInstance<FootstepType>();
            var preset = new UnityEditor.SerializedObject(_preset);
            UnityEditor.SerializedProperty bindings = preset.FindProperty("_bindings");
            bindings.arraySize = 2;
            WriteLegacy(bindings.GetArrayElementAtIndex(0), _surface, _actor, _step, _stepClip);
            WriteLegacy(bindings.GetArrayElementAtIndex(1), _surface, _actor, null, _fallbackClip);
            preset.ApplyModifiedPropertiesWithoutUndo();
            _preset.MigrateLegacyBindings();

            bool exact = _preset.TryResolve(_actor, _surface, _step, out WAVESResolvedFootstep exactEffect);
            bool missed = _preset.TryResolve(_actor, _surface, other, out WAVESResolvedFootstep missedEffect);

            Assert.IsTrue(exact);
            Assert.AreSame(_stepClip, exactEffect.Audio);
            Assert.IsTrue(missed);
            Assert.AreSame(_fallbackClip, missedEffect.Audio);
            Object.DestroyImmediate(other);
        }

        static void WriteLegacy(
            UnityEditor.SerializedProperty element,
            SurfaceTypeDefinition surface,
            ActorProfile actor,
            FootstepType step,
            AudioClip clip)
        {
            element.FindPropertyRelative("_surface").objectReferenceValue = surface;
            element.FindPropertyRelative("_actor").objectReferenceValue = actor;
            element.FindPropertyRelative("_step").objectReferenceValue = step;
            element.FindPropertyRelative("_audio").objectReferenceValue = clip;
        }
    }
}
