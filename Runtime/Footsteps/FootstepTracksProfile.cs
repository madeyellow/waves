using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadeYellow.WAVES.Footsteps
{
    /// <summary>Name and color of one track stored in a <see cref="FootstepTracksProfile"/>.</summary>
    [Serializable]
    public sealed class FootstepTrackDefinition : IColorCodedElement
    {
        /// <summary>Track name shown in the editor. Also the baked curve name when <see cref="bakeIntoAnimationCurve"/> is empty.</summary>
        public string name = "Foot";

        /// <summary>Animation curve these steps are baked into. Empty uses <see cref="name"/>.</summary>
        public string bakeIntoAnimationCurve = string.Empty;

        /// <summary>Color used for this track in the editor and gizmos.</summary>
        public Color color = WAVESPalette.Track0;

        /// <summary>Opaque track color.</summary>
        Color IColorCodedElement.Color => WAVESPalette.Opaque(color);

        /// <summary>Curve name written into the clip and read from the animator.</summary>
        public string BakedCurveName => ResolveCurveName(name, bakeIntoAnimationCurve);

        /// <summary>Uses the bake override when it is set, otherwise the track name.</summary>
        public static string ResolveCurveName(string trackName, string bakeIntoAnimationCurve)
        {
            if (!string.IsNullOrWhiteSpace(bakeIntoAnimationCurve))
                return bakeIntoAnimationCurve.Trim();

            return trackName ?? string.Empty;
        }

        /// <summary>True when steps bake into a curve other than the track name.</summary>
        public static bool UsesCustomCurve(string trackName, string bakeIntoAnimationCurve)
        {
            if (string.IsNullOrWhiteSpace(bakeIntoAnimationCurve))
                return false;

            return bakeIntoAnimationCurve.Trim() != trackName;
        }
    }

    /// <summary>Set of footstep tracks for one kind of character.</summary>
    [CreateAssetMenu(
        fileName = "FootstepTracksProfile",
        menuName = "MadeYellow/WAVES/Footstep Tracks Profile")]
    public sealed class FootstepTracksProfile : ScriptableObject
    {
        /// <summary>Tracks baked into the animator and read by <see cref="FootstepReader"/>.</summary>
        public List<FootstepTrackDefinition> tracks = new List<FootstepTrackDefinition>();
    }
}
