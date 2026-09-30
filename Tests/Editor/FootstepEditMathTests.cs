using System.Collections.Generic;
using MadeYellow.WAVES.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class FootstepEditMathTests
    {
        [Test]
        public void ClipDuration_UsesTheFrameRangeFromTheClipSettings()
        {
            Assert.That(FootstepEditMath.ClipDuration(0f, 48f, 24f), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(FootstepEditMath.ClipDuration(10f, 26f, 30f), Is.EqualTo(16f / 30f).Within(0.0001f));
            Assert.That(FootstepEditMath.ClipDuration(5f, 5f, 24f), Is.EqualTo(0f));
        }

        [Test]
        public void Move_StaysInsideTheGapBetweenNeighbors()
        {
            var left = new FootstepMarker { start = 0f, end = 0.2f };
            var step = new FootstepMarker { start = 0.4f, end = 0.6f };
            var right = new FootstepMarker { start = 0.8f, end = 1f };
            var steps = new List<FootstepMarker> { left, step, right };

            FootstepEditMath.GetLimits(steps, step, 1f, out float gapStart, out float gapEnd);
            FootstepEditMath.Move(step, gapStart, gapEnd, 1f);

            Assert.That(gapStart, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(gapEnd, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(step.start, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(step.end, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void GetGap_FindsTheEmptyIntervalUnderThePointer()
        {
            var steps = new List<FootstepMarker>
            {
                new FootstepMarker { start = 0f, end = 0.2f },
                new FootstepMarker { start = 0.4f, end = 0.6f }
            };

            FootstepEditMath.GetGap(steps, 0.3f, 1f, out float gapStart, out float gapEnd);

            Assert.That(gapStart, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(gapEnd, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void SnapToFrame_PicksTheNearestFrameInsideTheRange()
        {
            Assert.That(FootstepEditMath.SnapToFrame(0.34f, 10f, 0f, 1f), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(FootstepEditMath.SnapToFrame(0.36f, 10f, 0f, 1f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(FootstepEditMath.SnapToFrame(0.02f, 10f, 0.15f, 0.45f), Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void DuplicateStart_UsesTheNextFrameWhenSnapping()
        {
            Assert.That(FootstepEditMath.DuplicateStart(0.5f, 60f, false), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(FootstepEditMath.DuplicateStart(0.5f, 60f, true), Is.EqualTo(31f / 60f).Within(0.0001f));
            Assert.That(FootstepEditMath.DuplicateStart(0.51f, 60f, true), Is.EqualTo(31f / 60f).Within(0.0001f));
        }
    }
}
