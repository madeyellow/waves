using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    public static class FootstepEditMath
    {
        public static float MinDuration(float frameRate)
        {
            return 1f / Mathf.Max(frameRate, 1f);
        }

        public static float ClipDuration(float firstFrame, float lastFrame, float frameRate)
        {
            float frames = lastFrame - firstFrame;
            if (frames <= 0f)
                return 0f;

            return frames / Mathf.Max(frameRate, 1f);
        }

        public static void GetLimits(
            IReadOnlyList<FootstepMarker> steps,
            FootstepMarker marker,
            float clipLength,
            out float left,
            out float right)
        {
            left = 0f;
            right = Mathf.Max(0f, clipLength);
            float start = marker.start;
            float end = marker.end;

            for (int i = 0; i < steps.Count; i++)
            {
                FootstepMarker other = steps[i];
                if (ReferenceEquals(other, marker))
                    continue;

                if (other.end <= start)
                    left = Mathf.Max(left, other.end);

                if (other.start >= end)
                    right = Mathf.Min(right, other.start);
            }
        }

        public static void GetGap(
            IReadOnlyList<FootstepMarker> steps,
            float time,
            float clipLength,
            out float gapStart,
            out float gapEnd)
        {
            gapStart = 0f;
            gapEnd = Mathf.Max(0f, clipLength);

            for (int i = 0; i < steps.Count; i++)
            {
                FootstepMarker step = steps[i];
                if (time >= step.start && time <= step.end)
                {
                    gapStart = time;
                    gapEnd = time;
                    return;
                }

                if (step.end <= time)
                    gapStart = Mathf.Max(gapStart, step.end);

                if (step.start >= time)
                    gapEnd = Mathf.Min(gapEnd, step.start);
            }
        }

        public static void Move(FootstepMarker marker, float left, float right, float delta)
        {
            float duration = marker.end - marker.start;
            float start = Mathf.Clamp(marker.start + delta, left, Mathf.Max(left, right - duration));
            marker.start = start;
            marker.end = start + duration;
        }

        public static void SetStart(FootstepMarker marker, float left, float minDuration, float time)
        {
            marker.start = Mathf.Clamp(time, left, marker.end - minDuration);
        }

        public static void SetEnd(FootstepMarker marker, float right, float minDuration, float time)
        {
            marker.end = Mathf.Clamp(time, marker.start + minDuration, right);
        }

        public static float SnapToFrame(float time, float frameRate, float min, float max)
        {
            float fps = Mathf.Max(frameRate, 1f);
            float frame = Mathf.Round(time * fps);
            float minFrame = Mathf.Ceil(min * fps - 0.0001f);
            float maxFrame = Mathf.Floor(max * fps + 0.0001f);
            if (maxFrame < minFrame)
                return Mathf.Clamp(time, min, max);

            frame = Mathf.Clamp(frame, minFrame, maxFrame);
            return frame / fps;
        }

        public static float DuplicateStart(float end, float frameRate, bool snap)
        {
            if (!snap)
                return end;

            float fps = Mathf.Max(frameRate, 1f);
            float frames = end * fps;
            bool onFrame = Mathf.Abs(frames - Mathf.Round(frames)) < 0.001f;
            float next = onFrame ? Mathf.Round(frames) + 1f : Mathf.Ceil(frames);
            return next / fps;
        }
    }
}
