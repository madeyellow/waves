using System.Collections.Generic;
using MadeYellow.WAVES.Editor;
using MadeYellow.WAVES.Footsteps;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Tests.Editor
{
    public class FootstepCurveIOTests
    {
        [Test]
        public void Merge_WritesConstantSteps_KeepsOtherCurves_AndReadsThemBack()
        {
            var walk = ScriptableObject.CreateInstance<FootstepType>();
            var run = ScriptableObject.CreateInstance<FootstepType>();
            walk.weight = 1.5f;
            run.weight = 3f;
            walk.name = "Walk";
            run.name = "Run";

            var steps = new List<FootstepMarker>
            {
                new FootstepMarker { start = 0.2f, end = 0.5f, type = walk, weight = walk.weight },
                new FootstepMarker { start = 0.7f, end = 0.9f, type = run, weight = run.weight }
            };
            var tracks = new List<FootstepTrackState>
            {
                new FootstepTrackState { name = "LeftFoot", steps = steps },
                new FootstepTrackState { name = "RightFoot", steps = new List<FootstepMarker>() }
            };
            var existing = new[]
            {
                new ClipAnimationInfoCurve
                {
                    name = "Speed",
                    curve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
                },
                new ClipAnimationInfoCurve
                {
                    name = "LeftFoot",
                    curve = AnimationCurve.Constant(0f, 1f, 9f)
                }
            };

            try
            {
                ClipAnimationInfoCurve[] merged = FootstepCurveIO.Merge(existing, tracks, 1f, 60f);

                Assert.That(merged, Has.Length.EqualTo(3));
                Assert.That(merged[0].name, Is.EqualTo("Speed"));
                Assert.That(merged[0].curve.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(merged[1].name, Is.EqualTo("LeftFoot"));
                Assert.That(merged[2].name, Is.EqualTo("RightFoot"));

                AnimationCurve curve = merged[1].curve;
                Assert.That(
                    AnimationUtility.GetKeyRightTangentMode(curve, 0),
                    Is.EqualTo(AnimationUtility.TangentMode.Constant));
                Assert.That(curve.Evaluate(0.1f), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(curve.Evaluate(0.35f), Is.EqualTo(1.5f).Within(0.0001f));
                Assert.That(curve.Evaluate(0.6f), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(curve.Evaluate(0.8f), Is.EqualTo(3f).Within(0.0001f));

                List<FootstepMarker> loaded = FootstepCurveIO.ReadCurve(curve, 1f, 60f, new[] { run, walk });
                Assert.That(loaded, Has.Count.EqualTo(2));
                Assert.That(loaded[0].start, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(loaded[0].end, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(loaded[0].type, Is.SameAs(walk));
                Assert.That(loaded[1].start, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(loaded[1].end, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(loaded[1].type, Is.SameAs(run));

                List<FootstepMarker> empty = FootstepCurveIO.ReadCurve(merged[2].curve, 1f, 30f, new FootstepType[0]);
                Assert.That(empty, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(walk);
                Object.DestroyImmediate(run);
            }
        }

        [Test]
        public void Merge_UsesBakeCurveName_AndLeavesTheTrackNamedCurve()
        {
            var walk = ScriptableObject.CreateInstance<FootstepType>();
            walk.weight = 1f;
            walk.name = "Walk";
            var tracks = new List<FootstepTrackState>
            {
                new FootstepTrackState
                {
                    name = "LeftFoot",
                    bakeIntoAnimationCurve = "LeftFootContact",
                    steps = new List<FootstepMarker>
                    {
                        new FootstepMarker { start = 0.2f, end = 0.4f, type = walk, weight = 1f }
                    }
                }
            };
            var existing = new[]
            {
                new ClipAnimationInfoCurve
                {
                    name = "LeftFoot",
                    curve = AnimationCurve.Constant(0f, 1f, 9f)
                }
            };

            try
            {
                ClipAnimationInfoCurve[] merged = FootstepCurveIO.Merge(existing, tracks, 1f, 60f);
                Assert.That(merged, Has.Length.EqualTo(2));
                Assert.That(merged[0].name, Is.EqualTo("LeftFoot"));
                Assert.That(merged[0].curve.Evaluate(0.5f), Is.EqualTo(9f).Within(0.0001f));
                Assert.That(merged[1].name, Is.EqualTo("LeftFootContact"));
                Assert.That(merged[1].curve.Evaluate(0.3f), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(walk);
            }
        }

        [Test]
        public void BuildCurve_HoldsSilenceUntilTheEndOfTheClip()
        {
            var walk = ScriptableObject.CreateInstance<FootstepType>();
            walk.weight = 1f;
            walk.name = "Walk";

            try
            {
                var steps = new List<FootstepMarker>
                {
                    new FootstepMarker { start = 0.2f, end = 0.5f, type = walk, weight = walk.weight }
                };

                AnimationCurve curve = FootstepCurveIO.BuildCurve(60f, 2f, steps);

                Assert.That(curve.keys[curve.length - 1].time, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(curve.keys[curve.length - 1].value, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(curve.Evaluate(0.75f), Is.EqualTo(0f).Within(0.0001f));

                List<FootstepMarker> loaded = FootstepCurveIO.ReadCurve(curve, 2f, 60f, new[] { walk });
                Assert.That(loaded, Has.Count.EqualTo(1));
                Assert.That(loaded[0].start, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(loaded[0].end, Is.EqualTo(0.5f).Within(0.0001f));

                AnimationCurve empty = FootstepCurveIO.BuildCurve(60f, 2f, new List<FootstepMarker>());
                Assert.That(empty.keys[0].time, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(empty.keys[empty.length - 1].time, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(empty.Evaluate(1f), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(FootstepCurveIO.ReadCurve(empty, 2f, 60f, new[] { walk }), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(walk);
            }
        }

        [Test]
        public void BuildCurve_KeepsKeysInsideTheNormalizedClip()
        {
            var walk = ScriptableObject.CreateInstance<FootstepType>();
            walk.weight = 1f;
            walk.name = "Walk";

            try
            {
                var steps = new List<FootstepMarker>
                {
                    new FootstepMarker { start = 0f, end = 2f, type = walk, weight = walk.weight }
                };

                AnimationCurve curve = FootstepCurveIO.BuildCurve(24f, 2f, steps);

                Assert.That(curve.keys[curve.length - 1].time, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(curve.preWrapMode, Is.EqualTo(WrapMode.ClampForever));
                Assert.That(curve.postWrapMode, Is.EqualTo(WrapMode.ClampForever));

                List<FootstepMarker> loaded = FootstepCurveIO.ReadCurve(curve, 2f, 24f, new[] { walk });
                Assert.That(loaded, Has.Count.EqualTo(1));
                Assert.That(loaded[0].start, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(loaded[0].end, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(loaded[0].type, Is.SameAs(walk));
            }
            finally
            {
                Object.DestroyImmediate(walk);
            }
        }

        [Test]
        public void ReadCurve_LeavesLegacySecondKeysUntouched()
        {
            var walk = ScriptableObject.CreateInstance<FootstepType>();
            walk.weight = 1f;
            walk.name = "Walk";
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(2f, 0f));
            curve.preWrapMode = WrapMode.Loop;
            curve.postWrapMode = WrapMode.Loop;

            try
            {
                List<FootstepMarker> loaded = FootstepCurveIO.ReadCurve(curve, 2f, 24f, new[] { walk });
                Assert.That(loaded, Has.Count.EqualTo(1));
                Assert.That(loaded[0].start, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(loaded[0].end, Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(walk);
            }
        }
    }
}
