using System.Collections.Generic;
using MadeYellow.WAVES;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    /// <summary>WAVES colors, with overrides stored in Unity Preferences.</summary>
    public static class WAVESPalettePreferences
    {
        const string Prefix = "MadeYellow.WAVES.Palette.";

        public readonly struct Entry
        {
            public readonly string Group;
            public readonly string Key;
            public readonly string Label;
            public readonly bool ShowAlpha;
            public readonly Color Default;

            public Entry(string group, string key, string label, bool showAlpha, Color fallback)
            {
                Group = group;
                Key = key;
                Label = label;
                ShowAlpha = showAlpha;
                Default = fallback;
            }
        }

        static int _revision;
        static Entry[] _entries;

        public static int Revision => _revision;

        public static Entry[] Entries => _entries ??= BuildEntries();

        public static Color Track0 => Get("Track0", WAVESPalette.Track0);
        public static Color Track1 => Get("Track1", WAVESPalette.Track1);
        public static Color Track2 => Get("Track2", WAVESPalette.Track2);
        public static Color Track3 => Get("Track3", WAVESPalette.Track3);
        public static Color Track4 => Get("Track4", WAVESPalette.Track4);
        public static Color Track5 => Get("Track5", WAVESPalette.Track5);
        public static Color Playhead => Get("Playhead", WAVESPalette.Playhead);
        public static Color StepBorder => Get("StepBorder", WAVESPalette.StepBorder);
        public static Color GridLine => Get("GridLine", WAVESPalette.GridLine);
        public static float LaneAlpha => GetFloat("LaneAlpha", WAVESPalette.LaneAlpha);
        public static float LaneSelectedAlpha => GetFloat("LaneSelectedAlpha", WAVESPalette.LaneSelectedAlpha);
        public static float NameAlpha => GetFloat("NameAlpha", WAVESPalette.NameAlpha);
        public static float NameSelectedAlpha => GetFloat("NameSelectedAlpha", WAVESPalette.NameSelectedAlpha);

        public static Color Title => Get("Title", WAVESPalette.Title);
        public static Color Subtitle => Get("Subtitle", WAVESPalette.Subtitle);
        public static Color Ink => Get("Ink", WAVESPalette.Ink);
        public static Color InkStrong => Get("InkStrong", WAVESPalette.InkStrong);
        public static Color Connected => Get("Connected", WAVESPalette.Connected);
        public static Color Missing => Get("Missing", WAVESPalette.Missing);
        public static Color Neutral => Get("Neutral", WAVESPalette.Neutral);
        public static Color Actor => Get("Actor", WAVESPalette.Actor);
        public static Color FallbackSurface => Get("FallbackSurface", WAVESPalette.FallbackSurface);

        public static Color Gradient0 => Get("Gradient0", WAVESPalette.Gradient0);
        public static Color Gradient1 => Get("Gradient1", WAVESPalette.Gradient1);
        public static Color Gradient2 => Get("Gradient2", WAVESPalette.Gradient2);
        public static Color Gradient3 => Get("Gradient3", WAVESPalette.Gradient3);
        public static Color Gradient4 => Get("Gradient4", WAVESPalette.Gradient4);
        public static Color Gradient5 => Get("Gradient5", WAVESPalette.Gradient5);

        public static Color RowLeft => Get("RowLeft", WAVESPalette.RowLeft);
        public static Color RowRight => Get("RowRight", WAVESPalette.RowRight);
        public static Color RowHover => EditorGUIUtility.isProSkin
            ? Get("RowHoverPro", WAVESPalette.RowHoverPro)
            : Get("RowHoverPersonal", WAVESPalette.RowHoverPersonal);
        public static Color RackPreset => Get("RackPreset", WAVESPalette.RackPreset);
        public static Color RemoveIdle => Get("RemoveIdle", WAVESPalette.RemoveIdle);

        public static Color BrandLeft => Get("BrandLeft", WAVESPalette.BrandLeft);
        public static Color BrandRight => Get("BrandRight", WAVESPalette.BrandRight);

        public static Color SectionHeader => EditorGUIUtility.isProSkin
            ? Get("SectionHeaderPro", WAVESPalette.SectionHeaderPro)
            : Get("SectionHeaderPersonal", WAVESPalette.SectionHeaderPersonal);
        public static Color SectionContent => EditorGUIUtility.isProSkin
            ? Get("SectionContentPro", WAVESPalette.SectionContentPro)
            : Get("SectionContentPersonal", WAVESPalette.SectionContentPersonal);
        public static Color StepHeader => EditorGUIUtility.isProSkin
            ? Get("StepHeaderPro", WAVESPalette.StepHeaderPro)
            : Get("StepHeaderPersonal", WAVESPalette.StepHeaderPersonal);
        public static Color StepContent => EditorGUIUtility.isProSkin
            ? Get("StepContentPro", WAVESPalette.StepContentPro)
            : Get("StepContentPersonal", WAVESPalette.StepContentPersonal);
        public static Color EffectFill => EditorGUIUtility.isProSkin
            ? Get("EffectFillPro", WAVESPalette.EffectFillPro)
            : Get("EffectFillPersonal", WAVESPalette.EffectFillPersonal);
        public static Color EffectLine => EditorGUIUtility.isProSkin
            ? Get("EffectLinePro", WAVESPalette.EffectLinePro)
            : Get("EffectLinePersonal", WAVESPalette.EffectLinePersonal);
        public static Color DropLabel => EditorGUIUtility.isProSkin
            ? Get("DropLabelPro", WAVESPalette.DropLabelPro)
            : Get("DropLabelPersonal", WAVESPalette.DropLabelPersonal);
        public static Color OutlineText => EditorGUIUtility.isProSkin
            ? Get("OutlineTextPro", WAVESPalette.OutlineTextPro)
            : Get("OutlineTextPersonal", WAVESPalette.OutlineTextPersonal);

        public static Color TrackSwatch(int index)
        {
            int wrapped = index % 6;
            if (wrapped < 0)
                wrapped += 6;

            switch (wrapped)
            {
                case 0: return Track0;
                case 1: return Track1;
                case 2: return Track2;
                case 3: return Track3;
                case 4: return Track4;
                default: return Track5;
            }
        }

        /// <summary>First track swatch that is not already in <paramref name="used"/>. Repeats the cycle when all six are taken.</summary>
        public static Color NextFreeSwatch(IReadOnlyList<Color> used)
        {
            int count = used != null ? used.Count : 0;
            for (int index = 0; index < 6; index++)
            {
                Color candidate = TrackSwatch(index);
                if (!SwatchUsed(used, candidate))
                    return candidate;
            }

            return TrackSwatch(count);
        }

        static bool SwatchUsed(IReadOnlyList<Color> used, Color candidate)
        {
            if (used == null)
                return false;

            for (int i = 0; i < used.Count; i++)
            {
                Color color = used[i];
                if (Mathf.Abs(color.r - candidate.r) < 0.04f
                    && Mathf.Abs(color.g - candidate.g) < 0.04f
                    && Mathf.Abs(color.b - candidate.b) < 0.04f)
                    return true;
            }

            return false;
        }

        public static Color GradientStop(int index)
        {
            switch (index)
            {
                case 0: return Gradient0;
                case 1: return Gradient1;
                case 2: return Gradient2;
                case 3: return Gradient3;
                case 4: return Gradient4;
                default: return Gradient5;
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/WAVES", SettingsScope.User)
            {
                label = "WAVES",
                guiHandler = _ => DrawPreferences()
            };
        }

        static void DrawPreferences()
        {
            EditorGUILayout.LabelField(
                "Colors used by WAVES editors. Saved assets keep the color stored on the asset.",
                EditorStyles.wordWrappedMiniLabel);
            string group = null;
            Entry[] entries = Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry.Group != group)
                {
                    group = entry.Group;
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                }

                if (entry.Key == "LaneAlpha" || entry.Key == "LaneSelectedAlpha" || entry.Key == "NameAlpha" || entry.Key == "NameSelectedAlpha")
                {
                    float current = GetFloat(entry.Key, entry.Default.r);
                    float next = EditorGUILayout.Slider(entry.Label, current, 0f, 1f);
                    if (!Mathf.Approximately(next, current))
                        SetFloat(entry.Key, next, entry.Default.r);
                    continue;
                }

                Color currentColor = Get(entry.Key, entry.Default);
                Color picked = EditorGUILayout.ColorField(
                    new GUIContent(entry.Label),
                    currentColor,
                    true,
                    entry.ShowAlpha,
                    false);
                if (picked != currentColor)
                    Set(entry.Key, picked, entry.Default);
            }

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Reset WAVES Colors", GUILayout.Width(180f)))
                Reset();
        }

        public static void Reset()
        {
            Entry[] entries = Entries;
            for (int i = 0; i < entries.Length; i++)
                EditorPrefs.DeleteKey(Prefix + entries[i].Key);
            _revision++;
        }

        static Entry[] BuildEntries()
        {
            return new[]
            {
                Alpha("Tracks", "LaneAlpha", "Lane background alpha", WAVESPalette.LaneAlpha),
                Alpha("Tracks", "LaneSelectedAlpha", "Selected lane alpha", WAVESPalette.LaneSelectedAlpha),
                Alpha("Tracks", "NameAlpha", "Track name alpha", WAVESPalette.NameAlpha),
                Alpha("Tracks", "NameSelectedAlpha", "Selected track name alpha", WAVESPalette.NameSelectedAlpha),
                ColorEntry("Tracks", "Track0", "Track swatch 1", false, WAVESPalette.Track0),
                ColorEntry("Tracks", "Track1", "Track swatch 2", false, WAVESPalette.Track1),
                ColorEntry("Tracks", "Track2", "Track swatch 3", false, WAVESPalette.Track2),
                ColorEntry("Tracks", "Track3", "Track swatch 4", false, WAVESPalette.Track3),
                ColorEntry("Tracks", "Track4", "Track swatch 5", false, WAVESPalette.Track4),
                ColorEntry("Tracks", "Track5", "Track swatch 6", false, WAVESPalette.Track5),
                ColorEntry("Tracks", "Playhead", "Playhead", false, WAVESPalette.Playhead),
                ColorEntry("Tracks", "StepBorder", "Selected step border", true, WAVESPalette.StepBorder),
                ColorEntry("Tracks", "GridLine", "Frame grid", true, WAVESPalette.GridLine),
                ColorEntry("Identity", "Title", "Title", false, WAVESPalette.Title),
                ColorEntry("Identity", "Subtitle", "Subtitle", true, WAVESPalette.Subtitle),
                ColorEntry("Identity", "Ink", "Ink on light", false, WAVESPalette.Ink),
                ColorEntry("Identity", "InkStrong", "Strong ink", false, WAVESPalette.InkStrong),
                ColorEntry("Identity", "Connected", "Connected", false, WAVESPalette.Connected),
                ColorEntry("Identity", "Missing", "Missing", false, WAVESPalette.Missing),
                ColorEntry("Identity", "Neutral", "Neutral", false, WAVESPalette.Neutral),
                ColorEntry("Identity", "Actor", "Default actor", false, WAVESPalette.Actor),
                ColorEntry("Identity", "FallbackSurface", "Fallback surface", false, WAVESPalette.FallbackSurface),
                ColorEntry("Header", "Gradient0", "Gradient 1", false, WAVESPalette.Gradient0),
                ColorEntry("Header", "Gradient1", "Gradient 2", false, WAVESPalette.Gradient1),
                ColorEntry("Header", "Gradient2", "Gradient 3", false, WAVESPalette.Gradient2),
                ColorEntry("Header", "Gradient3", "Gradient 4", false, WAVESPalette.Gradient3),
                ColorEntry("Header", "Gradient4", "Gradient 5", false, WAVESPalette.Gradient4),
                ColorEntry("Header", "Gradient5", "Gradient 6", false, WAVESPalette.Gradient5),
                ColorEntry("Header", "BrandLeft", "Brand left", false, WAVESPalette.BrandLeft),
                ColorEntry("Header", "BrandRight", "Brand right", false, WAVESPalette.BrandRight),
                ColorEntry("Modules", "RowLeft", "Module row left", false, WAVESPalette.RowLeft),
                ColorEntry("Modules", "RowRight", "Module row right", false, WAVESPalette.RowRight),
                ColorEntry("Modules", "RowHoverPro", "Module hover (dark)", true, WAVESPalette.RowHoverPro),
                ColorEntry("Modules", "RowHoverPersonal", "Module hover (light)", true, WAVESPalette.RowHoverPersonal),
                ColorEntry("Modules", "RackPreset", "Module preset label", true, WAVESPalette.RackPreset),
                ColorEntry("Modules", "RemoveIdle", "Remove icon", true, WAVESPalette.RemoveIdle),
                ColorEntry("Modules", "EffectFillPro", "Effect fill (dark)", true, WAVESPalette.EffectFillPro),
                ColorEntry("Modules", "EffectFillPersonal", "Effect fill (light)", true, WAVESPalette.EffectFillPersonal),
                ColorEntry("Modules", "EffectLinePro", "Effect line (dark)", true, WAVESPalette.EffectLinePro),
                ColorEntry("Modules", "EffectLinePersonal", "Effect line (light)", true, WAVESPalette.EffectLinePersonal),
                ColorEntry("Modules", "DropLabelPro", "Drop label (dark)", true, WAVESPalette.DropLabelPro),
                ColorEntry("Modules", "DropLabelPersonal", "Drop label (light)", true, WAVESPalette.DropLabelPersonal),
                ColorEntry("Modules", "OutlineTextPro", "Outline text (dark)", true, WAVESPalette.OutlineTextPro),
                ColorEntry("Modules", "OutlineTextPersonal", "Outline text (light)", true, WAVESPalette.OutlineTextPersonal),
                ColorEntry("Panels", "SectionHeaderPro", "Section header (dark)", false, WAVESPalette.SectionHeaderPro),
                ColorEntry("Panels", "SectionHeaderPersonal", "Section header (light)", false, WAVESPalette.SectionHeaderPersonal),
                ColorEntry("Panels", "SectionContentPro", "Section content (dark)", false, WAVESPalette.SectionContentPro),
                ColorEntry("Panels", "SectionContentPersonal", "Section content (light)", false, WAVESPalette.SectionContentPersonal),
                ColorEntry("Panels", "StepHeaderPro", "Step header (dark)", false, WAVESPalette.StepHeaderPro),
                ColorEntry("Panels", "StepHeaderPersonal", "Step header (light)", false, WAVESPalette.StepHeaderPersonal),
                ColorEntry("Panels", "StepContentPro", "Step content (dark)", false, WAVESPalette.StepContentPro),
                ColorEntry("Panels", "StepContentPersonal", "Step content (light)", false, WAVESPalette.StepContentPersonal)
            };
        }

        static Entry ColorEntry(string group, string key, string label, bool showAlpha, Color fallback)
        {
            return new Entry(group, key, label, showAlpha, fallback);
        }

        static Entry Alpha(string group, string key, string label, float fallback)
        {
            return new Entry(group, key, label, false, new Color(fallback, 0f, 0f, 1f));
        }

        static Color Get(string key, Color fallback)
        {
            string stored = EditorPrefs.GetString(Prefix + key, string.Empty);
            if (string.IsNullOrEmpty(stored))
                return fallback;

            return ColorUtility.TryParseHtmlString("#" + stored, out Color color) ? color : fallback;
        }

        static float GetFloat(string key, float fallback)
        {
            return EditorPrefs.GetFloat(Prefix + key, fallback);
        }

        static void Set(string key, Color color, Color fallback)
        {
            if (Approximately(color, fallback))
                EditorPrefs.DeleteKey(Prefix + key);
            else
                EditorPrefs.SetString(Prefix + key, ColorUtility.ToHtmlStringRGBA(color));
            _revision++;
        }

        static void SetFloat(string key, float value, float fallback)
        {
            if (Mathf.Approximately(value, fallback))
                EditorPrefs.DeleteKey(Prefix + key);
            else
                EditorPrefs.SetFloat(Prefix + key, value);
            _revision++;
        }

        static bool Approximately(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) < 0.001f
                && Mathf.Abs(left.g - right.g) < 0.001f
                && Mathf.Abs(left.b - right.b) < 0.001f
                && Mathf.Abs(left.a - right.a) < 0.001f;
        }
    }
}
