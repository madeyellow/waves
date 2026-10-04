using System.Collections.Generic;
using MadeYellow.WAVES.Footsteps;
using NUnit.Framework;
using UnityEngine;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class FootstepContactLatchTests
    {
        FootstepType _walk;
        FootstepType _run;
        List<FootstepType> _types;
        FootstepContactLatch _latch;

        [SetUp]
        public void SetUp()
        {
            _walk = ScriptableObject.CreateInstance<FootstepType>();
            _run = ScriptableObject.CreateInstance<FootstepType>();
            _walk.weight = 2f;
            _run.weight = 3f;
            _types = new List<FootstepType> { _walk, _run };
            _latch = new FootstepContactLatch();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_walk);
            Object.DestroyImmediate(_run);
        }

        [Test]
        public void RiseThroughWalk_ConfirmsRun_OnTheSecondHeldSample()
        {
            Assert.That(Feed(0.4f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(3f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(3f), Is.EqualTo(FootstepLatchSignal.Began));
            Assert.That(_latch.Type, Is.SameAs(_run));
            Assert.That(_latch.InContact, Is.True);
        }

        [Test]
        public void OneSampleAtWalk_DoesNotOpenARunStep()
        {
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(3f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(3f), Is.EqualTo(FootstepLatchSignal.Began));
            Assert.That(_latch.Type, Is.SameAs(_run));
        }

        [Test]
        public void HeldWalk_ConfirmsWalk()
        {
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.Began));
            Assert.That(_latch.Type, Is.SameAs(_walk));
        }

        [Test]
        public void ValueBetweenWeights_DoesNotMatchEitherType()
        {
            Assert.That(FootstepContactLatch.MatchPlateau(2.5f, _types), Is.Null);
            Assert.That(Feed(2.5f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(2.5f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(_latch.InContact, Is.False);
        }

        [Test]
        public void FallThroughWalk_KeepsTheRunType()
        {
            Feed(3f);
            Assert.That(Feed(3f), Is.EqualTo(FootstepLatchSignal.Began));

            Assert.That(Feed(2.4f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(1.2f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(_latch.Type, Is.SameAs(_run));

            Assert.That(Feed(0f), Is.EqualTo(FootstepLatchSignal.Ended));
            Assert.That(_latch.InContact, Is.False);
        }

        [Test]
        public void SecondPlateau_Retypes()
        {
            Feed(3f);
            Feed(3f);
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(2f), Is.EqualTo(FootstepLatchSignal.Retyped));
            Assert.That(_latch.Type, Is.SameAs(_walk));
        }

        [Test]
        public void Spike_DoesNotOpenOrClose()
        {
            Assert.That(Feed(3f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(Feed(0f), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(_latch.InContact, Is.False);
        }

        [Test]
        public void NoTypes_ConfirmsContactWithANullType()
        {
            Assert.That(_latch.Sample(1f, null), Is.EqualTo(FootstepLatchSignal.None));
            Assert.That(_latch.Sample(1f, null), Is.EqualTo(FootstepLatchSignal.Began));
            Assert.That(_latch.Type, Is.Null);
            Assert.That(_latch.Sample(0f, null), Is.EqualTo(FootstepLatchSignal.Ended));
        }

        FootstepLatchSignal Feed(float weight)
        {
            return _latch.Sample(weight, _types);
        }
    }
}
