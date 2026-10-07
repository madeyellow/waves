using UnityEngine;

namespace MadeYellow.WAVES.AudioVisualEffects
{
    /// <summary>
    /// Starting playback values for every module.
    /// Unity creates serialized payload objects without field initializers, so these are applied again in code.
    /// </summary>
    public static class WAVESEffectDefaults
    {
        public const float Volume = 1f;
        public const float SpatialBlend = 1f;
        public const float ReverbZoneMix = 1f;
        public const float AudibleDistance = 100f;
        public const float MinDistance = 1f;
        public const float VisibleDistance = 100f;
        public const AudioRolloffMode Rolloff = AudioRolloffMode.Logarithmic;

        /// <summary>True when every playback field is still the serializer zero.</summary>
        public static bool IsUnsetPlayback(
            float volume,
            float spatialBlend,
            float reverbZoneMix,
            float audibleDistance,
            float minDistance,
            float dopplerLevel)
        {
            return Mathf.Approximately(volume, 0f)
                && Mathf.Approximately(spatialBlend, 0f)
                && Mathf.Approximately(reverbZoneMix, 0f)
                && Mathf.Approximately(audibleDistance, 0f)
                && Mathf.Approximately(minDistance, 0f)
                && Mathf.Approximately(dopplerLevel, 0f);
        }
    }
}
