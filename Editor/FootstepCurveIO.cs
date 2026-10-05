using System.Collections.Generic;
using MadeYellow.WAVES.Footsteps;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    public static class FootstepCurveIO
    {
        public const float SilentEpsilon = 0.0001f;

        public static List<FootstepMarker> Read(
            ModelImporter importer,
            string clipName,
            string trackName,
            float clipLength,
            float frameRate,
            IReadOnlyList<FootstepType> types)
        {
            return ReadCurve(FindCurve(importer, clipName, trackName), clipLength, frameRate, types);
        }

        public static List<FootstepMarker> ReadCurve(
            AnimationCurve curve,
            float clipLength,
            float frameRate,
            IReadOnlyList<FootstepType> types)
        {
            var steps = new List<FootstepMarker>();
            if (curve == null || curve.length == 0)
                return steps;

            Keyframe[] keys = curve.keys;
            float scale = UsesNormalizedTime(curve) && clipLength > 0f ? clipLength : 1f;
            bool inside = false;
            float start = 0f;
            float weight = 0f;

            // The last key repeats the first value so Loop Pose does not ramp the
            // curve. It is the loop point, not a second step.
            int keyCount = keys.Length;
            if (keyCount >= 3
                && Mathf.Abs(keys[keyCount - 1].value - keys[0].value) <= SilentEpsilon
                && Mathf.Abs(keys[keyCount - 2].value - keys[keyCount - 1].value) > SilentEpsilon)
                keyCount--;

            for (int i = 0; i < keyCount; i++)
            {
                float time = keys[i].time * scale;
                float value = keys[i].value;
                bool active = Mathf.Abs(value) > SilentEpsilon;

                if (!inside && active)
                {
                    inside = true;
                    start = time;
                    weight = value;
                    continue;
                }

                if (!inside)
                    continue;

                bool weightChanged = active && Mathf.Abs(value - weight) > SilentEpsilon;
                if (!active || weightChanged)
                {
                    AddStep(steps, start, time, weight, types);
                    inside = false;
                }

                if (weightChanged)
                {
                    inside = true;
                    start = time;
                    weight = value;
                }
            }

            if (inside)
            {
                float end = Mathf.Max(keys[keys.Length - 1].time * scale, clipLength);
                if (end <= start)
                    end = start + FootstepEditMath.MinDuration(frameRate);
                AddStep(steps, start, end, weight, types);
            }

            return steps;
        }

        public static float ConfiguredLength(
            ModelImporter importer,
            string clipName,
            float frameRate,
            float fallbackLength)
        {
            ModelImporterClipAnimation clip = FindClip(importer, clipName);
            if (clip == null)
                return Mathf.Max(0f, fallbackLength);

            float duration = FootstepEditMath.ClipDuration(clip.firstFrame, clip.lastFrame, frameRate);
            return duration > 0f ? duration : Mathf.Max(0f, fallbackLength);
        }

        public static bool TryBake(
            ModelImporter importer,
            string clipName,
            IReadOnlyList<FootstepTrackState> tracks,
            float clipLength,
            float frameRate)
        {
            if (importer == null || string.IsNullOrEmpty(clipName))
                return false;

            ModelImporterClipAnimation[] clips = EditableClips(importer);
            int index = IndexOfClip(clips, clipName);
            if (index < 0)
                return false;

            clips[index].curves = Merge(
                clips[index].curves,
                tracks,
                clipLength,
                frameRate,
                clips[index].loopPose);
            importer.clipAnimations = clips;
            return true;
        }

        public static ClipAnimationInfoCurve[] Merge(
            ClipAnimationInfoCurve[] existing,
            IReadOnlyList<FootstepTrackState> tracks,
            float clipLength,
            float frameRate,
            bool loopPose = false)
        {
            var owned = new Dictionary<string, FootstepTrackState>();
            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    FootstepTrackState track = tracks[i];
                    if (track == null)
                        continue;

                    string curveName = track.BakedCurveName;
                    if (string.IsNullOrEmpty(curveName))
                        continue;

                    owned[curveName] = track;
                }
            }

            var merged = new List<ClipAnimationInfoCurve>();
            var written = new HashSet<string>();
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    ClipAnimationInfoCurve curve = existing[i];
                    if (string.IsNullOrEmpty(curve.name) || !owned.ContainsKey(curve.name))
                    {
                        merged.Add(curve);
                        continue;
                    }

                    merged.Add(BuildInfo(curve.name, owned[curve.name], clipLength, frameRate, loopPose));
                    written.Add(curve.name);
                }
            }

            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    FootstepTrackState track = tracks[i];
                    if (track == null)
                        continue;

                    string curveName = track.BakedCurveName;
                    if (string.IsNullOrEmpty(curveName) || written.Contains(curveName) || owned[curveName] != track)
                        continue;

                    merged.Add(BuildInfo(curveName, track, clipLength, frameRate, loopPose));
                    written.Add(curveName);
                }
            }

            return merged.ToArray();
        }

        public static AnimationCurve BuildCurve(
            float frameRate,
            float clipLength,
            IReadOnlyList<FootstepMarker> steps,
            bool loopPose = false)
        {
            float epsilon = FootstepEditMath.MinDuration(frameRate);
            var ordered = new List<FootstepMarker>();
            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    FootstepMarker step = steps[i];
                    if (step != null && step.end > step.start)
                        ordered.Add(step);
                }
            }

            ordered.Sort((a, b) => a.start.CompareTo(b.start));

            var keys = new List<Keyframe>();
            float coveredUntil = 0f;

            for (int i = 0; i < ordered.Count; i++)
            {
                FootstepMarker step = ordered[i];
                float start = Mathf.Max(0f, step.start);
                float end = Mathf.Max(start + epsilon, step.end);
                float weight = step.type != null ? step.type.weight : step.weight;

                if (keys.Count > 0 && start > coveredUntil + epsilon * 0.5f)
                {
                    if (Mathf.Abs(keys[keys.Count - 1].value) > SilentEpsilon)
                        AddKey(keys, coveredUntil, 0f, epsilon);
                }
                else if (keys.Count > 0 && Mathf.Abs(keys[keys.Count - 1].value) <= SilentEpsilon)
                {
                    keys.RemoveAt(keys.Count - 1);
                }

                AddKey(keys, start, weight, epsilon);
                AddKey(keys, end, 0f, epsilon);
                coveredUntil = keys[keys.Count - 1].time;
            }

            if (keys.Count == 0)
                AddKey(keys, 0f, 0f, epsilon);

            // The importer maps the first key to the start of the clip and the
            // last key to the end. Silence before the first step and after the
            // last one still needs keys at 0 and at the clip length, or the
            // step is stretched across the whole clip.
            if (keys[0].time > 0f)
                keys.Insert(0, new Keyframe(0f, 0f, 0f, 0f));
            if (clipLength > keys[keys.Count - 1].time)
                AddKey(keys, clipLength, 0f, epsilon);

            // Importer curves are normalized: 0 is the first frame and 1 is the
            // last frame from the clip settings. A key written in seconds (for a
            // 2s clip, time 2) is treated as twice the clip and doubles its length.
            bool normalize = clipLength > 0f;
            if (normalize)
                FitAndNormalize(keys, clipLength);

            // Loop Pose spreads the gap between the first and last values across
            // the clip. A step on the first frame makes that gap the whole step
            // weight, so the animator never holds the plateau and never returns
            // to silence. The last key is the same instant as time 0 on a loop.
            // Copying the first value there keeps the segment before it and
            // removes the gap. Clips without Loop Pose keep a silent end so a
            // one-shot does not hold the step after it finishes.
            if (loopPose && keys.Count >= 2)
            {
                Keyframe end = keys[keys.Count - 1];
                if (Mathf.Abs(end.value - keys[0].value) > SilentEpsilon)
                {
                    end.value = keys[0].value;
                    keys[keys.Count - 1] = end;
                }
            }

            var curve = new AnimationCurve(keys.ToArray());
            if (normalize)
            {
                curve.preWrapMode = WrapMode.ClampForever;
                curve.postWrapMode = WrapMode.ClampForever;
            }
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyBroken(curve, i, true);
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }

            return curve;
        }

        public static FootstepType MatchType(float weight, IReadOnlyList<FootstepType> types)
        {
            FootstepType best = null;
            float bestDistance = float.MaxValue;
            if (types == null)
                return null;

            for (int i = 0; i < types.Count; i++)
            {
                FootstepType type = types[i];
                if (type == null)
                    continue;

                float distance = Mathf.Abs(type.weight - weight);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = type;
                }
            }

            return best;
        }

        static ClipAnimationInfoCurve BuildInfo(
            string curveName,
            FootstepTrackState track,
            float clipLength,
            float frameRate,
            bool loopPose)
        {
            return new ClipAnimationInfoCurve
            {
                name = curveName,
                curve = BuildCurve(frameRate, clipLength, track != null ? track.steps : null, loopPose)
            };
        }

        static AnimationCurve FindCurve(ModelImporter importer, string clipName, string trackName)
        {
            ModelImporterClipAnimation clip = FindClip(importer, clipName);
            if (clip == null || clip.curves == null || string.IsNullOrEmpty(trackName))
                return null;

            for (int i = 0; i < clip.curves.Length; i++)
            {
                ClipAnimationInfoCurve curve = clip.curves[i];
                if (curve.name == trackName)
                    return curve.curve;
            }

            return null;
        }

        static ModelImporterClipAnimation FindClip(ModelImporter importer, string clipName)
        {
            if (importer == null || string.IsNullOrEmpty(clipName))
                return null;

            ModelImporterClipAnimation[] defined = importer.clipAnimations;
            ModelImporterClipAnimation[] clips = defined != null && defined.Length > 0
                ? defined
                : importer.defaultClipAnimations;
            int index = IndexOfClip(clips, clipName);
            return index >= 0 ? clips[index] : null;
        }

        static ModelImporterClipAnimation[] EditableClips(ModelImporter importer)
        {
            ModelImporterClipAnimation[] defined = importer.clipAnimations;
            ModelImporterClipAnimation[] source = defined != null && defined.Length > 0
                ? defined
                : importer.defaultClipAnimations;
            if (source == null || source.Length == 0)
                return System.Array.Empty<ModelImporterClipAnimation>();

            var copy = new ModelImporterClipAnimation[source.Length];
            System.Array.Copy(source, copy, source.Length);
            return copy;
        }

        static int IndexOfClip(ModelImporterClipAnimation[] clips, string clipName)
        {
            if (clips == null || string.IsNullOrEmpty(clipName))
                return -1;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == clipName)
                    return i;
            }

            return -1;
        }

        static void AddStep(
            List<FootstepMarker> steps,
            float start,
            float end,
            float weight,
            IReadOnlyList<FootstepType> types)
        {
            if (end <= start)
                return;

            steps.Add(new FootstepMarker
            {
                start = start,
                end = end,
                weight = weight,
                type = MatchType(weight, types)
            });
        }

        static bool UsesNormalizedTime(AnimationCurve curve)
        {
            return curve != null
                && curve.preWrapMode == WrapMode.ClampForever
                && curve.postWrapMode == WrapMode.ClampForever;
        }

        static void FitAndNormalize(List<Keyframe> keys, float clipLength)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].time <= clipLength)
                    continue;

                Keyframe key = keys[i];
                key.time = clipLength;
                keys[i] = key;
            }

            for (int i = keys.Count - 1; i > 0; i--)
            {
                if (keys[i].time <= keys[i - 1].time)
                    keys.RemoveAt(i);
            }

            float inverse = 1f / clipLength;
            for (int i = 0; i < keys.Count; i++)
            {
                Keyframe key = keys[i];
                key.time *= inverse;
                keys[i] = key;
            }
        }

        static void AddKey(List<Keyframe> keys, float time, float value, float epsilon)
        {
            if (keys.Count > 0 && time <= keys[keys.Count - 1].time)
                time = keys[keys.Count - 1].time + epsilon * 0.25f;

            keys.Add(new Keyframe(time, value, 0f, 0f));
        }
    }
}
