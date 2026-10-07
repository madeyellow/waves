using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>Shared paint for the WAVES browser. Dark and light skins use the same shapes.</summary>
    static class WAVESChrome
    {
        const int PlateSize = 32;
        const int PlateRadius = 10;
        const int MarkSize = 24;

        static Texture2D _fillTexture;
        static Texture2D _strokeTexture;
        static Texture2D _circleTexture;
        static Texture2D _ringTexture;
        static GUIStyle _fill;
        static GUIStyle _stroke;
        static GUIStyle _inkLabel;
        static bool _pro = true;

        public static Color Canvas => Pro
            ? new Color(0.220f, 0.220f, 0.220f, 1f)
            : new Color(0.784f, 0.784f, 0.784f, 1f);

        public static Color Strip => Pro
            ? new Color(0.192f, 0.192f, 0.192f, 1f)
            : new Color(0.710f, 0.710f, 0.710f, 1f);

        public static Color Plate => Pro
            ? new Color(0.243f, 0.243f, 0.243f, 1f)
            : new Color(0.860f, 0.860f, 0.860f, 1f);

        public static Color PlateRaised => Pro
            ? new Color(0.290f, 0.290f, 0.290f, 1f)
            : new Color(0.940f, 0.940f, 0.940f, 1f);

        public static Color Line => Pro
            ? new Color(0f, 0f, 0f, 0.55f)
            : new Color(0f, 0f, 0f, 0.22f);

        public static Color Gold => Pro
            ? new Color(0.86f, 0.67f, 0.32f, 1f)
            : new Color(0.70f, 0.48f, 0.10f, 1f);

        public static Color Ink => Pro
            ? new Color(0.824f, 0.824f, 0.824f, 1f)
            : new Color(0.13f, 0.13f, 0.13f, 1f);

        public static Color Muted => Pro
            ? new Color(0.58f, 0.58f, 0.58f, 1f)
            : new Color(0.32f, 0.32f, 0.32f, 1f);

        public static Color Danger => Pro
            ? new Color(0.88f, 0.40f, 0.36f, 1f)
            : new Color(0.72f, 0.26f, 0.22f, 1f);

        public static Color Knob => Pro
            ? new Color(0.97f, 0.95f, 0.90f, 1f)
            : Color.white;

        static bool Pro => EditorGUIUtility.isProSkin;

        public static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width < 2f || rect.height < 2f)
                return;

            Ensure();
            Color previous = GUI.color;
            GUI.color = color;
            _fill.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = previous;
        }

        public static void Stroke(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width < 2f || rect.height < 2f)
                return;

            Ensure();
            Color previous = GUI.color;
            GUI.color = color;
            _stroke.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = previous;
        }

        public static void Circle(Rect rect, Color color)
        {
            Blit(_circleTexture, rect, color);
        }

        public static void Ring(Rect rect, Color color)
        {
            Blit(_ringTexture, rect, color);
        }

        /// <summary>Square outline control, the same treatment as the matrix plus buttons.</summary>
        public static bool FlatButton(Rect rect, string text)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            bool hover = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (hover)
                {
                    Color wash = Pro
                        ? new Color(0.72f, 0.72f, 0.72f, 0.12f)
                        : new Color(1f, 1f, 1f, 0.12f);
                    EditorGUI.DrawRect(rect, wash);
                }

                Color line = hover ? Ink : Line;
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), line);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), line);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);
                GUI.Label(rect, text, InkLabel());
            }

            return GUI.Button(rect, new GUIContent(string.Empty, text), GUIStyle.none);
        }

        public static GUIStyle InkLabel()
        {
            EnsureLabels();
            return _inkLabel;
        }

        static void Blit(Texture2D texture, Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width < 1f || rect.height < 1f)
                return;

            Ensure();
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        static void Ensure()
        {
            if (_fill != null)
                return;

            _fillTexture = Rounded(PlateSize, PlateRadius, 0f);
            _strokeTexture = Rounded(PlateSize, PlateRadius, 1.6f);
            _circleTexture = Disc(MarkSize, 0f);
            _ringTexture = Disc(MarkSize, 1.8f);
            _fill = Sliced(_fillTexture);
            _stroke = Sliced(_strokeTexture);
        }

        static void EnsureLabels()
        {
            bool pro = Pro;
            if (_inkLabel != null && _pro == pro)
                return;

            _pro = pro;
            _inkLabel = Centered(Ink);
        }

        static GUIStyle Centered(Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            return style;
        }

        static GUIStyle Sliced(Texture2D texture)
        {
            var style = new GUIStyle
            {
                border = new RectOffset(PlateRadius, PlateRadius, PlateRadius, PlateRadius)
            };
            style.normal.background = texture;
            return style;
        }

        static Texture2D Rounded(int size, float radius, float thickness)
        {
            var texture = NewTexture(size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Distance(x + 0.5f, y + 0.5f, size, size, radius);
                    float outer = radius - dist;
                    float alpha = thickness <= 0f
                        ? Mathf.Clamp01(outer + 0.5f)
                        : Mathf.Clamp01(outer + 0.5f) * Mathf.Clamp01(dist - (radius - thickness) + 0.5f);
                    if (alpha <= 0f)
                        continue;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static Texture2D Disc(int size, float thickness)
        {
            var texture = NewTexture(size);
            var pixels = new Color[size * size];
            float radius = size * 0.5f - 0.75f;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float outer = radius - dist;
                    float alpha = thickness <= 0f
                        ? Mathf.Clamp01(outer + 0.5f)
                        : Mathf.Clamp01(outer + 0.5f) * Mathf.Clamp01(dist - (radius - thickness) + 0.5f);
                    if (alpha <= 0f)
                        continue;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        static float Distance(float x, float y, float width, float height, float radius)
        {
            float cx = Mathf.Clamp(x, radius, width - radius);
            float cy = Mathf.Clamp(y, radius, height - radius);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        }
    }
}
