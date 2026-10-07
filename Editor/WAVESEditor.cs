using MadeYellow.WAVES.AudioVisualEffects.Modules;
using UnityEditor;
using UnityEngine;

namespace MadeYellow.WAVES.Editor
{
    [CustomEditor(typeof(AudioVisualEffects.WAVES))]
    public sealed class WAVESEditor : UnityEditor.Editor
    {
        const string Title = "WAVES";
        const string Subtitle = "World Audio and Visual Effects System";
        const float HeaderHeight = 64f;
        const float HeaderSpacing = 8f;
        const float RackRowHeight = 48f;
        const float RackPad = 10f;
        const float RemoveSize = 16f;
        const float DropHeight = 40f;
        const float Dash = 5f;
        const float DashGap = 3f;
        const float LinkCircle = 8f;
        const float LinkGap = 28f;
        static int _paletteRevision = -1;
        static bool _palettePro;

        static Color TitleColor => WAVESPalettePreferences.Title;
        static Color SubtitleColor => WAVESPalettePreferences.Subtitle;
        static Color RowLeft => WAVESPalettePreferences.RowLeft;
        static Color RowRight => WAVESPalettePreferences.RowRight;
        static Color ConnectedColor => WAVESPalettePreferences.Connected;
        static Color MissingColor => WAVESPalettePreferences.Missing;

        static Texture2D _row;
        static Texture2D _circle;
        static Texture2D _removeIcon;
        static GUIStyle _title;
        static GUIStyle _subtitle;
        static GUIStyle _rackTitle;
        static GUIStyle _rackPreset;
        static GUIStyle _dropLabel;
        static GUIStyle _removeStyle;
        static GUIStyle _linkTitle;
        static GUIStyle _linkOn;
        static GUIStyle _linkOff;
        static GUIStyle _outlineLabel;
        static bool _outlinePro;
        static Texture2D _outline;

        public override void OnInspectorGUI()
        {
            SyncPalette();
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            DrawBanner();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "_modules", "_cacheSurfaces", "_surfaceCacheLifetime");
            DrawCache();
            serializedObject.ApplyModifiedProperties();
            var waves = (AudioVisualEffects.WAVES)target;
            DrawRack(waves);
            DrawLinks(waves);
        }

        static readonly GUIContent UseCachingLabel = new GUIContent(
            "Use Caching",
            "Caching remembers queries that determine surface types and returns a previously computed result. This greatly improves performance. Leave caching enabled, and keep the cache lifetime around 60 seconds or more.");

        void DrawCache()
        {
            SerializedProperty enabled = serializedObject.FindProperty("_cacheSurfaces");
            EditorGUILayout.PropertyField(enabled, UseCachingLabel);
            if (!enabled.boolValue)
                return;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_surfaceCacheLifetime"));
        }

        static void DrawHeader(string title)
        {
            float line = EditorGUIUtility.singleLineHeight;
            Rect position = EditorGUILayout.GetControlRect(false, line * 1.5f);
            position.yMin += line * 0.5f;
            position = EditorGUI.IndentedRect(position);
            GUI.Label(position, title, EditorStyles.boldLabel);
        }

        static void DrawBanner()
        {
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            Rect bar = EditorGUILayout.GetControlRect(false, HeaderHeight);
            EditorGUI.indentLevel = indent;

            GUI.DrawTexture(bar, HeaderTexture(), ScaleMode.StretchToFill, false);
            DrawTitles(bar);
            GUILayout.Space(HeaderSpacing);
        }

        static void DrawTitles(Rect bar)
        {
            const float side = 8f;
            float textWidth = Mathf.Max(0f, bar.width - side * 2f);
            GUIStyle title = TitleStyle();
            GUIStyle subtitle = SubtitleStyle();
            var titleContent = new GUIContent(Title);
            var subtitleContent = new GUIContent(Subtitle);
            float titleHeight = title.CalcHeight(titleContent, textWidth);
            float subtitleHeight = subtitle.CalcHeight(subtitleContent, textWidth);
            const float gap = 1f;
            float y = bar.y + (bar.height - (titleHeight + gap + subtitleHeight)) * 0.5f;
            GUI.Label(new Rect(bar.x + side, y, textWidth, titleHeight), titleContent, title);
            GUI.Label(new Rect(bar.x + side, y + titleHeight + gap, textWidth, subtitleHeight), subtitleContent, subtitle);
        }

        static void SyncPalette()
        {
            int revision = WAVESPalettePreferences.Revision;
            bool pro = EditorGUIUtility.isProSkin;
            if (_paletteRevision == revision && _palettePro == pro)
                return;

            _paletteRevision = revision;
            _palettePro = pro;
            if (_row != null)
                DestroyImmediate(_row);
            _row = null;
            _title = null;
            _subtitle = null;
            _rackTitle = null;
            _rackPreset = null;
            _dropLabel = null;
            _outlineLabel = null;
            _outline = null;
        }

        static Texture2D HeaderTexture()
        {
            return WAVESBanner.Texture();
        }

        static GUIStyle TitleStyle()
        {
            if (_title != null)
                return _title;

            _title = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            _title.normal.textColor = TitleColor;
            _title.hover.textColor = TitleColor;
            _title.active.textColor = TitleColor;
            _title.focused.textColor = TitleColor;
            return _title;
        }

        static GUIStyle SubtitleStyle()
        {
            if (_subtitle != null)
                return _subtitle;

            _subtitle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                wordWrap = false
            };
            _subtitle.normal.textColor = SubtitleColor;
            _subtitle.hover.textColor = SubtitleColor;
            _subtitle.active.textColor = SubtitleColor;
            _subtitle.focused.textColor = SubtitleColor;
            return _subtitle;
        }

        static void DrawRack(AudioVisualEffects.WAVES waves)
        {
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            DrawHeader("Modules");

            int count = waves.ModuleCount;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    GUILayout.Space(3f);

                if (!DrawModuleRow(waves, waves.GetModule(i)))
                    continue;

                Undo.RecordObject(waves, "Remove WAVES Module");
                waves.RemoveModuleAt(i);
                EditorUtility.SetDirty(waves);
                WAVESBrowserWindow.RepaintOpen();
                EditorGUI.indentLevel = indent;
                GUIUtility.ExitGUI();
            }

            if (count == 0)
            {
                GUILayout.Space(6f);
                DrawOpenBrowserButton(waves);
            }

            EditorGUI.indentLevel = indent;
        }

        static bool DrawModuleRow(AudioVisualEffects.WAVES waves, WAVESModuleBase module)
        {
            Rect row = EditorGUILayout.GetControlRect(false, RackRowHeight);
            bool over = row.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(row, RowTexture(), ScaleMode.StretchToFill, false);
                if (over)
                {
                    Color wash = WAVESPalettePreferences.RowHover;
                    EditorGUI.DrawRect(row, wash);
                }
            }

            EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);

            Rect remove = new Rect(
                row.xMax - RackPad - RemoveSize,
                row.y + (row.height - RemoveSize) * 0.5f,
                RemoveSize,
                RemoveSize);
            bool deleted = DrawRemoveButton(remove, "Remove module");

            string title = WAVESModuleCatalog.Title(module);
            string preset = module != null ? module.name : string.Empty;
            float textWidth = Mathf.Max(0f, remove.x - row.x - RackPad * 2f);
            GUIStyle titleStyle = RackTitleStyle();
            GUIStyle presetStyle = RackPresetStyle();
            var titleContent = new GUIContent(title);
            float titleHeight = titleStyle.CalcHeight(titleContent, textWidth);
            float presetHeight = 0f;
            var presetContent = GUIContent.none;
            if (!string.IsNullOrEmpty(preset))
            {
                presetContent = new GUIContent(preset);
                presetHeight = presetStyle.CalcHeight(presetContent, textWidth);
            }

            float gap = presetHeight > 0f ? 1f : 0f;
            float y = row.y + (row.height - (titleHeight + gap + presetHeight)) * 0.5f;
            GUI.Label(new Rect(row.x + RackPad, y, textWidth, titleHeight), titleContent, titleStyle);
            if (presetHeight > 0f)
            {
                GUI.Label(
                    new Rect(row.x + RackPad, y + titleHeight + gap, textWidth, presetHeight),
                    presetContent,
                    presetStyle);
            }

            if (module != null)
            {
                var hint = new Rect(row.x, row.y, Mathf.Max(0f, remove.x - row.x), row.height);
                GUI.Label(hint, new GUIContent(string.Empty, "Click to edit in WAVES Module Browser"));
            }

            Event click = Event.current;
            if (module != null &&
                click.type == EventType.MouseDown &&
                click.button == 0 &&
                row.Contains(click.mousePosition) &&
                !remove.Contains(click.mousePosition))
            {
                WAVESBrowserWindow.Open(waves, module);
                click.Use();
            }

            return deleted;
        }

        static void DrawOpenBrowserButton(AudioVisualEffects.WAVES waves)
        {
            Rect zone = EditorGUILayout.GetControlRect(false, DropHeight);
            Event evt = Event.current;
            bool over = zone.Contains(evt.mousePosition);
            if (evt.type == EventType.Repaint)
            {
                var inner = new Rect(zone.x + 2f, zone.y + 2f, zone.width - 4f, zone.height - 4f);
                float wash = over ? 0.07f : 0.035f;
                Color fill = EditorGUIUtility.isProSkin
                    ? WAVESPalette.White(wash)
                    : WAVESPalette.Black(wash);
                EditorGUI.DrawRect(inner, fill);
                Color line = EditorGUIUtility.isProSkin
                    ? WAVESPalette.White(over ? 0.9f : 0.38f)
                    : WAVESPalette.Black(over ? 0.7f : 0.32f);
                DrawDashedFrame(zone, line, 2f);
                GUI.Label(zone, "Click to open WAVES Module Browser", DropLabelStyle());
            }

            EditorGUIUtility.AddCursorRect(zone, MouseCursor.Link);
            if (evt.type == EventType.MouseDown && evt.button == 0 && over)
            {
                WAVESBrowserWindow.Open(waves, null);
                evt.Use();
            }
        }

        static void DrawLinks(AudioVisualEffects.WAVES waves)
        {
            bool audio = waves.GetComponent<AudioVisualEffects.Abstractions.IWAVESAudioFX>() != null;
            bool visual = waves.GetComponent<AudioVisualEffects.Abstractions.IWAVESVisualFX>() != null;
            if (!DrawLinkRow(audio, visual, out bool addAudio, out bool addVisual))
                return;

            if (addAudio)
                Undo.AddComponent<AudioVisualEffects.WAVESAudioFX>(waves.gameObject);
            if (addVisual)
                Undo.AddComponent<AudioVisualEffects.WAVESVisualFX>(waves.gameObject);
        }

        static bool DrawLinkRow(bool audio, bool visual, out bool addAudio, out bool addVisual)
        {
            addAudio = false;
            addVisual = false;
            GUIStyle title = LinkTitleStyle();
            GUIStyle audioStatus = audio ? LinkOnStyle() : LinkOffStyle();
            GUIStyle visualStatus = visual ? LinkOnStyle() : LinkOffStyle();
            GUIStyle addStyle = OutlineButtonStyle();
            var audioTitle = new GUIContent("Audio FX");
            var visualTitle = new GUIContent("Visual FX");
            var audioState = new GUIContent(audio ? "Connected" : "Not connected");
            var visualState = new GUIContent(visual ? "Connected" : "Not connected");
            var addLabel = new GUIContent("Click to Add");
            float addWidth = addStyle.CalcSize(addLabel).x;
            float audioText = Mathf.Max(title.CalcSize(audioTitle).x, audioStatus.CalcSize(audioState).x);
            float visualText = Mathf.Max(title.CalcSize(visualTitle).x, visualStatus.CalcSize(visualState).x);
            if (!audio)
                audioText = Mathf.Max(audioText, addWidth);
            if (!visual)
                visualText = Mathf.Max(visualText, addWidth);

            float audioWidth = LinkCircle + 8f + audioText;
            float visualWidth = LinkCircle + 8f + visualText;
            float titleHeight = title.CalcHeight(audioTitle, audioText);
            float statusHeight = audioStatus.CalcHeight(audioState, audioText);
            float textHeight = titleHeight + 1f + statusHeight;
            bool anyMissing = !audio || !visual;
            float buttonHeight = anyMissing ? addStyle.CalcHeight(addLabel, addWidth) : 0f;
            float height = textHeight + (anyMissing ? 4f + buttonHeight : 0f);

            EditorGUILayout.Space(10f);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            Rect row = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.indentLevel = indent;

            float total = audioWidth + LinkGap + visualWidth;
            float x = row.x + Mathf.Max(0f, (row.width - total) * 0.5f);
            addAudio = DrawLink(
                new Rect(x, row.y, audioWidth, height),
                audio,
                audioTitle,
                audioState,
                addLabel,
                title,
                audioStatus,
                addStyle,
                titleHeight,
                statusHeight,
                textHeight);
            addVisual = DrawLink(
                new Rect(x + audioWidth + LinkGap, row.y, visualWidth, height),
                visual,
                visualTitle,
                visualState,
                addLabel,
                title,
                visualStatus,
                addStyle,
                titleHeight,
                statusHeight,
                textHeight);
            return addAudio || addVisual;
        }

        static bool DrawLink(
            Rect group,
            bool connected,
            GUIContent title,
            GUIContent status,
            GUIContent addLabel,
            GUIStyle titleStyle,
            GUIStyle statusStyle,
            GUIStyle addStyle,
            float titleHeight,
            float statusHeight,
            float textHeight)
        {
            float circleY = group.y + (textHeight - LinkCircle) * 0.5f;
            var circle = new Rect(group.x, circleY, LinkCircle, LinkCircle);
            Color previous = GUI.color;
            GUI.color = connected ? ConnectedColor : MissingColor;
            GUI.DrawTexture(circle, CircleTexture(), ScaleMode.ScaleToFit, true);
            GUI.color = previous;

            float textX = circle.xMax + 8f;
            float textWidth = Mathf.Max(0f, group.xMax - textX);
            GUI.Label(new Rect(textX, group.y, textWidth, titleHeight), title, titleStyle);
            GUI.Label(new Rect(textX, group.y + titleHeight + 1f, textWidth, statusHeight), status, statusStyle);
            if (connected)
                return false;

            var button = new Rect(textX, group.y + textHeight + 4f, textWidth, addStyle.CalcHeight(addLabel, textWidth));
            return DrawOutlineButton(button, addLabel);
        }

        static bool DrawOutlineButton(Rect rect, GUIContent label)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            bool hover = rect.Contains(Event.current.mousePosition);
            bool pro = EditorGUIUtility.isProSkin;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = pro
                ? WAVESPalette.White(hover ? 0.7f : 0.38f)
                : WAVESPalette.Black(hover ? 0.55f : 0.28f);
            bool pressed = GUI.Button(rect, label, OutlineButtonStyle());
            GUI.backgroundColor = previous;
            return pressed;
        }

        static GUIStyle OutlineButtonStyle()
        {
            bool pro = EditorGUIUtility.isProSkin;
            if (_outlineLabel != null && _outlinePro == pro)
                return _outlineLabel;

            _outlinePro = pro;
            Color text = WAVESPalettePreferences.OutlineText;
            _outlineLabel = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = false,
                padding = new RectOffset(8, 8, 3, 3),
                border = new RectOffset(5, 5, 5, 5)
            };
            Texture2D outline = RoundedOutline();
            _outlineLabel.normal.background = outline;
            _outlineLabel.hover.background = outline;
            _outlineLabel.active.background = outline;
            _outlineLabel.focused.background = outline;
            _outlineLabel.normal.textColor = text;
            _outlineLabel.hover.textColor = text;
            _outlineLabel.active.textColor = text;
            _outlineLabel.focused.textColor = text;
            return _outlineLabel;
        }

        static Texture2D RoundedOutline()
        {
            if (_outline != null)
                return _outline;

            const int size = 16;
            const float radius = 4.5f;
            const float thickness = 1.15f;
            _outline = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(distance) / thickness);
                    pixels[y * size + x] = WAVESPalette.White(alpha);
                }
            }

            _outline.SetPixels(pixels);
            _outline.Apply();
            return _outline;
        }

        static float RoundedDistance(float x, float y, float width, float height, float radius)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float px = Mathf.Abs(x - halfWidth) - (halfWidth - radius);
            float py = Mathf.Abs(y - halfHeight) - (halfHeight - radius);
            float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) + Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
            float inside = Mathf.Min(Mathf.Max(px, py), 0f);
            return outside + inside - radius;
        }

        static void DrawDashedFrame(Rect rect, Color color, float thickness)
        {
            DrawDashedHorizontal(rect.x, rect.y, rect.width, thickness, color);
            DrawDashedHorizontal(rect.x, rect.yMax - thickness, rect.width, thickness, color);
            DrawDashedVertical(rect.x, rect.y, rect.height, thickness, color);
            DrawDashedVertical(rect.xMax - thickness, rect.y, rect.height, thickness, color);
        }

        static void DrawDashedHorizontal(float x, float y, float width, float thickness, Color color)
        {
            float cursor = 0f;
            while (cursor < width)
            {
                float length = Mathf.Min(Dash, width - cursor);
                EditorGUI.DrawRect(new Rect(x + cursor, y, length, thickness), color);
                cursor += Dash + DashGap;
            }
        }

        static void DrawDashedVertical(float x, float y, float height, float thickness, Color color)
        {
            float cursor = 0f;
            while (cursor < height)
            {
                float length = Mathf.Min(Dash, height - cursor);
                EditorGUI.DrawRect(new Rect(x, y + cursor, thickness, length), color);
                cursor += Dash + DashGap;
            }
        }

        internal static bool DrawRemoveButton(Rect rect, string tooltip)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = rect.Contains(Event.current.mousePosition)
                ? MissingColor
                : WAVESPalettePreferences.RemoveIdle;
            bool deleted = GUI.Button(rect, new GUIContent(RemoveIcon(), tooltip), RemoveStyle());
            GUI.contentColor = previous;
            return deleted;
        }

        static GUIStyle RemoveStyle()
        {
            if (_removeStyle != null)
                return _removeStyle;

            _removeStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageOnly,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            return _removeStyle;
        }

        static GUIStyle RackTitleStyle()
        {
            if (_rackTitle != null)
                return _rackTitle;

            _rackTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            Color color = Color.white;
            _rackTitle.normal.textColor = color;
            _rackTitle.hover.textColor = color;
            _rackTitle.active.textColor = color;
            _rackTitle.focused.textColor = color;
            return _rackTitle;
        }

        static GUIStyle RackPresetStyle()
        {
            if (_rackPreset != null)
                return _rackPreset;

            _rackPreset = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            Color color = WAVESPalettePreferences.RackPreset;
            _rackPreset.normal.textColor = color;
            _rackPreset.hover.textColor = color;
            _rackPreset.active.textColor = color;
            _rackPreset.focused.textColor = color;
            return _rackPreset;
        }

        static GUIStyle DropLabelStyle()
        {
            if (_dropLabel != null)
                return _dropLabel;

            _dropLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true
            };
            Color color = WAVESPalettePreferences.DropLabel;
            _dropLabel.normal.textColor = color;
            _dropLabel.hover.textColor = color;
            _dropLabel.active.textColor = color;
            _dropLabel.focused.textColor = color;
            return _dropLabel;
        }

        static Texture2D RowTexture()
        {
            if (_row != null)
                return _row;

            _row = new Texture2D(2, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            _row.SetPixels(new[] { RowLeft, RowRight });
            _row.Apply();
            return _row;
        }

        static Texture2D RemoveIcon()
        {
            if (_removeIcon != null)
                return _removeIcon;

            const int size = 32;
            _removeIcon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            float thickness = 1.6f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x + 0.5f;
                    float v = y + 0.5f;
                    float diagonal = Mathf.Abs(u - v);
                    float cross = Mathf.Abs(u - (size - v));
                    float mark = Mathf.Max(Band(diagonal, thickness), Band(cross, thickness));
                    float inset = Mathf.Min(Mathf.Min(u, v), Mathf.Min(size - u, size - v));
                    if (inset < 6f)
                        mark = 0f;

                    pixels[y * size + x] = WAVESPalette.White(mark);
                }
            }

            _removeIcon.SetPixels(pixels);
            _removeIcon.Apply();
            return _removeIcon;
        }

        static Texture2D CircleTexture()
        {
            if (_circle != null)
                return _circle;

            const int size = 32;
            _circle = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / (radius - 1f);
                    float alpha = Mathf.Clamp01(1f - (distance - 0.82f) / 0.18f);
                    pixels[y * size + x] = WAVESPalette.White(alpha);
                }
            }

            _circle.SetPixels(pixels);
            _circle.Apply();
            return _circle;
        }

        static float Band(float distance, float thickness)
        {
            float edge = 1f - Mathf.Clamp01((distance - thickness) / 1.15f);
            return edge * edge;
        }

        static GUIStyle LinkTitleStyle()
        {
            if (_linkTitle != null)
                return _linkTitle;

            _linkTitle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            return _linkTitle;
        }

        static GUIStyle LinkOnStyle()
        {
            if (_linkOn != null)
                return _linkOn;

            _linkOn = LinkStatusStyle(ConnectedColor);
            return _linkOn;
        }

        static GUIStyle LinkOffStyle()
        {
            if (_linkOff != null)
                return _linkOff;

            _linkOff = LinkStatusStyle(MissingColor);
            return _linkOff;
        }

        static GUIStyle LinkStatusStyle(Color color)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 10,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            return style;
        }
    }
}
