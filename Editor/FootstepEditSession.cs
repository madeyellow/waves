using System;
using System.Collections.Generic;
using MadeYellow.WAVES;
using MadeYellow.WAVES.Footsteps;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    [Serializable]
    public sealed class FootstepMarker
    {
        public FootstepType type;
        public float start;
        public float end;
        public float weight = 1f;
    }

    [Serializable]
    public sealed class FootstepTrackState : IColorCodedElement
    {
        public string name;
        public string bakeIntoAnimationCurve = string.Empty;
        public Color color = Color.white;
        public List<FootstepMarker> steps = new List<FootstepMarker>();

        Color IColorCodedElement.Color => WAVESPalette.Opaque(color);

        public string BakedCurveName => FootstepTrackDefinition.ResolveCurveName(name, bakeIntoAnimationCurve);
    }

    public sealed class FootstepEditSession : ScriptableObject
    {
        public List<FootstepTrackState> tracks = new List<FootstepTrackState>();
        public bool hasUnbakedChanges;
    }
}
