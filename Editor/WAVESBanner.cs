using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>The WAVES header gradient shared by the inspector banner and module cards.</summary>
    static class WAVESBanner
    {
        static Texture2D _texture;
        static int _revision = -1;
        static bool _pro;

        public static Texture2D Texture()
        {
            int revision = WAVESPalettePreferences.Revision;
            bool pro = EditorGUIUtility.isProSkin;
            if (_texture != null && _revision == revision && _pro == pro)
                return _texture;

            _revision = revision;
            _pro = pro;
            if (_texture != null)
                Object.DestroyImmediate(_texture);

            const int width = 256;
            const int height = 64;
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f);
                    Color color = GradientAt(u);
                    float lift = 1f - Mathf.Abs(v - 0.42f) / 0.75f;
                    color = Color.Lerp(color, Color.white, Mathf.Clamp01(lift) * 0.1f);
                    float wave = Wave(u, v, 0.62f, 0.08f, 1.1f, 0.4f, 0.18f) * 0.42f;
                    wave += Wave(u, v, 0.4f, 0.06f, 0.75f, 1.8f, 0.14f) * 0.28f;
                    wave += Wave(u, v, 0.78f, 0.05f, 1.35f, 2.6f, 0.1f) * 0.2f;
                    pixels[y * width + x] = Color.Lerp(color, Color.white, Mathf.Clamp01(wave));
                }
            }

            _texture.SetPixels(pixels);
            _texture.Apply();
            return _texture;
        }

        static Color GradientAt(float t)
        {
            int last = 5;
            float scaled = Mathf.Clamp01(t) * last;
            int index = Mathf.Min((int)scaled, last - 1);
            float blend = scaled - index;
            blend = blend * blend * (3f - 2f * blend);
            return Color.Lerp(WAVESPalettePreferences.GradientStop(index), WAVESPalettePreferences.GradientStop(index + 1), blend);
        }

        static float Wave(float u, float v, float center, float amplitude, float frequency, float phase, float thickness)
        {
            float spine = center + amplitude * Mathf.Sin(u * Mathf.PI * 2f * frequency + phase);
            float distance = Mathf.Abs(v - spine) / thickness;
            if (distance >= 1f)
                return 0f;

            float edge = 1f - distance;
            return edge * edge;
        }
    }
}
